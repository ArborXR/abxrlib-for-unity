// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for the PlayerPrefs pairing store (SDK-60). Uses its own keys, so a real pairing is never touched.
using AbxrLib.Runtime.Services.Pairing;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class PairingStoreTests
{
    private const string TokenKey = "abxrlib_test_app_instance_token";
    private const string InstanceIdKey = "abxrlib_test_app_instance_id";

    private PlayerPrefsPairingStore _store;

    [SetUp]
    public void SetUp()
    {
        DeleteKeys();
        _store = new PlayerPrefsPairingStore(TokenKey, InstanceIdKey);
    }

    [TearDown]
    public void TearDown() => DeleteKeys();

    private static void DeleteKeys()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(InstanceIdKey);
        PlayerPrefs.Save();
    }

    [Test]
    public void DefaultKeys_AreTheOnesTheDocsName()
    {
        // ISV docs tell Android apps to exclude abxrlib_* from Auto Backup, so these names are part of the contract.
        Assert.AreEqual("abxrlib_app_instance_token", PlayerPrefsPairingStore.TokenKey);
        Assert.AreEqual("abxrlib_app_instance_id", PlayerPrefsPairingStore.InstanceIdKey);
    }

    [Test]
    public void Save_WritesBothValues()
    {
        _store.Save("token", "instance");

        Assert.AreEqual("token", PlayerPrefs.GetString(TokenKey));
        Assert.AreEqual("instance", PlayerPrefs.GetString(InstanceIdKey));
        Assert.IsTrue(_store.TryLoad(out string token, out string instanceId));
        Assert.AreEqual("token", token);
        Assert.AreEqual("instance", instanceId);
    }

    [Test]
    public void Clear_RemovesBothValues()
    {
        _store.Save("token", "instance");

        _store.Clear();

        Assert.IsFalse(PlayerPrefs.HasKey(TokenKey));
        Assert.IsFalse(PlayerPrefs.HasKey(InstanceIdKey));
        Assert.IsFalse(_store.TryLoad(out _, out _));
    }

    [Test]
    public void TryLoad_NeedsBothValues()
    {
        PlayerPrefs.SetString(TokenKey, "token");

        Assert.IsFalse(_store.TryLoad(out string token, out string instanceId));
        Assert.IsNull(token);
        Assert.IsNull(instanceId);
    }

    [Test]
    public void TryLoad_WithNothingStored_ReturnsFalse()
    {
        Assert.IsFalse(_store.TryLoad(out _, out _));
    }
}
