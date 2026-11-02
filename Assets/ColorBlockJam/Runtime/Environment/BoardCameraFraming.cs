using System;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Calculates the camera pose that shows a whole board from a fixed pitch, from the
    /// board bounds and the camera lens only, so the result never depends on where the
    /// camera was before. Pitch 75 and FOV 60 come from the CBJ Template CameraController
    /// and match the real game's perspective. The margins are calibrated to the real-game
    /// screenshots so the board fills about 0.9 of the screen width; the template's 4 and
    /// 12 left it at about 0.5 (DEC-103, DEC-118).
    /// </summary>
    public static class BoardCameraFraming
    {
        public const float ReferencePitch = 75f;
        public const float ReferenceSideMargin = 1.5f;
        public const float ReferenceVerticalMargin = 5f;

        /// <summary>
        /// The distance is the larger of the one that fits the board width plus the side
        /// margin horizontally and the one that fits the board depth plus the vertical
        /// margin vertically. The camera looks at the bounds centre from that distance.
        /// </summary>
        public static void Calculate(
            Bounds bounds, float verticalFieldOfView, float aspect, float pitch,
            float sideMargin, float verticalMargin, out Vector3 position, out Quaternion rotation)
        {
            if (verticalFieldOfView <= 0f || verticalFieldOfView >= 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(verticalFieldOfView), verticalFieldOfView, "Use 0 to 180 degrees.");
            }

            if (aspect <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "Aspect must be positive.");
            }

            float frustumHeightPerUnit = 2f * Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
            float widthDistance = (bounds.size.x + sideMargin) / (frustumHeightPerUnit * aspect);
            float depthDistance = (bounds.size.z + verticalMargin) / frustumHeightPerUnit;
            float distance = Mathf.Max(widthDistance, depthDistance);

            rotation = Quaternion.Euler(pitch, 0f, 0f);
            position = bounds.center - rotation * Vector3.forward * distance;
        }
    }
}
