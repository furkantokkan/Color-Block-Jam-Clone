using System;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Haptics;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// The only owner of the level session: its state and the transition table, the current attempt
    /// with its token and disposables, the fixed Suspend, Teardown and Won Finish steps
    /// (PROJECT.md Runtime Lifecycle), and the one booster use or opening an attempt applies
    /// (B-13, B-16). Requests outside the table or after disposal are refused; state observers run
    /// only after the transition's owned work is ready.
    /// </summary>
    public sealed class LevelSession : IDisposable
    {
        // The longest bounded Won presentation before Teardown, in foreground seconds (DEC-93).
        private const float k_maxFinishSeconds = 0.8f;

        private readonly DragInputRouter m_router;
        private readonly IPointerSource m_pointer;
        private readonly IBoardPresenter m_presenter;
        private readonly ICountdown m_countdown;

        // The countdown's time-booster side (B-8); null for a countdown without it, which refuses time boosters.
        private readonly IFreezeCountdown m_freeze;
        private readonly IPauseOverlay m_pauseOverlay;
        private readonly IAttemptHookSource m_hookSource;
        private readonly IFinishDeadline m_finishDeadline;
        private readonly Action m_completeFinish;
        private readonly SessionStateMachine m_machine = new SessionStateMachine();
        private readonly ReactiveProperty<SessionState> m_state = new ReactiveProperty<SessionState>(SessionState.Home);

        // HUD values (B-9): written only in the view sync phase and at Start and Teardown, and SetValue
        // notifies only on change, so a steady tick publishes nothing and allocates nothing.
        private readonly ReactiveProperty<int> m_displayedSeconds = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> m_freezeSeconds = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<bool> m_isFrozen = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> m_isOpening = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> m_isOpeningDimmed = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> m_isFinishing = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<int> m_boardRevision = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<bool> m_homeBackgrounded = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> m_isTimerStarted = new ReactiveProperty<bool>(false);

        private AttemptLevel m_level;
        private LevelAttempt m_attempt;
        private ICommandSource m_source;
        private int m_generation;
        private bool m_finishPending;
        private bool m_disposed;

        // The one booster use of the attempt in flight (B-13): captured, pending while its debit is written,
        // then committed until the next command phase applies it. Teardown discards it without refund.
        private BoosterIntent m_intent;
        private bool m_hasIntent;
        private bool m_writePending;
        private bool m_intentCommitted;

        // A foreground return during the write: the interim resume (DEC-116) waits for the write to resolve.
        private bool m_foregroundResumeDeferred;

        // The opening of a starting-booster Play (B-16): an intro of Playing time (the dim with the booster icons, then
        // its fade), the boosters applied once in the next command phase, then a hold while the Rocket flies. Pause
        // keeps the progress; Teardown ends it.
        private OpeningPlan m_opening;
        private bool m_openingPending;
        private bool m_openingActive;
        private float m_introRemaining;
        private float m_holdRemaining;

        // Vibrates when a committed booster applies and on Won and Failed (DEC-156); null: nothing.
        private IHaptics m_haptics;

        public LevelSession(DragInputRouter router, IPointerSource pointer, IBoardPresenter presenter,
            ICountdown countdown, IPauseOverlay pauseOverlay, IAttemptHookSource hookSource, IFinishDeadline finishDeadline)
        {
            m_router = router ?? throw new ArgumentNullException(nameof(router));
            m_pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            m_presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            m_countdown = countdown ?? throw new ArgumentNullException(nameof(countdown));
            m_freeze = countdown as IFreezeCountdown;
            m_pauseOverlay = pauseOverlay ?? throw new ArgumentNullException(nameof(pauseOverlay));
            m_hookSource = hookSource ?? throw new ArgumentNullException(nameof(hookSource));
            m_finishDeadline = finishDeadline ?? throw new ArgumentNullException(nameof(finishDeadline));
            m_completeFinish = CompleteFinish;
        }

        /// <summary>The one session state; it changes only on transitions, never in a steady tick.</summary>
        public IReadOnlyReactiveProperty<SessionState> State => m_state;

        public PauseReason PauseReason => m_machine.PauseReason;

        /// <summary>The current attempt, or null after Teardown.</summary>
        public LevelAttempt Attempt => m_attempt;

        /// <summary>The main countdown's whole seconds, rounded up (B-9).</summary>
        public IReadOnlyReactiveProperty<int> DisplayedSeconds => m_displayedSeconds;

        /// <summary>The Time Freeze counter's whole seconds, rounded up; 0 while no freeze runs (B-9).</summary>
        public IReadOnlyReactiveProperty<int> FreezeDisplayedSeconds => m_freezeSeconds;

        public IReadOnlyReactiveProperty<bool> IsFrozen => m_isFrozen;

        /// <summary>True from a starting-booster Play until its hold ends; the HUD boosters stay off meanwhile.</summary>
        public IReadOnlyReactiveProperty<bool> IsOpening => m_isOpening;

        /// <summary>
        /// True while the opening dims the screen and shows its boosters: from Play until the dim starts to fade,
        /// <see cref="OpeningPlan.k_dimFadeSeconds"/> before the boosters apply.
        /// </summary>
        public IReadOnlyReactiveProperty<bool> IsOpeningDimmed => m_isOpeningDimmed;

        /// <summary>
        /// True from Won until its bounded Finish ends and the attempt is torn down (DEC-93); it turns false only after
        /// Teardown has returned every piece, so a result popup opened on that change has nothing live behind it.
        /// </summary>
        public IReadOnlyReactiveProperty<bool> IsFinishing => m_isFinishing;

        /// <summary>Counts view-sync phases whose tick changed the board; the targeting overlay rebuilds on a change.</summary>
        public IReadOnlyReactiveProperty<int> BoardRevision => m_boardRevision;

        /// <summary>
        /// True while the app is in the background on Home (B-16): a Home-side starting-booster commit
        /// waits for the foreground before it plays.
        /// </summary>
        public IReadOnlyReactiveProperty<bool> IsHomeBackgrounded => m_homeBackgrounded;

        /// <summary>
        /// True once the player pressed a block in this attempt, which starts the countdown (DEC-189); false on Home,
        /// after Teardown and until the first press of a new attempt.
        /// </summary>
        public IReadOnlyReactiveProperty<bool> IsTimerStarted => m_isTimerStarted;

        /// <summary>True while an accepted booster's stock debit is being written.</summary>
        public bool IsBoosterWritePending => m_writePending;

        /// <summary>True when a booster's debit committed and its effect waits for the next command phase.</summary>
        public bool HasCommittedBooster => m_hasIntent && m_intentCommitted;

        /// <summary>True when the countdown can freeze and add time, so Time Freeze and Extra Time can apply.</summary>
        public bool SupportsTimeBoosters => m_freeze != null;

        /// <summary>
        /// Sets the haptics a committed booster and the Won and Failed transitions play (DEC-156); null plays nothing.
        /// The Game scene sets it once at scene build; the Vibration setting gate lives in <see cref="IHaptics"/>.
        /// </summary>
        public void SetHaptics(IHaptics haptics)
        {
            m_haptics = haptics;
        }

        /// <summary>
        /// Builds an attempt for the level on Home, where allocation is allowed: hooks, simulation,
        /// pooled roots and their placement. A previously prepared attempt is torn down first. A level
        /// the presenter rejects returns false with its error and commits no attempt.
        /// </summary>
        public bool TryPrepare(AttemptLevel level, out string error)
        {
            if (m_disposed)
            {
                error = "A disposed session cannot prepare an attempt.";
                return false;
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (m_machine.State != SessionState.Home)
            {
                error = "An attempt can only be prepared on Home.";
                return false;
            }

            if (m_attempt != null)
            {
                Teardown();
            }

            return TryCommitAttempt(level, out error);
        }

        public bool Play()
        {
            return Play(OpeningPlan.None);
        }

        /// <summary>
        /// Plays the prepared attempt with the starting boosters whose debit already committed on Home
        /// (B-16). The plan's intro runs first (dimmed screen, then its fade); the next command phase applies
        /// Extra Time, then the Rocket, once; the plan's hold then lets the Rocket fly. Intro and hold keep the
        /// countdown, the pointer and the command source off. An empty plan plays normally, and a second Play
        /// is refused, so nothing applies twice.
        /// </summary>
        public bool Play(in OpeningPlan plan)
        {
            if (m_disposed || m_attempt == null)
            {
                return false;
            }

            if (plan.HasExtraTime && m_freeze == null)
            {
                throw new InvalidOperationException("Extra Time needs a countdown that can add time.");
            }

            if (!m_machine.TryApply(SessionRequest.Play))
            {
                return false;
            }

            if (!plan.IsEmpty)
            {
                m_opening = plan;
                m_openingPending = true;
                m_openingActive = true;
                m_introRemaining = plan.IntroSeconds;
                m_holdRemaining = plan.HoldSeconds;
            }

            Start();
            m_state.SetValue(SessionState.Playing);
            return true;
        }

        /// <summary>Pauses a Playing session (Suspend), or latches a Paused one to Background.</summary>
        public bool Pause(PauseReason reason)
        {
            if (m_disposed)
            {
                return false;
            }

            SessionState from = m_machine.State;
            if (!m_machine.TryPause(reason))
            {
                return false;
            }

            if (from == SessionState.Playing)
            {
                Suspend();
            }

            m_state.SetValue(SessionState.Paused);
            return true;
        }

        /// <summary>
        /// Continues a paused attempt without replaying anything; an interrupted move or a committed
        /// booster runs first in the next tick. Refused while a booster's debit is being written (B-13).
        /// </summary>
        public bool Resume()
        {
            if (m_disposed || m_writePending || !m_machine.TryApply(SessionRequest.Resume))
            {
                return false;
            }

            ResumeGameplay();
            m_state.SetValue(SessionState.Playing);
            return true;
        }

        /// <summary>
        /// Tears the attempt down and rebuilds the same immutable level; nothing crosses attempts (AC-5.3).
        /// A committed but unapplied booster is discarded without refund; an unresolved write refuses it.
        /// </summary>
        public bool Restart()
        {
            if (m_disposed || m_level == null || m_writePending || !m_machine.TryApply(SessionRequest.Restart))
            {
                return false;
            }

            if (m_attempt != null)
            {
                Teardown();
            }

            if (!TryCommitAttempt(m_level, out string error))
            {
                // m_level is only kept after it built once, so only a broken presenter lands here.
                throw new InvalidOperationException(error);
            }

            Start();
            m_state.SetValue(SessionState.Playing);
            return true;
        }

        /// <summary>
        /// Ends the attempt and returns to Home; scene navigation belongs to the Hub task. Like Restart it
        /// discards a committed but unapplied booster and is refused while a write is unresolved.
        /// </summary>
        public bool GoHome()
        {
            if (m_disposed || m_writePending || !m_machine.TryApply(SessionRequest.Home))
            {
                return false;
            }

            // From Won this also forces a pending Finish to end (DEC-93).
            if (m_attempt != null)
            {
                Teardown();
            }

            m_state.SetValue(SessionState.Home);
            return true;
        }

        /// <summary>
        /// Forwards OS backgrounding (AC-3.12). Backgrounding suspends Playing, latches Paused to
        /// Background, latches IsHomeBackgrounded on Home and forces a pending Won Finish to clean up at
        /// once (DEC-93). Returning resumes only a Background pause, and only when the pause overlay
        /// allows it (DEC-116); during a booster write that resume waits for the write to resolve (B-13).
        /// </summary>
        public void NotifyBackground(bool backgrounded)
        {
            if (m_disposed)
            {
                return;
            }

            SessionState state = m_machine.State;
            if (backgrounded)
            {
                m_foregroundResumeDeferred = false;
                if (state == SessionState.Home)
                {
                    m_homeBackgrounded.SetValue(true);
                }
                else if (state == SessionState.Playing || state == SessionState.Paused)
                {
                    Pause(PauseReason.Background);
                }
                else if (state == SessionState.Won)
                {
                    CompleteFinish();
                }

                return;
            }

            m_homeBackgrounded.SetValue(false);
            if (m_pauseOverlay.ResumesOnForeground)
            {
                ResumeFromBackground();
            }
        }

        /// <summary>
        /// Resumes a Background pause after the foreground return. During a booster write the resume
        /// waits for the write to resolve (B-13); false when nothing resumed now.
        /// </summary>
        public bool ResumeFromBackground()
        {
            if (m_disposed || m_machine.State != SessionState.Paused || m_machine.PauseReason != PauseReason.Background)
            {
                return false;
            }

            if (m_writePending)
            {
                m_foregroundResumeDeferred = true;
                return false;
            }

            return Resume();
        }

        /// <summary>
        /// Accepts one captured booster use of the current attempt (B-13): only while Playing, outside the
        /// opening, with no other use in flight, and for Time Freeze only while no freeze runs. It drops the
        /// held drag and any interrupted move, then enters Paused(Saving), whose Suspend ends targeting and
        /// snaps the dropped block's root onto its committed cell, so only the intent survives. A caller
        /// inside the tick returns at once, and CompleteBoosterCommit reports the debit's result. False
        /// changes nothing.
        /// </summary>
        public bool TrySubmitBooster(in BoosterIntent intent)
        {
            if (m_disposed || m_attempt == null || m_machine.State != SessionState.Playing || m_hasIntent
                || m_openingActive || intent.Generation != m_attempt.Generation)
            {
                return false;
            }

            if (intent.IsTimeFreeze)
            {
                if (m_freeze == null || m_freeze.IsFrozen)
                {
                    return false;
                }
            }
            else if (!intent.IsBoard)
            {
                return false;
            }

            m_intent = intent;
            m_hasIntent = true;
            m_intentCommitted = false;
            m_writePending = true;

            // PROJECT.md Paused(Saving): drag commands are cleared before Suspend, which then snaps the
            // dropped block onto the committed cell the domain keeps.
            DropDragMove(m_attempt.Simulation.Drag);
            Pause(PauseReason.Saving);
            return true;
        }

        /// <summary>
        /// Reports the debit of the pending booster use (B-13). A stale generation, a duplicate call or a
        /// call without a pending write does nothing. A committed debit latches the intent for the next
        /// command phase; a failed one drops it. Play resumes only while the pause is still Saving; after
        /// backgrounding the session stays paused (a committed intent waits for Resume), unless the app
        /// already returned and a foreground resume waits for this result (ResumeFromBackground).
        /// </summary>
        public void CompleteBoosterCommit(int generation, bool committed)
        {
            if (m_disposed || !m_writePending || m_attempt == null || generation != m_intent.Generation)
            {
                return;
            }

            m_writePending = false;
            if (committed)
            {
                m_intentCommitted = true;
            }
            else
            {
                ClearIntent();
            }

            if (m_machine.State == SessionState.Paused && m_machine.PauseReason == PauseReason.Saving)
            {
                m_machine.TryApply(committed ? SessionRequest.CommitSucceeded : SessionRequest.CommitFailed);
                ResumeGameplay();
                m_state.SetValue(SessionState.Playing);
                return;
            }

            // Set only by ResumeFromBackground, whose caller already decided to resume.
            if (m_foregroundResumeDeferred)
            {
                m_foregroundResumeDeferred = false;
                ResumeFromBackground();
            }
        }

        /// <summary>
        /// Suspends Playing into Paused(Purchase) for the zero-stock purchase presentation (B-18). Refused
        /// outside Playing, during the opening and while a booster use is in flight.
        /// </summary>
        public bool TryOpenPurchase()
        {
            if (m_disposed || m_hasIntent || m_openingActive)
            {
                return false;
            }

            return Pause(PauseReason.Purchase);
        }

        /// <summary>Resumes a Paused(Purchase) session; after backgrounding the pause stays for the pause flow.</summary>
        public bool ClosePurchase()
        {
            if (m_disposed || !m_machine.TryApply(SessionRequest.PurchaseClosed))
            {
                return false;
            }

            ResumeGameplay();
            m_state.SetValue(SessionState.Playing);
            return true;
        }

        /// <summary>
        /// Attaches a command source (a replay, a bot) to the current attempt, or detaches it with null.
        /// The command phase asks it for one command per tick while no drag is held (DEC-122).
        /// Teardown drops it, so a source never drives a later attempt. False when no attempt exists.
        /// </summary>
        public bool TrySetCommandSource(ICommandSource source)
        {
            if (m_disposed || m_attempt == null)
            {
                return false;
            }

            m_source = source;
            return true;
        }

        /// <summary>
        /// One Playing tick in fixed phase order (B-15): the committed booster or the opening boosters,
        /// the interrupted move, the pointer and the command source (held off by the opening hold), the
        /// Won check, the timer slot, then view sync with the HUD values. A step that leaves Playing, such
        /// as an accepted booster target, ends the tick at once. Outside Playing it does nothing.
        /// </summary>
        public void RunTick(float deltaTime)
        {
            if (m_disposed || m_machine.State != SessionState.Playing)
            {
                return;
            }

            BoardSimulation simulation = m_attempt.Simulation;
            simulation.Events.Clear();

            // Every command applied this tick is stamped with this tick and the Playing time so far.
            simulation.Commands.AdvanceTick(deltaTime);

            // Phase 1: commands (RULE-18, DEC-122). A committed booster or the opening boosters apply
            // first and once, before the timer (B-13, B-16), then an interrupted drag's move, then the
            // pointer's command and one command from an attached source.
            ApplyCommittedBooster(simulation);
            ApplyOpening(simulation);
            if (simulation.Drag.TryTakePending(out GameCommand interrupted))
            {
                simulation.Commands.Execute(interrupted);
            }

            bool holding = m_openingActive;
            if (!holding)
            {
                m_router.Run(m_attempt, m_presenter);

                // An accepted booster target suspends inside the pointer step: nothing else runs this tick.
                if (m_machine.State != SessionState.Playing)
                {
                    return;
                }

                RunCommandSource(simulation);
                if (m_machine.State != SessionState.Playing)
                {
                    return;
                }
            }

            // Won is checked once, after the command phase and before the timer (RULE-29, V-E16).
            if (simulation.IsCleared)
            {
                EnterWon();
                return;
            }

            // Phase 2: the timer slot. The countdown (a Time Freeze included) waits for the player's first
            // press on a block (DEC-189). The opening hold keeps the pointer off, so no block can be pressed
            // before it ends and its Playing time never reaches the countdown.
            if (holding)
            {
                SpendHold(deltaTime);
            }
            else if (m_attempt.HasTouchedBlock && m_countdown.Tick(deltaTime))
            {
                EnterFailed();
                return;
            }

            // Phase 3: view sync and the HUD values.
            m_presenter.Sync(simulation, m_router.LastPointer);
            UpdateHud(simulation);
        }

        /// <summary>Ends a pending Won Finish and tears the attempt down; calling it again does nothing.</summary>
        public void CompleteFinish()
        {
            if (m_disposed || !m_finishPending)
            {
                return;
            }

            // Teardown step 3 stops the deadline handle.
            Teardown();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;

            // A pending Won Finish belongs to the attempt, so Teardown also forces it to end.
            if (m_attempt != null)
            {
                Teardown();
            }

            m_state.Dispose();
            m_displayedSeconds.Dispose();
            m_freezeSeconds.Dispose();
            m_isFrozen.Dispose();
            m_isOpening.Dispose();
            m_isOpeningDimmed.Dispose();
            m_isFinishing.Dispose();
            m_boardRevision.Dispose();
            m_homeBackgrounded.Dispose();
            m_isTimerStarted.Dispose();
        }

        // The presenter builds and places first, so a rejected level leaves no attempt, lifetime or
        // level behind; only then does the session take the level and the new attempt.
        private bool TryCommitAttempt(AttemptLevel level, out string error)
        {
            // The bound hook source decides the attempt's hooks, so effects plug in without a session change (DEC-72).
            AttemptHooks hooks = m_hookSource.Create(level);
            BoardSimulation simulation = new BoardSimulation(level.Model, hooks, DragSettings.Default);
            if (!m_presenter.TryBuild(level, out error))
            {
                return false;
            }

            // The presenter reads the board as the player sees it, held and interrupted moves included.
            m_presenter.Place(simulation.Preview);
            m_level = level;
            m_attempt = new LevelAttempt(level, simulation, ++m_generation);
            return true;
        }

        private void Start()
        {
            m_countdown.Start(m_attempt.Level.Model.TimerSeconds);
            m_pointer.Enable();
            ResetHud();
        }

        // The Resume steps (PROJECT.md): tweens, clocks, then input, the reverse of Suspend.
        private void ResumeGameplay()
        {
            m_presenter.SetTweensPaused(false);
            m_countdown.SetPaused(false);
            m_pointer.Enable();
        }

        // A committed booster applies once, as the first command of the first resumed command phase (B-13).
        // Its board command was validated when captured and nothing changed the board since; Time Freeze
        // was refused at capture while a freeze ran, so neither call can throw here.
        private void ApplyCommittedBooster(BoardSimulation simulation)
        {
            if (!m_hasIntent || !m_intentCommitted)
            {
                return;
            }

            BoosterIntent intent = m_intent;
            ClearIntent();
            if (intent.IsBoard)
            {
                simulation.Commands.Execute(intent.Command);
            }
            else if (intent.IsTimeFreeze && m_freeze != null)
            {
                m_freeze.TryFreeze(intent.FreezeSeconds);
            }

            m_haptics?.Play(HapticFeedback.BoosterApplied);
        }

        // The opening boosters apply once, in the first command phase after the intro: Extra Time, then the
        // Rocket (B-16). Play refused a plan with Extra Time when the countdown cannot add time, and the
        // intro holds the countdown, so it cannot have run out before this.
        private void ApplyOpening(BoardSimulation simulation)
        {
            if (!m_openingPending || m_introRemaining > 0f)
            {
                return;
            }

            m_openingPending = false;
            if (m_opening.HasExtraTime)
            {
                m_freeze.AddSeconds(m_opening.BonusSeconds);
            }

            if (m_opening.HasRocket)
            {
                simulation.Commands.Execute(m_opening.Rocket);
            }
        }

        // Spends the tick's Playing time on the opening: the intro first, then the hold. The tick that ends the
        // intro gives its rest to nothing: the boosters apply in the next command phase, and the hold is counted
        // from there, so the Rocket always gets its whole flight before input opens. The rest of the tick that
        // ends the hold goes nowhere either: the countdown has not started yet (DEC-189).
        private void SpendHold(float deltaTime)
        {
            float delta = deltaTime > 0f ? deltaTime : 0f;
            if (m_introRemaining > 0f)
            {
                m_introRemaining = delta < m_introRemaining ? m_introRemaining - delta : 0f;
                return;
            }

            if (m_openingPending)
            {
                return;
            }

            if (delta < m_holdRemaining)
            {
                m_holdRemaining -= delta;
                return;
            }

            m_holdRemaining = 0f;
            m_openingActive = false;
        }

        // Drops the held drag and any interrupted move for Paused(Saving) (B-13), so the preview returns to
        // the committed board. A held block keeps the presenter's held mark for Suspend's SnapHeld. A move
        // parked by an earlier Suspend left its root on the parked cell and unmarked; marking it again lets
        // that same SnapHeld put the root back on the committed cell.
        private void DropDragMove(DragResolver drag)
        {
            bool held = drag.IsActive;
            drag.Interrupt();
            if (drag.TryTakePending(out GameCommand dropped) && !held)
            {
                m_presenter.SetHeld(dropped.Block);
            }
        }

        private void ClearIntent()
        {
            m_intent = default;
            m_hasIntent = false;
            m_writePending = false;
            m_intentCommitted = false;
        }

        private void ClearOpening()
        {
            m_opening = OpeningPlan.None;
            m_openingPending = false;
            m_openingActive = false;
            m_introRemaining = 0f;
            m_holdRemaining = 0f;
        }

        // View sync of the HUD values; each changes, and notifies, only when its whole value changes.
        private void UpdateHud(BoardSimulation simulation)
        {
            m_displayedSeconds.SetValue(DisplaySeconds.Calculate(m_countdown.Remaining));
            if (m_freeze != null)
            {
                m_freezeSeconds.SetValue(DisplaySeconds.Calculate(m_freeze.FreezeRemaining));
                m_isFrozen.SetValue(m_freeze.IsFrozen);
            }

            m_isOpening.SetValue(m_openingActive);
            m_isOpeningDimmed.SetValue(IsDimmed());
            m_isTimerStarted.SetValue(m_attempt.HasTouchedBlock);
            if (simulation.Events.Count > 0)
            {
                m_boardRevision.SetValue(m_boardRevision.Value + 1);
            }
        }

        // Start and Teardown values: the clock as it stands, no freeze, the opening flag, revision 0.
        private void ResetHud()
        {
            m_displayedSeconds.SetValue(DisplaySeconds.Calculate(m_countdown.Remaining));
            m_freezeSeconds.SetValue(0);
            m_isFrozen.SetValue(false);
            m_isOpening.SetValue(m_openingActive);
            m_isOpeningDimmed.SetValue(IsDimmed());
            m_isTimerStarted.SetValue(false);
            m_boardRevision.SetValue(0);
        }

        // The dim shows for the intro except its last k_dimFadeSeconds, in which the layer fades out.
        private bool IsDimmed()
        {
            return m_openingActive && m_introRemaining > OpeningPlan.k_dimFadeSeconds;
        }

        // A source never moves a block the pointer holds or an interrupted move still owns, and it
        // stops offering once the board is cleared, so the Won check follows the last exit.
        private void RunCommandSource(BoardSimulation simulation)
        {
            if (m_source == null || simulation.IsCleared || simulation.Drag.IsActive || simulation.Drag.HasPending)
            {
                return;
            }

            if (m_source.TryGetNext(simulation.Board, out GameCommand command))
            {
                CommandOutcome outcome = simulation.Commands.Execute(command);
                m_source.OnExecuted(command, outcome);
            }
        }

        // Suspend (PROJECT.md): input off and drag interrupted, clocks paused, tweens paused. The router
        // reset also ends any booster targeting mode (B-12).
        private void Suspend()
        {
            m_pointer.Disable();
            m_router.Reset(m_attempt);
            m_presenter.SnapHeld();
            m_countdown.SetPaused(true);
            m_presenter.SetTweensPaused(true);
        }

        // Teardown (PROJECT.md): input off, clocks stopped, tweens stopped, token cancelled,
        // disposables disposed, pieces returned. The attempt is dropped as a unit, with any booster use
        // or opening it still held (no refund, B-13) and the HUD values reset.
        private void Teardown()
        {
            m_pointer.Disable();
            m_router.Reset(m_attempt);
            m_presenter.SnapHeld();
            m_countdown.Stop();
            m_presenter.StopTweens();
            if (m_finishPending)
            {
                m_finishPending = false;
                m_finishDeadline.Cancel();
            }

            m_attempt.Lifetime.Cancel();
            m_attempt.Lifetime.Dispose();
            m_presenter.ReturnPieces();
            m_source = null;
            m_attempt = null;
            ClearIntent();
            m_foregroundResumeDeferred = false;
            ClearOpening();
            ResetHud();

            // Last: observers of the Finish end see a fully released attempt.
            m_isFinishing.SetValue(false);
        }

        // Won Finish (DEC-93): the latch rejects further commands at once, the clocks stop, and only the
        // final exit motion may run for at most 0.8 foreground seconds before Teardown.
        private void EnterWon()
        {
            m_machine.TryApply(SessionRequest.AllRequiredCleared);
            m_pointer.Disable();
            m_router.Reset(m_attempt);
            m_presenter.SnapHeld();
            m_countdown.Stop();
            m_presenter.Sync(m_attempt.Simulation, m_router.LastPointer);
            UpdateHud(m_attempt.Simulation);

            float seconds = Math.Min(k_maxFinishSeconds, m_presenter.FinishSeconds);
            if (seconds <= 0f)
            {
                Teardown();
            }
            else
            {
                // Owner: this session. Teardown cancels on backgrounding, Home or Dispose.
                // The cached callback allocates nothing when a finish starts.
                m_finishPending = true;
                m_finishDeadline.Start(seconds, m_completeFinish);
                m_isFinishing.SetValue(true);
            }

            // Observers may call Home or Dispose synchronously, so Finish must already be ready.
            m_haptics?.Play(HapticFeedback.LevelWon);
            m_state.SetValue(SessionState.Won);
        }

        // Failed (RULE-19): the latch, then Teardown at once; the static result capture is a later task.
        private void EnterFailed()
        {
            m_machine.TryApply(SessionRequest.TimerExpired);
            Teardown();
            // A synchronous Restart observer must receive a fully released previous attempt.
            m_haptics?.Play(HapticFeedback.LevelFailed);
            m_state.SetValue(SessionState.Failed);
        }
    }
}
