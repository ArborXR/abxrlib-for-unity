# ABXRLib SDK - Unified Documentation

The name "ABXR" stands for "Analytics Backbone for XR"—a flexible, open-source foundation for capturing and transmitting spatial, interaction, and performance data in XR. When combined with **ArborXR Insights**, ABXR transforms from a lightweight instrumentation layer into a full-scale enterprise analytics solution—unlocking powerful dashboards, LMS/BI integrations, and AI-enhanced insights.

## Table of Contents
1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Quick Start Guide](#quickstart)
5. [Sending Data](#sending-data)
   - [Events](#events)
   - [Analytics Event Wrappers](#analytics-event-wrappers-essential-for-all-developers)
   - [Timed Events](#timed-events)
   - [Super Meta Data](#super-meta-data)
   - [Logging](#logging)
   - [Storage](#storage)
   - [Telemetry](#telemetry)
   - [AI Integration](#ai-integration)
   - [Exit Polls](#exit-polls)
   - [Metadata Formats](#metadata-formats)
6. [Advanced Features](#advanced-features)
   - [Module Targets](#module-targets)
   - [Authentication](#authentication)
   - [Headset Removal](#headset-removal)
   - [Session Management](#session-management)
   - [Debug Window](#debug-window)
   - [ArborXR Device Management](#arborxr-device-management)
   - [Mixpanel Compatibility](#mixpanel-compatibility)
   - [Cognitive3D Compatibility](#cognitive3d-compatibility)
7. [Support](#support)
   - [Resources](#resources)
   - [FAQ](#faq)

---

## Introduction

### Overview

The **ABXRLib SDK** is an open-source analytics and data collection library that provides developers with the tools to collect and send XR data to any service of their choice. This library enables scalable event tracking, telemetry, and session-based storage—essential for enterprise and education XR environments.

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

<details open>
<summary><strong>Unity (C#)</strong></summary>

1. Open Unity and go to `Window > Package Manager`.
2. Select the '+' dropdown and choose **'Add package from git URL'**.
3. Use the GitHub repo URL:
   ```
   https://github.com/ArborXR/abxrlib-for-unity.git
   ```
4. Once imported, you will see `Analytics for XR` in your Unity toolbar.

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

1. Download the plugin from the GitHub repository.
2. Copy the plugin folder to your project's `Plugins` directory.
3. Open your Unreal project and go to `Edit > Plugins`.
4. Find `ABXRLib SDK` and enable it.
5. Once imported, you will see `ABXRLib SDK` configuration options in your Project Settings.

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

#### NPM Installation
```bash
npm install abxrlib-for-webxr
```

#### CDN Installation
```html
<script src="https://cdn.jsdelivr.net/npm/abxrlib-for-webxr@latest/dist/abxrlib-for-webxr.js"></script>
```

#### Manual Installation
1. Download the latest release from the GitHub repository.
2. Include the `abxrlib-for-webxr.js` file in your project.
3. Initialize the library in your application.

</details>

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

#### Configure Your Project

> **⚠️ Security Note:** For production builds distributed to third parties, avoid compiling `Org ID` and `Auth Secret` directly into your project. These credentials should only be compiled into builds when creating custom applications for specific individual clients. For general distribution, use ArborXR-managed devices or implement runtime credential provisioning.

<details open>
<summary><strong>Unity (C#)</strong></summary>

1. Open `Analytics for XR > Configuration` in the Unity Editor.
2. **For Development/Testing:** Paste in the App ID, Org ID, and Auth Secret. All 3 are required if you are testing from Unity itself.
3. **For Production Builds:** Only include the App ID. Leave Org ID and Auth Secret empty for third-party distribution.

#### Alternative for Managed Headsets:
If you're using an ArborXR-managed device, only the App ID is required. The Org ID and Auth Secret auto-fill. 
On any non-managed headset, you must manually enter all three values for testing purposes only.

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

1. Open `Edit > Project Settings > Plugins > ABXR Configuration` in the Unreal Editor.
2. **For Development/Testing:** Paste in the App ID, Org ID, and Auth Secret. All 3 are required if you are testing from Unreal itself.
3. **For Production Builds:** Only include the App ID. Leave Org ID and Auth Secret empty for third-party distribution.

#### Alternative for Managed Headsets:
If you're using an ArborXR-managed device, only the App ID is required. The Org ID and Auth Secret auto-fill. 
On any non-managed headset, you must manually enter all three values for testing purposes only.

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Initialize ABXRLib with your credentials
Abxr.init({
    appId: 'your-app-id',
    orgId: 'your-org-id',        // Optional for managed devices
    authSecret: 'your-auth-secret' // Optional for managed devices
});
```

#### Alternative for Managed Devices:
If you're using an ArborXR-managed device, only the App ID is required:
```javascript
Abxr.init({
    appId: 'your-app-id'
    // orgId and authSecret are automatically provided
});
```

</details>

### Using with Other Backend Services
For information on implementing your own backend service or using other compatible services, please refer to the ABXR protocol specification.

---

## Sending Data

### Events

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
//C# Event Method Signatures
public static void Abxr.Event(string name);
public static void Abxr.Event(string name, Dictionary<string, string> meta = null, bool sendTelemetry = true);
public static void Abxr.Event(string name, Vector3 position, Dictionary<string, string> meta = null);

// Example Usage - Basic Event
Abxr.Event("button_pressed");

// Example Usage - Event with Metadata
Abxr.Event("item_collected", new Abxr.Dict {
    {"item_type", "coin"},
    {"item_value", "100"}
});

// Example Usage - Event with Metadata and Location
Abxr.Event("player_teleported", 
    new Abxr.Dict {{"destination", "spawn_point"}},
    new Vector3(1.5f, 0.0f, -3.2f)
);
```

**Platform-Specific Notes:**
- Use `Abxr.Dict` to avoid requiring using statements
- `location_data` parameter is a Vector3

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
//C++ Event Method Signatures
public static void Event(const FString& Name);
public static void Event(const FString& Name, TMap<FString, FString>& Meta);

// Example Usage - Basic Event
UAbxr::Event(TEXT("button_pressed"));

// Example Usage - Event with Metadata
TMap<FString, FString> Meta;
Meta.Add(TEXT("item_type"), TEXT("coin"));
Meta.Add(TEXT("item_value"), TEXT("100"));
UAbxr::Event(TEXT("item_collected"), Meta);
```

**Platform-Specific Notes:**
- Use `TEXT()` macro for string literals
- Metadata uses `TMap<FString, FString>`

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Event Method Signatures
static async Event(name: string, meta?: any): Promise<number>
static async Event(name: string, position: Vector3, meta?: any): Promise<number>

// Example Usage - Basic Event
Abxr.Event('button_pressed');

// Example Usage - Event with Metadata
Abxr.Event('item_collected', {
    item_type: 'coin',
    item_value: '100'
});

// Example Usage - Event with Metadata and Location
Abxr.Event('player_teleported', 
    { destination: 'spawn_point' },
    { x: 1.5, y: 0.0, z: -3.2 }
);
```

**Platform-Specific Notes:**
- Use single quotes for strings
- `position` parameter is a Vector3 object
- Methods return Promise<number>

</details>

Logs a named event with optional metadata and spatial context. Timestamps and origin (`user` or `system`) are automatically appended.

**Parameters:**
- `name`: The name of the event. Use snake_case for better analytics processing.
- `meta`: Optional. Additional key-value pairs describing the event.
- `position/location_data`: Optional. The (x, y, z) coordinates of the event in 3D space.

### Analytics Event Wrappers (Essential for All Developers)

**These analytics event functions are essential for ALL developers** They provide standardized tracking for key user interactions and learning outcomes that are crucial for understanding user behavior, measuring engagement, and optimizing XR experiences and power the analytics dashboards and reporting features. They also essential for integrations with Learning Management System (LMS) platforms.

**EventAssessmentStart and EventAssessmentComplete are REQUIRED for all ArborXR Insights usage**

#### Assessments, Objectives & Interactions

These three event types work together to provide comprehensive tracking of user progress:

- **Assessment**: Tracks overall performance across an entire experience, course, or curriculum. Think of it as the final score or outcome for a complete learning module. When an Assessment completes, it automatically records and closes out the session in supported LMS platforms.

- **Objective**: Tracks specific learning goals or sub-tasks within an assessment. These represent individual skills, concepts, or milestones that contribute to the overall assessment score.

- **Interaction**: Tracks individual user responses or actions within an objective or assessment. These capture specific user inputs, choices, or behaviors that demonstrate engagement and learning progress.

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
// Status enumeration for all analytics events
public enum EventStatus { Pass, Fail, Complete, Incomplete, Browsed, NotAttempted }
public enum InteractionType { Null, Bool, Select, Text, Rating, Number, Matching, Performance, Sequencing }
public enum InteractionResult { Correct, Incorrect, Neutral } // defaults to Neutral

//C# Method Signatures
public static void Abxr.EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null);
public static void Abxr.EventAssessmentComplete(string assessmentName, int score, EventStatus status, Dictionary<string, string> meta = null);
public static void Abxr.EventObjectiveStart(string objectiveName, Dictionary<string, string> meta = null);
public static void Abxr.EventObjectiveComplete(string objectiveName, int score, EventStatus status, Dictionary<string, string> meta = null);
public static void Abxr.EventInteractionStart(string interactionName, Dictionary<string, string> meta = null);
public static void Abxr.EventInteractionComplete(string interactionName, InteractionType type, InteractionResult result, string response, Dictionary<string, string> meta = null);

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

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// Status enumeration for all analytics events
public enum EEventStatus { Pass, Fail, Complete, Incomplete, Browsed }
public enum EInteractionType { Null, Bool, Select, Text, Rating, Number, Matching, Performance, Sequencing }

//C++ Method Signatures
public static void UAbxr::EventAssessmentStart(const FString& AssessmentName);
public static void UAbxr::EventAssessmentStart(const FString& AssessmentName, TMap<FString, FString>& Meta);
public static void UAbxr::EventAssessmentComplete(const FString& AssessmentName, int32 Score, EEventStatus Status);
public static void UAbxr::EventAssessmentComplete(const FString& AssessmentName, int32 Score, EEventStatus Status, TMap<FString, FString>& Meta);
public static void UAbxr::EventObjectiveStart(const FString& ObjectiveName);
public static void UAbxr::EventObjectiveStart(const FString& ObjectiveName, TMap<FString, FString>& Meta);
public static void UAbxr::EventObjectiveComplete(const FString& ObjectiveName, int32 Score, EEventStatus Status);
public static void UAbxr::EventObjectiveComplete(const FString& ObjectiveName, int32 Score, EEventStatus Status, TMap<FString, FString>& Meta);
public static void UAbxr::EventInteractionStart(const FString& InteractionName);
public static void UAbxr::EventInteractionStart(const FString& InteractionName, TMap<FString, FString>& Meta);
public static void UAbxr::EventInteractionComplete(const FString& InteractionName, EInteractionType Type, const FString& Result);
public static void UAbxr::EventInteractionComplete(const FString& InteractionName, EInteractionType Type, const FString& Result, TMap<FString, FString>& Meta);

// Assessment tracking (overall course/curriculum performance)
UAbxr::EventAssessmentStart(TEXT("final_exam"));
UAbxr::EventAssessmentComplete(TEXT("final_exam"), 92, EEventStatus::Pass);

// Objective tracking (specific learning goals)
UAbxr::EventObjectiveStart(TEXT("open_valve"));
UAbxr::EventObjectiveComplete(TEXT("open_valve"), 100, EEventStatus::Complete);

// Interaction tracking (individual user responses)
UAbxr::EventInteractionStart(TEXT("select_option_a"));
UAbxr::EventInteractionComplete(TEXT("select_option_a"), EInteractionType::Select, TEXT("true"));
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Status enumeration for all analytics events
Abxr.EventStatus.Pass, Abxr.EventStatus.Fail, Abxr.EventStatus.Complete, Abxr.EventStatus.Incomplete, Abxr.EventStatus.Browsed, Abxr.EventStatus.NotAttempted
Abxr.InteractionType.Null, Abxr.InteractionType.Bool, Abxr.InteractionType.Select, Abxr.InteractionType.Text, Abxr.InteractionType.Rating, Abxr.InteractionType.Number
Abxr.InteractionResult.Correct, Abxr.InteractionResult.Incorrect, Abxr.InteractionResult.Neutral // defaults to Neutral

// JavaScript Method Signatures
static async EventAssessmentStart(assessmentName: string, meta?: any): Promise<number>
static async EventAssessmentComplete(assessmentName: string, score: number | string, eventStatus: EventStatus, meta?: any): Promise<number>
static async EventObjectiveStart(objectiveName: string, meta?: any): Promise<number>
static async EventObjectiveComplete(objectiveName: string, score: number | string, eventStatus: EventStatus, meta?: any): Promise<number>
static async EventInteractionStart(interactionName: string, meta?: any): Promise<number>
static async EventInteractionComplete(interactionName: string, interactionType: InteractionType, result: InteractionResult = "neutral", response: string = "", meta?: any): Promise<number>

// Assessment tracking (overall course/curriculum performance)
Abxr.EventAssessmentStart('final_exam');
Abxr.EventAssessmentComplete('final_exam', 92, Abxr.EventStatus.Pass);

// Objective tracking (specific learning goals)
Abxr.EventObjectiveStart('open_valve');
Abxr.EventObjectiveComplete('open_valve', 100, Abxr.EventStatus.Complete);

// Interaction tracking (individual user responses)
Abxr.EventInteractionStart('select_option_a');
Abxr.EventInteractionComplete('select_option_a', Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, 'true');
```

</details>

#### Additional Event Wrappers

<details open>
<summary><strong>Unity (C#)</strong></summary>

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

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
//C++ Method Signatures
public static void UAbxr::EventLevelStart(const FString& LevelName);
public static void UAbxr::EventLevelStart(const FString& LevelName, TMap<FString, FString>& Meta);
public static void UAbxr::EventLevelComplete(const FString& LevelName, int32 Score);
public static void UAbxr::EventLevelComplete(const FString& LevelName, int32 Score, TMap<FString, FString>& Meta);
public static void UAbxr::EventCritical(const FString& EventName);
public static void UAbxr::EventCritical(const FString& EventName, TMap<FString, FString>& Meta);

// Level tracking 
UAbxr::EventLevelStart(TEXT("level_1"));
UAbxr::EventLevelComplete(TEXT("level_1"), 85);

// Critical event flagging (for safety training, high-risk errors, etc.)
UAbxr::EventCritical(TEXT("safety_violation"));
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Method Signatures
static async EventLevelStart(levelName: string, meta?: any): Promise<number>
static async EventLevelComplete(levelName: string, score: number | string, meta?: any): Promise<number>
static async EventCritical(eventName: string, meta?: any): Promise<number>

// Level tracking 
Abxr.EventLevelStart('level_1');
Abxr.EventLevelComplete('level_1', 85);

// Critical event flagging (for safety training, high-risk errors, etc.)
Abxr.EventCritical('safety_violation');
```

</details>

**Parameters for all Event Wrapper Functions:**
- `levelName/assessmentName/objectiveName/interactionName` (string): The identifier for the assessment, objective, interaction, or level.
- `score` (int): The numerical score achieved. While typically between 1-100, any integer is valid. In metadata, you can also set a minScore and maxScore to define the range of scores for this objective.
- `result` (`Interactions`): The result for the interaction is based on the `InteractionType`.
- `result_details` (string): Optional. Additional details about the result. For interactions, this can be a single character or a string. For example: "a", "b", "c" or "correct", "incorrect".
- `type` (`InteractionType`): Optional. The type of interaction for this event.
- `meta` (`Dictionary<string, string>` or `Abxr.Dict`): Optional. Additional key-value pairs describing the event. Use `Abxr.Dict` to avoid requiring using statements.

**Note:** All complete events automatically calculate duration if a corresponding start event was logged.

### Timed Events

The ABXRLib SDK includes a built-in timing system that allows you to measure the duration of any event. This is useful for tracking how long users spend on specific activities.

<details open>
<summary><strong>Unity (C#)</strong></summary>

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

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// C++ Timed Event Method Signature
public static void UAbxr::StartTimedEvent(const FString& EventName);

// Example Usage
UAbxr::StartTimedEvent(TEXT("Table puzzle"));
// ... user performs puzzle activity for 20 seconds ...
UAbxr::Event(TEXT("Table puzzle")); // Duration automatically included: 20 seconds

// Works with all event methods
UAbxr::StartTimedEvent(TEXT("Assessment"));
// ... later ...
UAbxr::EventAssessmentComplete(TEXT("Assessment"), 95, EEventStatus::Pass); // Duration included

// Also works with Mixpanel compatibility methods
UAbxr::StartTimedEvent(TEXT("User Session"));
// ... later ...
UAbxr::Track(TEXT("User Session")); // Duration automatically included
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Timed Event Method Signature
static StartTimedEvent(eventName: string): void

// Example Usage
Abxr.StartTimedEvent("Table puzzle");
// ... user performs puzzle activity for 20 seconds ...
Abxr.Event("Table puzzle"); // Duration automatically included: 20 seconds

// Works with all event methods
Abxr.StartTimedEvent("Assessment");
// ... later ...
Abxr.EventAssessmentComplete("Assessment", 95, Abxr.EventStatus.Pass); // Duration included

// Also works with Mixpanel compatibility methods
Abxr.StartTimedEvent("User Session");
// ... later ...
Abxr.Track("User Session"); // Duration automatically included
```

</details>

**Parameters:**
- `eventName`: The name of the event to start timing. Must match the event name used later.

**Note:** The timer automatically adds a `duration` field (in seconds) to any subsequent event with the same name. The timer is automatically removed after the first matching event.

### Super Meta Data

Global metadata automatically included in all events, logs, and telemetry data.

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
//C# Method Signatures
public static void Abxr.Register(string key, string value);
public static void Abxr.RegisterOnce(string key, string value);

// Set persistent "super metadata" (included in all events, logs, and telemetry)
Abxr.Register("user_type", "premium");
Abxr.Register("app_version", "1.2.3");

// Set only if not already set
Abxr.RegisterOnce("user_tier", "free");

// Management
Abxr.Unregister("device_type");  // Remove specific super metadata 
Abxr.Reset();                    // Clear all super metadata
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
//C++ Method Signatures
public static void UAbxr::Register(const FString& Key, const FString& Value);
public static void UAbxr::RegisterOnce(const FString& Key, const FString& Value);

// Set persistent properties (included in all events)
UAbxr::Register(TEXT("user_type"), TEXT("premium"));
UAbxr::Register(TEXT("app_version"), TEXT("1.2.3"));

// Set only if not already set
UAbxr::RegisterOnce(TEXT("user_tier"), TEXT("free"));

// Management
UAbxr::Unregister(TEXT("device_type"));  // Remove specific property
UAbxr::Reset();                          // Clear all super properties
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Method Signatures
static Register(key: string, value: string): void
static RegisterOnce(key: string, value: string): void

// Set persistent "super metadata" (included in all events, logs, and telemetry)
Abxr.Register("user_type", "premium");
Abxr.Register("app_version", "1.2.3");

// Set only if not already set
Abxr.RegisterOnce("user_tier", "free");

// Management
Abxr.Unregister("device_type");  // Remove specific super metadata 
Abxr.Reset();                    // Clear all super metadata
```

</details>

Perfect for user attributes, app state, and device information that should be included with every event, log entry, and telemetry data point.

### Logging

The Log Methods provide straightforward logging functionality, similar to syslogs. These functions are available to developers by default, even across enterprise users, allowing for consistent and accessible logging across different deployment scenarios.

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
//C# Method Signatures
public static void Abxr.Log(string message, LogLevel level = LogLevel.Info, Dictionary<string, string> meta = null)

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

// With metadata
Abxr.LogDebug("User interaction", new Abxr.Dict {
    {"action", "button_click"},
    {"screen", "main_menu"}
});
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
//C++ Event Method Signatures
public static void UAbxr::Log(const FString& Message, ELogLevel Level = ELogLevel::Info)

// Example usage
UAbxr::Log(TEXT("Module started")); // Defaults to ELogLevel::Info
UAbxr::Log(TEXT("Module started"), ELogLevel::Info);
UAbxr::Log(TEXT("Debug information"), ELogLevel::Debug);
UAbxr::Log(TEXT("Error occurred"), ELogLevel::Error);
```

Use standard or severity-specific logging:
```cpp
//C++ Method Signatures
public static void LogDebug(const FString& Message)
public static void LogInfo(const FString& Message)
public static void LogWarn(const FString& Message)
public static void LogError(const FString& Message)
public static void LogCritical(const FString& Message)

// Example usage
UAbxr::LogError(TEXT("Critical error in assessment phase"));
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Method Signatures
static async Log(message: string, level: LogLevel = LogLevel.Info, meta?: any): Promise<number>

// Example usage
Abxr.Log("Module started"); // Defaults to LogLevel.Info
Abxr.Log("Module started", Abxr.LogLevel.Info);
Abxr.Log("Debug information", Abxr.LogLevel.Debug);
```

Use standard or severity-specific logging:
```javascript
// JavaScript Method Signatures
static async LogDebug(message: string, meta?: any): Promise<number>
static async LogInfo(message: string, meta?: any): Promise<number>
static async LogWarn(message: string, meta?: any): Promise<number>
static async LogError(message: string, meta?: any): Promise<number>
static async LogCritical(message: string, meta?: any): Promise<number>

// Example usage
Abxr.LogError("Critical error in assessment phase");

// With metadata
Abxr.LogDebug("User interaction", {
    action: "button_click",
    screen: "main_menu"
});
```

</details>

---

## Advanced Features

### Module Targets

The **Module Target** feature enables developers to create single applications with multiple modules, where users can be assigned to specific module(s) and the application can automatically direct users to their assigned module within the application. When combined with Insights LMS integration, each module can be its own assignment, and individual grades and results are tracked for that specific assignment.

> **Note:** Module Targets are supported in Unity and WebXR. Unreal Engine support is planned for future releases.

#### Event-Based Module Handling (Recommended)

The recommended way to handle modules is to subscribe your Module/Deep link handler to the `OnModuleTarget` event, which gives you full control over how to handle each module target. This event works perfectly with existing Android deep link handlers—you can use the same routing logic for both external deep links and LMS module targets. **The module sequence executes automatically when authentication completes, and will wait for your subscription if needed**, so you only need to subscribe to the event:

**Features:**
- **Smart Automatic Execution**: Module sequence executes automatically when authentication completes, and waits for your subscription if needed
- **Event-Driven**: Uses the `OnModuleTarget` event for maximum flexibility
- **Developer Control**: You decide how to handle each module target
- **Deep Link Integration**: Perfect for connecting to existing deep link handlers
- **Unified Handling**: One method handles both external deep links and LMS module targets
- **Error Handling**: Continues to next module if an event handler throws an exception
- **Return Count**: Returns the number of successfully executed modules

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
// Subscribe to module target events
Abxr.OnModuleTarget += HandleModuleOrDeepLinkTarget; // HandleModuleOrDeepLinkTarget points to your module/deep link handler

// Module(s) will be executed automatically when authentication completes!

// Create or use your own module/deep link handler along these lines
private void HandleModuleOrDeepLinkTarget(string moduleTarget)
{
    Debug.Log($"Handling module target: {moduleTarget}");
    
    // Your existing deep link routing logic works here too
    switch (moduleTarget)
    {
        case "safety-training":
            LoadScene("SafetyTrainingScene");
            break;
        case "equipment-check":
            LoadScene("EquipmentCheckScene");
            break;
        default:
            Debug.LogWarning($"Unknown module target: {moduleTarget}");
            LoadScene("MainMenuScene");
            break;
    }
}
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Subscribe to module target events
Abxr.OnModuleTarget = (moduleTarget) => {
    console.log(`Handling module target: ${moduleTarget}`);
    
    // Your existing deep link routing logic works here too
    switch (moduleTarget) {
        case "safety-training":
            loadScene("SafetyTrainingScene");
            break;
        case "equipment-check":
            loadScene("EquipmentCheckScene");
            break;
        default:
            console.warn(`Unknown module target: ${moduleTarget}`);
            loadScene("MainMenuScene");
            break;
    }
};

// Module(s) will be executed automatically when authentication completes!
```

</details>

#### Manual Module Processing

For more control, you can manually process modules:

<details open>
<summary><strong>Unity (C#)</strong></summary>

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

// Get all available modules
var allModules = Abxr.GetModuleTargetList();
Debug.Log($"Total modules: {allModules.Count}");

// Reset progress
Abxr.ClearModuleTargets();

// Get current user information
var userData = Abxr.GetUserData();
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Get the next module target from available modules
const nextTarget = Abxr.GetModuleTarget();
if (nextTarget) {
    console.log(`Processing module: ${nextTarget.moduleTarget}`);
    enableModuleFeatures(nextTarget.moduleTarget);
    navigateToModule(nextTarget.moduleTarget);
} else {
    console.log("All modules completed!");
    showCompletionScreen();
}

// Check remaining module count
const remaining = Abxr.GetModuleTargetCount();
console.log(`Modules remaining: ${remaining}`);

// Get all available modules
const allModules = Abxr.GetModuleTargetList();
console.log(`Total modules: ${allModules.length}`);

// Reset progress
Abxr.ClearModuleTargets();

// Get current user information
const userData = Abxr.GetUserData();
```

</details>

#### Persistence and Recovery

Module progress is automatically persisted across app sessions and device restarts:

- **Session Persistence**: Module progress survives app crashes and restarts
- **Lazy Loading**: Progress is automatically loaded from storage when first accessed
- **Error Resilience**: Failed storage operations are logged but don't crash the application
- **Cross-Session Continuity**: Users can continue multi-module experiences across sessions

#### Best Practices

1. **Use OnModuleTarget Event**: Subscribe to `OnModuleTarget` for flexible module handling
2. **Subscribe to OnAuthCompleted**: Subscribe before authentication starts
3. **Connect to Deep Links**: Use `OnModuleTarget` to connect to your existing deep link handling
4. **Error Handling**: Handle cases where modules don't exist or fail
5. **Progress Tracking**: Use assessment events to track module completion
6. **Unsubscribe on Destroy**: Always unsubscribe from events in `OnDestroy()` to prevent memory leaks

#### Data Structures

The module target callback provides a `CurrentSessionData` object with the following properties:

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
public class CurrentSessionData
{
    public string moduleTarget;     // The target module identifier from LMS
    public object userData;         // Additional user data from authentication
    public object userId;           // User identifier
    public string userEmail;        // User email address
}
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
interface CurrentSessionData {
    moduleTarget: string;     // The target module identifier from LMS
    userData: any;           // Additional user data from authentication
    userId: string;          // User identifier
    userEmail: string;       // User email address
}
```

</details>

### Authentication

The ABXRLib SDK provides comprehensive authentication completion callbacks that deliver detailed user and module information. This enables rich post-authentication workflows including automatic module navigation and personalized user experiences.

#### Authentication Completion Event

If you would like to have logic to correspond to authentication completion, you can subscribe to this event.

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
// 'true' for success and 'false' for failure (string argument will contain the error message on failure)
public static Action<bool, string> OnAuthCompleted;

// Example usage
Abxr.OnAuthCompleted += (success, errorMessage) => {
    if (success) {
        Debug.Log("Authentication successful!");
        // Initialize your app features here
        StartGameFlow();
    } else {
        Debug.LogError($"Authentication failed: {errorMessage}");
        // Handle authentication failure
        ShowLoginScreen();
    }
};
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// Subscribe to authentication events to receive user information and module targets
UAbxr::OnAuthCompleted.AddDynamic(this, &AMyActor::HandleAuthCompleted);

// In your actor's header file (.h):
UFUNCTION()
void HandleAuthCompleted(const FAuthCompletedData& AuthData);

// In your actor's implementation file (.cpp):
void AMyActor::HandleAuthCompleted(const FAuthCompletedData& AuthData)
{
    if (AuthData.bSuccess)
    {
        UE_LOG(LogTemp, Log, TEXT("Welcome %s!"), *AuthData.UserEmail);
        
        // Handle initial vs reauthentication
        if (AuthData.bIsReauthentication)
        {
            RefreshUserData();
        }
        else
        {
            InitializeUserInterface();
        }
        
        // Navigate to module if specified
        if (!AuthData.ModuleTarget.IsEmpty())
        {
            NavigateToModule(AuthData.ModuleTarget);
        }
    }
}
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Subscribe to authentication completion - the recommended approach
Abxr.OnAuthCompleted = (authData) => {
    if (authData.success) {
        console.log("Authentication successful - AbxrLib is ready!");
        console.log(`Welcome ${authData.userEmail}!`);
        
        // Initialize your app features here
        startGameFlow();
    } else {
        console.error(`Authentication failed: ${authData.error}. Working in offline mode`);

        // Initialize your app features here
        startGameFlow();
    }
};
```

</details>

#### Use Cases

- **Post-authentication setup**: Initialize UI components and load user preferences
- **Module navigation**: Automatically direct users to specific LMS assignments 
- **Personalization**: Customize experience based on user data
- **Session management**: Handle reauthentication vs initial authentication differently
- **Error handling**: Respond appropriately to authentication failures

#### Connection Status Check

You can check if AbxrLib has an active connection to the server at any time:

<details open>
<summary><strong>Unity (C#)</strong></summary>

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
    Abxr.OnAuthCompleted += (success, error) => {
        if (success) {
            Debug.Log("Connection established successfully!");
        }
    };
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

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// C++ Method Signature
bool UAbxr::ConnectionActive();

// Example usage
// Check app-level connection status  
if (UAbxr::ConnectionActive())
{
    UE_LOG(LogTemp, Log, TEXT("AbxrLib is connected and ready to send data"));
    UAbxr::Event(TEXT("app_ready"));
}
else
{
    UE_LOG(LogTemp, Log, TEXT("Connection not active - waiting for authentication"));
    UAbxr::OnAuthCompleted.AddDynamic(this, &AMyActor::HandleAuthCompleted);
}

// Conditional feature access
if (UAbxr::ConnectionActive())
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

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Method Signature
static ConnectionActive(): boolean

// Example usage
// Check app-level connection status  
if (Abxr.ConnectionActive()) {
    console.log("AbxrLib is connected and ready to send data");
    Abxr.Event("app_ready");
} else {
    console.log("Connection not active - waiting for authentication");
    Abxr.OnAuthCompleted = (authData) => {
        if (authData.success) {
            console.log("Connection established successfully!");
        }
    };
}

// Conditional feature access
if (Abxr.ConnectionActive()) {
    showConnectedFeatures();
    sendTelemetryData();
} else {
    useOfflineMode();
}
```

**Returns:** Boolean indicating if the library has an active connection and can communicate with the server (app-level authentication status)

</details>

### Headset Removal

To improve session fidelity and reduce user spoofing or unintended headset sharing, the SDK triggers a re-authentication prompt when the headset is taken off and then put back on mid-session. If the headset is put on by a new user, this will trigger an event that you can subscribe to:

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
public static Action OnHeadsetPutOnNewSession;

// Example usage
Abxr.OnHeadsetPutOnNewSession += () => {
    Debug.Log("New user detected - headset was put on by someone else");
    // Handle new user scenario
    ShowUserSwitchPrompt();
};
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// Subscribe to headset removal events
UAbxr::OnHeadsetPutOnNewSession.AddDynamic(this, &AMyActor::HandleNewUser);

// In your actor's header file (.h):
UFUNCTION()
void HandleNewUser();

// In your actor's implementation file (.cpp):
void AMyActor::HandleNewUser()
{
    UE_LOG(LogTemp, Log, TEXT("New user detected - headset was put on by someone else"));
    // Handle new user scenario
    ShowUserSwitchPrompt();
}
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Subscribe to headset removal events
Abxr.OnHeadsetPutOnNewSession = () => {
    console.log("New user detected - headset was put on by someone else");
    // Handle new user scenario
    showUserSwitchPrompt();
};
```

</details>

### Session Management

The ABXRLib SDK provides comprehensive session management capabilities that allow you to control authentication state and session continuity. These methods are particularly useful for multi-user environments, testing scenarios, and creating seamless user experiences across devices and time.

#### StartNewSession
Start a new session with a fresh session identifier. This method generates a new session ID and performs fresh authentication, making it ideal for starting new training experiences or resetting user context.

**Use Cases:**
- Starting new training modules or courses
- Resetting user progress for a fresh start
- Creating separate sessions for different users on the same device
- Beginning new assessment attempts

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
//C# Method Signature
public static void Abxr.StartNewSession()

// Example Usage
Abxr.StartNewSession();
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// C++ Method Signature
void UAbxr::StartNewSession();

// Example Usage
UAbxr::StartNewSession();
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Method Signature
static StartNewSession(): void

// Example Usage
Abxr.StartNewSession();
```

</details>

#### ReAuthenticate
Trigger manual reauthentication with existing stored parameters. This method is primarily useful for testing authentication flows or recovering from authentication issues.

**Use Cases:**
- Testing authentication flows during development
- Recovering from authentication errors
- Refreshing expired credentials
- Debugging authentication issues

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
//C# Method Signature
public static void Abxr.ReAuthenticate()

// Example Usage
Abxr.ReAuthenticate();
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// C++ Method Signature
void UAbxr::ReAuthenticate();

// Example Usage
UAbxr::ReAuthenticate();
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// JavaScript Method Signature
static ReAuthenticate(): void

// Example Usage
Abxr.ReAuthenticate();
```

</details>

**Note:** All session management methods work asynchronously and will trigger the `OnAuthCompleted` event when authentication completes, allowing you to respond to success or failure states.

### Debug Window

The Debug Window is a little bonus feature from the AbxrLib developers.
To help with general debugging, this feature routes a copy of all AbxrLib messages (Logs, Events, etc) to a window within the VR space. This enables developers to view logs in VR without having to repeatedly take on and off your headset while debugging.

> **Note:** Debug Window is currently only available in Unity. WebXR and Unreal Engine support is planned for future releases.

#### Setup
To use this feature, simply drag the `AbxrDebugWindow` Prefab from `AbxrLib for Unity/Resources/Prefabs`, to whatever object in the scene you want this window attached to (i.e. `Left Controller`).

### ArborXR Device Management

These methods provide access to device-level information and SSO authentication status on ArborXR-managed devices. These are convenience methods that operate at the device level, separate from the app-level authentication managed by the ABXRLib SDK.

> **Note:** ArborXR Device Management is supported in Unity and WebXR. Unreal Engine support is planned for future releases.

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
// Device Information Methods
string deviceId = Abxr.GetDeviceId();           // UUID assigned to device by ArborXR
string deviceTitle = Abxr.GetDeviceTitle();     // Title given to device by admin
string deviceSerial = Abxr.GetDeviceSerial();   // Serial assigned to device by OEM
string[] deviceTags = Abxr.GetDeviceTags();     // Tags added to device by admin

// Organization Information
string orgId = Abxr.GetOrgId();                 // UUID of the organization
string orgTitle = Abxr.GetOrgTitle();           // Name assigned to organization
string orgSlug = Abxr.GetOrgSlug();             // Identifier generated by ArborXR

// Network Information
string macFixed = Abxr.GetMacAddressFixed();    // Physical MAC address
string macRandom = Abxr.GetMacAddressRandom();  // Randomized MAC address for WiFi

// Authentication Status
bool isAuthenticated = Abxr.GetIsAuthenticated(); // Whether device is SSO authenticated
string accessToken = Abxr.GetAccessToken();       // SSO access token
string refreshToken = Abxr.GetRefreshToken();     // SSO refresh token
DateTime expiresDate = Abxr.GetExpiresDateUtc();  // When SSO access token expires
string fingerprint = Abxr.GetFingerprint();       // Device fingerprint
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Device Information Methods
const deviceId = Abxr.GetDeviceId();           // UUID assigned to device by ArborXR
const deviceTitle = Abxr.GetDeviceTitle();     // Title given to device by admin
const deviceSerial = Abxr.GetDeviceSerial();   // Serial assigned to device by OEM
const deviceTags = Abxr.GetDeviceTags();       // Tags added to device by admin

// Organization Information
const orgId = Abxr.GetOrgId();                 // UUID of the organization
const orgTitle = Abxr.GetOrgTitle();           // Name assigned to organization
const orgSlug = Abxr.GetOrgSlug();             // Identifier generated by ArborXR

// Network Information
const macFixed = Abxr.GetMacAddressFixed();    // Physical MAC address
const macRandom = Abxr.GetMacAddressRandom();  // Randomized MAC address for WiFi

// Authentication Status
const isAuthenticated = Abxr.GetIsAuthenticated(); // Whether device is SSO authenticated
const accessToken = Abxr.GetAccessToken();         // SSO access token
const refreshToken = Abxr.GetRefreshToken();       // SSO refresh token
const expiresDate = Abxr.GetExpiresDateUtc();      // When SSO access token expires
const fingerprint = Abxr.GetFingerprint();         // Device fingerprint
```

</details>

### Mixpanel Compatibility

The ABXRLib SDK provides full compatibility with Mixpanel's tracking patterns, making migration simple and straightforward. You can replace your existing Mixpanel tracking calls with minimal code changes while gaining access to ABXR's advanced XR analytics capabilities.

#### Why Migrate from Mixpanel?

- **XR-Native Analytics**: Purpose-built for spatial computing and immersive experiences
- **Advanced Session Management**: Resume training across devices and sessions  
- **Enterprise Features**: LMS integrations, SCORM/xAPI support, and AI-powered insights
- **Spatial Tracking**: Built-in support for 3D position data and XR interactions
- **Open Source**: No vendor lock-in, deploy to any backend service

**Migration Steps:**
1. Remove Mixpanel references from your project
2. Configure ABXRLib SDK credentials in your project settings
3. Replace Mixpanel tracking calls with ABXRLib equivalents throughout codebase

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
// Mixpanel → ABXR migration example
// Before: Mixpanel.Track("Plan Selected", props);
// After:  Abxr.Track("Plan Selected", props);
//
// Before: var props = new Value();
// After: var props = new Abxr.Value(); 

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

// Super Meta Data (equivalent to Mixpanel's Super Properties)
Abxr.Register("user_type", "premium");     // Set persistent "super metadata "
Abxr.RegisterOnce("app_version", "1.2.3"); // Set only if not already set
Abxr.Reset();                              // Clear all super metadata
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// Mixpanel → ABXR migration example
// Before: Mixpanel->Track("Plan Selected", Properties);
// After:  UAbxr::Track(TEXT("Plan Selected"), Properties);

//C++ Method Signatures
public static void UAbxr::Track(const FString& EventName);
public static void UAbxr::Track(const FString& EventName, TMap<FString, FString>& Properties);

// ABXR compatibility methods for Mixpanel users
UAbxr::Track(TEXT("user_signup"));
TMap<FString, FString> Properties;
Properties.Add(TEXT("amount"), TEXT("29.99"));
Properties.Add(TEXT("currency"), TEXT("USD"));
UAbxr::Track(TEXT("purchase_completed"), Properties);

// Timed events
UAbxr::StartTimedEvent(TEXT("puzzle_solving"));
// ... later ...
UAbxr::Track(TEXT("puzzle_solving")); // Duration automatically included
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Mixpanel → ABXR migration example
// Before: mixpanel.track("Plan Selected", properties);
// After:  Abxr.Track("Plan Selected", properties);

// JavaScript Method Signatures
static async Track(eventName: string, properties?: any): Promise<number>

// ABXR compatibility methods for Mixpanel users
Abxr.Track("user_signup");
const properties = {
    amount: 29.99,
    currency: "USD"
};
Abxr.Track("purchase_completed", properties);

// Timed events
Abxr.StartTimedEvent("puzzle_solving");
// ... later ...
Abxr.Track("puzzle_solving"); // Duration automatically included
```

</details>

#### Key Advantages Over Mixpanel

| Feature | Mixpanel | ABXRLib SDK |
|---------|----------|-----------|
| **Basic Event Tracking** | ✅ | ✅ |
| **Custom Properties** | ✅ | ✅ |
| **Super Metadata/Properties** | ✅ | ✅ (Register/RegisterOnce available) |
| **Timed Events** | ✅ | ✅ (StartTimedEvent available) |
| **3D Spatial Data** | ❌ | ✅ (Built-in Vector3 support) |
| **XR-Specific Events** | ❌ | ✅ (Assessments, Interactions, Objectives) |
| **Session Persistence** | Limited | ✅ (Cross-device, resumable sessions) |
| **Enterprise LMS Integration** | ❌ | ✅ (SCORM, xAPI, major LMS platforms) |
| **Real-time Collaboration** | ❌ | ✅ (Multi-user session tracking) |
| **Open Source** | ❌ | ✅ |

**Migration:** Simply replace Mixpanel calls with ABXRLib equivalents throughout your codebase.

### Cognitive3D Compatibility

The ABXRLib SDK provides compatibility with most of the Cognitive3D SDK, allowing you to either migrate from Cognitive3D or use both libraries side-by-side. You can add or migrate to ABXRLib from existing Cognitive3D implementations with minimal code changes while gaining access to ABXR's advanced XR analytics capabilities and LMS integrations.

> **Note:** Enums are also available in the global namespace as `AbxrEventStatus`, `AbxrInteractionType`, `AbxrInteractionResult`, `AbxrLogLevel`, `AbxrStorageScope`, and `AbxrStoragePolicy` for compatibility, but the recommended approach is to use the `Abxr.*` namespace to avoid conflicts.

#### Why Add ABXRLib to Your Cognitive3D Setup?

- **LMS Integration**: Native LMS platform support with SCORM/xAPI compatibility
- **Advanced Analytics**: Purpose-built dashboards for learning and training outcomes
- **Enterprise Features**: Session management, cross-device continuity, and AI-powered insights
- **Open Source**: No vendor lock-in, deploy to any backend service
- **Structured Events**: Rich event wrappers for assessments, objectives, and interactions
- **Side-by-Side Usage**: Keep your existing Cognitive3D implementation while adding ABXR features

#### Migration Overview

There are a few options for migration or combined usage:

**Option 1: Quick Migration**
If you want to replace Cognitive3D entirely, migration can be as simple as the following steps.

Step 1: Disable using statement
```cpp
// Before: using Cognitive3D;
// After:  //using Cognitive3D;
```
Step 2: Replace "Cognitive3D." with "Abxr." throughout your codebase to use the compatibility methods
```cpp
// Before (Cognitive3D):
new Cognitive3D.CustomEvent("Pressed Space").Send();

// After (ABXRLib) - Direct replacement:
new Abxr.CustomEvent("Pressed Space").Send();
```

**Option 2: Side-by-Side Usage**
Keep your existing Cognitive3D implementation and add ABXRLib for enhanced features:

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
// Your existing Cognitive3D code stays unchanged, and then add the Abxr version right below
new Cognitive3D.CustomEvent("Pressed Space").Send();
new Abxr.CustomEvent("Pressed Space").Send();

// Both libraries work side-by-side seamlessly
Cognitive3D.StartEvent("final_exam");
Abxr.EventAssessmentStart("final_exam"); // Enhanced LMS integration

// Later...
Cognitive3D.EndEvent("final_exam", "pass", 95);
Abxr.EventAssessmentComplete("final_exam", 95, EventStatus.Pass); // Structured data
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Your existing Cognitive3D code stays unchanged, and then add the Abxr version right below
new Cognitive3D.CustomEvent("Pressed Space").Send();
new Abxr.CustomEvent("Pressed Space").Send();

// Both libraries work side-by-side seamlessly
Cognitive3D.StartEvent("final_exam");
Abxr.EventAssessmentStart("final_exam"); // Enhanced LMS integration

// Later...
Cognitive3D.EndEvent("final_exam", "pass", 95);
Abxr.EventAssessmentComplete("final_exam", 95, Abxr.EventStatus.Pass); // Structured data
```

</details>

#### Cognitive3D vs ABXRLib

| Feature | Cognitive3D | ABXRLib SDK |
|---------|-------------|-----------|
| **Spatial Analytics** | ✅ | ❌ |
| **Basic Event Tracking** | ✅ | ✅ |
| **Custom Properties** | ✅ | ✅ |
| **Session Properties** | ✅ | ✅ (Enhanced with persistence) |
| **LMS Integration** | ❌ | ✅ (SCORM, xAPI, major platforms) |
| **Structured Learning Events** | ❌ | ✅ (Assessments, Objectives, Interactions) |
| **Cross-Device Sessions** | ❌ | ✅ (Resume training across devices) |
| **AI-Powered Insights** | ❌ | ✅ (Content optimization, learner analysis) |
| **Open Source** | ❌ | ✅ |

---

## Support

### Resources

- **Docs:** [https://help.arborxr.com/](https://help.arborxr.com/)
- **GitHub Unity:** [https://github.com/ArborXR/abxrlib-for-unity](https://github.com/ArborXR/abxrlib-for-unity)
- **GitHub Unreal:** [https://github.com/ArborXR/abxrlib-for-unreal](https://github.com/ArborXR/abxrlib-for-unreal)
- **GitHub WebXR:** [https://github.com/ArborXR/abxrlib-for-webxr](https://github.com/ArborXR/abxrlib-for-webxr)

### FAQ

#### How do I retrieve my Application ID and Authorization Secret?
Your Application ID can be found in the ArborXR Insights Web Dashboard under the application details (you must be sure to use the App ID from the specific application you need data sent through). For the Authorization Secret, navigate to Settings > Organization Codes on the same dashboard.

#### How do I enable object tracking?
Object tracking can be enabled by adding the Track Object component to any GameObject in your scene via the Unity Inspector.

#### Which platforms support which features?

| Feature | Unity | Unreal | WebXR |
|---------|-------|--------|-------|
| **Basic Event Tracking** | ✅ | ✅ | ✅ |
| **Analytics Event Wrappers** | ✅ | ✅ | ✅ |
| **Timed Events** | ✅ | ✅ | ✅ |
| **Super Meta Data** | ✅ | ✅ | ✅ |
| **Logging** | ✅ | ✅ | ✅ |
| **Storage** | ✅ | ✅ | ✅ |
| **Telemetry** | ✅ | ✅ | ✅ |
| **AI Integration** | ✅ | ✅ | ✅ |
| **Exit Polls** | ✅ | ✅ | ✅ |
| **Module Targets** | ✅ | 🔄 Planned | ✅ |
| **Authentication** | ✅ | ✅ | ✅ |
| **Headset Removal** | ✅ | ✅ | ✅ |
| **Session Management** | ✅ | ✅ | ✅ |
| **Debug Window** | ✅ | 🔄 Planned | 🔄 Planned |
| **ArborXR Device Management** | ✅ | 🔄 Planned | ✅ |
| **Mixpanel Compatibility** | ✅ | ✅ | ✅ |
| **Cognitive3D Compatibility** | ✅ | ❌ | ✅ |

**Legend:** ✅ Supported | 🔄 Planned | ❌ Not Available

#### Troubleshooting

**Problem: Library fails to authenticate**
- **Solution**: Verify your App ID, Org ID, and Auth Secret are correct in your project settings
- **Check**: Ensure all three credentials are entered in your platform's configuration
- **Debug**: Check the console/logs for detailed ABXR authentication error messages

**Problem: Events not being sent**
- **Solution**: Verify authentication completed successfully during app startup
- **Debug**: Monitor the console/logs for ABXR connection status messages
- **Check**: Ensure your event names use snake_case format for best processing

**Problem: Library works in editor but not in packaged builds**
- **Solution**: For production builds, only include App ID in project settings
- **Check**: Remove Org ID and Auth Secret from packaged builds distributed to third parties
- **Alternative**: Use ArborXR-managed devices where credentials are automatically provided

**Problem: Missing credentials on non-managed devices**
- **Solution**: Ensure all three credentials (App ID, Org ID, Auth Secret) are configured for development/testing
- **Check**: Verify credentials are correctly entered in your platform's project settings
- **Debug**: Check that the configuration is properly saved with the project

---

## Quick Start Guide

Once installed and configuration is complete, you can start tracking assessments with these simple calls:

<details open>
<summary><strong>Unity (C#)</strong></summary>

```cpp
// Add at the start your training (or training module)
Abxr.EventAssessmentStart("safety_training1");

// Add at the end your training (or training module)
Abxr.EventAssessmentComplete("safety_training1", 92, EventStatus.Pass);
// or
Abxr.EventAssessmentComplete("safety_training1", 28, EventStatus.Fail);
```

In many cases you may want to track sub-tasks within the content

```cpp
// To track
Abxr.EventObjectiveStart("open_valve");
Abxr.EventObjectiveComplete("open_valve", 100, EventStatus.Complete);

// Interaction tracking (individual user responses)
Abxr.EventInteractionStart("select_option_a");
Abxr.EventInteractionComplete("select_option_a", InteractionType.Select, "true");
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// Add at the start your training (or training module)
UAbxr::EventAssessmentStart(TEXT("safety_training1"));

// Add at the end your training (or training module)
UAbxr::EventAssessmentComplete(TEXT("safety_training1"), 92, EEventStatus::Pass);
// or
UAbxr::EventAssessmentComplete(TEXT("safety_training1"), 28, EEventStatus::Fail);
```

In many cases you may want to track sub-tasks within the content

```cpp
// To track
UAbxr::EventObjectiveStart(TEXT("open_valve"));
UAbxr::EventObjectiveComplete(TEXT("open_valve"), 100, EEventStatus::Complete);

// Interaction tracking (individual user responses)
UAbxr::EventInteractionStart(TEXT("select_option_a"));
UAbxr::EventInteractionComplete(TEXT("select_option_a"), EInteractionType::Select, TEXT("true"));
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Add at the start your training (or training module)
Abxr.EventAssessmentStart('safety_training1');

// Add at the end your training (or training module)
Abxr.EventAssessmentComplete('safety_training1', 92, Abxr.EventStatus.Pass);
// or
Abxr.EventAssessmentComplete('safety_training1', 28, Abxr.EventStatus.Fail);
```

In many cases you may want to track sub-tasks within the content

```javascript
// To track
Abxr.EventObjectiveStart('open_valve');
Abxr.EventObjectiveComplete('open_valve', 100, Abxr.EventStatus.Complete);

// Interaction tracking (individual user responses)
Abxr.EventInteractionStart('select_option_a');
Abxr.EventInteractionComplete('select_option_a', Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, 'true');
```

</details>

<details>
<summary><strong>Unreal Engine (C++)</strong> - Click to expand</summary>

```cpp
// Add at the start your training (or training module)
UAbxr::EventAssessmentStart(TEXT("safety_training1"));

// Add at the end your training (or training module)
UAbxr::EventAssessmentComplete(TEXT("safety_training1"), 92, EEventStatus::Pass);
// or
UAbxr::EventAssessmentComplete(TEXT("safety_training1"), 28, EEventStatus::Fail);
```

In many cases you may want to track sub-tasks within the content

```cpp
// To track
UAbxr::EventObjectiveStart(TEXT("open_valve"));
UAbxr::EventObjectiveComplete(TEXT("open_valve"), 100, EEventStatus::Complete);

// Interaction tracking (individual user responses)
UAbxr::EventInteractionStart(TEXT("select_option_a"));
UAbxr::EventInteractionComplete(TEXT("select_option_a"), EInteractionType::Select, TEXT("true"));
```

</details>

<details>
<summary><strong>WebXR (JavaScript/TypeScript)</strong> - Click to expand</summary>

```javascript
// Add at the start your training (or training module)
Abxr.EventAssessmentStart('safety_training1');

// Add at the end your training (or training module)
Abxr.EventAssessmentComplete('safety_training1', 92, Abxr.EventStatus.Pass);
// or
Abxr.EventAssessmentComplete('safety_training1', 28, Abxr.EventStatus.Fail);
```

In many cases you may want to track sub-tasks within the content

```javascript
// To track
Abxr.EventObjectiveStart('open_valve');
Abxr.EventObjectiveComplete('open_valve', 100, Abxr.EventStatus.Complete);

// Interaction tracking (individual user responses)
Abxr.EventInteractionStart('select_option_a');
Abxr.EventInteractionComplete('select_option_a', Abxr.InteractionType.Select, Abxr.InteractionResult.Correct, 'true');
```

</details>

---
