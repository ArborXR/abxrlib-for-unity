using System.Collections.Generic;
using UnityEngine;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Runtime.UI.Keyboard
{
#if XR_TOOLKIT_AVAILABLE
    /// <summary>
    /// Manages XR ray interactors (laser pointers) to ensure they are enabled during keyboard/PIN pad interactions.
    /// Automatically detects and restores the original state when interactions are complete.
    /// Requires Unity XR Interaction Toolkit to be available.
    /// </summary>
    public static class LaserPointerManager
    {
        private static Dictionary<object, bool> _originalStates = new Dictionary<object, bool>();
        private static bool _isManagingLaserPointers = false;
        private static int _cleanupCounter = 0;
        private const int CLEANUP_FREQUENCY = 100; // Clean up every 100 operations
        
        // Cache for ray interactors to avoid expensive FindObjectsOfType calls
        private static UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor[] _cachedRayInteractors = null;
        private static bool _cacheValid = false; // Cache is valid until explicitly invalidated
        
        /// <summary>
        /// Cleans up any null references from destroyed objects to prevent memory leaks.
        /// This should be called periodically or when objects might be destroyed.
        /// </summary>
        public static void CleanupDestroyedReferences()
        {
            if (_originalStates.Count == 0) return;
            
            var keysToRemove = new List<object>();
            
            // Find all null keys (destroyed objects)
            foreach (var kvp in _originalStates)
            {
                if (kvp.Key == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            // Remove null keys from dictionary - we need to handle this carefully
            // since Dictionary.Remove(null) doesn't work as expected
            if (keysToRemove.Count > 0)
            {
                // Create a new dictionary without null keys
                var newDictionary = new Dictionary<object, bool>();
                foreach (var kvp in _originalStates)
                {
                    if (kvp.Key != null)
                    {
                        newDictionary[kvp.Key] = kvp.Value;
                    }
                }
                _originalStates = newDictionary;
                
                Debug.Log($"[AbxrLib] LaserPointerManager - Cleaned up {keysToRemove.Count} destroyed ray interactor references");
            }
        }
        
        /// <summary>
        /// Forces cleanup of all managed states. Use this when you want to reset the manager completely.
        /// </summary>
        public static void ForceCleanup()
        {
            _originalStates.Clear();
            _isManagingLaserPointers = false;
            _cachedRayInteractors = null; // Clear cache
            _cacheValid = false; // Invalidate cache
            Debug.Log("[AbxrLib] LaserPointerManager - Force cleanup completed");
        }
        
        /// <summary>
        /// Called when scenes change to ensure proper cleanup of destroyed objects.
        /// This prevents memory leaks when ray interactors are destroyed during scene transitions.
        /// </summary>
        public static void OnSceneChanged()
        {
            if (_isManagingLaserPointers)
            {
                Debug.Log("[AbxrLib] LaserPointerManager - Scene changed while managing laser pointers, performing cleanup");
                ForceCleanup();
            }
            else
            {
                // Just clean up any orphaned references
                CleanupDestroyedReferences();
            }
        }
        
        /// <summary>
        /// Checks if XR Interaction Toolkit is available and properly configured.
        /// </summary>
        /// <returns>True if XR Interaction Toolkit is available, false otherwise</returns>
        public static bool IsXRInteractionToolkitAvailable()
        {
            return true; // Always true since we're inside the XR_TOOLKIT_AVAILABLE block
        }

        /// <summary>
        /// Gets cached ray interactors or finds them if cache is invalid
        /// Ray interactors are cached for the lifetime of the app since they rarely change
        /// </summary>
        /// <returns>Array of XRRayInteractor components</returns>
        private static UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor[] GetRayInteractors()
        {
            // Check if cache is valid
            if (_cacheValid && _cachedRayInteractors != null)
            {
                // Filter out any null references from destroyed objects
                var validInteractors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
                foreach (var interactor in _cachedRayInteractors)
                {
                    if (interactor != null)
                    {
                        validInteractors.Add(interactor);
                    }
                }
                
                // Update cache with valid interactors only if some were destroyed
                if (validInteractors.Count != _cachedRayInteractors.Length)
                {
                    _cachedRayInteractors = validInteractors.ToArray();
                    Debug.Log($"[AbxrLib] LaserPointerManager - Updated cache, removed {_cachedRayInteractors.Length - validInteractors.Count} destroyed ray interactors");
                }
                
                return _cachedRayInteractors;
            }
            
            // Cache is invalid or null, find new ray interactors
            _cachedRayInteractors = UnityEngine.Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            _cacheValid = true;
            
            return _cachedRayInteractors;
        }

        /// <summary>
        /// Enables laser pointers for keyboard/PIN pad interaction if they are not already enabled.
        /// Stores the original state for restoration later.
        /// </summary>
        public static void EnableLaserPointersForInteraction()
        {
            // Periodic cleanup to prevent memory leaks
            if (++_cleanupCounter >= CLEANUP_FREQUENCY)
            {
                _cleanupCounter = 0;
                CleanupDestroyedReferences();
            }
            
            if (_isManagingLaserPointers) 
            {
                // Clean up any destroyed references before continuing
                CleanupDestroyedReferences();
                return; // Already managing, avoid duplicate calls
            }

            _isManagingLaserPointers = true;
            _originalStates.Clear();

            // Get cached ray interactors (or find them if cache is expired)
            var rayInteractors = GetRayInteractors();
            foreach (var rayInteractor in rayInteractors)
            {
                if (rayInteractor != null && _originalStates.Count < Configuration.Instance.maxDictionarySize)
                {
                    // Store original state
                    bool wasActive = rayInteractor.gameObject.activeInHierarchy;
                    _originalStates[rayInteractor] = wasActive;

                    // Enable if not already active
                    if (!wasActive)
                    {
                        rayInteractor.gameObject.SetActive(true);
                        Debug.Log($"[AbxrLib] LaserPointerManager - Enabled ray interactor on {rayInteractor.gameObject.name}");
                    }
                }
                else if (_originalStates.Count >= Configuration.Instance.maxDictionarySize)
                {
                    Debug.LogWarning($"[AbxrLib] LaserPointerManager - Maximum dictionary size ({Configuration.Instance.maxDictionarySize}) reached, skipping additional ray interactors");
                    break;
                }
            }
        }

        /// <summary>
        /// Ensures all World Space canvases under the given root have a TrackedDeviceGraphicRaycaster
        /// so that XR controller rays hit UI elements (otherwise the laser passes through).
        /// Call this after instantiating the keyboard or PIN pad prefab.
        /// </summary>
        public static void EnsureTrackedDeviceGraphicRaycasterOnCanvases(GameObject root)
        {
            if (root == null) return;
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                if (canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null)
                    continue;
                canvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                Debug.Log($"[AbxrLib] LaserPointerManager - Added TrackedDeviceGraphicRaycaster to canvas on {canvas.gameObject.name}");
            }
        }

        /// <summary>
        /// Restores laser pointers to their original state before keyboard/PIN pad interaction.
        /// </summary>
        public static void RestoreLaserPointerStates()
        {
            if (!_isManagingLaserPointers) return; // Not managing, nothing to restore

            // Clean up any null references before processing
            CleanupDestroyedReferences();

            var keysToRemove = new List<object>();
            
            foreach (var kvp in _originalStates)
            {
                object rayInteractor = kvp.Key;
                bool originalState = kvp.Value;

                var comp = rayInteractor as Component;
                if (comp != null)
                {
                    // Restore original state
                    if (comp.gameObject.activeInHierarchy != originalState)
                    {
                        comp.gameObject.SetActive(originalState);
                    }
                    keysToRemove.Add(rayInteractor); // Mark for removal after processing
                }
                else
                {
                    // Object was destroyed, mark for removal
                    keysToRemove.Add(rayInteractor);
                }
            }

            // Remove all processed entries - handle null keys properly
            if (keysToRemove.Count > 0)
            {
                // Create a new dictionary without the processed entries
                var newDictionary = new Dictionary<object, bool>();
                foreach (var kvp in _originalStates)
                {
                    if (!keysToRemove.Contains(kvp.Key))
                    {
                        newDictionary[kvp.Key] = kvp.Value;
                    }
                }
                _originalStates = newDictionary;
            }

            _isManagingLaserPointers = false;
        }

        /// <summary>
        /// Checks if laser pointers are currently being managed by this system.
        /// Returns false if XR Interaction Toolkit is not available.
        /// </summary>
        public static bool IsManagingLaserPointers => _isManagingLaserPointers;

        /// <summary>
        /// Gets the number of ray interactors currently being managed.
        /// Returns 0 if XR Interaction Toolkit is not available.
        /// </summary>
        public static int ManagedRayInteractorCount => _originalStates.Count;

        /// <summary>
        /// Disposes of all managed resources and clears state.
        /// </summary>
        public static void Dispose()
        {
            ForceCleanup();
        }
    }
#else
    /// <summary>
    /// Stub implementation when XR Interaction Toolkit is not available.
    /// This redirects all calls to the stub implementation.
    /// </summary>
    public static class LaserPointerManager
    {
        public static void CleanupDestroyedReferences() => LaserPointerManagerStub.CleanupDestroyedReferences();
        public static void ForceCleanup() => LaserPointerManagerStub.ForceCleanup();
        public static void OnSceneChanged() => LaserPointerManagerStub.OnSceneChanged();
        public static bool IsXRInteractionToolkitAvailable() => LaserPointerManagerStub.IsXRInteractionToolkitAvailable();
        public static void EnsureTrackedDeviceGraphicRaycasterOnCanvases(GameObject root) => LaserPointerManagerStub.EnsureTrackedDeviceGraphicRaycasterOnCanvases(root);
        public static void EnableLaserPointersForInteraction() => LaserPointerManagerStub.EnableLaserPointersForInteraction();
        public static void RestoreLaserPointerStates() => LaserPointerManagerStub.RestoreLaserPointerStates();
        public static bool IsManagingLaserPointers => LaserPointerManagerStub.IsManagingLaserPointers;
        public static int ManagedRayInteractorCount => LaserPointerManagerStub.ManagedRayInteractorCount;
        public static void Dispose() => LaserPointerManagerStub.Dispose();
    }
#endif
}
