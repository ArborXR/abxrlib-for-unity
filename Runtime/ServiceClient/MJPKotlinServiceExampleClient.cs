#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace AbxrLib.Runtime.ServiceClient.MJPKotlinExample
{
	// This is the core mechanism for the ServiceWrapper below for calling bound methods in the service.
	public static class AndroidJavaObjectExt
	{
		public static T CallResult<T>(this AndroidJavaObject native, string methodName, params object[] args) =>
			native.Call<AndroidJavaObject>(methodName, args) is var result
			&& result.Call<bool>("isOk")
				? result.Call<T>("getValue")
				: throw new MjpSdkException(result.Call<string>("getError"));	// Not a specific bound method... part of the framework supplied by result type.
	}

	/// <summary>Allows interacting with the SDK service.</summary>
	/// <remarks>
	///   Only a single instance of this class should be used per app. The SDK is automatically initialized and shut
	///   down whenever the instance of this class is enabled/disabled (respectively).
	/// </remarks>
	public class MJPKotlinServiceExampleClient : MonoBehaviour
	{
		private const string PackageName = "com.example.mjpkotlinserviceexample";
		private AndroidJavaObject?				_mjpsdk;
		private MJPNativeConnectionCallback?	_nativeCallback;
		public static MjpSdkServiceWrapper?		MjpServiceWrapper;

		// Constructor logging
		public MJPKotlinServiceExampleClient()
		{
			Debug.Log("[MJPKotlinServiceExampleClient] Constructor called - MJPKotlinServiceExampleClient instance created");
		}

		private void Awake()
		{
			Debug.Log($"[MJPKotlinServiceExampleClient] Awake() called on GameObject: {gameObject.name}");
		}

		private void Start()
		{
			Debug.Log($"[MJPKotlinServiceExampleClient] Start() called on GameObject: {gameObject.name}");
		}

		// Whenever we delay via Task.Delay, there is no guarantee that our current thread would be already attached to Android JNI,
		// so we must reattached the current thread to AndroidJNI right after Task.Delay to ensure we don't run into threading issues.
		private static Task DelayAndReattachThreadToJNI(int delay) => Task.Delay(delay).ContinueWith(_ => AndroidJNI.AttachCurrentThread());

		private AndroidJavaObject Sdk
		{
			get
			{
				if (_mjpsdk is null)
				{
					throw new InvalidOperationException("This MonoBehaviour is not enabled.");
				}

				return _mjpsdk;
			}
		}

		public static bool IsConnected() 
		{
			bool isConnected = MjpServiceWrapper != null;
			Debug.Log($"[MJPKotlinServiceExampleClient] IsConnected() = {isConnected}");
			return isConnected;
		}

		public static MJPKotlinServiceExampleClient? FindInstance()
		{
			var instance = FindObjectOfType<MJPKotlinServiceExampleClient>();
			Debug.Log($"[MJPKotlinServiceExampleClient] FindInstance() - found instance: {(instance != null ? "YES" : "NO")}");
			if (instance != null)
			{
				Debug.Log($"[MJPKotlinServiceExampleClient] Instance found on GameObject: {instance.gameObject.name}, enabled: {instance.enabled}");
			}
			return instance;
		}

		private void Connect()
		{
			Debug.Log("[MJPKotlinServiceExampleClient] Attempting to connect to service");
			try
			{
				using var unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
				_nativeCallback = new MJPNativeConnectionCallback(this);
				Debug.Log("[MJPKotlinServiceExampleClient] Calling Sdk.connect() method");
				Sdk.Call("connect", currentActivity, _nativeCallback);
				Debug.Log("[MJPKotlinServiceExampleClient] Sdk.connect() method called successfully");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[MJPKotlinServiceExampleClient] Error in Connect(): {ex.Message}");
				Debug.LogError($"[MJPKotlinServiceExampleClient] Stack trace: {ex.StackTrace}");
			}
		}

		protected void OnDisable()
		{
			Debug.Log("[MJPKotlinServiceExampleClient] OnDisable() called - cleaning up");
			_mjpsdk?.Dispose();
			_mjpsdk = null;
			MjpServiceWrapper = null;
		}

		protected void OnEnable()
		{
			Debug.Log($"[MJPKotlinServiceExampleClient] OnEnable() called - attempting to create SDK for package: {PackageName}");
			try
			{
				// Instantiates our `Sdk.java`.
				_mjpsdk = new AndroidJavaObject($"{PackageName}.Sdk");
				Debug.Log("[MJPKotlinServiceExampleClient] SDK object created successfully");
				Connect();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[MJPKotlinServiceExampleClient] Error in OnEnable(): {ex.Message}");
				Debug.LogError($"[MJPKotlinServiceExampleClient] Stack trace: {ex.StackTrace}");
			}
		}

		public sealed class MjpSdkServiceWrapper
		{
			private readonly AndroidJavaObject _native;	// MJP:  Somehow this knows it is an AndroidJavaObjectExt despite AndroidJavaObjectExt not inheriting from AndroidObject.

			public MjpSdkServiceWrapper(AndroidJavaObject native) => _native = native;

			public void BasicTypes(int anInt, long aLong, bool aBoolean, float aFloat, double aDouble, string aString) => _native.CallResult<string>("basicTypes");
			public void PlaySampleOnLoop() => _native.CallResult<string>("playSampleOnLoop");

			public void StopPlayback() => _native.CallResult<string>("stopPlayback");
			public string WhatTimeIsIt() 
			{
				Debug.Log("[MJPKotlinServiceExampleClient] WhatTimeIsIt() called");
				try
				{
					var result = _native.CallResult<string>("whatTimeIsIt");
					Debug.Log($"[MJPKotlinServiceExampleClient] WhatTimeIsIt() returned: {result}");
					return result;
				}
				catch (Exception ex)
				{
					Debug.LogError($"[MJPKotlinServiceExampleClient] Error in WhatTimeIsIt(): {ex.Message}");
					Debug.LogError($"[MJPKotlinServiceExampleClient] Stack trace: {ex.StackTrace}");
					return "";
				}
			}
			public bool GetIsInitialized()
			{
				//var value = _native.CallResult<string>("getIsInitialized");
				//return !string.IsNullOrWhiteSpace(value) && Convert.ToBoolean(value);
				return true;
			}
		}

		private async Task NotifyWhenInitializedAsync(AndroidJavaObject? nativeObj)
		{
			Debug.Log("[MJPKotlinServiceExampleClient] NotifyWhenInitializedAsync started");
			// If the application gets loaded before the XRDM client, the XRDM client may not have time to be initialized.
			// To avoid this timing issue, we should wait until XRDM client is initialized to fire the event of OnConnected.
			var delay = 500;
			var delayMultiplier = 1.5f;
			var maximumAttempts = 7;

#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CS8604 // Possible null reference argument.
			// nativeObj shouldn't be null, and if it is null, something really bad must have happened already.
			if (nativeObj == null)
			{
				Debug.LogError("[MJPKotlinServiceExampleClient] nativeObj is null in NotifyWhenInitializedAsync!");
				return;
			}
			var serviceWrapper = new MjpSdkServiceWrapper(nativeObj);
			Debug.Log("[MJPKotlinServiceExampleClient] Service wrapper created, checking initialization...");
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CA2000 // Dispose objects before losing scope
			try
			{
				for (var attempt = 0; attempt < maximumAttempts; attempt++)
				{
					Debug.Log($"[MJPKotlinServiceExampleClient] Initialization attempt {attempt + 1}/{maximumAttempts}");
					if (serviceWrapper.GetIsInitialized())
					{
						Debug.Log("[MJPKotlinServiceExampleClient] Service is initialized! Setting MjpServiceWrapper.");
						MjpServiceWrapper = serviceWrapper;
						return;
					}
					Debug.Log($"[MJPKotlinServiceExampleClient] Service not yet initialized, waiting {delay}ms...");
					await DelayAndReattachThreadToJNI(delay);
					_ = AndroidJNI.AttachCurrentThread();
					delay = (int)Math.Floor(delay * delayMultiplier);
				}
				Debug.LogWarning("[MJPKotlinServiceExampleClient] Maximum initialization attempts reached, service may not be ready");
#pragma warning disable CA1031
			}
			catch (Exception ex)
			{
				Debug.LogError($"[MJPKotlinServiceExampleClient] Exception in NotifyWhenInitializedAsync: {ex.Message}");
				Debug.LogError($"[MJPKotlinServiceExampleClient] Stack trace: {ex.StackTrace}");
				await DelayAndReattachThreadToJNI(delay);
				_ = AndroidJNI.AttachCurrentThread();
				Debug.Log("[MJPKotlinServiceExampleClient] Setting MjpServiceWrapper despite exception (fallback)");
				MjpServiceWrapper = serviceWrapper;
			}
#pragma warning restore CA1031
		}

		private sealed class MJPNativeConnectionCallback : AndroidJavaProxy
		{
			private readonly MJPKotlinServiceExampleClient _sdkBehavior;

			public MJPNativeConnectionCallback(MJPKotlinServiceExampleClient sdkBehavior) : base(PackageName + ".IConnectionCallback")
			{
				_sdkBehavior = sdkBehavior;
			}

			// Invoke the method ourselves, as the base does an expensive lookup via reflection:
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/AndroidJNI/AndroidJava.cs#L124-L139
			public override AndroidJavaObject? Invoke(string methodName, AndroidJavaObject[] javaArgs)
			{
				Debug.Log($"[MJPKotlinServiceExampleClient] Connection callback invoked: {methodName}");
				if (methodName == "onConnected")
				{
					Debug.Log("[MJPKotlinServiceExampleClient] onConnected callback triggered - starting initialization");
					_ = _sdkBehavior.NotifyWhenInitializedAsync(javaArgs[0]);
					// `onConnected` is a `void` method.
					return null;
				}

				return base.Invoke(methodName, javaArgs);
			}
		}
	}

	public class MjpSdkException : Exception
	{
		public MjpSdkException(string message) : base(message)
		{
		}
	}
}
