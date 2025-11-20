/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Custom Button System Example
 * 
 * This file demonstrates how to use the CustomButtonSystem for creating
 * custom UI interactions that integrate with AbxrLib's configuration system.
 * 
 * This example shows:
 * - How to extend CustomButtonSystem for custom behavior
 * - How to handle different button types
 * - How to integrate with AbxrLib analytics
 * - How to use configuration-driven behavior
 */

using UnityEngine;
using UnityEngine.UI;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Runtime.UI.Examples
{
    /// <summary>
    /// Example implementation of CustomButtonSystem
    /// Demonstrates how to create custom button interactions
    /// </summary>
    public class CustomButtonExample : CustomButtonSystem
    {
        [Header("Example-Specific Settings")]
        [Tooltip("Text component to display button feedback")]
        public Text feedbackText;
        
        [Tooltip("Sound to play when buttons are clicked")]
        public AudioSource clickSound;
        
        [Tooltip("Color to flash when button is clicked")]
        public Color flashColor = Color.yellow;
        
        private Color originalColor;
        private Image[] buttonImages;
        
        protected override void Start()
        {
            base.Start();
            SetupExampleSpecificFeatures();
        }
        
        private void SetupExampleSpecificFeatures()
        {
            // Store original button colors for flashing effect
            if (customButtons != null)
            {
                buttonImages = new Image[customButtons.Length];
                for (int i = 0; i < customButtons.Length; i++)
                {
                    if (customButtons[i] != null)
                    {
                        buttonImages[i] = customButtons[i].GetComponent<Image>();
                        if (buttonImages[i] != null && i == 0)
                        {
                            originalColor = buttonImages[i].color;
                        }
                    }
                }
            }
            
            // Set initial feedback text
            if (feedbackText != null)
            {
                feedbackText.text = "Custom Button System Ready";
            }
        }
        
        protected override void OnCustomButtonClick(int buttonIndex, string buttonName)
        {
            // Play click sound
            if (clickSound != null)
            {
                clickSound.Play();
            }
            
            // Flash button color
            if (buttonImages != null && buttonIndex < buttonImages.Length && buttonImages[buttonIndex] != null)
            {
                StartCoroutine(FlashButton(buttonIndex));
            }
            
            // Update feedback text
            if (feedbackText != null)
            {
                feedbackText.text = $"Clicked: {buttonName}";
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
            Debug.Log("AbxrLib: CustomButtonExample - Start button clicked - Starting application");
            
            // Log a level start event
            Abxr.EventLevelStart("custom_button_example", new Abxr.Dict
            {
                ["button_system"] = "custom",
                ["interaction_type"] = "direct_touch"
            });
        }
        
        private void HandleSettingsButton()
        {
            Debug.Log("AbxrLib: CustomButtonExample - Settings button clicked - Opening settings");
            
            // Log an interaction event
            Abxr.EventInteractionComplete("settings_button_click", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "settings_opened");
        }
        
        private void HandleExitButton()
        {
            Debug.Log("AbxrLib: CustomButtonExample - Exit button clicked - Exiting application");
            
            // Log an interaction event
            Abxr.EventInteractionComplete("exit_button_click", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "application_exit");
            
            // In a real application, you might want to quit or return to main menu
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        
        private void HandleHelpButton()
        {
            Debug.Log("AbxrLib: CustomButtonExample - Help button clicked - Showing help");
            
            // Log an interaction event
            Abxr.EventInteractionComplete("help_button_click", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                "help_requested");
        }
        
        private void HandleGenericButton(string buttonName)
        {
            Debug.Log($"AbxrLib: CustomButtonExample - Generic button '{buttonName}' clicked");
            
            // Log a generic interaction event
            Abxr.EventInteractionComplete($"generic_button_click", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                buttonName);
        }
        
        private System.Collections.IEnumerator FlashButton(int buttonIndex)
        {
            if (buttonImages == null || buttonIndex >= buttonImages.Length || buttonImages[buttonIndex] == null)
                yield break;
            
            Image buttonImage = buttonImages[buttonIndex];
            Color originalButtonColor = buttonImage.color;
            
            // Flash to highlight color
            buttonImage.color = flashColor;
            yield return new WaitForSeconds(0.1f);
            
            // Return to original color
            buttonImage.color = originalButtonColor;
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
            Debug.Log($"AbxrLib: CustomButtonExample - Face camera toggled to {!currentSetting}");
            
            if (feedbackText != null)
            {
                feedbackText.text = $"Face Camera: {(!currentSetting ? "Enabled" : "Disabled")}";
            }
        }
    }
}
