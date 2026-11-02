#if UNITY_EDITOR
using System.Collections;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Presentation.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using SessionState = ColorBlockJam.Domain.Session.SessionState;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// The gameplay HUD composed as the Game scene composes it (B-21 to B-25, AC-1, AC-2, AC-3, AC-10): the real board
    /// presenter, the project's booster catalog, the HUD document on the board overlay panel and a pointer that taps
    /// through the UI filter. Show binds on Home with the catalog's icons in bar order and the authored time; Rocket
    /// targeting marks every eligible cell and a tap on one spends one charge and removes that cell once; Time Freeze
    /// shows the frozen timer, its counter and the frost; Dispose unbinds.
    /// </summary>
    public sealed class GameplayHudCompositionTests
    {
        private const string k_hudPath = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";
        private const float k_frameSeconds = 1f / 60f;

        private BoosterPresenterScene m_scene;
        private GameObject m_hudHost;
        private UIDocument m_hudDocument;
        private HudBoosterStore m_store;
        private BoosterTargeting m_targeting;
        private ScriptedPointerSource m_pointer;
        private CameraBoardProjector m_projector;
        private LevelSession m_session;
        private BoosterUseCoordinator m_coordinator;
        private Material m_frostMaterial;
        private TimeFreezeFrostView m_frost;
        private Texture2D m_reticleTexture;
        private GameplayHud m_hud;
        private AttemptLevel m_level;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_scene = new BoosterPresenterScene();
            m_hudHost = new GameObject("Gameplay HUD");
            m_hudHost.SetActive(false);
            m_hudDocument = m_hudHost.AddComponent<UIDocument>();
            m_hudDocument.panelSettings = m_scene.Document.panelSettings;
            m_hudDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_hudPath);
            m_hudDocument.sortingOrder = 30f;
            m_hudHost.SetActive(true);

            m_store = new HudBoosterStore();
            m_targeting = new BoosterTargeting();
            m_pointer = new ScriptedPointerSource();
            m_projector = new CameraBoardProjector(m_scene.Camera, m_scene.Environment.BoardRoot, ExitMotionSettings.k_blockTopHeight);
            DragInputRouter router = new DragInputRouter(m_pointer, new UiToolkitPointerFilter(new[] { m_hudDocument }),
                m_projector, m_targeting);
            m_session = new LevelSession(router, m_pointer, m_scene.Presenter, new AttemptCountdown(), new NoPauseOverlay(true),
                m_scene.Hooks, new PrimeTweenFinishDeadline());
            m_coordinator = new BoosterUseCoordinator(m_session, m_store, m_store, m_store, m_scene.Catalog, m_targeting,
                new UnavailablePurchaseHost());
            m_frostMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_scene.Camera, m_frostMaterial));
            m_reticleTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            m_hud = new GameplayHud(new GameplayHudContext(m_hudDocument, m_reticleTexture), m_session, m_targeting,
                m_coordinator, m_scene.Catalog, m_store, m_store, m_projector, m_frost);

            LevelData data = BoardPresenterBoosterTests.SplitLevel();
            m_level = m_scene.Convert(data);
            m_scene.PrepareEffects(data, m_level);
            Assert.That(m_session.TryPrepare(m_level, out string error), Is.True, error);
            LookDownOnTheBoard();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            m_hud.Dispose();
            m_coordinator.Dispose();
            m_session.Dispose();
            m_frost.Dispose();
            m_targeting.Dispose();
            m_store.Dispose();
            m_scene.Dispose();
            Object.Destroy(m_hudHost);
            Object.Destroy(m_frostMaterial);
            Object.Destroy(m_reticleTexture);
        }

        [UnityTest]
        public IEnumerator ShowBindsOnHomeWithTheCatalogIconsAndTheAuthoredTime()
        {
            m_hud.Show(m_level);
            yield return null;

            Assert.That(m_hud.IsBound, Is.True);
            BoosterId[] order = { BoosterId.TimeFreeze, BoosterId.Rocket, BoosterId.Hammer, BoosterId.Vacuum };
            for (int slot = 0; slot < order.Length; slot++)
            {
                Assert.That(m_hud.Bar.GetSlotBooster(slot), Is.EqualTo(order[slot]));
                Assert.That(m_scene.Catalog.TryGet(order[slot], out BoosterDefinition definition), Is.True);
                VisualElement icon = m_hud.View.GetButton(slot).Q(GameplayHudView.BoosterIconName);
                Assert.That(icon.resolvedStyle.backgroundImage.sprite, Is.SameAs(definition.Icon), order[slot].ToString());
                Assert.That(m_hud.View.GetButton(slot).Q<Label>(GameplayHudView.BoosterCountLabelName).text, Is.EqualTo("1"));
                Assert.That(m_hud.View.GetButton(slot).enabledSelf, Is.False, "Home takes no booster press");
            }

            Assert.That(m_hudDocument.rootVisualElement.Q<Label>(GameplayHudView.TimerLabelName).text, Is.EqualTo("01:00"));
            Assert.That(m_frost.IsPrepared, Is.True, "the frost quad is made on Home");
            Assert.That(m_hud.ReticleView.Capacity, Is.EqualTo(m_level.Model.TotalCellCount));

            Assert.That(m_session.Play(), Is.True);
            yield return null;
            Assert.That(m_hud.View.GetButton(0).enabledSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator RocketMarksEveryEligibleCellAndATapSpendsOneChargeForThatCell()
        {
            m_hud.Show(m_level);
            Assert.That(m_session.Play(), Is.True);
            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingStarted));
            yield return null;

            Assert.That(m_hud.Reticles.ShownCount, Is.EqualTo(9), "every cell of the L and the cross");
            Assert.That(m_hud.View.HasRootClass(GameplayHudView.TargetingClass), Is.True);
            Assert.That(m_hud.ReticleView.GetReticle(0).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_scene.Catalog.TryGet(BoosterId.Rocket, out BoosterDefinition rocket), Is.True);
            Assert.That(rocket.Icon, Is.Not.Null);
            Assert.That(m_hudDocument.rootVisualElement.Q(GameplayHudView.BannerIconName).resolvedStyle.backgroundImage.sprite,
                Is.SameAs(rocket.Icon), "the banner shows the catalog's Rocket icon (AC-2)");

            IBoardReader board = m_session.Attempt.Simulation.Board;
            Cell target = FindTappableCell(board);
            ScreenPoint screen = m_projector.Footprint.Project(target.X, target.Y);
            int block = board.GetOccupant(target);
            int cells = board.GetFootprint(block).CellCount;
            Tap(new Vector2(screen.X, screen.Y));

            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.Saving), "the accepted target suspends for the debit");
            Assert.That(m_hud.Reticles.ShownCount, Is.Zero, "Suspend ended the targeting");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!m_coordinator.PendingSave.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            m_session.RunTick(k_frameSeconds);

            Assert.That(m_store.ConsumeCount, Is.EqualTo(1));
            Assert.That(m_store.GetStock(BoosterId.Rocket).Value, Is.Zero);
            Assert.That(board.GetOccupant(target), Is.EqualTo(-1), "the tapped cell is gone");
            Assert.That(board.GetFootprint(block).CellCount, Is.LessThan(cells));
            Assert.That(m_hud.View.GetButton(1).ClassListContains(GameplayHudView.EmptyClass), Is.True, "the plus badge shows");
            Assert.That(m_hud.View.HasRootClass(GameplayHudView.TargetingClass), Is.False);
        }

        [UnityTest]
        public IEnumerator TimeFreezeShowsTheFrozenTimerItsCounterAndTheFrost()
        {
            m_hud.Show(m_level);
            Assert.That(m_session.Play(), Is.True);

            Assert.That(m_coordinator.Press(BoosterId.TimeFreeze), Is.EqualTo(BoosterPressResult.Submitted));
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!m_coordinator.PendingSave.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            m_session.RunTick(k_frameSeconds);

            Assert.That(m_session.IsFrozen.Value, Is.True);
            Assert.That(m_hud.View.HasRootClass(GameplayHudView.FrozenClass), Is.True);
            Assert.That(m_hudDocument.rootVisualElement.Q<Label>(GameplayHudView.FreezeLabelName).text, Is.EqualTo("10"));
            Assert.That(m_hud.View.GetButton(0).enabledSelf, Is.False, "a second Time Freeze waits for this one");
            Assert.That(m_frost.IsFrozen, Is.True);
            deadline = Time.realtimeSinceStartup + TimeFreezeFrostView.k_fadeSeconds + 0.5f;
            while (m_frost.Amount < 1f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(m_frost.Amount, Is.EqualTo(1f));
            Assert.That(m_frost.IsVisible, Is.True);

            // The countdown waits for the first block press (DEC-189); this test exercises its HUD afterward.
            m_session.Attempt.MarkBlockTouched();
            m_session.RunTick(4f);
            Assert.That(m_hudDocument.rootVisualElement.Q<Label>(GameplayHudView.FreezeLabelName).text, Is.EqualTo("6"));
            Assert.That(m_hudDocument.rootVisualElement.Q<Label>(GameplayHudView.TimerLabelName).text, Is.EqualTo("01:00"),
                "the main time is held");
        }

        [UnityTest]
        public IEnumerator APinnedNotchPadsTheTopBandAndTheBarAndKeepsTheButtonsAndBannerOnThePanel()
        {
            m_hud.Show(m_level);
            Assert.That(m_session.Play(), Is.True);
            yield return null;

            // A 1080 x 2400 (20:9) surface with a notch band at the top, rounded sides and a gesture bar at the bottom.
            Rect safeArea = new Rect(24f, 66f, 1032f, 2244f);
            Vector2 surface = new Vector2(1080f, 2400f);
            m_hud.SetSafeAreaSource(safeArea, surface);
            yield return null;

            VisualElement root = m_hud.View.Root;
            SafeAreaInsets insets = SafeAreaPadding.Calculate(safeArea, surface, root.layout.size);
            Assert.That(insets.Top, Is.GreaterThan(0f));
            Assert.That(insets.Bottom, Is.GreaterThan(0f));
            AssertPadding(m_hud.View.TopArea, insets.Left, insets.Top, insets.Right, 0f, "the top band pads its top and sides");
            AssertPadding(m_hud.View.BoosterBar, insets.Left, 0f, insets.Right, insets.Bottom,
                "the bar pads its sides and bottom and its fill reaches the edge");

            Rect panel = root.worldBound;
            for (int slot = 0; slot < m_hud.View.SlotCount; slot++)
            {
                AssertInside(m_hud.View.GetButton(slot).worldBound, panel, "booster button " + slot);
            }

            Assert.That(m_coordinator.Press(BoosterId.Rocket), Is.EqualTo(BoosterPressResult.TargetingStarted));
            yield return null;
            AssertInside(m_hud.View.Banner.worldBound, panel, "the targeting banner");
            Assert.That(m_hud.View.Banner.worldBound.yMin, Is.GreaterThanOrEqualTo(panel.yMin + insets.Top - 0.5f),
                "the banner sits below the notch");
        }

        [UnityTest]
        public IEnumerator TheCastAnchorIsTheBoosterBarsTopCentreAboveEveryButton()
        {
            Assert.That(m_hud.TryGetLaunchViewportPoint(out _), Is.False, "no anchor before the HUD binds");
            m_hud.Show(m_level);
            yield return null;

            Assert.That(m_hud.TryGetLaunchViewportPoint(out Vector2 anchor), Is.True);
            Rect panel = m_hud.View.Root.panel.visualTree.worldBound;
            Assert.That(anchor.x, Is.EqualTo(0.5f).Within(0.01f), "the bar spans the screen, so its centre is the middle");
            Assert.That(anchor.y, Is.GreaterThan(0f).And.LessThan(0.5f), "the anchor is in the lower half of the screen");
            for (int slot = 0; slot < m_hud.View.SlotCount; slot++)
            {
                float buttonTop = 1f - (m_hud.View.GetButton(slot).worldBound.yMin - panel.yMin) / panel.height;
                Assert.That(anchor.y, Is.GreaterThanOrEqualTo(buttonTop), "the casts enter above booster button " + slot);
            }
        }

        [Test]
        public void DisposeUnbindsTheViewAndItsPresenters()
        {
            m_hud.Show(m_level);
            Button rocket = m_hud.View.GetButton(1);

            m_hud.Dispose();
            m_store.SetStock(BoosterId.Rocket, 5);

            Assert.That(m_hud.IsBound, Is.False);
            Assert.That(rocket.Q<Label>(GameplayHudView.BoosterCountLabelName).text, Is.EqualTo("1"), "no write after Dispose");
            Assert.That(m_hudDocument.rootVisualElement.Q(GameplayHudView.TargetLayerName).childCount, Is.Zero,
                "the pooled reticles are removed");
        }

        // The inline padding is exactly the computed inset; the laid-out padding may differ by the panel's pixel rounding.
        private static void AssertPadding(VisualElement element, float left, float top, float right, float bottom, string message)
        {
            IStyle inline = element.style;
            Assert.That(inline.paddingLeft.value.value, Is.EqualTo(left).Within(0.01f), message + " (left)");
            Assert.That(inline.paddingTop.value.value, Is.EqualTo(top).Within(0.01f), message + " (top)");
            Assert.That(inline.paddingRight.value.value, Is.EqualTo(right).Within(0.01f), message + " (right)");
            Assert.That(inline.paddingBottom.value.value, Is.EqualTo(bottom).Within(0.01f), message + " (bottom)");
            IResolvedStyle laidOut = element.resolvedStyle;
            Assert.That(laidOut.paddingLeft, Is.EqualTo(left).Within(1f), message + " (laid-out left)");
            Assert.That(laidOut.paddingTop, Is.EqualTo(top).Within(1f), message + " (laid-out top)");
            Assert.That(laidOut.paddingRight, Is.EqualTo(right).Within(1f), message + " (laid-out right)");
            Assert.That(laidOut.paddingBottom, Is.EqualTo(bottom).Within(1f), message + " (laid-out bottom)");
        }

        private static void AssertInside(Rect bounds, Rect panel, string message)
        {
            Assert.That(bounds.width, Is.GreaterThan(0f), message + " is laid out");
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(panel.xMin - 0.5f), message + " stays inside the panel (left)");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(panel.xMax + 0.5f), message + " stays inside the panel (right)");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(panel.yMin - 0.5f), message + " stays inside the panel (top)");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(panel.yMax + 0.5f), message + " stays inside the panel (bottom)");
        }

        // A top-down view centred on the board, so its cells are on screen away from the bars (the Game scene frames its
        // own camera; this fixture's camera is ad hoc).
        private void LookDownOnTheBoard()
        {
            Transform boardRoot = m_scene.Environment.BoardRoot;
            Vector3 centre = boardRoot.TransformPoint(m_level.Layout.LocalBounds.center);
            m_scene.Camera.transform.SetPositionAndRotation(centre + Vector3.up * 14f, Quaternion.Euler(90f, 0f, 0f));
            m_scene.Camera.fieldOfView = 60f;
        }

        // The eligible cell nearest the screen centre that no HUD element covers.
        private Cell FindTappableCell(IBoardReader board)
        {
            UiToolkitPointerFilter filter = new UiToolkitPointerFilter(new[] { m_hudDocument });
            Vector2 middle = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float best = float.MaxValue;
            Cell found = default;
            bool any = false;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Cell cell = new Cell(x, y);
                    int block = board.GetOccupant(cell);
                    if (block < 0 || board.GetFootprint(block).CellCount < 2)
                    {
                        continue;
                    }

                    ScreenPoint point = m_projector.Footprint.Project(x, y);
                    Vector2 screen = new Vector2(point.X, point.Y);
                    if (filter.IsOverUi(screen) || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
                    {
                        continue;
                    }

                    float distance = (screen - middle).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        found = cell;
                        any = true;
                    }
                }
            }

            Assert.That(any, Is.True, "an eligible cell is visible outside the HUD");
            return found;
        }

        private void Tap(Vector2 screen)
        {
            m_pointer.Set(PointerPhase.Down, screen);
            m_session.RunTick(k_frameSeconds);
            m_pointer.Set(PointerPhase.Up, screen);
            m_session.RunTick(k_frameSeconds);
            m_pointer.Set(PointerPhase.None, screen);
        }
    }
}
#endif
