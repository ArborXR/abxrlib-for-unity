// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for super metadata — Register, RegisterOnce, Unregister, Reset, GetSuperMetaData.
using NUnit.Framework;

[TestFixture]
public class SuperMetaDataTests : AbxrPlayModeTestBase
{
    // ── Initial state ─────────────────────────────────────────────────────

    [Test]
    public void GetSuperMetaData_InitiallyEmpty()
    {
        var meta = Abxr.GetSuperMetaData();
        // API returns null when there is no super metadata (empty state).
        Assert.That(meta, Is.Null.Or.Empty);
    }

    // ── Register ──────────────────────────────────────────────────────────

    [Test]
    public void Register_AddsKeyValuePair()
    {
        Abxr.Register("user_role", "student");
        var meta = Abxr.GetSuperMetaData();
        Assert.IsTrue(meta.ContainsKey("user_role"));
        Assert.AreEqual("student", meta["user_role"]);
    }

    [Test]
    public void Register_Overwrite_True_UpdatesExistingValue()
    {
        Abxr.Register("score_type", "raw");
        Abxr.Register("score_type", "normalized", overwrite: true);
        Assert.AreEqual("normalized", Abxr.GetSuperMetaData()["score_type"]);
    }

    [Test]
    public void Register_Overwrite_False_PreservesExistingValue()
    {
        Abxr.Register("session_type", "training");
        Abxr.Register("session_type", "assessment", overwrite: false);
        Assert.AreEqual("training", Abxr.GetSuperMetaData()["session_type"]);
    }

    [Test]
    public void Register_DefaultOverwrite_IsTrue()
    {
        Abxr.Register("lang", "en");
        Abxr.Register("lang", "fr"); // default overwrite = true
        Assert.AreEqual("fr", Abxr.GetSuperMetaData()["lang"]);
    }

    [Test]
    public void Register_MultipleKeys_AllPresent()
    {
        Abxr.Register("difficulty", "hard");
        Abxr.Register("language", "en-US");
        Abxr.Register("version", "2.3.1");
        var meta = Abxr.GetSuperMetaData();
        Assert.AreEqual("hard", meta["difficulty"]);
        Assert.AreEqual("en-US", meta["language"]);
        Assert.AreEqual("2.3.1", meta["version"]);
    }

    // ── Reserved keys ─────────────────────────────────────────────────────

    [Test]
    public void Register_ReservedKey_Module_IsRejected()
    {
        Abxr.Register("module", "some_value");
        var meta = Abxr.GetSuperMetaData();
        Assert.IsTrue(meta == null || !meta.ContainsKey("module"),
            "Reserved key 'module' must not be stored via Register()");
    }

    [Test]
    public void Register_ReservedKey_ModuleName_IsRejected()
    {
        Abxr.Register("moduleName", "My Module");
        var meta = Abxr.GetSuperMetaData();
        Assert.IsTrue(meta == null || !meta.ContainsKey("moduleName"));
    }

    [Test]
    public void Register_ReservedKey_ModuleId_IsRejected()
    {
        Abxr.Register("moduleId", "some-id");
        var meta = Abxr.GetSuperMetaData();
        Assert.IsTrue(meta == null || !meta.ContainsKey("moduleId"));
    }

    [Test]
    public void Register_ReservedKey_ModuleOrder_IsRejected()
    {
        Abxr.Register("moduleOrder", "1");
        var meta = Abxr.GetSuperMetaData();
        Assert.IsTrue(meta == null || !meta.ContainsKey("moduleOrder"));
    }

    // ── RegisterOnce ──────────────────────────────────────────────────────

    [Test]
    public void RegisterOnce_SetsValueWhenKeyAbsent()
    {
        Abxr.RegisterOnce("cohort", "group_a");
        Assert.AreEqual("group_a", Abxr.GetSuperMetaData()["cohort"]);
    }

    [Test]
    public void RegisterOnce_PreservesValueWhenKeyPresent()
    {
        Abxr.Register("cohort", "original");
        Abxr.RegisterOnce("cohort", "should_not_overwrite");
        Assert.AreEqual("original", Abxr.GetSuperMetaData()["cohort"]);
    }

    // ── Unregister ────────────────────────────────────────────────────────

    [Test]
    public void Unregister_RemovesExistingKey()
    {
        Abxr.Register("temp_key", "temp_value");
        Abxr.Unregister("temp_key");
        var meta = Abxr.GetSuperMetaData();
        Assert.IsTrue(meta == null || !meta.ContainsKey("temp_key"));
    }

    [Test]
    public void Unregister_NonExistentKey_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Unregister("key_that_does_not_exist"));

    [Test]
    public void Unregister_LeavesOtherKeysIntact()
    {
        Abxr.Register("keep_this", "value1");
        Abxr.Register("remove_this", "value2");
        Abxr.Unregister("remove_this");
        var meta = Abxr.GetSuperMetaData();
        Assert.IsTrue(meta.ContainsKey("keep_this"));
        Assert.IsFalse(meta.ContainsKey("remove_this"));
    }

    // ── Reset ─────────────────────────────────────────────────────────────

    [Test]
    public void Reset_ClearsAllSuperMetaData()
    {
        Abxr.Register("k1", "v1");
        Abxr.Register("k2", "v2");
        Abxr.Register("k3", "v3");
        Abxr.Reset();
        var meta = Abxr.GetSuperMetaData();
        Assert.That(meta?.Count ?? 0, Is.EqualTo(0));
    }

    [Test]
    public void Reset_EmptyDictionary_DoesNotThrow()
        => Assert.DoesNotThrow(() => Abxr.Reset());

    // ── GetSuperMetaData isolation ────────────────────────────────────────

    [Test]
    public void GetSuperMetaData_ReturnsIndependentCopy()
    {
        Abxr.Register("original", "value");
        var copy = Abxr.GetSuperMetaData();
        copy["injected"] = "should_not_affect_internal";

        var fresh = Abxr.GetSuperMetaData();
        Assert.IsFalse(fresh.ContainsKey("injected"),
            "GetSuperMetaData should return a copy, not a reference to the internal dictionary");
    }
}
