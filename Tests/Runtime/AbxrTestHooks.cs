using System.Reflection;
using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using UnityEngine;

namespace AbxrLib.Tests.Runtime
{
    internal static class AbxrTestHooks
    {
        internal static bool HasSubsystemInstance => AbxrSubsystem.Instance != null;

        internal static void ResetConfigurationForTest()
        {
            AbxrAuthService.TestAuthHandoffPayload = null;
            ClearSsoForTest();
            Configuration.UseTransientDefaultsForTest();
            Configuration.Instance.enableAutomaticInitialization = false;
        }

        internal static AbxrSubsystem CreateSubsystemForTest()
        {
            Abxr.Initialize();
            return AbxrSubsystem.Instance;
        }

        internal static void SetAuthHandoffPayloadForTest(string payload) =>
            AbxrAuthService.TestAuthHandoffPayload = payload;

        internal static void SetSsoForTest(bool isAuthenticated, string accessToken)
        {
            AbxrSubsystem.TestSsoIsAuthenticated = isAuthenticated;
            AbxrSubsystem.TestSsoAccessToken = accessToken;
        }

        internal static void ClearSsoForTest()
        {
            AbxrSubsystem.TestSsoIsAuthenticated = null;
            AbxrSubsystem.TestSsoAccessToken = null;
        }

        internal static AbxrAuthService GetAuthServiceForTest()
        {
            var instance = AbxrSubsystem.Instance;
            if (instance == null) return null;

            var field = typeof(AbxrSubsystem).GetField("_authService", BindingFlags.Instance | BindingFlags.NonPublic);

            return field?.GetValue(instance) as AbxrAuthService;
        }

        internal static string GetHandoffJsonForTest(bool includeReturnToPackage = false) =>
            GetAuthServiceForTest()?.GetHandoffJson(includeReturnToPackage);

        /// <summary>
        /// Destroy the current test-owned subsystem.
        /// This is the normal per-test runtime reset path: tests configure first,
        /// initialize a fresh subsystem, then destroy it during teardown.
        /// </summary>
        internal static void DestroySubsystemForTest(bool clearConfiguration = true)
        {
            AbxrAuthService.TestAuthHandoffPayload = null;
            ClearSsoForTest();

            var instance = AbxrSubsystem.Instance;
            if (instance != null)
            {
                instance.CleanupBeforeDestroyForTest();
                Object.Destroy(instance.gameObject);
            }

            if (clearConfiguration) ResetConfigurationForTest();
        }
    }
}
