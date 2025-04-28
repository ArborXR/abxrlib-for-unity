using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DefaultExecutionOrder(100)] // Doesn't matter when this one runs
[AddComponentMenu("ArborXR Insights/Track Object")]
public class TrackObject : MonoBehaviour
{
    private Vector3 _currentPosition;
    private Quaternion _currentRotation;

    private void Start()
    {
        float positionUpdateIntervalSeconds = (float)(60.0 / Configuration.Instance.trackingUpdatesPerMinute);
        InvokeRepeating(nameof(UpdateLocation), 0, positionUpdateIntervalSeconds);
    }

    private void UpdateLocation()
    {
        if (transform.position.Equals(_currentPosition)) return;
        if (transform.rotation.Equals(_currentRotation)) return;

        _currentPosition = transform.position;
        _currentRotation = transform.rotation;
        var positionDict = new Dictionary<string, string>
        {
            ["x"] = transform.position.x.ToString(CultureInfo.InvariantCulture),
            ["y"] = transform.position.y.ToString(CultureInfo.InvariantCulture),
            ["z"] = transform.position.z.ToString(CultureInfo.InvariantCulture)
        };
        Abxr.TelemetryEntry(name + " Position", positionDict);
    }
}