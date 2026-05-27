using AbxrLib.Runtime;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Tests.Runtime
{
    internal static class AbxrTestHooks
    {
        internal static bool hasSubsystemInstance => AbxrSubsystem.Instance != null;

        internal static void ResetConfigurationForTest()
        {
            Configuration.UseTransientDefaultsForTest();
            Configuration.Instance.enableAutomaticInitialization = false;
        }

        internal static AbxrSubsystem CreateSubsystemForTest()
        {
            Abxr.Initialize();
            return AbxrSubsystem.Instance;
        }

        /// <summary>
        /// Destroy the current test-owned subsystem.
        /// This is the normal per-test runtime reset path: tests configure first,
        /// initialize a fresh subsystem, then destroy it during teardown.
        /// </summary>
        internal static void DestroySubsystemForTest(bool clearConfiguration = true)
        {
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
