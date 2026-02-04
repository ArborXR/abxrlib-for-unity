/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - AbxrTarget Component
 * 
 * This component can be attached to GameObjects to track whether users are looking at them.
 * When Events occur, the system calculates a gaze score (0-1) indicating how directly
 * the user is looking at each target. This data is included in telemetry metadata.
 */

using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Runtime.Telemetry
{
    /// <summary>
    /// Component that marks a GameObject as a gaze tracking target.
    /// When Events occur, the system calculates how directly the user is looking at this target.
    /// </summary>
    [AddComponentMenu("Analytics for XR/Abxr Target")]
    public class AbxrTarget : MonoBehaviour
    {
        [Tooltip("Custom name for this target (e.g., 'head', 'leftleg', 'button1'). If empty, uses GameObject name.")]
        [SerializeField]
        public string targetName = "";

        [Tooltip("Layers that should block line-of-sight (e.g., walls, obstacles). Objects on these layers will occlude the target.")]
        [SerializeField]
        public LayerMask occlusionLayers = -1; // Default to all layers

        [Tooltip("Maximum distance for line-of-sight check. Targets beyond this distance won't be checked for occlusion. 0 = use global default from Configuration.")]
        [SerializeField]
        public float maxOcclusionCheckDistance = 0f; // 0 = use global default

        [Tooltip("Automatically create a trigger collider if none exists. Trigger colliders don't interfere with physics or interactions - they're only used for raycast detection. Initializes from global default in Configuration.")]
        [SerializeField]
        public bool autoCreateTriggerCollider = true; // Initializes from Configuration.defaultAutoCreateTriggerCollider
        
        /// <summary>
        /// Gets whether to automatically create a trigger collider.
        /// Uses local value (which defaults to the global default when components are created).
        /// The global default in Configuration primarily affects new components.
        /// </summary>
        private bool GetEffectiveAutoCreateTriggerCollider()
        {
            // Use local value - it was initialized from global default when component was created
            // Users can override per-component by changing autoCreateTriggerCollider directly
            return autoCreateTriggerCollider;
        }

        [Tooltip("Size of the auto-created trigger collider (if autoCreateTriggerCollider is enabled).")]
        [SerializeField]
        public float triggerColliderSize = 0.5f; // Default to 0.5 unit radius/sphere

        /// <summary>
        /// Gets the effective maximum occlusion check distance.
        /// Returns the local maxOcclusionCheckDistance if set (non-zero), otherwise returns the global default from Configuration.
        /// </summary>
        /// <returns>Effective maximum distance for occlusion checks (0 = unlimited)</returns>
        public float GetEffectiveMaxOcclusionCheckDistance()
        {
            // If local value is set (non-zero), use it
            if (maxOcclusionCheckDistance > 0f)
            {
                return maxOcclusionCheckDistance;
            }
            
            // Otherwise, use global default from Configuration
            return Configuration.Instance.defaultMaxOcclusionCheckDistance;
        }

        /// <summary>
        /// Gets the display name for this target.
        /// Returns custom targetName if set, otherwise falls back to GameObject name.
        /// </summary>
        public string GetTargetName()
        {
            return string.IsNullOrEmpty(targetName) ? gameObject.name : targetName;
        }

        /// <summary>
        /// Checks if this target (or any of its children) has a collider.
        /// This is useful for debugging occlusion issues.
        /// </summary>
        /// <returns>True if target has at least one collider, false otherwise</returns>
        public bool HasCollider()
        {
            // Check self
            if (GetComponent<Collider>() != null)
                return true;
            
            // Check children
            Collider[] childColliders = GetComponentsInChildren<Collider>();
            return childColliders != null && childColliders.Length > 0;
        }

        /// <summary>
        /// Ensures this target has a trigger collider for raycast detection.
        /// Trigger colliders don't interfere with physics or interactions - they're only used for detection.
        /// This is called automatically if autoCreateTriggerCollider is enabled.
        /// </summary>
        private void EnsureTriggerCollider()
        {
            // Use the effective value (local override or global default)
            // The local value defaults to true, but users can override it
            // The global default primarily affects new components, but we check it here for consistency
            bool shouldCreate = GetEffectiveAutoCreateTriggerCollider();
            
            if (!shouldCreate)
                return;

            // Check if we already have a collider (including children)
            if (HasCollider())
                return;

            // Create a small sphere trigger collider
            // Sphere colliders work well for point targets and are efficient
            SphereCollider triggerCollider = gameObject.AddComponent<SphereCollider>();
            triggerCollider.isTrigger = true; // Critical: trigger colliders don't block physics
            triggerCollider.radius = triggerColliderSize;
            triggerCollider.center = Vector3.zero; // Center at the GameObject's pivot

            // Hide it in the inspector to reduce clutter (optional, but helpful)
            // Note: This doesn't affect functionality, just makes it less visible in the inspector
        }

        private void Reset()
        {
            // Initialize from global default when component is first added or reset
            // This allows the global default to control new components
            autoCreateTriggerCollider = Configuration.Instance.defaultAutoCreateTriggerCollider;
        }

        private void Awake()
        {
            // Ensure we have a trigger collider for accurate detection
            // Using Awake() instead of Start() ensures colliders are created before any occlusion checks
            EnsureTriggerCollider();
        }

        /// <summary>
        /// Gets the world position of this target, handling child objects correctly.
        /// Always calculates world position correctly, even for child objects in hierarchies.
        /// </summary>
        /// <returns>World position of the target</returns>
        public Vector3 GetWorldPosition()
        {
            // Use transform.position (should be correct in most cases)
            Vector3 worldPosition = transform.position;
            
            // If world position is (0,0,0) and we have a parent, recalculate using TransformPoint
            // This handles edge cases where transform.position might return incorrect values
            if (worldPosition == Vector3.zero && transform.parent != null)
            {
                // TransformPoint converts local position to world position through the parent hierarchy
                worldPosition = transform.parent.TransformPoint(transform.localPosition);
            }
            
            // If still zero, try using the root transform
            if (worldPosition == Vector3.zero && transform.root != transform)
            {
                worldPosition = transform.root.TransformPoint(transform.localPosition);
            }
            
            // Final debug check - if still zero, log warning
            if (worldPosition == Vector3.zero && transform.localPosition != Vector3.zero)
            {
                Debug.LogWarning($"AbxrLib: AbxrTarget '{GetTargetName()}' - Unable to calculate world position. " +
                               $"Local: {transform.localPosition}, Parent: {(transform.parent != null ? transform.parent.name : "None")}, " +
                               $"Parent World: {(transform.parent != null ? transform.parent.position.ToString() : "N/A")}");
            }
            
            return worldPosition;
        }

        /// <summary>
        /// Calculates the gaze score (0-1) indicating how directly the camera is looking at this target.
        /// 1.0 = directly looking at target, 0.5 = perpendicular, 0.0 = looking away.
        /// Optionally checks for line-of-sight occlusion if enabled.
        /// </summary>
        /// <param name="cameraTransform">The camera transform to check gaze from</param>
        /// <returns>Normalized gaze score from 0.0 to 1.0</returns>
        public float CalculateGazeScore(Transform cameraTransform)
        {
            if (cameraTransform == null || !gameObject.activeInHierarchy)
            {
                return 0f;
            }

            // Get world position (handles child objects correctly)
            Vector3 targetWorldPosition = GetWorldPosition();

            // Calculate direction from camera to target
            Vector3 directionToTarget = (targetWorldPosition - cameraTransform.position).normalized;
            float distanceToTarget = Vector3.Distance(cameraTransform.position, targetWorldPosition);
            
            // Get camera forward direction
            Vector3 cameraForward = cameraTransform.forward;

            // Calculate dot product (ranges from -1 to 1)
            // 1 = directly looking at target, 0 = perpendicular, -1 = looking away
            float dotProduct = Vector3.Dot(cameraForward, directionToTarget);

            // Normalize to 0-1 range: (dotProduct + 1) / 2
            // This gives us: 1.0 = directly looking, 0.5 = perpendicular, 0.0 = looking away
            float gazeScore = (dotProduct + 1f) / 2f;

            // Always check for line-of-sight occlusion (required)
            // If view is blocked, set gaze score to 0
            if (gazeScore > 0f)
            {
                bool isOccluded = CheckLineOfSight(cameraTransform.position, targetWorldPosition, distanceToTarget);
                if (isOccluded)
                {
                    // View is blocked by an obstacle - set gaze score to 0
                    gazeScore = 0f;
                }
            }

            return Mathf.Clamp01(gazeScore);
        }

        /// <summary>
        /// Checks if there's a clear line-of-sight from the camera to the target.
        /// Uses raycasting to detect if any objects are blocking the view.
        /// Targets beyond maxOcclusionCheckDistance are treated as occluded (too far to see).
        /// </summary>
        /// <param name="fromPosition">Starting position (camera)</param>
        /// <param name="toPosition">Target position</param>
        /// <param name="distance">Distance to target</param>
        /// <returns>True if view is blocked (occluded), false if clear line-of-sight</returns>
        public bool CheckOcclusion(Vector3 fromPosition, Vector3 toPosition, float distance)
        {
            return CheckLineOfSight(fromPosition, toPosition, distance);
        }

        /// <summary>
        /// Checks if there's a clear line-of-sight from the camera to the target.
        /// Uses raycasting to detect if any objects are blocking the view.
        /// Targets beyond maxOcclusionCheckDistance are treated as occluded (too far to see).
        /// </summary>
        /// <param name="fromPosition">Starting position (camera)</param>
        /// <param name="toPosition">Target position</param>
        /// <param name="distance">Distance to target</param>
        /// <returns>True if view is blocked (occluded), false if clear line-of-sight</returns>
        private bool CheckLineOfSight(Vector3 fromPosition, Vector3 toPosition, float distance)
        {
            // Get effective max distance (local value or global default)
            float effectiveMaxDistance = GetEffectiveMaxOcclusionCheckDistance();
            
            // Check max distance if specified
            // If target is beyond max distance, treat it as occluded (too far to see)
            if (effectiveMaxDistance > 0f && distance > effectiveMaxDistance)
            {
                return true; // Too far, treat as occluded
            }

            Vector3 direction = (toPosition - fromPosition).normalized;
            float maxDistance = effectiveMaxDistance > 0f ? effectiveMaxDistance : distance;

            // Use RaycastAll to check all hits along the ray
            // This ensures we detect the target even if something else is hit first
            // Sort hits by distance to process them in order
            RaycastHit[] hits = Physics.RaycastAll(fromPosition, direction, maxDistance, occlusionLayers);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            
            // Check if target has a collider for accurate detection
            bool targetHasCollider = HasCollider();
            if (!targetHasCollider && hits.Length > 0)
            {
                // Only warn if auto-create is disabled (user explicitly chose not to use trigger colliders)
                if (!autoCreateTriggerCollider)
                {
                    Debug.LogWarning($"AbxrLib: Target '{GetTargetName()}' has no collider - occlusion detection may be less accurate. " +
                                   $"Enable 'Auto Create Trigger Collider' on the AbxrTarget component for more accurate detection. " +
                                   $"Trigger colliders don't interfere with physics or interactions.");
                }
            }
            
            if (hits.Length == 0)
            {
                // No hits means clear line-of-sight
                return false;
            }

            // Helper function to check if a transform is part of the target hierarchy
            bool IsPartOfTargetHierarchy(Transform checkTransform)
            {
                // Check if it's the target itself
                if (checkTransform == transform)
                    return true;
                
                // Check if it's a child of the target
                if (checkTransform.IsChildOf(transform))
                    return true;
                
                // Check if it's a parent of the target
                Transform currentParent = transform.parent;
                while (currentParent != null)
                {
                    if (checkTransform == currentParent)
                        return true;
                    currentParent = currentParent.parent;
                }
                
                return false;
            }

            // Find the closest hit that is NOT part of the target hierarchy
            float closestNonTargetHitDistance = float.MaxValue;
            Transform closestNonTargetHit = null;
            Vector3 closestNonTargetHitPoint = Vector3.zero;
            
            // Also track if we hit the target at all
            bool hitTarget = false;
            float targetHitDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                
                if (IsPartOfTargetHierarchy(hitTransform))
                {
                    // This is part of the target hierarchy
                    hitTarget = true;
                    if (hit.distance < targetHitDistance)
                    {
                        targetHitDistance = hit.distance;
                    }
                }
                else
                {
                    // This is something else - potential occluder
                    if (hit.distance < closestNonTargetHitDistance)
                    {
                        closestNonTargetHitDistance = hit.distance;
                        closestNonTargetHit = hitTransform;
                        closestNonTargetHitPoint = hit.point;
                    }
                }
            }

            // If we hit the target, check if anything non-target is blocking it
            if (hitTarget)
            {
                // If something non-target is closer than the target, it's occluded
                if (closestNonTargetHit != null && closestNonTargetHitDistance < targetHitDistance)
                {
                    return true;
                }
                // Target is hit and nothing is blocking it
                return false;
            }
            else
            {
                // Didn't hit the target at all - this could mean:
                // 1. Target has no collider (or collider is on excluded layer)
                // 2. Something is blocking the view before reaching the target
                // 
                // Check if any hit is closer than the target's actual distance
                // If something hits before reaching the target distance, it's occluded
                // If nothing hits, or all hits are beyond the target, line-of-sight is clear
                if (closestNonTargetHit != null)
                {
                    // Something was hit - check if it's blocking the target
                    // Use a small tolerance (0.1m) to account for floating point precision and target size
                    // This helps when targets don't have colliders - we give them a small "detection radius"
                    float occlusionTolerance = 0.1f;
                    float effectiveTargetDistance = distance + occlusionTolerance;
                    
                    if (closestNonTargetHitDistance < effectiveTargetDistance)
                    {
                        // Check if we're looking down at a target below ground level
                        // If camera is above target and hit point is above target, the hit might not actually block the view
                        // (e.g., looking down through ground plane to see something below)
                        bool cameraAboveTarget = fromPosition.y > toPosition.y;
                        if (cameraAboveTarget && closestNonTargetHitPoint != Vector3.zero)
                        {
                            // If hit point is above the target, it's not blocking when looking down
                            // (the ground plane extends infinitely, but doesn't block vertical view when looking down)
                            if (closestNonTargetHitPoint.y > toPosition.y)
                            {
                                // Hit is above target - not blocking when looking down
                                return false;
                            }
                        }
                        
                        // Check if the hit is very close to the target position (within tolerance)
                        // This handles cases where the target might be at the same position as another object
                        // or where floating point precision causes slight mismatches
                        if (closestNonTargetHitPoint != Vector3.zero)
                        {
                            float hitToTargetDistance = Vector3.Distance(closestNonTargetHitPoint, toPosition);
                            if (hitToTargetDistance < occlusionTolerance * 2f)
                            {
                                // Hit is very close to target position - might be the target itself or co-located object
                                // Treat as not occluded (conservative approach for targets without colliders)
                                return false;
                            }
                        }
                        
                        // Hit something before reaching the target - occluded
                        return true;
                    }
                    else
                    {
                        // Hit something beyond the target - not occluded (target has no collider but line-of-sight is clear)
                        return false;
                    }
                }
                else
                {
                    // Nothing was hit - clear line-of-sight (target has no collider)
                    return false;
                }
            }
        }
    }
}
