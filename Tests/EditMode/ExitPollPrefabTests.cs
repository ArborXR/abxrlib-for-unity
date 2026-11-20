// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins that every button field on the exit poll components is wired in the base prefabs (SDK-76). An empty field
// throws in Start, which leaves that button and every one after it without a listener, so the poll can't be answered
// with them. The prefabs live in Samples~, which Unity doesn't import, so this reads their YAML.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

[TestFixture]
public class ExitPollPrefabTests
{
    // uGUI's Button script GUID in com.unity.ugui.
    private const string ButtonGuid = "4e29b1a8efbd4b44bb3f3716e73f07ff";

    private static readonly Regex DocumentHeader = new(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.Multiline);

    // Prefab, the script GUID of its poll component, and how many buttons that component has.
    private static IEnumerable<TestCaseData> PollComponents() => new[]
    {
        new TestCaseData("AbxrExitPollRating", "9a2a647c952d43e0a7ce0b88a9e13cac", 5).SetName("Rating"),
        new TestCaseData("AbxrExitPollThumbs", "6374b67eed5042529b79f7b85e7ea484", 2).SetName("Thumbs"),
    };

    [TestCaseSource(nameof(PollComponents))]
    public void EveryButtonField_PointsAtAButton(string prefab, string scriptGuid, int buttonCount)
    {
        var docs = ReadDocuments(PrefabPath(prefab));
        var component = docs.Values.Single(d => d.Contains($"m_Script: {{fileID: 11500000, guid: {scriptGuid}"));
        var fields = Regex.Matches(component, @"^  (\w+Button): \{fileID: (-?\d+)\}", RegexOptions.Multiline)
            .Select(m => (Name: m.Groups[1].Value, Id: m.Groups[2].Value)).ToList();

        Assert.AreEqual(buttonCount, fields.Count, $"{prefab}: expected {buttonCount} button fields");
        foreach (var (name, id) in fields)
        {
            Assert.AreNotEqual("0", id, $"{prefab}: {name} is empty");
            Assert.IsTrue(docs.TryGetValue(id, out var target), $"{prefab}: {name} points at {id}, which isn't in the prefab");
            Assert.That(target, Does.Contain($"guid: {ButtonGuid}"), $"{prefab}: {name} doesn't point at a Button");
        }
    }

    private static string PrefabPath(string prefab)
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Abxr).Assembly);
        if (package == null) Assert.Ignore("AbxrLib isn't loaded as a package, so its Samples~ can't be found.");
        return Path.Combine(package.resolvedPath, "Samples~", "World-Space UI", "Resources", "Prefabs", prefab + ".prefab");
    }

    private static Dictionary<string, string> ReadDocuments(string path)
    {
        string yaml = File.ReadAllText(path);
        var headers = DocumentHeader.Matches(yaml).Cast<Match>().ToList();
        var docs = new Dictionary<string, string>();
        for (int i = 0; i < headers.Count; i++)
        {
            int end = i + 1 < headers.Count ? headers[i + 1].Index : yaml.Length;
            docs[headers[i].Groups[2].Value] = yaml.Substring(headers[i].Index, end - headers[i].Index);
        }
        return docs;
    }
}
