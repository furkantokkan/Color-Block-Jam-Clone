using UnityEngine;

namespace ColorBlockJam.Runtime.Boosters
{
    [CreateAssetMenu(
        fileName = "ExtraTime",
        menuName = "Color Block Jam/Boosters/Extra Time Definition")]
    public sealed class ExtraTimeBoosterDefinition : BoosterDefinition
    {
        [SerializeField, Min(1)] private int m_bonusSeconds = 10;

        public int BonusSeconds => m_bonusSeconds;

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (Id != BoosterId.ExtraTime || TargetKind != BoosterTargetKind.None
                || m_bonusSeconds < 1)
            {
                error = "Extra Time requires its ID, no target, and a positive bonus.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
