# Meta Quest QR Code Scanner Setup Guide

## Overview

AbxrLib uses `WebCamTexture` to access Meta Quest's passthrough camera for QR code scanning. The implementation is simple and doesn't require AR Foundation or camera settings modifications.

## Required Setup

### 1. Camera Permissions

The following permissions are **automatically added** to AndroidManifest.xml by AbxrLib:
- `android.permission.CAMERA`
- `horizonos.permission.HEADSET_CAMERA`

### 2. Device Settings (On Quest Headset)

1. Put on your Quest headset
2. Go to **Settings > Privacy > Camera Access**
3. Find your app and **enable** camera access

## How It Works

- AbxrLib uses `WebCamTexture` to access the passthrough camera
- The camera feed is displayed in a world-space UI overlay
- No scene camera modifications are made (your scene settings remain unchanged)
- No AR Foundation package is required
- Camera feed is displayed in a Canvas overlay, independent of scene rendering

### Button Visibility

The "Scan QR Code" button is **automatically shown or hidden** based on:
- Device support (Quest 3/3S/Pro only - Quest 2 is not supported)
- Camera permissions (both `CAMERA` and `HEADSET_CAMERA` must be granted)

**If the button is not visible**, it means one of these conditions is not met. The button will automatically appear once all requirements are satisfied (no app restart needed).

## Troubleshooting

### "Scan QR Code" Button Not Visible

If you don't see the "Scan QR Code" button on the PIN pad:

1. **Verify device support**:
   - The button only appears on Quest 3, Quest 3S, or Quest Pro
   - Quest 2 is not supported (camera quality insufficient)

2. **Check camera permissions**:
   - Quest Settings > Privacy & Safety > App Permissions > Headset Cameras
   - Ensure camera access is **enabled** for your app
   - The button will automatically appear once permissions are granted (no app restart needed)

3. **Check logs** for details:
   - Look for: `QR Code button hidden - QR scanning not available`
   - Look for: `HEADSET_CAMERA permission check returned: -1` (denied)
   - Look for: `Device '...' is not supported for QR code reading`

4. **If permissions were just granted**:
   - The button should appear automatically within a few seconds
   - If it doesn't appear, try closing and reopening the PIN pad

### Black Camera Feed

If you see a black camera feed after clicking "Scan QR Code":

1. **Check device permissions**:
   - Quest Settings > Privacy > Camera Access > Your App
   - Enable if disabled
   - The button should be hidden if permissions are denied, but check anyway

2. **Check logs** for permission status:
   - Look for: `HEADSET_CAMERA permission check returned: -1` (denied)
   - Look for: `Camera permission denied` errors
   - Grant permission in Quest Settings > Privacy & Safety > App Permissions > Headset Cameras > Your App > Enable

3. **Wait for camera initialization**:
   - The camera may take a few seconds to start producing valid frames
   - If the feed remains black after 5-10 seconds, check permissions

### Permission Denied

If `HEADSET_CAMERA` permission is denied:

1. The permission is automatically added to AndroidManifest.xml
2. You must grant it in **Quest Settings > Privacy > Camera Access**
3. The "Scan QR Code" button will be **hidden** until permissions are granted
4. Once granted, the button will automatically appear (no app restart needed)
5. Some Quest firmware versions may require the app to be restarted after granting permission


## Testing

1. Build and deploy to Quest 3/3S/Pro
2. Launch the app
3. **Grant camera permissions** (if not already granted):
   - Quest Settings > Privacy & Safety > App Permissions > Headset Cameras > Your App > Enable
4. Open the PIN pad
5. **Verify the "Scan QR Code" button is visible** (if not, see troubleshooting section)
6. Click "Scan QR Code" button
7. You should see the camera feed in a world-space overlay
8. Point the camera at a QR code starting with "ABXR:"

## Notes

- **Quest 2 is not supported** (camera quality insufficient for QR scanning)
- The QR scanner only processes QR codes that start with "ABXR:"
- The "Scan QR Code" button toggles to "Stop Scanning" when active
- Camera feed is displayed in a world-space overlay visible in passthrough mode
- **No scene modifications**: AbxrLib does not modify your scene camera settings
- **No package dependencies**: No Unity packages or OpenXR configuration required - WebCamTexture works directly on Quest
- **Pico compatibility**: No Meta-specific packages or configuration needed, so Pico-only builds won't have conflicts

## Debugging

View logs with:
```bash
adb logcat | grep -i "AbxrLib:"
```

Clear logs and view:
```bash
clear; adb logcat -c && adb logcat | grep -i "AbxrLib:"
```
