using System;
using System.Collections;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Builds and sends auth REST requests.
    /// </summary>
    internal sealed class AuthRequestRunner
    {
        private readonly RuntimeAuthContext _runtimeAuthContext;
        private readonly AuthSessionState _sessionState;
        private readonly IAuthApiClient _authApiClient;
        private readonly Func<AuthMechanism> _getAuthMechanism;
        private readonly Func<bool> _canContinue;
        private readonly Action<string> _onCredentialsRejected;

        internal AuthRequestRunner(RuntimeAuthContext runtimeAuthContext, AuthSessionState sessionState,
            IAuthApiClient authApiClient, Func<AuthMechanism> getAuthMechanism,
            Func<bool> canContinue, Action<string> onCredentialsRejected)
        {
            _runtimeAuthContext = runtimeAuthContext;
            _sessionState = sessionState;
            _authApiClient = authApiClient;
            _getAuthMechanism = getAuthMechanism;
            _canContinue = canContinue;
            _onCredentialsRejected = onCredentialsRejected;
        }

        /// <summary>
        /// Attempts auth via REST. Invokes onComplete(success, errorMessage). Device auth can retry
        /// transport/server transient failures; user-auth and SetUserData re-auth are one-shot.
        /// </summary>
        internal IEnumerator Run(AuthRequestStage stage, Action<bool, string> onComplete,
            string submittedAuthPrompt = null, string submittedInputSource = null)
        {
            onComplete ??= (_, _) => { };

            if (!CanContinue()) { onComplete(false, null); yield break; }
            if (_authApiClient == null) { onComplete(false, "Auth API client not set"); yield break; }

            var payload = _runtimeAuthContext.Payload;
            if (string.IsNullOrEmpty(payload.sessionId)) payload.sessionId = Guid.NewGuid().ToString();

            AuthMechanism sessionAuthMechanism = _getAuthMechanism?.Invoke();
            var requestPayload = AuthRequestBuilder.BuildPayload(
                payload, stage, sessionAuthMechanism, _sessionState.UserDataSnapshot, submittedAuthPrompt, submittedInputSource);
            var validationError = _runtimeAuthContext.PreparePayloadForAuth(requestPayload);
            if (validationError != null) { onComplete(false, validationError); yield break; }

            int retryIntervalSeconds = Math.Max(1, Configuration.Instance.sendRetryIntervalSeconds);
            int maxRetries = Math.Max(0, Configuration.Instance.sendRetriesOnFailure);
            int retriesAttempted = 0;

            while (true)
            {
                if (!CanContinue()) { onComplete(false, null); yield break; }

                string stageLabel = AuthRequestBuilder.GetStageLabel(stage);
                LogRequest(stageLabel, requestPayload, sessionAuthMechanism, submittedAuthPrompt);

                RestAuthResult result = null;
                yield return _authApiClient.AuthRequestCoroutine(requestPayload, r => result = r);

                if (result != null && result.Success && _sessionState.TryApply(result.Response, stageLabel))
                {
                    onComplete(true, null);
                    yield break;
                }

                string message = DescribeAuthFailure(result);

                if (stage != AuthRequestStage.Device)
                {
                    onComplete(false, message);
                    yield break;
                }

                if (result != null && result.AuthRejected)
                {
                    _onCredentialsRejected?.Invoke(message);
                    Logcat.Warning($"AuthRequest failed: {message} No further auth attempts will be made this session.");
                    onComplete(false, message);
                    yield break;
                }

                if (result == null || !result.Retryable)
                {
                    onComplete(false, message);
                    yield break;
                }

                if (retriesAttempted >= maxRetries)
                {
                    onComplete(false, message);
                    yield break;
                }

                retriesAttempted++;
                Logcat.Warning($"AuthRequest failed: {message} Retrying in {retryIntervalSeconds} seconds...");
                yield return new WaitForSeconds(retryIntervalSeconds);
            }
        }

        private bool CanContinue() => _canContinue == null || _canContinue();

        private static void LogRequest(string stageLabel, AuthPayload requestPayload,
            AuthMechanism sessionAuthMechanism, string submittedAuthPrompt)
        {
            if (requestPayload?.authMechanism != null)
            {
                string authMechLog = AuthRequestBuilder.FormatAuthMechanismForLog(requestPayload.authMechanism);
                string configuredPrompt = sessionAuthMechanism?.prompt ?? "(null)";
                Logcat.Debug($"Auth request ({stageLabel}): authMechanism=[{authMechLog}], " +
                             $"configuredPrompt={configuredPrompt} (submittedPromptLength={submittedAuthPrompt?.Length ?? 0})");
            }
            else
            {
                Logcat.Debug($"Auth request ({stageLabel}): no auth_mechanism");
            }
        }

        private static string DescribeAuthFailure(RestAuthResult result)
        {
            if (result == null) return "Authentication request failed.";
            return AuthResponseParser.DescribeFailure(result.Body, result.StatusCode);
        }
    }
}
