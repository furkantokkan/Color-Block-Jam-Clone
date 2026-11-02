using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Levels;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The gameplay HUD as the attempt loader sees it (B-21): shown for a level on Home, before Play, and told which
    /// starting boosters the opening applies. The loader resolves it with TryResolve, so a container without a HUD
    /// binding still plays.
    /// </summary>
    public interface IGameplayHud
    {
        /// <summary>Binds the HUD on first use and prepares it for the level on Home: text tables, reticle pool, frost.</summary>
        void Show(AttemptLevel level);

        /// <summary>The committed starting boosters Play is about to apply; an empty plan shows no opening icons.</summary>
        void SetOpening(in OpeningPlan plan);
    }
}
