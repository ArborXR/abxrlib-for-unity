using UnityEngine;

namespace AbxrLib.Runtime.Core.UI
{
    /// <summary>
    /// Sizes the World-Space UI's panels, which are built in metres for a headset, to fit a flat screen's camera view.
    /// </summary>
    internal static class FlatScreenFit
    {
        /// <summary>The share of the view a fitted panel may fill, leaving a margin at the edges.</summary>
        internal const float Fill = 0.9f;

        /// <summary>
        /// The scale, at most 1, that fits a panel reaching <paramref name="halfExtents"/> metres either side of its
        /// centre into the view of a perspective camera <paramref name="distance"/> metres away. It never grows a panel,
        /// and returns 1 when there's nothing to fit or the camera can't be measured.
        /// </summary>
        internal static float Scale(Vector2 halfExtents, float distance, float verticalFovDegrees, float aspect)
        {
            if (halfExtents.x <= 0f || halfExtents.y <= 0f || distance <= 0f || verticalFovDegrees <= 0f || aspect <= 0f)
                return 1f;
            float halfHeight = distance * Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * aspect;
            return Mathf.Min(1f, Fill * halfHeight / halfExtents.y, Fill * halfWidth / halfExtents.x);
        }
    }
}
