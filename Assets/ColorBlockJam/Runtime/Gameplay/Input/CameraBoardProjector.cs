using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Picking;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>
    /// Projects between the screen and the board plane with analytic ray/plane math against the
    /// BoardRoot plane lifted to block-top height, so picking and dragging need no colliders.
    /// Drag and pick share the plane, so the grab offset stays consistent (DEC-115).
    /// </summary>
    public sealed class CameraBoardProjector : IBoardProjector, IFootprintProjector
    {
        private const float k_parallelEpsilon = 1e-6f;

        private readonly Camera m_camera;
        private readonly Transform m_boardRoot;
        private readonly float m_planeHeight;

        public CameraBoardProjector(Camera camera, Transform boardRoot, float planeHeight)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (boardRoot == null)
            {
                throw new ArgumentNullException(nameof(boardRoot));
            }

            m_camera = camera;
            m_boardRoot = boardRoot;
            m_planeHeight = planeHeight;
        }

        public IFootprintProjector Footprint => this;

        public bool TryScreenToBoard(Vector2 screenPosition, out BoardPoint point)
        {
            Ray ray = m_camera.ScreenPointToRay(screenPosition);
            Vector3 normal = m_boardRoot.up;
            float denominator = Vector3.Dot(normal, ray.direction);
            if (Mathf.Abs(denominator) < k_parallelEpsilon)
            {
                point = default;
                return false;
            }

            Vector3 planePoint = m_boardRoot.TransformPoint(0f, m_planeHeight, 0f);
            float distance = Vector3.Dot(normal, planePoint - ray.origin) / denominator;
            if (distance < 0f)
            {
                point = default;
                return false;
            }

            Vector3 local = m_boardRoot.InverseTransformPoint(ray.GetPoint(distance));
            point = new BoardPoint(local.x, local.z);
            return true;
        }

        public ScreenPoint Project(float boardX, float boardY)
        {
            Vector3 screen = m_camera.WorldToScreenPoint(m_boardRoot.TransformPoint(boardX, m_planeHeight, boardY));
            return screen.z < 0f ? ScreenPoint.Offscreen : new ScreenPoint(screen.x, screen.y);
        }
    }
}
