/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Custom Button System
 * 
 * This file provides a custom button system that integrates with AbxrLib's
 * configuration system and supports both direct touch and ray casting interactions.
 * 
 * Features:
 * - Configurable interaction methods (direct touch vs ray casting)
 * - Integration with AbxrLib analytics
 * - Support for custom positioning and face camera behavior
 * - Easy setup and configuration through Unity Inspector
 */

using UnityEngine;
using UnityEngine.UI;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.UI.Keyboard;

namespace AbxrLib.Runtime.UI
{
    /// <summary>
    /// Custom button system that integrates with AbxrLib configuration
    /// Supports both direct touch and ray casting interactions
    /// </summary>
    public class CustomButtonSystem : MonoBehaviour
    {
        [Header("Button Configuration")]
        [Tooltip("Array of buttons to manage")]
        public Button[] customButtons;
        
        [Tooltip("Use AbxrLib configuration for interaction settings")]
        public bool useConfigurationSettings = true;
        
        [Header("Custom Settings (used when useConfigurationSettings is false)")]
        [Tooltip("Enable direct touch interaction")]
        public bool enableDirectTouch = true;
        
        [Tooltip("Enable face camera behavior")]
        public bool enableFaceCamera = false;
        
        [Header("Positioning")]
        [Tooltip("Custom position for the button panel (used when face camera is disabled)")]
        public Vector3 customPosition = Vector3.zero;
        
        [Tooltip("Use custom positioning instead of face camera")]
        public bool useCustomPositioning = false;
        
        [Header("Analytics")]
        [Tooltip("Prefix for analytics events (e.g., 'custom_button_')")]
        public string analyticsPrefix = "custom_button";
        
        private Configuration config;
        private bool isInitialized = false;
        
        protected virtual void Start()
        {
            InitializeButtonSystem();
        }
        
        protected void InitializeButtonSystem()
        {
            if (isInitialized) return;
            
            config = Configuration.Instance;
            
            // Set up interaction method based on configuration
            if (useConfigurationSettings)
            {
                SetupInteractionMethod(config.enableDirectTouchInteraction);
                SetupPositioning(config.authUIFollowCamera);
            }
            else
            {
                SetupInteractionMethod(enableDirectTouch);
                SetupPositioning(enableFaceCamera);
            }
            
            // Set up button event listeners
            SetupButtonListeners();
            
            isInitialized = true;
            
            Debug.Log($"AbxrLib - CustomButtonSystem: Initialized with {(useConfigurationSettings ? "configuration" : "custom")} settings");
        }
        
        private void SetupInteractionMethod(bool useDirectTouch)
        {
            if (useDirectTouch && LaserPointerManager.IsXRInteractionToolkitAvailable())
            {
                LaserPointerManager.EnableLaserPointersForInteraction();
                Debug.Log("AbxrLib - CustomButtonSystem: Direct touch interaction enabled");
            }
            else
            {
                Debug.Log("AbxrLib - CustomButtonSystem: Ray casting interaction enabled");
            }
        }
        
        private void SetupPositioning(bool faceCamera)
        {
            if (faceCamera)
            {
                // Add FaceCamera component if not present
                FaceCamera faceCameraComponent = GetComponent<FaceCamera>();
                if (faceCameraComponent == null)
                {
                    faceCameraComponent = gameObject.AddComponent<FaceCamera>();
                    faceCameraComponent.useConfigurationValues = useConfigurationSettings;
                }
                Debug.Log("AbxrLib - CustomButtonSystem: Face camera behavior enabled");
            }
            else if (useCustomPositioning)
            {
                transform.position = customPosition;
                Debug.Log($"AbxrLib - CustomButtonSystem: Custom positioning set to {customPosition}");
            }
        }
        
        private void SetupButtonListeners()
        {
            if (customButtons == null || customButtons.Length == 0)
            {
                Debug.LogWarning("AbxrLib - CustomButtonSystem: No buttons assigned to customButtons array");
                return;
            }
            
            for (int i = 0; i < customButtons.Length; i++)
            {
                if (customButtons[i] != null)
                {
                    int buttonIndex = i; // Capture for closure
                    customButtons[i].onClick.AddListener(() => OnButtonClicked(buttonIndex));
                    Debug.Log($"AbxrLib - CustomButtonSystem: Set up listener for button {i}");
                }
            }
        }
        
        private void OnButtonClicked(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= customButtons.Length || customButtons[buttonIndex] == null)
            {
                Debug.LogError($"AbxrLib - CustomButtonSystem: Invalid button index {buttonIndex}");
                return;
            }
            
            string buttonName = customButtons[buttonIndex].name;
            string eventName = $"{analyticsPrefix}_{buttonName.ToLower().Replace(" ", "_")}";
            
            // Log interaction for analytics
            Abxr.EventInteractionComplete(eventName, 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                buttonName);
            
            Debug.Log($"AbxrLib - CustomButtonSystem: Button '{buttonName}' clicked, logged as '{eventName}'");
            
            // Call custom button handler
            OnCustomButtonClick(buttonIndex, buttonName);
        }
        
        /// <summary>
        /// Override this method to handle custom button click logic
        /// </summary>
        /// <param name="buttonIndex">Index of the clicked button</param>
        /// <param name="buttonName">Name of the clicked button</param>
        protected virtual void OnCustomButtonClick(int buttonIndex, string buttonName)
        {
            // Override in derived classes for custom behavior
            Debug.Log($"AbxrLib - CustomButtonSystem: Custom button click handler - Button {buttonIndex}: {buttonName}");
        }
        
        /// <summary>
        /// Manually refresh the button system configuration
        /// Useful when configuration changes at runtime
        /// </summary>
        public void RefreshConfiguration()
        {
            isInitialized = false;
            InitializeButtonSystem();
        }
        
        /// <summary>
        /// Enable or disable direct touch interaction at runtime
        /// </summary>
        /// <param name="enable">True to enable direct touch, false to disable</param>
        public void SetDirectTouchEnabled(bool enable)
        {
            if (enable && LaserPointerManager.IsXRInteractionToolkitAvailable())
            {
                LaserPointerManager.EnableLaserPointersForInteraction();
            }
            else
            {
                LaserPointerManager.RestoreLaserPointerStates();
            }
            
            Debug.Log($"AbxrLib - CustomButtonSystem: Direct touch interaction {(enable ? "enabled" : "disabled")}");
        }
        
        private void OnDestroy()
        {
            // Clean up laser pointer management
            if (LaserPointerManager.IsManagingLaserPointers)
            {
                LaserPointerManager.RestoreLaserPointerStates();
            }
        }
        
        private void OnValidate()
        {
            // Ensure custom buttons array is properly set up
            if (customButtons != null)
            {
                for (int i = 0; i < customButtons.Length; i++)
                {
                    if (customButtons[i] == null)
                    {
                        Debug.LogWarning($"AbxrLib - CustomButtonSystem: Button at index {i} is null");
                    }
                }
            }
        }
    }
}
