// Copyright (c) 2026 ArborXR. All rights reserved.
// When building the Test Runner Player (Run on device / VR headset), inject ABXR_TEST_RUNNER_PLAYER
// so Initialize skips creating the AbxrSubsystem and we avoid a redundant init that would be destroyed in test SetUp.
using UnityEditor;
using UnityEditor.TestTools;

[assembly: TestPlayerBuildModifier(typeof(AbxrLib.Editor.AbxrTestPlayerBuildModifier))]

namespace AbxrLib.Editor
{
    internal class AbxrTestPlayerBuildModifier : ITestPlayerBuildModifier
    {
        public BuildPlayerOptions ModifyOptions(BuildPlayerOptions playerOptions)
        {
            var defines = playerOptions.extraScriptingDefines ?? System.Array.Empty<string>();
            var count = defines.Length;
            var newDefines = new string[count + 1];
            if (count > 0)
                System.Array.Copy(defines, newDefines, count);
            newDefines[count] = "ABXR_TEST_RUNNER_PLAYER";
            playerOptions.extraScriptingDefines = newDefines;
            return playerOptions;
        }
    }
}
