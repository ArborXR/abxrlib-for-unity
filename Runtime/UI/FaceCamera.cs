using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [Tooltip("How far in front of the camera the panel should float")]
    public float distanceFromCamera = 1.5f;

    [Tooltip("Vertical offset from the camera's eye height (in meters)")]
    public float verticalOffset = 0;

    private void Start()
    {
        var cam = Camera.main.transform;
        if (!cam) return;
        
        Vector3 targetPos = cam.position + cam.forward * distanceFromCamera + Vector3.up * verticalOffset;
        transform.position = targetPos;

        // face the camera
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
    }

    private void LateUpdate()
    {
        var cam = Camera.main.transform;
        if (!cam) return;
        
        Vector3 targetPos = cam.position + cam.forward * distanceFromCamera + Vector3.up * verticalOffset;
        Vector3 newPos = new Vector3(targetPos.x, transform.position.y, targetPos.z);
        transform.position = newPos;

        // face the camera
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
    }
}