using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;
using UnityEngine.XR;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Core.UI;

namespace AbxrLib.Runtime.UI
{
    // Only ever instantiated by loading a shipped prefab, so the managed linker cannot see it referenced.
    [Preserve]
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

        // Flat-screen fit: where the visible content sits around the pivot, in metres along the panel's own axes at its
        // prefab scale. Measured once, or given by FitTogether.
        private Vector3 _prefabScale;
        private bool _contentMeasured;
        private Vector2 _contentCenter;
        private Vector2 _contentHalfExtents;

        private void Awake() => _prefabScale = transform.localScale;

        private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;
        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        // A rig that persists across scenes keeps its camera but may move to a new spawn point, so look again after a load.
        private void OnActiveSceneChanged(Scene from, Scene to) => cam = null;

        private void Start()
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
            if (!cam) return;
            PlaceInFrontOfCamera();
        }

        /// <summary>
        /// Puts the panel in front of the camera and turns it to face it. Runs on Start and whenever a new camera is
        /// found, for example after a scene load, so a panel that outlives its scene doesn't stay where the old camera was.
        /// </summary>
        private void PlaceInFrontOfCamera()
        {
            config = Configuration.Instance;
            
            // Use configuration values if enabled
            if (useConfigurationValues && config != null)
            {
                faceCamera = config.authUIFollowCamera;
                // Use configuration values for positioning
                distanceFromCamera = config.authUIDistanceFromCamera;
                verticalOffset = DEFAULT_VERTICAL_OFFSET;
                xPosition = DEFAULT_X_POSITION;
            }
            else
            {
                faceCamera = Abxr.AuthUIFollowCamera;
            }

            if (!XRSettings.isDeviceActive)
            {
                FitToFlatScreen();
                return;
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
            
            // Always set initial rotation to face the camera, regardless of follow setting
            // This ensures the dialog faces the user initially, then stays locked if faceCamera is false
            transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
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
                    PlaceInFrontOfCamera();
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
                // Use configuration values for positioning
                distanceFromCamera = config.authUIDistanceFromCamera;
                verticalOffset = DEFAULT_VERTICAL_OFFSET;
                xPosition = DEFAULT_X_POSITION;
            }
            else
            {
                faceCamera = Abxr.AuthUIFollowCamera;
            }
            
            if (faceCamera && !XRSettings.isDeviceActive)
            {
                FitToFlatScreen();
            }
            else if (faceCamera)
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

        /// <summary>
        /// Gives every panel the same flat-screen fit, sized to hold all of them, so panels that are placed at the same
        /// point stay lined up (the exit poll and its backdrop). Call it before their Start.
        /// </summary>
        public static void FitTogether(params GameObject[] roots)
        {
            if (XRSettings.isDeviceActive) return;
            var panels = new System.Collections.Generic.List<FaceCamera>();
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var root in roots)
            {
                var panel = root != null ? root.GetComponent<FaceCamera>() : null;
                if (panel == null) continue;
                panel.MeasureContent();
                min = Vector2.Min(min, panel._contentCenter - panel._contentHalfExtents);
                max = Vector2.Max(max, panel._contentCenter + panel._contentHalfExtents);
                panels.Add(panel);
            }
            foreach (var panel in panels)
            {
                panel._contentCenter = (min + max) / 2f;
                panel._contentHalfExtents = (max - min) / 2f;
            }
        }

        /// <summary>
        /// On a flat screen the panel, sized in metres for a headset, overflows the view. Centre its content on the view
        /// direction, whose camera may look down at the scene, and shrink it until it fits.
        /// </summary>
        private void FitToFlatScreen()
        {
            if (!_contentMeasured) MeasureContent();
            Camera view = cam.GetComponent<Camera>();
            float scale = view != null && !view.orthographic
                ? FlatScreenFit.Scale(_contentHalfExtents, distanceFromCamera, view.fieldOfView, view.aspect)
                : 1f;
            transform.localScale = _prefabScale * scale;

            Vector3 target = cam.position + cam.forward * distanceFromCamera;
            Quaternion rotation = Quaternion.LookRotation(cam.forward, cam.up); // Square to the screen, whatever the pitch.
            transform.SetPositionAndRotation(target - rotation * (_contentCenter * scale), rotation);
        }

        /// <summary>
        /// Measures the panel's visible chrome around its pivot. Text is left out: its boxes can be wider than the panels
        /// they sit on.
        /// </summary>
        private void MeasureContent()
        {
            Vector3 currentScale = transform.localScale;
            transform.localScale = _prefabScale;
            Canvas.ForceUpdateCanvases(); // Lays out buttons added since the panel was created.

            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            var corners = new Vector3[4];
            foreach (var graphic in GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled || graphic.color.a <= 0f || graphic is TMP_Text || graphic is Text) continue;
                graphic.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 offset = corner - transform.position;
                    var point = new Vector2(Vector3.Dot(offset, transform.right), Vector3.Dot(offset, transform.up));
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }
            transform.localScale = currentScale;

            _contentMeasured = true;
            if (min.x > max.x) return; // Nothing to measure, so nothing to fit.
            _contentCenter = (min + max) / 2f;
            _contentHalfExtents = (max - min) / 2f;
        }
    }
}