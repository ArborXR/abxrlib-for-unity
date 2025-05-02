using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [Tooltip("How far in front of the camera the keyboard should float")]
    public float distanceFromCamera = 1.5f;

    [Tooltip("Vertical offset from the camera's eye height (in meters)")]
    public float verticalOffset = 0;

    void LateUpdate()
    {
        var cam = Camera.main.transform;
        if (cam == null) return;

        // compute target position: in front of camera + optional vertical offset
        Vector3 targetPos = cam.position 
                            + cam.forward * distanceFromCamera 
                            + Vector3.up * verticalOffset;

        // set position
        transform.position = targetPos;

        // face the camera (so forward of keyboard points at camera)
        // you might want keys to face the camera, so keyboard's forward should be opposite
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
    }
}