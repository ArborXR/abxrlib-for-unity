using UnityEngine;

namespace AbxrLib.Runtime.Services.Pairing
{
    /// <summary>Where the pairing lives between launches. The token and the app instance id are written and removed together.</summary>
    internal interface IPairingStore
    {
        /// <summary>False unless both values are stored.</summary>
        bool TryLoad(out string token, out string instanceId);
        void Save(string token, string instanceId);
        void Clear();
    }

    /// <summary>
    /// PlayerPrefs, like abxrlib_device_id: app-private prefs on Android, which uninstall wipes, and IndexedDB for the
    /// page origin on WebGL. Every write ends in PlayerPrefs.Save(), because on WebGL nothing is durable until then and
    /// a closed tab never runs the quit hook.
    /// </summary>
    internal sealed class PlayerPrefsPairingStore : IPairingStore
    {
        internal const string TokenKey = "abxrlib_app_instance_token";
        internal const string InstanceIdKey = "abxrlib_app_instance_id";

        private readonly string _tokenKey;
        private readonly string _instanceIdKey;

        internal PlayerPrefsPairingStore() : this(TokenKey, InstanceIdKey) { }

        /// <summary>Testing only: different keys, so a test run never touches a real pairing.</summary>
        internal PlayerPrefsPairingStore(string tokenKey, string instanceIdKey)
        {
            _tokenKey = tokenKey;
            _instanceIdKey = instanceIdKey;
        }

        public bool TryLoad(out string token, out string instanceId)
        {
            token = PlayerPrefs.GetString(_tokenKey, "");
            instanceId = PlayerPrefs.GetString(_instanceIdKey, "");
            if (token.Length > 0 && instanceId.Length > 0) return true;

            token = null;
            instanceId = null;
            return false;
        }

        public void Save(string token, string instanceId)
        {
            PlayerPrefs.SetString(_tokenKey, token);
            PlayerPrefs.SetString(_instanceIdKey, instanceId);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(_tokenKey);
            PlayerPrefs.DeleteKey(_instanceIdKey);
            PlayerPrefs.Save();
        }
    }
}
