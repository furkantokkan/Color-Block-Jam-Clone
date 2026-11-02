#if UNITY_EDITOR
using System.Collections;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using UnityEngine.TestTools.Utils;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the pooled gate-exit fragment emitters (DEC-131, AC-3.18): a capped warm-up on Home, a
    /// burst in its block's colour on the shared material with no instance, fragments flying out of the
    /// board, pause and resume, stop and return, a full pool that skips instead of growing, and zero
    /// allocation on the play path.
    /// </summary>
    public sealed class ExitFragmentsTests
    {
        private static readonly int s_paletteUVId = Shader.PropertyToID("_PaletteUV");
        private static readonly int s_clipEnabledId = Shader.PropertyToID("_ClipEnabled");

        // A top-door burst in BoardRoot-local units: column 1, the door line of a three-row board.
        private static readonly Vector3 s_doorLine = new Vector3(1f, 0.28f, 2.75f);

        private GameObject m_storage;
        private BoardArt m_art;
        private ExitFragments m_fragments;

        // The first allocation assertion in a domain also measures the GC.Alloc recorder's own setup
        // (ZeroAllocationTests), so one empty measurement runs first.
        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [SetUp]
        public void SetUp()
        {
            m_storage = new GameObject("Exit fragment storage");
            m_art = PresenterScene.CreateArt();
            m_fragments = ExitFragments.Create(m_art, m_storage.transform);
        }

        [TearDown]
        public void TearDown()
        {
            m_fragments.Dispose();
            Object.Destroy(m_storage);
        }

        [Test]
        public void PrepareWarmsAtMostTheCapOnceAndKeepsTheEmittersHidden()
        {
            m_fragments.Prepare(10);

            Assert.That(m_fragments.PreparedCount, Is.EqualTo(ExitFragmentSettings.k_maxEmitters));
            Assert.That(m_storage.transform.childCount, Is.EqualTo(ExitFragmentSettings.k_maxEmitters));
            AssertEveryEmitterHidden();

            m_fragments.Prepare(2);
            m_fragments.Prepare(ExitFragmentSettings.k_maxEmitters);

            Assert.That(m_storage.transform.childCount, Is.EqualTo(ExitFragmentSettings.k_maxEmitters),
                "Restart and smaller levels create nothing.");
            Assert.That(m_fragments.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ABurstDrawsTheSharedMaterialInItsBlockColourAndFliesOutOfTheBoard()
        {
            m_fragments.Prepare(1);
            AtlasColor red = m_art.GetAtlasColor(PaletteColor.Red);

            Assert.That(m_fragments.TryPlay(s_doorLine, Quaternion.identity, 2, 0.3f, red), Is.True);

            ParticleSystem system = m_storage.GetComponentInChildren<ParticleSystem>(false);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            Assert.That(system.transform.localPosition, Is.EqualTo(s_doorLine));
            Assert.That(system.isPlaying, Is.True);
            Assert.That(renderer.sharedMaterial, Is.SameAs(m_art.Catalog.Get(BlockPieceKind.Center).Renderer.sharedMaterial),
                "the shared board material, not an instance");
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetVector(s_paletteUVId), Is.EqualTo(red.UV), "the block colour's atlas cell");
            Assert.That(properties.GetFloat(s_clipEnabledId), Is.Zero, "fragments are never clipped at the door");

            yield return new WaitForSeconds(0.15f);

            // Local simulation: the emitter's +Z is outward, and every fragment moves that way.
            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[ExitFragmentSettings.k_maxParticles];
            int count = system.GetParticles(particles);
            Assert.That(count, Is.GreaterThan(0));
            float farthest = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Assert.That(particles[i].velocity.z, Is.GreaterThanOrEqualTo(0f), "no fragment flies back into the board");
                farthest = Mathf.Max(farthest, particles[i].position.z);
            }

            Assert.That(farthest, Is.GreaterThan(0.3f), "the first fragments have left the door line");
        }

        [UnityTest]
        public IEnumerator ABurstReturnsToThePoolOnceItsLastFragmentDies()
        {
            m_fragments.Prepare(1);
            Assert.That(m_fragments.TryPlay(s_doorLine, Quaternion.identity, 1, 0.2f, AtlasColor.FromCell(Vector2Int.zero)), Is.True);

            m_fragments.Sync();
            Assert.That(m_fragments.ActiveCount, Is.EqualTo(1), "a playing burst stays out of the pool");

            float deadline = Time.realtimeSinceStartup + 0.2f + ExitFragmentSettings.k_lifetimeSeconds + 1f;
            while (m_fragments.ActiveCount > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                m_fragments.Sync();
            }

            Assert.That(m_fragments.ActiveCount, Is.Zero, "the burst returned within its duration and lifetime");
            AssertEveryEmitterHidden();
        }

        [Test]
        public void AFullPoolSkipsTheBurstInsteadOfGrowing()
        {
            m_fragments.Prepare(1);

            Assert.That(m_fragments.TryPlay(s_doorLine, Quaternion.identity, 1, 0.3f, AtlasColor.FromCell(Vector2Int.zero)), Is.True);
            Assert.That(m_fragments.TryPlay(s_doorLine, Quaternion.identity, 1, 0.3f, AtlasColor.FromCell(Vector2Int.zero)), Is.False);

            Assert.That(m_fragments.ActiveCount, Is.EqualTo(1));
            Assert.That(m_storage.transform.childCount, Is.EqualTo(1), "nothing was created while playing");
        }

        [UnityTest]
        public IEnumerator PauseHoldsTheBurstAndResumeContinuesIt()
        {
            m_fragments.Prepare(1);
            m_fragments.TryPlay(s_doorLine, Quaternion.identity, 1, 0.5f, AtlasColor.FromCell(Vector2Int.zero));
            ParticleSystem system = m_storage.GetComponentInChildren<ParticleSystem>(false);
            yield return null;
            yield return null;

            m_fragments.SetPaused(true);
            float held = system.time;
            yield return new WaitForSeconds(0.1f);

            Assert.That(system.isPaused, Is.True);
            Assert.That(system.time, Is.EqualTo(held));

            m_fragments.SetPaused(false);
            Assert.That(system.time, Is.EqualTo(held), "Resume continues from the paused position, not from the start");
            yield return new WaitForSeconds(0.05f);

            Assert.That(system.isPlaying, Is.True);
            Assert.That(system.time, Is.GreaterThan(held));
        }

        [UnityTest]
        public IEnumerator ResumingAnEndedBurstDoesNotReplayIt()
        {
            m_fragments.Prepare(1);
            m_fragments.TryPlay(s_doorLine, Quaternion.identity, 1, 0.2f, AtlasColor.FromCell(Vector2Int.zero));
            ParticleSystem system = m_storage.GetComponentInChildren<ParticleSystem>(false);

            float deadline = Time.realtimeSinceStartup + 0.2f + ExitFragmentSettings.k_lifetimeSeconds + 1f;
            while (system.IsAlive(true) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            // Ended, but no Sync has returned it yet: a Suspend and Resume in between must not restart it.
            Assert.That(system.IsAlive(true), Is.False);
            m_fragments.SetPaused(true);
            m_fragments.SetPaused(false);

            Assert.That(system.IsAlive(true), Is.False);
            Assert.That(system.isEmitting, Is.False);
        }

        [UnityTest]
        public IEnumerator StopClearsEveryBurstAndReturnAllHidesThem()
        {
            m_fragments.Prepare(2);
            m_fragments.TryPlay(s_doorLine, Quaternion.identity, 1, 0.5f, AtlasColor.FromCell(Vector2Int.zero));
            m_fragments.TryPlay(s_doorLine + Vector3.right, Quaternion.identity, 2, 0.5f, AtlasColor.FromCell(Vector2Int.zero));
            yield return new WaitForSeconds(0.05f);

            m_fragments.Stop();

            foreach (ParticleSystem system in m_storage.GetComponentsInChildren<ParticleSystem>(false))
            {
                Assert.That(system.particleCount, Is.Zero);
                Assert.That(system.isPlaying, Is.False);
            }

            m_fragments.ReturnAll();

            Assert.That(m_fragments.ActiveCount, Is.Zero);
            AssertEveryEmitterHidden();
        }

        [Test]
        public void PlayingPausingStoppingAndReturningAllocateNothing()
        {
            m_fragments.Prepare(1);
            PlayPauseStopReturn();

            Assert.That(() => PlayPauseStopReturn(), Is.Not.AllocatingGCMemory());
        }

        private void PlayPauseStopReturn()
        {
            m_fragments.TryPlay(s_doorLine, Quaternion.identity, 2, 0.3f, m_art.GetAtlasColor(PaletteColor.Red));
            m_fragments.SetPaused(true);
            m_fragments.SetPaused(false);
            m_fragments.Sync();
            m_fragments.Stop();
            m_fragments.ReturnAll();
        }

        private void AssertEveryEmitterHidden()
        {
            foreach (Transform child in m_storage.transform)
            {
                Assert.That(child.gameObject.activeSelf, Is.False, child.name + " is back in its pool");
            }
        }
    }
}
#endif
