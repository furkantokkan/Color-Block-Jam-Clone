using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>Authoring-only effects share the canvas lifecycle, without effect branches in its painter.</summary>
    internal interface ILevelCanvasEffectLayer : System.IDisposable
    {
        void SetModel(LevelCanvasModel model, VisualElement parent);
        void Position(LevelCanvasGeometry geometry);
        VisualElement GetBadge(int block);
        void Draw(Painter2D painter, LevelCanvasGeometry geometry);
    }
}
