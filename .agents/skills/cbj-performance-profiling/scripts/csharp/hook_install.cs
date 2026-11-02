// execute_code body: records the next Play session with the Profiler, then stops, so the frames stay for analysis.
// Tokens: __FRAMES__ (history length, 300-2000) and __PROFILE_EDITOR__ (true shows inside EditorLoop).
// Recording starts on EnteredPlayMode and stops only on an ExitingPlayMode that follows it. The LevelEditor Play
// guard cancels a plain Play by firing ExitingPlayMode first; a one-shot hook that stopped there lost the session.
// Domain reload is disabled for Play Mode in this project, so the delegate survives the transition. A script
// recompile (domain reload) removes it: run state.cs before asking for the repro.
const string key = "cbj.perf.hook";
int frames = __FRAMES__;
bool profileEditor = __PROFILE_EDITOR__;
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var settings = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.Profiling.ProfilerUserSettings");
settings.GetProperty("frameCount", flags).SetValue(null, frames);
typeof(UnityEditorInternal.ProfilerDriver).GetMethod("SetMaxFrameHistoryLength", flags).Invoke(null, new object[] { frames });
UnityEditorInternal.ProfilerDriver.enabled = false;
UnityEditorInternal.ProfilerDriver.ClearAllFrames();
var old = System.AppDomain.CurrentDomain.GetData(key) as System.Action<PlayModeStateChange>;
if (old != null) EditorApplication.playModeStateChanged -= old;
bool recording = false;
System.Action<PlayModeStateChange> hook = null;
hook = state =>
{
    if (state == PlayModeStateChange.EnteredPlayMode && !recording)
    {
        recording = true;
        UnityEditorInternal.ProfilerDriver.ClearAllFrames();
        UnityEditorInternal.ProfilerDriver.deepProfiling = false;
        UnityEditorInternal.ProfilerDriver.profileEditor = profileEditor;
        UnityEditorInternal.ProfilerDriver.enabled = true;
        Debug.Log("[cbj perf] Profiler recording started (profileEditor=" + profileEditor + ").");
    }
    else if (state == PlayModeStateChange.ExitingPlayMode && recording)
    {
        UnityEditorInternal.ProfilerDriver.enabled = false;
        EditorApplication.playModeStateChanged -= hook;
        System.AppDomain.CurrentDomain.SetData(key, null);
        Debug.Log("[cbj perf] Profiler recording stopped; frames kept for analysis.");
    }
};
EditorApplication.playModeStateChanged += hook;
System.AppDomain.CurrentDomain.SetData(key, hook);
return "hook installed: history " + frames + " frames, profileEditor=" + profileEditor + ", playing=" + EditorApplication.isPlaying;
