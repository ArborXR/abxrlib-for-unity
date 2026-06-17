using System;
using System.Collections.Generic;
using System.Linq;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Owns the authenticated session response, token expiry, and local user-data snapshot.
    /// </summary>
    internal sealed class AuthSessionState
    {
        internal bool Authenticated { get; private set; }
        internal AuthResponse ResponseData { get; private set; } = new();
        internal DateTime TokenExpiryUtc { get; private set; } = DateTime.MinValue;
        internal Dictionary<string, string> UserDataSnapshot { get; private set; }

        internal void Clear()
        {
            Authenticated = false;
            ResponseData = new AuthResponse();
            TokenExpiryUtc = DateTime.MinValue;
            UserDataSnapshot = null;
        }

        internal void MarkAuthenticated() => Authenticated = true;

        internal bool MarkAuthenticatedAndMergeSsoUserData(bool ssoUserDataAlreadyMerged, string accessToken)
        {
            MarkAuthenticated();
            bool userDataChanged = false;
            if (ResponseData != null)
            {
                EnsureResponseUserData();
                if (ssoUserDataAlreadyMerged)
                {
                    userDataChanged = true;
                    RefreshUserDataSnapshotFromResponse();
                }
                else
                {
                    userDataChanged = TryMergeAccessTokenIntoUserData(accessToken);
                }
            }

            return userDataChanged;
        }

        internal void SetAuthenticated(bool value) => Authenticated = value;

        internal void SetResponseData(AuthResponse response)
        {
            ResponseData = response;
            RefreshUserDataSnapshotFromResponse();
        }

        internal void SetUserDataSnapshot(Dictionary<string, string> userData) =>
            UserDataSnapshot = userData != null ? new Dictionary<string, string>(userData) : null;

        private bool EnsureResponseUserData()
        {
            if (ResponseData == null) return false;
            ResponseData.UserData ??= new Dictionary<string, string>();
            return true;
        }

        private void RefreshUserDataSnapshotFromResponse() => SetUserDataSnapshot(ResponseData?.UserData);

        /// <summary>
        /// Parses and applies an auth response. REST auth success requires both token and secret.
        /// </summary>
        internal bool TryApply(string responseText, string stageLabel = null)
        {
            if (!AuthResponseParser.TryParseSuccess(responseText, out AuthResponse postResponse, out string parseError))
            {
                if (AuthResponseParser.IsParseFailure(parseError))
                {
                    Logcat.Error($"Authentication response handling failed: {parseError}");
                }

                return false;
            }

            return TryApply(postResponse, stageLabel);
        }

        /// <summary>
        /// Applies an already-parsed auth response. REST transport parses normal responses before invoking
        /// this method, so successful REST auth does not deserialize the same body twice.
        /// </summary>
        internal bool TryApply(AuthResponse postResponse, string stageLabel = null)
        {
            if (!AuthResponse.IsValidSuccess(postResponse)) return false;

            try
            {
                if (!TryDecodeTokenExpiryFromJwt(postResponse.Token, out DateTime tokenExpiryUtc)) return false;

                if (postResponse.Modules?.Count > 1)
                    postResponse.Modules = postResponse.Modules.OrderBy(m => m.Order).ToList();
                postResponse.UserData ??= new Dictionary<string, string>();

                ResponseData = postResponse;
                TokenExpiryUtc = tokenExpiryUtc;
                RefreshUserDataSnapshotFromResponse();

                LogAppliedResponse(postResponse, stageLabel);
                return true;
            }
            catch (Exception ex)
            {
                Logcat.Error($"Authentication response handling failed: {ex.Message}");
                return false;
            }
        }

        internal bool TrySetTokenExpiryFromJwt(string token)
        {
            if (!TryDecodeTokenExpiryFromJwt(token, out DateTime tokenExpiryUtc)) return false;
            SetTokenExpiryUtc(tokenExpiryUtc);
            return true;
        }

        internal void SetTokenExpiryUtc(DateTime tokenExpiryUtc) => TokenExpiryUtc = tokenExpiryUtc;

        internal bool TryMergeAccessTokenIntoUserData(string accessToken)
        {
            if (!EnsureResponseUserData()) return false;

            bool changed = SsoUserDataMerger.TryMergeAccessTokenIntoUserData(accessToken, ResponseData.UserData);
            RefreshUserDataSnapshotFromResponse();
            return changed;
        }

        private static bool TryDecodeTokenExpiryFromJwt(string token, out DateTime tokenExpiryUtc)
        {
            tokenExpiryUtc = DateTime.MinValue;

            Dictionary<string, object> decodedJwt = Utils.DecodeJwt(token);
            if (decodedJwt == null)
            {
                Logcat.Error("Failed to decode JWT token");
                return false;
            }
            if (!decodedJwt.TryGetValue("exp", out var expValue) || expValue == null)
            {
                Logcat.Error("JWT token missing expiration field");
                return false;
            }
            try
            {
                tokenExpiryUtc = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(expValue)).UtcDateTime;
                return true;
            }
            catch (Exception ex)
            {
                Logcat.Error($"Invalid JWT token expiration: {ex.Message}");
                return false;
            }
        }

        private static void LogAppliedResponse(AuthResponse response, string stageLabel)
        {
            string stagePrefix = !string.IsNullOrEmpty(stageLabel) ? $" ({stageLabel})" : "";
            var userDataLog = response.UserData == null
                ? "(null)"
                : string.Join(", ", response.UserData.Select(kvp => kvp.Key + "=" + kvp.Value));
            Logcat.Debug($"Auth response{stagePrefix}: userId={response.UserId ?? "(null)"}, userData=[{userDataLog}], " +
                         $"token={(!string.IsNullOrEmpty(response.Token) ? "present" : "(null)")}, " +
                         $"appId={response.AppId ?? "(null)"}, modules={response.Modules?.Count ?? 0}");
        }
    }
}
