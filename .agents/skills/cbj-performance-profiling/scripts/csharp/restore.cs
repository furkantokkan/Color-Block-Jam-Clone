// execute_code body: puts the Profiler back to the Editor defaults and frees the recorded frames.
// Profiler memory grows about 1 MB per recorded frame (1.5 GB after 1256 frames on 2026-09-29), so always run this.
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var settings = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.Profiling.ProfilerUserSettings");
var frameCount = settings.GetProperty("frameCount", flags);
frameCount.SetValue(null, 300);
typeof(UnityEditorInternal.ProfilerDriver).GetMethod("SetMaxFrameHistoryLength", flags).Invoke(null, new object[] { 300 });
UnityEditorInternal.ProfilerDriver.enabled = false;
UnityEditorInternal.ProfilerDriver.profileEditor = false;
UnityEditorInternal.ProfilerDriver.deepProfiling = false;
UnityEditorInternal.ProfilerDriver.ClearAllFrames();
var old = System.AppDomain.CurrentDomain.GetData("cbj.perf.hook") as System.Action<PlayModeStateChange>;
if (old != null) EditorApplication.playModeStateChanged -= old;
System.AppDomain.CurrentDomain.SetData("cbj.perf.hook", null);
System.GC.Collect();
return "restored: frameCount=" + frameCount.GetValue(null) + " enabled=" + UnityEditorInternal.ProfilerDriver.enabled
    + " frames=" + UnityEditorInternal.ProfilerDriver.firstFrameIndex + ".." + UnityEditorInternal.ProfilerDriver.lastFrameIndex;
