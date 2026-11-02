// execute_code body: lists open Editor windows (visible or behind a tab), the selection and the console size.
// Visible windows repaint during Play Mode and cost EditorLoop time; a Scene view or Inspector showing a moving
// object is a common Editor-only cost.
var sb = new System.Text.StringBuilder();
var parentField = typeof(EditorWindow).GetField("m_Parent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
{
    bool visible = false;
    var parent = parentField != null ? parentField.GetValue(w) : null;
    if (parent != null)
    {
        var actual = parent.GetType().GetProperty("actualView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        visible = actual != null && ReferenceEquals(actual.GetValue(parent), w);
    }
    sb.AppendLine((visible ? "VISIBLE " : "tabbed  ") + w.GetType().Name + " \"" + w.titleContent.text + "\" " + (int)w.position.width + "x" + (int)w.position.height + (w.maximized ? " MAX" : ""));
}
sb.AppendLine("selection: " + (Selection.activeObject != null ? Selection.activeObject.name + " (" + Selection.activeObject.GetType().Name + ")" : "none"));
var logEntries = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
if (logEntries != null) sb.AppendLine("console entries: " + logEntries.GetMethod("GetCount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(null, null));
return sb.ToString();
