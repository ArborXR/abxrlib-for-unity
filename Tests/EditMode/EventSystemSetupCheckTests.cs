// Copyright (c) 2026 ArborXR. All rights reserved.
// The Setup Wizard's EventSystem row reads the launch scene's file instead of opening it (SDK-77). An EventSystem
// usually arrives inside a prefab (an XR rig, a UI prefab), so the scan has to follow prefab instances, and a
// false "missing" would offer to add a second EventSystem to a scene that already has one.
using System.Collections.Generic;
using AbxrLib.Editor;
using NUnit.Framework;

[TestFixture]
public class EventSystemSetupCheckTests
{
    private const string PrefabA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string PrefabB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string Header = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

    private static string WithEventSystem() =>
        Header + "--- !u!114 &1\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " +
        SetupWizardChecks.EventSystemScriptGuid + ", type: 3}\n";

    private static string WithCamera() =>
        Header + "--- !u!20 &1\nCamera:\n  m_Enabled: 1\n";

    private static string Instancing(string prefabGuid) =>
        Header + "--- !u!1001 &2\nPrefabInstance:\n  m_SourcePrefab: {fileID: 100100000, guid: " + prefabGuid + ", type: 3}\n";

    private static System.Func<string, string> Assets(Dictionary<string, string> files) =>
        guid => files.TryGetValue(guid, out var text) ? text : null;

    [Test]
    public void SceneWithAnEventSystem_HasOne() =>
        Assert.AreEqual(true, SetupWizardChecks.YamlHasEventSystem(WithEventSystem(), Assets(new())));

    [Test]
    public void SceneWithout_HasNone() =>
        Assert.AreEqual(false, SetupWizardChecks.YamlHasEventSystem(WithCamera(), Assets(new())));

    [Test]
    public void EventSystemInsideAnInstancedPrefab_Counts() =>
        Assert.AreEqual(true, SetupWizardChecks.YamlHasEventSystem(Instancing(PrefabA),
            Assets(new() { [PrefabA] = WithEventSystem() })));

    [Test]
    public void EventSystemInsideANestedPrefab_Counts() =>
        Assert.AreEqual(true, SetupWizardChecks.YamlHasEventSystem(Instancing(PrefabA),
            Assets(new() { [PrefabA] = Instancing(PrefabB), [PrefabB] = WithEventSystem() })));

    [Test]
    public void PrefabsThatInstanceEachOther_DoNotLoop() =>
        Assert.AreEqual(false, SetupWizardChecks.YamlHasEventSystem(Instancing(PrefabA),
            Assets(new() { [PrefabA] = Instancing(PrefabB), [PrefabB] = Instancing(PrefabA) })));

    [Test]
    public void UnreadablePrefab_CountsAsNone() =>
        Assert.AreEqual(false, SetupWizardChecks.YamlHasEventSystem(Instancing(PrefabA), Assets(new())));

    [Test]
    public void BinaryScene_IsUnknown() =>
        Assert.IsNull(SetupWizardChecks.YamlHasEventSystem("\u0000\u0001binary", Assets(new())));
}
