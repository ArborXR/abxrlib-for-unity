using System.Collections.Generic;
using UnityEngine;

namespace AbxrLib.Runtime.UI.Keyboard
{
    /// <summary>
    /// Stub implementation of LaserPointerManager for when Unity XR Interaction Toolkit is not available.
    /// This class provides the same interface as LaserPointerManager but with no-op implementations.
    /// </summary>
    public static class LaserPointerManagerStub
    {
        /// <summary>
        /// Cleans up any null references from destroyed objects to prevent memory leaks.
        /// Stub implementation - no-op.
        /// </summary>
        public static void CleanupDestroyedReferences()
        {
            // No-op for stub implementation
        }
        
        /// <summary>
        /// Forces cleanup of all managed states. Use this when you want to reset the manager completely.
        /// Stub implementation - no-op.
        /// </summary>
        public static void ForceCleanup()
        {
            // No-op for stub implementation
        }
        
        /// <summary>
        /// Called when scenes change to ensure proper cleanup of destroyed objects.
        /// Stub implementation - no-op.
        /// </summary>
        public static void OnSceneChanged()
        {
            // No-op for stub implementation
        }
        
        /// <summary>
        /// Checks if XR Interaction Toolkit is available and properly configured.
        /// Stub implementation - always returns false.
        /// </summary>
        /// <returns>Always false for stub implementation</returns>
        public static bool IsXRInteractionToolkitAvailable()
        {
            return false;
        }

        /// <summary>
        /// Enables laser pointers for keyboard/PIN pad interaction if they are not already enabled.
        /// Stub implementation - no-op.
        /// </summary>
        public static void EnableLaserPointersForInteraction()
        {
            Debug.Log("AbxrLib - LaserPointerManagerStub: XR Interaction Toolkit not available, laser pointer management disabled");
        }

        /// <summary>
        /// Restores laser pointers to their original state before keyboard/PIN pad interaction.
        /// Stub implementation - no-op.
        /// </summary>
        public static void RestoreLaserPointerStates()
        {
            // No-op for stub implementation
        }

        /// <summary>
        /// Checks if laser pointers are currently being managed by this system.
        /// Stub implementation - always returns false.
        /// </summary>
        public static bool IsManagingLaserPointers => false;

        /// <summary>
        /// Gets the number of ray interactors currently being managed.
        /// Stub implementation - always returns 0.
        /// </summary>
        public static int ManagedRayInteractorCount => 0;

        /// <summary>
        /// Disposes of all managed resources and clears state.
        /// Stub implementation - no-op.
        /// </summary>
        public static void Dispose()
        {
            // No-op for stub implementation
        }
    }
}
