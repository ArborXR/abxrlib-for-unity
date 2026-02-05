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

                // Add gaze score to Event metadata if provided
                // Format: "gaze_score_{targetName}" = "0.9310"
                if (eventMetadata != null)
                {
                    string gazeScoreKey = $"gaze_score_{targetName}";
                    eventMetadata[gazeScoreKey] = gazeScore.ToString("F4", CultureInfo.InvariantCulture);
                }

                // Create telemetry metadata
                var telemetryData = new Dictionary<string, string>
                {
                    ["gaze_score"] = gazeScore.ToString("F4", CultureInfo.InvariantCulture),
                    ["target_position_x"] = worldPosition.x.ToString(CultureInfo.InvariantCulture),
                    ["target_position_y"] = worldPosition.y.ToString(CultureInfo.InvariantCulture),
                    ["target_position_z"] = worldPosition.z.ToString(CultureInfo.InvariantCulture),
                    ["target_name"] = targetName,
                    ["distance_to_target"] = distanceToTarget.ToString("F4", CultureInfo.InvariantCulture),
                    ["occluded"] = isOccluded ? "true" : "false"
                };

                // Send telemetry with targetName in the telemetry entry name
                Abxr.Telemetry($"{targetName} Gaze", telemetryData);
            }
        }
    }
}
