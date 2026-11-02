#if UNITY_EDITOR
using System.Collections;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// GameplayHud.uxml on the Game scene's board overlay panel (B-21 to B-24, AC-1): every named element the view and the
    /// reticle layer need exists, full-screen layers and labels are Ignore while the bar and banner are Position, so the
    /// UI filter vetoes a press over the bar or banner and never over the board, the font and square buttons resolve,
    /// the view's writes land as classes and text, and pooled reticles stay unpickable.
    /// </summary>
    public sealed class GameplayHudLayoutTests
    {
        private const string k_panelPath = "Assets/ColorBlockJam/Data/UI/BoardOverlayPanelSettings.asset";
        private const string k_hudPath = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";

        private GameObject m_host;
        private UIDocument m_document;
        private GameplayHudView m_view;
        private RocketReticleView m_reticles;
        private Texture2D m_texture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_host = new GameObject("Gameplay HUD test");
            m_host.SetActive(false);
            m_document = m_host.AddComponent<UIDocument>();
            m_document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(k_panelPath);
            m_document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_hudPath);
            Assert.That(m_document.panelSettings, Is.Not.Null);
            Assert.That(m_document.visualTreeAsset, Is.Not.Null, k_hudPath);
            m_host.SetActive(true);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            m_reticles?.Dispose();
            m_view?.Dispose();
            Object.Destroy(m_host);
            if (m_texture != null)
            {
                Object.Destroy(m_texture);
            }
        }

        [Test]
        public void EveryNamedElementTheViewNeedsExists()
        {
            VisualElement root = Root();
            string[] names =
            {
                GameplayHudView.TargetLayerName, GameplayHudView.TopAreaName, GameplayHudView.TimerBandName,
                GameplayHudView.TimerLabelName, GameplayHudView.FreezeCounterName, GameplayHudView.FreezeFillName,
                GameplayHudView.FreezeLabelName, GameplayHudView.BannerName, GameplayHudView.BannerIconName,
                GameplayHudView.BannerTextName, GameplayHudView.BannerTitleName, GameplayHudView.BannerTitleBackName,
                GameplayHudView.BannerCloseName, GameplayHudView.OpeningLayerName, GameplayHudView.OpeningRowName,
                GameplayHudView.BoosterBarName
            };
            foreach (string name in names)
            {
                Assert.That(root.Q(name), Is.Not.Null, name);
            }

            for (int slot = 0; slot < GameplayHudView.k_slotCount; slot++)
            {
                Button button = root.Q<Button>(GameplayHudView.BoosterButtonPrefix + slot);
                Assert.That(button, Is.Not.Null, "slot " + slot);
                Assert.That(button.Q(GameplayHudView.BoosterIconName), Is.Not.Null);
                Assert.That(button.Q<Label>(GameplayHudView.BoosterCountLabelName), Is.Not.Null);
                Assert.That(button.Q(GameplayHudView.BoosterPlusName), Is.Not.Null);
            }

            Assert.DoesNotThrow(() => m_view = new GameplayHudView(root));
            Assert.That(m_view.SlotCount, Is.EqualTo(4));
        }

        [Test]
        public void OnlyTheBarTheBannerAndButtonsArePickable()
        {
            VisualElement root = Root();
            string[] ignored =
            {
                GameplayHudView.RootName, GameplayHudView.TargetLayerName, GameplayHudView.TopAreaName, "top-content",
                GameplayHudView.TimerBandName, "timer-holder", GameplayHudView.FreezeCounterName,
                GameplayHudView.OpeningLayerName, "booster-row", "info-header-row"
            };
            foreach (string name in ignored)
            {
                Assert.That(Find(name).pickingMode, Is.EqualTo(PickingMode.Ignore), name);
            }

            Assert.That(Find(GameplayHudView.BoosterBarName).pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(Find(GameplayHudView.BannerName).pickingMode, Is.EqualTo(PickingMode.Position));
            root.Query<Label>().ForEach(label => Assert.That(label.pickingMode, Is.EqualTo(PickingMode.Ignore), label.name));
            root.Query<Button>().ForEach(button => Assert.That(button.pickingMode, Is.EqualTo(PickingMode.Position), button.name));
        }

        [UnityTest]
        public IEnumerator TheUiFilterVetoesTheBarAndTheBannerButNeverTheBoard()
        {
            m_view = new GameplayHudView(Root());
            UiToolkitPointerFilter filter = new UiToolkitPointerFilter(new[] { m_document });
            yield return null;

            Assert.That(filter.IsOverUi(ToScreen(Find(GameplayHudView.BoosterBarName).worldBound.center)), Is.True, "bar");
            Assert.That(filter.IsOverUi(ToScreen(m_view.GetButton(1).worldBound.center)), Is.True, "button");
            Assert.That(filter.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.False, "board centre");
            Assert.That(filter.IsOverUi(ToScreen(Find("timer-holder").worldBound.center)), Is.False, "the timer is Ignore");

            m_view.ShowBanner("ROCKET", BoosterInstructions.Rocket, null);
            yield return null;
            yield return null;

            Assert.That(m_view.HasRootClass(GameplayHudView.TargetingClass), Is.True);
            Assert.That(filter.IsOverUi(ToScreen(m_view.Banner.worldBound.center)), Is.True, "banner");
            Assert.That(filter.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.False, "board centre");

            m_view.HideBanner();
            yield return null;
            Assert.That(m_view.Banner.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheHudSetsItsOwnFontAndKeepsTheButtonsSquare()
        {
            Label timer = Root().Q<Label>(GameplayHudView.TimerLabelName);
            Assert.That(timer.resolvedStyle.unityFontDefinition.fontAsset, Is.Not.Null,
                "the panel loads no theme, so GameplayHud.uss sets LiberationSans");
            for (int slot = 0; slot < GameplayHudView.k_slotCount; slot++)
            {
                Button button = Root().Q<Button>(GameplayHudView.BoosterButtonPrefix + slot);
                Assert.That(button.resolvedStyle.width, Is.EqualTo(button.resolvedStyle.height).Within(0.01f));
                Assert.That(button.resolvedStyle.width, Is.GreaterThan(100f));
            }
        }

        [UnityTest]
        public IEnumerator TheViewWritesTextClassesAndEnabledStates()
        {
            m_view = new GameplayHudView(Root());
            int pressed = -1;
            m_view.SlotPressed += slot => pressed = slot;

            m_view.SetSlotStock(0, "3", false);
            m_view.SetSlotStock(1, "0", true);
            m_view.SetSlotSelected(2, true);
            m_view.SetSlotEnabled(3, false);
            m_view.HideSlot(3);
            m_view.SetTimer("0:42");
            m_view.SetFrozen(true);
            m_view.SetFreezeCounter("6", 0.6f);
            m_view.SetOpening(true);
            yield return null;

            Assert.That(m_view.GetButton(0).Q<Label>(GameplayHudView.BoosterCountLabelName).text, Is.EqualTo("3"));
            Assert.That(m_view.GetButton(1).ClassListContains(GameplayHudView.EmptyClass), Is.True);
            Assert.That(m_view.GetButton(1).Q(GameplayHudView.BoosterPlusName).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_view.GetButton(0).Q(GameplayHudView.BoosterPlusName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_view.GetButton(2).ClassListContains(GameplayHudView.SelectedClass), Is.True);
            Assert.That(m_view.GetButton(3).enabledSelf, Is.False);
            Assert.That(m_view.GetButton(3).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(Root().Q<Label>(GameplayHudView.TimerLabelName).text, Is.EqualTo("0:42"));
            Assert.That(m_view.HasRootClass(GameplayHudView.FrozenClass), Is.True);
            Assert.That(Find(GameplayHudView.FreezeCounterName).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Root().Q<Label>(GameplayHudView.FreezeLabelName).text, Is.EqualTo("6"));
            Assert.That(m_view.HasRootClass(GameplayHudView.OpeningClass), Is.True);

            m_view.PlaySlotBlocked(0);
            Assert.That(m_view.GetButton(0).ClassListContains(GameplayHudView.BlockedClass), Is.True);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (m_view.GetButton(0).ClassListContains(GameplayHudView.BlockedClass) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(m_view.GetButton(0).ClassListContains(GameplayHudView.BlockedClass), Is.False,
                "the flash ends with its own transition");

            Click(m_view.GetButton(2));
            yield return null;

            Assert.That(pressed, Is.EqualTo(2), "a click reports its slot");
        }

        [UnityTest]
        public IEnumerator PooledReticlesStayUnpickableAndFollowTheirScreenPoints()
        {
            m_view = new GameplayHudView(Root());
            m_texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[64];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(0, 0, 0, (byte)(index * 4));
            }

            m_texture.SetPixels32(pixels);
            m_texture.Apply();
            m_reticles = new RocketReticleView(m_view.TargetLayer, m_texture);
            m_reticles.Reserve(3);
            yield return null;

            Assert.That(m_reticles.HasWhiteCopy, Is.True);
            Assert.That(m_reticles.Capacity, Is.EqualTo(3));
            for (int index = 0; index < 3; index++)
            {
                VisualElement reticle = m_reticles.GetReticle(index);
                Assert.That(reticle.pickingMode, Is.EqualTo(PickingMode.Ignore));
                Assert.That(reticle.Query<VisualElement>().Where(element => element.pickingMode != PickingMode.Ignore).ToList(),
                    Is.Empty);
                Assert.That(reticle.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            }

            Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            m_reticles.Show(0, new ScreenPoint(screen.x, screen.y), Screen.width * 0.1f);
            m_reticles.Show(1, new ScreenPoint(1e9f, 1e9f), 40f);
            yield return null;

            VisualElement shown = m_reticles.GetReticle(0);
            Assert.That(shown.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Vector2 centre = ToScreen(shown.worldBound.center);
            Assert.That(centre.x, Is.EqualTo(screen.x).Within(1f));
            Assert.That(centre.y, Is.EqualTo(screen.y).Within(1f));
            Assert.That(ToScreen(shown.worldBound.max).x - ToScreen(shown.worldBound.min).x,
                Is.EqualTo(Screen.width * 0.1f).Within(1f));
            Assert.That(m_reticles.GetReticle(1).resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "a point behind the camera stays hidden");
            UiToolkitPointerFilter filter = new UiToolkitPointerFilter(new[] { m_document });
            Assert.That(filter.IsOverUi(screen), Is.False, "a reticle never vetoes a board press");

            m_reticles.HideFrom(0);
            yield return null;
            Assert.That(shown.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(m_reticles.ShownCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TheViewsTickWritesAllocateNothingBeyondUiToolkitsOwnTextChange()
        {
            m_view = new GameplayHudView(Root());
            m_reticles = new RocketReticleView(m_view.TargetLayer, null);
            m_reticles.Reserve(2);
            ScreenPoint point = new ScreenPoint(Screen.width * 0.4f, Screen.height * 0.5f);
            WriteTickValues("0:41", "5", 0.5f, point);
            WriteTickValues("0:41", "5", 0.6f, point);
            yield return null;

            // A tick that shows the same seconds rewrites nothing, and the bar fill and the reticle placement allocate
            // nothing. A changed Label.text allocates inside UI Toolkit 2022.3 itself (one block per change, measured),
            // which the presenters bound to once per shown second; the device Profiler sample stays the final check.
            // The delegate and the constraint run once first, so their own JIT compilation is not measured.
            TestDelegate writes = () =>
            {
                WriteTickValues("0:41", "5", 0.5f, point);
                WriteTickValues("0:41", "5", 0.6f, point);
            };
            writes();
            new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint().ApplyTo(writes);
            Assert.That(writes, UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not));
            Assert.That(Root().Q<Label>(GameplayHudView.TimerLabelName).text, Is.EqualTo("0:41"));
        }

        [UnityTest]
        public IEnumerator TheFirstRocketTargetingAllocatesOnlyTheBannerTextCopies()
        {
            m_view = new GameplayHudView(Root());
            ScreenPoint first = new ScreenPoint(Screen.width * 0.4f, Screen.height * 0.5f);
            ScreenPoint second = new ScreenPoint(Screen.width * 0.6f, Screen.height * 0.5f);
            m_texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Sprite icon = Sprite.Create(m_texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);

            // Mono allocates a string literal the first time a method runs it, so the measured lambdas read these
            // locals instead of literals.
            string title = "ROCKET";
            string instruction = BoosterInstructions.Rocket;

            // Code paths warm up on other elements: an attached copy of the HUD tree takes the banner writes and a
            // second reticle layer on the same panel takes Show, so the measured banner and reticles still meet every
            // write for the first time, as on the first Rocket selection of an attempt.
            VisualElement copy = m_document.visualTreeAsset.Instantiate();
            m_document.rootVisualElement.Add(copy);
            using (GameplayHudView warmView = new GameplayHudView(copy.Q(GameplayHudView.RootName)))
            {
                warmView.ShowBanner(title, instruction, icon);
                warmView.HideBanner();
            }

            copy.RemoveFromHierarchy();
            using (RocketReticleView warmReticles = new RocketReticleView(m_view.TargetLayer, null))
            {
                warmReticles.Reserve(1);
                warmReticles.Show(0, first, 40f);
                warmReticles.HideFrom(0);
            }

            GcAllocations.Count(() => { });

            // Home: the layer reserves its reticles before Playing.
            m_reticles = new RocketReticleView(m_view.TargetLayer, null);
            m_reticles.Reserve(3);
            yield return null;

            int selection = GcAllocations.Count(() => m_view.ShowBanner(title, instruction, icon));
            int reticles = GcAllocations.Count(() =>
            {
                m_reticles.Show(0, first, 40f);
                m_reticles.Show(1, second, 40f);
                m_reticles.HideFrom(2);
            });
            int unchanged = GcAllocations.Count(() => m_view.ShowBanner(title, instruction, icon));
            Object.Destroy(icon);

            Assert.That(selection, Is.EqualTo(3),
                "a selection allocates only UI Toolkit's copies of the banner title, its back and the instruction (B-31); "
                + "the icon and the targeting class allocate nothing");
            Assert.That(reticles, Is.Zero, "the first placement replaces inline values Reserve wrote on Home");
            Assert.That(unchanged, Is.Zero, "an unchanged banner writes no text, icon or class");
            Assert.That(m_reticles.ShownCount, Is.EqualTo(2));
            yield return null;
            Assert.That(m_reticles.GetReticle(0).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_view.HasRootClass(GameplayHudView.TargetingClass), Is.True);
        }

        [Test]
        public void AnUnreadableReticleSourceKeepsOnlyTheOutline()
        {
            m_view = new GameplayHudView(Root());
            m_texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            m_texture.Apply(false, true);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not readable"));

            m_reticles = new RocketReticleView(m_view.TargetLayer, m_texture);
            m_reticles.Reserve(1);

            Assert.That(m_reticles.HasWhiteCopy, Is.False);
            Assert.That(m_reticles.GetReticle(0).Q(className: RocketReticleView.OutlineClass), Is.Not.Null);
        }

        private void WriteTickValues(string timer, string freeze, float fill, ScreenPoint reticle)
        {
            m_view.SetTimer(timer);
            m_view.SetFreezeCounter(freeze, fill);
            m_reticles.Show(0, reticle, 40f + fill);
            m_reticles.HideFrom(1);
        }

        // A mouse press and release on the element, which Button's Clickable turns into clicked (HubTestSupport).
        private static void Click(VisualElement element)
        {
            Send(element, EventType.MouseDown);
            Send(element, EventType.MouseUp);
        }

        private static void Send(VisualElement element, EventType type)
        {
            Event systemEvent = new Event
            {
                type = type,
                mousePosition = element.worldBound.center,
                button = 0,
                clickCount = 1
            };
            using (EventBase mouseEvent = type == EventType.MouseDown
                       ? MouseDownEvent.GetPooled(systemEvent)
                       : (EventBase)MouseUpEvent.GetPooled(systemEvent))
            {
                mouseEvent.target = element;
                element.SendEvent(mouseEvent);
            }
        }

        private VisualElement Root()
        {
            VisualElement root = m_document.rootVisualElement.Q(GameplayHudView.RootName);
            Assert.That(root, Is.Not.Null);
            return root;
        }

        private VisualElement Find(string name)
        {
            VisualElement element = m_document.rootVisualElement.Q(name);
            Assert.That(element, Is.Not.Null, name);
            return element;
        }

        // Inverts RuntimePanelUtils.ScreenToPanel and flips back to screen space (origin bottom left).
        private Vector2 ToScreen(Vector2 panelPoint)
        {
            IPanel panel = m_document.rootVisualElement.panel;
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 unit = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one) - origin;
            Vector2 flipped = new Vector2((panelPoint.x - origin.x) / unit.x, (panelPoint.y - origin.y) / unit.y);
            return new Vector2(flipped.x, Screen.height - flipped.y);
        }
    }
}
#endif
