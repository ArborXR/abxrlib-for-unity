using UnityEngine;
using AbxrLib.Tests.Runtime.Utilities;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Initializes test mode as early as possible, before scene load.
    /// This ensures test mode is enabled before Authentication.Start() runs.
    /// </summary>
    public static class TestInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            // Enable test mode as early as possible so Authentication.Start() can detect it
            // This runs before any MonoBehaviour.Start() methods, including Authentication.Start()
            AuthenticationTestHelper.EnableTestMode();
            Debug.Log("TestInitializer: Test mode enabled before scene load");
        }
    }
}

