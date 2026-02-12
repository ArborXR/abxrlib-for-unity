using System.Collections.Generic;
using System.Globalization;
using AbxrLib.Runtime.Core;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Telemetry
{
    [DefaultExecutionOrder(100)] // Doesn't matter when this one runs
    public class TrackObject : MonoBehaviour
    {
        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private static float _timer = 1f;
    
        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= Configuration.Instance.telemetryTrackingPeriodSeconds) RecordLocation();
        }

        private void RecordLocation()
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
            Abxr.Telemetry(gameObject.name + " Position", positionDict);
        }
    }
}