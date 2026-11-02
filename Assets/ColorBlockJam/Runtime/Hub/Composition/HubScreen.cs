using System;
using ColorBlockJam.Haptics;
using ColorBlockJam.Hub.Home;
using ColorBlockJam.Hub.Lives;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Hub.Purchase;
using ColorBlockJam.Hub.Settings;
using ColorBlockJam.Hub.Tabs;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Presentation.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.UI.Haptics;
using ColorBlockJam.UI.Purchase;
using ColorBlockJam.UI.Settings;
using Onity.DI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Hub.Composition
{
    /// <summary>
    /// Connects the Hub UIDocument to its presenters: on <see cref="Show"/> it wraps the Home subtree, the tab bar, the
    /// shared Settings popup, the pre-level popup and the shared purchase popup in views, binds them, registers each
    /// overlay with the navigator under its own id, routes the pre-level purchase request to the purchase overlay,
    /// hooks the button haptics, applies the safe area and enables Back; <c>OnDisable</c> undoes all of it
    /// (Hub-And-Player-Data-Design 6.3, 7, 8, 9, table 10).
    /// </summary>
    /// <remarks>
    /// Thin MonoBehaviour with no Update, coroutine or Invoke. The Hub build callback calls <see cref="Show"/>
    /// once after the project scope is ready; a later re-enable binds again from the same level count. It runs
    /// after UIDocument (execution order), so a re-enable finds the document's rebuilt tree. A tab menu (DEC-169)
    /// registers here with <c>HubNavigator.RegisterMenu</c> after its view binds, and unregisters in Unbind.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HubScreen : MonoBehaviour
    {
        public const string HomeScreenName = "home-screen";
        public const string SafeAreaName = "safe-area";
        public const string TabBarName = "tab-bar";

        /// <summary>The second BoosterPurchasePopup.uxml instance in Hub.uxml, used as the life refill popup (DEC-174).</summary>
        public const string LifeRefillInstanceName = "life-refill-instance";

        // How often the view-owned lives timer runs while Home is bound; the text changes only once per second.
        private const long k_lifeTickMilliseconds = 250;

        [SerializeField] private UIDocument m_document = null;

        private HomePresenter m_homePresenter;
        private HubTabPresenter m_tabPresenter;
        private ButtonHaptics m_buttonHaptics;
        private DummyCurrencyReward m_dummyReward;
        private SettingsOverlay m_settingsOverlay;
        private PreLevelPresenter m_preLevelPresenter;
        private BoosterPurchaseOverlay m_purchaseOverlay;
        private Action<BoosterDefinition> m_onPurchaseRequested;
        private HubNavigator m_navigator;
        private HubBackInput m_backInput;
        private HomeView m_homeView;
        private HubTabBarView m_tabBarView;
        private SettingsPopupView m_settingsView;
        private PreLevelView m_preLevelView;
        private BoosterPurchaseView m_purchaseView;
        private SafeAreaPadding m_contentSafeArea;
        private SafeAreaPadding m_tabBarSafeArea;
        private SafeAreaPadding m_settingsSafeArea;
        private SafeAreaPadding m_preLevelSafeArea;
        private SafeAreaPadding m_purchaseSafeArea;
        private Rect m_pinnedSafeArea;
        private Vector2 m_pinnedScreenSize;
        private bool m_hasPinnedSafeArea;
        private int m_levelCount;
        private bool m_isReady;
        private LifeRefillOverlay m_lifeRefill;
        private ILives m_lives;
        private BoosterPurchaseView m_lifeRefillView;
        private SafeAreaPadding m_lifeRefillSafeArea;
        private IVisualElementScheduledItem m_lifeTicker;
        private Action m_onLifeTick;

        /// <summary>True between a successful bind and the next unbind.</summary>
        public bool IsBound => m_homeView != null;

        /// <param name="haptics">
        /// The project's haptics (DEC-156); null only in test harnesses, which then get no button haptics.
        /// </param>
        [Inject]
        public void Construct(HomePresenter homePresenter, SettingsOverlay settingsOverlay, HubNavigator navigator,
            HubBackInput backInput, PreLevelPresenter preLevelPresenter, BoosterPurchaseOverlay purchaseOverlay,
            DummyCurrencyReward dummyReward, HubTabPresenter tabPresenter, IHaptics haptics)
        {
            m_homePresenter = homePresenter;
            m_tabPresenter = tabPresenter;
            m_buttonHaptics = haptics != null ? new ButtonHaptics(haptics) : null;
            m_dummyReward = dummyReward;
            m_settingsOverlay = settingsOverlay;
            m_navigator = navigator;
            m_backInput = backInput;
            m_preLevelPresenter = preLevelPresenter;
            m_purchaseOverlay = purchaseOverlay;
        }

        /// <summary>
        /// The lives refill popup and the lives it shows (DEC-174). A separate [Inject] method, so harnesses that call
        /// <see cref="Construct"/> directly keep working; without it Hub shows no refill popup and no lives timer.
        /// </summary>
        [Inject]
        public void ConstructLives(LifeRefillOverlay lifeRefill, ILives lives)
        {
            m_lifeRefill = lifeRefill;
            m_lives = lives;
        }

        /// <summary>
        /// Called once by the Hub build callback. A <paramref name="levelCount"/> of 0 means the project
        /// scope failed or the database is empty; Home then shows "Level -" disabled.
        /// </summary>
        public void Show(int levelCount)
        {
            if (m_isReady)
            {
                return;
            }

            m_isReady = true;
            m_levelCount = levelCount;
            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        /// <summary>
        /// Pins the safe area to an explicit surface instead of <see cref="Screen"/>: needed when the panel
        /// renders into a texture (captures) or to simulate a notch. Applies now when bound, and on later binds.
        /// </summary>
        public void SetSafeAreaSource(Rect safeArea, Vector2 screenSize)
        {
            m_pinnedSafeArea = safeArea;
            m_pinnedScreenSize = screenSize;
            m_hasPinnedSafeArea = true;
            if (IsBound)
            {
                m_contentSafeArea.SetSource(safeArea, screenSize);
                m_tabBarSafeArea.SetSource(safeArea, screenSize);
                m_settingsSafeArea.SetSource(safeArea, screenSize);
                m_preLevelSafeArea.SetSource(safeArea, screenSize);
                m_purchaseSafeArea.SetSource(safeArea, screenSize);
                m_lifeRefillSafeArea?.SetSource(safeArea, screenSize);
            }
        }

        private void Reset()
        {
            m_document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (m_isReady)
            {
                Bind();
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (IsBound || m_homePresenter == null || m_tabPresenter == null || m_settingsOverlay == null || m_navigator == null
                || m_backInput == null || m_preLevelPresenter == null || m_purchaseOverlay == null)
            {
                return;
            }

            VisualElement root = m_document != null ? m_document.rootVisualElement : null;
            VisualElement home = root?.Q<VisualElement>(HomeScreenName);
            VisualElement safeArea = home?.Q<VisualElement>(SafeAreaName);
            VisualElement tabBar = home?.Q<VisualElement>(TabBarName);
            VisualElement settings = root?.Q<VisualElement>(SettingsPopupView.ScreenName);
            VisualElement settingsSafeArea = settings?.Q<VisualElement>(SettingsPopupView.SafeAreaName);
            VisualElement preLevel = root?.Q<VisualElement>(PreLevelView.ScreenName);
            VisualElement preLevelSafeArea = preLevel?.Q<VisualElement>(PreLevelView.SafeAreaName);
            VisualElement purchase = root?.Q<VisualElement>(BoosterPurchaseView.ScreenName);
            VisualElement purchaseSafeArea = purchase?.Q<VisualElement>(BoosterPurchaseView.SafeAreaName);
            if (safeArea == null || tabBar == null || settingsSafeArea == null || preLevelSafeArea == null
                || purchaseSafeArea == null)
            {
                Debug.LogError("Hub screen: the UIDocument has no home-screen, safe-area, tab-bar, settings-screen, "
                               + "pre-level-screen or booster-purchase-screen element, or one of their safe areas.", this);
                return;
            }

            m_homeView = new HomeView(home);
            m_tabBarView = new HubTabBarView(tabBar);
            m_settingsView = new SettingsPopupView(settings);
            m_preLevelView = new PreLevelView(preLevel);
            m_purchaseView = new BoosterPurchaseView(purchase);

            // The content pads the top and sides; the tab bar's fill reaches the bottom edge and pads its slots.
            m_contentSafeArea = new SafeAreaPadding(home, safeArea, SafeAreaEdges.Left | SafeAreaEdges.Top | SafeAreaEdges.Right);
            m_tabBarSafeArea = new SafeAreaPadding(home, tabBar, SafeAreaEdges.Left | SafeAreaEdges.Right | SafeAreaEdges.Bottom);

            // Settings measures against Home, which is always laid out; its own root is display:none while closed.
            m_settingsSafeArea = new SafeAreaPadding(home, settingsSafeArea, SafeAreaEdges.All);
            m_preLevelSafeArea = new SafeAreaPadding(home, preLevelSafeArea, SafeAreaEdges.All);
            m_purchaseSafeArea = new SafeAreaPadding(home, purchaseSafeArea, SafeAreaEdges.All);
            if (m_hasPinnedSafeArea)
            {
                m_contentSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
                m_tabBarSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
                m_settingsSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
                m_preLevelSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
                m_purchaseSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
            }
            else
            {
                m_contentSafeArea.Apply();
                m_tabBarSafeArea.Apply();
                m_settingsSafeArea.Apply();
                m_preLevelSafeArea.Apply();
                m_purchaseSafeArea.Apply();
            }

            m_homePresenter.Bind(m_homeView, m_levelCount);
            m_tabPresenter.Bind(m_tabBarView);
            m_dummyReward?.Attach(home);

            // One trickle-down click callback on the document root plays the haptic that each button's USS class
            // names (DEC-156): cbj-haptic-confirm on Level, cbj-haptic-tap on Settings and the tabs.
            m_buttonHaptics?.Attach(root);
            m_settingsOverlay.Bind(m_settingsView, destroyCancellationToken);
            m_navigator.Register(HubOverlayId.Settings, m_settingsOverlay);

            // The pre-level popup opens the purchase popup, so the purchase overlay binds first and unbinds last.
            m_purchaseOverlay.Bind(m_purchaseView, destroyCancellationToken);
            m_navigator.Register(HubOverlayId.BoosterPurchase, m_purchaseOverlay);
            m_preLevelPresenter.Bind(m_preLevelView, m_levelCount, destroyCancellationToken);
            m_navigator.Register(HubOverlayId.PreLevel, m_preLevelPresenter);
            m_onPurchaseRequested ??= OnPurchaseRequested;
            m_preLevelPresenter.PurchaseRequested += m_onPurchaseRequested;
            m_backInput.Enable();
            BindLives(root, home);
        }

        private void Unbind()
        {
            if (!IsBound)
            {
                return;
            }

            m_backInput.Disable();
            UnbindLives();

            // Unregister hides an open overlay before its view goes away; the purchase popup goes last (see Bind).
            m_preLevelPresenter.PurchaseRequested -= m_onPurchaseRequested;
            m_navigator.Unregister(HubOverlayId.PreLevel);
            m_preLevelPresenter.Unbind();
            m_preLevelView.Dispose();
            m_preLevelView = null;
            m_navigator.Unregister(HubOverlayId.BoosterPurchase);
            m_purchaseOverlay.Unbind();
            m_purchaseView.Dispose();
            m_purchaseView = null;

            // Unregister hides Settings when it is open, before its view goes away.
            m_navigator.Unregister(HubOverlayId.Settings);
            m_settingsOverlay.Unbind();
            m_settingsView.Dispose();
            m_settingsView = null;
            m_buttonHaptics?.Detach();
            m_dummyReward?.Detach();
            m_tabPresenter.Unbind();
            m_tabBarView.Dispose();
            m_tabBarView = null;
            m_homePresenter.Unbind();
            m_homeView.Dispose();
            m_homeView = null;
            m_contentSafeArea.Dispose();
            m_contentSafeArea = null;
            m_tabBarSafeArea.Dispose();
            m_tabBarSafeArea = null;
            m_settingsSafeArea.Dispose();
            m_settingsSafeArea = null;
            m_preLevelSafeArea.Dispose();
            m_preLevelSafeArea = null;
            m_purchaseSafeArea.Dispose();
            m_purchaseSafeArea = null;
        }

        // Binds the life refill popup, registers it as a menu and starts the view-owned lives timer (DEC-174). Hub that
        // shows with no life left (the player ran out in Game) opens the popup at once.
        private void BindLives(VisualElement root, VisualElement home)
        {
            if (m_lifeRefill == null || m_lives == null)
            {
                return;
            }

            VisualElement screen = root.Q<VisualElement>(LifeRefillInstanceName)?.Q<VisualElement>(BoosterPurchaseView.ScreenName);
            VisualElement safeArea = screen?.Q<VisualElement>(BoosterPurchaseView.SafeAreaName);
            if (safeArea == null)
            {
                Debug.LogError("Hub screen: Hub.uxml has no '" + LifeRefillInstanceName + "' purchase popup instance, "
                               + "so the life refill popup is unavailable.", this);
                return;
            }

            m_lifeRefillView = new BoosterPurchaseView(screen);
            m_lifeRefillSafeArea = new SafeAreaPadding(home, safeArea, SafeAreaEdges.All);
            if (m_hasPinnedSafeArea)
            {
                m_lifeRefillSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
            }
            else
            {
                m_lifeRefillSafeArea.Apply();
            }

            m_lifeRefill.Bind(m_lifeRefillView, destroyCancellationToken);
            m_navigator.RegisterMenu(LifeRefillOverlay.MenuId, m_lifeRefill);

            // Owner: this screen. Stops: UnbindLives (OnDisable). UI Toolkit runs it with the panel, never in Game.
            m_onLifeTick ??= OnLifeTick;
            m_lifeTicker = root.schedule.Execute(m_onLifeTick).Every(k_lifeTickMilliseconds);
            m_homePresenter.Tick();
            if (m_lives.GetAvailable() <= 0)
            {
                m_lifeRefill.Open();
            }
        }

        private void UnbindLives()
        {
            if (m_lifeRefillView == null)
            {
                return;
            }

            m_lifeTicker?.Pause();
            m_lifeTicker = null;

            // Unregistering hides the popup when it is open, before its view goes away.
            m_navigator.UnregisterMenu(LifeRefillOverlay.MenuId);
            m_lifeRefill.Unbind();
            m_lifeRefillView.Dispose();
            m_lifeRefillView = null;
            m_lifeRefillSafeArea.Dispose();
            m_lifeRefillSafeArea = null;
        }

        private void OnLifeTick()
        {
            m_homePresenter.Tick();
            m_lifeRefill.Tick();
        }

        private void OnPurchaseRequested(BoosterDefinition booster)
        {
            // The purchase popup covers the pre-level popup until it closes, so the two never overlap.
            m_purchaseOverlay.Open(booster, m_preLevelPresenter);
        }
    }
}
