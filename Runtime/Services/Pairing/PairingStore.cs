using UnityEngine;

namespace AbxrLib.Runtime.Services.Pairing
{
    /// <summary>
    /// Where the pairing lives between launches. The token and the app instance id are written and removed together.
    /// The paired device's name rides along, and changes on its own when the device is renamed in the Portal.
    /// </summary>
    internal interface IPairingStore
    {
        /// <summary>False unless both values are stored.</summary>
        bool TryLoad(out string token, out string instanceId);

        /// <summary>The paired device's name, or null when the pairing has none.</summary>
        string LoadDeviceName();

        /// <summary>Writes the pairing. A null deviceName removes any stored name.</summary>
        void Save(string token, string instanceId, string deviceName);

        /// <summary>Replaces only the name. Null removes it.</summary>
        void SaveDeviceName(string deviceName);

        /// <summary>Removes the pairing, name included.</summary>
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
        internal const string DeviceNameKey = "abxrlib_paired_device_name";

        private readonly string _tokenKey;
        private readonly string _instanceIdKey;
        private readonly string _deviceNameKey;

        internal PlayerPrefsPairingStore() : this(TokenKey, InstanceIdKey, DeviceNameKey) { }

        /// <summary>Testing only: different keys, so a test run never touches a real pairing.</summary>
        internal PlayerPrefsPairingStore(string tokenKey, string instanceIdKey, string deviceNameKey)
        {
            _tokenKey = tokenKey;
            _instanceIdKey = instanceIdKey;
            _deviceNameKey = deviceNameKey;
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

        public string LoadDeviceName()
        {
            string deviceName = PlayerPrefs.GetString(_deviceNameKey, "");
            return deviceName.Length > 0 ? deviceName : null;
        }

        public void Save(string token, string instanceId, string deviceName)
        {
            PlayerPrefs.SetString(_tokenKey, token);
            PlayerPrefs.SetString(_instanceIdKey, instanceId);
            WriteDeviceName(deviceName);
            PlayerPrefs.Save();
        }

        public void SaveDeviceName(string deviceName)
        {
            WriteDeviceName(deviceName);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(_tokenKey);
            PlayerPrefs.DeleteKey(_instanceIdKey);
            PlayerPrefs.DeleteKey(_deviceNameKey);
            PlayerPrefs.Save();
        }

        private void WriteDeviceName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) PlayerPrefs.DeleteKey(_deviceNameKey);
            else PlayerPrefs.SetString(_deviceNameKey, deviceName);
        }
    }
}
