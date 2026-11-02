// execute_code body: splits recorded frames into "held" (a block held: the Highlight Plus outline adds 15+ SetPass
// calls) and "free", then compares frame time, the frame-time histogram and per-marker self time.
// Valid for a capture with profileEditor=false: with profileEditor=true the SetPass counter also counts Editor views,
// so every frame reads as held. Output: per-group summary, top self markers, held-minus-free deltas.
var sb = new System.Text.StringBuilder();
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
sb.AppendLine("frames " + first + ".." + last + " profileEditor=" + UnityEditorInternal.ProfilerDriver.profileEditor);
if (first < 0) return sb.ToString();
var setpass = new System.Collections.Generic.Dictionary<int, long>();
var frameMs = new System.Collections.Generic.Dictionary<int, float>();
long minSp = long.MaxValue;
for (int f = first; f <= last; f++)
{
    using (var v = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f, 0))
    {
        if (v == null || !v.valid) continue;
        int id = v.GetMarkerId("SetPass Calls Count");
        long sp = id == UnityEditor.Profiling.FrameDataView.invalidMarkerId ? -1 : v.GetCounterValueAsLong(id);
        setpass[f] = sp; frameMs[f] = v.frameTimeMs;
        if (sp > 0 && sp < minSp) minSp = sp;
    }
}
long threshold = minSp + 15;
sb.AppendLine("min setpass " + minSp + ", held when setpass >= " + threshold);
string[] groups = { "free", "held" };
var self = new System.Collections.Generic.Dictionary<string, double>[2];
int[] n = new int[2]; double[] ft = new double[2]; int[,] hist = new int[2, 5]; long[] spSum = new long[2];
for (int g = 0; g < 2; g++) self[g] = new System.Collections.Generic.Dictionary<string, double>();
var children = new System.Collections.Generic.List<int>();
var stack = new System.Collections.Generic.Stack<int>();
foreach (var kv in setpass)
{
    int f = kv.Key; if (kv.Value <= 0) continue;
    int g = kv.Value >= threshold ? 1 : 0;
    float ms = frameMs[f];
    n[g]++; ft[g] += ms; spSum[g] += kv.Value;
    hist[g, ms < 17.5f ? 0 : ms < 25f ? 1 : ms < 34.5f ? 2 : ms < 50f ? 3 : 4]++;
    using (var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime, false))
    {
        if (view == null || !view.valid) continue;
        stack.Clear(); stack.Push(view.GetRootItemID());
        while (stack.Count > 0)
        {
            int id = stack.Pop();
            if (id != view.GetRootItemID())
            {
                string nm = view.GetItemName(id);
                double s; self[g].TryGetValue(nm, out s);
                self[g][nm] = s + view.GetItemColumnDataAsFloat(id, UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime);
            }
            children.Clear(); view.GetItemChildren(id, children);
            foreach (int c in children) stack.Push(c);
        }
    }
}
for (int g = 0; g < 2; g++)
{
    if (n[g] == 0) { sb.AppendLine("== " + groups[g] + ": no frames"); continue; }
    sb.AppendLine("== " + groups[g] + ": " + n[g] + " frames, avg " + (ft[g] / n[g]).ToString("F2") + " ms, setpass " + (spSum[g] / n[g]) + " | hist <17.5:" + hist[g, 0] + " 17.5-25:" + hist[g, 1] + " 25-34.5:" + hist[g, 2] + " 34.5-50:" + hist[g, 3] + " >50:" + hist[g, 4]);
    var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(self[g]);
    list.Sort((a, b) => b.Value.CompareTo(a.Value));
    for (int i = 0; i < System.Math.Min(15, list.Count); i++) sb.AppendLine("    " + (list[i].Value / n[g]).ToString("F3") + "  " + list[i].Key);
}
if (n[0] > 0 && n[1] > 0)
{
    var diff = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>();
    foreach (var kv in self[1]) { double a; self[0].TryGetValue(kv.Key, out a); diff.Add(new System.Collections.Generic.KeyValuePair<string, double>(kv.Key, kv.Value / n[1] - a / n[0])); }
    diff.Sort((a, b) => b.Value.CompareTo(a.Value));
    sb.AppendLine("== held minus free, self ms/frame:");
    for (int i = 0; i < System.Math.Min(12, diff.Count); i++) sb.AppendLine("    +" + diff[i].Value.ToString("F3") + "  " + diff[i].Key);
}
return sb.ToString();
