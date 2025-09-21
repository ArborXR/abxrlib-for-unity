using System.Collections.Generic;
using UnityEngine;


namespace AbxrLib.Runtime.UI.Keyboard
{
    /// <summary>
    /// Manages XR ray interactors (laser pointers) to ensure they are enabled during keyboard/PIN pad interactions.
    /// Automatically detects and restores the original state when interactions are complete.
    /// Requires Unity XR Interaction Toolkit to be available.
    /// </summary>
    public static class LaserPointerManager
    {
        private static Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor, bool> _originalStates = new Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor, bool>();
        private static bool _isManagingLaserPointers = false;
        
        /// <summary>
        /// Checks if XR Interaction Toolkit is available and properly configured.
        /// </summary>
        /// <returns>True if XR Interaction Toolkit is available, false otherwise</returns>
        public static bool IsXRInteractionToolkitAvailable()
        {
            try
            {
                // Try to access the XRRayInteractor type to verify the toolkit is available
                var rayInteractorType = typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor);
                return rayInteractorType != null;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Enables laser pointers for keyboard/PIN pad interaction if they are not already enabled.
        /// Stores the original state for restoration later.
        /// </summary>
        public static void EnableLaserPointersForInteraction()
        {
            // Check if XR Interaction Toolkit is available
            if (!IsXRInteractionToolkitAvailable())
            {
                Debug.LogWarning("AbxrLib - LaserPointerManager: XR Interaction Toolkit is not available. Laser pointer management will be skipped.");
                return;
            }

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
            // Check if XR Interaction Toolkit is available
            if (!IsXRInteractionToolkitAvailable())
            {
                Debug.LogWarning("AbxrLib - LaserPointerManager: XR Interaction Toolkit is not available. Laser pointer restoration will be skipped.");
                return;
            }

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
        /// Returns false if XR Interaction Toolkit is not available.
        /// </summary>
        public static bool IsManagingLaserPointers => IsXRInteractionToolkitAvailable() && _isManagingLaserPointers;

        /// <summary>
        /// Gets the number of ray interactors currently being managed.
        /// Returns 0 if XR Interaction Toolkit is not available.
        /// </summary>
        public static int ManagedRayInteractorCount => IsXRInteractionToolkitAvailable() ? _originalStates.Count : 0;
    }
}
