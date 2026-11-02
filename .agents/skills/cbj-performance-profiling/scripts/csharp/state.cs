// execute_code body: proves which Editor answered and reports play, profiler and frame-rate state.
var hook = System.AppDomain.CurrentDomain.GetData("cbj.perf.hook");
return Application.dataPath
    + " | playing=" + EditorApplication.isPlaying
    + " | scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    + " | profiler enabled=" + UnityEditorInternal.ProfilerDriver.enabled
    + " profileEditor=" + UnityEditorInternal.ProfilerDriver.profileEditor
    + " frames=" + UnityEditorInternal.ProfilerDriver.firstFrameIndex + ".." + UnityEditorInternal.ProfilerDriver.lastFrameIndex
    + " hook=" + (hook != null)
    + " | targetFPS=" + Application.targetFrameRate + " vsync=" + QualitySettings.vSyncCount
    + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]
    + " | playModeOptions=" + (EditorSettings.enterPlayModeOptionsEnabled ? EditorSettings.enterPlayModeOptions.ToString() : "off")
    + " | editorFocused=" + UnityEditorInternal.InternalEditorUtility.isApplicationActive;
