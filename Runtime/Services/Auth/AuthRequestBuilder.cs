using System.Collections.Generic;
using System.Linq;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    internal static class AuthRequestBuilder
    {
        private const string TypeKey = "type";
        private const string PromptKey = "prompt";
        private const string InputSourceKey = "inputSource";
        private const string CustomAuthType = "custom";

        internal static AuthPayload BuildPayload(AuthPayload sessionPayload, AuthRequestStage stage,
            AuthMechanism sessionAuthMechanism, IReadOnlyDictionary<string, string> userDataSnapshot,
            string submittedAuthPrompt = null, string submittedInputSource = null)
        {
            if (sessionPayload == null) return null;

            var authMechanism = BuildRequestAuthMechanism(
                stage, sessionAuthMechanism, userDataSnapshot, submittedAuthPrompt, submittedInputSource);

            return sessionPayload.CopyForRequest(authMechanism);
        }

        internal static Dictionary<string, string> BuildRequestAuthMechanism(AuthRequestStage stage,
            AuthMechanism sessionAuthMechanism, IReadOnlyDictionary<string, string> userDataSnapshot,
            string submittedAuthPrompt = null, string submittedInputSource = null)
        {
            var authMechanism = CreateAuthMechanismDict(
                stage, sessionAuthMechanism, userDataSnapshot, submittedAuthPrompt, submittedInputSource);

            // Device authentication never sends authMechanism. User auth and SetUserData sync send only explicit supported request shapes.
            return stage == AuthRequestStage.Device
                ? null
                : AuthMechanismResolver.IsRequestMeaningful(authMechanism) ? authMechanism : null;
        }

        internal static string BuildSubmittedAuthPrompt(AuthMechanism sessionAuthMechanism, string input)
        {
            // Server does not use domain from payload. Domain is client-only for prompting and building this value.
            if (sessionAuthMechanism != null && sessionAuthMechanism.type == AuthMechanismResolver.Email &&
                !string.IsNullOrEmpty(sessionAuthMechanism.domain) && input != null && !input.Contains("@"))
            {
                return input + "@" + sessionAuthMechanism.domain;
            }

            return input;
        }

        internal static string FormatAuthMechanismForLog(Dictionary<string, string> authMechanism)
        {
            return authMechanism == null
                ? ""
                : string.Join(", ", authMechanism.Select(kvp => kvp.Key + "=" + (string.IsNullOrEmpty(kvp.Value) ? "(empty)" : kvp.Value)));
        }

        internal static string GetStageLabel(AuthRequestStage stage) => stage switch
        {
            AuthRequestStage.Device => "device-auth",
            AuthRequestStage.UserInput => "user-auth",
            AuthRequestStage.UserDataSync => "user-data-sync",
            _ => "auth"
        };

        private static Dictionary<string, string> CreateAuthMechanismDict(AuthRequestStage stage,
            AuthMechanism sessionAuthMechanism, IReadOnlyDictionary<string, string> userDataSnapshot,
            string submittedAuthPrompt = null, string submittedInputSource = null)
        {
            var dict = new Dictionary<string, string>();
            if (stage == AuthRequestStage.Device) return dict;

            if (stage == AuthRequestStage.UserDataSync)
            {
                // SetUserData sync is the only client-originated custom auth path.
                dict[TypeKey] = CustomAuthType;
                dict[InputSourceKey] = AuthMechanismResolver.UserInputSource;
                AddUserDataSnapshot(dict, userDataSnapshot);
                return dict;
            }

            // User-input auth supports only the backend-defined types returned by config.
            if (stage != AuthRequestStage.UserInput || !AuthMechanismResolver.NeedsUserAuthentication(sessionAuthMechanism))
                return dict;

            dict[TypeKey] = sessionAuthMechanism.type;
            dict[PromptKey] = submittedAuthPrompt ?? sessionAuthMechanism.prompt ?? "";

            string requestInputSource = AuthMechanismResolver.NormalizeInputSource(submittedInputSource);
            if (!string.IsNullOrEmpty(requestInputSource)) dict[InputSourceKey] = requestInputSource;
            return dict;
        }

        private static void AddUserDataSnapshot(Dictionary<string, string> destination,
            IReadOnlyDictionary<string, string> userDataSnapshot)
        {
            if (userDataSnapshot == null) return;

            foreach (var item in userDataSnapshot)
            {
                if (!IsReservedAuthMechanismKey(item.Key)) destination[item.Key] = item.Value;
            }
        }

        private static bool IsReservedAuthMechanismKey(string key) =>
            key == TypeKey || key == PromptKey || key == InputSourceKey;
    }
}
