# ABXR SDK for Unity

The name "ABXR" stands for "Analytics Backbone for XR"—a flexible, open-source foundation for capturing and transmitting spatial, interaction, and performance data in XR. When combined with **ArborXR Insights**, ABXR transforms from a lightweight instrumentation layer into a full-scale enterprise analytics solution—unlocking powerful dashboards, LMS/BI integrations, and AI-enhanced insights.

## Table of Contents
1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Sending Data](#sending-data)
5. [Mixpanel Migration & Compatibility](#mixpanel-migration--compatibility)
6. [FAQ](#faq)
7. [Troubleshooting](#troubleshooting)
8. [Contact](#contact)

---

## Introduction

### Overview

The **ABXR SDK for Unity** is an open-source analytics and data collection library that provides developers with the tools to collect and send XR data to any service of their choice. This library enables scalable event tracking, telemetry, and session-based storage—essential for enterprise and education XR environments.

> 💡 **Quick Start:** Most developers can integrate ABXR SDK and log their first event in under **15 minutes**.

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

### Event Methods
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
Abxr.StartTimedEvent("Image Upload");
// ... user performs upload activity for 20 seconds ...
Abxr.Event("Image Upload"); // Duration automatically included: 20 seconds

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

---

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

### Storage API
The Storage API enables developers to store and retrieve learner/player progress, facilitating the creation of long-form training content. When users log in using ArborXR's facility or the developer's in-app solution, these methods allow users to continue their progress on different headsets, ensuring a seamless learning experience across multiple sessions or devices.

#### Save Progress
```cpp
//C# Event Method Signatures
public void Abxr.SetStorageEntry(Dictionary<string, string> data, string name = "state", bool keep_latest = true, string origin = null, bool session_data = false)

// Example usage
Abxr.SetStorageEntry(new Dictionary<string, string>{{"progress", "75%"}});
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
public Dictionary<string, string> Abxr.GetStorageEntry(string name = "state", string origin = null, string[] tags_any = null, string[] tags_all = null, bool user_only = false)

// Example usage
var state = Abxr.GetStorageEntry("state");
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
public void Abxr.RemoveStorageEntry(string name = "state")

// Example usage
Abxr.RemoveStorageEntry("state");
```
**Parameters:**
- `name` (string): Optional. The identifier of the storage entry to remove. Default is "state".

#### Get All Entries
```cpp
//C# Event Method Signatures
public Dictionary<string, string> Abxr.GetAllStorageEntries()

// Example usage
var allEntries = Abxr.GetAllStorageEntries();
```
**Returns:** A dictionary containing all storage entries for the current user/device.

---

### Telemetry
The Telemetry Methods provide comprehensive tracking of the XR environment. By default, they capture headset and controller movements, but can be extended to track any custom objects in the virtual space. These functions also allow collection of system-level data such as frame rates or device temperatures. This versatile tracking enables developers to gain deep insights into user interactions and application performance, facilitating optimization and enhancing the overall XR experience.

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
### AI Integration Methods
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

---

## Mixpanel Migration & Compatibility

The ABXR SDK provides full compatibility with Mixpanel's Unity SDK, making migration simple and straightforward. You can replace your existing Mixpanel tracking calls with minimal code changes while gaining access to ABXR's advanced XR analytics capabilities.

### Why Migrate from Mixpanel?

- **XR-Native Analytics**: Purpose-built for spatial computing and immersive experiences
- **Advanced Session Management**: Resume training across devices and sessions  
- **Enterprise Features**: LMS integrations, SCORM/xAPI support, and AI-powered insights
- **Spatial Tracking**: Built-in support for 3D position data and XR interactions
- **Open Source**: No vendor lock-in, deploy to any backend service

### 3-Step Migration:

#### Step 1: Remove Mixpanel References
```cpp
// Remove or comment out these lines:
// using mixpanel;
// Any Mixpanel initialization code...

// ABXR SDK is already available - no additional using statements needed
```

#### Step 2: Configure ABXR SDK
Follow the [Configuration](#configuration) section to set up your App ID, Org ID, and Auth Secret in the Unity Editor.

#### Step 3: Simple String Replace
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
var props = new Value();  // Value class included for compatibility
props["Plan"] = "Premium";
Abxr.Track("Plan Selected", props);
```

### Mixpanel Compatibility Methods

The ABXR SDK includes a complete `Value` class, `Track` methods, and `StartTimedEvent` that match Mixpanel's API:

```cpp
//C# Compatibility Class
public class Value : Dictionary<string, object>
{
    public Value() : base() { }
    public Value(IDictionary<string, object> dictionary) : base(dictionary) { }
    public Dictionary<string, string> ToDictionary()  // Converts to ABXR format
}

//C# Track Method Signatures  
public static void Abxr.StartTimedEvent(string eventName)
public static void Abxr.Track(string eventName)
public static void Abxr.Track(string eventName, Value properties)
public static void Abxr.Track(string eventName, Dictionary<string, object> properties)

// Example Usage - Drop-in Replacement
Abxr.Track("user_signup");
Abxr.Track("purchase_completed", new Value { ["amount"] = 29.99, ["currency"] = "USD" });

// Timed Events (matches Mixpanel exactly!)
Abxr.StartTimedEvent("Image Upload");
// ... 20 seconds later ...
Abxr.Track("Image Upload"); // Duration automatically added: 20 seconds
// OR
Abxr.Event("Image Upload"); // Also works with Event() - duration added automatically!
```

### Key Differences & Advantages

| Feature | Mixpanel | ABXR SDK |
|---------|----------|-----------|
| **Basic Event Tracking** | ✅ | ✅ |
| **Custom Properties** | ✅ | ✅ |
| **3D Spatial Data** | ❌ | ✅ (Built-in Vector3 support) |
| **XR-Specific Events** | ❌ | ✅ (Assessments, Interactions, Objectives) |
| **Session Persistence** | Limited | ✅ (Cross-device, resumable sessions) |
| **Enterprise LMS Integration** | ❌ | ✅ (SCORM, xAPI, major LMS platforms) |
| **Real-time Collaboration** | ❌ | ✅ (Multi-user session tracking) |
| **Open Source** | ❌ | ✅ |

### Migration Summary

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

### Value Class Compatibility

The included `Value` class is fully compatible with Mixpanel's implementation:

```cpp
var mixpanelStyleProps = new Value();
mixpanelStyleProps["user_id"] = "12345";
mixpanelStyleProps["plan_type"] = "premium";
mixpanelStyleProps["trial_days"] = 30;

// Works exactly the same as Mixpanel
Abxr.Track("subscription_started", mixpanelStyleProps);
```

Properties are automatically converted to the appropriate format for ABXR's backend while maintaining full compatibility with your existing Mixpanel integration patterns.

## Exit Polls
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

## Authentication Event
To subscribe to Authentication success or failure, use the following Action. This returns 'true' for success and 'false' for failure (along with the error message in the failure case).
```cpp
public static Action onAuthCompleted;
```

## Headset Removal (Session Integrity)
To improve session fidelity and reduce user spoofing or unintended headset sharing, we will trigger a re-authentication prompt when the headset is taken off and then put back on mid-session. If the headset is put on by a new user this will trigger an event defined in Abxr.cs. This can be subscribed to if the developer would like to have logic corresponding to this event.
```cpp
public static Action onHeadsetPutOnNewSession;
```
If the developer would like to have logic to correspond to these events, that would be done by subscribing to these events.

## Debug Window
The Debug Window is a little bonus feature from the AbxrLib developers.
To help with general debugging, this feature routes a copy of all AbxrLib messages (Logs, Events, etc) to a window within the VR space. This enables developers to view logs in VR without having to repeatedly take on and off your headset while debugging.

### Setup
To use this feature, simply drag the `AbxrDebugWindow` Prefab from `AbxrLib for Unity/Resources/Prefabs`, to whatever object in the scene you want this window attached to (i.e. `Left Controller`).

## FAQ

### Q: How do I retrieve my Application ID and Authorization Secret?
A: Your Application ID can be found in the Web Dashboard under the application details (you must be sure to use the App ID from the specific application you need data sent through). For the Authorization Secret, navigate to Settings > Organization Codes on the same dashboard.

### Q: How do I enable object tracking?
A: Object tracking can be enabled by adding the Track Object component to any GameObject in your scene via the Unity Inspector.


## Troubleshooting

---

## Persisting User State with ArborXR Insights

The ABXR SDK includes a built-in storage interface that enables persistent session data across XR devices. This is ideal for applications with long-form content, resumable training, or user-specific learning paths.

When integrated with **ArborXR Insights**, session state data is securely stored and can be retrieved from any device, enabling users to resume exactly where they left off. 

### Benefits of Using ArborXR Insights for Storage:
- Cross-device continuity and resuming sessions
- Secure, compliant storage (GDPR, HIPAA-ready)
- Configurable behaviors (e.g., `keepLatest`, append history)
- Seamless AI and analytics integration for stored user states

To use this feature, simply call the storage functions provided in the SDK (`SetStorageEntry`, `GetStorageEntry`, etc.). These entries are automatically synced with ArborXR’s cloud infrastructure, ensuring consistent data across sessions.

---

## ArborXR Insights Web Portal & API

For dashboards, analytics queries, impersonation, and integration management, use the **ArborXR Insights User API**, accessible through the platform's admin portal.

Example features:
- Visualize training completion & performance by cohort
- Export SCORM/xAPI-compatible results
- Query trends in interaction data

Endpoints of note:
- `/v1/analytics/dashboard`
- `/v1/admin/system/organization/{org_id}`
- `/v1/analytics/data`

## ArborXR Device Management

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

---

## Support

- **Docs:** [https://help.arborxr.com/](https://help.arborxr.com/)
- **GitHub:** [https://github.com/ArborXR/abxrlib-for-unity](https://github.com/ArborXR/abxrlib-for-unity)
