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

namespace AbxrLib.Runtime.Services.Telemetry
{
    [DefaultExecutionOrder(100)]
    public class TrackTargetGaze : MonoBehaviour, System.IDisposable
    {
        private static Transform _cachedCameraTransform;
        private static Camera _cachedCamera;

        private void Start()
        {
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
                _cachedCameraTransform = null;
                _cachedCamera = null;
            }
        }

        private void Update()
        {
            if (Time.frameCount % 300 == 0)
            {
                UpdateCameraReference();
            }
        }

        private static void UpdateCameraReference()
        {
            Transform cameraTransform = Utils.FindCameraTransform();
            if (cameraTransform != null)
            {
                _cachedCameraTransform = cameraTransform;
                _cachedCamera = cameraTransform.GetComponent<Camera>();
            }
            else
            {
                _cachedCamera = null;
                _cachedCameraTransform = null;
            }
        }

        private static string DetermineGazeDirection(float horizontalAngle, float verticalAngle, float totalAngle)
        {
            const float centerThreshold = 5f;
            const float significantThreshold = 15f;

            if (totalAngle < centerThreshold)
                return "center";

            string horizontalDir = "";
            if (Mathf.Abs(horizontalAngle) < significantThreshold)
                horizontalDir = "";
            else if (horizontalAngle > 0)
                horizontalDir = "right";
            else
                horizontalDir = "left";

            string verticalDir = "";
            if (Mathf.Abs(verticalAngle) < significantThreshold)
                verticalDir = "";
            else if (verticalAngle > 0)
                verticalDir = "above";
            else
                verticalDir = "below";

            if (string.IsNullOrEmpty(horizontalDir) && string.IsNullOrEmpty(verticalDir))
                return "near_center";
            if (string.IsNullOrEmpty(horizontalDir))
                return verticalDir;
            if (string.IsNullOrEmpty(verticalDir))
                return horizontalDir;
            return $"{verticalDir}_{horizontalDir}";
        }

        /// <summary>
        /// Sends gaze tracking data for all AbxrTarget objects in the scene.
        /// Called when Events occur. Optionally adds gaze scores to the provided event metadata.
        /// </summary>
        public static void SendTargetGazeData(Dictionary<string, string> eventMetadata = null)
        {
            AbxrTarget[] targets = AbxrTarget.GetAllTargets();
            if (targets == null || targets.Length == 0)
                return;

            if (_cachedCameraTransform == null || _cachedCamera == null || !_cachedCamera.gameObject.activeInHierarchy)
                UpdateCameraReference();

            if (_cachedCameraTransform == null || _cachedCamera == null || !_cachedCamera.gameObject.activeInHierarchy)
                return;

            foreach (var target in targets)
            {
                if (target == null || !target.enabled || !target.gameObject.activeInHierarchy || target.transform == null)
                    continue;

                string targetName = target.GetTargetName();
                if (!string.IsNullOrEmpty(targetName))
                    targetName = targetName.Trim();

                Vector3 worldPosition = target.GetWorldPosition();
                float distanceToTarget = Vector3.Distance(_cachedCameraTransform.position, worldPosition);
                bool isOccluded = target.CheckOcclusion(_cachedCameraTransform.position, worldPosition, distanceToTarget);
                float gazeScore = target.CalculateGazeScore(_cachedCameraTransform);

                Vector3 cameraPosition = _cachedCameraTransform.position;
                Vector3 cameraForward = _cachedCameraTransform.forward;
                Vector3 cameraRight = _cachedCameraTransform.right;
                Vector3 cameraUp = _cachedCameraTransform.up;
                Vector3 directionToTarget = (worldPosition - cameraPosition).normalized;

                float horizontalOffset = Vector3.Dot(worldPosition - cameraPosition, cameraRight);
                float verticalOffset = Vector3.Dot(worldPosition - cameraPosition, cameraUp);
                float depthOffset = Vector3.Dot(worldPosition - cameraPosition, cameraForward);

                Vector3 horizontalProjection = Vector3.ProjectOnPlane(directionToTarget, cameraUp).normalized;
                float horizontalAngle = Vector3.SignedAngle(cameraForward, horizontalProjection, cameraUp);
                Vector3 verticalProjection = Vector3.ProjectOnPlane(directionToTarget, cameraRight).normalized;
                float verticalAngle = Vector3.SignedAngle(cameraForward, verticalProjection, cameraRight);
                float totalAngle = Vector3.Angle(cameraForward, directionToTarget);
                float viewAngleDegrees = totalAngle;
                string gazeDirection = DetermineGazeDirection(horizontalAngle, verticalAngle, totalAngle);

                if (eventMetadata != null)
                {
                    string gazeScoreKey = $"gaze_score_{targetName}";
                    eventMetadata[gazeScoreKey] = gazeScore.ToString("F4", CultureInfo.InvariantCulture);
                }

                var telemetryData = new Dictionary<string, string>
                {
                    ["gaze_score"] = gazeScore.ToString("F4", CultureInfo.InvariantCulture),
                    ["target_position_x"] = worldPosition.x.ToString(CultureInfo.InvariantCulture),
                    ["target_position_y"] = worldPosition.y.ToString(CultureInfo.InvariantCulture),
                    ["target_position_z"] = worldPosition.z.ToString(CultureInfo.InvariantCulture),
                    ["target_name"] = targetName,
                    ["distance_to_target"] = distanceToTarget.ToString("F4", CultureInfo.InvariantCulture),
                    ["occluded"] = isOccluded ? "true" : "false",
                    ["gaze_offset_horizontal"] = horizontalOffset.ToString("F4", CultureInfo.InvariantCulture),
                    ["gaze_offset_vertical"] = verticalOffset.ToString("F4", CultureInfo.InvariantCulture),
                    ["gaze_offset_depth"] = depthOffset.ToString("F4", CultureInfo.InvariantCulture),
                    ["gaze_angle_horizontal"] = horizontalAngle.ToString("F2", CultureInfo.InvariantCulture),
                    ["gaze_angle_vertical"] = verticalAngle.ToString("F2", CultureInfo.InvariantCulture),
                    ["gaze_angle_total"] = totalAngle.ToString("F2", CultureInfo.InvariantCulture),
                    ["view_angle_degrees"] = viewAngleDegrees.ToString("F2", CultureInfo.InvariantCulture),
                    ["gaze_direction"] = gazeDirection
                };

                Abxr.Telemetry($"{targetName} Gaze", telemetryData);
            }
        }
    }
}
