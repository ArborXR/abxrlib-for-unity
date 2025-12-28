using UnityEngine;

namespace AbxrLib.Runtime.ServiceClient
{
    public static class DataCollectorClient
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string Authority = "com.arborxr.datasidecar.events";
        private static readonly string EventsUri = "content://" + Authority + "/events";
        private static readonly string ConfigUri   = "content://" + Authority + "/config";

        private static AndroidJavaObject GetContentResolver()
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            return activity.Call<AndroidJavaObject>("getContentResolver");
        }

        private static AndroidJavaObject ParseUri(string uri)
        {
            using var uriClass = new AndroidJavaClass("android.net.Uri");
            return uriClass.CallStatic<AndroidJavaObject>("parse", uri);
        }

        private static AndroidJavaObject NewContentValues()
        {
            return new AndroidJavaObject("android.content.ContentValues");
        }

        private static void PutString(AndroidJavaObject contentValues, string key, string value)
        {
            contentValues.Call("put", key, value);
        }

        /// <summary>
        /// Configures the collector with upload URL + app id + org id + auth secret + device ID + session ID.
        /// </summary>
        public static bool Configure(string uploadUrl, string appId, string orgId, string authSecret, string deviceId,
            string sessionId, string authToken = null, string responseSecret = null)
        {
            try
            {
                var resolver = GetContentResolver();
                var uri = ParseUri(ConfigUri);

                using var values = NewContentValues();
                PutString(values, "upload_url", uploadUrl);
                PutString(values, "app_id", appId);
                PutString(values, "org_id", orgId);
                PutString(values, "auth_secret", authSecret);
                PutString(values, "device_id", deviceId);
                PutString(values, "session_id", sessionId);

                if (!string.IsNullOrEmpty(authToken))
                {
                    PutString(values, "auth_token", authToken);
                    PutString(values, "response_secret", responseSecret);
                }

                int rows = resolver.Call<int>("update", uri, values, null, null);
                return rows > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enqueues one batch as a JSON string (DataPayloadWrapper).
        /// </summary>
        public static bool SendBatch(string payloadJson)
        {
            try
            {
                var resolver = GetContentResolver();
                var uri = ParseUri(EventsUri);

                using var values = NewContentValues();
                PutString(values, "payload", payloadJson);

                int rows = resolver.Call<int>("update", uri, values, null, null);
                return rows > 0;
            }
            catch
            {
                return false;
            }
        }
#else
        public static bool Configure(string uploadUrl, string appId, string orgId, string authSecret, string deviceId,
            string sessionId, string authToken = null, string responseSecret = null) => false;

        public static bool SendBatch(string payloadJson) => false;
#endif
    }
}
