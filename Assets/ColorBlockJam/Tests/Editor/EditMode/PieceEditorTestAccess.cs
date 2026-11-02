using System;
using System.Reflection;
using ColorBlockJam.Editor.BoardKit;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Private fixture access for the Piece Editor, matching LevelEditorTestAccess: the production window keeps no
    /// duplicate fields for tests, so every fixture reaches its draft, grid and key-handling state through one seam.
    /// </summary>
    internal static class PieceEditorTestAccess
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static object Get(PieceEditorWindow window, string name) => FindField(window, name).GetValue(window);

        internal static T Get<T>(PieceEditorWindow window, string name) => (T)Get(window, name);

        internal static void Set(PieceEditorWindow window, string name, object value) =>
            FindField(window, name).SetValue(window, value);

        internal static object Invoke(PieceEditorWindow window, string name, params object[] arguments)
        {
            MethodInfo method = typeof(PieceEditorWindow).GetMethod(name, k_members);
            if (method == null) throw new MissingMethodException(typeof(PieceEditorWindow).FullName, name);
            return method.Invoke(window, arguments);
        }

        private static FieldInfo FindField(PieceEditorWindow window, string name) =>
            typeof(PieceEditorWindow).GetField(name, k_members)
            ?? throw new MissingFieldException(typeof(PieceEditorWindow).FullName, name);
    }
}
