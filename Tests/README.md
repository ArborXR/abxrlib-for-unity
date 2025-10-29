# ABXRLib Unity Test Suite

This directory contains comprehensive tests for the ABXRLib Unity package using Unity Test Framework (UTF). The test suite validates all major features documented in the README and ensures the library works correctly across different scenarios.

## Test Structure

```
Tests/
├── Runtime/                    # PlayMode tests (run in Unity)
│   ├── AbxrLib.Tests.Runtime.asmdef
│   ├── Utilities/              # Test utilities and helpers
│   │   ├── TestHelpers.cs
│   │   ├── AuthenticationTestHelper.cs
│   │   ├── SharedAuthenticationHelper.cs
│   │   ├── CoroutineTestHelper.cs
│   │   ├── AuthHandoffTestHelper.cs
│   │   ├── TestDataCapture.cs
│   │   └── TestAuthenticationProvider.cs
│   ├── EventTrackingTests.cs
│   ├── AnalyticsEventTests.cs
│   ├── TimedEventTests.cs
│   └── SuperMetadataTests.cs
└── Editor/                     # EditMode tests (run in editor)
    └── AbxrLib.Tests.Editor.asmdef
```

## Running Tests

### In Unity Editor

1. Open Unity and load a project that has ABXRLib installed
2. Go to `Window > General > Test Runner`
3. Select the test category:
   - **PlayMode**: Tests that require Unity's runtime (most tests)
   - **EditMode**: Tests that run in editor only
4. Click **Run All** or run individual tests

### From Command Line

```bash
# Run all tests
Unity -batchmode -quit -projectPath /path/to/project -runTests -testPlatform playmode

# Run specific test
Unity -batchmode -quit -projectPath /path/to/project -runTests -testPlatform playmode -testFilter EventTrackingTests
```

## Test Categories

### Phase 1: Test Infrastructure ✅
- **Assembly Definitions**: Proper test assembly setup
- **Test Utilities**: Helper functions for common test operations
- **Real Server Testing**: All tests use actual server authentication

### Phase 2: Critical Path Tests ✅
- **Event Tracking Tests**: Basic event logging with various parameters
- **Analytics Event Tests**: Assessment, Objective, and Interaction wrappers
- **Timed Event Tests**: Duration calculation and timer management
- **Super Metadata Tests**: Persistent metadata across events

### Phase 3: Core Feature Tests (Planned)
- **Logging Tests**: Different log levels and metadata
- **Storage Tests**: Device and user scope storage operations
- **Telemetry Tests**: Manual and automatic telemetry collection
- **Authentication Tests**: Different authentication scenarios

### Phase 4: Advanced Feature Tests (Planned)
- **Module Target Tests**: LMS module assignment handling
- **Session Management Tests**: Cross-session continuity
- **AI Integration Tests**: AI proxy functionality
- **Exit Poll Tests**: User feedback collection
- **Compatibility Tests**: Mixpanel and Cognitive3D compatibility

### Phase 5: Integration Tests (Planned)
- **Data Batching Tests**: Queue management and batch processing
- **Configuration Tests**: ScriptableObject validation
- **Error Handling Tests**: Network failures and edge cases
- **Metadata Format Tests**: Various metadata formats

### Phase 6: Performance Tests (Planned)
- **Performance Tests**: High-frequency operations and memory usage
- **Thread Safety Tests**: Concurrent operations

## Configuration

### Using Existing Configuration

The test suite automatically uses the existing configuration from the demo app's `Assets/Resources/AbxrLib.asset` file. This includes:

- **appID**: Application identifier (already configured)
- **orgID**: Organization identifier (already configured) 
- **authSecret**: Authentication secret (already configured)
- **restUrl**: REST API endpoint URL (already configured)

### Configuration Validation

The tests automatically validate that the configuration is properly set up:

```csharp
// Tests automatically check configuration validity
if (!Configuration.Instance.IsValid())
{
    Debug.LogError("Configuration is invalid - check your AbxrLib.asset file");
}
```

### Real Server Integration Testing

The test suite uses **real server integration testing** with the existing configuration:

#### **Benefits of Real Server Testing**
- ✅ **End-to-end validation**: Tests complete authentication flow with real server
- ✅ **Real-world conditions**: Validates network conditions and server responses
- ✅ **Server compatibility**: Ensures client works with actual server
- ✅ **Real data**: Tests with actual user data and module configurations
- ✅ **No setup required**: Uses existing configuration from demo app

#### **Authentication Flow**

The tests automatically:
1. **Connect** to your configured server
2. **Authenticate** with existing credentials
3. **Handle AuthMechanism** responses from the server
4. **Validate** authentication completion

#### **Test Execution**

```csharp
[UnityTest]
public IEnumerator Test_RealServerAuthentication_CompletesSuccessfully()
{
    // Wait for authentication to complete
    float timeout = 30f;
    float elapsed = 0f;
    
    while (!Abxr.ConnectionActive() && elapsed < timeout)
    {
        yield return new WaitForSeconds(0.1f);
        elapsed += 0.1f;
    }
    
    Assert.IsTrue(Abxr.ConnectionActive(), "Authentication should complete");
}
```

## Test Development Guidelines

### Test Name Generation
- **ALWAYS** use `TestHelpers.GenerateRandomName(prefix)` for generating unique test names
- **NEVER** use hardcoded names that could cause test conflicts
- **Examples:**
  ```csharp
  string eventName = TestHelpers.GenerateRandomName("event");
  string assessmentName = TestHelpers.GenerateRandomName("assessment");
  string objectiveName = TestHelpers.GenerateRandomName("objective");
  string interactionName = TestHelpers.GenerateRandomName("interaction");
  ```

### SCORM Educational Content Hierarchy
When testing educational analytics events, follow SCORM standards:

#### Assessment → Objective → Interaction Hierarchy
- **Assessments** are the top-level learning containers
- **Objectives** happen within Assessments
- **Interactions** can happen within Assessments OR within Objectives
- **Multiple Objectives** can exist within one Assessment
- **Multiple Interactions** can exist within one Assessment or one Objective

#### Proper Test Sequencing
```csharp
// Correct SCORM hierarchy test pattern:
Abxr.EventAssessmentStart(assessmentName);
yield return new WaitForSeconds(0.2f);

Abxr.EventObjectiveStart(objectiveName);
yield return new WaitForSeconds(0.2f);

Abxr.EventInteractionStart(interactionName, interactionMeta);
yield return new WaitForSeconds(0.3f);

Abxr.EventInteractionComplete(interactionName, type, result, response);
yield return new WaitForSeconds(0.2f);

Abxr.EventObjectiveComplete(objectiveName, score, status);
yield return new WaitForSeconds(0.2f);

Abxr.EventAssessmentComplete(assessmentName, score, status);
```

#### Event Start/Complete Pairs
- **AssessmentComplete** should typically have **AssessmentStart** first
- **ObjectiveComplete** should typically have **ObjectiveStart** first  
- **InteractionComplete** should typically have **InteractionStart** first
- **Exception:** Test orphan events (complete without start) for error handling

### Shared Authentication Session
The test suite uses a shared authentication session for efficiency:

#### Test Execution Order
- **`_AuthenticationTests`** runs first (alphabetical ordering ensures this)
- **All other test classes** use `SharedAuthenticationHelper` to reuse the authenticated session
- **No need to authenticate** in each individual test

#### Setup Pattern
```csharp
[UnitySetUp]
public IEnumerator UnitySetUp()
{
    // Ensure shared authentication is completed before running tests
    yield return SharedAuthenticationHelper.EnsureAuthenticated();
}

[TearDown]
public void TearDown()
{
    TestHelpers.CleanupTestEnvironment();
    _dataCapture?.Clear();
}

[UnityTearDown]
public void UnityTearDown()
{
    // Reset shared authentication state for next test run
    SharedAuthenticationHelper.ResetAuthenticationState();
}
```

#### Benefits
- **Faster test execution** - Authentication happens once, not per test
- **Consistent state** - All tests start with the same authenticated session
- **Real server testing** - Uses actual authentication flow with real credentials
- **Test isolation** - Each test class resets state appropriately

### Event Interaction Testing
When testing interaction events, use the correct method signatures:

#### EventInteractionStart Method
```csharp
// Correct usage - pass InteractionType in metadata
var interactionMeta = new Dictionary<string, string> { 
    ["interaction_type"] = Abxr.InteractionType.Select.ToString() 
};
Abxr.EventInteractionStart(interactionName, interactionMeta);

// WRONG - don't pass InteractionType directly as second parameter
// Abxr.EventInteractionStart(interactionName, type); // This will cause compilation errors
```

#### Valid InteractionTypes
- `Null`, `Bool`, `Select`, `Text`, `Rating`, `Number`, `Matching`, `Performance`, `Sequencing`
- **Note:** `DragAndDrop` is not a valid InteractionType (use `Matching` instead)

### Test Types
- **Unit Tests**: Test specific API functionality with `TestDataCapture` and meaningful assertions
- **Integration Tests**: Test end-to-end behavior with `new WaitForSeconds()` and exception-free verification
- **Use unit tests** for testing specific API behavior (e.g., metadata registration)
- **Use integration tests** for testing complete workflows and server communication

### Test Isolation
- Each test should be independent and not rely on state from other tests
- Use randomized names to prevent test interference
- Clean up test state in `[TearDown]` methods
- Reset shared authentication state between test runs

## Platform Limitations

### Unity Editor Testing Limitations

Some ABXRLib features depend on platform-specific native SDK calls that are not available when running tests in the Unity Editor:

#### **ArborServiceClient Methods**
The following methods require Android platform-specific native SDK calls and will not work in Unity Editor tests:

- `Abxr.GetDeviceId()` - Returns device UUID from ArborXR
- `Abxr.GetDeviceSerial()` - Returns device serial number
- `Abxr.GetDeviceTitle()` - Returns device title from ArborXR portal
- `Abxr.GetDeviceTags()` - Returns device tags
- `Abxr.GetOrgId()` - Returns organization UUID (fallback to config available)
- `Abxr.GetOrgTitle()` - Returns organization title
- `Abxr.GetOrgSlug()` - Returns organization slug
- `Abxr.GetMacAddressFixed()` - Returns fixed MAC address
- `Abxr.GetMacAddressRandom()` - Returns randomized MAC address
- `Abxr.GetIsAuthenticated()` - Returns SSO authentication status
- `Abxr.GetAccessToken()` - Returns SSO access token
- `Abxr.GetRefreshToken()` - Returns SSO refresh token
- `Abxr.GetExpiresDateUtc()` - Returns token expiration
- `Abxr.GetFingerprint()` - Returns device fingerprint

#### **Testing Strategy**
- **Unity Editor Tests**: Focus on core functionality that doesn't require native SDK calls
- **Android Device Tests**: Test platform-specific features on actual Android devices
- **Real Server Testing**: All tests authenticate against actual servers to verify end-to-end functionality

#### **Affected Test Categories**
- Session Management Tests (removed from Unity Editor test suite)
- Device Information Tests (should be tested on Android devices)
- SSO Authentication Tests (should be tested on Android devices)

### Troubleshooting

#### **Configuration Issues**
- Ensure `Assets/Resources/AbxrLib.asset` exists and is properly configured
- Check that all required fields (appID, orgID, authSecret, restUrl) are set
- Verify the configuration is valid using `Configuration.Instance.IsValid()`

#### **Authentication Timeout**
- Check server connectivity
- Verify credentials in `AbxrLib.asset` are correct
- Ensure server is accessible from your network

#### **Network Errors**
- Ensure stable internet connection
- Check firewall settings
- Verify server URL in configuration is accessible

## Test Configuration

### Real Server Testing

The test suite uses real server authentication to ensure both client and server work together properly. All tests authenticate against actual servers using the configuration from the demo app.

#### TestDataCapture
```csharp
// Capture events for verification
var capture = new TestDataCapture();

// Verify events were captured
Assert.IsTrue(capture.WasEventCaptured("event_name"));
Assert.IsTrue(capture.WasEventCaptured("event_name", expectedMetadata));

// Get captured data
var event = capture.GetLastEvent("event_name");
var logs = capture.GetLogs("Info");
var telemetry = capture.GetTelemetry("telemetry_name");
```

### Test Helpers

#### TestHelpers
```csharp
// Setup and cleanup
TestHelpers.SetupTestEnvironment();
TestHelpers.CleanupTestEnvironment();

// Create test metadata
var metadata = TestHelpers.CreateTestMetadata(
    ("key1", "value1"),
    ("key2", "value2")
);

// Assertions
TestHelpers.AssertMetadataContains(actual, expected);
TestHelpers.AssertVector3Approximately(actual, expected);
TestHelpers.AssertDurationApproximately(actual, expected);

// Wait for conditions
yield return TestHelpers.WaitForEvent(capture, "event_name");
yield return TestHelpers.WaitForEventCount(capture, 5);
```

#### AuthenticationTestHelper
```csharp
// Wait for authentication to complete
yield return AuthenticationTestHelper.WaitForAuthenticationToComplete();

// Assertions
AuthenticationTestHelper.AssertAuthenticationSuccessful();
AuthenticationTestHelper.AssertConfigurationValid();

// Get status for debugging
string status = AuthenticationTestHelper.GetAuthenticationStatus();
```

#### CoroutineTestHelper
```csharp
// Wait for coroutines
yield return CoroutineTestHelper.WaitForCoroutine(coroutine);
yield return CoroutineTestHelper.WaitForCondition(() => condition);
yield return CoroutineTestHelper.WaitForFrames(5);

// Wait for specific events
yield return CoroutineTestHelper.WaitForAuthentication();
yield return CoroutineTestHelper.WaitForModuleTarget("module_name");
```

## Test Naming Convention

Tests follow the pattern: `Test_MethodName_Scenario_ExpectedResult`

Examples:
- `Test_Event_BasicEvent_SendsEventSuccessfully`
- `Test_EventAssessmentComplete_WithValidScore_CompletesAssessment`
- `Test_StartTimedEvent_WithDuration_CalculatesDuration`

## Writing New Tests

### Basic Test Structure
```csharp
[UnityTest]
public IEnumerator Test_MethodName_Scenario_ExpectedResult()
{
    // Arrange
    string eventName = "test_event";
    var metadata = TestHelpers.CreateTestMetadata(("key", "value"));
    
    // Act
    Abxr.Event(eventName, metadata);
    
    // Wait for processing
    yield return TestHelpers.WaitForEvent(_dataCapture, eventName);
    
    // Assert
    TestHelpers.AssertEventCaptured(_dataCapture, eventName, metadata);
}
```

### Setup and Teardown
```csharp
[SetUp]
public void Setup()
{
    TestHelpers.SetupTestEnvironment();
    _dataCapture = new TestDataCapture();
}

[TearDown]
public void TearDown()
{
    TestHelpers.CleanupTestEnvironment();
    _dataCapture?.Clear();
}
```

### Testing Coroutines
```csharp
[UnityTest]
public IEnumerator Test_CoroutineMethod_Scenario_ExpectedResult()
{
    // Arrange
    TestHelpers.SetupTestEnvironmentWithExistingConfig();
    
    // Act
    yield return AuthenticationTestHelper.WaitForAuthenticationToComplete();
    
    // Assert
    AuthenticationTestHelper.AssertAuthenticationSuccessful();
}
```

## Debugging Tests

### Test Data Capture
Use `TestDataCapture.PrintSummary()` to see what data was captured:
```csharp
_dataCapture.PrintSummary();
```

### Debug Logging
Tests include debug logging to help understand what's happening:
```csharp
Debug.Log("Test: Starting authentication simulation");
```

### Test Isolation
Each test is isolated and cleans up after itself:
- Super metadata is reset
- Timers are cleared
- Captured data is cleared

## CI/CD Integration

The test suite is designed to work with CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Unity Tests
  run: |
    Unity -batchmode -quit -projectPath . -runTests -testPlatform playmode -testResults test-results.xml
```

## Troubleshooting

### Common Issues

1. **Tests not appearing in Test Runner**
   - Ensure assembly definition files are properly configured
   - Check that test files are in the correct directories
   - Verify Unity Test Framework package is installed

2. **Tests failing with timeout**
   - Increase timeout values in `WaitForCondition` calls
   - Check that server is accessible and responding
   - Ensure test data capture is working correctly

3. **Authentication tests failing**
   - Verify server credentials in AbxrLib.asset are correct
   - Check that server is accessible from your network
   - Ensure configuration is valid for real server testing

### Getting Help

- Check the test debug logs for detailed information
- Use `TestDataCapture.PrintSummary()` to see captured data
- Review the test helpers for common patterns
- Refer to the main ABXRLib README for API documentation

## Contributing

When adding new tests:

1. Follow the existing naming convention
2. Use the provided test helpers for real server authentication
3. Include proper setup and teardown
4. Add debug logging for complex scenarios
5. Test both success and failure cases
6. Ensure tests work with real server authentication
6. Include edge cases and error conditions

## Future Enhancements

- **NUnit Tests**: Pure C# tests without Unity dependencies
- **Integration Tests**: Tests against real test backend
- **UI Automation**: Automated testing of UI components
- **Performance Benchmarks**: Automated performance testing
- **Code Coverage**: Integration with code coverage tools
