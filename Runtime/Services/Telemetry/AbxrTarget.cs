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
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace AbxrLib.Runtime.Services.Telemetry
{
    /// <summary>
    /// Component that marks a GameObject as a gaze tracking target.
    /// When Events occur, the system calculates how directly the user is looking at this target.
    /// </summary>
    [AddComponentMenu("Analytics for XR/Abxr Target")]
    public class AbxrTarget : MonoBehaviour
    {
        // Static registry of all AbxrTarget instances for efficient lookup
        private static System.Collections.Generic.List<AbxrTarget> _allTargets = new System.Collections.Generic.List<AbxrTarget>();
        // Fast path: avoid GetAllTargets() when no targets exist (e.g. on every Event when scene has no AbxrTargets)
        private static int _targetCount;

        /// <summary>
        /// Returns whether any AbxrTarget instances exist. Use this for a quick exit when no targets are in the scene.
        /// </summary>
        public static bool HasAnyTargets => _targetCount > 0;

        /// <summary>
        /// Gets all AbxrTarget instances in the scene.
        /// Uses a cached registry for performance instead of FindObjectsOfType.
        /// </summary>
        /// <returns>Array of all AbxrTarget instances</returns>
        public static AbxrTarget[] GetAllTargets()
        {
            if (_allTargets == null) return new AbxrTarget[0];
            _allTargets.RemoveAll(target => target == null);
            _targetCount = _allTargets.Count; // Keep in sync after pruning
            return _allTargets.ToArray();
        }

        [Tooltip("Target name for this target (e.g., 'head', 'leftleg', 'button1'). If empty, uses GameObject name.")]
        [SerializeField]
        private string targetName = "";

        [Tooltip("Layers that should block line-of-sight (e.g., walls, obstacles). Objects on these layers will occlude the target.")]
        [SerializeField]
        public LayerMask occlusionLayers = -1; // Default to all layers

        [Tooltip("Maximum distance for line-of-sight check. Targets beyond this distance won't be checked for occlusion. 0 = use global default from Configuration.")]
        [SerializeField]
        public float maxDistanceLimit = 0f; // 0 = use global default

        [Tooltip("Automatically create a trigger collider if none exists. Trigger colliders don't interfere with physics or interactions - they're only used for raycast detection. Initializes from global default in Configuration.")]
        [SerializeField]
        public bool autoCreateTriggerCollider = true; // Initializes from Configuration.defaultAutoCreateTriggerCollider
        
        /// <summary>
        /// Gets whether to automatically create a trigger collider.
        /// Uses local value (which defaults to the global default when components are created).
        /// The global default in Configuration primarily affects new components.
        /// </summary>
        private bool GetAutoCreateTriggerCollider()
        {
            // Use local value - it was initialized from global default when component was created
            // Users can override per-component by changing autoCreateTriggerCollider directly
            return autoCreateTriggerCollider;
        }

        [Tooltip("Size of the auto-created trigger collider (if autoCreateTriggerCollider is enabled). Automatically calculated from parent bounds when parented.")]
        [HideInInspector]
        [SerializeField]
        public float triggerColliderSize = 1f; // Default fallback size (automatically calculated from parent bounds when parented)

        [Tooltip("When reparented, automatically center this target at the parent's origin (local position 0,0,0).")]
        [SerializeField]
        public bool autoCenterOnParent = true; // Default to true for convenience

        /// <summary>
        /// Gets the maximum occlusion check distance.
        /// Returns the local maxDistanceLimit if set (non-zero), otherwise returns the global default from Configuration.
        /// </summary>
        /// <returns>Maximum distance for occlusion checks (0 = unlimited)</returns>
        public float GetMaxDistanceLimit()
        {
            // If local value is set (non-zero), use it
            if (maxDistanceLimit > 0f)
            {
                return maxDistanceLimit;
            }
            
            // Otherwise, use global default from Configuration
            return Configuration.Instance.defaultMaxDistanceLimit;
        }

        /// <summary>
        /// Gets the targetName for this target (the name used in TargetInfo and other APIs).
        /// Returns the targetName field if set, otherwise falls back to GameObject name.
        /// </summary>
        public string GetTargetName()
        {
            return string.IsNullOrEmpty(targetName) ? gameObject.name : targetName;
        }

        /// <summary>
        /// Sets the targetName for this target.
        /// Validates and sanitizes the name to ensure it's safe for use in telemetry keys.
        /// </summary>
        /// <param name="name">The target name to set</param>
        public void SetTargetName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                targetName = "";
                return;
            }
            
            // Sanitize the name: remove or replace characters that could break telemetry keys
            // Telemetry keys use format "gaze_score_{targetName}", so we need safe characters
            // Allow: letters, numbers, underscores, hyphens
            // Replace spaces with underscores, remove other special characters
            System.Text.StringBuilder sanitized = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sanitized.Append(c);
                }
                else if (char.IsWhiteSpace(c))
                {
                    sanitized.Append('_');
                }
                // Skip other special characters
            }
            
            string sanitizedName = sanitized.ToString();
            
            // Ensure it's not empty after sanitization
            if (string.IsNullOrEmpty(sanitizedName))
            {
                Debug.LogWarning($"AbxrTarget: Target name '{name}' was sanitized to empty string. Using original name.", this);
                targetName = name;
            }
            else
            {
                targetName = sanitizedName;
                
                // Warn if name was changed during sanitization
                if (sanitizedName != name)
                {
                    Debug.LogWarning($"AbxrTarget: Target name '{name}' was sanitized to '{sanitizedName}' for safe use in telemetry keys.", this);
                }
            }
        }

        /// <summary>
        /// Checks if a custom targetName is set (not empty).
        /// Returns false if targetName is empty (will use GameObject name).
        /// </summary>
        /// <returns>True if custom targetName is set, false otherwise</returns>
        public bool HasCustomTargetName()
        {
            return !string.IsNullOrEmpty(targetName);
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
        /// <param name="deferIfInValidation">If true and called during OnValidate, defers the component addition to avoid Unity's SendMessage restriction</param>
        private void EnsureTriggerCollider(bool deferIfInValidation = false)
        {
            // Use the effective value (local override or global default)
            // The local value defaults to true, but users can override it
            // The global default primarily affects new components, but we check it here for consistency
            bool shouldCreate = GetAutoCreateTriggerCollider();
            
            if (!shouldCreate)
                return;

            // Check if we already have a collider (including children)
            if (HasCollider())
                return;

#if UNITY_EDITOR
            // If we're in validation and should defer, schedule the component addition for later
            if (deferIfInValidation)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && gameObject != null && !HasCollider())
                    {
                        CreateTriggerCollider();
                    }
                };
                return;
            }
#endif

            CreateTriggerCollider();
        }

        /// <summary>
        /// Creates the trigger collider component. Separated to allow deferred creation.
        /// </summary>
        private void CreateTriggerCollider()
        {
            // Create a small sphere trigger collider
            // Sphere colliders work well for point targets and are efficient
            SphereCollider triggerCollider = gameObject.AddComponent<SphereCollider>();
            triggerCollider.isTrigger = true; // Critical: trigger colliders don't block physics
            // Use triggerColliderSize, but ensure it's at least 1f as a fallback (will be recalculated from parent bounds when parented)
            triggerCollider.radius = triggerColliderSize > 0f ? triggerColliderSize : 1f;
            triggerCollider.center = Vector3.zero; // Center at the GameObject's pivot

            // Hide it in the inspector to reduce clutter (optional, but helpful)
            // Note: This doesn't affect functionality, just makes it less visible in the inspector
        }

        private void Reset()
        {
            // Initialize from global default when component is first added or reset
            // This allows the global default to control new components
            autoCreateTriggerCollider = Configuration.Instance.defaultAutoCreateTriggerCollider;
            
            // Auto-assign a unique targetName if targetName field is empty
            if (string.IsNullOrEmpty(targetName))
            {
                targetName = GenerateUniqueTargetName();
            }
        }

        /// <summary>
        /// Generates a unique targetName in the format "AbxrTarget1", "AbxrTarget2", etc.
        /// Finds all existing AbxrTarget components and assigns the next available number.
        /// </summary>
        /// <returns>A unique targetName</returns>
        private string GenerateUniqueTargetName()
        {
            // Use cached registry instead of FindObjectsOfType for performance
            AbxrTarget[] allTargets = GetAllTargets();
            
            int maxNumber = 0;
            string baseName = "AbxrTarget";
            
            // Find the highest number used
            foreach (var target in allTargets)
            {
                if (target == null || target == this) continue;
                
                string targetName = target.GetTargetName();
                
                // Skip if the targetName is exactly the base name (no number) - these are likely unnamed targets
                if (targetName == baseName) continue;
                
                // Check if this name matches our pattern "AbxrTarget" followed by a number
                if (targetName.StartsWith(baseName))
                {
                    string suffix = targetName.Substring(baseName.Length);
                    if (int.TryParse(suffix, out int number))
                    {
                        if (number > maxNumber)
                        {
                            maxNumber = number;
                        }
                    }
                }
            }
            
            // Return the next available number
            return $"{baseName}{maxNumber + 1}";
        }

        /// <summary>
        /// Validates that this target's targetName is unique among all AbxrTarget components.
        /// If a duplicate is found, logs a warning to help the user identify the conflict.
        /// </summary>
        private void ValidateTargetNameUniqueness()
        {
            string currentTargetName = GetTargetName();
            
            // Use cached registry instead of FindObjectsOfType for performance
            AbxrTarget[] allTargets = GetAllTargets();
            
            foreach (var target in allTargets)
            {
                if (target == null || target == this) continue;
                
                string otherTargetName = target.GetTargetName();
                
                // If we find a duplicate targetName, warn the user
                if (otherTargetName == currentTargetName)
                {
                    Debug.LogWarning(
                        $"AbxrTarget: Duplicate targetName '{currentTargetName}' detected! " +
                        $"Both '{gameObject.name}' and '{target.gameObject.name}' have the same targetName. " +
                        $"This may cause confusion when using Abxr.TargetEnable/Disable by targetName. " +
                        $"Please assign unique targetNames to each AbxrTarget component.",
                        this
                    );
                    return; // Only warn once per duplicate
                }
            }
        }

        private void Awake()
        {
            // Register this target in the static registry for efficient lookup
            // Do this in Awake to catch targets that start disabled
            // Add null checks to prevent race conditions
            if (this != null && _allTargets != null && !_allTargets.Contains(this))
            {
                _allTargets.Add(this);
                _targetCount++;
            }

            // Ensure we have a trigger collider for accurate detection
            // Using Awake() instead of Start() ensures colliders are created before any occlusion checks
            EnsureTriggerCollider();
            
#if UNITY_EDITOR
            // Update collider size and scale based on parent bounds
            UpdateTargetSize();
#endif
        }

        private void OnEnable()
        {
            // Ensure we're registered (in case object was disabled and re-enabled)
            // Add null checks to prevent race conditions
            if (this != null && _allTargets != null && !_allTargets.Contains(this))
            {
                _allTargets.Add(this);
                _targetCount++;
            }
        }

        private void OnDisable()
        {
            // Unregister this target from the static registry
            // Add null checks to prevent race conditions and ensure proper cleanup
            if (_allTargets != null && this != null)
            {
                if (_allTargets.Remove(this))
                    _targetCount--;
            }
        }

        private void OnDestroy()
        {
            // Ensure we're removed from the registry when destroyed
            // Add null checks to prevent race conditions and ensure proper cleanup to prevent memory leaks
            if (_allTargets != null)
            {
                if (_allTargets.Remove(this))
                    _targetCount--;
                _allTargets.RemoveAll(target => target == null);
                _targetCount = _allTargets.Count; // Resync after prune
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Forces an update of the target size and collider based on parent bounds. Call this when reparenting to ensure the size matches the new parent's bounds.
        /// </summary>
        public void UpdateDebugVisualization()
        {
            UpdateTargetSize();
        }

        /// <summary>
        /// Updates the target's scale and collider size based on parent object's bounds.
        /// This ensures the gizmo displays correctly and the collider matches the target area.
        /// </summary>
        private void UpdateTargetSize()
        {
            // Calculate bounds based on parent object's bounds if available
            Bounds? targetBounds = CalculateTargetBounds();
            
            Vector3 targetSize;
            if (targetBounds.HasValue)
            {
                targetSize = targetBounds.Value.size;
            }
            else
            {
                // Fallback to CalculateTargetSize for backwards compatibility
                targetSize = CalculateTargetSize();
            }
            
            // Ensure no component is zero (Unity doesn't like zero scale)
            if (targetSize.x <= 0.01f) targetSize.x = 0.1f;
            if (targetSize.y <= 0.01f) targetSize.y = 0.1f;
            if (targetSize.z <= 0.01f) targetSize.z = 0.1f;
            
            // Set local scale to match parent object size (used by gizmo for visualization)
            // Use a small threshold to avoid unnecessary updates
            if (Vector3.Distance(transform.localScale, targetSize) > 0.01f)
            {
                transform.localScale = targetSize;
                
                // Verify it was set correctly
                if (Mathf.Abs(transform.localScale.y) < 0.01f)
                {
                    // Force it again
                    Vector3 correctedScale = transform.localScale;
                    correctedScale.y = Mathf.Max(correctedScale.y, 0.1f);
                    transform.localScale = correctedScale;
                }
            }

            // Update collider size to match the target size
            // Use the average of the three dimensions for sphere collider radius
            Collider collider = GetComponent<Collider>();
            if (collider != null && collider is SphereCollider sphereCollider)
            {
                // Use the average size for sphere collider radius
                float avgSize = (targetSize.x + targetSize.y + targetSize.z) / 3f;
                float newRadius = avgSize / 2f; // Radius is half the diameter
                
                if (Mathf.Abs(sphereCollider.radius - newRadius) > 0.01f)
                {
                    sphereCollider.radius = newRadius;
                    // Update triggerColliderSize to match
                    triggerColliderSize = avgSize;
                }
            }
        }

        /// <summary>
        /// Calculates the appropriate bounds for the target visualization based on parent object bounds.
        /// If parent has renderers or colliders, uses their bounds. Otherwise falls back to triggerColliderSize.
        /// </summary>
        /// <returns>Bounds object with center and size, or null if no parent bounds available</returns>
        private Bounds? CalculateTargetBounds()
        {
            // If we have a parent, try to get its bounds
            if (transform.parent != null)
            {
                Bounds? parentBounds = GetParentBounds(transform.parent);
                
                if (parentBounds.HasValue)
                {
                    Bounds bounds = parentBounds.Value;
                    
                    // Ensure minimum size (at least 0.1 units in each dimension)
                    Vector3 size = bounds.size;
                    size.x = Mathf.Max(size.x, 0.1f);
                    size.y = Mathf.Max(size.y, 0.1f);
                    size.z = Mathf.Max(size.z, 0.1f);
                    
                    // Create bounds with corrected size
                    Bounds correctedBounds = new Bounds(bounds.center, size);
                    
                    return correctedBounds;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Calculates the appropriate size for the target visualization based on parent object bounds.
        /// Legacy method for backwards compatibility - prefer using CalculateTargetBounds().
        /// </summary>
        /// <returns>Vector3 representing the size (scale) for the visualization</returns>
        private Vector3 CalculateTargetSize()
        {
            Bounds? bounds = CalculateTargetBounds();
            if (bounds.HasValue)
            {
                return bounds.Value.size;
            }
            
            // Fallback to triggerColliderSize (with safety check)
            float fallbackSize = triggerColliderSize > 0f ? triggerColliderSize : 1f;
            Vector3 size = Vector3.one * fallbackSize;
            
            // No parent, use collider size if available
            Collider collider = GetComponent<Collider>();
            if (collider != null && collider is SphereCollider sphereCollider)
            {
                float radius = sphereCollider.radius * 2f;
                size = Vector3.one * radius;
            }
            
            return size;
        }

        /// <summary>
        /// Gets the local position where this target should be centered based on parent's children's bounds.
        /// Returns the center of the parent's children's bounds if available, otherwise returns Vector3.zero (parent's pivot).
        /// </summary>
        /// <returns>Local position where the target should be centered</returns>
        public Vector3 GetTargetLocalPosition()
        {
            if (transform.parent == null)
            {
                return Vector3.zero;
            }
            
            Bounds? parentBounds = GetParentBounds(transform.parent);
            if (parentBounds.HasValue)
            {
                return parentBounds.Value.center;
            }
            
            return Vector3.zero;
        }

        /// <summary>
        /// Gets the combined bounds of all renderers and colliders in the parent object and its children.
        /// </summary>
        /// <param name="parent">The parent transform to get bounds from</param>
        /// <returns>Combined bounds, or null if no renderers or colliders found</returns>
        internal Bounds? GetParentBounds(Transform parent)
        {
            if (parent == null) return null;

            Bounds? combinedBounds = null;
            bool hasBounds = false;

            // Check renderers first (most accurate for visual representation)
            // Exclude this AbxrTarget's own renderer to avoid including itself
            // Wrap in try-catch for defensive programming
            try
            {
                Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
                if (renderers != null)
                {
                    foreach (Renderer renderer in renderers)
                    {
                        // Skip this AbxrTarget's own renderer
                        if (renderer != null && renderer.transform != this.transform && renderer.bounds.size.magnitude > 0)
                        {
                            if (!hasBounds)
                            {
                                combinedBounds = renderer.bounds;
                                hasBounds = true;
                            }
                            else
                            {
                                Bounds currentBounds = combinedBounds.Value;
                                currentBounds.Encapsulate(renderer.bounds);
                                combinedBounds = currentBounds;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AbxrTarget: Failed to get renderer bounds from parent '{parent.name}': {ex.Message}", this);
            }

            // If no renderers, check colliders
            // Exclude this AbxrTarget's own collider to avoid including itself
            // Wrap in try-catch for defensive programming
            if (!hasBounds)
            {
                try
                {
                    Collider[] colliders = parent.GetComponentsInChildren<Collider>();
                    if (colliders != null)
                    {
                        foreach (Collider collider in colliders)
                        {
                            // Skip this AbxrTarget's own collider
                            if (collider != null && collider.transform != this.transform && collider.bounds.size.magnitude > 0)
                            {
                                if (!hasBounds)
                                {
                                    combinedBounds = collider.bounds;
                                    hasBounds = true;
                                }
                                else
                                {
                                    Bounds currentBounds = combinedBounds.Value;
                                    currentBounds.Encapsulate(collider.bounds);
                                    combinedBounds = currentBounds;
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AbxrTarget: Failed to get collider bounds from parent '{parent.name}': {ex.Message}", this);
                }
            }

            // Transform bounds from world space to local space relative to parent
            if (hasBounds && combinedBounds.HasValue)
            {
                Bounds worldBounds = combinedBounds.Value;
                
                // Get all corners of the world bounds
                Vector3[] worldCorners = new Vector3[]
                {
                    worldBounds.center + new Vector3(-worldBounds.extents.x, -worldBounds.extents.y, -worldBounds.extents.z),
                    worldBounds.center + new Vector3(worldBounds.extents.x, -worldBounds.extents.y, -worldBounds.extents.z),
                    worldBounds.center + new Vector3(-worldBounds.extents.x, worldBounds.extents.y, -worldBounds.extents.z),
                    worldBounds.center + new Vector3(worldBounds.extents.x, worldBounds.extents.y, -worldBounds.extents.z),
                    worldBounds.center + new Vector3(-worldBounds.extents.x, -worldBounds.extents.y, worldBounds.extents.z),
                    worldBounds.center + new Vector3(worldBounds.extents.x, -worldBounds.extents.y, worldBounds.extents.z),
                    worldBounds.center + new Vector3(-worldBounds.extents.x, worldBounds.extents.y, worldBounds.extents.z),
                    worldBounds.center + new Vector3(worldBounds.extents.x, worldBounds.extents.y, worldBounds.extents.z)
                };
                
                // Convert all corners to local space
                Vector3[] localCorners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    localCorners[i] = parent.InverseTransformPoint(worldCorners[i]);
                }
                
                // Find min/max in local space
                Vector3 min = localCorners[0];
                Vector3 max = localCorners[0];
                for (int i = 1; i < 8; i++)
                {
                    min = Vector3.Min(min, localCorners[i]);
                    max = Vector3.Max(max, localCorners[i]);
                }
                
                // Create local-space bounds
                Vector3 localCenter = (min + max) / 2f;
                Vector3 localSize = max - min;
                
                // Ensure size is never zero or negative
                localSize.x = Mathf.Max(localSize.x, 0.01f);
                localSize.y = Mathf.Max(localSize.y, 0.01f);
                localSize.z = Mathf.Max(localSize.z, 0.01f);
                
                Bounds localBounds = new Bounds(localCenter, localSize);
                
                return localBounds;
            }

            return null;
        }

        /// <summary>
        /// Called when the transform's parent changes.
        /// Ensures the collider center is reset to local origin when reparented.
        /// Optionally centers the target at the parent's origin.
        /// </summary>
        private void OnTransformParentChanged()
        {
            // If auto-center is enabled and we have a parent, center at the center of parent's children's bounds
            if (autoCenterOnParent && transform.parent != null)
            {
                // Get the target local position (bounds center or parent pivot)
                Vector3 targetLocalPosition = GetTargetLocalPosition();
                
                if (Vector3.Distance(transform.localPosition, targetLocalPosition) > 0.01f)
                {
                    // Record undo for the transform change
                    Undo.RecordObject(transform, "Auto-center AbxrTarget on parent");
                    
                    // Set the local position to the center of parent's children's bounds
                    transform.localPosition = targetLocalPosition;
                    transform.localRotation = Quaternion.identity;
                    
                    // Mark the scene as dirty so changes are saved
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(gameObject);
                        EditorUtility.SetDirty(transform);
                        EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    }
                    
                    // Use delayCall to verify position after Unity processes the change
                    // This helps catch cases where Unity might override our change
                    EditorApplication.delayCall += () =>
                    {
                        if (this != null && transform != null)
                        {
                            // Recalculate target position in case parent changed
                            Vector3 delayedTargetPos = GetTargetLocalPosition();
                            
                            if (Vector3.Distance(transform.localPosition, delayedTargetPos) > 0.01f && autoCenterOnParent && transform.parent != null)
                            {
                                Undo.RecordObject(transform, "Re-center AbxrTarget (delayed)");
                                transform.localPosition = delayedTargetPos;
                                transform.localRotation = Quaternion.identity;
                                EditorUtility.SetDirty(gameObject);
                                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                            }
                        }
                    };
                }
            }
            
            // Ensure collider exists (OnValidate might not have run yet)
            EnsureTriggerCollider();
            
            // When reparented, ensure collider center is at local origin
            // This ensures the collider visualization updates correctly
            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                if (collider is SphereCollider sphereCollider)
                {
                    if (sphereCollider.center != Vector3.zero)
                    {
                        Undo.RecordObject(sphereCollider, "Reset collider center");
                        sphereCollider.center = Vector3.zero;
                    }
                }
                else if (collider is BoxCollider boxCollider)
                {
                    if (boxCollider.center != Vector3.zero)
                    {
                        Undo.RecordObject(boxCollider, "Reset collider center");
                        boxCollider.center = Vector3.zero;
                    }
                }
            }
            
            // Update target size - use delayCall to ensure parent hierarchy is fully updated
            EditorApplication.delayCall += () =>
            {
                if (this != null && transform != null && gameObject != null)
                {
                    UpdateTargetSize();
                }
            };
        }
#endif

        /// <summary>
        /// Called when values are changed in the Inspector (including transform changes).
        /// Ensures collider center stays at local origin and validates target name uniqueness.
        /// </summary>
        private void OnValidate()
        {
#if UNITY_EDITOR
            // In editor, OnValidate is called before Awake, so ensure collider and size are updated
            // Defer component addition to avoid Unity's SendMessage restriction during validation
            EnsureTriggerCollider(deferIfInValidation: true);
            UpdateTargetSize();
            
            // If auto-center is enabled and we have a parent, ensure we're centered at the center of parent's children's bounds
            // This handles cases where OnTransformParentChanged might not have fired immediately
            // Note: This will only run when OnValidate is called (on changes), not every frame
            if (autoCenterOnParent && transform.parent != null)
            {
                // Get the target local position (bounds center or parent pivot)
                Vector3 targetLocalPosition = GetTargetLocalPosition();
                
                if (Vector3.Distance(transform.localPosition, targetLocalPosition) > 0.01f)
                {
                    // Record undo for the transform change
                    Undo.RecordObject(transform, "Auto-center AbxrTarget on parent (OnValidate)");
                    
                    transform.localPosition = targetLocalPosition;
                    transform.localRotation = Quaternion.identity;
                    
                    // Mark the scene as dirty so changes are saved
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(gameObject);
                        EditorUtility.SetDirty(transform);
                        EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    }
                }
            }
#endif
            
            // Ensure collider center is at local origin
            Collider collider = GetComponent<Collider>();
            if (collider != null)
            {
                if (collider is SphereCollider sphereCollider)
                {
                    if (sphereCollider.center != Vector3.zero)
                    {
                        sphereCollider.center = Vector3.zero;
                    }
                }
                else if (collider is BoxCollider boxCollider)
                {
                    if (boxCollider.center != Vector3.zero)
                    {
                        boxCollider.center = Vector3.zero;
                    }
                }
            }

            // Validate target name uniqueness
            if (!string.IsNullOrEmpty(targetName))
            {
                ValidateTargetNameUniqueness();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draws gizmos in the Scene view to visualize the target position.
        /// Uses transform.position which correctly respects parent hierarchy.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;
            DrawTargetGizmo(false);
        }

        /// <summary>
        /// Draws gizmos when the target is selected in the Scene view.
        /// Uses transform.position which correctly respects parent hierarchy.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;
            DrawTargetGizmo(true);
        }

        /// <summary>
        /// Draws the target gizmo at the current transform position.
        /// </summary>
        /// <param name="isSelected">Whether the target is currently selected</param>
        private void DrawTargetGizmo(bool isSelected)
        {
            DrawGizmoInternal(this, isSelected);
        }

        /// <summary>
        /// Internal static method to draw gizmo for an AbxrTarget instance.
        /// Shared between runtime gizmo drawing and editor gizmo drawing.
        /// </summary>
        /// <param name="target">The AbxrTarget instance to draw gizmo for</param>
        /// <param name="isSelected">Whether the target is currently selected</param>
        public static void DrawGizmoInternal(AbxrTarget target, bool isSelected)
        {
            if (target == null || target.transform == null) return;

            // Use transform.position which correctly handles parent hierarchy
            // This ensures the gizmo moves correctly when the object is reparented
            Vector3 worldPosition = target.transform.position;
            
            // Get the size from transform.localScale to match the visualization
            // The visualization sets localScale to match the parent's bounds size
            Vector3 gizmoSize = target.transform.localScale;
            
            // Ensure minimum size (at least 0.1 units in each dimension)
            if (gizmoSize.x <= 0.01f) gizmoSize.x = 0.1f;
            if (gizmoSize.y <= 0.01f) gizmoSize.y = 0.1f;
            if (gizmoSize.z <= 0.01f) gizmoSize.z = 0.1f;
            
            // Fallback: if scale is still default (1,1,1) and we have a collider, use that
            if (gizmoSize == Vector3.one)
            {
                Collider collider = target.GetComponent<Collider>();
                if (collider != null)
                {
                    if (collider is SphereCollider sphereCollider)
                    {
                        float radius = sphereCollider.radius * 2f; // Convert radius to diameter
                        gizmoSize = Vector3.one * radius;
                    }
                    else if (collider is BoxCollider boxCollider)
                    {
                        gizmoSize = boxCollider.size;
                    }
                }
                else
                {
                    // Final fallback
                    float fallbackSize = target.triggerColliderSize > 0f ? target.triggerColliderSize : 1f;
                    gizmoSize = Vector3.one * fallbackSize;
                }
            }

            // Draw a wireframe cube at the target position
            // Use company color: #00db97 (RGB: 0, 219, 151)
            Color companyColor = new Color(0f, 219f / 255f, 151f / 255f, 1f);
            if (isSelected)
            {
                Gizmos.color = companyColor; // Full opacity when selected
            }
            else
            {
                Gizmos.color = new Color(companyColor.r, companyColor.g, companyColor.b, 0.6f); // Semi-transparent when not selected
            }
            
            // Draw wireframe cube with actual dimensions
            Gizmos.DrawWireCube(worldPosition, gizmoSize);
            
            // Draw a solid cube for better visibility
            Gizmos.color = new Color(companyColor.r, companyColor.g, companyColor.b, 0.2f); // Very transparent
            Gizmos.DrawCube(worldPosition, gizmoSize);
            
            // Draw a small sphere at the center (use average size for sphere radius)
            float avgSize = (gizmoSize.x + gizmoSize.y + gizmoSize.z) / 3f;
            Gizmos.color = companyColor; // Solid color
            Gizmos.DrawSphere(worldPosition, avgSize * 0.15f);
        }
#endif

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
        /// Targets beyond maxDistanceLimit are treated as occluded (too far to see).
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
        /// Targets beyond maxDistanceLimit are treated as occluded (too far to see).
        /// </summary>
        /// <param name="fromPosition">Starting position (camera)</param>
        /// <param name="toPosition">Target position</param>
        /// <param name="distance">Distance to target</param>
        /// <returns>True if view is blocked (occluded), false if clear line-of-sight</returns>
        private bool CheckLineOfSight(Vector3 fromPosition, Vector3 toPosition, float distance)
        {
            // Get max distance (local value or global default)
            float effectiveMaxDistance = GetMaxDistanceLimit();
            
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
