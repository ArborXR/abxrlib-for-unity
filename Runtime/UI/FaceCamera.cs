using UnityEngine;

namespace AbxrLib.Runtime.UI
{
    public class FaceCamera : MonoBehaviour
    {
        [Tooltip("How far in front of the camera the panel should float")]
        public float distanceFromCamera = 1.5f;

        [Tooltip("Vertical offset from the camera's eye height (in meters)")]
        public float verticalOffset = 0;

        private void LateUpdate()
        {
            var cam = Camera.main.transform;
            if (!cam) return;
        
            Vector3 targetPos = cam.position + cam.forward * distanceFromCamera;
            Vector3 newPos = new Vector3(targetPos.x, 1.1f + verticalOffset, targetPos.z);
            transform.position = newPos;

            // face the camera
            transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
        }
    }
}