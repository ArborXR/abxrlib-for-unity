#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Platform
{
    public static class AndroidJavaObjectExt
    {
        public static T CallResult<T>(this AndroidJavaObject native, string methodName, params object[] args)
        {
            try
            {
                var result = native.Call<AndroidJavaObject>(methodName, args);
                if (result != null && result.Call<bool>("isOk"))
                {
                    return result.Call<T>("getValue");
                }
                else
                {
                    var error = result?.Call<string>("getError") ?? "Unknown SDK error";
                    Debug.LogWarning($"[AbxrLib] SDK call {methodName} failed: {error}");
                    return default(T)!;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbxrLib] SDK call {methodName} threw exception: {ex.Message}");
                return default(T)!;
            }
        }
    }

    /// <summary>Allows interacting with the SDK service.</summary>
    /// <remarks>
    ///   Only a single instance of this class should be used per app. The SDK is automatically initialized and shut
    ///   down whenever the instance of this class is enabled/disabled (respectively).
    /// </remarks>
    public class ArborServiceClient
    {
        private const string PackageName = "app.xrdm.sdk.external";
        private AndroidJavaObject? _sdk;
        private NativeConnectionCallback? _nativeCallback;
        internal SdkServiceWrapper? ServiceWrapper;

        // Whenever we delay via Task.Delay, there is no guarantee that our current thread would be already attached to Android JNI,
        // so we must reattached the current thread to AndroidJNI right after Task.Delay to ensure we don't run into threading issues.
        private static Task DelayAndReattachThreadToJNI(int delay) => Task.Delay(delay).ContinueWith(_ => AndroidJNI.AttachCurrentThread());

        private AndroidJavaObject? Sdk
        {
            get
            {
                if (_sdk is null)
                {
                    Debug.LogWarning("[AbxrLib] ArborServiceClient SDK is not initialized. This MonoBehaviour may not be enabled.");
                }

                return _sdk;
            }
        }

        public bool IsConnected() => ServiceWrapper != null;

        private void Connect()
        {
            using var unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
            _nativeCallback = new NativeConnectionCallback(this);
            Sdk?.Call("connect", currentActivity, _nativeCallback);
        }
        
        public void Initialize()
        {
            _sdk = new AndroidJavaObject($"{PackageName}.Sdk");
            Connect();
        }

        public void Shutdown()
        {
            ServiceWrapper = null;
            _sdk?.Dispose();
            _sdk = null;
        }

        public sealed class SdkServiceWrapper
        {
            private readonly AndroidJavaObject _native;

            public SdkServiceWrapper(AndroidJavaObject native) => _native = native;

            public string GetDeviceId() => _native.CallResult<string>("getDeviceId");

            public string GetDeviceSerial() => _native.CallResult<string>("getDeviceSerial");

            public string GetDeviceTitle() => _native.CallResult<string>("getDeviceTitle");

            public string[] GetDeviceTags()
            {
                var javaObj = _native.CallResult<AndroidJavaObject>("getDeviceTags");
                return AndroidJNIHelper.ConvertFromJNIArray<string[]>(javaObj.GetRawObject());
            }

            public string GetOrgId() => _native.CallResult<string>("getOrgId");

            public string GetOrgTitle() => _native.CallResult<string>("getOrgTitle");

            public string GetOrgSlug() => _native.CallResult<string>("getOrgSlug");

            public string GetMacAddressFixed() => _native.CallResult<string>("getMacAddressFixed");

            public string GetMacAddressRandom() => _native.CallResult<string>("getMacAddressRandom");

            public bool GetIsAuthenticated()
            {
                var value = _native.CallResult<string>("getIsAuthenticated");
                return !string.IsNullOrWhiteSpace(value) && Convert.ToBoolean(value);
            }

            public string GetAccessToken() => _native.CallResult<string>("getAccessToken");

            public string GetRefreshToken() => _native.CallResult<string>("getRefreshToken");

            public DateTime? GetExpiresDateUtc()
            {
                var value = _native.CallResult<string>("getExpiresDateUtc");
                return string.IsNullOrWhiteSpace(value) ? null : Convert.ToDateTime(value);
            }

            public bool GetIsInitialized()
            {
                var value = _native.CallResult<string>("getIsInitialized");
                return !string.IsNullOrWhiteSpace(value) && Convert.ToBoolean(value);
            }

            public string GetFingerprint() => _native.CallResult<string>("getFingerprint");
        }

        private async Task NotifyWhenInitializedAsync(AndroidJavaObject? nativeObj)
        {
            // If the application gets loaded before the XRDM client, the XRDM client may not have time to be initialized.
            // To avoid this timing issue, we should wait until XRDM client is initialized to fire the event of OnConnected.
            var delay = 500;
            var delayMultiplier = 1.5f;
            var maximumAttempts = 7;

#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CS8604 // Possible null reference argument.
            // nativeObj shouldn't be null, and if it is null, something really bad must have happened already.
            var serviceWrapper = new SdkServiceWrapper(nativeObj);
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CA2000 // Dispose objects before losing scope
            try
            {
                for (var attempt = 0; attempt < maximumAttempts; attempt++)
                {
                    if (serviceWrapper.GetIsInitialized())
                    {
                        ServiceWrapper = serviceWrapper;
                        return;
                    }
                    await DelayAndReattachThreadToJNI(delay);
                    _ = AndroidJNI.AttachCurrentThread();
                    delay = (int)Math.Floor(delay * delayMultiplier);
                }
#pragma warning disable CA1031

            }
            catch
            {
                await DelayAndReattachThreadToJNI(delay);
                _ = AndroidJNI.AttachCurrentThread();
                ServiceWrapper = serviceWrapper;
            }
#pragma warning restore CA1031
        }

        private sealed class NativeConnectionCallback : AndroidJavaProxy
        {
            private readonly ArborServiceClient _sdkBehavior;

            public NativeConnectionCallback(ArborServiceClient sdkBehavior) : base(PackageName + ".IConnectionCallback")
            {
                _sdkBehavior = sdkBehavior;
            }

            // Invoke the method ourselves, as the base does an expensive lookup via reflection:
            // https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/AndroidJNI/AndroidJava.cs#L124-L139
            public override AndroidJavaObject? Invoke(string methodName, AndroidJavaObject[] javaArgs)
            {
                if (methodName == "onConnected")
                {
                    _ = _sdkBehavior.NotifyWhenInitializedAsync(javaArgs[0]);
                    // `onConnected` is a `void` method.
                    return null;
                }

                return base.Invoke(methodName, javaArgs);
            }
        }
    }

    public class SdkException : Exception
    {
        public SdkException(string message) : base(message)
        {
        }
    }
}