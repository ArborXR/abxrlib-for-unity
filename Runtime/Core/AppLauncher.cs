using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Launches another AbxrLib app and delivers the current auth session to it.
    ///
    /// Per-platform behavior:
    ///   Android/VR: Uses request.AndroidPackageName, and request.AndroidActivityClassName if set.
    ///               When the Activity class is provided, an explicit-component Intent is used —
    ///               this avoids needing a queries entry in this app's manifest, because
    ///               startActivity() with an explicit component does not require package visibility
    ///               (only PackageManager queries do). When only the package name is provided,
    ///               getLaunchIntentForPackage is used as a fallback, which does require queries on Android 11+.
    ///   WebGL: Uses request.Url. Handoff is base64-encoded and added as the "auth_handoff" entry in the URL fragment.
    ///          Navigates same-tab via JS so popup blockers do not interfere with calls made outside a user gesture.
    ///   Standalone: Uses request.Url. Same query-parameter handoff as WebGL, opened via Application.OpenURL.
    ///               Return-to-launcher is not supported on Standalone.
    /// </summary>
    internal static class AppLauncher
    {
        private const string AuthHandoffName = "auth_handoff";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AbxrNavigateTo(string url);
#endif

        internal static bool LaunchAppWithAuthHandoff(Abxr.AppLaunchRequest request, string handoffJson)
        {
#if UNITY_EDITOR
            Logcat.Warning("LaunchApp is not supported in the Unity Editor");
            return false;
#elif UNITY_ANDROID
            return LaunchForAndroid(request, handoffJson);
#elif UNITY_WEBGL
            return LaunchForWebGL(request, handoffJson);
#else
            return LaunchForStandalone(request, handoffJson);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool LaunchForAndroid(Abxr.AppLaunchRequest request, string handoffJson)
        {
            if (string.IsNullOrEmpty(request.AndroidPackageName))
            {
                Logcat.Warning("Android launch requires AndroidPackageName");
                return false;
            }

            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null)
                {
                    Logcat.Warning("LaunchAppWithAuthHandoff failed: Unity currentActivity is null");
                    return false;
                }

                using var intentClass = new AndroidJavaClass("android.content.Intent");
                int flagNewTask = intentClass.GetStatic<int>("FLAG_ACTIVITY_NEW_TASK");

                // Preferred path: explicit-component Intent. Does NOT require <queries> in this app's manifest because
                // startActivity() with an explicit component is not gated by package visibility (only PackageManager queries are).
                if (!string.IsNullOrEmpty(request.AndroidActivityClassName))
                {
                    return LaunchExplicitActivity(activity, request.AndroidPackageName,
                        request.AndroidActivityClassName, handoffJson, flagNewTask);
                }

                // Fallback path: package-only. Requires <queries> on Android 11+ because
                // getLaunchIntentForPackage is a PackageManager query.
                return LaunchByPackage(activity, request.AndroidPackageName, handoffJson, flagNewTask);
            }
            catch (AndroidJavaException e)
            {
                Logcat.Warning($"Android launch failed for '{request.AndroidPackageName}': {e.Message}");
                return false;
            }
        }

        private static bool LaunchExplicitActivity(AndroidJavaObject activity,
            string packageName, string activityClassName, string handoffJson, int flagNewTask)
        {
            try
            {
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.MAIN");
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.LAUNCHER");
                intent.Call<AndroidJavaObject>("setClassName", packageName, activityClassName);
                intent.Call<AndroidJavaObject>("putExtra", AuthHandoffName, handoffJson);
                intent.Call<AndroidJavaObject>("addFlags", flagNewTask);
                activity.Call("startActivity", intent);
                return true;
            }
            catch (AndroidJavaException e)
            {
                Logcat.Warning($"Explicit Activity launch failed for '{packageName}/{activityClassName}': {e.Message}");
                return false;
            }
        }

        private static bool LaunchByPackage(AndroidJavaObject activity, string packageName, string handoffJson, int flagNewTask)
        {
            using var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
            using var launchIntent = packageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", packageName);

            if (launchIntent == null)
            {
                using var buildVersion = new AndroidJavaClass("android.os.Build$VERSION");
                int sdkInt = buildVersion.GetStatic<int>("SDK_INT");

                string message = $"Could not get launch intent for package '{packageName}'. " +
                    "The app may not be installed, may have no launcher Activity, or may not be visible to this app.";
                if (sdkInt >= 30)
                {
                    message += " On Android 11+ (API 30+), either declare the target package in this app's AndroidManifest " +
                        $"(<queries><package android:name=\"{packageName}\" /></queries>), or set AndroidActivityClassName " +
                        "on the launch request to use an explicit-component launch that bypasses package visibility.";
                }

                Logcat.Warning(message);
                return false;
            }

            launchIntent.Call<AndroidJavaObject>("putExtra", AuthHandoffName, handoffJson);
            launchIntent.Call<AndroidJavaObject>("addFlags", flagNewTask);
            activity.Call("startActivity", launchIntent);
            return true;
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        private static bool LaunchForWebGL(Abxr.AppLaunchRequest request, string handoffJson)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                Logcat.Warning("WebGL launch requires URL");
                return false;
            }

            string finalUrl = AppendHandoffToFragment(request.Url, handoffJson);
            try
            {
                AbxrNavigateTo(finalUrl);
                return true;
            }
            catch (Exception e)
            {
                Logcat.Warning($"WebGL navigation failed: {e.Message}");
                return false;
            }
        }
#endif

#if !UNITY_ANDROID && !UNITY_WEBGL && !UNITY_EDITOR
        private static bool LaunchForStandalone(Abxr.AppLaunchRequest request, string handoffJson)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                Logcat.Warning("Standalone launch requires URL");
                return false;
            }

            string finalUrl = AppendHandoffToFragment(request.Url, handoffJson);
            Application.OpenURL(finalUrl);
            return true;
        }
#endif
        
        // Appends or replaces the "auth_handoff" entry in the URL fragment.
        // The handoff JSON is base64-encoded so it survives URL parsing intact regardless of the JSON's contents.
        // Fragment is used instead of query string so the credential does not reach the server
        // (browsers strip the fragment before the HTTP request) and does not appear in referer
        // headers from any outbound links on the assessment page. The receiving SDK strips the
        // fragment from the address bar on read so it doesn't linger in the URL or browser history.
        private static string AppendHandoffToFragment(string url, string handoffJson)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;

            string encodedValue = Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(handoffJson)));
            string encodedKey = Uri.EscapeDataString(AuthHandoffName);
            string replacement = encodedKey + "=" + encodedValue;

            int fragmentIndex = url.IndexOf('#');
            string beforeFragment = fragmentIndex >= 0 ? url.Substring(0, fragmentIndex) : url;
            string fragment = fragmentIndex >= 0 ? url.Substring(fragmentIndex + 1) : string.Empty;

            if (string.IsNullOrEmpty(fragment))
            {
                return beforeFragment + "#" + replacement;
            }

            // Replace any existing auth_handoff entry; otherwise append.
            string[] parts = fragment.Split('&');
            var updated = new List<string>(parts.Length + 1);
            bool replaced = false;
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                int equalsIndex = part.IndexOf('=');
                string partKey = equalsIndex >= 0 ? part.Substring(0, equalsIndex) : part;

                if (Uri.UnescapeDataString(partKey) == AuthHandoffName)
                {
                    if (!replaced)
                    {
                        updated.Add(replacement);
                        replaced = true;
                    }
                }
                else
                {
                    updated.Add(part);
                }
            }

            if (!replaced)
            {
                updated.Add(replacement);
            }

            return beforeFragment + "#" + string.Join("&", updated.ToArray());
        }
    }
}
