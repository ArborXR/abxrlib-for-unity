using System.Text.RegularExpressions;

namespace AbxrLib.Runtime.Types
{
    /// <summary>
    /// Runtime auth configuration: auth-related values copied from Configuration and updated by GetArborData, GetQueryData, intent, and Abxr.SetOrgId/SetAuthSecret/SetDeviceId.
    /// Validated via IsValid() before building the auth request; does not touch the Configuration asset.
    /// </summary>
    public class RuntimeAuthConfig
    {
        /// <summary>Resolved runtime value copied from Configuration after defaults and GET-config merges are applied.</summary>
        public bool enableAutoStartAuthentication = true;
        /// <summary>Resolved runtime value copied from Configuration after defaults and GET-config merges are applied.</summary>
        public bool enableReturnTo = true;
        /// <summary>Resolved runtime value copied from Configuration after defaults and GET-config merges are applied.</summary>
        public bool enableAutoStartModules = true;
        /// <summary>Resolved runtime value copied from Configuration after defaults and GET-config merges are applied.</summary>
        public bool enableAutoAdvanceModules = true;

        public bool useAppTokens;
        public string appToken;
        public string orgToken;
        public string appId;
        public string orgId;
        public string authSecret;
        public string buildType;
        /// <summary>Device id from subsystem (GetDeviceId) or MDM when connected.</summary>
        public string deviceId;
        /// <summary>Partner identifier; "none" when not from MDM, "arborxr" when from ArborMdmClient.</summary>
        public string partner;
        /// <summary>Device tags from MDM when connected; otherwise null/empty.</summary>
        public string[] tags;

        /// <summary>Auth mechanism (type, prompt, domain). When null or empty type, filled from GET config when received.</summary>
        public AuthMechanism authMechanism;

        /// <summary>
        /// Validates the current runtime auth values. Returns null if valid, or an error message if invalid.
        /// Call after loading from Configuration and applying GetArborData/GetQueryData/overrides.
        /// </summary>
        public string IsValid()
        {
            return ValidateAuthFields(useAppTokens, buildType, appId, orgId, authSecret, appToken, orgToken);
        }

        /// <summary>
        /// Call when about to send an auth request. Runs IsValid(), then enforces that credentials are complete (orgToken for app tokens, orgId/authSecret for legacy) so we never send without them. Use this after GetArborData/overrides have run; IsValid() alone allows empty org for non-production_custom because Configuration asset validation does not require them.
        /// </summary>
        public string IsValidToSend()
        {
            var err = IsValid();
            if (err != null) return err;
            if (useAppTokens && string.IsNullOrEmpty(orgToken))
                return "Organization identification unavailable.";
            if (!useAppTokens && (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(authSecret)))
                return "Organization identification unavailable.";
            return null;
        }

        /// <summary>
        /// Shared auth-field validation used by both Configuration.IsValid() and RuntimeAuthConfig.IsValid().
        /// Returns null if valid, or a short error message (e.g. "App identification not set."). Configuration prefixes with "Authentication error: " when setting LastValidationErrorMessage.
        /// </summary>
        public static string ValidateAuthFields(bool useAppTokens, string buildType, string appId, string orgId, string authSecret, string appToken, string orgToken)
        {
            if (useAppTokens)
            {
                if (string.IsNullOrEmpty(appToken))
                    return "App identification not set.";
                if (!LooksLikeJwt(appToken))
                    return "App identification not set.";
                if (buildType == "production_custom")
                {
                    if (string.IsNullOrEmpty(orgToken))
                        return "Organization identification unavailable.";
                    if (!LooksLikeJwt(orgToken))
                        return "Organization identification unavailable.";
                }
                else if (!string.IsNullOrEmpty(orgToken) && !LooksLikeJwt(orgToken))
                    return "Organization identification unavailable.";
            }
            else
            {
                if (string.IsNullOrEmpty(appId))
                    return "App identification not set.";
                if (!LooksLikeUuid(appId))
                    return "App identification not set.";
                if (buildType == "production_custom")
                {
                    if (string.IsNullOrEmpty(orgId))
                        return "Organization identification unavailable.";
                    if (string.IsNullOrEmpty(authSecret) || string.IsNullOrWhiteSpace(authSecret))
                        return "Organization identification unavailable.";
                }
                if (!string.IsNullOrEmpty(orgId) && !LooksLikeUuid(orgId))
                    return "Organization identification unavailable.";
                if (!string.IsNullOrEmpty(authSecret) && string.IsNullOrWhiteSpace(authSecret))
                    return "Organization identification unavailable.";
            }

            return null;
        }

        private static bool LooksLikeUuid(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            const string uuidPattern = "^[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}$";
            return Regex.IsMatch(value, uuidPattern);
        }

        private static bool LooksLikeJwt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('.');
            return parts.Length == 3;
        }

        /// <summary>
        /// Copy auth and device/partner fields from this runtime config into the given payload. Only sets the auth fields appropriate for the current mode (useAppTokens vs legacy); the other mode's fields are cleared.
        /// </summary>
        public void CopyAuthFieldsTo(AuthPayload payload)
        {
            if (payload == null) return;
            payload.deviceId = deviceId;
            payload.partner = partner ?? "none";
            payload.tags = tags;
            if (useAppTokens)
            {
                payload.appToken = appToken;
                payload.orgToken = orgToken;
                payload.appId = null;
                payload.orgId = null;
                payload.authSecret = null;
            }
            else
            {
                payload.appId = appId;
                payload.orgId = orgId;
                payload.authSecret = authSecret;
                payload.appToken = null;
                payload.orgToken = null;
            }
        }

        /// <summary>
        /// Validate the auth fields required for a backend auth request, then copy the single selected credential mode into the payload.
        /// Returns null when the payload is ready to send, or a user-facing validation error otherwise.
        /// </summary>
        public string PreparePayloadForAuth(AuthPayload payload)
        {
            var err = IsValidToSend();
            if (err != null) return err;
            CopyAuthFieldsTo(payload);
            return null;
        }
    }
}
