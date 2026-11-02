using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.TopBar;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.DI;
using Onity.Unity.Async;
using Onity.Unity.Contexts;
using Onity.Unity.SceneFlow;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Composition
{
    /// <summary>
    /// Builds the Game scene's first attempt once the scene container is ready: it waits for the
    /// project scope to read the level database's manifest, loads the pack of the chosen level (DEC-196),
    /// builds the play area and the attempt on Home, frames the board camera, shows the HUD and plays. The
    /// level comes from Hub's launch request, else the Level Editor's Test Level request (DEC-127), else the
    /// installer's serialized index for direct Play (design 9.3). The launch request's starting boosters are
    /// handed over once as the scene's starting-booster source; with a selection, their combined debit
    /// commits on Home before Play applies them once (B-16, B-17). Starting play by itself stays interim
    /// until the pause flow exists (DEC-116). A scene loaded to prepare a level instead builds it hidden and waits
    /// for the level preloader to start it (DEC-199).
    /// </summary>
    public sealed class GameAttemptLoader : IStartingBoosterSource, IPreparedLevel
    {
        private readonly int m_levelIndex;
        private readonly BoardCamera m_boardCamera;
        private readonly LevelPrepareRequest m_prepare;
        private readonly GameSceneVisibility m_visibility;

        // Hub's launch request for this scene, or null (direct Play, Test Level). Consumed once in LoadAsync; its
        // starting boosters become m_startingBoosters, which TryTake hands to the opening once (B-17, AC-5.7).
        private LevelLaunchRequest m_launch;
        private StartingBoosterSelection m_startingBoosters;

        // A prepared build keeps its attempt here until the preloader starts it (DEC-199).
        private IResolver m_resolver;
        private LevelSession m_session;
        private IGameplayHud m_hud;
        private StartingBoosterPreparation m_preparation;
        private bool m_hasPreparation;
        private bool m_isPrepared;
        private bool m_isStarted;

        /// <param name="levelIndex">The authored level to play.</param>
        /// <param name="boardCamera">The camera framing the board; null logs one warning and play continues.</param>
        public GameAttemptLoader(int levelIndex, BoardCamera boardCamera)
            : this(levelIndex, boardCamera, null, null)
        {
        }

        /// <param name="levelIndex">The authored level to play.</param>
        /// <param name="boardCamera">The camera framing the board; null logs one warning and play continues.</param>
        /// <param name="prepare">The preloader's request when this scene prepares a level hidden, else null.</param>
        /// <param name="visibility">The scene's visibility, shown when a prepared level starts; null without one.</param>
        public GameAttemptLoader(int levelIndex, BoardCamera boardCamera, LevelPrepareRequest prepare,
            GameSceneVisibility visibility)
        {
            if (levelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex));
            }

            m_levelIndex = levelIndex;
            m_boardCamera = boardCamera;
            m_prepare = prepare;
            m_visibility = visibility;
        }

        public bool IsPrepared => m_isPrepared && !m_isStarted;

        public int LevelIndex => IsPrepared ? m_prepare.LevelIndex : -1;

        /// <summary>
        /// Maps a launch request's starting boosters onto a selection (B-17): null or an empty list selects nothing. A
        /// booster that is not a starting booster is a wiring error: it is logged and nothing is selected, so no
        /// charge is spent for a selection the popup never offered.
        /// </summary>
        public static StartingBoosterSelection ReadStartingBoosters(LevelLaunchRequest request)
        {
            if (request == null)
            {
                return StartingBoosterSelection.None;
            }

            try
            {
                return StartingBoosterSelection.From(request.StartingBoosters);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("Starting boosters ignored: " + exception.Message);
                return StartingBoosterSelection.None;
            }
        }

        /// <summary>
        /// Hands the launch request's starting selection over once; Restart, Next Level and a second build never
        /// reuse it (B-17).
        /// </summary>
        public bool TryTake(out StartingBoosterSelection selection)
        {
            selection = m_startingBoosters;
            m_startingBoosters = StartingBoosterSelection.None;
            return !selection.IsEmpty;
        }

        /// <summary>
        /// Owner: the Game scene context, which awaits this build callback, so it is never fire-and-forget.
        /// Stops: the scene build token, or the end of the build.
        /// </summary>
        public async Task LoadAsync(IResolver resolver, CancellationToken cancellationToken)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            // A prepared build turns its UI documents on with their trees hidden before anything binds them (DEC-199).
            m_visibility?.ShowDocumentsHidden();

            // Consumed once, before any wait, so a cancelled build leaves no request behind for a later Game scene. A
            // prepared build takes none: its starting boosters come with the start.
            if (m_prepare == null)
            {
                OnitySceneTransitionStore.TryConsumeActiveEnterData<LevelLaunchRequest>(out m_launch);
                m_startingBoosters = ReadStartingBoosters(m_launch);
            }

            // Direct Play starts this scene while the project scope is still reading the level database's manifest.
            Task projectReady = ProjectContext.Instance != null ? ProjectContext.Instance.ReadyTask : Task.CompletedTask;
            while (!projectReady.IsCompleted)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await Task.Yield();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!projectReady.IsCompletedSuccessfully || !resolver.TryResolve(out LevelCatalog levels))
            {
                Debug.LogWarning("Game attempt skipped: the project scope or its level database is unavailable.");
                return;
            }

            if (!TrySelectLevel(levels, out int levelIndex, out int levelNumber, out bool isTestPlay))
            {
                return;
            }

            // The level's pack loads on Home and stays resident, so Restart and Next Level inside it load nothing
            // (DEC-196). A pack that cannot load skips the attempt instead of faulting the scene.
            string packError;
            try
            {
                packError = await LevelCatalogLoader.LoadPackAsync(levels, levelIndex, cancellationToken);
                await OnityTask.SwitchToMainThread(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (packError != null)
            {
                Debug.LogWarning($"Game attempt skipped: {packError}.");
                return;
            }

            LevelData data = levels.GetAt(levelIndex);
            if (isTestPlay)
            {
                Debug.Log($"Test play: {data.Name} ({data.Id}) from the Level Editor.");
            }

            if (m_prepare != null)
            {
                Prepare(resolver, data, levelNumber);
                return;
            }

            // A container without a starting-booster source (a test's) or an empty selection plays at once.
            if (!resolver.TryResolve(out IStartingBoosterSource source) || !source.TryTake(out StartingBoosterSelection selection))
            {
                Load(resolver, data, levelNumber);
                return;
            }

            await LoadWithStartingBoostersAsync(resolver, data, levelNumber, selection, cancellationToken);
        }

        // Everything here runs on Home, where allocation is allowed.
        private void Load(IResolver resolver, LevelData data, int levelNumber)
        {
            if (TryBuild(resolver, data, levelNumber, out LevelSession session, out IGameplayHud _))
            {
                session.Play();
            }
        }

        // The opening (B-16): prepare the starting boosters on Home and commit their combined debit, wait for the
        // foreground if the app went to the background meanwhile, then play the committed plan once. A failed or
        // empty commit spends nothing and plays plain (B-29).
        private async Task LoadWithStartingBoostersAsync(IResolver resolver, LevelData data, int levelNumber,
            StartingBoosterSelection selection, CancellationToken cancellationToken)
        {
            if (!TryBuild(resolver, data, levelNumber, out LevelSession session, out IGameplayHud hud))
            {
                return;
            }

            StartingBoosterPreparation preparation;
            try
            {
                preparation = await CommitStartingBoostersAsync(resolver, session, selection, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The attempt ended on Home (the scene unloaded): a committed debit stays spent and nothing
                // applies (PROJECT.md, B-16).
                return;
            }

            PlayOpening(session, hud, preparation);
        }

        // Hot level: builds the attempt on Home as Load does, then waits hidden for the preloader's start (DEC-199).
        private void Prepare(IResolver resolver, LevelData data, int levelNumber)
        {
            if (!TryBuild(resolver, data, levelNumber, out LevelSession session, out IGameplayHud hud))
            {
                return;
            }

            m_resolver = resolver;
            m_session = session;
            m_hud = hud;
            m_isPrepared = true;
        }

        /// <summary>
        /// Commits the request's starting boosters while the prepared level is still hidden, as the opening does on
        /// Home (B-16); a request without any commits nothing. Owner: the level preloader, which awaits it. Stops: the
        /// commit's end, <paramref name="cancellationToken"/> or the attempt's end.
        /// </summary>
        public async Task PrepareStartAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!IsPrepared)
            {
                throw new InvalidOperationException("Game attempt: no prepared level to start.");
            }

            m_hasPreparation = false;
            StartingBoosterSelection selection = ReadStartingBoosters(request);
            if (selection.IsEmpty)
            {
                return;
            }

            m_preparation = await CommitStartingBoostersAsync(m_resolver, m_session, selection, cancellationToken);
            m_hasPreparation = true;
        }

        /// <summary>Shows the prepared level and plays it, with the opening its start committed; once.</summary>
        public void Play()
        {
            if (!IsPrepared)
            {
                return;
            }

            m_isStarted = true;
            m_visibility?.Show();

            // The camera moved back with the play area, so the board's screen-space labels are placed again.
            m_resolver.Resolve<BoardPresenter>().ReprojectEffects();
            if (m_hasPreparation)
            {
                PlayOpening(m_session, m_hud, m_preparation);
                return;
            }

            m_session.Play();
        }

        // The opening's Home commit (B-16): prepares the starting boosters, commits their combined debit and, if the app
        // went to the background meanwhile, waits for the foreground, so the plan it committed plays once and nothing is
        // spent or sampled again. Throws OperationCanceledException when the attempt ends on Home first.
        private static async Task<StartingBoosterPreparation> CommitStartingBoostersAsync(IResolver resolver,
            LevelSession session, StartingBoosterSelection selection, CancellationToken cancellationToken)
        {
            StartingBoosterPreparer preparer = resolver.Resolve<StartingBoosterPreparer>();
            ISeedSource seeds = resolver.TryResolve(out ISeedSource bound) ? bound : new TickCountSeedSource();

            // Owner of these waits: the attempt just prepared. Onity passes build callbacks no scene token, so the
            // attempt's token is their stop point: its Teardown, which the scene unload runs through
            // LevelSession.Dispose, ends the wait for the write lock, the main-thread return and the foreground wait
            // (PROJECT.md Rule 4, AC-15).
            using (CancellationTokenSource stop =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Attempt.Lifetime.Token))
            {
                CancellationToken token = stop.Token;
                StartingBoosterPreparation preparation =
                    await preparer.PrepareAsync(session, selection, seeds.NextSeed(), token);

                // Onity awaits build callbacks with ConfigureAwait(false), so return to the main thread before the
                // session or a view is touched again.
                await OnityTask.SwitchToMainThread(token);
                while (session.IsHomeBackgrounded.Value)
                {
                    token.ThrowIfCancellationRequested();
                    await OnityTask.NextFrame(token);
                }

                return preparation;
            }
        }

        private static void PlayOpening(LevelSession session, IGameplayHud hud, in StartingBoosterPreparation preparation)
        {
            if (preparation.Outcome == StartingBoosterOutcome.CommitFailed)
            {
                Debug.LogWarning($"Starting boosters not applied: their debit did not commit ({preparation.ConsumeResult}); "
                                 + "the level starts without them (B-29).");
            }

            hud?.SetOpening(preparation.Plan);
            session.Play(preparation.Plan);
        }

        // Builds the level's play area and attempt on Home, frames the camera and shows the HUD for it; false when
        // the level cannot be played.
        private bool TryBuild(IResolver resolver, LevelData data, int levelNumber, out LevelSession session,
            out IGameplayHud hud)
        {
            session = null;
            hud = null;
            if (!BoardLayout.TryCreate(data, out BoardLayout layout, out string error)
                || !AttemptLevelConverter.TryConvert(data, layout, out AttemptLevel level, out error))
            {
                Debug.LogWarning($"Game attempt skipped: {error}");
                return false;
            }

            EnvironmentView environment = resolver.Resolve<EnvironmentView>();
            environment.Build(layout, data);

            resolver.Resolve<BlockEffectContextSource>().Prepare(level, data);
            session = resolver.Resolve<LevelSession>();
            if (!session.TryPrepare(level, out error))
            {
                Debug.LogWarning($"Game attempt skipped: {error}");
                session = null;
                return false;
            }

            Frame(environment, layout);
            resolver.Resolve<BoardPresenter>().ReprojectEffects();

            // An interface, so a container without a HUD binding (a test's) resolves nothing and still plays (B-21).
            if (resolver.TryResolve(out IGameplayHud bound))
            {
                hud = bound;
                hud.Show(level);
            }

            // The attempt's "Level N" for the top bar, the Retry title and the win result; bound only with the HUD. It
            // is the launched number, which keeps counting past the last database level (DEC-173).
            if (resolver.TryResolve(out AttemptLevelNumber number))
            {
                number.Set(levelNumber);
            }

            // The top bar's Level badge, Retry, Pause and coin holder; bound only with the HUD (Game HUD design 8).
            if (resolver.TryResolve(out IGameTopBar topBar))
            {
                topBar.Show(level);
            }

            return true;
        }

        // Hub's launch request wins (design 9.3); the Level Editor's Test Level names its level by stable ID
        // (PRD 11.5, DEC-127); otherwise the installer's index plays (direct Play, DEC-116). LevelLaunchResolution
        // owns the precedence and the level number; a Test Level's number is its position in the database. Only the
        // manifest is read here; the level's pack loads afterwards (DEC-196).
        private bool TrySelectLevel(LevelCatalog levels, out int levelIndex, out int levelNumber, out bool isTestPlay)
        {
            bool hasTestPlay = LevelTestPlayRequest.TryRead(out LevelTestPlayRequest testPlay);
            // A prepare request names its level as a launch request does (DEC-199).
            int? requestedIndex = m_prepare != null ? m_prepare.LevelIndex : m_launch?.LevelIndex;
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(
                requestedIndex, hasTestPlay, m_levelIndex, levels.Count);
            levelIndex = resolution.LevelIndex;
            levelNumber = resolution.LevelNumber;
            isTestPlay = resolution.Source == LevelLaunchSource.TestPlay;

            switch (resolution.Source)
            {
                case LevelLaunchSource.Request:
                case LevelLaunchSource.Serialized:
                    return true;

                case LevelLaunchSource.TestPlay:
                    if (!testPlay.TryFindIndex(levels, out levelIndex, out string error))
                    {
                        Debug.LogWarning($"Test play skipped: {error}");
                        return false;
                    }

                    levelNumber = levelIndex + 1;
                    return true;

                default:
                    Debug.LogWarning(m_launch != null
                        ? $"Game attempt skipped: Hub requested level index {m_launch.LevelIndex}, but the database has no level."
                        : $"Game attempt skipped: level index {m_levelIndex} does not exist; the database has {levels.Count} level(s).");
                    return false;
            }
        }

        // Frames the board once per build, after the environment has centred BoardRoot (design 0.6).
        private void Frame(EnvironmentView environment, IBoardLayout layout)
        {
            if (m_boardCamera == null)
            {
                Debug.LogWarning("Board camera framing skipped: the Game scene installer has no BoardCamera.");
                return;
            }

            Bounds local = layout.LocalBounds;
            m_boardCamera.Frame(new Bounds(environment.BoardRoot.TransformPoint(local.center), local.size));
        }
    }
}
