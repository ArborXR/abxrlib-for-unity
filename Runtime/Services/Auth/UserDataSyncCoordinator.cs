using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Owns the SetUserData merge + custom re-auth flow. The auth service exposes the public API and attempt state;
    /// </summary>
    internal sealed class UserDataSyncCoordinator
    {
        private readonly MonoBehaviour _runner;
        private readonly AuthSessionState _sessionState;
        private readonly AuthRequestRunner _authRequestRunner;
        private readonly Func<bool> _isStopping;
        private readonly Func<bool> _isAttemptActive;
        private readonly Action<bool> _setAttemptActive;
        private readonly Action<bool, string> _onCompleted;

        internal UserDataSyncCoordinator(MonoBehaviour runner, AuthSessionState sessionState,
            AuthRequestRunner authRequestRunner, Func<bool> isStopping, Func<bool> isAttemptActive,
            Action<bool> setAttemptActive, Action<bool, string> onCompleted)
        {
            _runner = runner;
            _sessionState = sessionState;
            _authRequestRunner = authRequestRunner;
            _isStopping = isStopping;
            _isAttemptActive = isAttemptActive;
            _setAttemptActive = setAttemptActive;
            _onCompleted = onCompleted;
        }

        internal void SetUserData(string id = null, Dictionary<string, string> additionalUserData = null)
        {
            if (!_sessionState.Authenticated)
            {
                Logcat.Warning("Cannot set user data - not authenticated. Call Authenticate() first.");
                return;
            }

            if (IsStopping() || IsAttemptActive())
            {
                Logcat.Warning("Authentication in progress. Unable to sync user data.");
                return;
            }

            _sessionState.SetUserDataSnapshot(BuildMergedUserData(id, additionalUserData));

            // Reauthenticate to sync with server. Do not fire OnSucceeded/OnAuthCompleted (users think they are just updating user reference).
            // Completion is reported via OnUserDataSyncCompleted only; optional app code can subscribe there.
            _setAttemptActive?.Invoke(true);
            _runner.StartCoroutine(CoSetUserDataReAuth());
        }

        private IEnumerator CoSetUserDataReAuth()
        {
            yield return _authRequestRunner.Run(AuthRequestStage.UserDataSync, FinishUserDataSyncAttempt);
        }

        private void FinishUserDataSyncAttempt(bool success, string errorMessage)
        {
            _setAttemptActive?.Invoke(false);
            _onCompleted?.Invoke(success, errorMessage ?? "");
        }

        private Dictionary<string, string> BuildMergedUserData(string id, Dictionary<string, string> additionalUserData)
        {
            // Build merged user data: start from current response, then apply id (userData.id) and additionalUserData.
            // Do not set session userId (read-only, set by backend).
            var merged = _sessionState.ResponseData?.UserData != null
                ? new Dictionary<string, string>(_sessionState.ResponseData.UserData)
                : new Dictionary<string, string>();

            // Do not send id when it equals the session userId (anonymizedUserId); that would cause the server to hash an already-hashed value.
            string anonymizedUserId = _sessionState.ResponseData?.UserId?.ToString();
            if (!string.IsNullOrEmpty(id) && id != anonymizedUserId)
                merged["id"] = id;

            if (additionalUserData != null)
            {
                foreach (var kvp in additionalUserData)
                    merged[kvp.Key] = kvp.Value;
            }

            return merged;
        }

        private bool IsStopping() => _isStopping != null && _isStopping();
        private bool IsAttemptActive() => _isAttemptActive != null && _isAttemptActive();
    }
}
