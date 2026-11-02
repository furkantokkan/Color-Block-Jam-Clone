namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Tuning for the board presenter's motion: the door exit, the release settle, the door clip line
    /// and the block-top height the pointer plane uses. Motion is presentation only; the domain has
    /// already committed every move and exit before a tween starts (DEC-113).
    /// </summary>
    public static class ExitMotionSettings
    {
        /// <summary>Exit travel time per cell of travel; the whole exit is clamped to the bounds below.</summary>
        public const float k_secondsPerCell = 0.12f;

        public const float k_minSeconds = 0.2f;

        /// <summary>The longest exit; it stays under the 0.8 s Won Finish bound (DEC-93).</summary>
        public const float k_maxSeconds = 0.6f;

        /// <summary>
        /// How far beyond the floor edge the clip plane sits: 0.25 cell puts it on the door piece's
        /// centre line, because a door slot sits at cell + outward * 0.75 (DoorSlot.LocalPosition).
        /// </summary>
        public const float k_clipOffsetCells = 0.25f;

        /// <summary>How long a released block takes to settle from its lead offset onto its cell.</summary>
        public const float k_settleSeconds = 0.08f;

        /// <summary>
        /// Height of the highest LEGO stud above the block root, in board units. Measured from
        /// docs/art/board-kit/arrows-after.json: the arrow marker's underside is at 0.6811 - 0.0896 =
        /// 0.5915 and its recorded clearance above the highest stud is 0.025, so the stud top is 0.5665.
        /// Picking and dragging project onto this plane (DEC-115); the in-scene feel is UNPROVEN.
        /// </summary>
        public const float k_blockTopHeight = 0.5665f;
    }
}
