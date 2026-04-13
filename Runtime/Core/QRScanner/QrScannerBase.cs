/*
 * Shared logic for both the Quest (WebCamTexture) and PICO (PXR_CameraImage)
 * QR scanner implementations. Eliminates duplicated:
 *   - ProcessQrScanResult / InvokeAndClearCallback
 *   - Android camera permission request coroutine
 *   - StopScanningInternal skeleton (pin-pad restore, coroutine teardown)
 *   - State fields shared by both scanners
 *
 * Platform-specific camera startup, scan loop, and shutdown are left to subclasses.
 */
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;

namespace AbxrLib.Runtime.Core.QRScanner
{
    public abstract class QrScannerBase : MonoBehaviour, IQrScanner
    {
        public static AbxrAuthService AuthService;

        // ── Shared state ────────────────────────────────────────────────────────────
        protected bool IsOfferedOnThisDevice;
        protected bool IsScanning;
        protected bool IsInitializing;
        protected bool CameraPermissionRequested;
        protected bool RestorePinPadOnCancel;

        protected Action<string> ScanResultCallback;
        protected Coroutine ScanCoroutine;
        protected QrScanPanel Panel;
        protected ZXing.BarcodeReader BarcodeReader;

        // ── IQrScanner ──────────────────────────────────────────────────────────────
        bool IQrScanner.IsAvailable => IsOfferedOnThisDevice;
        bool IQrScanner.IsScanning => IsScanning;
        bool IQrScanner.IsInitializing => IsInitializing;
        bool IQrScanner.ArePermissionsDenied => AreCameraPermissionsDenied();
        Texture IQrScanner.GetCameraTexture() => GetCameraTexture();
        void IQrScanner.CancelScan() => CancelScanning();

        // ── Abstract surface ────────────────────────────────────────────────────────

        /// <summary>Latest camera preview texture shown in the scan panel.</summary>
        public abstract Texture GetCameraTexture();

        /// <summary>
        /// Called by <see cref="ScanQRCode"/> after camera permission has been granted.
        /// Implementations should start <see cref="ScanCoroutine"/> from here.
        /// </summary>
        protected abstract IEnumerator ScanLoopCoroutine();

        /// <summary>
        /// Tear down the platform-specific camera (WebCamTexture, PXR session, etc.).
        /// Called from <see cref="StopScanningInternal"/>.
        /// </summary>
        protected abstract void ShutdownCameraBackend();

        /// <summary>
        /// Returns true when the device reports that camera permission has been denied.
        /// Override to add headset-camera or other platform checks (e.g. Meta Quest).
        /// </summary>
        protected virtual bool AreCameraPermissionsDenied()
        {
            return CameraPermissionRequested &&
                   !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
        }

        // ── Public API ──────────────────────────────────────────────────────────────

        public void SetScanResultCallback(Action<string> callback) => ScanResultCallback = callback;

        public void ScanQRCode()
        {
            if (!IsOfferedOnThisDevice)
            {
                Logcat.Warning($"{GetType().Name}: ScanQRCode ignored (not available on this device).");
                InvokeAndClearCallback(null);
                return;
            }

            if (IsScanning || IsInitializing)
            {
                Logcat.Info($"{GetType().Name}: scan already in progress.");
                return;
            }

            EnsureRuntimeObjects();
            StartCoroutine(RequestPermissionThenScanCoroutine());
        }

        public void CancelScanning()
        {
            if (!IsScanning && !IsInitializing) return;
            StopScanningInternal(true);
            Logcat.Info($"{GetType().Name}: scan cancelled by user.");
        }

        // ── Shared permission coroutine ──────────────────────────────────────────────

        /// <summary>
        /// Requests the Android CAMERA permission, then delegates to
        /// <see cref="OnPermissionGranted"/> for any platform-specific follow-up
        /// before kicking off <see cref="ScanLoopCoroutine"/>.
        /// </summary>
        protected virtual IEnumerator RequestPermissionThenScanCoroutine()
        {
            IsInitializing = true;

            // Always call RequestUserPermission
            // if already granted, Android fires the granted callback immediately with no dialog shown.
            CameraPermissionRequested = true;
            bool? permissionGranted = null;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => permissionGranted = true;
            callbacks.PermissionDenied  += _ => permissionGranted = false;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);

            while (!permissionGranted.HasValue)
            {
                if (!IsInitializing) yield break;
                yield return null;
            }

            if (!permissionGranted.Value)
            {
                Logcat.Warning($"{GetType().Name}: camera permission denied.");
                OnPermissionDenied();
                yield break;
            }

            // Let subclasses do extra checks (e.g. Meta headset-camera permission, focus wait) before the scan loop starts
            bool continueToScan = true;
            yield return StartCoroutine(OnPermissionGranted(result => continueToScan = result));
            if (!continueToScan) yield break;

            RestorePinPadOnCancel = KeyboardHandler.IsPinPadVisible();
            if (RestorePinPadOnCancel) KeyboardHandler.HidePinPad();

            ScanCoroutine = StartCoroutine(ScanLoopCoroutine());
        }

        /// <summary>
        /// Called when camera permission is denied. Default shows the panel with an
        /// error message then cleans up. Override to change the UX.
        /// </summary>
        protected virtual void OnPermissionDenied()
        {
            IsInitializing = false;
            InvokeAndClearCallback(null);
        }

        /// <summary>
        /// Hook for subclasses to perform additional async checks after the base
        /// camera permission is granted (e.g. the Meta headset-camera check or the
        /// focus-return wait on Quest). Invoke <paramref name="setResult"/> with
        /// <c>false</c> to abort the scan, <c>true</c> to continue.
        /// Default implementation always continues.
        /// </summary>
        protected virtual IEnumerator OnPermissionGranted(Action<bool> setResult)
        {
            setResult(true);
            yield break;
        }

        // ── Shared stop logic ────────────────────────────────────────────────────────

        protected void StopScanningInternal(bool invokeCallbackWithNull)
        {
            if (ScanCoroutine != null)
            {
                StopCoroutine(ScanCoroutine);
                ScanCoroutine = null;
            }

            IsScanning = false;
            IsInitializing = false;
            Panel?.Hide();
            ShutdownCameraBackend();

            if (invokeCallbackWithNull && RestorePinPadOnCancel) KeyboardHandler.ShowPinPad();

            RestorePinPadOnCancel = false;

            if (invokeCallbackWithNull) InvokeAndClearCallback(null);
        }

        // ── Shared result handling ────────────────────────────────────────────────────

        /// <summary>
        /// Identical in both original readers. Parses the scanned payload and either
        /// fires the optional callback or authenticates via AuthService.
        /// </summary>
        protected void ProcessQrScanResult(string scanResult)
        {
            if (ScanResultCallback != null)
            {
                string callbackPin = null;
                if (!string.IsNullOrEmpty(scanResult) && !QrCodeScanCommon.TryExtractPinFromQrPayload(scanResult, out callbackPin))
                {
                    Logcat.Warning("Invalid QR code format (expected ABXR:XXXXXX or 6 digits): " + scanResult);
                }

                InvokeAndClearCallback(callbackPin);
                return;
            }

            if (string.IsNullOrEmpty(scanResult)) return;

            AuthService.SetInputSource("QRlms");
            if (!QrCodeScanCommon.TryExtractPinFromQrPayload(scanResult, out string pin))
            {
                Logcat.Warning("Invalid QR code format (expected ABXR:XXXXXX or 6 digits): " + scanResult);
            }

            AuthService.KeyboardAuthenticate(pin);
        }

        protected void InvokeAndClearCallback(string value)
        {
            if (ScanResultCallback == null) return;
            Action<string> cb = ScanResultCallback;
            ScanResultCallback = null;
            cb?.Invoke(value);
        }

        // ── Runtime object setup (overridable) ───────────────────────────────────────

        /// <summary>
        /// Ensures Panel and BarcodeReader exist. Subclasses override to also
        /// create their platform-specific camera component.
        /// </summary>
        protected virtual void EnsureRuntimeObjects()
        {
            BarcodeReader ??= QrCodeScanCommon.CreateBarcodeReader();

            if (Panel == null)
            {
                Panel = GetComponentInChildren<QrScanPanel>(true);
                if (Panel == null) Panel = QrScanPanel.CreateRuntimePanel(transform, CancelScanning);
            }
        }
    }
}
#endif
