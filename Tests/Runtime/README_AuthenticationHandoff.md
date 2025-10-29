# Authentication Handoff Testing

This document describes the authentication handoff testing system that simulates the real-world flow where a launcher app authenticates and then launches a target app with authentication data.

## Overview

The authentication handoff system allows:
1. A launcher app to authenticate and get an `AuthResponse` with a `PackageName`
2. The launcher app to launch a target app with the `AuthResponse` data as `auth_handoff` parameter
3. The target app to receive and process the handoff data as its own authentication
4. The target app to use all Abxr functionality (Log, Event, ModuleTarget, etc.)

## Test Structure

### AuthHandoffTestHelper
Located in `Tests/Runtime/Utilities/AuthHandoffTestHelper.cs`

This helper class simulates the launcher app behavior:

```csharp
// Simulate complete launcher app flow
string handoffJson = AuthHandoffTestHelper.SimulateCompleteLauncherFlow("com.arborxr.testapp");

// Create handoff data with specific parameters
string handoffJson = AuthHandoffTestHelper.SimulateLauncherAppHandoff(
    launcherAppId: "com.arborxr.launcher",
    targetPackageName: "com.arborxr.targetapp",
    userId: "user123",
    userData: customUserData,
    modules: customModules
);

// Simulate launcher app authentication to get real handoff data
yield return AuthHandoffTestHelper.SimulateLauncherAppHandoff();
```

### AuthenticationHandoffTests
Located in `Tests/Runtime/AuthenticationHandoffTests.cs`

Comprehensive test suite covering:
- Basic handoff flow
- Handoff with module data
- Invalid data handling
- Abxr functionality after handoff
- Edge cases and error scenarios
- Complete workflow simulation

## Key Test Methods

### Basic Handoff Flow
```csharp
[UnityTest]
public IEnumerator Test_AuthenticationHandoff_BasicFlow_CompletesSuccessfully()
{
    // Simulate launcher app creating handoff data
    string handoffJson = AuthHandoffTestHelper.SimulateCompleteLauncherFlow(TestPackageName);
    
    // Simulate target app receiving handoff and authenticating
    yield return Authentication.Authenticate();
    
    // Verify handoff was processed successfully
    Assert.IsTrue(Authentication.Authenticated());
    Assert.IsTrue(Authentication.FullyAuthenticated());
}
```

### Testing Abxr Functionality After Handoff
```csharp
[UnityTest]
public IEnumerator Test_AuthenticationHandoff_AbxrLog_WorksAfterHandoff()
{
    // Setup handoff
    string handoffJson = AuthHandoffTestHelper.SimulateCompleteLauncherFlow(TestPackageName);
    yield return Authentication.Authenticate();
    
    // Test Abxr functionality
    Abxr.Log("Test message after handoff");
    Abxr.Event("test_event", metadata);
    
    // Verify functionality works
    Assert.IsTrue(Authentication.Authenticated());
}
```

## Test Injection System

The system includes a test injection mechanism that allows injecting handoff data without requiring actual command line arguments or Android intents:

```csharp
// Inject handoff data for testing
Authentication.SetTestHandoffData(handoffJson);

// Clear injected data
Authentication.ClearTestHandoffData();
```

This is implemented in the `CheckAuthHandoff()` method in `Authentication.cs`:

```csharp
private static IEnumerator CheckAuthHandoff()
{
    // Check Android intent parameters first
    string handoffJson = Utils.GetAndroidIntentParam("auth_handoff");

    // If not found, check command line arguments
    if (string.IsNullOrEmpty(handoffJson))
    {
        handoffJson = Utils.GetCommandLineArg("auth_handoff");
    }
    
    // Check for test-injected handoff data (for unit testing)
    if (string.IsNullOrEmpty(handoffJson))
    {
        handoffJson = GetTestHandoffData();
    }
    
    if (!string.IsNullOrEmpty(handoffJson))
    {
        yield return ProcessAuthHandoff(handoffJson);
    }
}
```

## Real-World Flow Simulation

The tests simulate the exact flow from the real launcher app code:

1. **Launcher App Authentication**:
   ```csharp
   // Real launcher app code
   Abxr.OnAuthCompleted += OnAuthenticationCompleted;
   // ... authentication process ...
   
   private void OnAuthenticationCompleted(bool success, string error)
   {
       Authentication.AuthResponse authData = Authentication.GetAuthResponse();
       LaunchAppWithAuth.LaunchWithAuth(authData.PackageName, JsonConvert.SerializeObject(authData));
   }
   ```

2. **Target App Handoff Processing**:
   ```csharp
   // Target app receives auth_handoff parameter and processes it
   yield return Authentication.Authenticate(); // This calls CheckAuthHandoff()
   ```

3. **Target App Functionality**:
   ```csharp
   // Target app can now use all Abxr functionality
   Abxr.Log("Target app is working!");
   Abxr.Event("user_action", metadata);
   var moduleTarget = Abxr.GetModuleTarget();
   ```

## Running the Tests

The tests can be run using Unity's Test Runner or through the command line:

```bash
# Run all handoff tests
Unity -batchmode -quit -projectPath . -runTests -testResults results.xml -testFilter "AuthenticationHandoffTests"

# Run specific test
Unity -batchmode -quit -projectPath . -runTests -testResults results.xml -testFilter "Test_AuthenticationHandoff_BasicFlow_CompletesSuccessfully"
```

## Test Data Structure

The handoff data follows the `AuthResponse` structure:

```json
{
  "Token": "auth_token_here",
  "Secret": "api_secret_here", 
  "UserId": "user123",
  "AppId": "com.arborxr.launcher",
  "PackageName": "com.arborxr.targetapp",
  "UserData": {
    "id": "user123",
    "name": "Test User",
    "email": "test@example.com"
  },
  "Modules": [
    {
      "id": "module1",
      "name": "Test Module",
      "target": "test_target",
      "order": 1
    }
  ]
}
```

This matches exactly what the real launcher app serializes and passes to the target app.
