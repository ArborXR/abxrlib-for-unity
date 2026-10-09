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
    private const string DeviceNameKey = "abxrlib_test_paired_device_name";

    private PlayerPrefsPairingStore _store;

    [SetUp]
    public void SetUp()
    {
        DeleteKeys();
        _store = new PlayerPrefsPairingStore(TokenKey, InstanceIdKey, DeviceNameKey);
    }

    [TearDown]
    public void TearDown() => DeleteKeys();

    private static void DeleteKeys()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(InstanceIdKey);
        PlayerPrefs.DeleteKey(DeviceNameKey);
        PlayerPrefs.Save();
    }

    [Test]
    public void DefaultKeys_AreTheOnesTheDocsName()
    {
        // ISV docs tell Android apps to exclude abxrlib_* from Auto Backup, so these names are part of the contract.
        Assert.AreEqual("abxrlib_app_instance_token", PlayerPrefsPairingStore.TokenKey);
        Assert.AreEqual("abxrlib_app_instance_id", PlayerPrefsPairingStore.InstanceIdKey);
        Assert.AreEqual("abxrlib_paired_device_name", PlayerPrefsPairingStore.DeviceNameKey);
    }

    [Test]
    public void Save_WritesBothValues()
    {
        _store.Save("token", "instance", null);

        Assert.AreEqual("token", PlayerPrefs.GetString(TokenKey));
        Assert.AreEqual("instance", PlayerPrefs.GetString(InstanceIdKey));
        Assert.IsTrue(_store.TryLoad(out string token, out string instanceId));
        Assert.AreEqual("token", token);
        Assert.AreEqual("instance", instanceId);
        Assert.IsNull(_store.LoadDeviceName());
    }

    [Test]
    public void Save_WithAName_StoresIt()
    {
        _store.Save("token", "instance", "Headset 12");

        Assert.AreEqual("Headset 12", PlayerPrefs.GetString(DeviceNameKey));
        Assert.AreEqual("Headset 12", _store.LoadDeviceName());
    }

    [Test]
    public void Save_WithoutAName_RemovesAnOldOne()
    {
        _store.Save("token", "instance", "Headset 12");

        _store.Save("token2", "instance2", null);

        Assert.IsFalse(PlayerPrefs.HasKey(DeviceNameKey));
    }

    [Test]
    public void SaveDeviceName_ReplacesOnlyTheName()
    {
        _store.Save("token", "instance", "Headset 12");

        _store.SaveDeviceName("Headset 7");

        Assert.AreEqual("Headset 7", _store.LoadDeviceName());
        Assert.IsTrue(_store.TryLoad(out string token, out _));
        Assert.AreEqual("token", token);

        _store.SaveDeviceName(null);
        Assert.IsNull(_store.LoadDeviceName());
    }

    [Test]
    public void Clear_RemovesBothValues()
    {
        _store.Save("token", "instance", "Headset 12");

        _store.Clear();

        Assert.IsFalse(PlayerPrefs.HasKey(TokenKey));
        Assert.IsFalse(PlayerPrefs.HasKey(InstanceIdKey));
        Assert.IsFalse(PlayerPrefs.HasKey(DeviceNameKey));
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
