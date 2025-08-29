# ABXR SDK for Unity

The name "ABXR" stands for "Analytics Backbone for XR"—a flexible, open-source foundation for capturing and transmitting spatial, interaction, and performance data in XR. When combined with **ArborXR Insights**, ABXR transforms from a lightweight instrumentation layer into a full-scale enterprise analytics solution—unlocking powerful dashboards, LMS/BI integrations, and AI-enhanced insights.

## Table of Contents
1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Sending Data](#sending-data)
   - [Events](#events)
   - [Analytics Event Wrappers](#analytics-event-wrappers-essential-for-all-developers)
   - [Timed Events](#timed-events)
   - [Super Properties](#super-properties)
   - [Logging](#logging)
   - [Storage](#storage)
   - [Telemetry](#telemetry)
   - [AI Integration](#ai-integration)
   - [Exit Polls](#exit-polls)
   - [Metadata Formats](#metadata-formats)
5. [Advanced Features](#advanced-features)
   - [Module Targets](#module-targets)
   - [Authentication](#authentication)
   - [Headset Removal](#headset-removal)
   - [Session Management](#session-management)
   - [Debug Window](#debug-window)
   - [ArborXR Device Management](#arborxr-device-management)
   - [Mixpanel Compatibility](#mixpanel-compatibility)
8. [Support](#support)
   - [Resources](#resources)
   - [FAQ](#faq)

---

## Introduction

### Overview

The **ABXR SDK for Unity** is an open-source analytics and data collection library that provides developers with the tools to collect and send XR data to any service of their choice. This library enables scalable event tracking, telemetry, and session-based storage—essential for enterprise and education XR environments.

> **Quick Start:** Most developers can integrate ABXR SDK and log their first event in under **15 minutes**.

**Why Use ABXR SDK?**

- **Open-Source** & portable to any backend—no vendor lock-in  
- **Quick integration**—track user interactions in minutes  
- **Secure & scalable**—ready for enterprise use cases  
- **Pluggable with ArborXR Insights**—seamless access to LMS/BI integrations, session replays, AI diagnostics, and more

### Core Features

The ABXR SDK provides:
- **Event Tracking:** Monitor user behaviors, interactions, and system events.
- **Spatial & Hardware Telemetry:** Capture headset/controller movement and hardware metrics.
- **Object & System Info:** Track XR objects and environmental state.
- **Storage & Session Management:** Support resumable training and long-form experiences.
- **Logs:** Developer and system-level logs available across sessions.

### Backend Services

The ABXR SDK is designed to work with any backend service that implements the ABXR protocol. Currently supported services include:

#### ArborXR Insights
When paired with [**ArborXR Insights**](https://arborxr.com/insights), ABXR becomes a full-service platform offering:
- Seamless data pipeline from headset to dashboard
- End-to-end session tracking, analysis, and replay
- AI-driven insights for content quality, learner performance, and device usage
- One-click LMS and BI integrations for scalable deployments

#### Custom Implementations
Developers can implement their own backend services by following the ABXR protocol specification. This allows for complete control over data storage, processing, and visualization.

---

## Installation

### Unity Package Installation

1. Open Unity and go to `Window > Package Manager`.
2. Select the '+' dropdown and choose **'Add package from git URL'**.
3. Use the GitHub repo URL:
   ```
   https://github.com/ArborXR/abxrlib-for-unity.git
   ```
4. Once imported, you will see `Analytics for XR` in your Unity toolbar.

---

## Configuration

### Using with ArborXR Insights

To use the ABXR SDK with ArborXR Insights:

#### Get Your Credentials
1. Go to the ArborXR Insights web app and log in.
2. Grab these three values from the **View Data** screen of the specific app you are configuring:
- App ID
- Organization ID
- Authentication Secret

#### Configure Unity Project

> **⚠️ Security Note:** For production builds distributed to third parties, avoid compiling `Org ID` and `Auth Secret` directly into your Unity project. These credentials should only be compiled into builds when creating custom applications for specific individual clients. For general distribution, use ArborXR-managed devices or implement runtime credential provisioning.

1. Open `Analytics for XR > Configuration` in the Unity Editor.
2. **For Development/Testing:** Paste in the App ID, Org ID, and Auth Secret. All 3 are required if you are testing from Unity itself.
3. **For Production Builds:** Only include the App ID. Leave Org ID and Auth Secret empty for third-party distribution.

#### Alternative for Managed Headsets:
If you're using an ArborXR-managed device, only the App ID is required. The Org ID and Auth Secret auto-fill. 
On any non-managed headset, you must manually enter all three values for testing purposes only.

### Using with Other Backend Services
For information on implementing your own backend service or using other compatible services, please refer to the ABXR protocol specification.

---

## Sending Data

### Events
```cpp
//C# Event Method Signatures
public void Abxr.Event(string name);
public void Abxr.Event(string name, Dictionary<string, string> meta = null);
public void Abxr.Event(string name, Dictionary<string, string> meta = null, Vector3 location_data = null);

// Example Usage - Basic Event
Abxr.Event("button_pressed");

// Example Usage - Event with Metadata
Abxr.Event("item_collected", new Dictionary<string, string> {
    {"item_type", "coin"},
    {"item_value", "100"}
});

// Example Usage - Event with Metadata and Location
Abxr.Event("player_teleported", 
    new Dictionary<string, string> {{"destination", "spawn_point"}},
    new Vector3(1.5f, 0.0f, -3.2f)
);
```
**Parameters:**
- `name` (string): The name of the event. Use snake_case for better analytics processing.
- `meta` (Dictionary<string, string>): Optional. Additional key-value pairs describing the event.
- `location_data` (Vector3): Optional. The (x, y, z) coordinates of the event in 3D space.

Logs a named event with optional metadata and spatial context. Timestamps and origin (`user` or `system`) are automatically appended.

### Analytics Event Wrappers (Essential for All Developers)

**These analytics event functions are essential for ALL developers, not just those integrating with LMS platforms.** They provide standardized tracking for key user interactions and learning outcomes that are crucial for understanding user behavior, measuring engagement, and optimizing XR experiences.

**EventAssessmentStart and EventAssessmentComplete should be considered REQUIRED for proper usage** of the ABXR SDK, as they provide critical insights into user performance and completion rates.

The Analytics Event Functions are specialized versions of the Event method, tailored for common scenarios in XR experiences. These functions help enforce consistency in event logging across different parts of the application and provide valuable data for analytics, user experience optimization, and business intelligence. While they also power integrations with Learning Management System (LMS) platforms, their benefits extend far beyond educational use cases.

#### Assessments
Assessments are intended to track the overall performance of a learner across multiple Objectives and Interactions. 
* Think of it as the learner's score for a specific course or curriculum.
* When the Assessment is complete, it will automatically record and close out the Assessment in the various LMS platforms we support.

```cpp
//C# List Definition
public enum EventStatus
{
    Pass,
    Fail,
    Complete,
    Incomplete,
    Browsed
}

//C# Event Method Signatures
public void Abxr.EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null)

public void Abxr.EventAssessmentComplete(string assessmentName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null)

// Example Usage
Abxr.EventAssessmentStart("final_exam");
Abxr.EventAssessmentComplete("final_exam", 92, EventStatus.Pass);
```

#### Objectives
```cpp
//C# Event Method Signatures
public void Abxr.EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null)

public void Abxr.EventObjectiveComplete(string objectiveName, int score, EventStatus status = EventStatus.Complete, Dictionary<string, string> meta = null)

// Example Usage
Abxr.EventObjectiveStart("open_valve");
Abxr.EventObjectiveComplete("open_valve", 100, EventStatus.Complete);
```

#### Interactions
```cpp
//C# List Definition
public enum InteractionType
{
   Null, 
   Bool, // 1 or 0
   Select, // true or false and the result_details value should be a single letter or for multiple choice a,b,c
   Text, // a string 
   Rating, // a single digit value
   Number, // integer
   Matching,
   Performance,
   Sequencing
}

//C# Event Method Signatures
public void Abxr.EventInteractionStart(string interactionName, Dictionary<string, string> meta = null)

public void Abxr.EventInteractionComplete(string interactionName, InteractionType interactionType, string response = "", Dictionary<string, string> meta = null)

// Example Usage
Abxr.EventInteractionStart("select_option_a");
Abxr.EventInteractionComplete("select_option_a", InteractionType.Select, "true");
```

### Other Event Wrappers
#### Levels
```cpp
//C# Event Method Signatures
public void Abxr.EventLevelStart(string assessmentName) 

public void Abxr.EventLevelComplete(string levelName, int score)
public void Abxr.EventLevelComplete(string levelName, int score, Dictionary<string, string> meta = null)

// Example Usage
Abxr.EventLevelStart("level_1");
Abxr.EventLevelComplete("level_1", 85);

// For flagging critical training events (e.g., skipped safety checks, high-risk errors) for auto-inclusion in the Critical Choices Chart
public void Abxr.EventCritical(string label)
public void Abxr.EventCritical(string label, Dictionary<string, string> meta = null)
```

**Parameters for all Event Wrapper Functions:**
- `levelName/assessmentName/objectiveName/interactionName` (string): The identifier for the assessment, objective, interaction, or level.
- `score` (int): The numerical score achieved. While typically between 1-100, any integer is valid. In metadata, you can also set a minScore and maxScore to define the range of scores for this objective.
- `result` (Interactions): The result for the interaction is based on the InteractionType.
- `result_details` (string): Optional. Additional details about the result. For interactions, this can be a single character or a string. For example: "a", "b", "c" or "correct", "incorrect".
- `type` (InteractionType): Optional. The type of interaction for this event.
- `meta` (Dictionary<string, string>): Optional. Additional key-value pairs describing the event.

**Note:** All complete events automatically calculate duration if a corresponding start event was logged.

### Timed Events

The ABXR SDK includes a built-in timing system that allows you to measure the duration of any event. This is useful for tracking how long users spend on specific activities.

```cpp
//C# Timed Event Method Signature
public static void Abxr.StartTimedEvent(string eventName)

// Example Usage
Abxr.StartTimedEvent("Table puzzle");
// ... user performs puzzle activity for 20 seconds ...
Abxr.Event("Table puzzle"); // Duration automatically included: 20 seconds

// Works with all event methods
Abxr.StartTimedEvent("Assessment");
// ... later ...
Abxr.EventAssessmentComplete("Assessment", 95, EventStatus.Pass); // Duration included

// Also works with Mixpanel compatibility methods
Abxr.StartTimedEvent("User Session");
// ... later ...
Abxr.Track("User Session"); // Duration automatically included
```

**Parameters:**
- `eventName` (string): The name of the event to start timing. Must match the event name used later.

**Note:** The timer automatically adds a `duration` field (in seconds) to any subsequent event with the same name. The timer is automatically removed after the first matching event.



### Logging
The Log Methods provide straightforward logging functionality, similar to syslogs. These functions are available to developers by default, even across enterprise users, allowing for consistent and accessible logging across different deployment scenarios.

```cpp
//C# Event Method Signatures
public void Abxr.Log(LogLevel level, string message)

// Example usage
Abxr.Log("Info", "Module started");
```

Use standard or severity-specific logging:
```cpp
//C# Event Method Signatures
public void Abxr.LogDebug(string message)
public void Abxr.LogInfo(string message)
public void Abxr.LogWarn(string message)
public void Abxr.LogError(string message)
public void Abxr.LogCritical(string message)

// Example usage
Abxr.LogError("Critical error in assessment phase");
```

---

### Storage
The Storage API enables developers to store and retrieve learner/player progress, facilitating the creation of long-form training content. When users log in using ArborXR's facility or the developer's in-app solution, these methods allow users to continue their progress on different headsets, ensuring a seamless learning experience across multiple sessions or devices.

#### Save Progress
```cpp
//C# Event Method Signatures
public void Abxr.StorageSetEntry(Dictionary<string, string> data, string name = "state", bool keep_latest = true, string origin = null, bool session_data = false)

// Example usage
Abxr.StorageSetEntry(new Dictionary<string, string>{{"progress", "75%"}});
```
**Parameters:**
- `data` (Dictionary<string, string>): The key-value pairs to store.
- `name` (string): Optional. The identifier for this storage entry. Default is "state".
- `keep_latest` (bool): Optional. If true, only the most recent entry is kept. If false, entries are appended. Default is true.
- `origin` (string): Optional. The source of the data (e.g., "system").
- `session_data` (bool): Optional. If true, the data is specific to the current session. Default is false.

#### Retrieve Data
```cpp
//C# Event Method Signatures
public Dictionary<string, string> Abxr.StorageGetEntry(string name = "state", string origin = null, string[] tags_any = null, string[] tags_all = null, bool user_only = false)

// Example usage
var state = Abxr.StorageGetEntry("state");
```
**Parameters:**
- `name` (string): Optional. The identifier of the storage entry to retrieve. Default is "state".
- `origin` (string): Optional. Filter entries by their origin ("system", "user", or "admin").
- `tags_any` (string[]): Optional. Retrieve entries matching any of these tags.
- `tags_all` (string[]): Optional. Retrieve entries matching all of these tags.
- `user_only` (bool): Optional. If true, retrieve data for the current user across all devices for this app. Default is false.

**Returns:** A dictionary containing the retrieved storage entry.

#### Remove Storage
```cpp
//C# Event Method Signatures
public void Abxr.StorageRemoveEntry(string name = "state")

// Example usage
Abxr.StorageRemoveEntry("state");
```
**Parameters:**
- `name` (string): Optional. The identifier of the storage entry to remove. Default is "state".

#### Remove Default Storage Entry
```cpp
//C# Event Method Signatures
public void Abxr.StorageRemoveDefaultEntry(StorageScope scope = StorageScope.user)

// Example usage
Abxr.StorageRemoveDefaultEntry(StorageScope.user);
Abxr.StorageRemoveDefaultEntry(); // Defaults to user scope
```
**Parameters:**
- `scope` (StorageScope): Optional. Remove from 'device' or 'user' storage. Default is 'user'.

**Note:** This is a convenience method that removes the default "state" entry. It's equivalent to calling `RemoveStorageEntry("state")` but allows you to specify the storage scope.

#### Get All Entries
```cpp
//C# Event Method Signatures
public Dictionary<string, string> Abxr.GetAllStorageEntries()

// Example usage
var allEntries = Abxr.GetAllStorageEntries();
```
**Returns:** A dictionary containing all storage entries for the current user/device.

#### Remove All Storage Entries for Scope
```cpp
//C# Event Method Signatures
public void Abxr.StorageRemoveMultipleEntries(StorageScope scope = StorageScope.user)

// Example usage
Abxr.StorageRemoveMultipleEntries(StorageScope.user); // Clear all user data
Abxr.StorageRemoveMultipleEntries(StorageScope.device); // Clear all device data
Abxr.StorageRemoveMultipleEntries(); // Defaults to user scope
```
**Parameters:**
- `scope` (StorageScope): Optional. Remove all from 'device' or 'user' storage. Default is 'user'.

**Note:** This is a bulk operation that clears all stored entries at once. Use with caution as this cannot be undone.

---

### Telemetry
The Telemetry Methods provide comprehensive tracking of the XR environment. By default, they capture headset and controller movements, but can be extended to track any custom objects in the virtual space. These functions also allow collection of system-level data such as frame rates or device temperatures. This versatile tracking enables developers to gain deep insights into user interactions and application performance, facilitating optimization and enhancing the overall XR experience.

#### Manual Telemetry Activation
```cpp
//C# Event Method Signatures
public static void Abxr.TrackAutoTelemetry()

// Example usage
Abxr.TrackAutoTelemetry(); // Start manual telemetry tracking
```

**Use Case:** If you select 'Disable Automatic Telemetry' in the AbxrLib configuration, you can manually start tracking system telemetry with this function call. This captures headset/controller movements, performance metrics, and environmental data on demand.

#### Custom Telemetry Logging
To log spatial or system telemetry:
```cpp
//C# Event Method Signatures
public void Abxr.Telemetry(string name, Dictionary<string, string> data)

// Example usage
Abxr.Telemetry("headset_position", new Dictionary<string, string> { {"x", "1.23"}, {"y", "4.56"}, {"z", "7.89"} });
```

**Parameters:**
- `name` (string): The type of telemetry data (e.g., "OS_Version", "Battery_Level", "RAM_Usage").
- `data` (Dictionary<string, string>): Key-value pairs of telemetry data.

---
### AI Integration
The Integration Methods offer developers access to additional services, enabling customized experiences for enterprise users. Currently, this includes access to GPT services through the AIProxy method, allowing for advanced AI-powered interactions within the XR environment. More integration services are planned for future releases, further expanding the capabilities available to developers for creating tailored enterprise solutions.

#### AIProxy
```cpp
//C# Event Method Signatures
public string Abxr.AIProxy(string prompt, string past_messages = "", string bot_id = "")

// Example usage
Abxr.AIProxy("Provide me a randomized greeting that includes common small talk and ends by asking some form of how can I help");
```

**Parameters:**
- `prompt` (string): The input prompt for the AI.
- `past_messages` (string): Optional. Previous conversation history for context.
- `bot_id` (string): Optional. An identifier for a specific pre-defined chatbot.

**Returns:** The AI-generated response as a string.

**Note:** AIProxy calls are processed immediately and bypass the cache system. However, they still respect the SendRetriesOnFailure and SendRetryInterval settings.

### Exit Polls
Deliver questionnaires to users to gather feedback.
```cpp
// C# List Definition
public enum PollType
{
    Thumbs,
    Rating,
    MultipleChoice
}

// C# Event Method Signature
public static void PollUser(string prompt, ExitPollHandler.PollType pollType, List<string> responses = null, Action<string> callback = null)

// Example usage
Abxr.PollUser("How would you rate this training experience?", PollType.Rating);
```
**Poll Types:**
- `Thumbs Up/Thumbs Down`
- `Rating (1-5)`
- `Multiple Choice (2-8 string options)`

### Metadata Formats

The ABXR SDK supports multiple flexible formats for the `meta` parameter in all event and log methods. All formats are automatically converted to `Dictionary<string, string>` for use with the Unity SDK:

#### 1. Dictionary<string, string> (Native)
```cpp
// Native C# format - most efficient
var meta = new Dictionary<string, string>
{
    ["action"] = "click",
    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
    ["userId"] = "12345",
    ["completed"] = "true"
};

Abxr.Event("user_action", meta);
Abxr.LogInfo("User login", new Dictionary<string, string> 
{
    ["username"] = "john_doe",
    ["loginMethod"] = "oauth",
    ["deviceType"] = "quest3"
});
```

#### 2. Dictionary<string, object> (Mixpanel Compatibility)
```cpp
// Automatically converts objects to strings
var mixpanelStyle = new Dictionary<string, object>
{
    ["score"] = 95,           // Converted to "95"
    ["completed"] = true,     // Converted to "true"
    ["timestamp"] = DateTime.UtcNow,  // Converted to ISO string
    ["userData"] = new { name = "John", level = 5 }  // Converted to JSON
};

Abxr.Track("assessment_complete", mixpanelStyle);
```

#### 3. Abxr.Value Class (Full Mixpanel Compatibility)
```cpp
// Drop-in replacement for Mixpanel's Value class
var props = new Abxr.Value();
props["plan"] = "Premium";
props["amount"] = 29.99;
props["currency"] = "USD";

Abxr.Track("purchase_completed", props);
```

#### 4. Anonymous Objects (via Reflection)
```cpp
// Convenient for simple metadata (performance cost for reflection)
Abxr.Event("button_click", new { 
    buttonId = "submit_btn", 
    screenName = "checkout", 
    userTier = "premium" 
}.ToDictionary());

// Helper extension method for anonymous objects
public static Dictionary<string, string> ToDictionary(this object obj)
{
    return obj.GetType().GetProperties()
        .ToDictionary(p => p.Name, p => p.GetValue(obj)?.ToString() ?? "");
}
```

#### 5. No Metadata
```cpp
// Events and logs work fine without metadata
Abxr.Event("app_started");
Abxr.LogInfo("Application initialized");
```

#### 6. Vector3 Position Data (Unity Specific)
```cpp
// Unity-specific: Automatic position metadata
Vector3 playerPosition = transform.position;
Abxr.Event("player_teleported", playerPosition, new Dictionary<string, string>
{
    ["destination"] = "spawn_point",
    ["method"] = "instant"
});

// Automatically adds: position_x, position_y, position_z to metadata
```

#### Automatic Type Conversion Examples

The SDK automatically handles type conversion:

```cpp
var metadata = new Dictionary<string, object>
{
    ["score"] = 95,              // int → "95"
    ["passed"] = true,           // bool → "true"
    ["duration"] = 45.7f,        // float → "45.7"
    ["timestamp"] = DateTime.UtcNow,  // DateTime → ISO string
    ["player"] = new { name = "John", level = 5 }  // object → JSON string
};

Abxr.EventAssessmentComplete("math_test", 95, EventStatus.Pass, metadata);
```

#### Super Properties Integration

All metadata formats work seamlessly with Super Properties:

```cpp
// Set super properties once
Abxr.Register("user_type", "premium");
Abxr.Register("app_version", "1.2.3");

// All events automatically include super properties
Abxr.Event("button_click", new Dictionary<string, string> { ["button"] = "submit" });
// Final metadata: { "button": "submit", "user_type": "premium", "app_version": "1.2.3" }
```

#### JSON Array Handling

Unity has specific considerations when working with JSON arrays in metadata:

##### **Supported: Pre-serialized JSON Strings**
```cpp
// This works perfectly - JSON string is passed through as-is
var meta = new Dictionary<string, string>
{
    ["items"] = "[\"sword\", \"shield\", \"potion\"]",
    ["scores"] = "[95, 87, 92, 88]", 
    ["coordinates"] = "[{\"x\":1.5, \"y\":2.3}, {\"x\":4.1, \"y\":1.7}]"
};

Abxr.Event("inventory_updated", meta);
```

##### **Problem: Raw C# Arrays/Lists**
```cpp
// This does NOT work as expected - calls .ToString() on arrays
var meta = new Dictionary<string, object>
{
    ["items"] = new string[] {"sword", "shield", "potion"}, // Becomes "System.String[]"
    ["scores"] = new List<int> {95, 87, 92, 88}             // Becomes "System.Collections.Generic.List`1[System.Int32]"
};

Abxr.Track("inventory_updated", meta); // Results in useless string representations!
```

##### **Solutions: Manual JSON Serialization**
```cpp
// Option 1: Using JsonUtility (Unity built-in)
var items = new string[] {"sword", "shield", "potion"};
var scores = new int[] {95, 87, 92, 88};

// Simple wrapper class for arrays
[System.Serializable]
public class JsonArray<T> { public T[] array; }

var meta = new Dictionary<string, string>
{
    ["items"] = JsonUtility.ToJson(new JsonArray<string> { array = items }).Replace("{\"array\":", "").Replace("}", ""),
    ["scores"] = JsonUtility.ToJson(new JsonArray<int> { array = scores }).Replace("{\"array\":", "").Replace("}", "")
};

// Option 2: Using System.Text.Json (Unity 2021.2+)
using System.Text.Json;
var meta = new Dictionary<string, string>
{
    ["items"] = JsonSerializer.Serialize(items),
    ["scores"] = JsonSerializer.Serialize(scores)
};

// Option 3: Manual string building (simple arrays)
var meta = new Dictionary<string, string>
{
    ["items"] = "[\"" + string.Join("\", \"", items) + "\"]",
    ["scores"] = "[" + string.Join(", ", scores) + "]"
};

// Option 4: Helper Extension Method
public static class ArrayExtensions
{
    public static string ToJsonArray<T>(this T[] array)
    {
        if (typeof(T) == typeof(string))
            return "[\"" + string.Join("\", \"", array) + "\"]";
        else
            return "[" + string.Join(", ", array) + "]";
    }
}

// Usage with helper:
var meta = new Dictionary<string, string>
{
    ["items"] = items.ToJsonArray(),    // ["sword", "shield", "potion"]
    ["scores"] = scores.ToJsonArray()   // [95, 87, 92, 88]
};

Abxr.Event("inventory_updated", meta);
```

**Key Takeaway:** Always serialize arrays to JSON strings before passing to ABXR SDK methods.

**All event and log methods support these flexible metadata formats:**
- `Abxr.Event(name, meta?)`
- `Abxr.Event(name, position, meta?)` (Unity-specific)
- `Abxr.EventAssessmentStart/Complete(..., meta?)`
- `Abxr.EventObjectiveStart/Complete(..., meta?)`
- `Abxr.EventInteractionStart/Complete(..., meta?)`
- `Abxr.EventLevelStart/Complete(..., meta?)`
- `Abxr.LogDebug/Info/Warn/Error/Critical(message, meta?)`
- `Abxr.Track(eventName, properties?)` (Mixpanel compatibility)

---

## Advanced Features

### Module Targets

The **Module Target** feature enables developers to create single applications with multiple modules, where each module can be its own assignment in an LMS. When a learner enters from the LMS for a specific module, the application can automatically direct the user to that module within the application. Individual grades and results are then tracked for that specific assignment in the LMS.

#### Getting Module Target Information

You can also process module targets sequentially:

```cpp
// Get the next module target from the queue
CurrentSessionData nextTarget = Abxr.GetModuleTarget();
if (nextTarget != null)
{
    Debug.Log($"Processing module: {nextTarget.moduleTarget}");
    EnableModuleFeatures(nextTarget.moduleTarget);
    NavigateToModule(nextTarget.moduleTarget);
}
else
{
    Debug.Log("All modules completed!");
    ShowCompletionScreen();
}

// Check remaining module count
int remaining = Abxr.GetModuleTargetCount();
Debug.Log($"Modules remaining: {remaining}");

// Get current user information
var userId = Abxr.GetUserId();
var userData = Abxr.GetUserData();
string userEmail = Abxr.GetUserEmail();
```

#### Module Target Management

You can also manage the module target queue directly:

```cpp
// Clear all module targets and storage
Abxr.ClearModuleTargets();

// Check how many module targets remain
int count = Abxr.GetModuleTargetCount();
Debug.Log($"Modules remaining: {count}");
```

**Use Cases:**
- **Reset state**: Clear module targets when starting a new experience
- **Error recovery**: Clear corrupted module target data
- **Testing**: Reset module queue during development
- **Session management**: Clean up between different users

#### Best Practices

1. **Set up auth callback early**: Subscribe to `OnAuthCompleted` before authentication starts
2. **Handle first module**: Process the first module target from `authData.moduleTarget` 
3. **Use GetModuleTarget() sequentially**: Call after completing each module to get the next one
4. **Validate modules**: Check if requested module exists before navigation
5. **Progress tracking**: Use assessment events to track module completion
6. **Error handling**: Handle cases where navigation fails or module is invalid
7. **Check completion**: Use `GetModuleTarget()` returning null to detect when all modules are done

#### Example: Complete Multi-Module Setup

```cpp
public class MultiModuleManager : MonoBehaviour
{
    [System.Serializable]
    public class ModuleInfo
    {
        public string name;
        public string sceneName;
        public GameObject moduleObject;
    }

    [SerializeField] private ModuleInfo[] modules;
    
    void Start()
    {
        // Set up authentication completion callback
        Abxr.OnAuthCompleted(OnAuthCompleted);
    }
    
    private void OnAuthCompleted(Abxr.AuthCompletedData authData)
    {
        if (authData.success)
        {
            Debug.Log("Authentication completed successfully!");
            Debug.Log($"User ID: {authData.userId}");
            Debug.Log($"User Email: {authData.userEmail}");
            
            if (authData.isReauthentication)
            {
                Debug.Log("User reauthenticated - refreshing data");
                RefreshUserData();
            }
            else
            {
                Debug.Log("Initial authentication - full setup");
                InitializeUserInterface();
            }
            
            // Handle first module target from authentication
            if (!string.IsNullOrEmpty(authData.moduleTarget))
            {
                Debug.Log($"Starting with module: {authData.moduleTarget}");
                NavigateToModule(authData.moduleTarget);
            }
            else
            {
                Debug.Log("No initial module target - showing main menu");
                ShowModuleSelectionMenu();
            }
        }
        else
        {
            Debug.LogError("Authentication failed");
        }
    }
    
    // Call this when a module is completed to check for next module
    public void OnModuleCompleted(string completedModuleName)
    {
        Debug.Log($"Module '{completedModuleName}' completed!");
        
        // Complete the assessment for this module
        Abxr.EventAssessmentComplete(completedModuleName, 100, Abxr.EventStatus.Complete);
        
        // Check if there are more modules to process
        var nextModule = Abxr.GetModuleTarget();
        if (nextModule != null)
        {
            Debug.Log($"Next module available: {nextModule.moduleTarget}");
            NavigateToModule(nextModule.moduleTarget);
        }
        else
        {
            Debug.Log("All modules completed - showing completion screen");
            ShowCompletionScreen();
        }
    }
    
    private void NavigateToModule(string moduleTargetId)
    {
        var module = System.Array.Find(modules, m => m.name == moduleTargetId);
        
        if (module != null)
        {
            Debug.Log($"Navigating to module: {module.name}");
            LoadModule(module);
            
            // Start assessment tracking for this module
            Abxr.EventAssessmentStart(moduleTargetId, new Dictionary<string, string>
            {
                ["module_name"] = module.name,
                ["user_id"] = Abxr.GetUserId()?.ToString() ?? "unknown",
                ["user_email"] = Abxr.GetUserEmail() ?? "unknown"
            });
        }
        else
        {
            Debug.LogWarning($"Unknown module target: {moduleTargetId}");
            ShowModuleSelectionMenu();
        }
    }
    
    private void LoadModule(ModuleInfo module)
    {
        // Your module loading logic here
        if (!string.IsNullOrEmpty(module.sceneName))
        {
            SceneManager.LoadScene(module.sceneName);
        }
        else if (module.moduleObject != null)
        {
            module.moduleObject.SetActive(true);
        }
    }
    
    private void ShowModuleSelectionMenu()
    {
        // Show your module selection UI
    }
    
    private void ShowCompletionScreen()
    {
        // Show completion UI or return to main menu
    }
    
    private void RefreshUserData()
    {
        // Refresh user-specific data
    }
    
    private void InitializeUserInterface()
    {
        // Initialize UI components
    }
}
```

#### Data Structures

The module target callback provides a `CurrentSessionData` object with the following properties:

```cpp
public class CurrentSessionData
{
    public string moduleTarget;     // The target module identifier from LMS
    public object userData;         // Additional user data from authentication
    public object userId;           // User identifier
    public string userEmail;        // User email address
}
```

**Note:** The actual implementation of user data retrieval (`GetUserData`, `GetUserId`, `GetUserEmail`) has been completed and now properly integrates with the authentication system. Module targets are now handled through the sequential `GetModuleTarget()` method which processes targets from the authentication response.

### Authentication

The ABXR SDK provides comprehensive authentication completion callbacks that deliver detailed user and module information. This enables rich post-authentication workflows including automatic module navigation and personalized user experiences.

#### Authentication Completion Callback

Subscribe to authentication events to receive detailed information about the authenticated user and any module targets from LMS integration:

```cpp
// Subscribe to authentication completion events
Abxr.OnAuthCompleted((authData) =>
{
    Debug.Log($"Authentication completed: {authData.success}");
    Debug.Log($"User ID: {authData.userId}");
    Debug.Log($"User Email: {authData.userEmail}");
    Debug.Log($"Module Target: {authData.moduleTarget}");
    Debug.Log($"Is Reauthentication: {authData.isReauthentication}");
    
    if (authData.success)
    {
        // Authentication was successful
        if (authData.isReauthentication)
        {
            // User reauthenticated - maybe just refresh data
            Debug.Log("Welcome back!");
            RefreshUserData();
        }
        else
        {
            // Initial authentication - full setup
            Debug.Log("Welcome! Setting up your experience...");
            InitializeUserInterface();
            LoadUserPreferences();
        }
        
        // Check if we have a module target from auth
        if (!string.IsNullOrEmpty(authData.moduleTarget))
        {
            NavigateToModule(authData.moduleTarget);
        }
    }
});

// Multiple callbacks are supported
Abxr.OnAuthCompleted(HandleUserSetup);
Abxr.OnAuthCompleted(HandleAnalyticsInit);

// Remove specific callback when no longer needed
Abxr.RemoveAuthCompletedCallback(HandleUserSetup);

// Clear all callbacks
Abxr.ClearAuthCompletedCallbacks();
```

#### Callback Management

The authentication system supports multiple subscribers for flexible integration:

```cpp
// Store callback reference for later management
System.Action<Abxr.AuthCompletedData> authCallback = (authData) =>
{
    if (authData.success)
    {
        InitializeUserInterface();
        if (authData.moduleTarget != null)
        {
            NavigateToModule(authData.moduleTarget);
        }
    }
};

// Subscribe to authentication events
Abxr.OnAuthCompleted(authCallback);

// Remove callback when component is destroyed
void OnDestroy()
{
    Abxr.RemoveAuthCompletedCallback(authCallback);
}
```

#### Authentication Data Structure

The callback provides an `AuthCompletedData` object with comprehensive authentication information:

```cpp
public class AuthCompletedData
{
    public bool success;             // Whether authentication was successful
    public object userData;          // Additional user data from authentication response
    public object userId;            // User identifier
    public string userEmail;         // User email address
    public string moduleTarget;      // Target module from LMS (if applicable)
    public bool isReauthentication;  // Whether this was a reauthentication (vs initial auth)
}
```

#### Use Cases

- **Post-authentication setup**: Initialize UI components and load user preferences
- **Module navigation**: Automatically direct users to specific LMS assignments 
- **Personalization**: Customize experience based on user data
- **Session management**: Handle reauthentication vs initial authentication differently
- **Error handling**: Respond appropriately to authentication failures

#### Connection Status Check

You can check if AbxrLib has an active connection to the server at any time:

```cpp
//C# Method Signature
public static bool Abxr.ConnectionActive()

// Example usage
if (Abxr.ConnectionActive())
{
    Debug.Log("AbxrLib is connected and ready to send data");
    // Proceed with data operations
    Abxr.Event("app_ready");
}
else
{
    Debug.Log("Connection not active - waiting for authentication");
    // Set up authentication callbacks
    Abxr.OnAuthCompleted((authData) => {
        if (authData.success) {
            Debug.Log("Connection established successfully!");
        }
    });
}

// Conditional feature access
if (Abxr.ConnectionActive())
{
    ShowConnectedFeatures();
    SendTelemetryData();
}
else
{
    UseOfflineMode();
}
```

**Returns:** Boolean indicating if the library can communicate with the server

**Use Cases:**
- **Conditional logic**: Only send events/logs when connection is active
- **UI state management**: Show online/offline status indicators
- **Error prevention**: Check connection before making API calls
- **Feature gating**: Enable/disable features that require server communication

### Headset Removal
To improve session fidelity and reduce user spoofing or unintended headset sharing, we will trigger a re-authentication prompt when the headset is taken off and then put back on mid-session. If the headset is put on by a new user this will trigger an event defined in Abxr.cs. This can be subscribed to if the developer would like to have logic corresponding to this event.
```cpp
public static Action onHeadsetPutOnNewSession;
```
If the developer would like to have logic to correspond to these events, that would be done by subscribing to these events.

### Session Management

The ABXR SDK provides comprehensive session management capabilities that allow you to control authentication state and session continuity. These methods are particularly useful for multi-user environments, testing scenarios, and creating seamless user experiences across devices and time.

#### StartNewSession
Start a new session with a fresh session identifier. This method generates a new session ID and performs fresh authentication, making it ideal for starting new training experiences or resetting user context.

```cpp
//C# Method Signature
public static void Abxr.StartNewSession()

// Example Usage
Abxr.StartNewSession();
```

**Use Cases:**
- Starting new training modules or courses
- Resetting user progress for a fresh start
- Creating separate sessions for different users on the same device
- Beginning new assessment attempts

#### ReAuthenticate
Trigger manual reauthentication with existing stored parameters. This method is primarily useful for testing authentication flows or recovering from authentication issues.

```cpp
//C# Method Signature
public static void Abxr.ReAuthenticate()

// Example Usage
Abxr.ReAuthenticate();
```

**Use Cases:**
- Testing authentication flows during development
- Recovering from authentication errors
- Refreshing expired credentials
- Debugging authentication issues

**Note:** All session management methods work asynchronously and will trigger the `onAuthCompleted` callback when authentication completes, allowing you to respond to success or failure states.

### Debug Window
The Debug Window is a little bonus feature from the AbxrLib developers.
To help with general debugging, this feature routes a copy of all AbxrLib messages (Logs, Events, etc) to a window within the VR space. This enables developers to view logs in VR without having to repeatedly take on and off your headset while debugging.

#### Setup
To use this feature, simply drag the `AbxrDebugWindow` Prefab from `AbxrLib for Unity/Resources/Prefabs`, to whatever object in the scene you want this window attached to (i.e. `Left Controller`).

### ArborXR Device Management

#### Abxr.GetDeviceId()
- Return Type: string
- Description: UUID assigned to device by ArborXR

#### Abxr.GetDeviceTitle()
- Return Type: string
- Description: Title given to device by admin through the ArborXR Web Portal

#### Abxr.GetDeviceSerial()
- Return Type: string
- Description: Serial assigned to device by OEM

#### Abxr.GetDeviceTags()
- Return Type: string array
- Description: Tags added to device by admin through the ArborXR Web Portal

#### Abxr.GetOrgId()
- Return Type: string
- Description: The UUID of the organization where the device is assigned. Organizations are created in the ArborXR Web Portal.

#### Abxr.GetOrgTitle()
- Return Type: string
- Description: Name assigned to organization by admin through the ArborXR Web Portal

#### Abxr.GetOrgSlug()
- Return Type: string
- Description: Identifier generated by ArborXR when admin assigns title to organization

#### Abxr.GetMacAddressFixed()
- Return Type: string
- Description: physical MAC address assigned to device by OEM

#### Abxr.GetMacAddressRandom()
- Return Type: string
- Description: randomized MAC address for the current WiFi connection

#### Abxr.GetIsAuthenticated()
- Return Type: boolean
- Description: whether the device is SSO authenticated

#### Abxr.GetAccessToken()
- Return Type: string
- Description: the SSO access token

#### Abxr.GetRefreshToken()
- Return Type: string
- Description: the SSO refresh token

#### Abxr.GetExpiresDateUtc()
- Return Type: datetime
- Description: when the SSO access token expires in UTC time

#### Abxr.GetFingerprint()
- Return Type: string
- Description: the device fingerprint

### Mixpanel Compatibility

The ABXR SDK provides full compatibility with Mixpanel's Unity SDK, making migration simple and straightforward. You can replace your existing Mixpanel tracking calls with minimal code changes while gaining access to ABXR's advanced XR analytics capabilities.

#### Why Migrate from Mixpanel?

- **XR-Native Analytics**: Purpose-built for spatial computing and immersive experiences
- **Advanced Session Management**: Resume training across devices and sessions  
- **Enterprise Features**: LMS integrations, SCORM/xAPI support, and AI-powered insights
- **Spatial Tracking**: Built-in support for 3D position data and XR interactions
- **Open Source**: No vendor lock-in, deploy to any backend service

#### 3-Step Migration:

##### Step 1: Remove Mixpanel References
```cpp
// Remove or comment out these lines:
// using mixpanel;
// Any Mixpanel initialization code...

// ABXR SDK is already available - no additional using statements needed
```

##### Step 2: Configure ABXR SDK
Follow the [Configuration](#configuration) section to set up your App ID, Org ID, and Auth Secret in the Unity Editor.

##### Step 3: Simple String Replace
```cpp
// Find and replace throughout your codebase:
// Mixpanel.Track  ->  Abxr.Track

// Before (Mixpanel)
Mixpanel.Track("Sent Message");
var props = new Value();
props["Plan"] = "Premium"; 
Mixpanel.Track("Plan Selected", props);

// After (just string replace!)
Abxr.Track("Sent Message");
var props = new Abxr.Value();  // Abxr.Value class included for compatibility
props["Plan"] = "Premium";
Abxr.Track("Plan Selected", props);
```

#### Super Properties

Super Properties are global event properties that are automatically included in all events. They persist across app sessions and are perfect for setting user attributes, application state, or any data you want included in every event.

```cpp
//C# Super Properties Method Signatures
public static void Abxr.Register(string key, string value)
public static void Abxr.RegisterOnce(string key, string value)
public static void Abxr.Unregister(string key)
public static void Abxr.Reset()
public static Dictionary<string, string> Abxr.GetSuperProperties()

// Example Usage
// Set user properties that will be included in all events
Abxr.Register("user_type", "premium");
Abxr.Register("app_version", "1.2.3");
Abxr.Register("device_type", "quest3");

// All subsequent events automatically include these properties
Abxr.Event("button_click"); // Includes user_type, app_version, device_type
Abxr.EventAssessmentStart("quiz"); // Also includes all super properties
Abxr.Track("purchase"); // Mixpanel compatibility method also gets super properties

// Set default values that won't overwrite existing super properties
Abxr.RegisterOnce("user_tier", "free"); // Only sets if not already set
Abxr.RegisterOnce("user_tier", "premium"); // Ignored - "free" remains

// Manage super properties
Abxr.Unregister("device_type"); // Remove specific super property
var props = Abxr.GetSuperProperties(); // Get all current super properties
Abxr.Reset(); // Remove all super properties (matches Mixpanel.Reset())
```

**Key Features:**
- **Automatic Inclusion**: Super properties are automatically added to every event
- **Persistent Storage**: Super properties persist across app launches using PlayerPrefs
- **No Overwriting**: Super properties don't overwrite event-specific properties with the same name
- **Universal**: Works with all event methods (Event, Track, EventAssessmentStart, etc.)

**Use Cases:**
- User attributes (subscription type, user level, demographics)
- Application state (app version, build number, feature flags)
- Device information (device type, OS version, screen size)
- Session context (session ID, experiment groups, A/B test variants)

#### Mixpanel Compatibility Methods

The ABXR SDK includes a complete `Abxr.Value` class, `Track`, `StartTimedEvent` and `Register` methods that match Mixpanel's API:

```cpp
//C# Compatibility Class
public class Abxr.Value : Dictionary<string, object>
{
    public Value() : base() { }
    public Value(IDictionary<string, object> dictionary) : base(dictionary) { }
    public Dictionary<string, string> ToDictionary()  // Converts to ABXR format
}

//C# Track Method Signatures  
public static void Abxr.StartTimedEvent(string eventName)
public static void Abxr.Track(string eventName)
public static void Abxr.Track(string eventName, Abxr.Value properties)
public static void Abxr.Track(string eventName, Dictionary<string, object> properties)

// Example Usage - Drop-in Replacement
Abxr.Track("user_signup");
Abxr.Track("purchase_completed", new Abxr.Value { ["amount"] = 29.99, ["currency"] = "USD" });

// Timed Events (matches Mixpanel exactly!)
Abxr.StartTimedEvent("Table puzzle");
// ... 20 seconds later ...
Abxr.Track("Table puzzle"); // Duration automatically added: 20 seconds

// Super Properties (global properties included in all events)
Abxr.Register("user_type", "premium"); // Same as Mixpanel.Register()
Abxr.RegisterOnce("device", "quest3");  // Same as Mixpanel.RegisterOnce()
// All events now include user_type and device automatically!
```

**Additional Core Features Beyond Mixpanel:**
ABXR also includes core [Super Properties](#super-properties) functionality (`Register`, `RegisterOnce`) that works identically to Mixpanel, plus advanced [Timed Events](#timed-events) that work universally across all event types.

#### Key Differences & Advantages

| Feature | Mixpanel | ABXR SDK |
|---------|----------|-----------|
| **Basic Event Tracking** | ✅ | ✅ |
| **Custom Properties** | ✅ | ✅ |
| **Super Properties** | ✅ | ✅ (Register/RegisterOnce available) |
| **Timed Events** | ✅ | ✅ (StartTimedEvent available) |
| **3D Spatial Data** | ❌ | ✅ (Built-in Vector3 support) |
| **XR-Specific Events** | ❌ | ✅ (Assessments, Interactions, Objectives) |
| **Session Persistence** | Limited | ✅ (Cross-device, resumable sessions) |
| **Enterprise LMS Integration** | ❌ | ✅ (SCORM, xAPI, major LMS platforms) |
| **Real-time Collaboration** | ❌ | ✅ (Multi-user session tracking) |
| **Open Source** | ❌ | ✅ |

#### Migration Summary

**Migration Time: ~10 minutes for most projects**

1. **Install ABXR SDK** - Follow [Installation](#installation) guide
2. **Configure credentials** - Set App ID, Org ID, Auth Secret in Unity Editor  
3. **String replace** - `Mixpanel.Track` → `Abxr.Track` throughout your code
4. **Remove Mixpanel** - Comment out `using mixpanel;` and config code
5. **Done!** - All your existing tracking calls now work with ABXR

**Optional:** Add XR-specific features like spatial tracking and LMS assessments:
```cpp
// Enhanced XR tracking beyond Mixpanel capabilities  
Abxr.Event("object_grabbed", transform.position);  // Include 3D position
Abxr.EventAssessmentStart("safety_training");       // LMS-compatible assessments
```

#### Value Class Compatibility

The included `Abxr.Value` class is fully compatible with Mixpanel's implementation:

```cpp
var mixpanelStyleProps = new Abxr.Value();
mixpanelStyleProps["user_id"] = "12345";
mixpanelStyleProps["plan_type"] = "premium";
mixpanelStyleProps["trial_days"] = 30;

// Works exactly the same as Mixpanel
Abxr.Track("subscription_started", mixpanelStyleProps);
```

Properties are automatically converted to the appropriate format for ABXR's backend while maintaining full compatibility with your existing Mixpanel integration patterns.

## Support

## Resources

- **Docs:** [https://help.arborxr.com/](https://help.arborxr.com/)
- **GitHub:** [https://github.com/ArborXR/abxrlib-for-unity](https://github.com/ArborXR/abxrlib-for-unity)

## FAQ

### How do I retrieve my Application ID and Authorization Secret?
Your Application ID can be found in the Web Dashboard under the application details (you must be sure to use the App ID from the specific application you need data sent through). For the Authorization Secret, navigate to Settings > Organization Codes on the same dashboard.

### How do I enable object tracking?
Object tracking can be enabled by adding the Track Object component to any GameObject in your scene via the Unity Inspector.
