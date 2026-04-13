#if UNITY_ANDROID && !UNITY_EDITOR && PICO_ENTERPRISE_SDK_3
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.XR.PXR;
using UnityEngine;

namespace AbxrLib.Runtime.Core.QRScanner
{
    /// <summary>
    /// Thin adapter over the PICO 3.4 PXR_CameraImage API.
    /// Creates a camera device/session, captures RGBA frames, and exposes them as a Texture2D + Color32[] buffer.
    /// </summary>
    public class PicoEnterpriseCameraFrameProvider : MonoBehaviour
    {
        [Header("Preferred Camera Settings")]
        [SerializeField] private XrCameraIdPICO preferredCameraId = XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO;
        [SerializeField] private int preferredWidth = 640;
        [SerializeField] private int preferredHeight = 480;
        [SerializeField] private XrCameraImageFpsPICO preferredFps = XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO;
        [SerializeField] private bool verboseLogging;

        private bool _isInitialized;
        private bool _isCapturing;
        private bool _isStarting;
        private XrCameraIdPICO _activeCameraId;
        private Texture2D _runtimeTexture;
        private Color32[] _pixelBuffer;
        private byte[] _byteBuffer;
        private long _lastCaptureTime;
        private CancellationTokenSource _startupCts;

        public bool IsStarting => _isStarting;
        public bool IsCapturing => _isCapturing;
        public string LastError { get; private set; }

        public void StartCamera()
        {
            if (_isStarting || _isCapturing) return;

            _startupCts?.Cancel();
            _startupCts?.Dispose();
            _startupCts = new CancellationTokenSource();
            LastError = null;
            _isStarting = true;
            _ = StartCameraInternalAsync(_startupCts.Token);
        }

        public void StopCamera()
        {
            ShutdownCamera();
        }

        public bool TryGetLatestFrame(out CameraFrame frame)
        {
            frame = default;

            if (!_isCapturing) return false;

            PxrResult acquireResult = PXR_CameraImage.AcquireCameraImage(_activeCameraId, _lastCaptureTime, out ulong imageId, out long captureTime);
            if (acquireResult != PxrResult.SUCCESS) return false;

            try
            {
                PxrResult dataResult = PXR_CameraImage.GetCameraImageData(_activeCameraId, imageId, out XrCameraImageDataRawBuffer rawBuffer);
                if (dataResult != PxrResult.SUCCESS)
                {
                    LastError = "GetCameraImageData failed: " + dataResult;
                    return false;
                }

                int width = (int)rawBuffer.width;
                int height = (int)rawBuffer.height;
                int stride = (int)rawBuffer.stride;
                int bytesPerPixel = (int)rawBuffer.bytesPerPixel;
                int bufferSize = (int)rawBuffer.bufferSize;

                if (width <= 0 || height <= 0 || bytesPerPixel != 4 || bufferSize <= 0 || rawBuffer.buffer == IntPtr.Zero)
                {
                    LastError = "Camera image buffer was empty or not RGBA8888.";
                    return false;
                }

                EnsureBuffers(width, height, bufferSize);
                Marshal.Copy(rawBuffer.buffer, _byteBuffer, 0, bufferSize);

                int expectedRowBytes = width * 4;
                if (stride <= 0) stride = expectedRowBytes;

                int dst = 0;
                for (int y = 0; y < height; y++)
                {
                    int rowStart = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int src = rowStart + x * 4;
                        if (src + 3 >= _byteBuffer.Length) break;

                        _pixelBuffer[dst++] = new Color32(_byteBuffer[src + 0], _byteBuffer[src + 1],
                            _byteBuffer[src + 2], _byteBuffer[src + 3]);
                    }
                }

                _runtimeTexture.SetPixels32(_pixelBuffer);
                _runtimeTexture.Apply(false, false);

                _lastCaptureTime = captureTime;
                LastError = null;

                frame = new CameraFrame
                {
                    Texture = _runtimeTexture,
                    Pixels = _pixelBuffer,
                    Width = width,
                    Height = height
                };

                return true;
            }
            finally
            {
                PXR_CameraImage.ReleaseCameraImage(_activeCameraId, imageId);
            }
        }

        private async Task StartCameraInternalAsync(CancellationToken token)
        {
            try
            {
                PxrResult getCamerasResult = PXR_CameraImage.GetAvailableCameras(out XrCameraIdPICO[] cameras);
                if (getCamerasResult != PxrResult.SUCCESS || cameras == null || cameras.Length == 0)
                {
                    LastError = "GetAvailableCameras failed: " + getCamerasResult;
                    Logcat.Warning(LastError);
                    return;
                }

                _activeCameraId = ChooseCameraId(cameras);

                PxrResult createDeviceResult = await PXR_CameraImage.CreateCameraDeviceAsync(_activeCameraId, token);
                if (createDeviceResult != PxrResult.SUCCESS)
                {
                    LastError = "CreateCameraDeviceAsync failed: " + createDeviceResult;
                    Logcat.Warning(LastError);
                    return;
                }

                _isInitialized = true;

                PxrExtent2Di captureResolution = ChooseResolution(_activeCameraId, preferredWidth, preferredHeight);
                XrCameraImageFpsPICO captureFps = ChooseFps(_activeCameraId, preferredFps);
                XrCameraImageFormatPICO captureFormat = ChooseFormat(_activeCameraId, XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO);
                XrCameraDataTransferTypePICO transferType = ChooseTransferType(_activeCameraId, XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO);
                XrCameraModelPICO cameraModel = ChooseCameraModel(_activeCameraId, XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO);

                PxrResult createSessionResult = await PXR_CameraImage.CreateCameraCaptureSessionAsync(
                    _activeCameraId, captureResolution.width, captureResolution.height, captureFps,
                    captureFormat, transferType, cameraModel, token);

                if (createSessionResult != PxrResult.SUCCESS)
                {
                    LastError = "CreateCameraCaptureSessionAsync failed: " + createSessionResult;
                    Logcat.Warning(LastError);
                    ShutdownCamera();
                    return;
                }

                PxrResult beginCaptureResult = PXR_CameraImage.BeginCameraCapture(_activeCameraId);
                if (beginCaptureResult != PxrResult.SUCCESS)
                {
                    LastError = "BeginCameraCapture failed: " + beginCaptureResult;
                    Logcat.Warning(LastError);
                    ShutdownCamera();
                    return;
                }

                _lastCaptureTime = 0;
                _isCapturing = true;
                LastError = null;
            }
            catch (OperationCanceledException)
            {
                LastError = "Camera startup canceled.";
            }
            catch (Exception ex)
            {
                LastError = "Camera startup exception: " + ex.Message;
                Logcat.Warning(LastError);
                ShutdownCamera();
            }
            finally
            {
                _isStarting = false;
            }
        }

        private XrCameraIdPICO ChooseCameraId(XrCameraIdPICO[] cameras)
        {
            foreach (var t in cameras)
            {
                if (t == preferredCameraId) return t;
            }

            return cameras[0];
        }

        private static PxrExtent2Di ChooseResolution(XrCameraIdPICO cameraId, int targetWidth, int targetHeight)
        {
            PxrExtent2Di fallback = new PxrExtent2Di { width = targetWidth, height = targetHeight };
            PxrResult result = PXR_CameraImage.GetCameraImageResolutionCapability(cameraId, out PxrExtent2Di[] resolutions);
            if (result != PxrResult.SUCCESS || resolutions == null || resolutions.Length == 0)
                return fallback;

            int bestIndex = 0;
            int bestScore = int.MaxValue;
            for (int i = 0; i < resolutions.Length; i++)
            {
                int score = Mathf.Abs(resolutions[i].width - targetWidth) + Mathf.Abs(resolutions[i].height - targetHeight);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return resolutions[bestIndex];
        }

        private static XrCameraImageFpsPICO ChooseFps(XrCameraIdPICO cameraId, XrCameraImageFpsPICO preferred)
        {
            PxrResult result = PXR_CameraImage.GetCameraImageFpsCapability(cameraId, out XrCameraImageFpsPICO[] fpsOptions);
            if (result != PxrResult.SUCCESS || fpsOptions == null || fpsOptions.Length == 0) return preferred;

            foreach (var t in fpsOptions)
            {
                if (t == preferred) return t;
            }

            return fpsOptions[0];
        }

        private static XrCameraImageFormatPICO ChooseFormat(XrCameraIdPICO cameraId, XrCameraImageFormatPICO preferred)
        {
            PxrResult result = PXR_CameraImage.GetCameraImageFormatCapability(cameraId, out XrCameraImageFormatPICO[] options);
            if (result != PxrResult.SUCCESS || options == null || options.Length == 0) return preferred;

            foreach (var t in options)
            {
                if (t == preferred) return t;
            }

            return options[0];
        }

        private static XrCameraDataTransferTypePICO ChooseTransferType(XrCameraIdPICO cameraId, XrCameraDataTransferTypePICO preferred)
        {
            PxrResult result = PXR_CameraImage.GetCameraDataTransferTypeCapability(cameraId, out XrCameraDataTransferTypePICO[] options);
            if (result != PxrResult.SUCCESS || options == null || options.Length == 0) return preferred;

            foreach (var t in options)
            {
                if (t == preferred) return t;
            }

            return options[0];
        }

        private static XrCameraModelPICO ChooseCameraModel(XrCameraIdPICO cameraId, XrCameraModelPICO preferred)
        {
            PxrResult result = PXR_CameraImage.GetCameraCameraModelCapability(cameraId, out XrCameraModelPICO[] options);
            if (result != PxrResult.SUCCESS || options == null || options.Length == 0) return preferred;

            foreach (var t in options)
            {
                if (t == preferred) return t;
            }

            return options[0];
        }

        private void EnsureBuffers(int width, int height, int bufferSize)
        {
            if (_runtimeTexture == null || _runtimeTexture.width != width || _runtimeTexture.height != height)
            {
                _runtimeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            int pixelCount = width * height;
            if (_pixelBuffer == null || _pixelBuffer.Length != pixelCount)
            {
                _pixelBuffer = new Color32[pixelCount];
            }

            if (_byteBuffer == null || _byteBuffer.Length != bufferSize)
            {
                _byteBuffer = new byte[bufferSize];
            }
        }

        private void ShutdownCamera()
        {
            _startupCts?.Cancel();
            _startupCts?.Dispose();
            _startupCts = null;

            if (_isCapturing)
            {
                PXR_CameraImage.EndCameraCapture(_activeCameraId);
            }

            if (_isInitialized)
            {
                PXR_CameraImage.DestroyCameraCaptureSession(_activeCameraId);
                PXR_CameraImage.DestroyCameraDevice(_activeCameraId);
            }

            _isCapturing = false;
            _isInitialized = false;
            _isStarting = false;
            _lastCaptureTime = 0;
        }

        private void OnDisable()
        {
            ShutdownCamera();
        }

        private void OnDestroy()
        {
            ShutdownCamera();
        }
    }
}
#endif
