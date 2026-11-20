# Panel Architecture Guide

This guide explains the current panel architecture in AbxrLib for Unity, which distinguishes between authentication keyboards with built-in panels and independent panel prefabs for other UI messages.

## Overview

The AbxrLib UI system now uses a dual-panel approach:

1. **Authentication Keyboards**: Have built-in panels (like PIN pad)
2. **Independent Panels**: Used for other messages (like exit polls, notifications)

## Authentication Keyboards with Built-in Panels

### What Changed

Previously, authentication keyboards required separate panel prefabs to be instantiated alongside them. Now, the keyboard prefabs (`AbxrKeyboard` and `AbxrPinPad`) include their own integrated panel structures.

### Current Structure

```
AbxrKeyboard (Root GameObject)
├── PanelCanvas (Canvas with World Space rendering)
│   ├── Panel (Background panel)
│   ├── Input Panel (Input field and prompt)
│   ├── Key Panel (Keyboard keys)
│   └── Control Panel (Space, Delete, Submit buttons)
└── FaceCamera (Positioning component)
```

### Benefits

- **Self-contained**: No need to manage separate panel instances
- **Consistent styling**: Built-in panels match the keyboard design
- **Simplified usage**: Just call `KeyboardHandler.Create()` - panels are automatic
- **Better performance**: Fewer GameObjects to manage

### Usage

```csharp
// Authentication keyboards automatically include panels
KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
KeyboardHandler.Create(KeyboardHandler.KeyboardType.PinPad);

// Set prompts - the built-in panel will display them
KeyboardHandler.SetPrompt("Please enter your credentials");
```

## Independent Panel Prefabs

### When to Use

Independent panel prefabs are still used for:

- **Exit polls** (rating, thumbs up/down, multiple choice)
- **Notifications** and alerts
- **Custom messages** that aren't part of authentication
- **Any UI that needs a panel but isn't a keyboard**

### Available Prefabs

- `AbxrDarkPanelWithText` - Basic dark panel with text
- `AbxrExitPollRating` - Rating poll interface
- `AbxrExitPollThumbs` - Thumbs up/down interface
- `AbxrExitPollMulti` - Multiple choice interface

### Usage Example

```csharp
// Load independent panel prefab
GameObject panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
GameObject panelInstance = Instantiate(panelPrefab);

// Set custom message text
TextMeshProUGUI panelText = panelInstance.GetComponentInChildren<TextMeshProUGUI>();
panelText.text = "Your custom message here";

// Clean up when done
Destroy(panelInstance, 5f); // Auto-destroy after 5 seconds
```

### Exit Poll Example

```csharp
// Exit polls use independent panels
ExitPollHandler.AddPoll(
    "How was your experience?", 
    ExitPollHandler.PollType.Rating, 
    null, 
    (response) => Debug.Log($"User response: {response}")
);
```

## Migration Guide

### From Old System

If you were previously using separate panel prefabs with keyboards:

**Old Way:**
```csharp
// Old approach - separate panel and keyboard
GameObject panel = Instantiate(panelPrefab);
GameObject keyboard = Instantiate(keyboardPrefab);
```

**New Way:**
```csharp
// New approach - keyboard includes panel automatically
KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
```

### For Custom UI Messages

If you need panels for non-authentication messages:

```csharp
// Use independent panel prefabs
GameObject panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
GameObject panelInstance = Instantiate(panelPrefab);
// Configure and use as needed
```

## Best Practices

### Authentication Keyboards

1. **Use the built-in system**: Don't try to add separate panels to authentication keyboards
2. **Set prompts properly**: Use `KeyboardHandler.SetPrompt()` to update the built-in panel text
3. **Handle events**: Subscribe to `OnKeyboardCreated` and `OnKeyboardDestroyed` events
4. **Clean up**: Always call `KeyboardHandler.Destroy()` when done

### Independent Panels

1. **Choose the right prefab**: Use `AbxrDarkPanelWithText` for simple messages, specialized prefabs for polls
2. **Manage lifecycle**: Always destroy panel instances when done
3. **Set text properly**: Use `GetComponentInChildren<TextMeshProUGUI>()` to find and update text
4. **Consider positioning**: Independent panels may need manual positioning or FaceCamera components

### General

1. **Don't mix approaches**: Don't try to use independent panels with authentication keyboards
2. **Use configuration**: Leverage AbxrLib configuration for consistent behavior
3. **Test both types**: Ensure both authentication keyboards and independent panels work in your app
4. **Monitor performance**: Built-in panels are more efficient than separate instances

## Examples

### Complete Authentication Flow

```csharp
public class AuthenticationExample : MonoBehaviour
{
    public void StartAuthentication()
    {
        // Authentication keyboards automatically include panels
        KeyboardHandler.Create(KeyboardHandler.KeyboardType.FullKeyboard);
        KeyboardHandler.SetPrompt("Please enter your username and password");
        
        // Handle completion
        KeyboardHandler.OnKeyboardDestroyed += OnAuthenticationComplete;
    }
    
    private void OnAuthenticationComplete()
    {
        Debug.Log("Authentication completed");
        KeyboardHandler.OnKeyboardDestroyed -= OnAuthenticationComplete;
    }
}
```

### Custom Message Display

```csharp
public class MessageDisplayExample : MonoBehaviour
{
    public void ShowCustomMessage(string message)
    {
        // Use independent panel for custom messages
        GameObject panelPrefab = Resources.Load<GameObject>("Prefabs/AbxrDarkPanelWithText");
        GameObject panelInstance = Instantiate(panelPrefab);
        
        // Set message text
        var panelText = panelInstance.GetComponentInChildren<TextMeshProUGUI>();
        panelText.text = message;
        
        // Auto-destroy after 3 seconds
        Destroy(panelInstance, 3f);
    }
}
```

### Exit Poll Integration

```csharp
public class PollExample : MonoBehaviour
{
    public void ShowRatingPoll()
    {
        // Exit polls use independent panels automatically
        ExitPollHandler.AddPoll(
            "Rate your experience", 
            ExitPollHandler.PollType.Rating, 
            null, 
            OnPollResponse
        );
    }
    
    private void OnPollResponse(string response)
    {
        Debug.Log($"User rated: {response}");
    }
}
```

## Troubleshooting

### Authentication Keyboard Issues

- **Panel not showing**: Check if `KeyboardHandler.Create()` was called
- **Prompt not updating**: Use `KeyboardHandler.SetPrompt()` after creation
- **Positioning issues**: Check FaceCamera component and configuration settings

### Independent Panel Issues

- **Prefab not found**: Verify the prefab exists in `Resources/Prefabs/`
- **Text not updating**: Use `GetComponentInChildren<TextMeshProUGUI>()` to find text component
- **Memory leaks**: Always destroy panel instances when done

### General Issues

- **Mixed usage**: Don't try to use independent panels with authentication keyboards
- **Configuration conflicts**: Ensure configuration settings are consistent
- **Event handling**: Properly subscribe/unsubscribe from keyboard events

## Summary

The new panel architecture provides:

- **Simplified authentication**: Built-in panels for keyboards
- **Flexible messaging**: Independent panels for other UI needs
- **Better performance**: Fewer GameObjects and cleaner code
- **Consistent styling**: Unified design across all UI elements

Use authentication keyboards for credential entry and independent panels for everything else. This approach gives you the best of both worlds: simplicity for common use cases and flexibility for custom needs.
