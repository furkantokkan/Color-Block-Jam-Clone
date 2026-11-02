using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Places its target camera at the reference game angle so a built board fills the
    /// portrait screen. It has no Update: the board owner calls Frame once per board
    /// build, and the pose stays until the next build.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardCamera : MonoBehaviour
    {
        // Defaults to the camera on this object when left empty.
        [SerializeField] private Camera m_camera;
        [SerializeField, Range(10f, 90f)] private float m_pitch = BoardCameraFraming.ReferencePitch;
        [SerializeField, Min(0f)] private float m_sideMargin = BoardCameraFraming.ReferenceSideMargin;
        [SerializeField, Min(0f)] private float m_verticalMargin = BoardCameraFraming.ReferenceVerticalMargin;

        /// <summary>Frames the board's world bounds with the camera's current FOV and aspect.</summary>
        public void Frame(Bounds boardBounds)
        {
            if (m_camera == null)
            {
                m_camera = GetComponent<Camera>();
                if (m_camera == null)
                {
                    Debug.LogWarning("Board camera skipped: no target camera.", this);
                    return;
                }
            }

            BoardCameraFraming.Calculate(boardBounds, m_camera.fieldOfView, m_camera.aspect, m_pitch,
                m_sideMargin, m_verticalMargin, out Vector3 position, out Quaternion rotation);
            m_camera.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
