# ABXR SDK for Unity

The name "ABXR" stands for "Analytics Backbone for XR"—a flexible, open-source foundation for capturing and transmitting spatial, interaction, and performance data in XR. When combined with **ArborXR Insights**, ABXR transforms from a lightweight instrumentation layer into a full-scale enterprise analytics solution—unlocking powerful dashboards, LMS/BI integrations, and AI-enhanced insights.

## Table of Contents
1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Sending Data](#sending-data)
5. [FAQ](#faq)
6. [Troubleshooting](#troubleshooting)
7. [Contact](#contact)

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

### Using with ArborXR Insights Early Access

To use the ABXR SDK with ArborXR Insights Early Access program:

#### Get Early Access Credentials
1. Go to the ArborXR Insights Early Access web app and log in (will require [official Early Access sign up](https://arborxr.com/insights-early-access) & onboarding process to access).
2. Grab these three values from the **View Data** screen of the specific app you are configuring:
- App ID
- Organization ID
- Authentication Secret

#### Configure Unity Project
1. Open `Analytics for XR > Configuration` in the Unity Editor.
2. Paste in the Early Access App ID, Org ID, and Auth Secret. All 3 are required if you are testing from Unity itself.

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

### Event Wrappers (for LMS Compatibility)
-The LMS Event Functions are specialized versions of the Event method, tailored for common scenarios in XR experiences. These functions help enforce consistency in event logging across different parts of the application and are crucial for powering integrations with Learning Management System (LMS) platforms. By using these standardized wrapper functions, developers ensure that key events like starting or completing levels, assessments, or interactions are recorded in a uniform format. This consistency not only simplifies data analysis but also facilitates seamless communication with external educational systems, enhancing the overall learning ecosystem.

#### Assessments
Assessments are intended to track the overall performance of a learner across multiple Objectives and Interactions. 
* Think of it as the learner's score for a specific course or curriculum.
* When the Assessment is complete, it will automatically record and close out the Assessment in the various LMS platforms we support.

```cpp
//C# List Definition
public enum ResultOptions
{
    Pass,
    Fail,
    Complete,
    Incomplete
}

//C# Event Method Signatures
public void Abxr.EventAssessmentStart(string assessmentName) 
public void Abxr.EventAssessmentStart(string assessmentName, Dictionary<string, string> meta = null)

public void Abxr.EventAssessmentComplete(string assessmentName, int score, ResultOptions result = ResultOptions.Complete)
public void Abxr.EventAssessmentComplete(string assessmentName, int score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null)

// Example Usage
Abxr.EventAssessmentStart("final_exam");
Abxr.EventAssessmentComplete("final_exam", 92, ResultOptions.Pass);
```

#### Objectives
```cpp
//C# List Definition
public enum ResultOptions
{
    Pass,
    Fail,
    Complete,
    Incomplete
}

//C# Event Method Signatures
public void Abxr.EventObjectiveStart(string objectiveName)
public void Abxr.EventObjectiveStart(string objectiveName, Dictionary<string, string> meta)
public void Abxr.EventObjectiveStart(string objectiveName, string metaString = "")

public void Abxr.EventObjectiveComplete(string objectiveName, int score, ResultOptions result = ResultOptions.Complete)
public void Abxr.EventObjectiveComplete(string objectiveName, int score, ResultOptions result = ResultOptions.Complete, Dictionary<string, string> meta = null)

// Example Usage
Abxr.EventObjectiveStart("open_valve");
Abxr.EventObjectiveComplete("open_valve", 100, ResultOptions.Complete);
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
   Number // integer
}

//C# Event Method Signatures
public void Abxr.EventInteractionStart(string interactionName)

public void Abxr.EventInteractionComplete(string interactionName, string result)
public void Abxr.EventInteractionComplete(string interactionName, string result, string result_details = null)
public void Abxr.EventInteractionComplete(string interactionName, string result, string result_details = null, InteractionType type = InteractionType.Text)
public void Abxr.EventInteractionComplete(string interactionName, string result, string result_details = null, InteractionType type = InteractionType.Text, Dictionary<string, string> meta = null)

// Example Usage
Abxr.EventInteractionStart("select_option_a");
Abxr.EventInteractionComplete("select_option_a", "true", "a", InteractionType.Select);
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
```

**Parameters for all Event Wrapper Functions:**
- `levelName/assessmentName/objectiveName/interactionName` (string): The identifier for the assessment, objective, interaction, or level.
- `score` (int): The numerical score achieved. While typically between 1-100, any integer is valid. In metadata, you can also set a minScore and maxScore to define the range of scores for this objective.
- `result` (ResultOptions for Assessment and Objective): The basic result of the assessment or objective.
- `result` (Interactions): The result for the interaction is based on the InteractionType.
- `result_details` (string): Optional. Additional details about the result. For interactions, this can be a single character or a string. For example: "a", "b", "c" or "correct", "incorrect".
- `type` (InteractionType): Optional. The type of interaction for this event.
- `meta` (Dictionary<string, string>): Optional. Additional key-value pairs describing the event.

**Note:** All complete events automatically calculate duration if a corresponding start event was logged.

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

### Authentication Methods

#### SetUserId
```cpp
//C# Event Method Signatures
public void Abxr.SetUserId(string userId)
```

#### SetUserMeta
```cpp
//C# Event Method Signatures
public void Abxr.SetUserMeta(string metaString)
```

**Parameters:**
- `userId` (string): The User ID used during authentication (setting this with trigger re-authentication).
- `metaString` (string): A string of key-value pairs in JSON format.

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
A: Your Application ID can be found in the Web Dashboard under the application details. For the Authorization Secret, navigate to Settings > Organization Codes on the same dashboard.

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

---

## Support

- **Docs:** [https://help.arborxr.com/](https://help.arborxr.com/)
- **GitHub:** [https://github.com/ArborXR/abxrlib-for-unity](https://github.com/ArborXR/abxrlib-for-unity)
