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
        public float distanceFromCamera = 1.5f;

        [Tooltip("Vertical offset from the camera's eye height (in meters)")]
        public float verticalOffset = 0;
        
        [Tooltip("Use configuration values instead of inspector values")]
        public bool useConfigurationValues = true;
        
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
                distanceFromCamera = config.uiDistanceFromCamera;
                verticalOffset = config.uiVerticalOffset;
                xPosition = config.uiHorizontalOffset;
            }
            
#if UNITY_EDITOR
            Vector3 targetPos = cam.position + cam.forward * distanceFromCamera;
            Vector3 newPos = new Vector3(targetPos.x + xPosition, 1.1f + verticalOffset, targetPos.z);
            transform.position = newPos;
#else
            Vector3 targetPos = cam.position + cam.forward * distanceFromCamera;
            Vector3 newPos = new Vector3(targetPos.x + xPosition - 0.05f, 1.1f + verticalOffset, targetPos.z);
            transform.position = newPos;
#endif
        }
   
        private void Update()
        {
            // Use configuration values if enabled
            if (useConfigurationValues && config != null)
            {
                faceCamera = config.authUIFollowCamera;
                distanceFromCamera = config.uiDistanceFromCamera;
                verticalOffset = config.uiVerticalOffset;
                xPosition = config.uiHorizontalOffset;
            }
            else
            {
                faceCamera = Abxr.AuthUIFollowCamera;
            }
            
            if (faceCamera)
            {
                Vector3 targetPos = cam.position + cam.forward * distanceFromCamera;
                Vector3 newPos = new Vector3(cam.position.x + xPosition, 1.1f + verticalOffset, targetPos.z);
                transform.position = newPos;

                // face the camera
                transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
            }
        }
    }
}