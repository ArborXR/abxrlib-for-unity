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

        internal RuntimeAuthContext() => Payload = CreateBasePayload();

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

        internal void ClearAuthenticationState()
        {
            Payload.sessionId = null;
            RuntimeAuth.AuthMechanism = null;
            WebGlAssessmentPin = null;
        }

        internal void PrepareNewSession()
        {
            ClearAuthenticationState();
            Payload.sessionId = Guid.NewGuid().ToString();
        }

        internal void ClearAuthMechanismForSession() => RuntimeAuth.AuthMechanism = null;

        internal AuthMechanism CopyAuthMechanismForSession() =>
            AuthMechanismResolver.CopyForSession(RuntimeAuth.AuthMechanism);

        internal AuthMechanism ResolveConfigAuthMechanism(AuthMechanism configMechanism, bool learnerLauncherModeEnabled)
        {
            RuntimeAuth.AuthMechanism = AuthMechanismResolver.ResolveConfigMechanism(configMechanism, learnerLauncherModeEnabled);
            return CopyAuthMechanismForSession();
        }

        internal bool TryForceWebGlAssessmentPinAuthMechanism(out AuthMechanism sessionMechanism)
        {
            sessionMechanism = null;
            if (!HasWebGlAssessmentPin) return false;

            RuntimeAuth.AuthMechanism = AuthMechanismResolver.ForceAssessmentPin(RuntimeAuth.AuthMechanism);
            sessionMechanism = CopyAuthMechanismForSession();
            return true;
        }

        internal void SetWebGlAssessmentPin(string value) => WebGlAssessmentPin = value;

        internal void ClearWebGlAssessmentPin() => WebGlAssessmentPin = null;

        internal bool HasWebGlAssessmentPin => !string.IsNullOrEmpty(WebGlAssessmentPin);

        internal void ApplyRuntimeFlagsFromConfiguration(Configuration config)
        {
            RuntimeAuth.EnableAutoStartAuthentication = config?.enableAutoStartAuthentication ?? true;
            RuntimeAuth.EnableReturnTo = config?.enableReturnTo ?? true;
            RuntimeAuth.EnableAutoStartModules = config?.enableAutoStartModules ?? true;
            RuntimeAuth.EnableAutoAdvanceModules = config?.enableAutoAdvanceModules ?? true;
        }

        internal bool GetEffectiveEnableAutoStartModules() => RuntimeAuth.EnableAutoStartModules;

        internal bool GetEffectiveEnableAutoAdvanceModules() => RuntimeAuth.EnableAutoAdvanceModules;

        internal bool GetEffectiveEnableReturnTo() => RuntimeAuth.EnableReturnTo;

        internal bool GetEnableAutoStartAuthentication() => RuntimeAuth.EnableAutoStartAuthentication;

        internal void SetRuntimeAuthOrgId(string value) => RuntimeAuth.OrgId = value ?? "";

        internal void SetRuntimeAuthAuthSecret(string value) => RuntimeAuth.AuthSecret = value ?? "";

        internal void SetRuntimeAuthDeviceId(string value) => RuntimeAuth.DeviceId = value ?? "";
    }
}
