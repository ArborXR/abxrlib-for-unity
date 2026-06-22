using System;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Owns auth_handoff discovery and per-session handoff state.
    /// </summary>
    internal sealed class AuthHandoffCoordinator
    {
        private readonly IAuthPlatformSource _platformSource;

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Lets PlayMode tests exercise the receiver path without mutating process command-line args or Android intents.
        /// </summary>
        internal static string TestPayload { get; set; }
#endif

        internal bool SessionUsedHandoff { get; private set; }
        private string _returnToPackage;

        internal AuthHandoffCoordinator(IAuthPlatformSource platformSource) =>
            _platformSource = platformSource ?? UnityAuthPlatformSource.Instance;

        internal void Clear()
        {
            SessionUsedHandoff = false;
            _returnToPackage = null;
        }

        /// <summary>
        /// Looks for auth_handoff in Android intent extras, command-line args, WebGL query params, or test override.
        /// </summary>
        internal bool TryReadIncomingPayload(out string normalizedPayload)
        {
            normalizedPayload = null;

            string handoffPayload = _platformSource.GetAndroidIntentParam("auth_handoff");
            if (string.IsNullOrEmpty(handoffPayload))
                handoffPayload = _platformSource.GetCommandLineArg("auth_handoff");
            if (string.IsNullOrEmpty(handoffPayload) && _platformSource.IsWebGlPlayer)
                handoffPayload = Utils.GetQueryParam("auth_handoff", _platformSource.AbsoluteUrl ?? "");
#if UNITY_INCLUDE_TESTS
            string testHandoffPayload = TestPayload;
            TestPayload = null;

            if (string.IsNullOrEmpty(handoffPayload)) handoffPayload = testHandoffPayload;
#endif
            if (string.IsNullOrEmpty(handoffPayload)) return false;

            normalizedPayload = AuthHandoffPayload.Normalize(handoffPayload);
            if (!string.IsNullOrEmpty(normalizedPayload)) return true;

            Logcat.Warning("auth_handoff was present but could not be normalized to JSON; continuing with device authentication.");
            return false;
        }

        internal void MarkApplied(AuthResponse response)
        {
            Logcat.Info($"Auth handoff applied. Modules: {response?.Modules?.Count ?? 0}");
            SessionUsedHandoff = true;
            _returnToPackage = response?.ReturnToPackage;
        }

        internal static string BuildOutgoingPayload(AuthResponse response, AuthPayload payload,
            DateTime tokenExpiryUtc, bool authenticated, string returnToPackage = null)
        {
            if (response == null || !authenticated) return null;

            return AuthHandoffPayload.Build(response, payload, tokenExpiryUtc, returnToPackage);
        }

        internal string GetAndClearReturnToPackage()
        {
            var value = _returnToPackage;
            _returnToPackage = null;
            return value;
        }
    }
}
