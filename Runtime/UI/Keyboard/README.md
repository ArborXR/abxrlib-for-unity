# Keyboard System

The AbxrLib keyboard system provides comprehensive VR input capabilities with both full keyboards and PIN pads, featuring configurable behavior, analytics integration, and seamless authentication flow integration.

## Overview

The keyboard system includes:
- **Full keyboard** for text input with shift functionality
- **PIN pad** for numeric input
- **Configurable face camera behavior** through the Unity Inspector
- **Direct touch interaction** support for smooth user experience
- **Integration with AbxrLib analytics** for tracking user interactions
- **Panel-based UI structure** for better organization and styling

## Quick Start

```csharp
using AbxrLib.Runtime.UI.Keyboard;

// Create a full keyboard
KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);

// Create a PIN pad
KeyboardHandler.Create(KeyboardHandler.KeyboardType.PinPad);

// Set a custom prompt
KeyboardHandler.SetPrompt("Please enter your username");

// Destroy when done
KeyboardHandler.Destroy();
```

## Keyboard Types

### Full Keyboard
- Complete QWERTY layout with numbers and symbols
- Shift functionality for uppercase letters and symbols
- Space, delete, and submit buttons
- Suitable for text input, usernames, passwords, etc.

### PIN Pad
- Numeric keypad (0-9)
- Submit and delete functionality
- Suitable for PIN entry, numeric codes, etc.

## Basic Usage

### Creating Keyboards

```csharp
// Create a full keyboard
KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);

// Create a PIN pad
KeyboardHandler.Create(KeyboardHandler.KeyboardType.PinPad);

// Set custom prompt
KeyboardHandler.SetPrompt("Please enter your credentials");
```

### Destroying Keyboards

```csharp
// Destroy the current keyboard instance
KeyboardHandler.Destroy();
```

### Keyboard Events

```csharp
// Subscribe to keyboard events
KeyboardHandler.OnKeyboardCreated += OnKeyboardCreated;
KeyboardHandler.OnKeyboardDestroyed += OnKeyboardDestroyed;

private void OnKeyboardCreated()
{
    Debug.Log("Keyboard created successfully");
    // Your custom logic here
}

private void OnKeyboardDestroyed()
{
    Debug.Log("Keyboard destroyed");
    // Your custom logic here
}

// Don't forget to unsubscribe
private void OnDestroy()
{
    KeyboardHandler.OnKeyboardCreated -= OnKeyboardCreated;
    KeyboardHandler.OnKeyboardDestroyed -= OnKeyboardDestroyed;
}
```

## Advanced Usage

### Monitoring Keyboard Input

```csharp
public class KeyboardInputMonitor : MonoBehaviour
{
    private void Update()
    {
        // Check if keyboard is active and has input
        if (KeyboardManager.Instance != null && KeyboardManager.Instance.inputField != null)
        {
            string currentInput = KeyboardManager.Instance.inputField.text;
            Debug.Log($"Current input: {currentInput}");
        }
    }
}
```

### Custom Keyboard Behavior

```csharp
public class CustomKeyboardHandler : MonoBehaviour
{
    [Header("Custom Settings")]
    public string customPrompt = "Enter your information";
    public bool useCustomPositioning = false;
    
    public void CreateCustomKeyboard()
    {
        // Create keyboard
        KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
        
        // Set custom prompt
        KeyboardHandler.SetPrompt(customPrompt);
        
        // Custom positioning logic here if needed
        if (useCustomPositioning)
        {
            // Your custom positioning code
        }
    }
}
```

## Configuration

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

## Integration with Authentication

The keyboard system automatically integrates with AbxrLib's authentication flow:

```csharp
// Authentication automatically creates appropriate keyboards
Abxr.Authenticate("username", "password");        // Full keyboard
Abxr.Authenticate("assessmentPin", "123456");     // PIN pad
```

## Analytics Integration

All keyboard interactions are automatically logged:

```csharp
// Automatic analytics events include:
// - Keyboard creation/destruction
// - Key press events
// - Submit events
// - Error events

// You can also log custom events
Abxr.EventInteractionComplete("custom_keyboard_action", 
    Abxr.InteractionType.Select, 
    Abxr.InteractionResult.Correct, 
    "keyboard_used");
```

## Panel Structure

The authentication keyboard prefabs now include built-in panel structures that provide:

- **Better visual organization** with integrated background panels
- **Improved styling** with consistent theming
- **Enhanced user experience** with clear visual boundaries
- **Responsive design** that adapts to different screen sizes
- **Self-contained UI** that doesn't require separate panel prefabs

### Authentication Keyboard Panel Components

- **PanelCanvas**: The main canvas containing the entire keyboard interface
- **Panel**: Background panel providing visual structure
- **Input Panel**: Houses the input field and prompt text
- **Key Panel**: Contains all the keyboard keys
- **Control Panel**: Houses special keys (space, delete, submit)

### Independent Panel Usage

For non-authentication messages (like exit polls, notifications, etc.), you can still use independent panel prefabs:

```csharp
// Load independent panel prefab for custom messages
GameObject panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
GameObject panelInstance = Instantiate(panelPrefab);

// Set custom message text
TextMeshProUGUI panelText = panelInstance.GetComponentInChildren<TextMeshProUGUI>();
panelText.text = "Your custom message here";
```

This approach allows you to:
- Use built-in panels for authentication keyboards (automatic)
- Use independent panels for other UI messages (manual)
- Maintain consistent styling across different UI elements

## Face Camera Control

### Global Control

```csharp
// Disable face camera behavior globally
Configuration.Instance.authUIFollowCamera = false;

// Enable face camera behavior globally
Configuration.Instance.authUIFollowCamera = true;
```

### Per-Instance Control

```csharp
// The keyboard system automatically applies configuration settings
// when creating keyboard instances
KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);

// The FaceCamera component on the keyboard prefab will automatically
// use the configuration values if useConfigurationValues is true
```

## Direct Touch Interaction

### Enabling Direct Touch

```csharp
// Direct touch is automatically enabled when creating keyboards
KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);

// The system automatically calls:
// LaserPointerManager.EnableLaserPointersForInteraction();
```

### Requirements for Direct Touch

Your project needs:
- XR Interaction Toolkit installed
- `GraphicRaycaster` on the Canvas
- `EventSystem` in the scene
- Proper layer configuration

## Error Handling

```csharp
public class RobustKeyboardHandler : MonoBehaviour
{
    public void CreateKeyboardSafely()
    {
        try
        {
            KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
            Debug.Log("Keyboard created successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to create keyboard: {ex.Message}");
            
            // Log error for analytics
            Abxr.EventInteractionComplete("keyboard_creation_error", 
                Abxr.InteractionType.Select, 
                Abxr.InteractionResult.Incorrect, 
                ex.Message);
        }
    }
}
```

## Best Practices

1. **Always Check for Existing Keyboards**: Prevent duplicate keyboard creation
2. **Use Configuration Settings**: Leverage AbxrLib configuration for consistency
3. **Handle Events Properly**: Subscribe and unsubscribe from keyboard events
4. **Clean Up Resources**: Always destroy keyboards when done
5. **Test Both Modes**: Test with both direct touch and ray casting enabled
6. **Monitor Input**: Use KeyboardManager.Instance to monitor user input
7. **Log Analytics**: Take advantage of automatic analytics integration

## Troubleshooting

### Keyboard Not Appearing
- Check if AbxrLib is properly initialized
- Verify the keyboard prefab exists in Resources/Prefabs/
- Check console for error messages
- Ensure the scene has proper XR setup

### Face Camera Not Working
- Check `Configuration.Instance.authUIFollowCamera` is true
- Verify `FaceCamera.useConfigurationValues` is true on the prefab
- Ensure camera is properly assigned

### Direct Touch Not Working
- Verify XR Interaction Toolkit is installed
- Check `LaserPointerManager.IsXRInteractionToolkitAvailable()`
- Ensure UI has proper `GraphicRaycaster` and `EventSystem`

### Input Not Registering
- Check if KeyboardManager.Instance exists
- Verify the input field is properly assigned
- Check for null reference exceptions in console

## Examples

### Complete Implementation
See `KeyboardExample.cs` in the Examples folder for a complete implementation that demonstrates:
- Keyboard creation and destruction
- Event handling
- Input monitoring
- Analytics integration
- Configuration management
- Error handling

### Documentation
See `KeyboardGuide.md` in the Examples folder for detailed implementation guidance.

## Migration from Old System

If you're upgrading from an older version:

1. **Update Keyboard Creation**: Use `KeyboardHandler.Create()` instead of direct prefab instantiation
2. **Handle Events**: Subscribe to `OnKeyboardCreated` and `OnKeyboardDestroyed` events
3. **Use Configuration**: Leverage the new configuration system for consistent behavior
4. **Update Analytics**: Take advantage of automatic analytics integration

## Performance Considerations

- Keyboards are created on-demand and destroyed when no longer needed
- The system prevents duplicate keyboard creation
- Face camera behavior is optimized for VR performance
- Direct touch interaction is more efficient than ray casting for close interactions

## API Reference

### KeyboardHandler

#### Static Methods
- `Create(KeyboardType keyboardType)` - Creates a keyboard instance
- `Destroy()` - Destroys the current keyboard instance
- `SetPrompt(string prompt)` - Sets the prompt text for the keyboard
- `RefreshPrefabs()` - Reloads prefabs from configuration

#### Events
- `OnKeyboardCreated` - Fired when a keyboard is created
- `OnKeyboardDestroyed` - Fired when a keyboard is destroyed

#### Enums
- `KeyboardType.FullKeyboard` - Full QWERTY keyboard
- `KeyboardType.PinPad` - Numeric PIN pad

### KeyboardManager

#### Properties
- `Instance` - Singleton instance of the keyboard manager
- `inputField` - The input field component
- `shiftButton1`, `shiftButton2` - Shift buttons
- `deleteButton` - Delete button
- `spaceButton` - Space button
- `submitButton` - Submit button

### KeyboardKey

#### Properties
- `character` - The character this key represents
- `shiftCharacter` - The character when shift is active
- `keyLabel` - The text component displaying the key

#### Methods
- Automatically handles shift state and character input
- Integrates with KeyboardManager for input processing
