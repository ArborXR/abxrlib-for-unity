/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Target Gaze Tracking
 * 
 * This class tracks gaze scores for all AbxrTarget objects in the scene when Events occur.
 * Calculates how directly users are looking at each target and includes this data in telemetry.
 */

using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Runtime.Telemetry
{
    [DefaultExecutionOrder(100)] // Doesn't matter when this one runs
    public class TrackTargetGaze : MonoBehaviour, System.IDisposable
    {
        // Static cached camera reference to avoid repeated lookups
        private static Transform _cachedCameraTransform;
        private static Camera _cachedCamera;

        private void Start()
        {
            // Initialize camera reference
            UpdateCameraReference();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clear cached references
                _cachedCameraTransform = null;
                _cachedCamera = null;
            }
        }

        private void Update()
        {
            // Periodically update camera reference in case it changes
            // Check every few seconds to avoid overhead
            if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
            {
                UpdateCameraReference();
            }
        }

        /// <summary>
        /// Updates the cached camera reference.
        /// Tries Camera.main first, then falls back to finding XR camera.
        /// </summary>
        private static void UpdateCameraReference()
        {
            if (Camera.main != null)
            {
                _cachedCamera = Camera.main;
                _cachedCameraTransform = Camera.main.transform;
                return;
            }

            // Fallback: Try to find XR camera via InputDevices
            try
            {
                var hmd = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                if (hmd.isValid)
                {
                    // Try to find camera in scene that matches HMD
                    Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                    foreach (var cam in cameras)
                    {
                        if (cam.enabled && cam.gameObject.activeInHierarchy)
                        {
                            _cachedCamera = cam;
                            _cachedCameraTransform = cam.transform;
                            return;
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // Ignore errors - camera might not be available yet
            }

            // If no camera found, clear references
            _cachedCamera = null;
            _cachedCameraTransform = null;
        }

        /// <summary>
        /// Determines a categorical gaze direction based on horizontal and vertical angles.
        /// Helps with analyzing user behavior patterns.
        /// </summary>
        /// <param name="horizontalAngle">Horizontal angle in degrees (positive = right, negative = left)</param>
        /// <param name="verticalAngle">Vertical angle in degrees (positive = up, negative = down)</param>
        /// <param name="totalAngle">Total angle from forward direction in degrees</param>
        /// <returns>Categorical direction string (e.g., "center", "above_right", "left", etc.)</returns>
        private static string DetermineGazeDirection(float horizontalAngle, float verticalAngle, float totalAngle)
        {
            // Threshold angles for determining direction (in degrees)
            const float centerThreshold = 5f; // Within 5 degrees = "center"
            const float significantThreshold = 15f; // Beyond 15 degrees = significant offset
            
            // If very close to center, return "center"
            if (totalAngle < centerThreshold)
            {
                return "center";
            }
            
            // Determine horizontal component
            string horizontalDir = "";
            if (Mathf.Abs(horizontalAngle) < significantThreshold)
            {
                horizontalDir = ""; // Centered horizontally
            }
            else if (horizontalAngle > 0)
            {
                horizontalDir = "right";
            }
            else
            {
                horizontalDir = "left";
            }
            
            // Determine vertical component
            string verticalDir = "";
            if (Mathf.Abs(verticalAngle) < significantThreshold)
            {
                verticalDir = ""; // Centered vertically
            }
            else if (verticalAngle > 0)
            {
                verticalDir = "above";
            }
            else
            {
                verticalDir = "below";
            }
            
            // Combine directions
            if (string.IsNullOrEmpty(horizontalDir) && string.IsNullOrEmpty(verticalDir))
            {
                return "near_center";
            }
            else if (string.IsNullOrEmpty(horizontalDir))
            {
                return verticalDir;
            }
            else if (string.IsNullOrEmpty(verticalDir))
            {
                return horizontalDir;
            }
            else
            {
                return $"{verticalDir}_{horizontalDir}";
            }
        }

        /// <summary>
        /// Sends gaze tracking data for all AbxrTarget objects in the scene.
        /// This method is called when Events occur to include target gaze information in telemetry.
        /// Optionally adds gaze scores to Event metadata if provided.
        /// </summary>
        /// <param name="eventMetadata">Optional metadata dictionary to add gaze scores to (e.g., Event metadata)</param>
        public static void SendTargetGazeData(Dictionary<string, string> eventMetadata = null)
        {
            // Early return optimization: Check if any targets exist before doing any work
            // This ensures zero overhead when no targets are present
            AbxrTarget[] targets = UnityEngine.Object.FindObjectsOfType<AbxrTarget>();
            if (targets == null || targets.Length == 0)
            {
                return; // No targets, exit immediately with zero overhead
            }

            // Update camera reference if needed
            if (_cachedCameraTransform == null)
            {
                UpdateCameraReference();
            }

            // If still no camera, can't calculate gaze
            if (_cachedCameraTransform == null)
            {
                return;
            }

            // Calculate and send gaze data for each target
            foreach (var target in targets)
            {
                // Skip null targets
                if (target == null)
                {
                    continue;
                }

                // Skip targets where the AbxrTarget component itself is disabled
                // This check must come before other checks to ensure disabled targets are excluded
                if (!target.enabled)
                {
                    continue;
                }

                // Skip inactive GameObjects
                if (!target.gameObject.activeInHierarchy)
                {
                    continue; // Skip disabled or destroyed targets
                }

                // Skip if transform is null (defensive check)
                if (target.transform == null)
                {
                    continue;
                }

                // Get targetName (custom targetName or GameObject name)
                string targetName = target.GetTargetName();
                // Trim whitespace to prevent extra spaces in metadata keys
                if (!string.IsNullOrEmpty(targetName))
                {
                    targetName = targetName.Trim();
                }

                // Get world position using the helper method that handles child objects correctly
                Vector3 worldPosition = target.GetWorldPosition();

                // Calculate distance from camera to target
                float distanceToTarget = Vector3.Distance(_cachedCameraTransform.position, worldPosition);

                // Check occlusion status
                bool isOccluded = target.CheckOcclusion(_cachedCameraTransform.position, worldPosition, distanceToTarget);

                // Calculate gaze score (uses the same GetWorldPosition method internally)
                float gazeScore = target.CalculateGazeScore(_cachedCameraTransform);

                // Calculate camera-relative position offsets
                Vector3 cameraPosition = _cachedCameraTransform.position;
                Vector3 cameraForward = _cachedCameraTransform.forward;
                Vector3 cameraRight = _cachedCameraTransform.right;
                Vector3 cameraUp = _cachedCameraTransform.up;
                
                // Direction from camera to target
                Vector3 directionToTarget = (worldPosition - cameraPosition).normalized;
                
                // Calculate offsets in camera-relative space (in world units)
                // Right = positive, Left = negative
                float horizontalOffset = Vector3.Dot(worldPosition - cameraPosition, cameraRight);
                // Up = positive, Down = negative
                float verticalOffset = Vector3.Dot(worldPosition - cameraPosition, cameraUp);
                // Forward = positive, Back = negative
                float depthOffset = Vector3.Dot(worldPosition - cameraPosition, cameraForward);
                
                // Calculate angular offsets from camera forward direction
                // Project direction onto horizontal plane (right-forward plane)
                Vector3 horizontalProjection = Vector3.ProjectOnPlane(directionToTarget, cameraUp).normalized;
                float horizontalAngle = Vector3.SignedAngle(cameraForward, horizontalProjection, cameraUp);
                // Positive = right, negative = left
                
                // Project direction onto vertical plane (up-forward plane)
                Vector3 verticalProjection = Vector3.ProjectOnPlane(directionToTarget, cameraRight).normalized;
                float verticalAngle = Vector3.SignedAngle(cameraForward, verticalProjection, cameraRight);
                // Positive = up, negative = down
                
                // Total angle from forward direction (0-180 degrees)
                float totalAngle = Vector3.Angle(cameraForward, directionToTarget);
                
                // Calculate view angle (same as total angle, but kept for clarity)
                float viewAngleDegrees = totalAngle;
                
                // Determine categorical gaze direction for easier analysis
                string gazeDirection = DetermineGazeDirection(horizontalAngle, verticalAngle, totalAngle);
                
                // Add gaze score to Event metadata if provided
                // Format: "gaze_score_{targetName}" = "0.9310"
                if (eventMetadata != null)
                {
                    string gazeScoreKey = $"gaze_score_{targetName}";
                    eventMetadata[gazeScoreKey] = gazeScore.ToString("F4", CultureInfo.InvariantCulture);
                }

                // Create telemetry metadata with enhanced gaze information
                var telemetryData = new Dictionary<string, string>
                {
                    ["gaze_score"] = gazeScore.ToString("F4", CultureInfo.InvariantCulture),
                    ["target_position_x"] = worldPosition.x.ToString(CultureInfo.InvariantCulture),
                    ["target_position_y"] = worldPosition.y.ToString(CultureInfo.InvariantCulture),
                    ["target_position_z"] = worldPosition.z.ToString(CultureInfo.InvariantCulture),
                    ["target_name"] = targetName,
                    ["distance_to_target"] = distanceToTarget.ToString("F4", CultureInfo.InvariantCulture),
                    ["occluded"] = isOccluded ? "true" : "false",
                    
                    // Camera-relative position offsets (in world units)
                    ["gaze_offset_horizontal"] = horizontalOffset.ToString("F4", CultureInfo.InvariantCulture),
                    ["gaze_offset_vertical"] = verticalOffset.ToString("F4", CultureInfo.InvariantCulture),
                    ["gaze_offset_depth"] = depthOffset.ToString("F4", CultureInfo.InvariantCulture),
                    
                    // Angular measurements (in degrees)
                    ["gaze_angle_horizontal"] = horizontalAngle.ToString("F2", CultureInfo.InvariantCulture),
                    ["gaze_angle_vertical"] = verticalAngle.ToString("F2", CultureInfo.InvariantCulture),
                    ["gaze_angle_total"] = totalAngle.ToString("F2", CultureInfo.InvariantCulture),
                    ["view_angle_degrees"] = viewAngleDegrees.ToString("F2", CultureInfo.InvariantCulture),
                    
                    // Categorical direction for easier analysis
                    ["gaze_direction"] = gazeDirection
                };

                // Send telemetry with targetName in the telemetry entry name
                Abxr.Telemetry($"{targetName} Gaze", telemetryData);
            }
        }
    }
}
