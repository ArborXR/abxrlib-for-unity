/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Hand Tracking Button System Example
 * 
 * This file demonstrates how to use the HandTrackingButtonSystem for creating
 * custom hand tracking interactions that integrate with AbxrLib's configuration system.
 * 
 * This example shows:
 * - How to set up hand tracking buttons with colliders
 * - How to handle hand tracking interactions
 * - How to integrate with AbxrLib analytics
 * - How to use configuration-driven behavior
 */

using UnityEngine;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Runtime.UI.Examples
{
    /// <summary>
    /// Example implementation of HandTrackingButtonSystem
    /// Demonstrates how to create hand tracking button interactions
    /// </summary>
    public class HandTrackingButtonExample : HandTrackingButtonSystem
    {
        [Header("Example-Specific Settings")]
        [Tooltip("Text component to display button feedback")]
        public UnityEngine.UI.Text feedbackText;
        
        [Tooltip("Color to flash when button is activated")]
        public Color flashColor = Color.green;
        
        [Tooltip("Duration of flash effect")]
        public float flashDuration = 0.3f;
        
        private Color[] originalColors;
        private Renderer[] buttonRenderers;
        
        protected override void Start()
        {
            base.Start();
            SetupExampleSpecificFeatures();
        }
        
        private void SetupExampleSpecificFeatures()
        {
            // Store original button colors for flashing effect
            if (handTrackingButtons != null)
            {
                buttonRenderers = new Renderer[handTrackingButtons.Length];
                originalColors = new Color[handTrackingButtons.Length];
                
                for (int i = 0; i < handTrackingButtons.Length; i++)
                {
                    if (handTrackingButtons[i] != null && handTrackingButtons[i].buttonObject != null)
                    {
                        buttonRenderers[i] = handTrackingButtons[i].buttonObject.GetComponent<Renderer>();
                        if (buttonRenderers[i] != null)
                        {
                            originalColors[i] = buttonRenderers[i].material.color;
                        }
                    }
                }
            }
            
            // Set initial feedback text
            if (feedbackText != null)
            {
                feedbackText.text = "Hand Tracking Button System Ready";
            }
        }
        
        protected override void OnHandTrackingButtonClick(int buttonIndex, string buttonName)
        {
            // Flash button color
            if (buttonRenderers != null && buttonIndex < buttonRenderers.Length && buttonRenderers[buttonIndex] != null)
            {
                StartCoroutine(FlashButton(buttonIndex));
            }
            
            // Update feedback text
            if (feedbackText != null)
            {
                feedbackText.text = $"Hand Activated: {buttonName}";
            }
            
            // Handle specific button actions
            switch (buttonName.ToLower())
            {
                case "start":
                    HandleStartButton();
                    break;
                case "settings":
                    HandleSettingsButton();
                    break;
                case "exit":
                    HandleExitButton();
                    break;
                case "help":
                    HandleHelpButton();
                    break;
                default:
                    HandleGenericButton(buttonName);
                    break;
            }
        }
        
        private void HandleStartButton()
        {
            Debug.Log("AbxrLib - HandTrackingButtonExample: Start button activated by hand tracking - Starting application");
            
            // Log a level start event
            Abxr.EventLevelStart("hand_tracking_example", new Abxr.Dict
            {
                ["button_system"] = "hand_tracking",
                ["interaction_type"] = "direct_touch",
                ["input_method"] = "hand_tracking"
            });
        }
        
        private void HandleSettingsButton()
        {
            Debug.Log("AbxrLib - HandTrackingButtonExample: Settings button activated by hand tracking - Opening settings");
            
            // Log an interaction event
            Abxr.EventInteractionComplete("settings_button_hand_activated", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "settings_opened_hand_tracking");
        }
        
        private void HandleExitButton()
        {
            Debug.Log("AbxrLib - HandTrackingButtonExample: Exit button activated by hand tracking - Exiting application");
            
            // Log an interaction event
            Abxr.EventInteractionComplete("exit_button_hand_activated", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "application_exit_hand_tracking");
            
            // In a real application, you might want to quit or return to main menu
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        
        private void HandleHelpButton()
        {
            Debug.Log("AbxrLib - HandTrackingButtonExample: Help button activated by hand tracking - Showing help");
            
            // Log an interaction event
            Abxr.EventInteractionComplete("help_button_hand_activated", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "help_requested_hand_tracking");
        }
        
        private void HandleGenericButton(string buttonName)
        {
            Debug.Log($"AbxrLib - HandTrackingButtonExample: Generic hand tracking button '{buttonName}' activated");
            
            // Log a generic interaction event
            Abxr.EventInteractionComplete($"generic_hand_button_activated", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                buttonName);
        }
        
        private System.Collections.IEnumerator FlashButton(int buttonIndex)
        {
            if (buttonRenderers == null || buttonIndex >= buttonRenderers.Length || buttonRenderers[buttonIndex] == null)
                yield break;
            
            Renderer buttonRenderer = buttonRenderers[buttonIndex];
            Color originalColor = originalColors[buttonIndex];
            
            // Flash to highlight color
            buttonRenderer.material.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            
            // Return to original color
            buttonRenderer.material.color = originalColor;
        }
        
        /// <summary>
        /// Example method to demonstrate runtime configuration changes
        /// </summary>
        public void ToggleDirectTouch()
        {
            bool currentSetting = useConfigurationSettings ? 
                Configuration.Instance.enableDirectTouchInteraction : 
                enableDirectTouch;
            
            SetDirectTouchEnabled(!currentSetting);
            
            if (feedbackText != null)
            {
                feedbackText.text = $"Direct Touch: {(!currentSetting ? "Enabled" : "Disabled")}";
            }
        }
        
        /// <summary>
        /// Example method to demonstrate runtime positioning changes
        /// </summary>
        public void ToggleFaceCamera()
        {
            bool currentSetting = useConfigurationSettings ? 
                Configuration.Instance.authUIFollowCamera : 
                enableFaceCamera;
            
            // This would require more complex implementation to toggle at runtime
            // For now, just log the change
            Debug.Log($"AbxrLib - HandTrackingButtonExample: Face camera toggled to {!currentSetting}");
            
            if (feedbackText != null)
            {
                feedbackText.text = $"Face Camera: {(!currentSetting ? "Enabled" : "Disabled")}";
            }
        }
        
        /// <summary>
        /// Example method to test hand tracking button activation programmatically
        /// Useful for testing without actual hand tracking
        /// </summary>
        public void TestButtonActivation(int buttonIndex)
        {
            if (handTrackingButtons != null && buttonIndex < handTrackingButtons.Length && handTrackingButtons[buttonIndex] != null)
            {
                handTrackingButtons[buttonIndex].Activate();
            }
        }
    }
}
