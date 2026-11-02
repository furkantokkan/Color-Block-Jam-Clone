// execute_code body: memory counters per recorded frame. A GC collection shows as a drop in "GC Used Memory" (so
// spikes without a drop are not GC); per-frame growth of the object and memory counters separates a real leak
// (Mesh/Texture/RenderTexture/Material counts rising) from the Profiler's own buffer ("Profiler Used Memory").
var sb = new System.Text.StringBuilder();
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
if (first < 0) return "no frames";
string[] names = { "GC Used Memory", "GC Allocated In Frame", "Total Used Memory", "Profiler Used Memory", "Gfx Used Memory", "Mesh Count", "Texture Count", "Render Textures Count", "Material Count", "Object Count", "Game Object Count" };
int n = last - first + 1;
var val = new long[names.Length, n];
var ms = new float[n];
for (int f = first; f <= last; f++)
{
    using (var v = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f, 0))
    {
        if (v == null || !v.valid) continue;
        ms[f - first] = v.frameTimeMs;
        for (int k = 0; k < names.Length; k++)
        {
            int id = v.GetMarkerId(names[k]);
            val[k, f - first] = id == UnityEditor.Profiling.FrameDataView.invalidMarkerId ? long.MinValue : v.GetCounterValueAsLong(id);
        }
    }
}
int spikes = 0, spikeDrops = 0, normals = 0, normalDrops = 0;
for (int i = 1; i < n; i++)
{
    bool drop = val[0, i] != long.MinValue && val[0, i - 1] != long.MinValue && val[0, i] < val[0, i - 1] - 256 * 1024;
    if (ms[i] >= 34.5f) { spikes++; if (drop) spikeDrops++; } else { normals++; if (drop) normalDrops++; }
}
sb.AppendLine("frames " + n + " | spikes >= 34.5 ms: " + spikes + " (GC Used drop > 256 KB in " + spikeDrops + ") | normal: " + normals + " (drop in " + normalDrops + ")");
for (int k = 1; k < names.Length; k++)
{
    long a = val[k, 1], b = val[k, n - 1];
    if (a == long.MinValue || b == long.MinValue) { sb.AppendLine(names[k] + ": n/a"); continue; }
    sb.AppendLine(names[k] + ": start " + a + " end " + b + " | per frame " + ((b - a) / (double)(n - 2)).ToString("F0"));
}
return sb.ToString();
