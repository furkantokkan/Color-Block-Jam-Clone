using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Picking;

namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// The domain side of one attempt: occupancy, the command executor and its log, the drag resolver,
    /// the picker, the tick's event log and the attempt's hooks. It is built on Home, where allocation
    /// is allowed, and a new one is built for every attempt, so nothing crosses attempts (V-E11).
    /// </summary>
    public sealed class BoardSimulation
    {
        // One tick holds at most one booster command, an interrupted move, a pointer move and a command
        // source's move. A booster raises at most one event per live block (Vacuum) or per remaining
        // part (Rocket), both bounded by the level's cell count; the headroom covers the moves (B-7).
        private const int k_eventHeadroom = 4;

        // Far more moves than a level's timer allows; a longer attempt keeps playing with a truncated log.
        private const int k_commandLogCapacity = 1024;

        public BoardSimulation(LevelModel model, AttemptHooks hooks, DragSettings settings)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            Hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            Board = new BoardState(model, hooks);
            Drag = new DragResolver(Board, hooks, settings);
            Picker = new BlockPicker(model.Width, model.Height);
            Events = new BoardEventLog(model.TotalCellCount + k_eventHeadroom);

            // Command handlers are the only code that changes the board during play (DEC-122).
            ICommandHandler[] handlers =
            {
                new MoveBlockHandler(Board, hooks, Events),
                new RemoveCellHandler(Board, hooks, Events),
                new RemoveBlockHandler(Board, hooks, Events),
                new RemoveColorHandler(Board, hooks, Events)
            };
            Commands = new CommandExecutor(handlers, new CommandLog(k_commandLogCapacity));
        }

        public BoardState Board { get; }

        /// <summary>Runs every command of the attempt and logs the applied ones in order.</summary>
        public CommandExecutor Commands { get; }

        public DragResolver Drag { get; }

        /// <summary>The board as the player sees it: the committed board plus the held or interrupted move.</summary>
        public IBoardReader Preview => Drag.Preview;

        public BlockPicker Picker { get; }

        public BoardEventLog Events { get; }

        public AttemptHooks Hooks { get; }

        /// <summary>True when no required block remains; checked once after the command phase (RULE-29).</summary>
        public bool IsCleared => Board.RequiredRemaining == 0;
    }
}
