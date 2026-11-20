# Hand Tracking Button System Implementation Guide

This guide explains how to use the new HandTrackingButtonSystem in AbxrLib for Unity, specifically designed for applications using hand tracking with collider-based interactions.

## Overview

The HandTrackingButtonSystem provides:
- **Collider-based button detection** for hand tracking applications
- **Integration with XR Interaction Toolkit** hand tracking
- **Configurable face camera behavior** through the Unity Inspector
- **Direct touch interaction** support for smooth user experience
- **Integration with AbxrLib analytics** for tracking user interactions
- **Easy setup and configuration** through the ConfigInspector

## Why Hand Tracking Button System?

Unlike traditional Unity UI Button components that work with ray casting, hand tracking applications typically use:
- **Collider-based interactions** where hands physically touch 3D objects
- **Direct touch detection** using OnTriggerEnter/OnTriggerStay
- **3D spatial positioning** rather than 2D UI canvas positioning
- **Hand-specific interaction patterns** optimized for natural hand movements

## Setup Instructions

### 1. Basic Setup

1. **Add HandTrackingButtonSystem Component**:
   ```csharp
   // Add to any GameObject that will contain your hand tracking buttons
   HandTrackingButtonSystem handTrackingSystem = gameObject.AddComponent<HandTrackingButtonSystem>();
   ```

2. **Configure in Inspector**:
   - Assign your hand tracking buttons to the `handTrackingButtons` array
   - Set `useConfigurationSettings` to true to use AbxrLib configuration
   - Set `analyticsPrefix` for your analytics events (e.g., "main_menu_")

### 2. Create Hand Tracking Buttons

For each button you want to create:

1. **Create a GameObject** with a Collider:
   ```csharp
   // Create button GameObject
   GameObject buttonObject = new GameObject("StartButton");
   
   // Add collider (BoxCollider, SphereCollider, etc.)
   Collider buttonCollider = buttonObject.AddComponent<BoxCollider>();
   buttonCollider.isTrigger = true; // Important for hand tracking
   
   // Add visual representation (optional)
   Renderer buttonRenderer = buttonObject.AddComponent<MeshRenderer>();
   buttonRenderer.material = yourButtonMaterial;
   ```

2. **Configure HandTrackingButton**:
   ```csharp
   HandTrackingButton handButton = new HandTrackingButton();
   handButton.buttonName = "Start";
   handButton.buttonObject = buttonObject;
   handButton.buttonCollider = buttonCollider;
   handButton.cooldownTime = 0.5f; // Prevent rapid activations
   ```

### 3. Hand Tracking Setup

Your hand tracking system needs to:

1. **Tag Hand Colliders**:
   ```csharp
   // Tag your hand/finger colliders appropriately
   handCollider.tag = "Hand"; // or "Finger", "HandTracking"
   ```

2. **Set Up Layers** (Optional):
   ```csharp
   // Create a "HandTracking" layer for better organization
   handCollider.gameObject.layer = LayerMask.NameToLayer("HandTracking");
   ```

3. **Configure XR Interaction Toolkit**:
   - Ensure XR Interaction Toolkit is installed
   - Set up hand tracking in your XR Origin
   - Configure hand tracking colliders

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

## Advanced Usage

### 1. Extend HandTrackingButtonSystem

```csharp
public class MyHandTrackingButtons : HandTrackingButtonSystem
{
    protected override void OnHandTrackingButtonClick(int buttonIndex, string buttonName)
    {
        // Your custom button logic here
        Debug.Log($"Hand tracking button {buttonName} was activated!");
        
        // Analytics are automatically logged
    }
}
```

### 2. Custom Hand Detection

You can customize how hand detection works by modifying the `IsHandCollider` method:

```csharp
// In HandTrackingButtonInteraction.cs, customize this method:
private bool IsHandCollider(Collider other)
{
    // Your custom hand detection logic
    return other.CompareTag("MyCustomHandTag") || 
           other.GetComponent<MyHandComponent>() != null ||
           other.gameObject.layer == LayerMask.NameToLayer("MyHandLayer");
}
```

### 3. Runtime Configuration Changes

```csharp
// Toggle direct touch at runtime
handTrackingSystem.SetDirectTouchEnabled(true);

// Refresh configuration
handTrackingSystem.RefreshConfiguration();
```

## Analytics Integration

All hand tracking button interactions are automatically logged with AbxrLib analytics:

```csharp
// Automatic logging format:
// Event: "{analyticsPrefix}_{buttonName}"
// Type: InteractionType.Select
// Result: InteractionResult.Neutral
// Response: buttonName

// Example:
// Event: "hand_button_start_activated"
// Type: "select"
// Result: "neutral"
// Response: "Start"
```

## Best Practices

### 1. Button Design
- **Size**: Make buttons large enough for easy hand interaction (minimum 2-3cm in real world scale)
- **Spacing**: Provide adequate spacing between buttons to prevent accidental activations
- **Visual Feedback**: Use clear visual indicators for button states
- **Audio Feedback**: Add audio cues for button activations

### 2. Hand Tracking Optimization
- **Cooldown**: Use appropriate cooldown times to prevent rapid activations
- **Trigger Colliders**: Always use `isTrigger = true` for hand tracking colliders
- **Layer Management**: Use layers to organize hand tracking objects
- **Performance**: Avoid complex collider shapes for better performance

### 3. Configuration
- **Use Configuration Settings**: Set `useConfigurationSettings = true` for consistency
- **Consistent Analytics**: Use descriptive `analyticsPrefix` values
- **Test Both Modes**: Test with both direct touch and ray casting enabled
- **Position Carefully**: Use appropriate distances and offsets for your UI

## Troubleshooting

### Hand Tracking Not Working
- Verify hand colliders have appropriate tags ("Hand", "Finger", "HandTracking")
- Check that button colliders have `isTrigger = true`
- Ensure XR Interaction Toolkit hand tracking is properly configured
- Verify hand tracking is enabled in your XR Origin

### Buttons Not Activating
- Check that `HandTrackingButtonInteraction` component is attached to button objects
- Verify button colliders are properly configured
- Check cooldown settings - buttons may be in cooldown period
- Ensure hand colliders are entering the button trigger zones

### Analytics Not Logging
- Check `analyticsPrefix` is set correctly
- Verify button names don't contain special characters
- Ensure AbxrLib is properly initialized
- Check that `OnHandTrackingButtonClick` is being called

### Face Camera Issues
- Check `Configuration.Instance.authUIFollowCamera` setting
- Verify `FaceCamera.useConfigurationValues` is true
- Ensure camera is properly assigned
- Check positioning values in configuration

## Example Scenes

See the `HandTrackingButtonExample.cs` for a complete implementation example that demonstrates:
- Hand tracking button setup
- Visual feedback (color flashing)
- Audio feedback
- Analytics integration
- Runtime configuration changes
- Programmatic button testing

## Migration from Unity UI Buttons

If you're migrating from Unity UI Button components:

1. **Replace Button Components**: Remove Unity UI Button components
2. **Add Colliders**: Add appropriate colliders to your button GameObjects
3. **Set Up Hand Tracking**: Configure hand tracking colliders and tags
4. **Use HandTrackingButtonSystem**: Replace your custom button system
5. **Update Analytics**: Update your analytics calls to use the new system

## Performance Considerations

- **Collider Complexity**: Use simple collider shapes (Box, Sphere) for better performance
- **Collision Detection**: Use trigger-based detection rather than physics-based
- **Update Frequency**: Hand tracking systems typically run at 60-90Hz
- **Memory Usage**: HandTrackingButtonSystem is lightweight and efficient

This system provides a robust foundation for hand tracking applications while maintaining full compatibility with AbxrLib's configuration and analytics systems.
