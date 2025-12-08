# Meta Quest QR Code Scanner Setup Guide

## Overview

AbxrLib uses `WebCamTexture` to access Meta Quest's passthrough camera for QR code scanning. The implementation is simple and doesn't require AR Foundation or camera settings modifications.

## Required Setup

### 1. Install Unity OpenXR Meta Package

**Required for Meta Quest builds only.** If you're building for Pico or other platforms, you can skip this.

1. Open **Package Manager** (Window > Package Manager)
2. Click the **+** button and select **Add package by name...**
3. Enter: `com.unity.xr.meta-openxr`
4. Click **Add** to install the package

**OR** if using Unity Package Manager UI:
- In Package Manager, switch to **Unity Registry**
- Search for **"Unity OpenXR Meta"**
- Click **Install**

**Note**: This package is **not** included as a dependency in AbxrLib to avoid conflicts with Pico builds. You must add it manually if building for Meta Quest.

### 2. Enable OpenXR Features

1. Open **Project Settings** (Edit > Project Settings)
2. Navigate to **XR Plug-in Management > OpenXR**
3. Under the **Features** section, find and **enable**:
   - **Meta Quest: Camera (Passthrough)**
   - **Meta Quest: Session** (automatically required when Camera is enabled)

### 3. Camera Permissions

The following permissions are **automatically added** to AndroidManifest.xml by AbxrLib:
- `android.permission.CAMERA`
- `horizonos.permission.HEADSET_CAMERA`

### 4. Device Settings (On Quest Headset)

1. Put on your Quest headset
2. Go to **Settings > Privacy > Camera Access**
3. Find your app and **enable** camera access

## How It Works

- AbxrLib uses `WebCamTexture` to access the passthrough camera
- The camera feed is displayed in a world-space UI overlay
- No scene camera modifications are made (your scene settings remain unchanged)
- No AR Foundation package is required
- Camera feed is displayed in a Canvas overlay, independent of scene rendering

## Troubleshooting

### Black Camera Feed

If you see a black camera feed:

1. **Verify OpenXR feature is enabled**:
   - Project Settings > XR Plug-in Management > OpenXR > Features
   - Ensure "Meta Quest: Camera (Passthrough)" is checked
   - Ensure "Meta Quest: Session" is also checked

2. **Check device permissions**:
   - Quest Settings > Privacy > Camera Access > Your App
   - Enable if disabled

3. **Verify Unity OpenXR Meta package is installed**:
   - Check Package Manager for "Unity OpenXR Meta" package
   - If not installed, see step 1 in "Required Setup"

4. **Check logs** for permission status:
   - Look for: `HEADSET_CAMERA permission check returned: -1` (denied)
   - Look for: `Camera permission denied` errors
   - Grant permission in Quest Settings > Privacy > Camera Access

### Permission Denied

If `HEADSET_CAMERA` permission is denied:

1. The permission is automatically added to AndroidManifest.xml
2. You must grant it in **Quest Settings > Privacy > Camera Access**
3. Some Quest firmware versions may require the app to be restarted after granting permission

### OpenXR Features Not Enabled

If you see errors about OpenXR features not being enabled:

1. **Install Unity OpenXR Meta package**: See step 1 in "Required Setup"
2. **Enable the features**: Project Settings > XR Plug-in Management > OpenXR > Features
   - Enable "Meta Quest: Camera (Passthrough)"
   - Enable "Meta Quest: Session" (required by Camera feature)
3. **Rebuild the project**: Clean and rebuild after enabling the features

## Testing

1. Build and deploy to Quest 3/3S/Pro
2. Launch the app
3. Open the PIN pad
4. Click "Scan QR Code" button
5. You should see the camera feed in a world-space overlay
6. Point the camera at a QR code starting with "ABXR:"

## Notes

- **Quest 2 is not supported** (camera quality insufficient for QR scanning)
- The QR scanner only processes QR codes that start with "ABXR:"
- The "Scan QR Code" button toggles to "Stop Scanning" when active
- Camera feed is displayed in a world-space overlay visible in passthrough mode
- **No scene modifications**: AbxrLib does not modify your scene camera settings
- **Pico compatibility**: The Meta OpenXR package is optional, so Pico-only builds won't have conflicts

## Debugging

View logs with:
```bash
adb logcat | grep -i "AbxrLib:"
```

Clear logs and view:
```bash
clear; adb logcat -c && adb logcat | grep -i "AbxrLib:"
```
