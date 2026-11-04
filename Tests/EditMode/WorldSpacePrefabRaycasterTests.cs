// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins that every canvas in the World-Space UI prompts people tap carries a plain GraphicRaycaster. A flat Android app
// loads the _OpenXR variants, whose TrackedDeviceGraphicRaycaster ignores screen taps, so without one the prompt can't
// be answered. The prefabs live in Samples~, which Unity doesn't import, so this reads their YAML.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

[TestFixture]
public class WorldSpacePrefabRaycasterTests
{
    // GraphicRaycaster's script GUID in com.unity.ugui.
    private const string GraphicRaycasterGuid = "dc42784cf147c0c48a680349fa168899";
    private const int CanvasClassId = 223;

    private static readonly Regex DocumentHeader = new(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.Multiline);

    private static IEnumerable<string> TappablePrefabs() => new[]
    {
        "AbxrExitPollRating", "AbxrExitPollThumbs", "AbxrExitPollMulti", "AbxrPinPad", "AbxrKeyboard"
    };

    [TestCaseSource(nameof(TappablePrefabs))]
    public void EveryCanvas_HasAGraphicRaycaster(string prefab)
    {
        var docs = ReadDocuments(PrefabPath(prefab));
        var canvases = docs.Values.Where(d => d.ClassId == CanvasClassId && !d.Stripped).ToList();
        Assert.IsNotEmpty(canvases, $"{prefab} has no canvas of its own");

        foreach (var canvas in canvases)
        {
            string gameObjectId = Field(canvas.Body, "m_GameObject");
            var components = Regex.Matches(docs[gameObjectId].Body, @"component: \{fileID: (-?\d+)\}")
                .Select(m => m.Groups[1].Value);
            bool hasRaycaster = components.Any(id =>
                docs.TryGetValue(id, out var c) && c.Body.Contains($"guid: {GraphicRaycasterGuid}"));
            Assert.IsTrue(hasRaycaster, $"{prefab}: the canvas on {Name(docs[gameObjectId].Body)} has no GraphicRaycaster");
        }
    }

    private static string PrefabPath(string prefab)
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Abxr).Assembly);
        if (package == null) Assert.Ignore("AbxrLib isn't loaded as a package, so its Samples~ can't be found.");
        return Path.Combine(package.resolvedPath, "Samples~", "World-Space UI", "Resources", "Prefabs", prefab + ".prefab");
    }

    private static Dictionary<string, (int ClassId, bool Stripped, string Body)> ReadDocuments(string path)
    {
        string yaml = File.ReadAllText(path);
        var headers = DocumentHeader.Matches(yaml).Cast<Match>().ToList();
        var docs = new Dictionary<string, (int, bool, string)>();
        for (int i = 0; i < headers.Count; i++)
        {
            int end = i + 1 < headers.Count ? headers[i + 1].Index : yaml.Length;
            docs[headers[i].Groups[2].Value] = (int.Parse(headers[i].Groups[1].Value), headers[i].Groups[3].Success,
                yaml.Substring(headers[i].Index, end - headers[i].Index));
        }
        return docs;
    }

    private static string Field(string body, string name) =>
        Regex.Match(body, name + @": \{fileID: (-?\d+)\}").Groups[1].Value;

    private static string Name(string body) => Regex.Match(body, @"m_Name: (.*)").Groups[1].Value.Trim();
}
