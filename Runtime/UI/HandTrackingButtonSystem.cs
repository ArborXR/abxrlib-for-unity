/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Hand Tracking Button System
 * 
 * This file provides a specialized button system for hand tracking applications
 * that use collider-based interactions instead of Unity UI Button components.
 * 
 * Features:
 * - Collider-based button detection for hand tracking
 * - Integration with XR Interaction Toolkit hand tracking
 * - Configurable face camera behavior
 * - Built-in analytics integration
 * - Support for both direct touch and ray casting
 */

using UnityEngine;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.UI.Keyboard;

namespace AbxrLib.Runtime.UI
{
    /// <summary>
    /// Hand tracking button system that works with collider-based interactions
    /// Designed for applications using hand tracking exclusively
    /// </summary>
    public class HandTrackingButtonSystem : MonoBehaviour
    {
        [Header("Hand Tracking Configuration")]
        [Tooltip("Array of collider-based buttons to manage")]
        public HandTrackingButton[] handTrackingButtons;
        
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
        [Tooltip("Prefix for analytics events (e.g., 'hand_button_')")]
        public string analyticsPrefix = "hand_button";
        
        [Header("Hand Tracking Settings")]
        [Tooltip("Layer mask for hand tracking interactions")]
        public LayerMask handTrackingLayerMask = -1;
        
        [Tooltip("Maximum distance for hand tracking interactions")]
        public float maxInteractionDistance = 0.1f;
        
        private Configuration config;
        private bool isInitialized = false;
        
        protected virtual void Start()
        {
            InitializeHandTrackingSystem();
        }
        
        private void InitializeHandTrackingSystem()
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
            
            // Set up hand tracking button listeners
            SetupHandTrackingButtons();
            
            isInitialized = true;
            
            Debug.Log($"[AbxrLib] HandTrackingButtonSystem - Initialized with {(useConfigurationSettings ? "configuration" : "custom")} settings");
        }
        
        private void SetupInteractionMethod(bool useDirectTouch)
        {
            if (useDirectTouch && LaserPointerManager.IsXRInteractionToolkitAvailable())
            {
                LaserPointerManager.EnableLaserPointersForInteraction();
                Debug.Log("[AbxrLib] HandTrackingButtonSystem - Direct touch interaction enabled for hand tracking");
            }
            else
            {
                Debug.Log("[AbxrLib] HandTrackingButtonSystem - Ray casting interaction enabled for hand tracking");
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
                Debug.Log("[AbxrLib] HandTrackingButtonSystem - Face camera behavior enabled");
            }
            else if (useCustomPositioning)
            {
                transform.position = customPosition;
                Debug.Log($"[AbxrLib] HandTrackingButtonSystem - Custom positioning set to {customPosition}");
            }
        }
        
        private void SetupHandTrackingButtons()
        {
            if (handTrackingButtons == null || handTrackingButtons.Length == 0)
            {
                Debug.LogWarning("[AbxrLib] HandTrackingButtonSystem - No hand tracking buttons assigned");
                return;
            }
            
            for (int i = 0; i < handTrackingButtons.Length; i++)
            {
                if (handTrackingButtons[i] != null)
                {
                    // Set up the hand tracking button
                    handTrackingButtons[i].Initialize(this, i, analyticsPrefix);
                    Debug.Log($"[AbxrLib] HandTrackingButtonSystem - Set up hand tracking button {i}");
                }
            }
        }
        
        /// <summary>
        /// Called when a hand tracking button is activated
        /// </summary>
        /// <param name="buttonIndex">Index of the activated button</param>
        /// <param name="buttonName">Name of the activated button</param>
        public void OnHandTrackingButtonActivated(int buttonIndex, string buttonName)
        {
            if (buttonIndex < 0 || buttonIndex >= handTrackingButtons.Length || handTrackingButtons[buttonIndex] == null)
            {
                Debug.LogError($"[AbxrLib] HandTrackingButtonSystem - Invalid button index {buttonIndex}");
                return;
            }
            
            string eventName = $"{analyticsPrefix}_{buttonName.ToLower().Replace(" ", "_")}";
            
            // Log interaction for analytics
            Abxr.EventInteractionComplete(eventName, 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Neutral, 
                buttonName);
            
            Debug.Log($"[AbxrLib] HandTrackingButtonSystem - Hand tracking button '{buttonName}' activated, logged as '{eventName}'");
            
            // Call custom button handler
            OnHandTrackingButtonClick(buttonIndex, buttonName);
        }
        
        /// <summary>
        /// Override this method to handle custom hand tracking button click logic
        /// </summary>
        /// <param name="buttonIndex">Index of the clicked button</param>
        /// <param name="buttonName">Name of the clicked button</param>
        protected virtual void OnHandTrackingButtonClick(int buttonIndex, string buttonName)
        {
            // Override in derived classes for custom behavior
            Debug.Log($"[AbxrLib] HandTrackingButtonSystem - Hand tracking button click handler - Button {buttonIndex}: {buttonName}");
        }
        
        /// <summary>
        /// Manually refresh the hand tracking system configuration
        /// Useful when configuration changes at runtime
        /// </summary>
        public void RefreshConfiguration()
        {
            isInitialized = false;
            InitializeHandTrackingSystem();
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
            
            Debug.Log($"[AbxrLib] HandTrackingButtonSystem - Direct touch interaction {(enable ? "enabled" : "disabled")}");
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
            // Ensure hand tracking buttons array is properly set up
            if (handTrackingButtons != null)
            {
                for (int i = 0; i < handTrackingButtons.Length; i++)
                {
                    if (handTrackingButtons[i] == null)
                    {
                        Debug.LogWarning($"[AbxrLib] HandTrackingButtonSystem - Hand tracking button at index {i} is null");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Individual hand tracking button component
    /// Attach this to GameObjects with colliders for hand tracking interaction
    /// </summary>
    [System.Serializable]
    public class HandTrackingButton
    {
        [Header("Button Configuration")]
        [Tooltip("Name of this button (used for analytics)")]
        public string buttonName = "Button";
        
        [Tooltip("GameObject with collider for hand tracking interaction")]
        public GameObject buttonObject;
        
        [Tooltip("Collider component for hand tracking detection")]
        public Collider buttonCollider;
        
        [Tooltip("Visual feedback when button is activated")]
        public GameObject visualFeedback;
        
        [Tooltip("Audio feedback when button is activated")]
        public AudioSource audioFeedback;
        
        [Header("Interaction Settings")]
        [Tooltip("Cooldown time between activations (seconds)")]
        public float cooldownTime = 0.5f;
        
        [Tooltip("Visual feedback duration (seconds)")]
        public float visualFeedbackDuration = 0.2f;
        
        private HandTrackingButtonSystem parentSystem;
        private int buttonIndex;
        private string analyticsPrefix;
        private float lastActivationTime;
        private bool isInitialized = false;
        
        /// <summary>
        /// Initialize the hand tracking button
        /// </summary>
        /// <param name="system">Parent hand tracking button system</param>
        /// <param name="index">Index of this button in the system</param>
        /// <param name="prefix">Analytics prefix for events</param>
        public void Initialize(HandTrackingButtonSystem system, int index, string prefix)
        {
            parentSystem = system;
            buttonIndex = index;
            analyticsPrefix = prefix;
            
            // Auto-find collider if not assigned
            if (buttonCollider == null && buttonObject != null)
            {
                buttonCollider = buttonObject.GetComponent<Collider>();
            }
            
            // Ensure collider is set up for hand tracking
            if (buttonCollider != null)
            {
                // Make sure collider is a trigger for hand tracking
                buttonCollider.isTrigger = true;
                
                // Add hand tracking interaction component if not present
                if (buttonObject.GetComponent<HandTrackingButtonInteraction>() == null)
                {
                    var interaction = buttonObject.AddComponent<HandTrackingButtonInteraction>();
                    interaction.Initialize(this);
                }
            }
            else
            {
                Debug.LogError($"[AbxrLib] HandTrackingButton - No collider found for button '{buttonName}'");
            }
            
            isInitialized = true;
            Debug.Log($"[AbxrLib] HandTrackingButton - Initialized button '{buttonName}'");
        }
        
        /// <summary>
        /// Activate this button (called by hand tracking interaction)
        /// </summary>
        public void Activate()
        {
            if (!isInitialized)
            {
                Debug.LogWarning($"[AbxrLib] HandTrackingButton - Button '{buttonName}' not initialized");
                return;
            }
            
            // Check cooldown
            if (Time.time - lastActivationTime < cooldownTime)
            {
                return;
            }
            
            lastActivationTime = Time.time;
            
            // Provide visual feedback
            if (visualFeedback != null)
            {
                StartVisualFeedback();
            }
            
            // Provide audio feedback
            if (audioFeedback != null)
            {
                audioFeedback.Play();
            }
            
            // Notify parent system
            if (parentSystem != null)
            {
                parentSystem.OnHandTrackingButtonActivated(buttonIndex, buttonName);
            }
        }
        
        private void StartVisualFeedback()
        {
            if (visualFeedback != null)
            {
                visualFeedback.SetActive(true);
                // Note: In a real implementation, you'd want to use a coroutine or animation system
                // to turn off the visual feedback after visualFeedbackDuration seconds
            }
        }
    }
    
    /// <summary>
    /// Hand tracking interaction component for individual buttons
    /// Handles the actual hand tracking detection and button activation
    /// </summary>
    public class HandTrackingButtonInteraction : MonoBehaviour
    {
        private HandTrackingButton button;
        private bool isInitialized = false;
        
        public void Initialize(HandTrackingButton handTrackingButton)
        {
            button = handTrackingButton;
            isInitialized = true;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!isInitialized || button == null) return;
            
            // Check if the collider belongs to a hand (you may need to adjust this based on your hand tracking setup)
            if (IsHandCollider(other))
            {
                button.Activate();
            }
        }
        
        private bool IsHandCollider(Collider other)
        {
            // This is a basic implementation - you may need to customize this based on your hand tracking setup
            // Common approaches:
            // 1. Check for specific tags (e.g., "Hand", "Finger")
            // 2. Check for specific layer
            // 3. Check for specific component types
            // 4. Check for specific naming conventions
            
            return other.CompareTag("Hand") || 
                   other.CompareTag("Finger") || 
                   other.CompareTag("HandTracking") ||
                   other.gameObject.layer == LayerMask.NameToLayer("HandTracking");
        }
    }
}
