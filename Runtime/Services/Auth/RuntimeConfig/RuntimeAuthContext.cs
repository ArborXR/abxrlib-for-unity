using System;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Owns mutable runtime authentication state. Configuration/platform resolution lives in <see cref="RuntimeAuthResolver"/>.
    /// </summary>
    internal sealed class RuntimeAuthContext
    {
        private const string PartnerNone = "none";

        internal AuthPayload Payload { get; }
        internal RuntimeAuthConfig RuntimeAuth { get; } = new();

        /// <summary>
        /// WebGL: pre-filled assessment PIN from org token JWT claim <c>pin</c> (preferred) or from assessment_pin URL query.
        /// When set, GET config can be overridden to assessmentPin and the first user-auth attempt auto-submits this value.
        /// </summary>
        internal string WebGlAssessmentPin { get; private set; }

        internal RuntimeAuthContext()
        {
            Payload = CreateBasePayload();
        }

        private static AuthPayload CreateBasePayload()
        {
            return new AuthPayload
            {
                partner = PartnerNone,
                deviceId = SystemInfo.deviceUniqueIdentifier,
                sessionId = Guid.NewGuid().ToString(),
                osVersion = SystemInfo.operatingSystem,
                appVersion = Application.version,
                unityVersion = Application.unityVersion,
                abxrLibType = "unity",
                abxrLibVersion = AbxrLibVersion.Version
            };
        }

        internal string PreparePayloadForAuth() => PreparePayloadForAuth(Payload);

        internal string PreparePayloadForAuth(AuthPayload requestPayload) => RuntimeAuth.PreparePayloadForAuth(requestPayload);

        internal void SetSessionId(string sessionId) => Payload.sessionId = sessionId;

        internal void ClearAuthenticationState()
        {
            Payload.sessionId = null;
            RuntimeAuth.authMechanism = null;
            WebGlAssessmentPin = null;
        }

        internal void PrepareNewSession()
        {
            ClearAuthenticationState();
            Payload.sessionId = Guid.NewGuid().ToString();
        }

        internal void ClearAuthMechanismForSession() => RuntimeAuth.authMechanism = null;

        internal AuthMechanism CopyAuthMechanismForSession() =>
            AuthMechanismResolver.CopyForSession(RuntimeAuth.authMechanism);

        internal AuthMechanism ResolveConfigAuthMechanism(AuthMechanism configMechanism, bool learnerLauncherModeEnabled)
        {
            RuntimeAuth.authMechanism = AuthMechanismResolver.ResolveConfigMechanism(configMechanism, learnerLauncherModeEnabled);
            return CopyAuthMechanismForSession();
        }

        internal bool TryForceWebGlAssessmentPinAuthMechanism(out AuthMechanism sessionMechanism)
        {
            sessionMechanism = null;
            if (!HasWebGlAssessmentPin) return false;

            RuntimeAuth.authMechanism = AuthMechanismResolver.ForceAssessmentPin(RuntimeAuth.authMechanism);
            sessionMechanism = CopyAuthMechanismForSession();
            return true;
        }

        internal void SetWebGlAssessmentPin(string value) => WebGlAssessmentPin = value;

        internal void ClearWebGlAssessmentPin() => WebGlAssessmentPin = null;

        internal bool HasWebGlAssessmentPin => !string.IsNullOrEmpty(WebGlAssessmentPin);

        internal void ApplyRuntimeFlagsFromConfiguration(Configuration config)
        {
            RuntimeAuth.enableAutoStartAuthentication = config?.enableAutoStartAuthentication ?? true;
            RuntimeAuth.enableReturnTo = config?.enableReturnTo ?? true;
            RuntimeAuth.enableAutoStartModules = config?.enableAutoStartModules ?? true;
            RuntimeAuth.enableAutoAdvanceModules = config?.enableAutoAdvanceModules ?? true;
        }

        internal bool GetEffectiveEnableAutoStartModules() => RuntimeAuth.enableAutoStartModules;

        internal bool GetEffectiveEnableAutoAdvanceModules() => RuntimeAuth.enableAutoAdvanceModules;

        internal bool GetEffectiveEnableReturnTo() => RuntimeAuth.enableReturnTo;

        internal bool GetEnableAutoStartAuthentication() => RuntimeAuth.enableAutoStartAuthentication;

        internal void SetRuntimeAuthOrgId(string value) => RuntimeAuth.orgId = value ?? "";

        internal void SetRuntimeAuthAuthSecret(string value) => RuntimeAuth.authSecret = value ?? "";

        internal void SetRuntimeAuthDeviceId(string value) => RuntimeAuth.deviceId = value ?? "";
    }
}
