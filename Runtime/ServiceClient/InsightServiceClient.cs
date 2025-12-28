using UnityEngine;

namespace AbxrLib.Runtime.ServiceClient
{
    public static class InsightServiceClient
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _bridge;

        private static AndroidJavaObject Bridge
        {
            get
            {
                if (_bridge != null) return _bridge;
                using var cls = new AndroidJavaClass("com.arborxr.insightbridge.InsightBridge");
                _bridge = cls.CallStatic<AndroidJavaObject>("getInstance");
                return _bridge;
            }
        }

        private static AndroidJavaObject Activity
        {
            get
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        public static bool IsProviderAvailable()
        {
            try
            {
                return Bridge.Call<bool>("isProviderAvailable", Activity);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogError("AbxrLib: InsightBridge IsProviderAvailable AndroidJavaException:\n" + e);
                return false;
            }
        }

        public static bool Configure(
            string uploadUrl, string appId, string orgId, string authSecret, string deviceId,
            string sessionId, string authMechanism = null, string authToken = null, string responseSecret = null)
        {
            try
            {
                return Bridge.Call<bool>("configure", Activity,
                    uploadUrl, appId, orgId, authSecret, deviceId, sessionId,
                    authMechanism, authToken, responseSecret
                );
            }
            catch (AndroidJavaException e)
            {
                Debug.LogError("AbxrLib: InsightBridge Configure AndroidJavaException:\n" + e);
                return false;
            }
        }

        public static bool SendBatch(string payloadJson)
        {
            try
            {
                return Bridge.Call<bool>("enqueueBatch", Activity, payloadJson);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogError("AbxrLib: InsightBridge EnqueueBatch AndroidJavaException:\n" + e);
                return false;
            }
        }
#else
        public static bool IsProviderAvailable() => false;
        public static bool Configure(string uploadUrl, string appId, string orgId, string authSecret, string deviceId,
            string sessionId, string authMechanism = null, string authToken = null, string responseSecret = null) => false;
        public static bool SendBatch(string payloadJson) => false;
#endif
    }
}
