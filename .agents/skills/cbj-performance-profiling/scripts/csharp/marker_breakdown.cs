// execute_code body: finds the marker __MARKER__ anywhere in each frame (for example Application.Message, OnGUI,
// PlayerLoop) and compares its total and self time in spike frames (>= __SPIKE_MS__ ms) with normal frames, then
// lists its children in the spike frames. Self time far above the children means the cost is inside the marker
// itself (for Application.Message: the Windows message pump, not an Editor handler).
var sb = new System.Text.StringBuilder();
string marker = "__MARKER__";
float spikeMs = (float)(__SPIKE_MS__);
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
if (first < 0) return "no frames";
var children = new System.Collections.Generic.List<int>();
var stack = new System.Collections.Generic.Stack<int>();
var childTotals = new System.Collections.Generic.Dictionary<string, double>();
double[] frame = new double[2], markerTotal = new double[2], markerSelf = new double[2]; int[] n = new int[2];
for (int f = first; f <= last; f++)
{
    using (var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime, false))
    {
        if (view == null || !view.valid) continue;
        float ms = view.frameTimeMs;
        int g = ms >= spikeMs ? 1 : 0;
        n[g]++; frame[g] += ms;
        stack.Clear(); stack.Push(view.GetRootItemID());
        while (stack.Count > 0)
        {
            int id = stack.Pop();
            if (id != view.GetRootItemID() && view.GetItemName(id) == marker)
            {
                markerTotal[g] += view.GetItemColumnDataAsFloat(id, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime);
                markerSelf[g] += view.GetItemColumnDataAsFloat(id, UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime);
                if (g == 1)
                {
                    children.Clear(); view.GetItemChildren(id, children);
                    foreach (int c in children)
                    {
                        string nm = view.GetItemName(c);
                        double s; childTotals.TryGetValue(nm, out s);
                        childTotals[nm] = s + view.GetItemColumnDataAsFloat(c, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime);
                    }
                }
                continue;
            }
            children.Clear(); view.GetItemChildren(id, children);
            foreach (int c in children) stack.Push(c);
        }
    }
}
string[] names = { "normal", "spike" };
for (int g = 0; g < 2; g++)
{
    if (n[g] == 0) { sb.AppendLine(names[g] + ": none"); continue; }
    sb.AppendLine(names[g] + " frames " + n[g] + ": frame " + (frame[g] / n[g]).ToString("F1") + " ms, " + marker + " total " + (markerTotal[g] / n[g]).ToString("F1") + " ms, self " + (markerSelf[g] / n[g]).ToString("F1") + " ms");
}
var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(childTotals);
list.Sort((a, b) => b.Value.CompareTo(a.Value));
sb.AppendLine("children of " + marker + " in spike frames (total ms over all spikes):");
for (int i = 0; i < System.Math.Min(10, list.Count); i++) sb.AppendLine("  " + list[i].Value.ToString("F1") + "  " + list[i].Key);
return sb.ToString();
