#if UNITY_EDITOR
using System;
using System.Collections;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Player;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using SessionState = ColorBlockJam.Domain.Session.SessionState;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// The in-game zero-stock purchase on a laid-out HUD panel (AC-12, B-18, B-32): the real booster bar, use coordinator,
    /// InGamePurchaseHost and shared popup over GameplayHud.uxml, with an isolated in-memory stock and shop. A press on an
    /// empty booster button shows the popup above the bar under Paused(Purchase), its scrim blocking the board; Buy
    /// grants stock, refreshes the badge and keeps the popup open without casting; a refused or failed purchase shows its
    /// message and changes nothing; a second Buy while saving is refused; the close button and a scrim tap resume; a
    /// result that arrives after closing shows nothing and pauses nothing.
    /// </summary>
    public sealed class InGamePurchasePopupTests
    {
        private const string k_hudPath = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";
        private const int k_rocketSlot = 1;

        // The shared popup's element names and messages (ColorBlockJam.UI); this test assembly does not reference it.
        private const string k_screenName = "booster-purchase-screen";
        private const string k_scrimName = "booster-purchase-scrim";
        private const string k_buyName = "booster-purchase-buy";
        private const string k_closeName = "booster-purchase-close";
        private const string k_messageName = "booster-purchase-message";
        private const string k_purchasedPrefix = "Purchased! You have ";
        private const string k_notEnoughPrefix = "Not enough ";
        private const string k_saveFailedMessage = "Couldn't save. Try again.";

        private BoosterPresenterScene m_scene;
        private GameObject m_hudHost;
        private UIDocument m_hudDocument;
        private HudBoosterStore m_store;
        private BoosterTargeting m_targeting;
        private ScriptedPointerSource m_pointer;
        private LevelSession m_session;
        private InGamePurchaseHost m_host;
        private BoosterUseCoordinator m_coordinator;
        private Material m_frostMaterial;
        private TimeFreezeFrostView m_frost;
        private Texture2D m_reticleTexture;
        private GameplayHud m_hud;
        private AttemptLevel m_level;
        private BoosterDefinition m_rocket;

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
            CameraBoardProjector projector = new CameraBoardProjector(m_scene.Camera, m_scene.Environment.BoardRoot,
                ExitMotionSettings.k_blockTopHeight);
            DragInputRouter router = new DragInputRouter(m_pointer, new UiToolkitPointerFilter(new[] { m_hudDocument }),
                projector, m_targeting);
            m_session = new LevelSession(router, m_pointer, m_scene.Presenter, new AttemptCountdown(), new NoPauseOverlay(true),
                m_scene.Hooks, new PrimeTweenFinishDeadline());
            m_frostMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m_frost = new TimeFreezeFrostView(new TimeFreezeFrostContext(m_scene.Camera, m_frostMaterial));
            m_reticleTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            GameplayHudContext context = new GameplayHudContext(m_hudDocument, m_reticleTexture);
            m_host = new InGamePurchaseHost(context, m_session, m_store, m_store);
            m_coordinator = new BoosterUseCoordinator(m_session, m_store, m_store, m_store, m_scene.Catalog, m_targeting,
                m_host);
            m_hud = new GameplayHud(context, m_session, m_targeting, m_coordinator, m_scene.Catalog, m_store, m_store,
                projector, m_frost);
            Assert.That(m_scene.Catalog.TryGet(BoosterId.Rocket, out m_rocket), Is.True);

            LevelData data = BoardPresenterBoosterTests.SplitLevel();
            m_level = m_scene.Convert(data);
            m_scene.PrepareEffects(data, m_level);
            Assert.That(m_session.TryPrepare(m_level, out string error), Is.True, error);
            m_hud.Show(m_level);
            Assert.That(m_session.Play(), Is.True);
            m_store.SetStock(BoosterId.Rocket, 0);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            m_store?.PurchaseGate?.TrySetResult(true);
            if (m_host != null)
            {
                yield return WaitFor(m_host.PendingPurchase);
            }

            // The Game scene container's order: the HUD, then the host, then the coordinator and the session.
            m_hud?.Dispose();
            m_host?.Dispose();
            m_coordinator?.Dispose();
            m_session?.Dispose();
            m_frost?.Dispose();
            m_targeting?.Dispose();
            m_store?.Dispose();
            m_scene?.Dispose();
            Object.Destroy(m_hudHost);
            Object.Destroy(m_frostMaterial);
            Object.Destroy(m_reticleTexture);
        }

        [UnityTest]
        public IEnumerator AnEmptyBoosterButtonShowsThePopupAboveTheBarUnderPausedPurchase()
        {
            VisualElement screen = Find(k_screenName);
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the popup starts hidden");
            Button rocket = m_hud.View.GetButton(k_rocketSlot);
            Assert.That(rocket.ClassListContains(GameplayHudView.EmptyClass), Is.True, "the empty button shows its plus badge");

            Click(rocket);
            yield return null;

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.Purchase));
            Assert.That(m_host.IsShowing, Is.True);
            Assert.That(m_host.Booster, Is.SameAs(m_rocket));
            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            IPanel panel = m_hudDocument.rootVisualElement.panel;
            VisualElement picked = panel.Pick(rocket.worldBound.center);
            Assert.That(picked != null && screen.Contains(picked), Is.True, "the popup draws above the bar and takes its presses");
            UiToolkitPointerFilter filter = new UiToolkitPointerFilter(new[] { m_hudDocument });
            Assert.That(filter.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True,
                "the scrim blocks the board");
            Assert.That(m_store.ConsumeCount, Is.Zero);
            Assert.That(m_store.PurchaseCount, Is.Zero, "opening buys nothing");
        }

        [UnityTest]
        public IEnumerator BuyGrantsStockRefreshesTheBadgeAndKeepsThePopupOpenWithoutCasting()
        {
            yield return OpenRocketPurchase();

            Click(Find(k_buyName));
            yield return WaitFor(m_host.PendingPurchase);
            yield return null;

            int granted = Math.Max(1, m_rocket.PurchaseAmount);
            Assert.That(m_store.PurchaseCount, Is.EqualTo(1));
            Assert.That(m_store.GetStock(BoosterId.Rocket).Value, Is.EqualTo(granted));
            Assert.That(Message().text, Is.EqualTo(k_purchasedPrefix + granted + "."));
            Assert.That(Message().resolvedStyle.visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(m_host.IsShowing, Is.True, "the popup stays open after a purchase");
            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.Purchase), "play stays paused");
            Assert.That(m_targeting.Mode.Value, Is.EqualTo(BoosterId.None), "no automatic selection");
            Assert.That(m_store.ConsumeCount, Is.Zero, "no automatic cast");
            Button rocket = m_hud.View.GetButton(k_rocketSlot);
            Assert.That(rocket.ClassListContains(GameplayHudView.EmptyClass), Is.False, "the badge shows the new stock");
            Assert.That(rocket.Q<Label>(GameplayHudView.BoosterCountLabelName).text, Is.EqualTo(granted.ToString()));
            Assert.That(Find(k_buyName).enabledSelf, Is.True, "another purchase may follow");
        }

        [UnityTest]
        public IEnumerator NotEnoughCoinsShowsItsMessageAndChangesNothing()
        {
            yield return BuyAndExpectNoChange(BoosterPurchaseResult.InsufficientFunds);
            Assert.That(Message().text, Does.StartWith(k_notEnoughPrefix));
        }

        [UnityTest]
        public IEnumerator AFailedSaveShowsItsMessageAndChangesNothing()
        {
            yield return BuyAndExpectNoChange(BoosterPurchaseResult.SaveFailed);
            Assert.That(Message().text, Is.EqualTo(k_saveFailedMessage));
        }

        [UnityTest]
        public IEnumerator ASecondBuyWhileTheFirstIsSavingIsRefused()
        {
            yield return OpenRocketPurchase();
            m_store.PurchaseGate = new TaskCompletionSource<bool>();
            Button buy = Find<Button>(k_buyName);

            Click(buy);
            yield return null;

            Assert.That(m_store.PurchaseCount, Is.EqualTo(1));
            Assert.That(m_store.IsSaving.Value, Is.True);
            Assert.That(buy.enabledSelf, Is.False, "Buy is disabled while the write is pending");
            Click(buy);
            yield return null;
            Assert.That(m_store.PurchaseCount, Is.EqualTo(1), "a duplicate submission never reaches the shop");

            m_store.PurchaseGate.SetResult(true);
            yield return WaitFor(m_host.PendingPurchase);
            yield return null;

            Assert.That(m_store.GetStock(BoosterId.Rocket).Value, Is.EqualTo(Math.Max(1, m_rocket.PurchaseAmount)),
                "one purchase, one grant");
            Assert.That(Message().text, Does.StartWith(k_purchasedPrefix));
            Assert.That(buy.enabledSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator TheCloseButtonAndAScrimTapHideThePopupAndResumePlay()
        {
            yield return OpenRocketPurchase();

            Click(Find(k_closeName));
            yield return null;

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing), "close button");
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(Find(k_screenName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            yield return OpenRocketPurchase();
            VisualElement scrim = Find(k_scrimName);
            Tap(scrim, new Vector2(scrim.worldBound.xMin + 8f, scrim.worldBound.yMin + 8f));
            yield return null;

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing), "scrim tap");
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(m_targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_store.PurchaseCount, Is.Zero);
            Assert.That(m_store.ConsumeCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AResultAfterClosingShowsNothingAndPausesNothing()
        {
            yield return OpenRocketPurchase();
            m_store.PurchaseGate = new TaskCompletionSource<bool>();
            Click(Find(k_buyName));
            yield return null;
            Assert.That(m_store.PurchaseCount, Is.EqualTo(1));

            Click(Find(k_closeName));
            yield return null;
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));

            m_store.PurchaseGate.SetResult(true);
            yield return WaitFor(m_host.PendingPurchase);
            yield return null;

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing), "the late result changes no state");
            Assert.That(m_host.IsShowing, Is.False);
            Assert.That(Find(k_screenName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(Message().resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden), "no message for a closed offer");
            Assert.That(m_store.GetStock(BoosterId.Rocket).Value, Is.EqualTo(Math.Max(1, m_rocket.PurchaseAmount)),
                "the committed write still counts");
            Assert.That(m_hud.View.GetButton(k_rocketSlot).ClassListContains(GameplayHudView.EmptyClass), Is.False);
            Assert.That(m_targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_store.ConsumeCount, Is.Zero);
        }

        private IEnumerator BuyAndExpectNoChange(BoosterPurchaseResult result)
        {
            yield return OpenRocketPurchase();
            m_store.NextPurchase = result;

            Click(Find(k_buyName));
            yield return WaitFor(m_host.PendingPurchase);
            yield return null;

            Assert.That(m_store.PurchaseCount, Is.EqualTo(1));
            Assert.That(m_store.GetStock(BoosterId.Rocket).Value, Is.Zero, result + " changes no stock");
            Assert.That(Message().resolvedStyle.visibility, Is.EqualTo(Visibility.Visible), result + " shows feedback");
            Assert.That(m_host.IsShowing, Is.True);
            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.Purchase));
            Assert.That(m_hud.View.GetButton(k_rocketSlot).ClassListContains(GameplayHudView.EmptyClass), Is.True);
            Assert.That(m_store.ConsumeCount, Is.Zero);
        }

        // Through the real bar button, as a player presses it; one frame lays the popup out for clicks.
        private IEnumerator OpenRocketPurchase()
        {
            Click(m_hud.View.GetButton(k_rocketSlot));
            yield return null;
            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.Purchase));
            Assert.That(m_host.IsShowing, Is.True);
        }

        private static IEnumerator WaitFor(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the purchase finished in time");
        }

        private Label Message() => Find<Label>(k_messageName);

        private VisualElement Find(string name) => Find<VisualElement>(name);

        private T Find<T>(string name) where T : VisualElement
        {
            T element = m_hudDocument.rootVisualElement.Q<T>(name);
            Assert.That(element, Is.Not.Null, name);
            return element;
        }

        // A mouse press and release at the element's centre reach Button's Clickable (GameplayHudLayoutTests' pattern).
        private static void Click(VisualElement element)
        {
            SendMouse(element, EventType.MouseDown);
            SendMouse(element, EventType.MouseUp);
        }

        private static void SendMouse(VisualElement element, EventType type)
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

        // The scrim closes on a ClickEvent, the event a press and release on the same element produce.
        private static void Tap(VisualElement element, Vector2 panelPosition)
        {
            Event systemEvent = new Event
            {
                type = EventType.MouseUp,
                mousePosition = panelPosition,
                button = 0,
                clickCount = 1
            };
            using (ClickEvent click = ClickEvent.GetPooled(systemEvent))
            {
                click.target = element;
                element.SendEvent(click);
            }
        }
    }
}
#endif
