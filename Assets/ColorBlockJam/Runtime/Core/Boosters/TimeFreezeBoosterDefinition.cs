using UnityEngine;

namespace ColorBlockJam.Runtime.Boosters
{
    [CreateAssetMenu(
        fileName = "TimeFreeze",
        menuName = "Color Block Jam/Boosters/Time Freeze Definition")]
    public sealed class TimeFreezeBoosterDefinition : BoosterDefinition
    {
        [SerializeField, Min(0.1f)] private float m_durationSeconds = 10f;

        public float DurationSeconds => m_durationSeconds;

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (Id != BoosterId.TimeFreeze || TargetKind != BoosterTargetKind.None
                || m_durationSeconds <= 0f)
            {
                error = "Time Freeze requires its ID, no target, and a positive duration.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
