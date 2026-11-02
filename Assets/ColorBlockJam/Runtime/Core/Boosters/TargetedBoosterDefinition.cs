using UnityEngine;

namespace ColorBlockJam.Runtime.Boosters
{
    [CreateAssetMenu(
        fileName = "TargetedBooster",
        menuName = "Color Block Jam/Boosters/Targeted Definition")]
    public sealed class TargetedBoosterDefinition : BoosterDefinition
    {
        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (Id != BoosterId.Rocket && Id != BoosterId.StartingRocket
                && Id != BoosterId.Hammer && Id != BoosterId.Vacuum)
            {
                error = "A targeted definition must be Rocket, Starting Rocket, Hammer, or Vacuum.";
                return false;
            }

            if (TargetKind == BoosterTargetKind.None || EffectPrefab == null)
            {
                error = $"{Id} requires a target kind and effect prefab.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
