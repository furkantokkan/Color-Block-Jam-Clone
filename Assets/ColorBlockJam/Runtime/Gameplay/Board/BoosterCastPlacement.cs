using ColorBlockJam.Runtime.Layout;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>Allocation-free cast placement for the current board, camera and HUD anchor.</summary>
    internal readonly struct BoosterCastPlacement
    {
        private const float k_castEdgeGapCells = 1f;
        private const float k_vacuumFrontGap = 0.1f;

        private readonly Transform m_boardRoot;
        private readonly IBoardLayout m_layout;
        private readonly Camera m_camera;
        private readonly IBoosterCastAnchor m_anchor;

        public BoosterCastPlacement(Transform boardRoot, IBoardLayout layout, Camera camera,
            IBoosterCastAnchor anchor)
        {
            m_boardRoot = boardRoot;
            m_layout = layout;
            m_camera = camera;
            m_anchor = anchor;
        }

        // The floor point under the bar; an unavailable anchor falls back below the lower board edge.
        public Vector3 GetLaunch()
        {
            if (m_camera != null && m_anchor != null && m_anchor.TryGetLaunchViewportPoint(out Vector2 viewport))
            {
                Ray ray = m_camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
                Plane floor = new Plane(m_boardRoot.up, m_boardRoot.position);
                if (floor.Raycast(ray, out float distance))
                {
                    return ray.GetPoint(distance);
                }
            }

            Bounds bounds = m_layout.LocalBounds;
            Vector3 local = new Vector3(bounds.center.x, 0f,
                bounds.min.z - k_castEdgeGapCells * BoardLayout.CellSize);
            return m_boardRoot.TransformPoint(local);
        }

        // Move toward the camera on the same view ray until the flat Vacuum clears the near wall tops.
        public Vector3 GetVacuumLaunch()
        {
            Vector3 launch = GetLaunch();
            if (m_camera == null)
            {
                return launch;
            }

            Bounds bounds = m_layout.LocalBounds;
            Vector3 eye = m_camera.transform.position;
            Vector3 forward = m_camera.transform.forward;
            float front = Mathf.Min(
                Vector3.Dot(m_boardRoot.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z)) - eye, forward),
                Vector3.Dot(m_boardRoot.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z)) - eye, forward))
                - k_vacuumFrontGap;
            float depth = Vector3.Dot(launch - eye, forward);
            if (depth <= front || front <= m_camera.nearClipPlane)
            {
                return launch;
            }

            return eye + (launch - eye) * (front / depth);
        }

        // Keep the mallet's upright swing plane aligned with the view.
        public Quaternion GetHammerRotation()
        {
            if (m_camera == null)
            {
                return m_boardRoot.rotation;
            }

            Vector3 forward = m_camera.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(forward, Vector3.up)
                : m_boardRoot.rotation;
        }

        public Quaternion GetFacingRotation()
        {
            return m_camera != null ? m_camera.transform.rotation : m_boardRoot.rotation;
        }
    }
}
