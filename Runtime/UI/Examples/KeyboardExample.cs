/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Keyboard System Example
 * 
 * This file demonstrates how to use the AbxrLib keyboard system for creating
 * custom keyboard interactions that integrate with AbxrLib's configuration system.
 * 
 * This example shows:
 * - How to create and manage keyboard instances
 * - How to handle keyboard events and callbacks
 * - How to integrate with AbxrLib analytics
 * - How to use configuration-driven behavior
 * - How to work with authentication keyboards that have built-in panels
 * - How to use independent panel prefabs for other UI messages
 */

using UnityEngine;
using UnityEngine.UI;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.UI.Keyboard;

namespace AbxrLib.Runtime.UI.Examples
{
    /// <summary>
    /// Example implementation demonstrating the AbxrLib keyboard system
    /// Shows how to create, manage, and interact with keyboard instances
    /// </summary>
    public class KeyboardExample : MonoBehaviour
    {
        [Header("Keyboard Example Settings")]
        [Tooltip("Button to create a full keyboard")]
        public Button createFullKeyboardButton;
        
        [Tooltip("Button to create a PIN pad")]
        public Button createPinPadButton;
        
        [Tooltip("Button to destroy the current keyboard")]
        public Button destroyKeyboardButton;
        
        [Tooltip("Text component to display keyboard status")]
        public Text statusText;
        
        [Tooltip("Text component to display input feedback")]
        public Text inputFeedbackText;
        
        [Tooltip("Input field to show keyboard input")]
        public InputField inputField;
        
        [Header("Configuration")]
        [Tooltip("Use AbxrLib configuration settings for keyboard behavior")]
        public bool useConfigurationSettings = true;
        
        [Tooltip("Custom prompt text for the keyboard")]
        public string customPromptText = "Enter your text here";
        
        private bool _keyboardActive = false;
        private KeyboardHandler.KeyboardType _currentKeyboardType;
        
        private void Start()
        {
            SetupButtons();
            SetupKeyboardEvents();
            UpdateStatusText("Keyboard Example Ready - Click a button to create a keyboard");
        }
        
        private void SetupButtons()
        {
            if (createFullKeyboardButton != null)
            {
                createFullKeyboardButton.onClick.AddListener(CreateFullKeyboard);
            }
            
            if (createPinPadButton != null)
            {
                createPinPadButton.onClick.AddListener(CreatePinPad);
            }
            
            if (destroyKeyboardButton != null)
            {
                destroyKeyboardButton.onClick.AddListener(DestroyKeyboard);
            }
        }
        
        private void SetupKeyboardEvents()
        {
            // Subscribe to keyboard events
            KeyboardHandler.OnKeyboardCreated += OnKeyboardCreated;
            KeyboardHandler.OnKeyboardDestroyed += OnKeyboardDestroyed;
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from keyboard events
            KeyboardHandler.OnKeyboardCreated -= OnKeyboardCreated;
            KeyboardHandler.OnKeyboardDestroyed -= OnKeyboardDestroyed;
            
            // Clean up any active keyboard
            if (_keyboardActive)
            {
                KeyboardHandler.Destroy();
            }
        }
        
        /// <summary>
        /// Creates a full keyboard instance
        /// </summary>
        public void CreateFullKeyboard()
        {
            if (_keyboardActive)
            {
                Debug.LogWarning("[AbxrLib] KeyboardExample - Keyboard already active. Destroy current keyboard first.");
                UpdateStatusText("Keyboard already active - Destroy current keyboard first");
                return;
            }
            
            Debug.Log("[AbxrLib] KeyboardExample - Creating full keyboard");
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_full_keyboard_created", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "full_keyboard");
            
            // Create the keyboard
            KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
            
            // Set custom prompt if not using configuration
            if (!useConfigurationSettings)
            {
                KeyboardHandler.SetPrompt(customPromptText);
            }
            
            _currentKeyboardType = KeyboardHandler.KeyboardType.FullKeyboard;
            UpdateStatusText("Full Keyboard Created - Use VR controllers or direct touch to interact");
        }
        
        /// <summary>
        /// Creates a PIN pad instance
        /// </summary>
        public void CreatePinPad()
        {
            if (_keyboardActive)
            {
                Debug.LogWarning("[AbxrLib] KeyboardExample - Keyboard already active. Destroy current keyboard first.");
                UpdateStatusText("Keyboard already active - Destroy current keyboard first");
                return;
            }
            
            Debug.Log("[AbxrLib] KeyboardExample - Creating PIN pad");
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_pin_pad_created", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "pin_pad");
            
            // Create the PIN pad
            KeyboardHandler.Create(KeyboardHandler.KeyboardType.PinPad);
            
            // Set custom prompt if not using configuration
            if (!useConfigurationSettings)
            {
                KeyboardHandler.SetPrompt("Enter your 6-digit PIN");
            }
            
            _currentKeyboardType = KeyboardHandler.KeyboardType.PinPad;
            UpdateStatusText("PIN Pad Created - Use VR controllers or direct touch to interact");
        }
        
        /// <summary>
        /// Destroys the current keyboard instance
        /// </summary>
        public void DestroyKeyboard()
        {
            if (!_keyboardActive)
            {
                Debug.LogWarning("[AbxrLib] KeyboardExample - No keyboard to destroy");
                UpdateStatusText("No keyboard to destroy");
                return;
            }
            
            Debug.Log("[AbxrLib] KeyboardExample - Destroying keyboard");
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_keyboard_destroyed", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                _currentKeyboardType.ToString());
            
            // Destroy the keyboard
            KeyboardHandler.Destroy();
            
            UpdateStatusText("Keyboard Destroyed");
        }
        
        /// <summary>
        /// Called when a keyboard is created
        /// </summary>
        private void OnKeyboardCreated()
        {
            _keyboardActive = true;
            Debug.Log("[AbxrLib] KeyboardExample - Keyboard created successfully");
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_keyboard_created_success", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Correct, 
                _currentKeyboardType.ToString());
            
            // Update UI to reflect keyboard state
            UpdateButtonStates();
            
            // Start monitoring keyboard input if we have an input field
            if (inputField != null)
            {
                StartCoroutine(MonitorKeyboardInput());
            }
        }
        
        /// <summary>
        /// Called when a keyboard is destroyed
        /// </summary>
        private void OnKeyboardDestroyed()
        {
            _keyboardActive = false;
            Debug.Log("[AbxrLib] KeyboardExample - Keyboard destroyed successfully");
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_keyboard_destroyed_success", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Correct, 
                _currentKeyboardType.ToString());
            
            // Update UI to reflect keyboard state
            UpdateButtonStates();
        }
        
        /// <summary>
        /// Monitors keyboard input and updates the input field
        /// </summary>
        private System.Collections.IEnumerator MonitorKeyboardInput()
        {
            while (_keyboardActive)
            {
                // Check if KeyboardManager instance exists and has input
                if (KeyboardManager.Instance != null && KeyboardManager.Instance.inputField != null)
                {
                    string currentInput = KeyboardManager.Instance.inputField.text;
                    
                    // Update our input field if it's different
                    if (inputField != null && inputField.text != currentInput)
                    {
                        inputField.text = currentInput;
                        
                        // Update feedback text
                        if (inputFeedbackText != null)
                        {
                            inputFeedbackText.text = $"Input: {currentInput}";
                        }
                        
                        // Log input event for analytics
                        Abxr.EventInteractionComplete("keyboard_example_input_updated", 
                            Abxr.InteractionType.Select, 
                            Abxr.InteractionResult.Neutral, 
                            $"input_length_{currentInput.Length}");
                    }
                }
                
                yield return new WaitForSeconds(0.1f); // Check every 100ms
            }
        }
        
        /// <summary>
        /// Updates button states based on keyboard status
        /// </summary>
        private void UpdateButtonStates()
        {
            bool canCreate = !_keyboardActive;
            bool canDestroy = _keyboardActive;
            
            if (createFullKeyboardButton != null)
            {
                createFullKeyboardButton.interactable = canCreate;
            }
            
            if (createPinPadButton != null)
            {
                createPinPadButton.interactable = canCreate;
            }
            
            if (destroyKeyboardButton != null)
            {
                destroyKeyboardButton.interactable = canDestroy;
            }
        }
        
        /// <summary>
        /// Updates the status text
        /// </summary>
        private void UpdateStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[AbxrLib] KeyboardExample - {message}");
        }
        
        /// <summary>
        /// Toggles between configuration settings and custom settings
        /// </summary>
        public void ToggleConfigurationSettings()
        {
            useConfigurationSettings = !useConfigurationSettings;
            
            string message = useConfigurationSettings ? 
                "Using AbxrLib configuration settings" : 
                "Using custom settings";
            
            UpdateStatusText(message);
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_configuration_toggled", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                useConfigurationSettings.ToString());
        }
        
        /// <summary>
        /// Sets a custom prompt text
        /// </summary>
        public void SetCustomPrompt(string prompt)
        {
            customPromptText = prompt;
            
            if (_keyboardActive)
            {
                KeyboardHandler.SetPrompt(customPromptText);
            }
            
            UpdateStatusText($"Custom prompt set: {prompt}");
        }
        
        /// <summary>
        /// Example method to demonstrate keyboard integration with authentication
        /// </summary>
        public void DemonstrateAuthenticationFlow()
        {
            Debug.Log("[AbxrLib] KeyboardExample - Demonstrating authentication flow");
            
            // This would typically be called by the authentication system
            // but we can demonstrate the flow here
            if (!_keyboardActive)
            {
                CreateFullKeyboard();
                KeyboardHandler.SetPrompt("Please enter your credentials");
            }
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_auth_flow_demo", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "authentication_demo");
        }
        
        /// <summary>
        /// Example method to demonstrate PIN pad usage
        /// </summary>
        public void DemonstratePinPadFlow()
        {
            Debug.Log("[AbxrLib] KeyboardExample - Demonstrating PIN pad flow");
            
            if (!_keyboardActive)
            {
                CreatePinPad();
                KeyboardHandler.SetPrompt("Enter your 6-digit assessment PIN");
            }
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_pin_flow_demo", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "pin_demo");
        }
        
        /// <summary>
        /// Example method to demonstrate independent panel usage for non-authentication messages
        /// This shows how to use panel prefabs for other UI messages while authentication
        /// keyboards have their own built-in panels
        /// </summary>
        public void DemonstrateIndependentPanelUsage()
        {
            Debug.Log("[AbxrLib] KeyboardExample - Demonstrating independent panel usage");
            
            // Load independent panel prefab for custom messages
            GameObject panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
            if (panelPrefab != null)
            {
                GameObject panelInstance = Instantiate(panelPrefab);
                
                // Set custom message text
                var panelText = panelInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (panelText != null)
                {
                    panelText.text = "This is an independent panel for custom messages.\nAuthentication keyboards have built-in panels.";
                }
                
                // Auto-destroy after 5 seconds
                Destroy(panelInstance, 5f);
                
                Debug.Log("[AbxrLib] KeyboardExample - Independent panel created and will auto-destroy in 5 seconds");
            }
            else
            {
                Debug.LogWarning("[AbxrLib] KeyboardExample - Could not load AbxrDarkPanelWithText prefab");
            }
            
            // Log analytics event
            Abxr.EventInteractionComplete("keyboard_example_independent_panel_demo", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "independent_panel_demo");
        }
        
        /// <summary>
        /// Gets the current keyboard status for external scripts
        /// </summary>
        public bool IsKeyboardActive => _keyboardActive;
        
        /// <summary>
        /// Gets the current keyboard type for external scripts
        /// </summary>
        public KeyboardHandler.KeyboardType CurrentKeyboardType => _currentKeyboardType;
    }
}
