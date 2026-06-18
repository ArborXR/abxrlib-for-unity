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
            TrySetDynamicOrgToken(RuntimeAuth.orgId, RuntimeAuth.authSecret);

            if (_platformSource.IsAndroidPlayer) ApplyAndroidIntentOrgTokenIfAvailable(copyToPayload: false);
            else if (_platformSource.IsWebGlPlayer) ApplyWebGlQueryData();
            else if (_platformSource.IsStandalonePlayer) ApplyDesktopQueryData();

            return _context.PreparePayloadForAuth();
        }

        /// <summary>Loads auth-related values from Configuration into RuntimeAuth.</summary>
        private void LoadRuntimeAuthFromConfig()
        {
            var s = Configuration.Instance;
            RuntimeAuth.useAppTokens = s.useAppTokens;
            RuntimeAuth.buildType = !string.IsNullOrEmpty(s.buildType) ? s.buildType : ProductionBuildType;
            if (s.useAppTokens)
            {
                RuntimeAuth.appToken = s.appToken;
                RuntimeAuth.orgToken = string.Equals(s.buildType, ProductionBuildType, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : s.orgToken;
            }
            else
            {
                RuntimeAuth.appId = s.appID;
                if (string.Equals(s.buildType, ProductionBuildType, StringComparison.OrdinalIgnoreCase))
                {
                    RuntimeAuth.orgId = null;
                    RuntimeAuth.authSecret = null;
                }
                else
                {
                    RuntimeAuth.orgId = s.orgID;
                    RuntimeAuth.authSecret = s.authSecret;
                }
            }

            SetDefaultRuntimeDeviceContext();
        }

        private void SetDefaultRuntimeDeviceContext()
        {
            string deviceIdFromSubsystem = _platformSource.GetCurrentDeviceId();
            RuntimeAuth.deviceId = !string.IsNullOrEmpty(deviceIdFromSubsystem) ? deviceIdFromSubsystem : Payload.deviceId;
            RuntimeAuth.partner = PartnerNone;
            RuntimeAuth.tags = null;
        }

        private void ApplyWebGlDeviceIdFromPlatform()
        {
            string webGlDeviceId = _platformSource.GetOrCreateWebGlDeviceId();
            if (string.IsNullOrEmpty(webGlDeviceId)) return;

            Payload.deviceId = webGlDeviceId;
            RuntimeAuth.deviceId = webGlDeviceId;
        }

        private bool TrySetDynamicOrgToken(string orgId, string authSecret, bool overwriteExisting = false)
        {
            if (!RuntimeAuth.useAppTokens) return false;
            if (!overwriteExisting && !string.IsNullOrEmpty(RuntimeAuth.orgToken)) return false;
            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(authSecret)) return false;

            string dynamicToken = Utils.BuildOrgTokenDynamic(orgId, authSecret);
            if (string.IsNullOrEmpty(dynamicToken)) return false;

            RuntimeAuth.orgToken = dynamicToken;
            return true;
        }

        private void LoadConfigData()
        {
            var config = Configuration.Instance;
            _context.ApplyRuntimeFlagsFromConfiguration(config);

            var configData = Utils.ExtractConfigData(config);
            if (!configData.isValid) return;

            SetDefaultRuntimeDeviceContext();

            RuntimeAuth.useAppTokens = configData.useAppTokens;
            RuntimeAuth.buildType = configData.buildType ?? ProductionBuildType;
            if (configData.useAppTokens)
            {
                RuntimeAuth.appToken = configData.appToken;
                RuntimeAuth.orgToken = configData.orgToken;
            }
            else
            {
                RuntimeAuth.appId = configData.appId;
                RuntimeAuth.orgId = configData.orgId;
                RuntimeAuth.authSecret = configData.authSecret;
            }

            RuntimeAuth.CopyAuthFieldsTo(Payload);
        }

        /// <summary>Applies an Android org_token intent extra when app-token auth has no org token yet.</summary>
        private void ApplyAndroidIntentOrgTokenIfAvailable(bool copyToPayload = true)
        {
            if (!RuntimeAuth.useAppTokens || !string.IsNullOrEmpty(RuntimeAuth.orgToken)) return;

            string orgTokenIntent = _platformSource.GetAndroidIntentParam("org_token");
            if (string.IsNullOrEmpty(orgTokenIntent)) return;

            RuntimeAuth.orgToken = orgTokenIntent;
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
            RuntimeAuth.partner = PartnerArborXr;
            RuntimeAuth.deviceId = _platformSource.GetCurrentDeviceId();
            RuntimeAuth.tags = _platformSource.GetCurrentDeviceTags();

            // production_custom: only deviceId/partner/tags from MDM; org credentials stay from config.
            if (RuntimeAuth.buildType == ProductionCustomBuildType)
            {
                RuntimeAuth.CopyAuthFieldsTo(Payload);
                return;
            }

            // Non-production_custom: update auth from MDM (dynamic org token or orgId/authSecret).
            if (RuntimeAuth.useAppTokens)
            {
                TrySetDynamicOrgToken(_platformSource.GetCurrentOrgId(), _platformSource.GetCurrentFingerprint(), overwriteExisting: true);
            }
            else
            {
                RuntimeAuth.orgId = _platformSource.GetCurrentOrgId();
                RuntimeAuth.authSecret = _platformSource.GetCurrentFingerprint();
            }

            RuntimeAuth.CopyAuthFieldsTo(Payload);
        }

        private void ApplyWebGlQueryData()
        {
            if (RuntimeAuth.buildType == ProductionCustomBuildType) return;

            string absoluteUrl = _platformSource.AbsoluteUrl ?? "";
            string orgTokenQuery = Utils.GetQueryParam("org_token", absoluteUrl);
            if (!string.IsNullOrEmpty(orgTokenQuery))
            {
                RuntimeAuth.orgToken = orgTokenQuery;
                RuntimeAuth.CopyAuthFieldsTo(Payload);
            }

            _context.SetWebGlAssessmentPin(null);
            string pinFromOrgJwt = TryGetAssessmentPinFromOrgTokenPayload(RuntimeAuth.orgToken);
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
            if (RuntimeAuth.buildType == ProductionCustomBuildType) return;

            string orgToken = _platformSource.GetDesktopOrgToken();
            if (!string.IsNullOrEmpty(orgToken))
            {
                RuntimeAuth.orgToken = orgToken;
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
            if (!string.IsNullOrEmpty(orgId)) RuntimeAuth.orgId = orgId;

            string authSecret = _platformSource.GetCurrentFingerprint();
            if (!string.IsNullOrEmpty(authSecret)) RuntimeAuth.authSecret = authSecret;

            string deviceId = _platformSource.GetCurrentDeviceId();
            if (!string.IsNullOrEmpty(deviceId)) RuntimeAuth.deviceId = deviceId;

            string[] tags = _platformSource.GetCurrentDeviceTags();
            if (tags != null && tags.Length > 0) RuntimeAuth.tags = tags;
        }
    }
}
