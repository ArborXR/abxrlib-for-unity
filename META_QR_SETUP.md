# Meta Quest QR Code Scanner Setup Guide

## Overview

AbxrLib uses `WebCamTexture` to access Meta Quest's passthrough camera for QR code scanning. No AR Foundation is required. On Meta Quest, the SDK checks that OpenXR **Meta Quest Support** and **Meta Quest: Camera (Passthrough)** (and Session) are enabled; it will log errors if they are missing.

## Required Setup

### 1. Camera Permissions

AbxrLib automatically adds to AndroidManifest.xml:
- `android.permission.CAMERA`
- `horizonos.permission.HEADSET_CAMERA`

Both are runtime permissions. When someone taps "Scan QR Code", AbxrLib asks for `CAMERA` and then for Headset Cameras, one after the other. Allowing the first doesn't grant the second.

### 2. Device Settings (On Quest)

1. Put on the headset and go to **Settings > Privacy > Camera Access** (or **Privacy & Safety > App Permissions > Headset Cameras**, depending on firmware).
2. Find your app and **enable** camera access.

## How It Works

- AbxrLib uses `WebCamTexture` for the passthrough camera; the feed is shown in a world-space Canvas overlay.
- Supported devices: **Quest 3, Quest 3S, Quest Pro** (Quest 2 is not supported).
- The "Scan QR Code" button is shown on supported devices. Tapping it asks for any permission that isn't granted yet; if one is denied, the scan panel says so and the PIN pad comes back.

## Troubleshooting

### "Scan QR Code" Button Not Visible

- **Device:** Button only appears on Quest 3, 3S, or Pro.
- **Logs:** Look for `QR Code button hidden - no supported QR scanner is active for this device`. On a supported device you'll see `Device '...' is supported for QR code reading`.

### Black Camera Feed

- Confirm camera is enabled for the app in Quest Settings.
- Allow a few seconds for the camera to start; if it stays black, re-check permissions and look for `Quest QR scanner requires the Headset Cameras permission` or `camera permission denied` in logs.
- If the Headset Cameras dialog never appears and the permission is denied at once, a device policy may be blocking it. Check the headset's Settings > Privacy & Safety > App Permissions > Headset Cameras, and any camera restrictions in your MDM.

### OpenXR Errors

If you see errors about **Meta Quest: Session** or **Meta Quest: Camera (Passthrough)** not enabled, enable them in **Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups** (Meta Quest Support, and the Camera (Passthrough) feature).

## Testing

1. Build and deploy to Quest 3/3S/Pro.
2. Grant camera permissions for your app if prompted (Quest Settings > Privacy / Camera Access or Headset Cameras).
3. Open the PIN pad and tap "Scan QR Code."
4. Point the camera at a QR code that starts with **ABXR:** (e.g. `ABXR:123456`).

## Notes

- **Quest 2** is not supported (camera quality insufficient for reliable QR scanning).
- Only QR codes starting with **ABXR:** are accepted; the button label toggles to "Stop Scanning" while active.
- For custom UI, use `Abxr.GetQRScanCameraTexture()` to get the camera texture (e.g. for a RawImage). Pico uses a different path; Meta-only builds do not conflict with Pico.

## Debugging

View AbxrLib logs:

```bash
adb logcat | grep -i "[AbxrLib]"
```

Clear and then view:

```bash
adb logcat -c && adb logcat | grep -i "[AbxrLib]"
```
