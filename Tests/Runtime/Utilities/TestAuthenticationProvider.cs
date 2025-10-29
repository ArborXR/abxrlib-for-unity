using System;
using System.Collections;
using UnityEngine;
using AbxrLib.Runtime.Authentication;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Tests.Runtime.Utilities
{
    /// <summary>
    /// Test authentication provider that can hijack the authentication process
    /// and provide programmatic responses instead of showing UI keyboards
    /// 
    /// This is the SINGLE source of truth for test authentication credentials.
    /// All test authentication uses these default values unless explicitly overridden.
    /// </summary>
    public static class TestAuthenticationProvider
    {
        private static bool _isTestMode = false;
        
        // Default test credentials - SINGLE SOURCE OF TRUTH
        private static string _defaultPin = "999999";
        private static string _defaultEmail = "testuser";
        private static string _defaultText = "EmpID1234";
        
        /// <summary>
        /// Enable test mode for authentication
        /// </summary>
        public static void EnableTestMode()
        {
            _isTestMode = true;
            Debug.Log("TestAuthenticationProvider: Test mode ENABLED - authentication will use programmatic responses");
            Debug.Log($"TestAuthenticationProvider: Current test mode status: {_isTestMode}");
            
            // Register this provider with the main ABXRLib
            TestAuthenticationRegistry.RegisterProvider(new TestAuthProviderImpl());
        }
        
        /// <summary>
        /// Disable test mode for authentication
        /// </summary>
        public static void DisableTestMode()
        {
            _isTestMode = false;
            Debug.Log("TestAuthenticationProvider: Test mode disabled - authentication will use normal UI flow");
            
            // Unregister the provider from the main ABXRLib
            TestAuthenticationRegistry.UnregisterProvider();
        }
        
        /// <summary>
        /// Check if we're in test mode
        /// </summary>
        public static bool IsTestMode => _isTestMode;
        
        /// <summary>
        /// Set default responses for different authentication mechanisms
        /// </summary>
        public static void SetDefaultResponses(string pin = null, string email = null, string text = null)
        {
            if (pin != null) _defaultPin = pin;
            if (email != null) _defaultEmail = email;
            if (text != null) _defaultText = text;
            
            Debug.Log($"TestAuthenticationProvider: Default responses set - PIN: {_defaultPin}, Email: {_defaultEmail}, Text: {_defaultText}");
        }
        
        /// <summary>
        /// Get the current default test credentials for debugging
        /// </summary>
        public static (string pin, string email, string text) GetDefaultResponses()
        {
            return (_defaultPin, _defaultEmail, _defaultText);
        }
        
        /// <summary>
        /// Get the appropriate test response for the given authentication mechanism type
        /// </summary>
        public static string GetTestResponse(string authType, string domain = null)
        {
            if (!_isTestMode)
            {
                Debug.LogWarning("TestAuthenticationProvider: GetTestResponse called but not in test mode");
                return null;
            }
            
            return authType switch
            {
                "assessmentPin" => _defaultPin,
                "email" => domain != null ? $"{_defaultEmail}@{domain}" : _defaultEmail,
                "text" => _defaultText,
                "keyboard" => _defaultText,
                _ => _defaultText // Default fallback
            };
        }
        
        /// <summary>
        /// Hijack the PresentKeyboard call and provide a test response instead
        /// This method should be called from the modified Abxr.PresentKeyboard method
        /// </summary>
        public static IEnumerator HandleTestAuthentication(string promptText, string keyboardType, string emailDomain)
        {
            if (!_isTestMode)
            {
                Debug.LogWarning("TestAuthenticationProvider: HandleTestAuthentication called but not in test mode");
                yield break;
            }
            
            Debug.Log("TestAuthenticationProvider: HIJACKING authentication!");
            Debug.Log($"TestAuthenticationProvider: Server requested AuthMechanism type: '{keyboardType}'");
            Debug.Log($"TestAuthenticationProvider: Server prompt: '{promptText}'");
            Debug.Log($"TestAuthenticationProvider: Email domain: '{emailDomain}'");
            
            // Get the test response
            string testResponse = GetTestResponse(keyboardType, emailDomain);
            
            if (string.IsNullOrEmpty(testResponse))
            {
                Debug.LogError($"TestAuthenticationProvider: No test response available for auth type: {keyboardType}");
                yield break;
            }
            
            Debug.Log($"TestAuthenticationProvider: Providing test response: '{testResponse}' for auth type: '{keyboardType}'");
            
            // Call KeyboardAuthenticate with the test response
            yield return Authentication.KeyboardAuthenticate(testResponse);
            
            Debug.Log($"TestAuthenticationProvider: Authentication attempt completed with response: '{testResponse}'");
        }
    }
    
    /// <summary>
    /// Implementation of ITestAuthenticationProvider for the static TestAuthenticationProvider
    /// </summary>
    internal class TestAuthProviderImpl : ITestAuthenticationProvider
    {
        public bool IsTestMode => TestAuthenticationProvider.IsTestMode;
        
        public IEnumerator HandleTestAuthentication(string promptText, string keyboardType, string emailDomain)
        {
            return TestAuthenticationProvider.HandleTestAuthentication(promptText, keyboardType, emailDomain);
        }
    }
}
