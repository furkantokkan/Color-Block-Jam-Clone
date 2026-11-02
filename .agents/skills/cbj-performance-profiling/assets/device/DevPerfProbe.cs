// COPY-ONLY device probe for the drag performance check (cbj-performance-profiling skill). device_build.ps1 copies it
// into Assets/DevProbe of a mirror copy; it must never be added to the real project.
// Logs one "CBJPERF" line per window: a window ends when the touch state changes or after 120 frames.
// With s_outlineAB (device_build.ps1 -OutlineAB) every second touch disables all HighlightEffect components to
// measure the outline; it is off by default because a tester reads the missing outline as a bug. Block screen
// positions and the pixels per board cell are logged 4 s after every scene change so adb swipes can target them.
using System.Collections.Generic;
using HighlightPlus;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class DevPerfProbe : MonoBehaviour
{
    private const int k_windowFrames = 120;
    private static readonly bool s_outlineAB = false;

    private ProfilerRecorder m_main;
    private ProfilerRecorder m_gc;
    private ProfilerRecorder m_gcCount;
    private ProfilerRecorder m_setPass;
    private ProfilerRecorder m_batches;
    private ProfilerRecorder m_triangles;
    private readonly FrameTiming[] m_timing = new FrameTiming[1];
    private readonly float[] m_dts = new float[k_windowFrames + 8];

    private int m_frames;
    private double m_dtSum;
    private double m_mainSum;
    private long m_gcSum;
    private long m_gcCountSum;
    private long m_setPassSum;
    private long m_batchSum;
    private long m_triSum;
    private double m_gpuSum;
    private int m_gpuN;
    private double m_cpuSum;
    private int m_cpuN;
    private bool m_windowHeld;
    private bool m_outlineOn = true;
    private int m_presses;
    private bool m_wasPressed;
    private bool m_loggedBlocks;
    private float m_sceneTime;
    private float m_nextBlockScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        var go = new GameObject("DevPerfProbe");
        DontDestroyOnLoad(go);
        go.AddComponent<DevPerfProbe>();
    }

    private void OnEnable()
    {
        m_main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        m_gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        m_gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count", 1);
        m_setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
        m_batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
        m_triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Debug.Log($"CBJPERF device model={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} cpu={SystemInfo.processorType} x{SystemInfo.processorCount} mem={SystemInfo.systemMemorySize}MB screen={Screen.width}x{Screen.height} system={Display.main.systemWidth}x{Display.main.systemHeight} refresh={Screen.currentResolution.refreshRateRatio.value:F0} targetFps={Application.targetFrameRate} vsync={QualitySettings.vSyncCount} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} input=new-only:{!IsLegacyInputEnabled()}");
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        m_main.Dispose(); m_gc.Dispose(); m_gcCount.Dispose(); m_setPass.Dispose(); m_batches.Dispose(); m_triangles.Dispose();
    }

    // Init -> Hub -> Game: log the blocks again for each scene that has them.
    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        m_loggedBlocks = false;
        m_sceneTime = 0f;
        m_nextBlockScan = 0f;
        Debug.Log("CBJPERF scene " + next.name);
    }

    private static bool IsLegacyInputEnabled()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return true;
#else
        return false;
#endif
    }

    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        m_sceneTime += Time.unscaledDeltaTime;

        bool pressed = Pointer.current != null && Pointer.current.press.isPressed;
        if (pressed && !m_wasPressed)
        {
            m_presses++;
            if (s_outlineAB)
            {
                m_outlineOn = (m_presses % 2) == 1; // press 1, 3, 5 ... keep the outline; 2, 4, 6 ... disable it
                SetOutlines(m_outlineOn);
            }
        }
        m_wasPressed = pressed;

        if (pressed != m_windowHeld || m_frames >= k_windowFrames)
        {
            Flush();
            m_windowHeld = pressed;
        }

        float dt = Time.unscaledDeltaTime;
        m_dts[m_frames] = dt;
        m_frames++;
        m_dtSum += dt;
        if (m_main.Valid) m_mainSum += m_main.LastValue * 1e-6;
        if (m_gc.Valid) m_gcSum += m_gc.LastValue;
        if (m_gcCount.Valid) m_gcCountSum += m_gcCount.LastValue;
        if (m_setPass.Valid) m_setPassSum += m_setPass.LastValue;
        if (m_batches.Valid) m_batchSum += m_batches.LastValue;
        if (m_triangles.Valid) m_triSum += m_triangles.LastValue;
        if (FrameTimingManager.GetLatestTimings(1, m_timing) > 0)
        {
            if (m_timing[0].gpuFrameTime > 0) { m_gpuSum += m_timing[0].gpuFrameTime; m_gpuN++; }
            if (m_timing[0].cpuFrameTime > 0) { m_cpuSum += m_timing[0].cpuFrameTime; m_cpuN++; }
        }

        // Scans once a second until a scene with blocks is found, so Hub frames are not charged a scan each.
        if (!m_loggedBlocks && m_sceneTime > 4f && m_sceneTime >= m_nextBlockScan)
        {
            m_nextBlockScan = m_sceneTime + 1f;
            LogBlocks();
        }
    }

    private void SetOutlines(bool on)
    {
        foreach (HighlightEffect effect in FindObjectsOfType<HighlightEffect>(true)) effect.enabled = on;
    }

    private void Flush()
    {
        if (m_frames == 0) return;
        System.Array.Sort(m_dts, 0, m_frames);
        float p95 = m_dts[Mathf.Min(m_frames - 1, (int)(m_frames * 0.95f))] * 1000f;
        float max = m_dts[m_frames - 1] * 1000f;
        int over20 = 0, over34 = 0;
        for (int i = 0; i < m_frames; i++) { if (m_dts[i] > 0.020f) over20++; if (m_dts[i] > 0.034f) over34++; }
        double n = m_frames;
        Debug.Log($"CBJPERF window held={m_windowHeld} outline={(m_windowHeld ? (m_outlineOn ? "on" : "off") : "-")} frames={m_frames} fps={n / m_dtSum:F1} dtAvg={m_dtSum / n * 1000:F2} p95={p95:F2} max={max:F2} over20={over20} over34={over34} main={m_mainSum / n:F2} cpuFrame={(m_cpuN > 0 ? m_cpuSum / m_cpuN : -1):F2} gpu={(m_gpuN > 0 ? m_gpuSum / m_gpuN : -1):F2} gcB={m_gcSum / n:F0} gcN={m_gcCountSum / n:F2} setpass={m_setPassSum / n:F0} batches={m_batchSum / n:F0} tris={m_triSum / n:F0}");
        m_frames = 0; m_dtSum = 0; m_mainSum = 0; m_gcSum = 0; m_gcCountSum = 0; m_setPassSum = 0; m_batchSum = 0; m_triSum = 0;
        m_gpuSum = 0; m_gpuN = 0; m_cpuSum = 0; m_cpuN = 0;
    }

    // Screen position of every block root (type name BlockPiece), adb coordinates (origin top-left, physical pixels).
    private void LogBlocks()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        float scaleX = (float)Display.main.systemWidth / Screen.width;
        float scaleY = (float)Display.main.systemHeight / Screen.height;
        var found = new List<string>();
        foreach (MonoBehaviour behaviour in FindObjectsOfType<MonoBehaviour>())
        {
            if (behaviour.GetType().Name != "BlockPiece" || !behaviour.gameObject.activeInHierarchy) continue;
            Renderer[] renderers = behaviour.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) continue;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 min = camera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
            Vector3 max = camera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));
            Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Vector3 center = camera.WorldToScreenPoint(top);
            // A board cell spans one local unit of the block root's parent (IBoardLayout).
            Transform parent = behaviour.transform.parent;
            Vector3 unit = parent != null ? parent.TransformVector(Vector3.right) : Vector3.right;
            float cellPx = (camera.WorldToScreenPoint(top + unit) - center).magnitude * scaleX;
            found.Add($"{behaviour.name} adb=({center.x * scaleX:F0},{(Screen.height - center.y) * scaleY:F0}) sizePx=({Mathf.Abs(max.x - min.x) * scaleX:F0}x{Mathf.Abs(max.y - min.y) * scaleY:F0}) renderers={renderers.Length} cellPx={cellPx:F0}");
        }
        if (found.Count == 0) return;
        m_loggedBlocks = true;
        Debug.Log("CBJPERF blocks " + found.Count + " | " + string.Join(" | ", found));
    }
}
