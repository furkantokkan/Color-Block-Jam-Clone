#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Boosters;
using NUnit.Framework;
using PrimeTween;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.PlayMode
{
    public sealed class BoosterEffectViewTests
    {
        private const string k_prefabFolder = "Assets/ColorBlockJam/Prefabs/Boosters/Effects/";
        private readonly List<BoosterEffectView> m_views = new List<BoosterEffectView>();

        [TearDown]
        public void TearDown()
        {
            foreach (BoosterEffectView view in m_views)
            {
                if (view != null)
                {
                    view.Stop();
                    Object.DestroyImmediate(view.gameObject);
                }
            }
            m_views.Clear();
        }

        [UnityTest]
        public IEnumerator PrefabsStayIdleUntilExplicitPlay()
        {
            CreateAll();
            yield return new WaitForSeconds(0.15f);
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(view.IsPlaying, Is.False, view.name);
                Assert.That(view.IsPaused, Is.False, view.name);
                Assert.That(Visual(view).gameObject.activeSelf, Is.False, view.name);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
                Assert.That(view.Duration, Is.InRange(0.1f, 0.8f), view.name);
            }
        }

        [UnityTest]
        public IEnumerator RealTimeAdvancesEveryVisualAndCompletesWithinBoundedDuration()
        {
            CreateAll();
            var starts = new List<VisualState>();
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(Play(view), Is.True, view.name);
                starts.Add(new VisualState(Visual(view)));
                Assert.That(Tween.GetTweensCount(view), Is.EqualTo(1), view.name);
            }
            SpriteRenderer vacuumRenderer = m_views[2].GetComponentInChildren<SpriteRenderer>(true);
            Sprite firstFrame = vacuumRenderer.sprite;

            yield return new WaitForSeconds(0.2f);
            Assert.That(vacuumRenderer.sprite, Is.Not.SameAs(firstFrame), "Vacuum frames did not advance in real time");
            for (int i = 0; i < m_views.Count; i++)
            {
                BoosterEffectView view = m_views[i];
                Assert.That(view.IsPlaying, Is.True, view.name);
                Assert.That(starts[i].Matches(Visual(view)), Is.False, view.name + " never advanced");
            }

            yield return new WaitForSeconds(0.65f);
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(view.IsPlaying, Is.False, view.name);
                Assert.That(Visual(view).gameObject.activeSelf, Is.False, view.name);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
            }
        }

        [UnityTest]
        public IEnumerator SuspendFreezesPoseAndSpriteUntilExplicitResume()
        {
            CreateAll();
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(Play(view), Is.True);
            }
            yield return new WaitForSeconds(0.2f);

            var pausedStates = new List<VisualState>();
            foreach (BoosterEffectView view in m_views)
            {
                view.Suspend();
                pausedStates.Add(new VisualState(Visual(view)));
                Assert.That(view.IsPaused, Is.True, view.name);
            }
            yield return new WaitForSeconds(0.9f);

            for (int i = 0; i < m_views.Count; i++)
            {
                BoosterEffectView view = m_views[i];
                Assert.That(view.IsPlaying, Is.True, view.name);
                Assert.That(pausedStates[i].Matches(Visual(view)), Is.True, view.name + " changed while suspended");
                view.Resume();
                Assert.That(view.IsPaused, Is.False, view.name);
            }
            yield return new WaitForSeconds(0.15f);
            for (int i = 0; i < m_views.Count; i++)
            {
                Assert.That(pausedStates[i].Matches(Visual(m_views[i])), Is.False, m_views[i].name + " did not resume");
            }
            yield return new WaitForSeconds(0.55f);
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(view.IsPlaying, Is.False, view.name);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
            }
        }

        [UnityTest]
        public IEnumerator ReplayReplacesOwnedTweenAndStopRestoresRestPose()
        {
            CreateAll();
            var rests = new List<VisualState>();
            var starts = new List<VisualState>();
            foreach (BoosterEffectView view in m_views)
            {
                rests.Add(new VisualState(Visual(view)));
                Assert.That(Play(view), Is.True);
                starts.Add(new VisualState(Visual(view)));
            }
            yield return new WaitForSeconds(0.22f);

            for (int i = 0; i < m_views.Count; i++)
            {
                BoosterEffectView view = m_views[i];
                Assert.That(Play(view), Is.True);
                Assert.That(Tween.GetTweensCount(view), Is.EqualTo(1), view.name);
                Assert.That(starts[i].Matches(Visual(view)), Is.True, view.name + " replay did not restart");
                view.Stop();
                Assert.That(rests[i].Matches(Visual(view)), Is.True, view.name + " did not reset");
                Assert.That(Visual(view).gameObject.activeSelf, Is.False, view.name);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
            }
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < m_views.Count; i++)
            {
                Assert.That(rests[i].Matches(Visual(m_views[i])), Is.True, m_views[i].name + " moved after Stop");
            }
        }

        [UnityTest]
        public IEnumerator DisablingRootStopsWorkAndReenablingDoesNotAutoplay()
        {
            CreateAll();
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(Play(view), Is.True);
            }
            yield return new WaitForSeconds(0.15f);
            foreach (BoosterEffectView view in m_views)
            {
                view.gameObject.SetActive(false);
                Assert.That(view.IsPlaying, Is.False, view.name);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
                Assert.That(Play(view), Is.False, view.name + " played while disabled");
                view.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(0.15f);
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(view.IsPlaying, Is.False, view.name);
                Assert.That(Visual(view).gameObject.activeSelf, Is.False, view.name);
                Assert.That(Tween.GetTweensCount(view), Is.Zero, view.name);
            }
        }

        [UnityTest]
        public IEnumerator ApplicationPauseLatchesSuspensionUntilOwnerResumes()
        {
            CreateAll();
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(Play(view), Is.True);
            }
            yield return new WaitForSeconds(0.15f);
            var states = new List<VisualState>();
            foreach (BoosterEffectView view in m_views)
            {
                view.SendMessage("OnApplicationPause", true);
                states.Add(new VisualState(Visual(view)));
                view.SendMessage("OnApplicationPause", false);
            }
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < m_views.Count; i++)
            {
                BoosterEffectView view = m_views[i];
                Assert.That(view.IsPaused, Is.True, view.name);
                Assert.That(states[i].Matches(Visual(view)), Is.True, view.name);
                view.Resume();
                Assert.That(view.IsPaused, Is.False, view.name);
            }
        }

        [UnityTest]
        public IEnumerator InvalidRocketDestinationsPreserveTheCurrentFlight()
        {
            var rocket = (RocketEffectView)Create("RocketPurple");
            Vector3 destination = new Vector3(2f, 1f, 4f);
            Assert.That(rocket.Play(destination), Is.True);
            yield return new WaitForSeconds(0.15f);
            rocket.Suspend();
            var state = new VisualState(Visual(rocket));
            Vector3[] invalidDestinations =
            {
                rocket.transform.position,
                new Vector3(float.NaN, 0f, 0f),
                new Vector3(0f, float.PositiveInfinity, 0f),
                new Vector3(0f, 0f, float.NegativeInfinity)
            };
            foreach (Vector3 invalid in invalidDestinations)
            {
                Assert.That(rocket.Play(invalid), Is.False);
                Assert.That(rocket.IsPaused, Is.True);
                Assert.That(Tween.GetTweensCount(rocket), Is.EqualTo(1));
                Assert.That(state.Matches(Visual(rocket)), Is.True);
            }
            rocket.SampleForPreview(0.9f);
            Assert.That(Vector3.Distance(Visual(rocket).TransformPoint(new Vector3(0f, 2.29f, 0f)), destination), Is.LessThan(0.0001f));
        }

        [Test]
        public void RocketTipContactsWorldDestinationWithTranslatedRotatedScaledRoot()
        {
            var rocket = (RocketEffectView)Create("RocketPurple");
            rocket.transform.SetPositionAndRotation(new Vector3(3f, 2f, -4f), Quaternion.Euler(12f, 37f, 8f));
            rocket.transform.localScale = new Vector3(1.2f, 0.8f, 1.1f);
            Vector3 destination = new Vector3(-1f, 0.6f, 2f);
            Assert.That(rocket.Play(destination), Is.True);
            rocket.Suspend();
            rocket.SampleForPreview(0.9f);
            Vector3 tip = Visual(rocket).TransformPoint(new Vector3(0f, 2.29f, 0f));
            Assert.That(Vector3.Distance(tip, destination), Is.LessThan(0.0001f));
        }

        [Test]
        public void HammerStrikingFaceContactsRootAtImpact()
        {
            var hammer = (HammerEffectView)Create("Hammer");
            hammer.transform.SetPositionAndRotation(new Vector3(3f, 0.6f, 2f), Quaternion.Euler(0f, 32f, 0f));
            hammer.transform.localScale = Vector3.one * 1.2f;
            hammer.SampleForPreview(0.74f);
            Vector3 contact = Visual(hammer).TransformPoint(new Vector3(1f, 1.61f, -0.55f));
            Assert.That(Vector3.Distance(contact, hammer.transform.position), Is.LessThan(0.0001f));
            Assert.That(Tween.GetTweensCount(hammer), Is.Zero, "Preview must not start a runtime tween");
        }

        [Test]
        public void TheHammerStaysUprightOnScreenAndItsFaceComesDownOnTheRoot()
        {
            // The presenter turns the root to face the camera about the vertical axis, so the root's right axis is the
            // screen's horizontal: a handle with no component along it lies on one vertical screen line.
            var hammer = (HammerEffectView)Create("Hammer");
            hammer.transform.SetPositionAndRotation(new Vector3(3f, 0.6f, 2f), Quaternion.Euler(0f, 32f, 0f));
            Transform root = hammer.transform;
            Vector3 handle = new Vector3(0f, 1.61f, -0.55f);
            foreach (float progress in new[] { 0f, 0.2f, 0.42f, 0.6f, HammerEffectView.k_strikeFraction, 0.85f, 1f })
            {
                hammer.SampleForPreview(progress);
                Vector3 direction = Visual(hammer).TransformDirection(handle).normalized;
                Assert.That(Vector3.Dot(direction, root.right), Is.EqualTo(0f).Within(1e-4f), "upright at " + progress);
                Assert.That(Vector3.Dot(direction, root.forward), Is.GreaterThan(0f),
                    "the head stays beyond the grip, above it on screen, at " + progress);
                Assert.That(Vector3.Dot(direction, root.up), Is.GreaterThanOrEqualTo(-1e-4f), "never below the ground plane at " + progress);
            }

            hammer.SampleForPreview(HammerEffectView.k_strikeFraction);
            Assert.That(Vector3.Dot(Visual(hammer).TransformDirection(Vector3.right).normalized, -root.up), Is.EqualTo(1f).Within(1e-4f),
                "the striking cap faces straight down at the contact");
        }

        [Test]
        public void VacuumUsesThirtyTwoSharedTextureFramesAndOneRendererWithoutRig()
        {
            var vacuum = (VacuumEffectView)Create("Vacuum");
            var secondVacuum = (VacuumEffectView)Create("Vacuum");
            SpriteRenderer renderer = vacuum.GetComponentInChildren<SpriteRenderer>(true);
            SpriteRenderer secondRenderer = secondVacuum.GetComponentInChildren<SpriteRenderer>(true);
            // One sprite renderer on the visual; the only other renderer is the suction particle's (DEC-152).
            Assert.That(Visual(vacuum).GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1));
            Assert.That(vacuum.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(2));
            Assert.That(vacuum.GetComponentsInChildren<ParticleSystemRenderer>(true).Length, Is.EqualTo(1));
            Assert.That(vacuum.GetComponentsInChildren<SkinnedMeshRenderer>(true), Is.Empty);
            Assert.That(vacuum.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(vacuum.GetComponentsInChildren<Animation>(true), Is.Empty);
            Assert.That(vacuum.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1));
            Assert.That(renderer.sharedMaterial, Is.SameAs(secondRenderer.sharedMaterial));
            Assert.That(Visual(vacuum).Find("Intake"), Is.Not.Null);

            var frames = new HashSet<Sprite>();
            Texture2D texture = renderer.sprite.texture;
            for (int i = 0; i < 32; i++)
            {
                vacuum.SampleForPreview((i + 0.5f) / 32f);
                Assert.That(renderer.sprite, Is.Not.Null);
                frames.Add(renderer.sprite);
                Assert.That(renderer.sprite.texture, Is.SameAs(texture));
                Assert.That(renderer.sharedMaterial, Is.SameAs(secondRenderer.sharedMaterial));
            }
            Assert.That(frames.Count, Is.EqualTo(32));
            Assert.That(texture.width, Is.EqualTo(768));
            Assert.That(texture.height, Is.EqualTo(1536));
            Assert.That(texture.isReadable, Is.False);
            Assert.That(Tween.GetTweensCount(vacuum), Is.Zero);

            vacuum.SampleForPreview(0.5f);
            Assert.That(Vector3.Distance(vacuum.IntakePosition, Visual(vacuum).Find("Intake").position), Is.LessThan(0.0001f));
            vacuum.Stop();
            Assert.That(renderer.sprite, Is.SameAs(secondRenderer.sprite), "Stop must restore frame zero");
        }

        [Test]
        public void VacuumIntakeStaysCenteredAboveBodyAcrossFramesAndRootTransforms()
        {
            var vacuum = (VacuumEffectView)Create("Vacuum");
            Transform visual = Visual(vacuum);
            SpriteRenderer renderer = vacuum.GetComponentInChildren<SpriteRenderer>(true);
            vacuum.SampleForPreview(0.5f);
            Vector3 intakeInRoot = vacuum.transform.InverseTransformPoint(vacuum.IntakePosition);

            for (int placement = 0; placement < 2; placement++)
            {
                if (placement == 1)
                {
                    vacuum.transform.SetPositionAndRotation(new Vector3(3f, 0.6f, -2f), Quaternion.Euler(15f, 37f, 9f));
                    vacuum.transform.localScale = new Vector3(1.2f, 0.8f, 1.1f);
                }
                Vector3 expectedWorldIntake = vacuum.transform.TransformPoint(intakeInRoot);
                for (int frame = 4; frame <= 27; frame++)
                {
                    vacuum.SampleForPreview((frame + 0.5f) / 32f);
                    Assert.That(vacuum.IsIntakeReady,Is.True);
                    Bounds spriteBounds = renderer.sprite.bounds;
                    Vector3 localIntake = visual.InverseTransformPoint(vacuum.IntakePosition);
                    float horizontal = Mathf.InverseLerp(spriteBounds.min.x, spriteBounds.max.x, localIntake.x);
                    float vertical = Mathf.InverseLerp(spriteBounds.min.y, spriteBounds.max.y, localIntake.y);

                    Assert.That(horizontal, Is.InRange(0.4f, 0.6f), "The intake must be near the horizontal center");
                    Assert.That(vertical, Is.InRange(0.65f, 0.95f), "The intake must be at the upper nozzle, not the floor head");
                    Assert.That(Vector3.Distance(visual.localScale, Vector3.one), Is.LessThan(0.0001f), "Sample must use full visual scale");
                    Assert.That(Vector3.Distance(vacuum.IntakePosition, expectedWorldIntake), Is.LessThan(0.0001f),
                        "Hose frame changes must not move the block collection destination");
                }
            }
            Assert.That(Tween.GetTweensCount(vacuum), Is.Zero, "Preview sampling must not start runtime work");
        }

        [Test]
        public void VacuumIntakeIsDisabledDuringTurningAndResetOnStop()
        {
            var vacuum=(VacuumEffectView)Create("Vacuum");
            vacuum.SampleForPreview(.06f); Assert.That(vacuum.IsIntakeReady,Is.False);
            vacuum.SampleForPreview(.5f); Assert.That(vacuum.IsIntakeReady,Is.True);
            vacuum.SampleForPreview(.94f); Assert.That(vacuum.IsIntakeReady,Is.False);
            vacuum.Stop(); Assert.That(vacuum.IsIntakeReady,Is.False);
        }

        [UnityTest]
        public IEnumerator RemainingSecondsCountsDownTheMotionHoldsWhileSuspendedAndClearsOnStop()
        {
            CreateAll();
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(view.RemainingSeconds, Is.Zero, view.name + " is idle");
                Assert.That(Play(view), Is.True, view.name);
                Assert.That(view.RemainingSeconds, Is.EqualTo(view.Duration).Within(0.05f), view.name);
            }

            yield return new WaitForSeconds(0.2f);
            var remaining = new List<float>();
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(view.RemainingSeconds, Is.LessThan(view.Duration - 0.1f), view.name + " counts down");
                view.Suspend();
                remaining.Add(view.RemainingSeconds);
            }

            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < m_views.Count; i++)
            {
                BoosterEffectView view = m_views[i];
                Assert.That(view.RemainingSeconds, Is.EqualTo(remaining[i]).Within(1e-4f), view.name + " holds while suspended");
                view.Stop();
                Assert.That(view.RemainingSeconds, Is.Zero, view.name);
            }
        }

        [Test]
        public void TheContactConstantsMatchEachMotion()
        {
            // The board presenter times removals by these fractions (B-20), so they must be the motions' own.
            var hammer = (HammerEffectView)Create("Hammer");
            hammer.transform.SetPositionAndRotation(new Vector3(1f, 0.5f, -2f), Quaternion.Euler(0f, 20f, 0f));
            hammer.SampleForPreview(HammerEffectView.k_strikeFraction);
            Vector3 contact = Visual(hammer).TransformPoint(new Vector3(1f, 1.61f, -0.55f));
            Assert.That(Vector3.Distance(contact, hammer.transform.position), Is.LessThan(0.0001f), "the strike lands at the root");

            var rocket = (RocketEffectView)Create("RocketPurple");
            Vector3 destination = new Vector3(2f, 0.6f, 3f);
            Assert.That(rocket.Play(destination), Is.True);
            rocket.Suspend();
            rocket.SampleForPreview(RocketEffectView.k_noseFraction);
            Vector3 tip = Visual(rocket).TransformPoint(new Vector3(0f, 2.29f, 0f));
            Assert.That(Vector3.Distance(tip, destination), Is.LessThan(0.0001f), "the nose reaches its destination");
            rocket.Stop();

            var vacuum = (VacuumEffectView)Create("Vacuum");
            vacuum.SampleForPreview(VacuumEffectView.k_intakeStartFraction - 0.01f);
            Assert.That(vacuum.IsIntakeReady, Is.False);
            vacuum.SampleForPreview(VacuumEffectView.k_intakeStartFraction);
            Assert.That(vacuum.IsIntakeReady, Is.True);
            vacuum.SampleForPreview(VacuumEffectView.k_intakeEndFraction);
            Assert.That(vacuum.IsIntakeReady, Is.True);
            vacuum.SampleForPreview(VacuumEffectView.k_intakeEndFraction + 0.01f);
            Assert.That(vacuum.IsIntakeReady, Is.False);
        }

        [Test]
        public void TheReadyIntakeIsWhereTheFullyShownNozzleCollectsEvenBeforeTheMotionShowsIt()
        {
            var vacuum = (VacuumEffectView)Create("Vacuum");
            vacuum.transform.SetPositionAndRotation(new Vector3(3f, 0.6f, -2f), Quaternion.Euler(15f, 37f, 9f));
            vacuum.transform.localScale = new Vector3(1.2f, 0.8f, 1.1f);
            vacuum.SampleForPreview(0.5f);
            Vector3 shown = vacuum.IntakePosition;

            // At the start the visual is scaled to zero, so only the ready intake still names the nozzle.
            vacuum.SampleForPreview(0f);
            Assert.That(Vector3.Distance(vacuum.ReadyIntakePosition, shown), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(vacuum.IntakePosition, shown), Is.GreaterThan(0.01f));
            vacuum.Stop();
            Assert.That(Vector3.Distance(vacuum.ReadyIntakePosition, shown), Is.LessThan(0.0001f), "Stop restores the rest pose");
        }

        [UnityTest]
        public IEnumerator EachCastCarriesItsTemplateParticleIdleUntilPlay()
        {
            CreateAll();
            yield return new WaitForSeconds(0.15f);
            foreach (BoosterEffectView view in m_views)
            {
                ParticleSystem system = Particles(view);
                Assert.That(view.HasParticles, Is.True, view.name);
                Assert.That(system.gameObject.activeSelf, Is.False, view.name + " particle waits hidden");
                Assert.That(system.particleCount, Is.Zero, view.name);
                foreach (ParticleSystem child in system.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(child.main.playOnAwake, Is.False, child.name + " would play when the pool activates it");
                }
                Assert.That(view.IsBusy, Is.False, view.name);
            }
        }

        [UnityTest]
        public IEnumerator TheHammerBurstStartsAtTheStrikeInTheHitColourAndOutlivesTheMotion()
        {
            var hammer = (HammerEffectView)Create("Hammer");
            ParticleSystem system = Particles(hammer);
            Assert.That(hammer.Play(Color.red), Is.True);
            Assert.That(system.main.startColor.color, Is.EqualTo(Color.red));
            yield return new WaitForSeconds(hammer.ParticleStartSeconds - 0.15f);
            Assert.That(hammer.AreParticlesPlaying, Is.False, "the burst waits for the strike");

            yield return new WaitForSeconds(0.3f);
            Assert.That(hammer.AreParticlesPlaying, Is.True);
            Assert.That(system.gameObject.activeSelf, Is.True);
            Assert.That(system.isPlaying, Is.True);

            yield return new WaitForSeconds(hammer.Duration - hammer.ParticleStartSeconds + 0.05f);
            Assert.That(hammer.IsPlaying, Is.False, "the motion ended");
            Assert.That(hammer.IsBusy, Is.True, "the burst is still shown");

            yield return new WaitForSeconds(hammer.ParticleSeconds);
            Assert.That(hammer.IsBusy, Is.False);
            Assert.That(system.particleCount, Is.Zero);
            Assert.That(system.gameObject.activeSelf, Is.False);
            Assert.That(Tween.GetTweensCount(system), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SuspendHoldsTheParticleShowAndResumeContinuesIt()
        {
            var hammer = (HammerEffectView)Create("Hammer");
            ParticleSystem system = Particles(hammer);
            Assert.That(hammer.Play(Color.green), Is.True);
            yield return new WaitForSeconds(hammer.ParticleStartSeconds + 0.1f);
            Assert.That(hammer.AreParticlesPlaying, Is.True);

            hammer.Suspend();
            Assert.That(system.isPaused, Is.True);
            int particles = system.particleCount;
            yield return new WaitForSeconds(hammer.ParticleSeconds + 0.3f);
            Assert.That(hammer.IsBusy, Is.True, "a suspended show does not end");
            Assert.That(system.particleCount, Is.EqualTo(particles), "paused particles neither die nor spawn");

            hammer.Resume();
            Assert.That(system.isPaused, Is.False);
            Assert.That(system.isPlaying, Is.True);
            yield return new WaitForSeconds(hammer.ParticleSeconds + 0.1f);
            Assert.That(hammer.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator StopClearsARunningParticleShow()
        {
            CreateAll();
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(Play(view), Is.True, view.name);
            }
            yield return new WaitForSeconds(0.7f);
            foreach (BoosterEffectView view in m_views)
            {
                Assert.That(view.AreParticlesPlaying, Is.True, view.name);
                view.Stop();
                ParticleSystem system = Particles(view);
                Assert.That(view.IsBusy, Is.False, view.name);
                Assert.That(system.particleCount, Is.Zero, view.name);
                Assert.That(system.gameObject.activeSelf, Is.False, view.name);
                Assert.That(Tween.GetTweensCount(system), Is.Zero, view.name);
            }
        }

        [UnityTest]
        public IEnumerator TheVacuumSuctionEmitsOnlyDuringItsIntakeWindow()
        {
            var vacuum = (VacuumEffectView)Create("Vacuum");
            ParticleSystem system = Particles(vacuum);
            // The prefab authors the lines on the intake, so the Scene view shows the pose play uses.
            Assert.That(Vector3.Distance(system.transform.position, vacuum.ReadyIntakePosition), Is.LessThan(0.001f),
                "the suction is authored on the intake");
            Assert.That(system.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture.name,
                Is.EqualTo("suction_line"), "the suction draws straight lines, not lightning");
            Assert.That(vacuum.Play(), Is.True);
            Assert.That(Vector3.Distance(system.transform.position, vacuum.ReadyIntakePosition), Is.LessThan(0.0001f));
            // The ring faces the camera like the flat Vacuum root, so on screen its lines close in on the nozzle.
            Vector3 ringNormal = system.transform.rotation * Quaternion.Euler(system.shape.rotation) * Vector3.forward;
            Assert.That(Mathf.Abs(Vector3.Dot(ringNormal, vacuum.transform.forward)), Is.GreaterThan(0.999f),
                "the suction ring lies in the root's camera-facing plane");
            Assert.That(system.shape.radius, Is.LessThan(1f), "the ring stays around the nozzle, not the whole vacuum");
            Assert.That(system.shape.arc, Is.EqualTo(180f), "only the board side of the ring emits");
            yield return new WaitForSeconds(vacuum.Duration * 0.5f);
            Assert.That(system.isEmitting, Is.True, "suction runs while the intake faces the board");

            yield return new WaitForSeconds(vacuum.Duration * (1f - 0.5f) + 0.05f);
            Assert.That(system.isEmitting, Is.False, "suction stops when the intake turns away");
            Assert.That(vacuum.IsBusy, Is.True, "the last particles still fly in");
        }

        [Test]
        public void TheRocketShattersAtItsTargetCellInTheCellColour()
        {
            var rocket = (RocketEffectView)Create("RocketPurple");
            Vector3 destination = new Vector3(2f, 0.6f, 3f);
            Assert.That(rocket.Play(destination, Color.yellow), Is.True);
            ParticleSystem system = Particles(rocket);
            Assert.That(Vector3.Distance(system.transform.position, destination), Is.LessThan(0.0001f));
            Assert.That(system.main.startColor.color, Is.EqualTo(Color.yellow));
            Assert.That(rocket.ParticleStartSeconds, Is.EqualTo(rocket.Duration * RocketEffectView.k_noseFraction).Within(1e-5f));
            rocket.Stop();
        }

        private static ParticleSystem Particles(BoosterEffectView view)
        {
            var serialized = new SerializedObject(view);
            var system = (ParticleSystem)serialized.FindProperty("m_particles").objectReferenceValue;
            Assert.That(system, Is.Not.Null, view.name + " has no particle");
            return system;
        }

        private void CreateAll()
        {
            Create("Hammer");
            Create("RocketPurple");
            Create("Vacuum");
        }

        private BoosterEffectView Create(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_prefabFolder + name + "Effect.prefab");
            Assert.That(prefab, Is.Not.Null, name + " prefab missing");
            GameObject instance = Object.Instantiate(prefab);
            BoosterEffectView view = instance.GetComponent<BoosterEffectView>();
            Assert.That(view, Is.Not.Null, name + " view missing");
            m_views.Add(view);
            Assert.That(Visual(view), Is.Not.Null, name + " visual missing");
            return view;
        }

        private static Transform Visual(BoosterEffectView view) => view.transform.Find("Visual");

        private static bool Play(BoosterEffectView view)
        {
            if (view is HammerEffectView hammer)
            {
                return hammer.Play();
            }
            if (view is RocketEffectView rocket)
            {
                return rocket.Play(rocket.transform.TransformPoint(new Vector3(1f, 0f, 3f)));
            }
            return ((VacuumEffectView)view).Play();
        }

        private readonly struct VisualState
        {
            private readonly Vector3 m_position;
            private readonly Quaternion m_rotation;
            private readonly Vector3 m_scale;
            private readonly Sprite m_sprite;

            public VisualState(Transform visual)
            {
                m_position = visual.localPosition;
                m_rotation = visual.localRotation;
                m_scale = visual.localScale;
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                m_sprite = renderer != null ? renderer.sprite : null;
            }

            public bool Matches(Transform visual)
            {
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                Sprite sprite = renderer != null ? renderer.sprite : null;
                return Vector3.Distance(m_position, visual.localPosition) < 0.00001f
                    && Quaternion.Angle(m_rotation, visual.localRotation) < 0.001f
                    && Vector3.Distance(m_scale, visual.localScale) < 0.00001f
                    && m_sprite == sprite;
            }
        }
    }
}
#endif
