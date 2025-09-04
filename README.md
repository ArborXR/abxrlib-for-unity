# ABXRLib SDK for Unity

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
   - [Cognitive3D Compatibility](#cognitive3d-compatibility)
8. [Support](#support)
   - [Resources](#resources)
   - [FAQ](#faq)

---

## Introduction

### Overview

The **ABXRLib SDK for Unity** is an open-source analytics and data collection library that provides developers with the tools to collect and send XR data to any service of their choice. This library enables scalable event tracking, telemetry, and session-based storage—essential for enterprise and education XR environments.

> **Quick Start:** Most developers can integrate ABXRLib SDK and log their first event in under **15 minutes**.

**Why Use ABXRLib SDK?**

- **Open-Source** & portable to any backend—no vendor lock-in  
- **Quick integration**—track user interactions in minutes  
- **Secure & scalable**—ready for enterprise use cases  
- **Pluggable with ArborXR Insights**—seamless access to LMS/BI integrations, session replays, AI diagnostics, and more

### Core Features

The ABXRLib SDK provides:
- **Event Tracking:** Monitor user behaviors, interactions, and system events.
- **Spatial & Hardware Telemetry:** Capture headset/controller movement and hardware metrics.
- **Object & System Info:** Track XR objects and environmental state.
- **Storage & Session Management:** Support resumable training and long-form experiences.
- **Logs:** Developer and system-level logs available across sessions.

### Backend Services

The ABXRLib SDK is designed to work with any backend service that implements the ABXR protocol. Currently supported services include:

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

To use the ABXRLib SDK with ArborXR Insights:

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

// Example Usage - Event with Metadata (using Abxr.Dict - no using statements required!)
Abxr.Event("item_collected", new Abxr.Dict {
    {"item_type", "coin"},
    {"item_value", "100"}
});

// Example Usage - Event with Metadata and Location
Abxr.Event("player_teleported", 
    new Abxr.Dict {{"destination", "spawn_point"}},
    new Vector3(1.5f, 0.0f, -3.2f)
);

// Alternative: Traditional Dictionary (requires using System.Collections.Generic;)
Abxr.Event("item_collected", new Dictionary<string, string> {
    {"item_type", "coin"},
    {"item_value", "100"}
});
```

**Parameters:**
- `name` (string): The name of the event. Use snake_case for better analytics processing.
- `meta` (Dictionary<string, string> or Abxr.Dict): Optional. Additional key-value pairs describing the event. Use `Abxr.Dict` to avoid requiring using statements.
- `location_data` (Vector3): Optional. The (x, y, z) coordinates of the event in 3D space.

Logs a named event with optional metadata and spatial context. Timestamps and origin (`user` or `system`) are automatically appended.

### Analytics Event Wrappers (Essential for All Developers)

**These analytics event functions are essential for ALL developers** They provide standardized tracking for key user interactions and learning outcomes that are crucial for understanding user behavior, measuring engagement, and optimizing XR experiences and power the analytics dashboards and reporting features. They also essential for integrations with Learning Management System (LMS) platforms.

**EventAssessmentStart** and **EventAssessmentComplete** are **REQUIRED** for all ArborXR Insights usage

#### Assessments, Objectives & Interactions

These three event types work together to provide comprehensive tracking of user progress:

- **Assessment**: Tracks overall performance across an entire experience, course, or curriculum. Think of it as the final score or outcome for a complete learning module. When an Assessment completes, it automatically records and closes out the session in supported LMS platforms.

- **Objective**: Tracks specific learning goals or sub-tasks within an assessment. These represent individual skills, concepts, or milestones that contribute to the overall assessment score.

- **Interaction**: Tracks individual user responses or actions within an objective or assessment. These capture specific user inputs, choices, or behaviors that demonstrate engagement and learning progress.

```cpp
// Status enumeration for all analytics events
public enum EventStatus { Pass, Fail, Complete, Incomplete, Browsed }
public enum InteractionType { Null, Bool, Select, Text, Rating, Number, Matching, Performance, Sequencing }

//C# Method Signatures
public static void Abxr.EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null);
public static void Abxr.EventAssessmentComplete(string assessmentName, int score, EventStatus status, Dictionary<string, string> meta = null);
public static void Abxr.EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null);
public static void Abxr.EventObjectiveComplete(string objectiveName, int score, EventStatus status, Dictionary<string, string> meta = null);
public static void Abxr.EventInteractionStart(string interactionName, Dictionary<string, string> meta = null);
public static void Abxr.EventInteractionComplete(string interactionName, InteractionType type, string result, Dictionary<string, string> meta = null);

// Assessment tracking (overall course/curriculum performance)
Abxr.EventAssessmentStart("final_exam");
Abxr.EventAssessmentComplete("final_exam", 92, EventStatus.Pass);

// Objective tracking (specific learning goals)
Abxr.EventObjectiveStart("open_valve");
Abxr.EventObjectiveComplete("open_valve", 100, EventStatus.Complete);

// Interaction tracking (individual user responses)
Abxr.EventInteractionStart("select_option_a");
Abxr.EventInteractionComplete("select_option_a", InteractionType.Select, "true");
```

#### Additional Event Wrappers
```cpp
//C# Method Signatures
public static void Abxr.EventLevelStart(string levelName, Dictionary<string, string> meta = null);
public static void Abxr.EventLevelComplete(string levelName, int score, Dictionary<string, string> meta = null);
public static void Abxr.EventCritical(string eventName, Dictionary<string, string> meta = null);

// Level tracking 
Abxr.EventLevelStart("level_1");
Abxr.EventLevelComplete("level_1", 85);

// Critical event flagging (for safety training, high-risk errors, etc.)
Abxr.EventCritical("safety_violation");
```

**Parameters for all Event Wrapper Functions:**
- `levelName/assessmentName/objectiveName/interactionName` (string): The identifier for the assessment, objective, interaction, or level.
- `score` (int): The numerical score achieved. While typically between 1-100, any integer is valid. In metadata, you can also set a minScore and maxScore to define the range of scores for this objective.
- `result` (Interactions): The result for the interaction is based on the InteractionType.
- `result_details` (string): Optional. Additional details about the result. For interactions, this can be a single character or a string. For example: "a", "b", "c" or "correct", "incorrect".
- `type` (InteractionType): Optional. The type of interaction for this event.
- `meta` (Dictionary<string, string> or Abxr.Dict): Optional. Additional key-value pairs describing the event. Use `Abxr.Dict` to avoid requiring using statements.

**Note:** All complete events automatically calculate duration if a corresponding start event was logged.

### Timed Events

The ABXRLib SDK includes a built-in timing system that allows you to measure the duration of any event. This is useful for tracking how long users spend on specific activities.

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

#### Super Properties

Global properties automatically included in all events:

```cpp
//C# Method Signatures
public static void Abxr.Register(string key, string value);
public static void Abxr.RegisterOnce(string key, string value);

// Set persistent properties (included in all events)
Abxr.Register("user_type", "premium");
Abxr.Register("app_version", "1.2.3");

// Set only if not already set
Abxr.RegisterOnce("user_tier", "free");

// Management
Abxr.Unregister("device_type");  // Remove specific property
Abxr.Reset();                    // Clear all super properties
```

Perfect for user attributes, app state, and device information that should be included with every event.

### Logging
The Log Methods provide straightforward logging functionality, similar to syslogs. These functions are available to developers by default, even across enterprise users, allowing for consistent and accessible logging across different deployment scenarios.

```cpp
//C# Event Method Signatures
public void Abxr.Log(string message, LogLevel level = LogLevel.Info)

// Example usage
Abxr.Log("Module started"); // Defaults to LogLevel.Info
Abxr.Log("Module started", LogLevel.Info);
Abxr.Log("Debug information", LogLevel.Debug);
```

Use standard or severity-specific logging:
```cpp
//C# Method Signatures
public static void Abxr.LogDebug(string text, Dictionary<string, string> meta = null)
public static void Abxr.LogInfo(string text, Dictionary<string, string> meta = null)
public static void Abxr.LogWarn(string text, Dictionary<string, string> meta = null)
public static void Abxr.LogError(string text, Dictionary<string, string> meta = null)
public static void Abxr.LogCritical(string text, Dictionary<string, string> meta = null)

// Example usage
Abxr.LogError("Critical error in assessment phase");

// With metadata (using Abxr.Dict - no using statements required!)
Abxr.LogDebug("User interaction", new Abxr.Dict {
    {"action", "button_click"},
    {"screen", "main_menu"}
});

// Alternative: Traditional Dictionary (requires using System.Collections.Generic;)
Abxr.LogDebug("User interaction", new Dictionary<string, string> {
    {"action", "button_click"},
    {"screen", "main_menu"}
});
```

---

### Storage
The Storage API enables developers to store and retrieve learner/player progress, facilitating the creation of long-form training content. When users log in using ArborXR's facility or the developer's in-app solution, these methods allow users to continue their progress on different headsets, ensuring a seamless learning experience across multiple sessions or devices.

```cpp
//C# Method Signatures
public static void Abxr.StorageSetEntry(string name, Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.KeepLatest);
public static void Abxr.StorageSetDefaultEntry(Dictionary<string, string> entry, StorageScope scope, StoragePolicy policy = StoragePolicy.KeepLatest);
public static IEnumerator Abxr.StorageGetEntry(string name, StorageScope scope, Action<string> callback);
public static IEnumerator Abxr.StorageGetDefaultEntry(StorageScope scope, Action<string> callback);
public static void Abxr.StorageRemoveEntry(string name, StorageScope scope);

// Save progress data (using Abxr.Dict - no using statements required!)
Abxr.StorageSetEntry("state", new Abxr.Dict{{"progress", "75%"}}, StorageScope.user);
Abxr.StorageSetDefaultEntry(new Abxr.Dict{{"progress", "75%"}}, StorageScope.user);

// Alternative: Traditional Dictionary (requires using System.Collections.Generic;)
Abxr.StorageSetEntry("state", new Dictionary<string, string>{{"progress", "75%"}}, StorageScope.user);
Abxr.StorageSetDefaultEntry(new Dictionary<string, string>{{"progress", "75%"}}, StorageScope.user);

// Retrieve progress data (requires coroutine)
StartCoroutine(Abxr.StorageGetEntry("state", StorageScope.user, result => {
    Debug.Log("Retrieved data: " + result);
}));

StartCoroutine(Abxr.StorageGetDefaultEntry(StorageScope.user, result => {
    Debug.Log("Retrieved default data: " + result);
}));

// Remove storage entries  
Abxr.StorageRemoveEntry("state", StorageScope.user);
Abxr.StorageRemoveDefaultEntry(StorageScope.user);
Abxr.StorageRemoveMultipleEntries(StorageScope.user); // Clear all entries (use with caution)
```

**Parameters:**
- `name` (string): The identifier for this storage entry.
- `entry` (Dictionary<string, string> or Abxr.Dict): The key-value pairs to store. Use `Abxr.Dict` to avoid requiring using statements.
- `scope` (StorageScope): Store/retrieve from 'device' or 'user' storage.
- `policy` (StoragePolicy): How data should be stored - 'keepLatest' or 'appendHistory' (defaults to 'keepLatest').
- `callback` (Action): Callback function for retrieval operations.

---

### Telemetry
The Telemetry Methods provide comprehensive tracking of the XR environment. By default, they capture headset and controller movements, but can be extended to track any custom objects in the virtual space. These functions also allow collection of system-level data such as frame rates or device temperatures. This versatile tracking enables developers to gain deep insights into user interactions and application performance, facilitating optimization and enhancing the overall XR experience.

```cpp
//C# Method Signatures
public static void Abxr.TelemetryEntry(string name, Dictionary<string, string> meta);

// Manual telemetry activation (when auto-telemetry is disabled)
Abxr.TrackAutoTelemetry();

// Custom telemetry logging (using Abxr.Dict - no using statements required!)
Abxr.TelemetryEntry("headset_position", new Abxr.Dict { 
    {"x", "1.23"}, {"y", "4.56"}, {"z", "7.89"} 
});

// Alternative: Traditional Dictionary (requires using System.Collections.Generic;)
Abxr.TelemetryEntry("headset_position", new Dictionary<string, string> { 
    {"x", "1.23"}, {"y", "4.56"}, {"z", "7.89"} 
});
```

**Parameters:**
- `name` (string): The type of telemetry data (e.g., "headset_position", "frame_rate", "battery_level").
- `meta` (Dictionary<string, string> or Abxr.Dict): Key-value pairs of telemetry measurements. Use `Abxr.Dict` to avoid requiring using statements.

---
### AI Integration

```cpp
// Access GPT services for AI-powered interactions (requires coroutine)
StartCoroutine(Abxr.AIProxy("How can I help you today?", "gpt-4", result => {
    Debug.Log("AI Response: " + result);
}));

// With previous messages for context
var pastMessages = new List<string> {"Hello", "Hi there! How can I help?"};
StartCoroutine(Abxr.AIProxy("What's the weather like?", pastMessages, "gpt-4", result => {
    Debug.Log("AI Response: " + result);
}));
```

**Parameters:**
- `prompt` (string): The input prompt for the AI.
- `llmProvider` (string): The LLM provider identifier.
- `pastMessages` (List<string>): Optional. Previous conversation history for context.
- `callback` (Action<string>): Callback function that receives the AI response.

**Note:** AIProxy calls are processed immediately and bypass the cache system.

### Exit Polls
Deliver questionnaires to users to gather feedback.
```cpp
// Poll type enumeration
public enum PollType { Thumbs, Rating, MultipleChoice }

//C# Method Signatures
public static void Abxr.PollUser(string question, PollType pollType);

// Poll types: Thumbs, Rating (1-5), MultipleChoice (2-8 options)
Abxr.PollUser("How would you rate this training experience?", PollType.Rating);
```

### Abxr.Dict - Easy Metadata Creation

**NEW:** The ABXRLib SDK now includes `Abxr.Dict` - a wrapper class that makes creating metadata dictionaries simple without requiring `using System.Collections.Generic;` statements:

```cpp
// Simple creation (no using statements needed!)
var meta = new Abxr.Dict
{
    ["level"] = "5",
    ["score"] = "1250"
};

// Fluent API for method chaining
var meta = new Abxr.Dict()
    .With("level", "5")
    .With("score", "1250")
    .With("completed", "true");

// Works with all AbxrLib methods
Abxr.Event("level_complete", new Abxr.Dict { ["time"] = "45.2" });
Abxr.LogInfo("Player action", new Abxr.Dict { ["action"] = "jump" });
Abxr.StorageSetEntry("progress", new Abxr.Dict { ["level"] = "3" }, StorageScope.user);
```

**Key Benefits:**
- ✅ **No using statements required** - Works immediately without imports
- ✅ **Backwards compatible** - Seamlessly integrates with existing Dictionary parameters  
- ✅ **Multiple usage patterns** - Collection initializer, fluent API, or traditional approaches
- ✅ **Automatic compatibility** - Inherits from Dictionary<string, string> for seamless integration

### Metadata Formats

The ABXRLib SDK supports multiple flexible metadata formats. All formats are automatically converted to `Dictionary<string, string>`:

```cpp
// 1. Abxr.Dict (Recommended - no using statements required!)
Abxr.Event("user_action", new Abxr.Dict
{
    ["action"] = "click",
    ["userId"] = "12345"
});

// 2. Abxr.Dict with fluent API
Abxr.Event("purchase_complete", new Abxr.Dict()
    .With("amount", "29.99")
    .With("currency", "USD")
    .With("plan", "Premium"));

// 3. Native C# Dictionary (requires using System.Collections.Generic;)
Abxr.Event("user_action", new Dictionary<string, string>
{
    ["action"] = "click",
    ["userId"] = "12345"
});

// 4. Mixpanel-style Dictionary (auto-converts objects)
Abxr.Track("assessment_complete", new Dictionary<string, object>
{
    ["score"] = 95,           // → "95"
    ["completed"] = true,     // → "true"
    ["timestamp"] = DateTime.UtcNow  // → ISO string
});

// 5. Abxr.Value class (Mixpanel compatibility)
var props = new Abxr.Value();
props["plan"] = "Premium";
props["amount"] = 29.99;
Abxr.Track("purchase_completed", props);

// 6. No metadata
Abxr.Event("app_started");

// 7. With Unity Vector3 position data
Abxr.Event("player_teleported", transform.position, 
    new Abxr.Dict { ["destination"] = "spawn_point" });
```

#### JSON Arrays in Metadata

** Use pre-serialized JSON strings:**
```cpp
// Using Abxr.Dict (recommended - no using statements required!)
var meta = new Abxr.Dict
{
    ["items"] = "[\"sword\", \"shield\", \"potion\"]",
    ["scores"] = "[95, 87, 92, 88]"
};

// Alternative: Traditional Dictionary (requires using System.Collections.Generic;)
var meta = new Dictionary<string, string>
{
    ["items"] = "[\"sword\", \"shield\", \"potion\"]",
    ["scores"] = "[95, 87, 92, 88]"
};
```

** Avoid raw arrays/lists:**
```cpp
// Don't do this - becomes "System.String[]"
["items"] = new string[] {"sword", "shield", "potion"}
```

**Quick serialization helper:**
```cpp
public static string ToJsonArray<T>(this T[] array) =>
    typeof(T) == typeof(string) 
        ? "[\"" + string.Join("\", \"", array) + "\"]"
        : "[" + string.Join(", ", array) + "]";
```

**Key Takeaways:** 
- **Use `Abxr.Dict` for simplicity** - No using statements required, clean syntax
- Always serialize arrays to JSON strings before passing to ABXRLib SDK methods
- All event and log methods support these flexible metadata formats

### Automatic Data Collection

The ABXRLib SDK automatically enhances your data with additional context and metadata without requiring explicit configuration:

#### Scene Name Auto-Addition
Every event, log entry, and telemetry data point automatically includes the current Unity scene name:
```cpp
Abxr.Event("button_pressed"); // Automatically includes {"sceneName": "MainMenu"}
Abxr.LogInfo("User logged in"); // Automatically includes {"sceneName": "LoginScene"}
```

#### Super Properties Auto-Merge
Super properties are automatically merged into **every** event's metadata. Event-specific properties take precedence when keys conflict:
```cpp
// Set super properties
Abxr.Register("app_version", "1.2.3");
Abxr.Register("user_type", "premium");

// Every event automatically includes super properties (using Abxr.Dict)
Abxr.Event("level_complete", new Abxr.Dict {
    {"level", "3"}, 
    {"user_type", "trial"}  // This overrides the super property
});
// Result includes: app_version=1.2.3, user_type=trial, level=3, sceneName=CurrentScene
```

#### Automatic Telemetry Triggering
Every call to `Abxr.Event()` automatically triggers system telemetry collection unless explicitly disabled:
```cpp
// These automatically send telemetry data
Abxr.Event("user_action");                    // sendTelemetry=true (default)
Abxr.Event("quiet_event", meta, false);       // sendTelemetry=false (manual override)

// Automatic telemetry includes:
// - TrackSystemInfo.SendAll() - performance metrics, hardware info
// - TrackInputDevices.SendLocationData() - headset/controller positions
```

#### Duration Auto-Calculation
When using timed events or event wrappers, duration is automatically calculated and included:
```cpp
// Manual timed events
Abxr.StartTimedEvent("puzzle_solving");
// ... 30 seconds later ...
Abxr.Event("puzzle_solving"); // Automatically includes {"duration": "30"}

// Event wrapper functions automatically handle duration
Abxr.EventAssessmentStart("final_exam");
// ... 45 seconds later ...
Abxr.EventAssessmentComplete("final_exam", 95, EventStatus.Pass); // Automatically includes duration

// Works for all start/complete pairs:
// - EventAssessmentStart/Complete
// - EventObjectiveStart/Complete  
// - EventInteractionStart/Complete
// - EventLevelStart/Complete

// Duration defaults to "0" if no corresponding start event was found
// Timer is automatically removed after the first matching event
```

---

## Advanced Features

### Module Targets

The **Module Target** feature enables developers to create single applications with multiple modules, where each module can be its own assignment in an LMS. When a learner enters from the LMS for a specific module, the application can automatically direct the user to that module within the application. Individual grades and results are then tracked for that specific assignment in the LMS.

#### Getting Module Target Information

You can also process module targets sequentially:

```cpp
// Get the next module target from available modules
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

You can manage module progress and access rich module data:

```cpp
// Check remaining modules and preview current
int remaining = Abxr.GetModuleTargetCount();
ModuleData currentModule = Abxr.GetCurrentModule();
if (currentModule != null)
{
    Debug.Log($"Next: {currentModule.name} ({remaining} remaining)");
}

// Get all available modules
var allModules = Abxr.GetAvailableModules();
Debug.Log($"Total modules: {allModules.Count}");

// Reset progress or access learner data
Abxr.ClearModuleTargets();
var learnerData = Abxr.GetLearnerData();
```

**Use Cases:**
- **Reset state**: Reset module progress when starting a new experience
- **Error recovery**: Clear module progress and restart from beginning
- **Testing**: Reset module sequence during development
- **Session management**: Clean up between different users
- **Rich module data**: Access complete module information including names, IDs, and ordering

#### Persistence and Recovery

Module progress is automatically persisted across app sessions and device restarts:

```cpp
// Module data is automatically retrieved from authentication response
// Module progress is automatically saved when advancing through modules

// When app restarts or crashes, module progress is automatically restored
CurrentSessionData nextTarget = Abxr.GetModuleTarget(); // Loads progress from storage if needed
```

**Automatic Recovery Features:**
- **Session Persistence**: Module progress survives app crashes and restarts
- **Lazy Loading**: Progress is automatically loaded from storage when first accessed
- **Error Resilience**: Failed storage operations are logged but don't crash the application
- **Cross-Session Continuity**: Users can continue multi-module experiences across sessions
- **Rich Data Access**: Complete module information available from authentication response

**Storage Details:**
- Module progress is stored in user-scoped storage (not device-scoped)
- Storage key: `"AbxrModuleIndex"` (handled internally)
- Automatic cleanup when `ClearModuleTargets()` is called
- Uses ABXRLib's storage system for reliability and sync capabilities
- Module data comes directly from authentication response for accuracy

#### Best Practices

1. **Set up auth callback early**: Subscribe to `OnAuthCompleted` before authentication starts
2. **Handle first module**: Process the first module target from `authData.moduleTarget` 
3. **Use GetModuleTarget() sequentially**: Call after completing each module to get the next one
4. **Validate modules**: Check if requested module exists before navigation
5. **Progress tracking**: Use assessment events to track module completion
6. **Error handling**: Handle cases where navigation fails or module is invalid
7. **Check completion**: Use `GetModuleTarget()` returning null to detect when all modules are done

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

### Authentication

The ABXRLib SDK provides comprehensive authentication completion callbacks that deliver detailed user and module information. This enables rich post-authentication workflows including automatic module navigation and personalized user experiences.

#### Authentication Completion Callback

Subscribe to authentication events to receive user information and module targets:

```cpp
// Basic authentication callback
Abxr.OnAuthCompleted((authData) =>
{
    if (authData.success)
    {
        Debug.Log($"Welcome {authData.userEmail}!");
        
        // Handle initial vs reauthentication
        if (authData.isReauthentication)
            RefreshUserData();
        else
            InitializeUserInterface();
        
        // Navigate to module if specified
        if (!string.IsNullOrEmpty(authData.moduleTarget))
            NavigateToModule(authData.moduleTarget);
    }
});

// Callback management
Abxr.RemoveAuthCompletedCallback(authCallback);  // Remove specific callback
Abxr.ClearAuthCompletedCallbacks();              // Clear all callbacks
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
// Check app-level connection status  
if (Abxr.ConnectionActive())
{
    Debug.Log("AbxrLib is connected and ready to send data");
    Abxr.Event("app_ready");
}
else
{
    Debug.Log("Connection not active - waiting for authentication");
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

**Returns:** Boolean indicating if the library has an active connection and can communicate with the server (app-level authentication status)

**Use Cases:**
- **Conditional logic**: Only send events/logs when connection is active
- **UI state management**: Show online/offline status indicators  
- **Error prevention**: Check connection before making API calls
- **Feature gating**: Enable/disable features that require server communication

#### Accessing Learner Data

After authentication completes, you can access comprehensive learner data and preferences:

```cpp
// Get learner data and preferences
Dictionary<string, object> learnerData = Abxr.GetLearnerData();
if (learnerData != null)
{
    var userName = learnerData["name"]?.ToString();
    var audioPreference = learnerData["audioPreference"]?.ToString();
    
    Debug.Log($"Welcome back, {userName}!");
    SetAudioLevel(audioPreference);
}

// Check connection status before accessing data
if (Abxr.ConnectionActive())
{
    CustomizeExperience(Abxr.GetLearnerData());
}
```

**Returns:** Dictionary containing learner data from the authentication response, or null if not authenticated

**Available Data (when provided by authentication response):**
- **User Preferences**: `audioPreference`, `speedPreference`, `textPreference`
- **User Information**: `name`, `email`, `id`, `user_id`
- **Custom Fields**: Any additional data provided in the userData object

**Use Cases:**
- **Personalization**: Customize audio levels, playback speed, and text size based on user preferences
- **Accessibility**: Apply user-specific accessibility settings automatically
- **User Experience**: Greet users by name and show personalized content
- **Analytics**: Track usage patterns based on user preferences
- **Adaptive Content**: Adjust content difficulty or presentation based on user data

### Headset Removal
To improve session fidelity and reduce user spoofing or unintended headset sharing, we will trigger a re-authentication prompt when the headset is taken off and then put back on mid-session. If the headset is put on by a new user this will trigger an event defined in Abxr.cs. This can be subscribed to if the developer would like to have logic corresponding to this event.
```cpp
public static Action onHeadsetPutOnNewSession;
```
If the developer would like to have logic to correspond to these events, that would be done by subscribing to these events.

### Session Management

The ABXRLib SDK provides comprehensive session management capabilities that allow you to control authentication state and session continuity. These methods are particularly useful for multi-user environments, testing scenarios, and creating seamless user experiences across devices and time.

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

These methods provide access to device-level information and SSO authentication status on ArborXR-managed devices. These are convenience methods that operate at the device level, separate from the app-level authentication managed by the ABXRLib SDK.

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

The ABXRLib SDK provides full compatibility with Mixpanel's Unity SDK, making migration simple and straightforward. You can replace your existing Mixpanel tracking calls with minimal code changes while gaining access to ABXR's advanced XR analytics capabilities.

#### Why Migrate from Mixpanel?

- **XR-Native Analytics**: Purpose-built for spatial computing and immersive experiences
- **Advanced Session Management**: Resume training across devices and sessions  
- **Enterprise Features**: LMS integrations, SCORM/xAPI support, and AI-powered insights
- **Spatial Tracking**: Built-in support for 3D position data and XR interactions
- **Open Source**: No vendor lock-in, deploy to any backend service

**Migration Steps:**
1. Remove Mixpanel references (`using mixpanel;`)
2. Configure ABXRLib SDK credentials in Unity Editor
3. Replace `Mixpanel.Track` → `Abxr.Track` throughout codebase
4. Replace `new Value();` → `new Abxr.Value();` throughout codebase

```cpp
// Mixpanel → ABXR migration example
// Before: Mixpanel.Track("Plan Selected", props);
// After:  Abxr.Track("Plan Selected", props);
//
// Before; var props = new Value();
// After: var props = new Abxr.Value(); 
```

#### Drop-in Compatibility Methods

```cpp
//C# Method Signatures
public static void Abxr.Track(string eventName);
public static void Abxr.Track(string eventName, Abxr.Value properties);
public static void Abxr.Track(string eventName, Dictionary<string, object> properties);

// Abxr.Value class for Mixpanel compatibility
var props = new Abxr.Value();
props["amount"] = 29.99;
props["currency"] = "USD";

// Track methods (exactly like Mixpanel)
Abxr.Track("user_signup");
Abxr.Track("purchase_completed", props);

// Timed events
Abxr.StartTimedEvent("puzzle_solving");
// ... later ...
Abxr.Track("puzzle_solving"); // Duration automatically included
```

#### Key Advantages Over Mixpanel

| Feature | Mixpanel | ABXRLib SDK |
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

**Migration:** Simply replace `Mixpanel.Track` → `Abxr.Track` throughout your codebase.

### Cognitive3D Compatibility

The ABXRLib SDK provides full compatibility with Cognitive3D SDK, making migration simple and straightforward for event tracking. You can replace your existing Cognitive3D tracking calls with minimal code changes while gaining access to ABXR's advanced XR analytics capabilities and LMS integrations.

> **Note:** This compatibility guide covers event tracking only. Spatial analytics features of Cognitive3D are not covered as they have different architectures.

#### Why Migrate from Cognitive3D?

- **LMS Integration**: Native LMS platform support with SCORM/xAPI compatibility
- **Advanced Analytics**: Purpose-built dashboards for learning and training outcomes
- **Enterprise Features**: Session management, cross-device continuity, and AI-powered insights
- **Open Source**: No vendor lock-in, deploy to any backend service
- **Structured Events**: Rich event wrappers for assessments, objectives, and interactions

#### Migration Overview

| **Cognitive3D SDK**                          | **Equivalent in AbxrLib SDK**                                                |
| -------------------------------------------- | ---------------------------------------------------------------------------- |
| `new CustomEvent("event_name").Send()`       | `new Abxr.CustomEvent("event_name").Send()` or `Abxr.Event("event_name")`    |
| `Cognitive3D.StartEvent("assessment")`       | `Abxr.StartEvent("assessment")` or `Abxr.EventAssessmentStart("assessment")` |
| `Cognitive3D.EndEvent("assessment", result)` | `Abxr.EndEvent("assessment", result)` or `Abxr.EventAssessmentComplete(...)` |
| `Cognitive3D.SendEvent("event", props)`      | `Abxr.SendEvent("event", props)` or `Abxr.EventObjectiveComplete(...)`       |
| `Cognitive3D.SetSessionProperty(key, val)`   | `Abxr.SetSessionProperty(key, val)` or `Abxr.Register(key, val)`             |
| `Cognitive3D.Log("message")`                 | `Abxr.Log("message")` or `Abxr.LogInfo("message")`                           |

#### Migration Steps

**Step 1: Namespace Updates**
```cpp
// Option 1: Update using statements
// Before: using Cognitive3D;
// After:   (remove - use Abxr static methods)

// Option 2: String replacement approach
// Replace "Cognitive3D." with "Abxr." throughout your codebase for compatibility methods
```

**Step 2: Event Tracking Migration**

```cpp
///// CUSTOM EVENTS /////

// Before (Cognitive3D):
new Cognitive3D.CustomEvent("Pressed Space").Send();

// After (ABXRLib) - Direct replacement:
new Abxr.CustomEvent("Pressed Space").Send();

// After (ABXRLib) - Recommended approach:
Abxr.Event("Pressed Space");

///// START/END EVENTS (Assessment Tracking) /////

// Before (Cognitive3D):
Cognitive3D.StartEvent("final_exam");
Cognitive3D.EndEvent("final_exam", "pass", 95);

// After (ABXRLib) - Direct replacement:
Abxr.StartEvent("final_exam");
Abxr.EndEvent("final_exam", "pass", 95);

// After (ABXRLib) - Recommended approach:
Abxr.EventAssessmentStart("final_exam");
Abxr.EventAssessmentComplete("final_exam", 95, EventStatus.Pass);

///// SEND EVENT (Objective Tracking) /////

// Before (Cognitive3D):
Cognitive3D.SendEvent("valve_opened", new Dictionary<string, object> {
    {"result", "success"},
    {"score", 100}
});

// After (ABXRLib) - Direct replacement:
Abxr.SendEvent("valve_opened", new Dictionary<string, object> {
    {"result", "success"},
    {"score", 100}
});

// After (ABXRLib) - Recommended approach:
Abxr.EventObjectiveComplete("valve_opened", 100, EventStatus.Complete);

///// SESSION PROPERTIES /////

// Before (Cognitive3D):
Cognitive3D.SetSessionProperty("user_type", "technician");

// After (ABXRLib) - Direct replacement:
Abxr.SetSessionProperty("user_type", "technician");

// After (ABXRLib) - Recommended approach:
Abxr.Register("user_type", "technician");

///// LOGGING /////

// Before (Cognitive3D):
Cognitive3D.Log("Assessment started");

// After (ABXRLib):
Abxr.Log("Assessment started"); // Defaults to LogLevel.Info
// Or with specific levels:
Abxr.Log("Assessment started", LogLevel.Info);
Abxr.Log("Error occurred", LogLevel.Error);
```

#### Advanced Migration Features

**Custom Event Properties:**
```cpp
// Cognitive3D approach:
new Cognitive3D.CustomEvent("button_press")
    .SetProperty("button_id", "submit")
    .SetProperty("screen", "main_menu")
    .Send();

// ABXRLib equivalent:
new Abxr.CustomEvent("button_press")
    .SetProperty("button_id", "submit")
    .SetProperty("screen", "main_menu")
    .Send();

// ABXRLib recommended (using Abxr.Dict - no using statements required!):
Abxr.Event("button_press", new Abxr.Dict {
    {"button_id", "submit"},
    {"screen", "main_menu"}
});

// Alternative: Traditional Dictionary (requires using System.Collections.Generic;)
Abxr.Event("button_press", new Dictionary<string, string> {
    {"button_id", "submit"},
    {"screen", "main_menu"}
});
```

**Result Conversion Logic:**

The ABXRLib compatibility layer automatically converts common Cognitive3D result formats:

```cpp
// These Cognitive3D result values...
"pass", "success", "complete", "true", "1" → EventStatus.Pass
"fail", "error", "false", "0"              → EventStatus.Fail  
"incomplete"                               → EventStatus.Incomplete
"browse"                                   → EventStatus.Browsed
// All others                              → EventStatus.Complete (default)
```

#### Key Advantages Over Cognitive3D

| Feature | Cognitive3D | ABXRLib SDK |
|---------|-------------|-----------|
| **Basic Event Tracking** | ✅ | ✅ |
| **Custom Properties** | ✅ | ✅ |
| **Session Properties** | ✅ | ✅ (Enhanced with persistence) |
| **LMS Integration** | ❌ | ✅ (SCORM, xAPI, major platforms) |
| **Structured Learning Events** | ❌ | ✅ (Assessments, Objectives, Interactions) |
| **Cross-Device Sessions** | ❌ | ✅ (Resume training across devices) |
| **AI-Powered Insights** | ❌ | ✅ (Content optimization, learner analysis) |
| **Open Source** | ❌ | ✅ |

#### Migration Recommendations

**For Quick Migration:**
1. Use the direct compatibility methods (`Abxr.StartEvent`, `Abxr.EndEvent`, etc.)
2. Perform string replacement: `"Cognitive3D."` → `"Abxr."`
3. Test existing functionality

**For Enhanced Features:**
1. Replace `StartEvent`/`EndEvent` with `EventAssessmentStart`/`EventAssessmentComplete`
2. Replace `SendEvent` with `EventObjectiveComplete` where appropriate
3. Use structured `EventStatus` enum instead of string results
4. Add `InteractionType` tracking for detailed user behavior analysis

**Migration Path:**
```cpp
// Phase 1: Direct replacement (immediate compatibility)
Abxr.StartEvent("training_module");        // Works immediately
Abxr.EndEvent("training_module", "pass");  // Automatic conversion

// Phase 2: Enhanced features (recommended)  
Abxr.EventAssessmentStart("training_module");
Abxr.EventAssessmentComplete("training_module", 92, EventStatus.Pass);
```

## Support

## Resources

- **Docs:** [https://help.arborxr.com/](https://help.arborxr.com/)
- **GitHub:** [https://github.com/ArborXR/abxrlib-for-unity](https://github.com/ArborXR/abxrlib-for-unity)

## FAQ

### How do I retrieve my Application ID and Authorization Secret?
Your Application ID can be found in the Web Dashboard under the application details (you must be sure to use the App ID from the specific application you need data sent through). For the Authorization Secret, navigate to Settings > Organization Codes on the same dashboard.

### How do I enable object tracking?
Object tracking can be enabled by adding the Track Object component to any GameObject in your scene via the Unity Inspector.
