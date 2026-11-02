using UnityEngine;

namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>Reports whether a press landed on runtime UI, so the board ignores it (AC-3.13).</summary>
    public interface IUiPointerFilter
    {
        bool IsOverUi(Vector2 screenPosition);
    }
}
