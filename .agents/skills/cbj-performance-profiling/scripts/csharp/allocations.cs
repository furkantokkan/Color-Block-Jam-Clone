// execute_code body: totals GC.Alloc samples by their parent path. __SCOPE__ = PlayerLoop walks only the subtrees of
// PlayerLoop nodes wherever they sit (with profileEditor=true PlayerLoop is nested inside EditorLoop, so a root-only
// search finds nothing and wrongly reports zero); __SCOPE__ = All walks the whole frame (use it to prove the scan
// works: Editor painting always allocates). Performance Rule 5 wants 0 B per Playing frame in the game's own code.
// Known Editor-only allocation: PlayerLoop > GUI.Repaint/ProcessEvents > GUIUtility.BeginGUI comes from
// UIDocument.OnGUI, which the Android player does not have.
var sb = new System.Text.StringBuilder();
bool playerOnly = "__SCOPE__" == "PlayerLoop";
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
if (first < 0) return "no frames";
var children = new System.Collections.Generic.List<int>();
var kids = new System.Collections.Generic.List<int>();
var bytes = new System.Collections.Generic.Dictionary<string, double>();
var count = new System.Collections.Generic.Dictionary<string, double>();
var stack = new System.Collections.Generic.Stack<System.ValueTuple<int, string>>();
var find = new System.Collections.Generic.Stack<int>();
int frames = 0;
for (int f = first + 30; f <= last; f++)
{
    using (var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory, false))
    {
        if (view == null || !view.valid) continue;
        frames++;
        stack.Clear(); find.Clear();
        children.Clear(); view.GetItemChildren(view.GetRootItemID(), children);
        foreach (int c in children) find.Push(c);
        while (find.Count > 0)
        {
            int x = find.Pop();
            string nm = view.GetItemName(x);
            if (!playerOnly || nm == "PlayerLoop") { stack.Push((x, nm)); continue; }
            kids.Clear(); view.GetItemChildren(x, kids);
            foreach (int k in kids) find.Push(k);
        }
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            children.Clear(); view.GetItemChildren(item.Item1, children);
            foreach (int c in children)
            {
                string nm = view.GetItemName(c);
                if (nm == "GC.Alloc")
                {
                    double b; bytes.TryGetValue(item.Item2, out b); bytes[item.Item2] = b + view.GetItemColumnDataAsFloat(c, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory);
                    double k; count.TryGetValue(item.Item2, out k); count[item.Item2] = k + view.GetItemColumnDataAsFloat(c, UnityEditor.Profiling.HierarchyFrameDataView.columnCalls);
                }
                else if (view.GetItemColumnDataAsFloat(c, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory) > 0)
                {
                    stack.Push((c, item.Item2 + " > " + nm));
                }
            }
        }
    }
}
var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(bytes);
list.Sort((a, b) => b.Value.CompareTo(a.Value));
sb.AppendLine("frames " + frames + ", scope " + (playerOnly ? "PlayerLoop" : "All") + "; GC.Alloc by parent (bytes/frame, allocs/frame):");
if (list.Count == 0) sb.AppendLine("  none");
for (int i = 0; i < System.Math.Min(15, list.Count); i++)
{
    string p = list[i].Key.Replace("UnityEngine.CoreModule.dll!UnityEngine.Rendering::RenderPipelineManager.DoRenderLoop_Internal() [Invoke] > Inl_UniversalRenderTotal > Inl_RenderCameraStack > ", "URP > ");
    if (p.Length > 320) p = "..." + p.Substring(p.Length - 320);
    sb.AppendLine("  " + (list[i].Value / frames).ToString("F0") + " B  " + (count[list[i].Key] / frames).ToString("F2") + "  " + p);
}
return sb.ToString();
