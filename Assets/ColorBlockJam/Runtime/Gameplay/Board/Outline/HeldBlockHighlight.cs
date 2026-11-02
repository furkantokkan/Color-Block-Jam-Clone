using HighlightPlus;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board.Outline
{
    /// <summary>
    /// Gives a block root the white held-block outline through Highlight Plus (DEC-89, DEC-145): one
    /// screen-space outline around the silhouette of all the root's tile renderers together, so modular tiles
    /// show no inner seams, drawn on top so no wall or neighbour hides it. BoardPresenter prepares each placed
    /// root on Home, and prepares a Rocket split or recomposed root again the first time it is highlighted in
    /// Playing, because that root's tiles changed.
    /// </summary>
    public static class HeldBlockHighlight
    {
        /// <summary>Outline width in Highlight Plus units at Highest quality (screen-space, constant width).</summary>
        public const float k_width = 1.2f;

        /// <summary>Highlight Plus outline downsampling: 2 blurs the outline at half resolution (DEC-201).</summary>
        public const int k_downsampling = 2;

        /// <summary>
        /// Adds and configures the effect the first time a pooled root is placed, and re-reads its renderers,
        /// since a root is composed from pooled tiles. BoardPresenter calls this on Home for a newly placed
        /// block, and again in Playing the first time a Rocket split or recomposed root is highlighted
        /// (DEC-145). Returns it switched off.
        /// </summary>
        public static HighlightEffect Prepare(GameObject root)
        {
            if (!root.TryGetComponent(out HighlightEffect effect))
            {
                effect = root.AddComponent<HighlightEffect>();
                Configure(effect);
            }

            effect.Refresh();
            effect.SetHighlighted(false);
            return effect;
        }

        private static void Configure(HighlightEffect effect)
        {
            effect.effectGroup = TargetOptions.Children;
            effect.outline = 1f;
            effect.outlineColor = Color.white;
            effect.outlineWidth = k_width;
            effect.outlineQuality = HighlightPlus.QualityLevel.Highest;
            // The blur chain at half resolution: holding a block dropped the Galaxy A16 from 58.5 to 46.7 fps, felt as a
            // delay on press; the silhouette mask stays full resolution (DEC-201).
            effect.outlineDownsampling = k_downsampling;
            effect.outlineContourStyle = ContourStyle.AroundObjectShape;
            effect.outlineVisibility = Visibility.AlwaysOnTop;
            effect.constantWidth = true;
            effect.glow = 0f;
            effect.innerGlow = 0f;
            effect.overlay = 0f;
            effect.seeThrough = SeeThroughMode.Never;
        }
    }
}
