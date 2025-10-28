using System.Collections;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Interface for test authentication providers
    /// This allows the main ABXRLib to work with test authentication without direct dependencies
    /// </summary>
    public interface ITestAuthenticationProvider
    {
        bool IsTestMode { get; }
        IEnumerator HandleTestAuthentication(string promptText, string keyboardType, string emailDomain);
    }
    
    /// <summary>
    /// Static registry for test authentication providers
    /// This allows test code to register itself with the main ABXRLib
    /// </summary>
    public static class TestAuthenticationRegistry
    {
        private static ITestAuthenticationProvider _provider;
        
        /// <summary>
        /// Register a test authentication provider
        /// </summary>
        public static void RegisterProvider(ITestAuthenticationProvider provider)
        {
            _provider = provider;
        }
        
        /// <summary>
        /// Unregister the test authentication provider
        /// </summary>
        public static void UnregisterProvider()
        {
            _provider = null;
        }
        
        /// <summary>
        /// Check if a test provider is registered and active
        /// </summary>
        public static bool IsTestModeActive => _provider?.IsTestMode ?? false;
        
        /// <summary>
        /// Get the registered test provider
        /// </summary>
        public static ITestAuthenticationProvider GetProvider() => _provider;
    }
}
