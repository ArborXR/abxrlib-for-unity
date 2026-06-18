using System;
using System.Collections;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Loads the post-auth runtime configuration and resolves the session auth mechanism returned by config.
    /// </summary>
    internal sealed class AuthConfigurationLoader
    {
        private readonly IAuthApiClient _authApiClient;
        private readonly RuntimeAuthContext _runtimeAuthContext;
        private readonly AuthSessionState _sessionState;
        private readonly Func<bool> _canContinue;

        internal AuthConfigurationLoader(IAuthApiClient authApiClient, RuntimeAuthContext runtimeAuthContext,
            AuthSessionState sessionState, Func<bool> canContinue)
        {
            _authApiClient = authApiClient;
            _runtimeAuthContext = runtimeAuthContext;
            _sessionState = sessionState;
            _canContinue = canContinue;
        }

        internal IEnumerator Load(Action<bool, string, AuthMechanism> onComplete)
        {
            onComplete ??= (_, _, _) => { };

            if (!CanContinue()) { onComplete(false, null, null); yield break; }
            if (_authApiClient == null) { onComplete(false, "Auth API client not set", null); yield break; }

            string configJson = null;
            string failureDetail = null;
            yield return _authApiClient.GetConfigCoroutine(_sessionState.ResponseData, (ok, json) =>
            {
                if (ok) configJson = json; else failureDetail = json;
            });

            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<ConfigPayload>(configJson);
                    if (config != null)
                    {
                        Configuration.Instance.ApplyConfigPayload(config);

                        AuthMechanism authMechanism = _runtimeAuthContext.ResolveConfigAuthMechanism(
                            config.authMechanism, Configuration.Instance.enableLearnerLauncherMode);
                        LogResolvedAuthMechanism(authMechanism);
                        onComplete(true, null, authMechanism);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    failureDetail = ex.Message;
                    Logcat.Error($"GetConfiguration response handling failed: {ex.Message}");
                }
            }

            onComplete(false, failureDetail ?? "no config returned", null);
        }

        private bool CanContinue() => _canContinue == null || _canContinue();

        private static void LogResolvedAuthMechanism(AuthMechanism authMechanism)
        {
            if (AuthMechanismResolver.NeedsUserAuthentication(authMechanism))
            {
                string authType = authMechanism?.type ?? "";
                Logcat.Info("User Authentication Required.");
                Logcat.Debug($" - Type: {authType} & Prompt: {(authMechanism?.prompt ?? "")}");
            }
            else
            {
                Logcat.Info("User authentication not required. Using anonymous session.");
            }
        }
    }
}
