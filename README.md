# ArborXR Insights Unity SDK

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

The **ArborXR Insights SDK for Unity** empowers developers to seamlessly integrate enterprise-grade XR analytics and data tracking into their applications. Built on the **AbxrLib** runtime, this open-source library enables scalable event tracking, telemetry, and session-based storage—essential for enterprise and education XR environments.

ArborXR Insights enhances product value by offering:
- Seamless LMS & Business Intelligence integrations
- A robust, analytics-driven backend
- Encrypted, cross-session data persistence
- AI-ready event streams

### Core Features

- **Event Tracking:** Monitor user behaviors, interactions, and system events.
- **Spatial & Hardware Telemetry:** Capture headset/controller movement and hardware metrics.
- **Object & System Info:** Track XR objects and environmental state.
- **Storage & Session Management:** Support resumable training and long-form experiences.
- **Logs:** Developer and system-level logs available across sessions.

---

## Installation

### Unity Package Installation

1. Open Unity and go to `Window > Package Manager`.
2. Select the '+' dropdown and choose **'Add package from git URL'**.
3. Use the GitHub repo URL:
   ```
   https://github.com/informXR/abxrlib-for-unity.git
   ```
4. Once imported, you will see `ArborXR Insights` in your Unity toolbar.

---

## Configuration

### Setup & Authentication

Configure the SDK with settings available via the [ArborXR Dashboard](https://app.arborxr.com/):

1. In Unity, navigate to: `ArborXR Insights > Configuration`.
2. Input: **Application ID**

(CLOSED BETA NOTE: Currently, you will need to go to the **ArborXR Insights Beta Portal** to access SDK settings. Simply go to the "Data Policies" tab, where you can check and modify "Configurations Settings" that will effect how the SDK operates. We advise not to change them unless you have a specific use case to explore.

Also, if you want to use LMS integration in-headset sign-in, you MUST make sure that in **Data Policies** > **Configure Headset Login Policy**, that **"LMS Integration"** is selected. This will apply globally to all headsets using the SDK. In the future, you should be able to set it for certain Groups managed by ArborXR MDM.)

The value is retrieved from the [ArborXR Dashboard](https://app.arborxr.com/):

- Navigate to `Content Library > [Your Application]` and you will find the value in the URL.
- Example: https:\/\/app.arborxr.com\/abcd1234-abc1-2345-6789-abc1234d\/content\/`987654cba-54ba-dc43-98cb-dcba54321`
  - **Application ID** = `987654cba-54ba-dc43-98cb-dcba54321`

(CLOSED BETA NOTE: You can retrieve the Application ID, as well as the Organization ID & Authentication Secret from the **ArborXR Insights Beta Portal** from any app shown in the View Data tab. You should only require the Application ID if you are distributing the app from ArborXR's Content Manager services, however, if you wish to sideload content or distrubute another way, you must include the Organization ID & Authentication Secret in the Unity Configurations to authenticate successfully.)

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
public void Abxr.EventAssessmentStart(string assessmentName) 

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
    Rating
}

// C# Event Method Signature
public void PollUser(string prompt, ExitPollHandler.PollType pollType)

// Example usage
Abxr.PollUser("How would you rate this training experience?", PollType.Rating);
```
**Poll Types:**
- `Thumbs Up/Thumbs Down`
- `Rating (1-5)`

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

## Backend Integration: ArborXR Insights Storage API

All Unity data is securely routed to the **ArborXR Insights Storage API**, which:
- Validates authentication via signed JWTs
- Ensures session continuity across user/device
- Persists structured logs into MongoDB Atlas
- Provides async-ready responses for batch telemetry logging

Example endpoints:
- `/v1/collect/event` → Event logging
- `/v1/collect/log` → Developer log ingestion
- `/v1/collect/telemetry` → Positional + hardware data
- `/v1/storage` → Persisted user/device state

---

## Web UI + Insights User API

For dashboards, analytics queries, impersonation, and integration management, use the **ArborXR Insights User API**, accessible through the platform’s admin portal.

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
