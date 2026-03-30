# ABXRLib SDK for Unity

The name "ABXR" stands for "Analytics Backbone for XR"—a flexible, open-source foundation for capturing and transmitting spatial, interaction, and performance data in XR. When combined with **ArborXR Insights**, ABXR transforms from a lightweight instrumentation layer into a full-scale enterprise analytics solution—unlocking powerful dashboards, LMS/BI integrations, and AI-enhanced insights.

## Table of Contents
1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Quick Start](#quick-start)
5. [Full Documentation](#full-documentation)
6. [Support](#support)

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

To use the ABXRLib SDK with ArborXR Insights, configure **app token** and **org token** (recommended). The same model applies to the [Unreal](https://github.com/ArborXR/abxrlib-for-unreal#configuration) and [WebXR](https://github.com/ArborXR/abxrlib-for-webxr#configuration) SDKs.

#### App token and org token (recommended)

#### Configure Unity Project
1. Open `Analytics for XR > Configuration` in the Unity Editor.
2. **Use App Tokens** is on by default for new configuration; keep it enabled for JWT authentication.
3. Set **App Token** (required): JWT for your app, from your distribution channel or ArborXR portal (**Content Library** → **Managed** app → **Insights Hub**).
4. **Org Token:** Leave empty to use the **dynamic org token** (derived from device/org context when available). For single-customer builds (e.g. production_custom), set the org token as required. For local testing in the Editor, you can paste your **App Token** into the **Org Token** field when you need both fields populated.

**Development / testing:** Set App Token; on ArborXR-managed devices, org context can be supplied at runtime (dynamic org token).

**Production builds:** Set App Token; use dynamic org token (empty org token in config) where the device or runtime provides org context.

> **⚠️ Security Note:** Avoid compiling org tokens or long-lived secrets into builds distributed to third parties. For general distribution, use ArborXR-managed devices or dynamic org token. For single-customer deployments, follow your security guidelines.

#### Legacy (App ID / Org ID / Auth Secret)

If your project still uses the legacy scheme: in Configuration, leave **Use App Tokens** off and set App ID, Org ID, and Auth Secret from the app’s credential or details views in the portal where your organization still exposes them. On ArborXR-managed devices, only App ID may be required; Org ID and Auth Secret can auto-fill. New integrations should use app token and org token.

### Using with Other Backend Services
For information on implementing your own backend service or using other compatible services, please refer to the ABXR protocol specification.

---

## Quick Start

### Essential Event Tracking (Required)

**Assessment events are required** to activate grading dashboards and LMS integration. Send these events to track training completion, scores, and pass/fail status.

```csharp
// When training starts
Abxr.EventAssessmentStart("safety_training");

// When training completes
Abxr.EventAssessmentComplete("safety_training", 92, EventStatus.Pass);
// or
Abxr.EventAssessmentComplete("safety_training", 25, EventStatus.Fail);
```

**Assessment Complete Parameters:**
- `Score` (second parameter) takes a 0-100 value
- The `EventStatus` enum has `Pass`, `Fail`, `Complete`, `Incomplete`, `Browsed`, `NotAttempted` options

### Tracking Objectives (Recommended)

For more detailed tracking, you can also track specific objectives within your training:

```csharp
// To mark a specific objective start
Abxr.EventObjectiveStart("open_valve");

// When the objective is complete
Abxr.EventObjectiveComplete("open_valve", 100, EventStatus.Complete);
```

---

## Full Documentation

- **[INTEGRATION_INSTRUCTIONS.md](INTEGRATION_INSTRUCTIONS.md)** – Step-by-step integration guide for adopting AbxrLib in your project; also written for AI assistants to implement or review an integration consistently.

For comprehensive documentation covering all features, advanced topics, and detailed examples, visit:

- **[ArborXR Insights Documentation](https://developers.arborxr.com/docs/insights)** - Main documentation hub
- **[Complete SDK Documentation](https://developers.arborxr.com/docs/insights/full-documentation/)** - Full API reference and feature documentation

The full documentation includes:
- Complete event tracking API (Events, Analytics Event Wrappers, Timed Events)
- Advanced features (Module Targets, Authentication, Session Management)
- Storage, Telemetry, Logging, and AI Integration
- Mixpanel and Cognitive3D compatibility guides
- Troubleshooting and best practices
- Platform-specific examples and code samples

---

## Support

### Resources

- **Docs:** [https://help.arborxr.com/](https://help.arborxr.com/)
- **GitHub:** [https://github.com/ArborXR/abxrlib-for-unity](https://github.com/ArborXR/abxrlib-for-unity)

### FAQ

#### How do I get my App Token and Org Token?
Use **App Token** and **Org Token** (recommended). Copy them from **Content Library** → **Managed** app → **Insights Hub** in the portal, or use values from your distribution channel. Leave **Org Token** empty to use the dynamic org token when the device or runtime provides org context; for Editor testing you can paste **App Token** into **Org Token** if needed. For legacy setups, Application ID and Authorization Secret are still available under application details and Settings > Organization Codes.

#### How do I enable object tracking?
Object tracking can be enabled by adding the Track Object component to any GameObject in your scene via the Unity Inspector.

For more troubleshooting help and detailed FAQs, see the [full documentation](https://developers.arborxr.com/docs/insights/full-documentation/).
