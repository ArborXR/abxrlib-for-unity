using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Normalizes backend-provided auth mechanisms into the small set of user-auth shapes
    /// AbxrAuthService knows how to prompt for and submit.
    /// </summary>
    internal static class AuthMechanismResolver
    {
        internal const string AssessmentPin = "assessmentPin";
        internal const string Email = "email";
        internal const string Text = "text";
        internal const string UserInputSource = "user";
        internal const string QrLmsInputSource = "QRlms";

        internal static string NormalizeInputSource(string inputSource) =>
            string.IsNullOrWhiteSpace(inputSource) ? UserInputSource : inputSource.Trim();

        /// <summary>Returns a mutable, normalized copy of a supported user-auth mechanism, or null when user auth is not required.</summary>
        internal static AuthMechanism CopyForSession(AuthMechanism source)
        {
            if (source == null) return null;

            string type = (source.type ?? "").Trim();
            if (string.IsNullOrEmpty(type) || string.Equals(type, "none", StringComparison.OrdinalIgnoreCase))
                return null;

            string normalizedType = NormalizeUserAuthType(type);
            if (string.IsNullOrEmpty(normalizedType))
            {
                Logcat.Warning($"Unsupported authMechanism.type '{type}' from configuration; continuing without user authentication.");
                return null;
            }

            return new AuthMechanism
            {
                type = normalizedType,
                prompt = source.prompt ?? "",
                domain = source.domain ?? "",
                inputSource = NormalizeInputSource(source.inputSource),
                allowGuest = source.allowGuest
            };
        }

        /// <summary>
        /// Applies GET-config auth-mechanism rules.
        /// Learner Launcher Mode always requires an assessment PIN, even when the backend config says no user auth is required.
        /// </summary>
        internal static AuthMechanism ResolveConfigMechanism(AuthMechanism configMechanism, bool learnerLauncherModeEnabled)
        {
            var resolved = CopyForSession(configMechanism);
            if (learnerLauncherModeEnabled && !IsType(resolved, AssessmentPin))
                return ForceAssessmentPin(configMechanism);

            return resolved;
        }

        /// <summary>Forces an auth mechanism to assessmentPin while preserving prompt/domain when present.</summary>
        internal static AuthMechanism ForceAssessmentPin(AuthMechanism source)
        {
            return new AuthMechanism
            {
                type = AssessmentPin,
                prompt = source?.prompt ?? "",
                domain = source?.domain ?? "",
                inputSource = NormalizeInputSource(source?.inputSource),
                allowGuest = source?.allowGuest
            };
        }

        internal static bool NeedsUserAuthentication(AuthMechanism mechanism) =>
            mechanism != null && IsSupportedUserAuthType(mechanism.type);

        internal static bool IsType(AuthMechanism mechanism, string type) =>
            mechanism != null && string.Equals(mechanism.type, type, StringComparison.OrdinalIgnoreCase);

        internal static bool IsRequestMeaningful(Dictionary<string, string> dict) =>
            dict != null && dict.TryGetValue("type", out var type) && !string.IsNullOrEmpty(type);

        internal static string NormalizeUserAuthType(string type)
        {
            if (string.Equals(type, AssessmentPin, StringComparison.OrdinalIgnoreCase)) return AssessmentPin;
            if (string.Equals(type, Email, StringComparison.OrdinalIgnoreCase)) return Email;
            if (string.Equals(type, Text, StringComparison.OrdinalIgnoreCase)) return Text;
            return null;
        }

        private static bool IsSupportedUserAuthType(string type) => !string.IsNullOrEmpty(NormalizeUserAuthType(type));
    }
}
