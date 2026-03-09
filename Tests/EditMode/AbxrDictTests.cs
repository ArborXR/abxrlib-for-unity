// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode unit tests for Abxr.Dict
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class AbxrDictTests
{
    [Test]
    public void DefaultConstructor_CreatesEmptyDict()
    {
        var dict = new Abxr.Dict();
        Assert.AreEqual(0, dict.Count);
    }

    [Test]
    public void With_AddsSinglePair()
    {
        var dict = new Abxr.Dict().With("key", "value");
        Assert.AreEqual("value", dict["key"]);
        Assert.AreEqual(1, dict.Count);
    }

    [Test]
    public void With_ReturnsDict_ForMethodChaining()
    {
        var dict = new Abxr.Dict()
            .With("a", "1")
            .With("b", "2")
            .With("c", "3");
        Assert.AreEqual(3, dict.Count);
        Assert.AreEqual("1", dict["a"]);
        Assert.AreEqual("2", dict["b"]);
        Assert.AreEqual("3", dict["c"]);
    }

    [Test]
    public void With_OverwritesExistingKey()
    {
        var dict = new Abxr.Dict()
            .With("key", "original")
            .With("key", "updated");
        Assert.AreEqual("updated", dict["key"]);
        Assert.AreEqual(1, dict.Count);
    }

    [Test]
    public void CopyConstructor_ContainsAllPairs()
    {
        var source = new Dictionary<string, string>
        {
            ["x"] = "1",
            ["y"] = "2"
        };
        var dict = new Abxr.Dict(source);
        Assert.AreEqual(2, dict.Count);
        Assert.AreEqual("1", dict["x"]);
        Assert.AreEqual("2", dict["y"]);
    }

    [Test]
    public void CopyConstructor_IsIndependentFromSource()
    {
        var source = new Dictionary<string, string> { ["k"] = "original" };
        var dict = new Abxr.Dict(source);
        source["k"] = "mutated";
        Assert.AreEqual("original", dict["k"]);
    }

    [Test]
    public void Dict_IsAssignableToStringStringDictionary()
    {
        Dictionary<string, string> typed = new Abxr.Dict().With("k", "v");
        Assert.AreEqual("v", typed["k"]);
    }

    [Test]
    public void Dict_CanBePassedWhereStringDictionaryExpected()
    {
        // Verify it works as a metadata argument
        var meta = new Abxr.Dict().With("score", "95").With("level", "2");
        Assert.AreEqual("95", meta["score"]);
        Assert.AreEqual("2", meta["level"]);
    }
}
