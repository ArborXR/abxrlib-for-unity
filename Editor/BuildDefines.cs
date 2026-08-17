/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 *
 * Reading and adding scripting define symbols, in one place.
 *
 * Unity has two generations of this API: the BuildTargetGroup overloads (obsolete from Unity 6, which warns on every
 * compile) and the NamedBuildTarget ones. NamedBuildTarget exists as far back as 2021.2 - below AbxrLib's minimum
 * Editor version - so the newer form is used everywhere and callers stay warning-free on new and old Editors alike.
 *
 * Translating a BuildTargetGroup to a NamedBuildTarget throws for groups that have no named target, so that is
 * handled here rather than at each call site.
 */

using System;
using UnityEditor;
using UnityEditor.Build;

namespace AbxrLib.Editor
{
    internal static class BuildDefines
    {
        /// <summary>
        /// The scripting define symbols for a build target group, or empty when the group has no named target (an
        /// unknown or no-longer-supported platform). Never throws.
        /// </summary>
        internal static string Get(BuildTargetGroup group)
        {
            if (!TryGetNamedTarget(group, out NamedBuildTarget target)) return "";

            return PlayerSettings.GetScriptingDefineSymbols(target) ?? "";
        }

        /// <summary>
        /// Adds a define to a build target group unless it is already there. Returns true only when the define was
        /// actually added, so callers can log that they changed the project without logging on every Editor load.
        /// </summary>
        internal static bool Add(string define, BuildTargetGroup group)
        {
            if (string.IsNullOrEmpty(define)) return false;
            if (!TryGetNamedTarget(group, out NamedBuildTarget target)) return false;

            string defines = PlayerSettings.GetScriptingDefineSymbols(target) ?? "";
            if (defines.Contains(define)) return false;

            PlayerSettings.SetScriptingDefineSymbols(target,
                string.IsNullOrEmpty(defines) ? define : defines + ";" + define);
            return true;
        }

        private static bool TryGetNamedTarget(BuildTargetGroup group, out NamedBuildTarget target)
        {
            target = default;
            if (group == BuildTargetGroup.Unknown) return false;

            try
            {
                target = NamedBuildTarget.FromBuildTargetGroup(group);
                return true;
            }
            catch (ArgumentException)
            {
                // A group with no named target (for example a platform Unity no longer supports).
                return false;
            }
        }
    }
}
