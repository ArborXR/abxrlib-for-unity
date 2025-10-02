# Custom Button System Implementation Guide

This guide explains how to use the new configurable button system in AbxrLib for Unity, which allows you to easily control face camera behavior and use direct touch interactions.

## Overview

The implementation provides:
- **Configurable face camera behavior** through the Unity Inspector
- **Direct touch interaction** support for smooth user experience
- **Integration with AbxrLib analytics** for tracking user interactions
- **Easy setup and configuration** through the ConfigInspector

## Configuration Options

### Through Unity Inspector

1. **Open the AbxrLib Configuration**:
   - Navigate to `Resources/AbxrLib.asset` in your project
   - Select the asset to view the configuration in the Inspector

2. **UI Behavior Control Section**:
   - **Auth UI Follow Camera**: Toggle whether UI panels follow the camera
   - **Enable Direct Touch Interaction**: Toggle direct touch vs ray casting
   - **UI Distance From Camera**: Distance from camera when face camera is enabled
   - **UI Vertical Offset**: Vertical offset from camera eye height
   - **UI Horizontal Offset**: Horizontal offset from camera center

### Programmatically

```csharp
// Access configuration
var config = Configuration.Instance;

// Disable face camera behavior
config.authUIFollowCamera = false;

// Enable direct touch interaction
config.enableDirectTouchInteraction = true;

// Set custom positioning
config.uiDistanceFromCamera = 2.0f;
config.uiVerticalOffset = 0.5f;
config.uiHorizontalOffset = 0.2f;
```

## Using the Custom Button System

### Basic Setup

1. **Add CustomButtonSystem Component**:
   ```csharp
   // Add to any GameObject with UI buttons
   CustomButtonSystem buttonSystem = gameObject.AddComponent<CustomButtonSystem>();
   ```

2. **Configure in Inspector**:
   - Assign your buttons to the `customButtons` array
   - Set `useConfigurationSettings` to true to use AbxrLib configuration
   - Set `analyticsPrefix` for your analytics events

### Advanced Usage

1. **Extend CustomButtonSystem**:
   ```csharp
   public class MyCustomButtons : CustomButtonSystem
   {
       protected override void OnCustomButtonClick(int buttonIndex, string buttonName)
       {
           // Your custom button logic here
           Debug.Log($"Button {buttonName} was clicked!");
           
           // Analytics are automatically logged
       }
   }
   ```

2. **Runtime Configuration Changes**:
   ```csharp
   // Toggle direct touch at runtime
   buttonSystem.SetDirectTouchEnabled(true);
   
   // Refresh configuration
   buttonSystem.RefreshConfiguration();
   ```

## Face Camera Control

### Disable Face Camera Globally

```csharp
// Method 1: Through configuration
Configuration.Instance.authUIFollowCamera = false;

// Method 2: Through Abxr API (legacy)
Abxr.AuthUIFollowCamera = false; // This now reads from configuration
```

### Per-Prefab Control

```csharp
// Disable FaceCamera component on specific prefab
FaceCamera faceCameraScript = prefabInstance.GetComponent<FaceCamera>();
if (faceCameraScript != null)
{
    faceCameraScript.enabled = false;
    // OR
    faceCameraScript.useConfigurationValues = false;
    faceCameraScript.faceCamera = false;
}
```

## Direct Touch Interaction

### Enable Direct Touch

```csharp
// Enable direct touch for UI interactions
LaserPointerManager.EnableLaserPointersForInteraction();
```

### Requirements for Direct Touch

Your UI elements need:
- `Button` component (Unity UI)
- `GraphicRaycaster` on the Canvas
- `EventSystem` in the scene
- XR Interaction Toolkit (for laser pointer management)

### Example Setup

```csharp
public class DirectTouchExample : MonoBehaviour
{
    public Button[] buttons;
    
    void Start()
    {
        // Enable direct touch
        LaserPointerManager.EnableLaserPointersForInteraction();
        
        // Set up button listeners
        foreach (Button btn in buttons)
        {
            btn.onClick.AddListener(() => OnButtonClick(btn.name));
        }
    }
    
    void OnButtonClick(string buttonName)
    {
        // Log interaction for analytics
        Abxr.EventInteractionComplete($"button_{buttonName}_click", 
            InteractionType.Select, 
            InteractionResult.Neutral, 
            buttonName);
    }
    
    void OnDestroy()
    {
        // Clean up
        LaserPointerManager.RestoreLaserPointerStates();
    }
}
```

## Analytics Integration

All button interactions are automatically logged with AbxrLib analytics:

```csharp
// Automatic logging format:
// Event: "{analyticsPrefix}_{buttonName}"
// Type: InteractionType.Select
// Result: InteractionResult.Neutral
// Response: buttonName

// Example:
// Event: "custom_button_start_click"
// Type: "select"
// Result: "neutral"
// Response: "Start"
```

## Migration Guide

### From Old System

1. **Replace hardcoded AuthUIFollowCamera**:
   ```csharp
   // Old way
   Abxr.AuthUIFollowCamera = false;
   
   // New way
   Configuration.Instance.authUIFollowCamera = false;
   ```

2. **Update FaceCamera components**:
   - Set `useConfigurationValues = true` on existing FaceCamera components
   - Remove hardcoded positioning values

3. **Replace custom button systems**:
   - Use `CustomButtonSystem` instead of custom implementations
   - Leverage built-in analytics integration

## Best Practices

1. **Use Configuration Settings**: Set `useConfigurationSettings = true` for consistency
2. **Consistent Analytics**: Use descriptive `analyticsPrefix` values
3. **Clean Up**: Always call `LaserPointerManager.RestoreLaserPointerStates()` when done
4. **Test Both Modes**: Test with both direct touch and ray casting enabled
5. **Position Carefully**: Use appropriate distances and offsets for your UI

## Troubleshooting

### Face Camera Not Working
- Check `Configuration.Instance.authUIFollowCamera` is true
- Verify `FaceCamera.useConfigurationValues` is true
- Ensure camera is properly assigned

### Direct Touch Not Working
- Verify XR Interaction Toolkit is installed
- Check `LaserPointerManager.IsXRInteractionToolkitAvailable()`
- Ensure UI has proper `GraphicRaycaster` and `EventSystem`

### Analytics Not Logging
- Check `analyticsPrefix` is set correctly
- Verify button names don't contain special characters
- Ensure AbxrLib is properly initialized

## Example Scenes

### Custom Button System
See the `CustomButtonExample.cs` for a complete implementation example that demonstrates:
- Custom button handling
- Visual feedback (color flashing)
- Sound effects
- Analytics integration
- Runtime configuration changes

### Keyboard System
See the `KeyboardExample.cs` for a complete implementation example that demonstrates:
- Keyboard creation and management
- Event handling and callbacks
- Input monitoring
- Configuration management
- Analytics integration
- Error handling

### Hand Tracking Buttons
See the `HandTrackingButtonExample.cs` for hand tracking button interactions:
- Hand tracking button setup
- Visual feedback for hand interactions
- Analytics integration for hand tracking
- Configuration management

## Documentation

- **CustomButtonExample.cs**: Complete custom button system implementation
- **KeyboardExample.cs**: Comprehensive keyboard system usage
- **HandTrackingButtonExample.cs**: Hand tracking button interactions
- **KeyboardGuide.md**: Detailed keyboard system documentation
- **HandTrackingGuide.md**: Hand tracking implementation guide
- **PanelArchitectureGuide.md**: Complete guide to the dual-panel architecture (authentication keyboards vs independent panels)

## Specialized Documentation

For users who need detailed information about specific systems:

- **Keyboard System**: See `../Keyboard/README.md` for comprehensive keyboard system documentation
- **Custom Button System**: See this README for custom button implementation guide
- **Hand Tracking**: See `HandTrackingGuide.md` for hand tracking implementation guide
- **Panel Architecture**: See `PanelArchitectureGuide.md` for understanding the dual-panel system (authentication keyboards vs independent panels)
