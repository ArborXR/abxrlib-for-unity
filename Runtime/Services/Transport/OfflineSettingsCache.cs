using System;
using System.Collections.Generic;
using System.IO;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;

namespace AbxrLib.Runtime.Services.Transport
{
    /// <summary>What the SDK remembers of an org's config for this app, so an offline launch behaves as the last online one did.</summary>
    [Serializable]
    internal sealed class CachedOrgSettings
    {
        /// <summary>The sign-in policy from the last config (offline RFC D7). Type, prompt and domain only.</summary>
        public AuthMechanism authMechanism;
        /// <summary>The org's automatic telemetry setting from the last config (offline RFC D4). Null when the config didn't say.</summary>
        public bool? automaticTelemetry;
    }

    /// <summary>
    /// The org settings an offline launch needs, per app, org and paired instance, in settings.json in the queue's folder
    /// (offline RFC D4, D7). Cleared when the pairing is cleared or replaced. WebGL keeps nothing between launches.
    /// Main thread only.
    /// </summary>
    internal sealed class OfflineSettingsCache
    {
        [Serializable]
        private sealed class Stored
        {
            public string last;
            public Dictionary<string, CachedOrgSettings> entries = new Dictionary<string, CachedOrgSettings>();
        }

        private readonly string _path;
        private Stored _stored = new Stored();

        /// <summary>root null: kept in memory only (WebGL).</summary>
        internal OfflineSettingsCache(string root)
        {
            _path = root != null ? Path.Combine(root, "settings.json") : null;
            if (_path == null || !File.Exists(_path)) return;
            try { _stored = JsonConvert.DeserializeObject<Stored>(File.ReadAllText(_path)) ?? new Stored(); }
            catch (Exception ex) { Logcat.Warning($"Ignored unreadable offline settings: {ex.Message}"); }
            _stored.entries ??= new Dictionary<string, CachedOrgSettings>();
        }

        /// <summary>True when the settings survive the app. False on WebGL, which starts every launch with nothing cached.</summary>
        internal bool IsPersistent => _path != null;

        internal static string KeyOf(AuthPayload payload) =>
            SavedSignIn.AppKeyOf(payload) + "|" + SavedSignIn.OrgKeyOf(payload) + "|" + SavedSignIn.InstanceKeyOf(payload);

        internal CachedOrgSettings Get(string key) =>
            key != null && _stored.entries.TryGetValue(key, out var settings) ? settings : null;

        /// <summary>The settings of the identity this app used last. At start-up, before identity is settled, that's the best guess.</summary>
        internal CachedOrgSettings GetLast() => Get(_stored.last);

        internal void Save(string key, CachedOrgSettings settings)
        {
            if (key == null || settings == null) return;
            _stored.entries[key] = settings;
            _stored.last = key;
            Write();
        }

        internal void Clear()
        {
            _stored = new Stored();
            Write();
        }

        private void Write()
        {
            if (_path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(_path, JsonConvert.SerializeObject(_stored));
            }
            catch (Exception ex) { Logcat.Warning($"Couldn't save offline settings: {ex.Message}"); }
        }
    }
}
