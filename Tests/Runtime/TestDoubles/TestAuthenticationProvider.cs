using System;
using System.Collections;
using UnityEngine;
using AbxrLib.Runtime.Authentication;

namespace AbxrLib.Tests.Runtime.TestDoubles
{
    /// <summary>
    /// Test authentication provider that can hijack the authentication process
    /// and provide programmatic responses instead of showing UI keyboards
    /// </summary>
    public static class TestAuthenticationProvider
    {
        private static bool _isTestMode = false;
        private static string _defaultPin = "999999";
        private static string _defaultEmail = "testuser";
        private static string _defaultText = "EmpID1234";
        
        /// <summary>
        /// Enable test mode for authentication
        /// </summary>
        public static void EnableTestMode()
        {
            _isTestMode = true;
            Debug.Log("TestAuthenticationProvider: Test mode enabled - authentication will use programmatic responses");
        }
        
        /// <summary>
        /// Disable test mode for authentication
        /// </summary>
        public static void DisableTestMode()
        {
            _isTestMode = false;
            Debug.Log("TestAuthenticationProvider: Test mode disabled - authentication will use normal UI flow");
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
            
            Debug.Log($"TestAuthenticationProvider: Handling test authentication - Type: {keyboardType}, Domain: {emailDomain}");
            
            // Get the test response
            string testResponse = GetTestResponse(keyboardType, emailDomain);
            
            if (string.IsNullOrEmpty(testResponse))
            {
                Debug.LogError($"TestAuthenticationProvider: No test response available for auth type: {keyboardType}");
                yield break;
            }
            
            Debug.Log($"TestAuthenticationProvider: Providing test response: {testResponse}");
            
            // Call KeyboardAuthenticate with the test response
            yield return Authentication.KeyboardAuthenticate(testResponse);
        }
    }
}
