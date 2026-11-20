// Copyright (c) 2026 ArborXR. All rights reserved.
// EditMode tests for fitting the World-Space UI's panels to a flat screen (SDK-74).
using AbxrLib.Runtime.Core.UI;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class FlatScreenFitTests
{
    private const float Tolerance = 1e-4f;

    [Test]
    public void Scale_WhenThePanelFits_IsOne()
    {
        Assert.AreEqual(1f, FlatScreenFit.Scale(new Vector2(0.2f, 0.2f), 1f, 60f, 16f / 9f));
    }

    [Test]
    public void Scale_ATallPanelInLandscape_FitsTheHeight()
    {
        // The exit poll's backdrop is about 1.07 m tall. At 1 m with a 60° view, half the view is tan(30°) m tall.
        float halfHeight = Mathf.Tan(30f * Mathf.Deg2Rad);
        float scale = FlatScreenFit.Scale(new Vector2(0.75f, 0.9f), 1f, 60f, 16f / 9f);

        Assert.AreEqual(FlatScreenFit.Fill * halfHeight / 0.9f, scale, Tolerance);
    }

    [Test]
    public void Scale_AWidePanelInPortrait_FitsTheWidth()
    {
        float halfWidth = Mathf.Tan(30f * Mathf.Deg2Rad) * 0.5f;
        float scale = FlatScreenFit.Scale(new Vector2(0.75f, 0.3f), 1f, 60f, 0.5f);

        Assert.AreEqual(FlatScreenFit.Fill * halfWidth / 0.75f, scale, Tolerance);
    }

    [Test]
    public void Scale_FurtherAway_ShrinksLess()
    {
        var size = new Vector2(0.75f, 0.9f);

        Assert.Greater(FlatScreenFit.Scale(size, 1.5f, 60f, 16f / 9f), FlatScreenFit.Scale(size, 1f, 60f, 16f / 9f));
    }

    [TestCase(0f, 0.5f, 1f, 60f, 1.7f)]
    [TestCase(0.5f, 0.5f, 0f, 60f, 1.7f)]
    [TestCase(0.5f, 0.5f, 1f, 0f, 1.7f)]
    [TestCase(0.5f, 0.5f, 1f, 60f, 0f)]
    public void Scale_WithNothingToMeasure_IsOne(float halfWidth, float halfHeight, float distance, float fov, float aspect)
    {
        Assert.AreEqual(1f, FlatScreenFit.Scale(new Vector2(halfWidth, halfHeight), distance, fov, aspect));
    }
}
