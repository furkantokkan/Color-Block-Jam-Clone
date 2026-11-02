// COPY-ONLY GPU breakdown probe (cbj-performance-profiling skill). device_build.ps1 -GpuBreakdown copies it into
// Assets/DevProbe of a mirror copy instead of DevPerfProbe; it must never be added to the real project.
// Once, 8 s after the first scene loads, it applies one render configuration at a time, lets it settle for 1 s,
// measures 3 s and logs "CBJPERF config=<name> ...", then restores the original settings and logs "CBJPERF config done".
// Nobody should touch the phone while it runs: a held block adds the outline passes to whatever is being measured.
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public sealed class DevGpuBreakdown : MonoBehaviour
{
    private const float k_startDelay = 8f;
    private const float k_settle = 1f;
    private const float k_measure = 3f;

    private readonly List<KeyValuePair<string, Action<bool>>> m_configs = new List<KeyValuePair<string, Action<bool>>>();
    private readonly FrameTiming[] m_timing = new FrameTiming[1];
    private readonly float[] m_dts = new float[1024];
    private ProfilerRecorder m_main;
    private ProfilerRecorder m_setPass;
    private UniversalRenderPipelineAsset m_asset;
    private ScriptableRendererFeature m_ssao;
    private int m_msaa;
    private float m_scale;
    private float m_shadowDistance;
    private float m_clock;
    private int m_index = -1;
    private bool m_measuring;
    private int m_frames;
    private double m_dtSum, m_gpuSum, m_mainSum, m_setPassSum;
    private int m_gpuN;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        var go = new GameObject("DevGpuBreakdown");
        DontDestroyOnLoad(go);
        go.AddComponent<DevGpuBreakdown>();
    }

    private void OnEnable()
    {
        m_main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        m_setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
    }

    private void OnDisable()
    {
        m_main.Dispose();
        m_setPass.Dispose();
    }

    private void Start()
    {
        m_asset = (QualitySettings.renderPipeline ?? GraphicsSettings.currentRenderPipeline) as UniversalRenderPipelineAsset;
        if (m_asset == null) { Debug.Log("CBJPERF config error: no URP asset"); enabled = false; return; }
        m_msaa = m_asset.msaaSampleCount;
        m_scale = m_asset.renderScale;
        m_shadowDistance = m_asset.shadowDistance;
        var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        var list = field != null ? field.GetValue(m_asset) as ScriptableRendererData[] : null;
        if (list != null && list.Length > 0 && list[0] != null)
        {
            foreach (ScriptableRendererFeature feature in list[0].rendererFeatures)
            {
                if (feature != null && feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion")) m_ssao = feature;
            }
        }

        m_configs.Add(Config("baseline", on => { }));
        m_configs.Add(Config("ssao-off", on => { if (m_ssao != null) m_ssao.SetActive(!on); }));
        m_configs.Add(Config("msaa-1x", on => m_asset.msaaSampleCount = on ? 1 : m_msaa));
        m_configs.Add(Config("shadows-off", on => m_asset.shadowDistance = on ? 0f : m_shadowDistance));
        m_configs.Add(Config("render-scale-0.75", on => m_asset.renderScale = on ? 0.75f : m_scale));
        m_configs.Add(Config("hud-hidden", SetHudHidden));
        m_configs.Add(Config("ssao+msaa+shadows-off", on =>
        {
            if (m_ssao != null) m_ssao.SetActive(!on);
            m_asset.msaaSampleCount = on ? 1 : m_msaa;
            m_asset.shadowDistance = on ? 0f : m_shadowDistance;
        }));
        m_configs.Add(Config("baseline-again", on => { }));
        Debug.Log($"CBJPERF config start msaa={m_msaa} scale={m_scale} shadowDistance={m_shadowDistance} ssao={(m_ssao != null ? m_ssao.isActive.ToString() : "none")} screen={Screen.width}x{Screen.height}");
    }

    private static KeyValuePair<string, Action<bool>> Config(string name, Action<bool> apply)
    {
        return new KeyValuePair<string, Action<bool>>(name, apply);
    }

    private static void SetHudHidden(bool hidden)
    {
        foreach (UIDocument document in FindObjectsOfType<UIDocument>())
        {
            if (document.rootVisualElement != null) document.rootVisualElement.style.display = hidden ? DisplayStyle.None : StyleKeyword.Null;
        }
    }

    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        m_clock += Time.unscaledDeltaTime;
        if (m_index < 0)
        {
            if (m_clock < k_startDelay) return;
            Next();
            return;
        }

        if (!m_measuring)
        {
            if (m_clock >= k_settle) { m_measuring = true; m_clock = 0f; }
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (m_frames < m_dts.Length) m_dts[m_frames] = dt;
        m_frames++;
        m_dtSum += dt;
        if (m_main.Valid) m_mainSum += m_main.LastValue * 1e-6;
        if (m_setPass.Valid) m_setPassSum += m_setPass.LastValue;
        if (FrameTimingManager.GetLatestTimings(1, m_timing) > 0 && m_timing[0].gpuFrameTime > 0) { m_gpuSum += m_timing[0].gpuFrameTime; m_gpuN++; }
        if (m_clock >= k_measure) Finish();
    }

    private void Next()
    {
        m_index++;
        if (m_index >= m_configs.Count)
        {
            Debug.Log("CBJPERF config done");
            enabled = false;
            return;
        }

        m_configs[m_index].Value(true);
        m_clock = 0f;
        m_measuring = false;
        m_frames = 0; m_dtSum = 0; m_gpuSum = 0; m_gpuN = 0; m_mainSum = 0; m_setPassSum = 0;
    }

    private void Finish()
    {
        int n = Mathf.Max(1, Mathf.Min(m_frames, m_dts.Length));
        Array.Sort(m_dts, 0, n);
        int over20 = 0;
        for (int i = 0; i < n; i++) if (m_dts[i] > 0.020f) over20++;
        Debug.Log($"CBJPERF config={m_configs[m_index].Key} frames={m_frames} fps={m_frames / m_dtSum:F1} dtAvg={m_dtSum / m_frames * 1000:F2} p95={m_dts[(int)(n * 0.95f)] * 1000:F2} over20={over20} gpu={(m_gpuN > 0 ? m_gpuSum / m_gpuN : -1):F2} main={m_mainSum / m_frames:F2} setpass={m_setPassSum / m_frames:F0}");
        m_configs[m_index].Value(false);
        Next();
    }
}
