# Meta Quest QR Code Scanner Setup Guide

## Required Configuration in Unity Demo App

To enable QR code scanning on Meta Quest 3/3S/Pro, you need to configure the following in your Unity project:

### 1. Install Unity OpenXR Meta Package

1. Open **Package Manager** (Window > Package Manager)
2. Click the **+** button and select **Add package by name...**
3. Enter: `com.unity.xr.openxr.meta`
4. Click **Add** to install the package

**OR** if using Unity Package Manager UI:
- In Package Manager, switch to **Unity Registry**
- Search for **"Unity OpenXR Meta"**
- Click **Install**

### 2. Enable Passthrough Camera API in OpenXR Settings

1. Open **Project Settings** (Edit > Project Settings)
2. Navigate to **XR Plug-in Management > OpenXR**
3. Under the **Features** section, find and **enable**:
   - **Meta Quest: Camera (Passthrough)**
   - **Note**: Enabling this will automatically require you to also enable **Meta Quest: Session** - enable that as well

### 3. Install AR Foundation Package (Required)

**CRITICAL**: The Meta OpenXR Camera documentation states that **AR Camera Manager component is required** for passthrough to work.

1. Open **Package Manager** (Window > Package Manager)
2. Switch to **Unity Registry**
3. Search for **"AR Foundation"**
4. Click **Install**
5. **Note**: AbxrLib will automatically add the AR Camera Manager component to your camera at runtime, but you can also add it manually in the Unity Editor

### 4. Configure Camera Settings

**Required for Passthrough**:
1. Select your **Main Camera** in the scene
2. Set **Clear Flags** to **Solid Color**
3. Set **Background Color** alpha channel to **0** (transparent)
   - This allows passthrough video to be visible behind rendered content
4. **Note**: AbxrLib will automatically configure these settings at runtime if not already set

### 5. Verify Meta XR Core SDK

- Ensure **Meta XR Core SDK v74+** is installed
- This is included with the **Unity OpenXR Meta** package

### 6. Camera Permissions

The following permissions are automatically added to AndroidManifest.xml by AbxrLib:
- `android.permission.CAMERA`
- `horizonos.permission.HEADSET_CAMERA`

### 7. Device Settings (On Quest Headset)

1. Put on your Quest headset
2. Go to **Settings > Privacy > Camera Access**
3. Find your app and **enable** camera access

### 8. Project Settings (Recommended)

For best compatibility, ensure:
- **Color Space**: Linear (Project Settings > Player > Other Settings)
- **Graphics API**: Vulkan (for Android builds)
- **Scripting Backend**: IL2CPP
- **Target Architecture**: ARM64

## Troubleshooting

### Black Camera Feed

If you see a black camera feed:

1. **Verify AR Camera Manager is present**:
   - Check your Main Camera GameObject
   - Ensure **AR Camera Manager** component is attached and **enabled**
   - If missing, AbxrLib will try to add it automatically, but you may need to install AR Foundation package first
   - Install AR Foundation: Window > Package Manager > Unity Registry > AR Foundation

2. **Check camera Clear Flags**:
   - Main Camera > Clear Flags should be **Solid Color**
   - Background Color alpha should be **0**
   - AbxrLib will try to set these automatically, but verify they're correct

3. **Check logs** for permission status:
   - Look for: `HEADSET_CAMERA permission check returned: -1`
   - Look for: `Camera subsystem permission status: False`
   - This means permission is denied or AR Camera Manager is missing

4. **Verify OpenXR feature is enabled**:
   - Project Settings > XR Plug-in Management > OpenXR > Features
   - Ensure "Meta Quest: Camera (Passthrough)" is checked
   - Ensure "Meta Quest: Session" is also checked (required by Camera feature)

5. **Check device permissions**:
   - Quest Settings > Privacy > Camera Access > Your App
   - Enable if disabled

6. **Verify Unity OpenXR Meta package is installed**:
   - Check Package Manager for "Unity OpenXR Meta" package
   - Should be version 1.x or higher
   - If not installed, see step 1 in "Required Configuration"

### Permission Denied

If `HEADSET_CAMERA` permission is denied:

1. The permission is automatically added to AndroidManifest.xml
2. You must grant it in **Quest Settings > Privacy > Camera Access**
3. Some Quest firmware versions may require the app to be restarted after granting permission

### Passthrough Camera API Types Not Found

If you see: `Passthrough camera type not found via reflection`:

1. **Install Unity OpenXR Meta package**: See step 1 in "Required Configuration"
2. **Install AR Foundation package**: See step 3 in "Required Configuration" (AR Camera Manager is required)
3. **Enable the feature**: Project Settings > XR Plug-in Management > OpenXR > Features > Meta Quest: Camera (Passthrough)
4. **Enable required feature**: Also enable "Meta Quest: Session" (required by Camera feature)
5. **Rebuild the project**: Clean and rebuild after enabling the features

### Permission Granted is False

If logs show `Camera subsystem permission status: False`:

1. **Verify AR Camera Manager component is attached and enabled** on your Main Camera
2. **Verify AR Foundation package is installed** (required for AR Camera Manager)
3. **Check camera Clear Flags** is set to Solid Color with alpha=0
4. **Verify OpenXR feature is enabled** in Project Settings
5. **Check Quest device permissions** in Settings > Privacy > Camera Access
6. The subsystem's `permissionGranted` property may be false if AR Camera Manager is missing, even if Android permissions are granted

## Testing

1. Build and deploy to Quest 3/3S/Pro
2. Launch the app
3. Open the PIN pad
4. Click "Scan QR Code" button
5. You should see the camera feed in a passthrough window
6. Point the camera at a QR code starting with "ABXR:"

## Notes

- **Quest 2 is not supported** (camera quality insufficient for QR scanning)
- The QR scanner only processes QR codes that start with "ABXR:"
- The "Scan QR Code" button toggles to "Stop Scanning" when active
- Camera feed is displayed in a world-space overlay visible in passthrough mode
- adb logcat | grep -i "AbxrLib\|Unity"

- clear; adb logcat -c && adb logcat | grep -i "AbxrLib:"
- clear; adb logcat | grep -i "AbxrLib:"