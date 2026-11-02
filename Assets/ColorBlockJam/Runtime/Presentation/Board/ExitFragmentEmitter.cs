using System;
using ColorBlockJam.Runtime.Art;
using Onity.Pooling;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// One pooled burst of gate-exit cube fragments: a Mesh-mode ParticleSystem built in code on Home
    /// that draws the shared board material in the exiting block's atlas colour through its own cached
    /// property block (PROJECT.md Performance Rule 1, DEC-131). Its owner plays, pauses, stops and
    /// returns it; it simulates only from Play until its last fragment dies or Stop clears it.
    /// </summary>
    public sealed class ExitFragmentEmitter : IPoolHooks, IDisposable
    {
        private readonly GameObject m_root;
        private readonly ParticleSystem m_system;
        private readonly ParticleSystemRenderer m_renderer;
        private readonly MaterialPropertyBlock m_properties = new MaterialPropertyBlock();

        public ExitFragmentEmitter(Mesh mesh, Material material, Transform storage)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            // Inactive before AddComponent, so the new system never wakes up with playOnAwake still on.
            m_root = new GameObject("Exit fragments");
            m_root.SetActive(false);
            m_root.transform.SetParent(storage, false);
            try
            {
                m_system = m_root.AddComponent<ParticleSystem>();
                m_renderer = m_root.GetComponent<ParticleSystemRenderer>();
                ConfigureSystem();
                ConfigureRenderer(mesh, material);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>True once the burst has stopped emitting and its last fragment has died, or after Stop.</summary>
        public bool IsFinished => !m_system.IsAlive(true);

        /// <summary>
        /// Starts one burst: the pose under its storage (on the door line, +Z out of the board), the lanes
        /// the block covers across the door, the exit motion's duration and the block's colour.
        /// </summary>
        public void Play(Vector3 localPosition, Quaternion localRotation, int lanes, float seconds, AtlasColor color)
        {
            m_root.transform.SetLocalPositionAndRotation(localPosition, localRotation);

            // A pooled emitter is stopped and cleared, the only state in which Unity accepts a new duration.
            ParticleSystem.MainModule main = m_system.main;
            main.duration = seconds;
            ParticleSystem.EmissionModule emission = m_system.emission;
            emission.rateOverTimeMultiplier = ExitFragmentSettings.k_ratePerLane * lanes;
            ParticleSystem.ShapeModule shape = m_system.shape;
            shape.scale = new Vector3(lanes, ExitFragmentSettings.k_boxHeight, ExitFragmentSettings.k_boxDepth);

            m_properties.SetVector(BoardLitShader.PaletteUVId, color.UV);
            m_renderer.SetPropertyBlock(m_properties);
            m_system.Play(true);
        }

        /// <summary>Suspend holds a live burst in place; Resume continues only a paused burst, never replays an ended one.</summary>
        public void SetPaused(bool paused)
        {
            if (paused)
            {
                if (m_system.IsAlive(true))
                {
                    m_system.Pause(true);
                }
            }
            else if (m_system.isPaused)
            {
                m_system.Play(true);
            }
        }

        /// <summary>Teardown: stops emitting and clears every live fragment at once.</summary>
        public void Stop()
        {
            // A scene unload can destroy the emitter before its owner tears down.
            if (m_system == null)
            {
                return;
            }

            m_system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void OnPoolGet()
        {
            if (m_root == null)
            {
                throw new InvalidOperationException("An exit fragment emitter was destroyed outside its owner.");
            }

            m_root.SetActive(true);
        }

        // A returned emitter is stopped, cleared and hidden, so its next Play starts clean.
        public void OnPoolRelease()
        {
            if (m_root == null)
            {
                return;
            }

            Stop();
            m_root.SetActive(false);
        }

        public void Dispose()
        {
            UnityObjects.Destroy(m_root);
        }

        // The reference values (ExitFragmentSettings), made non-looping so one exit is one burst. Local
        // simulation keeps the fragments in the door's frame; completion is polled through IsFinished even
        // off screen, so culling never keeps an emitter out of its pool.
        private void ConfigureSystem()
        {
            float fullTurn = 2f * Mathf.PI;
            float spin = ExitFragmentSettings.k_spinDegreesPerSecond * Mathf.Deg2Rad;

            ParticleSystem.MainModule main = m_system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = ExitFragmentSettings.k_lifetimeSeconds;
            main.startSpeed = ExitFragmentSettings.k_speed;
            main.startSize = new ParticleSystem.MinMaxCurve(ExitFragmentSettings.k_minSize, ExitFragmentSettings.k_maxSize);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, fullTurn);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, fullTurn);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, fullTurn);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = ExitFragmentSettings.k_maxParticles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = m_system.emission;
            emission.enabled = true;
            emission.rateOverTime = ExitFragmentSettings.k_ratePerLane;
            emission.rateOverDistance = 0f;

            // A Box emits along its local +Z, which Play turns out of the board.
            ParticleSystem.ShapeModule shape = m_system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.boxThickness = Vector3.zero;
            shape.scale = Vector3.one;
            shape.randomDirectionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;

            // Full size, 63 % at 73 % of the life, then nothing: the reference curve and its slopes.
            ParticleSystem.SizeOverLifetimeModule size = m_system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(0.73f, 0.63f, -1.9f, -1.9f),
                new Keyframe(1f, 0f, -3.8f, -3.8f)));

            ParticleSystem.RotationOverLifetimeModule rotation = m_system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-spin, spin);
            rotation.y = new ParticleSystem.MinMaxCurve(-spin, spin);
            rotation.z = new ParticleSystem.MinMaxCurve(-spin, spin);

            ParticleSystem.LimitVelocityOverLifetimeModule limit = m_system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = false;
            limit.limit = 0f;
            limit.dampen = ExitFragmentSettings.k_dampen;
        }

        private void ConfigureRenderer(Mesh mesh, Material material)
        {
            m_renderer.renderMode = ParticleSystemRenderMode.Mesh;
            m_renderer.mesh = mesh;
            m_renderer.alignment = ParticleSystemRenderSpace.Local;
            m_renderer.sharedMaterial = material;

            // Board Lit has no procedural particle instancing, so the burst draws through the CPU mesh path.
            m_renderer.enableGPUInstancing = false;
            m_renderer.shadowCastingMode = ShadowCastingMode.Off;
            m_renderer.receiveShadows = false;
            m_renderer.lightProbeUsage = LightProbeUsage.Off;
            m_renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            m_renderer.sortMode = ParticleSystemSortMode.None;

            // Play moves the UV onto the block colour's atlas cell, as BoardPieceView does (DEC-202).
            m_properties.SetFloat(BoardLitShader.ClipEnabledId, 0f);
            m_renderer.SetPropertyBlock(m_properties);
        }
    }
}
