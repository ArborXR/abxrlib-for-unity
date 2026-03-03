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
        private float _timer;
        private readonly Dictionary<string, string> _positionData = new Dictionary<string, string>(3);

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= Configuration.Instance.telemetryTrackingPeriodSeconds)
            {
                _timer = 0f;
                RecordLocation();
            }
        }

        private void RecordLocation()
        {
            if (transform.position.Equals(_currentPosition)) return;
            if (transform.rotation.Equals(_currentRotation)) return;

            _currentPosition = transform.position;
            _currentRotation = transform.rotation;
            _positionData.Clear();
            _positionData["x"] = transform.position.x.ToString(CultureInfo.InvariantCulture);
            _positionData["y"] = transform.position.y.ToString(CultureInfo.InvariantCulture);
            _positionData["z"] = transform.position.z.ToString(CultureInfo.InvariantCulture);
            Abxr.Telemetry(gameObject.name + " Position", _positionData);
        }
    }
}