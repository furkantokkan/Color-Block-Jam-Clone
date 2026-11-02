using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// A level session wired to fakes that share one journal, built from a BoardFixture map. Ticks
    /// take board points, which the fake projector maps to screen pixels and back exactly. A test may
    /// pass its own clock (such as a real AttemptCountdown) and ask for booster targeting; the fake
    /// countdown then stays unused.
    /// </summary>
    public sealed class SessionFixture : IDisposable
    {
        public const float k_frameSeconds = 1f / 60f;

        // Presized so journaling inside a measured tick never grows the list.
        private const int k_journalCapacity = 256;

        public SessionFixture(string[] rows, DoorSpec[] doors, bool resumesOnForeground = true,
            IAttemptHookSource hookSource = null, ICountdown clock = null, bool withTargeting = false)
        {
            Journal = new List<string>(k_journalCapacity);
            Pointer = new FakePointerSource(Journal);
            Ui = new FakeUiPointerFilter();
            Projector = new FakeBoardProjector();
            Presenter = new FakeBoardPresenter(Journal);
            Countdown = new FakeCountdown(Journal);
            Deadline = new FakeFinishDeadline();
            Clock = clock ?? Countdown;
            Targeting = withTargeting ? new BoosterTargeting() : null;
            Level = CreateLevel(BoardFixture.Create(rows, doors));
            DragInputRouter router = Targeting != null
                ? new DragInputRouter(Pointer, Ui, Projector, Targeting)
                : new DragInputRouter(Pointer, Ui, Projector);
            Session = new LevelSession(router, Pointer, Presenter, Clock, new NoPauseOverlay(resumesOnForeground),
                hookSource ?? new EmptyAttemptHookSource(), Deadline);
        }

        /// <summary>The countdown the session drives: the journaling fake unless the test passed its own clock.</summary>
        public ICountdown Clock { get; }

        /// <summary>The router's targeting mode, or null when the fixture was built without targeting.</summary>
        public BoosterTargeting Targeting { get; }

        public List<string> Journal { get; }

        public FakePointerSource Pointer { get; }

        public FakeUiPointerFilter Ui { get; }

        public FakeBoardProjector Projector { get; }

        public FakeBoardPresenter Presenter { get; }

        public FakeCountdown Countdown { get; }

        public FakeFinishDeadline Deadline { get; }

        public AttemptLevel Level { get; }

        public LevelSession Session { get; }

        /// <summary>Prepares the level on Home and plays it.</summary>
        public void PrepareAndPlay()
        {
            if (!Session.TryPrepare(Level, out string error) || !Session.Play())
            {
                throw new InvalidOperationException(error ?? "Play was refused.");
            }
        }

        public void Tick(PointerPhase phase, float boardX, float boardY)
        {
            Pointer.Set(phase, FakeBoardProjector.ToScreen(boardX, boardY));
            Session.RunTick(k_frameSeconds);
        }

        public void TickIdle()
        {
            TickIdle(k_frameSeconds);
        }

        /// <summary>One tick with no pointer activity that advances Playing time by seconds.</summary>
        public void TickIdle(float seconds)
        {
            Pointer.Set(PointerPhase.None, Vector2.zero);
            Session.RunTick(seconds);
        }

        /// <summary>
        /// Presses and releases the board point in two ticks of zero Playing time: a press on a block starts the countdown
        /// (DEC-189) without spending any of it, so a test's clock values stay exact.
        /// </summary>
        public void StartTimer(float boardX, float boardY)
        {
            Pointer.Set(PointerPhase.Down, FakeBoardProjector.ToScreen(boardX, boardY));
            Session.RunTick(0f);
            Pointer.Set(PointerPhase.Up, FakeBoardProjector.ToScreen(boardX, boardY));
            Session.RunTick(0f);
        }

        /// <summary>A press and a release on the board point in two ticks: a tap.</summary>
        public void Tap(float boardX, float boardY)
        {
            Tick(PointerPhase.Down, boardX, boardY);
            Tick(PointerPhase.Up, boardX, boardY);
        }

        /// <summary>Where the player sees the block: its committed origin, or its held or interrupted preview origin.</summary>
        public Cell GetOrigin(int block) => Session.Attempt.Simulation.Preview.GetOrigin(block);

        public void Dispose()
        {
            Session.Dispose();
            Targeting?.Dispose();
        }

        // The fake presenter ignores visuals, so every block gets a placeholder spec of its own cells.
        private static AttemptLevel CreateLevel(LevelModel model)
        {
            if (!BoardLayout.TryCreate(model.Width, model.Height, Array.Empty<Vector2Int>(), Array.Empty<LevelDoorData>(),
                    out BoardLayout layout, out string error))
            {
                throw new InvalidOperationException(error);
            }

            BlockVisualSpec[] visuals = new BlockVisualSpec[model.BlockCount];
            string[] ids = new string[model.BlockCount];
            for (int block = 0; block < model.BlockCount; block++)
            {
                BlockDefinition definition = model.GetBlock(block);
                Vector2Int[] cells = new Vector2Int[definition.CellCount];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = new Vector2Int(definition.GetCell(i).X, definition.GetCell(i).Y);
                }

                visuals[block] = new BlockVisualSpec(PaletteColor.Red, cells,
                    new Vector2Int(definition.Origin.X, definition.Origin.Y));
                ids[block] = "b" + block;
            }

            return new AttemptLevel(model, layout, "fixture", visuals, ids);
        }
    }
}
