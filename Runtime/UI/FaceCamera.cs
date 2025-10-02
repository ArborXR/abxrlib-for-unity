using UnityEngine;
using AbxrLib.Runtime.Core;

namespace AbxrLib.Runtime.UI
{
    public class FaceCamera : MonoBehaviour
    {
        [Tooltip("Should the panel face the camera all the time?")]
        public bool faceCamera;
        public float xPosition;
        
        [Tooltip("How far in front of the camera the panel should float")]
        public float distanceFromCamera = 1.0f;

        [Tooltip("Vertical offset from the camera's eye height (in meters)")]
        public float verticalOffset = 0;
        
        [Tooltip("Use configuration values instead of inspector values")]
        public bool useConfigurationValues = true;
        
        // Default values when using configuration
        private const float DEFAULT_DISTANCE_FROM_CAMERA = 1.0f;
        private const float DEFAULT_VERTICAL_OFFSET = 0f;
        private const float DEFAULT_X_POSITION = 0f;
        
        // Build-specific positioning offset
        private const float BUILD_X_OFFSET = -0.05f;
        
        private Transform cam;
        private Configuration config;

        private void Start()
        {
            cam = Camera.main.transform;
            if (!cam) return;
            
            config = Configuration.Instance;
            
            // Use configuration values if enabled
            if (useConfigurationValues)
            {
                // Use default positioning values - no longer configurable
                distanceFromCamera = DEFAULT_DISTANCE_FROM_CAMERA;
                verticalOffset = DEFAULT_VERTICAL_OFFSET;
                xPosition = DEFAULT_X_POSITION;
            }
            
#if UNITY_EDITOR
            Vector3 targetPos = cam.position + cam.forward * distanceFromCamera;
            Vector3 newPos = new Vector3(targetPos.x + xPosition, cam.position.y + verticalOffset, targetPos.z);
            transform.position = newPos;
#else
            Vector3 targetPos = cam.position + cam.forward * distanceFromCamera;
            Vector3 newPos = new Vector3(targetPos.x + xPosition + BUILD_X_OFFSET, cam.position.y + verticalOffset, targetPos.z);
            transform.position = newPos;
#endif
        }
   
        private void Update()
        {
            // Check if camera reference is still valid
            if (cam == null)
            {
                // Try to reacquire the camera reference
                if (Camera.main != null)
                {
                    cam = Camera.main.transform;
                }
                else
                {
                    // Camera is not available, skip this update
                    return;
                }
            }
            
            // Use configuration values if enabled
            if (useConfigurationValues && config != null)
            {
                faceCamera = config.authUIFollowCamera;
                // Use default positioning values - no longer configurable
                distanceFromCamera = DEFAULT_DISTANCE_FROM_CAMERA;
                verticalOffset = DEFAULT_VERTICAL_OFFSET;
                xPosition = DEFAULT_X_POSITION;
            }
            else
            {
                faceCamera = Abxr.AuthUIFollowCamera;
            }
            
            if (faceCamera)
            {
                // Position the pinpad directly in front of the camera at the specified distance
                Vector3 targetPos = cam.position + cam.forward * distanceFromCamera;
                // Use the camera's actual Y position plus vertical offset for proper alignment
                Vector3 newPos = new Vector3(targetPos.x + xPosition, cam.position.y + verticalOffset, targetPos.z);
                transform.position = newPos;

                // face the camera - ensure perfect perpendicular alignment
                transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
            }
        }
    }
}