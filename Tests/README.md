# ABXRLib Unity Test Suite

This directory contains comprehensive tests for the ABXRLib Unity package using Unity Test Framework (UTF). The test suite validates all major features documented in the README and ensures the library works correctly across different scenarios.

## Test Structure

```
Tests/
├── Runtime/                    # PlayMode tests (run in Unity)
│   ├── AbxrLib.Tests.Runtime.asmdef
│   ├── TestDoubles/            # Mock objects and test doubles
│   │   ├── MockAuthenticationProvider.cs
│   │   ├── MockNetworkProvider.cs
│   │   ├── MockConfiguration.cs
│   │   └── TestDataCapture.cs
│   ├── Utilities/              # Test helper utilities
│   │   ├── TestHelpers.cs
│   │   ├── AuthenticationTestHelper.cs
│   │   └── CoroutineTestHelper.cs
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
- **Test Doubles**: Mock providers for authentication, network, and configuration
- **Test Utilities**: Helper functions for common test operations

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

### Mock Providers

The test suite uses configurable mock providers to simulate different scenarios:

#### MockAuthenticationProvider
```csharp
// Set up successful authentication
var mockAuth = AuthenticationTestHelper.SetupSuccessfulAuth();

// Set up failed authentication
var mockAuth = AuthenticationTestHelper.SetupFailedAuth("Invalid credentials");

// Set up keyboard authentication
var mockAuth = AuthenticationTestHelper.SetupKeyboardAuth();

// Set up SSO authentication
var mockAuth = AuthenticationTestHelper.SetupSSOAuth();
```

#### MockNetworkProvider
```csharp
// Set up successful network responses
mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.Success;

// Set up network errors
mockNetwork.CurrentScenario = MockNetworkProvider.NetworkScenario.ConnectionError;

// Set up authentication response
mockNetwork.SetAuthResponse("token", "secret", DateTime.UtcNow.AddHours(1));
```

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
// Setup different auth scenarios
var mockAuth = AuthenticationTestHelper.SetupSuccessfulAuth();
var mockAuth = AuthenticationTestHelper.SetupFailedAuth();
var mockAuth = AuthenticationTestHelper.SetupKeyboardAuth();
var mockAuth = AuthenticationTestHelper.SetupSSOAuth();

// Assertions
AuthenticationTestHelper.AssertAuthenticationSuccessful(mockAuth);
AuthenticationTestHelper.AssertAuthenticationFailed(mockAuth, "Expected error");
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
    var mockAuth = AuthenticationTestHelper.SetupSuccessfulAuth();
    
    // Act
    yield return AuthenticationTestHelper.SimulateAuthentication(mockAuth);
    
    // Assert
    AuthenticationTestHelper.AssertAuthenticationSuccessful(mockAuth);
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
- Mock providers are reset

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
   - Check that mock providers are properly configured
   - Ensure test data capture is working correctly

3. **Authentication tests failing**
   - Verify mock authentication provider is set up correctly
   - Check that authentication scenarios match expected behavior
   - Ensure test helpers are properly configured

### Getting Help

- Check the test debug logs for detailed information
- Use `TestDataCapture.PrintSummary()` to see captured data
- Review the test helpers for common patterns
- Refer to the main ABXRLib README for API documentation

## Contributing

When adding new tests:

1. Follow the existing naming convention
2. Use the provided test helpers and mock providers
3. Include proper setup and teardown
4. Add debug logging for complex scenarios
5. Test both success and failure cases
6. Include edge cases and error conditions

## Future Enhancements

- **NUnit Tests**: Pure C# tests without Unity dependencies
- **Integration Tests**: Tests against real test backend
- **UI Automation**: Automated testing of UI components
- **Performance Benchmarks**: Automated performance testing
- **Code Coverage**: Integration with code coverage tools
