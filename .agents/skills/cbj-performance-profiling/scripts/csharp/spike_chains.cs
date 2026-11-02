// execute_code body: for every recorded frame of at least __SPIKE_MS__ ms, follows the most expensive child from the
// root down while one child still dominates (>= 35 % of its parent) and totals the resulting chains. With
// profileEditor=true the chain names the Editor work inside EditorLoop (for example "EditorLoop > Application.Message").
// Also lists the spacing between spikes: a regular spacing points to periodic work.
var sb = new System.Text.StringBuilder();
float spikeMs = (float)(__SPIKE_MS__);
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
sb.AppendLine("frames " + first + ".." + last + " profileEditor=" + UnityEditorInternal.ProfilerDriver.profileEditor);
if (first < 0) return sb.ToString();
var children = new System.Collections.Generic.List<int>();
var chains = new System.Collections.Generic.Dictionary<string, double>();
var counts = new System.Collections.Generic.Dictionary<string, int>();
var gaps = new System.Collections.Generic.List<int>();
int frames = 0, spikes = 0, previous = -1; double total = 0;
for (int f = first; f <= last; f++)
{
    using (var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime, false))
    {
        if (view == null || !view.valid) continue;
        frames++;
        float ms = view.frameTimeMs; total += ms;
        if (ms < spikeMs) continue;
        spikes++;
        if (previous >= 0) gaps.Add(f - previous);
        previous = f;
        int current = view.GetRootItemID();
        string chain = "";
        for (int depth = 0; depth < 14; depth++)
        {
            children.Clear(); view.GetItemChildren(current, children);
            if (children.Count == 0) break;
            int best = children[0]; float bestTime = -1;
            foreach (int c in children) { float t = view.GetItemColumnDataAsFloat(c, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime); if (t > bestTime) { bestTime = t; best = c; } }
            float parentTime = current == view.GetRootItemID() ? ms : view.GetItemColumnDataAsFloat(current, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime);
            if (depth > 0 && bestTime < 0.35f * parentTime) break;
            chain += (chain.Length > 0 ? " > " : "") + view.GetItemName(best);
            current = best;
        }
        double sum; chains.TryGetValue(chain, out sum); chains[chain] = sum + ms;
        int count; counts.TryGetValue(chain, out count); counts[chain] = count + 1;
    }
}
sb.AppendLine("frames " + frames + ", avg " + (frames > 0 ? total / frames : 0).ToString("F2") + " ms, spikes >= " + spikeMs + " ms: " + spikes);
var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(chains);
list.Sort((a, b) => b.Value.CompareTo(a.Value));
for (int i = 0; i < System.Math.Min(10, list.Count); i++) sb.AppendLine("  " + list[i].Value.ToString("F0") + " ms x" + counts[list[i].Key] + "  " + list[i].Key);
sb.AppendLine("gaps between spikes (frames): " + string.Join(",", gaps));
return sb.ToString();
