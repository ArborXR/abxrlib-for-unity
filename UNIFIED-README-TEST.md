# ABXRLib SDK - Unified Documentation

## Quick Start Guide

Once installed and configuration is complete, you can start tracking assessments with these simple calls:

### Assessment Tracking Examples

<details open>
<summary><strong>Unity (C#)</strong> - Click to collapse</summary>

```cpp
// Add at the start your training (or training module)
Abxr.EventAssessmentStart("safety_training1");

// Add at the end your training (or training module)
Abxr.EventAssessmentComplete("safety_training1", 92, EventStatus.Pass);
// or
Abxr.EventAssessmentComplete("safety_training1", 28, EventStatus.Fail);
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

</details>

---
