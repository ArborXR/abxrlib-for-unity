// Copyright (c) 2026 ArborXR. All rights reserved.
using System;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// One line describing the environment AbxrLib is running in. It rides on the first log line at startup (which
    /// already names the AbxrLib version), so a device log carries the Unity version, platform, build type, and
    /// backend host without anyone having to ask for them.
    ///
    /// Only the host of <see cref="Configuration.restUrl"/> is included. Staging against production is the first
    /// question support asks, and the host answers it without putting the whole URL in every log.
    /// </summary>
    internal static class EnvironmentSummary
    {
        internal static string Describe(Configuration config)
        {
            string buildType = config?.buildType ?? "(none)";
            string tokens = config == null ? "?" : config.useAppTokens ? "on" : "off";
            string host = HostOf(config?.restUrl);
            return $"Unity {Application.unityVersion} | {Application.platform} | buildType={buildType} | tokens={tokens} | host={host}";
        }

        /// <summary>The host part of an absolute URL, or a marker when the value is not one.</summary>
        internal static string HostOf(string url)
        {
            if (string.IsNullOrEmpty(url)) return "(not set)";
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri.Host
                : "(invalid url)";
        }
    }
}
