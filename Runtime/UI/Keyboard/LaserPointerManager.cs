using System.Collections.Generic;
using UnityEngine;


namespace AbxrLib.Runtime.UI.Keyboard
{
    /// <summary>
    /// Manages XR ray interactors (laser pointers) to ensure they are enabled during keyboard/PIN pad interactions.
    /// Automatically detects and restores the original state when interactions are complete.
    /// </summary>
    public static class LaserPointerManager
    {
        private static Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor, bool> _originalStates = new Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor, bool>();
        private static bool _isManagingLaserPointers = false;

        /// <summary>
        /// Enables laser pointers for keyboard/PIN pad interaction if they are not already enabled.
        /// Stores the original state for restoration later.
        /// </summary>
        public static void EnableLaserPointersForInteraction()
        {
            if (_isManagingLaserPointers) return; // Already managing, avoid duplicate calls

            _isManagingLaserPointers = true;
            _originalStates.Clear();

            // Find all XRRayInteractor components in the scene
            UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor[] rayInteractors = Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();

            foreach (UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor in rayInteractors)
            {
                if (rayInteractor != null)
                {
                    // Store original state
                    bool wasActive = rayInteractor.gameObject.activeInHierarchy;
                    _originalStates[rayInteractor] = wasActive;

                    // Enable if not already active
                    if (!wasActive)
                    {
                        rayInteractor.gameObject.SetActive(true);
                        Debug.Log($"AbxrLib - LaserPointerManager: Enabled ray interactor on {rayInteractor.gameObject.name}");
                    }
                }
            }

            Debug.Log($"AbxrLib - LaserPointerManager: Managing {_originalStates.Count} ray interactors for keyboard interaction");
        }

        /// <summary>
        /// Restores laser pointers to their original state before keyboard/PIN pad interaction.
        /// </summary>
        public static void RestoreLaserPointerStates()
        {
            if (!_isManagingLaserPointers) return; // Not managing, nothing to restore

            foreach (var kvp in _originalStates)
            {
                UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor = kvp.Key;
                bool originalState = kvp.Value;

                if (rayInteractor != null)
                {
                    // Only change state if it's different from original
                    if (rayInteractor.gameObject.activeInHierarchy != originalState)
                    {
                        rayInteractor.gameObject.SetActive(originalState);
                        Debug.Log($"AbxrLib - LaserPointerManager: Restored ray interactor on {rayInteractor.gameObject.name} to {(originalState ? "enabled" : "disabled")}");
                    }
                }
            }

            _originalStates.Clear();
            _isManagingLaserPointers = false;

            Debug.Log("AbxrLib - LaserPointerManager: Restored all ray interactors to original states");
        }

        /// <summary>
        /// Checks if laser pointers are currently being managed by this system.
        /// </summary>
        public static bool IsManagingLaserPointers => _isManagingLaserPointers;

        /// <summary>
        /// Gets the number of ray interactors currently being managed.
        /// </summary>
        public static int ManagedRayInteractorCount => _originalStates.Count;
    }
}
