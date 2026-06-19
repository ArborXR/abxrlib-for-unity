using System;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Resolves runtime authentication state from Configuration, platform sources, MDM data, URL/query data, and runtime overrides.
    /// </summary>
    internal sealed class RuntimeAuthResolver
    {
        private const string ProductionBuildType = "production";
        private const string ProductionCustomBuildType = "production_custom";
        private const string PartnerNone = "none";
        private const string PartnerArborXr = "arborxr";

        private readonly RuntimeAuthContext _context;
        private readonly ArborMdmClient _arborMdmClient;
        private readonly IAuthPlatformSource _platformSource;

        private AuthPayload Payload => _context.Payload;
        private RuntimeAuthConfig RuntimeAuth => _context.RuntimeAuth;

        internal RuntimeAuthResolver(RuntimeAuthContext context, ArborMdmClient arborMdmClient, IAuthPlatformSource platformSource)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _arborMdmClient = arborMdmClient;
            _platformSource = platformSource ?? UnityAuthPlatformSource.Instance;
        }

        internal void LoadStartupData()
        {
            LoadConfigData();
            if (_platformSource.IsAndroidPlayer)
            {
                ApplyArborMdmData();
                ApplyAndroidIntentOrgTokenIfAvailable();
            }
            else if (_platformSource.IsWebGlPlayer)
            {
                ApplyWebGlQueryData();
                ApplyWebGlDeviceIdFromPlatform();
            }
            else if (_platformSource.IsStandalonePlayer)
            {
                ApplyDesktopQueryData();
            }

            ApplySessionData();
        }

        /// <summary>
        /// Reloads configuration and platform auth inputs immediately before an auth attempt.
        /// Returns null when the payload is ready to send, or a user-facing validation error otherwise.
        /// </summary>
        internal string PrepareForAuthentication()
        {
            LoadRuntimeAuthFromConfig();
            if (_platformSource.IsAndroidPlayer) ApplyArborMdmData();

            ApplyAbxrOverridesToRuntimeAuth();

            // When app-token auth has orgId/authSecret but not an orgToken, build a dynamic org token from
            // Abxr overrides or MDM data.
            TrySetDynamicOrgToken(RuntimeAuth.OrgId, RuntimeAuth.AuthSecret);

            if (_platformSource.IsAndroidPlayer) ApplyAndroidIntentOrgTokenIfAvailable(copyToPayload: false);
            else if (_platformSource.IsWebGlPlayer) ApplyWebGlQueryData();
            else if (_platformSource.IsStandalonePlayer) ApplyDesktopQueryData();

            return _context.PreparePayloadForAuth();
        }

        /// <summary>Loads auth-related values from Configuration into RuntimeAuth.</summary>
        private void LoadRuntimeAuthFromConfig()
        {
            var s = Configuration.Instance;
            RuntimeAuth.UseAppTokens = s.useAppTokens;
            RuntimeAuth.BuildType = !string.IsNullOrEmpty(s.buildType) ? s.buildType : ProductionBuildType;
            if (s.useAppTokens)
            {
                RuntimeAuth.AppToken = s.appToken;
                RuntimeAuth.OrgToken = string.Equals(s.buildType, ProductionBuildType, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : s.orgToken;
            }
            else
            {
                RuntimeAuth.AppId = s.appID;
                if (string.Equals(s.buildType, ProductionBuildType, StringComparison.OrdinalIgnoreCase))
                {
                    RuntimeAuth.OrgId = null;
                    RuntimeAuth.AuthSecret = null;
                }
                else
                {
                    RuntimeAuth.OrgId = s.orgID;
                    RuntimeAuth.AuthSecret = s.authSecret;
                }
            }

            SetDefaultRuntimeDeviceContext();
        }

        private void SetDefaultRuntimeDeviceContext()
        {
            string deviceIdFromSubsystem = _platformSource.GetCurrentDeviceId();
            RuntimeAuth.DeviceId = !string.IsNullOrEmpty(deviceIdFromSubsystem) ? deviceIdFromSubsystem : Payload.deviceId;
            RuntimeAuth.Partner = PartnerNone;
            RuntimeAuth.Tags = null;
        }

        private void ApplyWebGlDeviceIdFromPlatform()
        {
            string webGlDeviceId = _platformSource.GetOrCreateWebGlDeviceId();
            if (string.IsNullOrEmpty(webGlDeviceId)) return;

            Payload.deviceId = webGlDeviceId;
            RuntimeAuth.DeviceId = webGlDeviceId;
        }

        private bool TrySetDynamicOrgToken(string orgId, string authSecret, bool overwriteExisting = false)
        {
            if (!RuntimeAuth.UseAppTokens) return false;
            if (!overwriteExisting && !string.IsNullOrEmpty(RuntimeAuth.OrgToken)) return false;
            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(authSecret)) return false;

            string dynamicToken = Utils.BuildOrgTokenDynamic(orgId, authSecret);
            if (string.IsNullOrEmpty(dynamicToken)) return false;

            RuntimeAuth.OrgToken = dynamicToken;
            return true;
        }

        private void LoadConfigData()
        {
            var config = Configuration.Instance;
            _context.ApplyRuntimeFlagsFromConfiguration(config);

            var configData = Utils.ExtractConfigData(config);
            if (!configData.isValid) return;

            SetDefaultRuntimeDeviceContext();

            RuntimeAuth.UseAppTokens = configData.useAppTokens;
            RuntimeAuth.BuildType = configData.buildType ?? ProductionBuildType;
            if (configData.useAppTokens)
            {
                RuntimeAuth.AppToken = configData.appToken;
                RuntimeAuth.OrgToken = configData.orgToken;
            }
            else
            {
                RuntimeAuth.AppId = configData.appId;
                RuntimeAuth.OrgId = configData.orgId;
                RuntimeAuth.AuthSecret = configData.authSecret;
            }

            RuntimeAuth.CopyAuthFieldsTo(Payload);
        }

        /// <summary>Applies an Android org_token intent extra when app-token auth has no org token yet.</summary>
        private void ApplyAndroidIntentOrgTokenIfAvailable(bool copyToPayload = true)
        {
            if (!RuntimeAuth.UseAppTokens || !string.IsNullOrEmpty(RuntimeAuth.OrgToken)) return;

            string orgTokenIntent = _platformSource.GetAndroidIntentParam("org_token");
            if (string.IsNullOrEmpty(orgTokenIntent)) return;

            RuntimeAuth.OrgToken = orgTokenIntent;
            if (copyToPayload) RuntimeAuth.CopyAuthFieldsTo(Payload);
        }

        /// <summary>
        /// When Arbor MDM is available and connected: updates deviceId, partner, tags from MDM;
        /// for production_custom that is all we accept (org credentials stay from config).
        /// For other build types, updates orgToken (app tokens) or orgId/authSecret (legacy) from MDM.
        /// </summary>
        private void ApplyArborMdmData()
        {
            if (!_platformSource.IsArborMdmConnected(_arborMdmClient)) return;

            // MDM available: always accept deviceId, partner, tags from Arbor.
            RuntimeAuth.Partner = PartnerArborXr;
            RuntimeAuth.DeviceId = _platformSource.GetCurrentDeviceId();
            RuntimeAuth.Tags = _platformSource.GetCurrentDeviceTags();

            // production_custom: only deviceId/partner/tags from MDM; org credentials stay from config.
            if (RuntimeAuth.BuildType == ProductionCustomBuildType)
            {
                RuntimeAuth.CopyAuthFieldsTo(Payload);
                return;
            }

            // Non-production_custom: update auth from MDM (dynamic org token or orgId/authSecret).
            if (RuntimeAuth.UseAppTokens)
            {
                TrySetDynamicOrgToken(_platformSource.GetCurrentOrgId(), _platformSource.GetCurrentFingerprint(), overwriteExisting: true);
            }
            else
            {
                RuntimeAuth.OrgId = _platformSource.GetCurrentOrgId();
                RuntimeAuth.AuthSecret = _platformSource.GetCurrentFingerprint();
            }

            RuntimeAuth.CopyAuthFieldsTo(Payload);
        }

        private void ApplyWebGlQueryData()
        {
            if (RuntimeAuth.BuildType == ProductionCustomBuildType) return;

            string absoluteUrl = _platformSource.AbsoluteUrl ?? "";
            string orgTokenQuery = Utils.GetQueryParam("org_token", absoluteUrl);
            if (!string.IsNullOrEmpty(orgTokenQuery))
            {
                RuntimeAuth.OrgToken = orgTokenQuery;
                RuntimeAuth.CopyAuthFieldsTo(Payload);
            }

            _context.SetWebGlAssessmentPin(null);
            string pinFromOrgJwt = TryGetAssessmentPinFromOrgTokenPayload(RuntimeAuth.OrgToken);
            if (!string.IsNullOrEmpty(pinFromOrgJwt))
            {
                _context.SetWebGlAssessmentPin(pinFromOrgJwt);
                return;
            }

            string pinQuery = Utils.GetQueryParam("assessment_pin", absoluteUrl);
            if (string.IsNullOrEmpty(pinQuery)) pinQuery = Utils.GetQueryParam("assessmentPin", absoluteUrl);
            if (!string.IsNullOrWhiteSpace(pinQuery)) _context.SetWebGlAssessmentPin(pinQuery.Trim());
        }

        private void ApplyDesktopQueryData()
        {
            if (RuntimeAuth.BuildType == ProductionCustomBuildType) return;

            string orgToken = _platformSource.GetDesktopOrgToken();
            if (!string.IsNullOrEmpty(orgToken))
            {
                RuntimeAuth.OrgToken = orgToken;
                RuntimeAuth.CopyAuthFieldsTo(Payload);
            }
        }

        private void ApplySessionData()
        {
            Payload.deviceModel = DeviceModel.deviceModel;
            if (_platformSource.IsAndroidPlayer)
            {
                if (Configuration.Instance.recordIpAddress) Payload.ipAddress = _platformSource.GetIpAddress();

                // Read build_fingerprint from Android manifest.
                Payload.buildFingerprint = _platformSource.GetAndroidManifestMetadata("com.arborxr.abxrlib.build_fingerprint");

                string xrdmVersion = _platformSource.GetXrdmVersion();
                if (!string.IsNullOrEmpty(xrdmVersion))
                    Payload.xrdmVersion = xrdmVersion;
            }
            // TODO Geolocation
        }

        /// <summary>Returns the <c>pin</c> string from a JWT org token payload, or null if missing or not a JWT.</summary>
        private static string TryGetAssessmentPinFromOrgTokenPayload(string orgToken)
        {
            if (string.IsNullOrEmpty(orgToken)) return null;
            var payload = Utils.TryDecodeJwtPayload(orgToken);
            if (payload == null || !payload.TryGetValue("pin", out var pinObj) || pinObj == null) return null;

            string s = Utils.JwtPayloadValueToString(pinObj);
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Trim();
        }

        /// <summary>
        /// Applies current platform/subsystem getters (GetOrgId, GetFingerprint, GetDeviceId, GetDeviceTags)
        /// to RuntimeAuth so values set via Abxr setters or MDM getters are used. Only overwrites when the
        /// getter returns a non-empty value so we do not wipe configured credentials with empty values.
        /// </summary>
        private void ApplyAbxrOverridesToRuntimeAuth()
        {
            string orgId = _platformSource.GetCurrentOrgId();
            if (!string.IsNullOrEmpty(orgId)) RuntimeAuth.OrgId = orgId;

            string authSecret = _platformSource.GetCurrentFingerprint();
            if (!string.IsNullOrEmpty(authSecret)) RuntimeAuth.AuthSecret = authSecret;

            string deviceId = _platformSource.GetCurrentDeviceId();
            if (!string.IsNullOrEmpty(deviceId)) RuntimeAuth.DeviceId = deviceId;

            string[] tags = _platformSource.GetCurrentDeviceTags();
            if (tags != null && tags.Length > 0) RuntimeAuth.Tags = tags;
        }
    }
}
