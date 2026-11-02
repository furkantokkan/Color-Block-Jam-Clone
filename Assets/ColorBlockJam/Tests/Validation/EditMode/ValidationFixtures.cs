using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;
using static ColorBlockJam.Tests.Validation.ValidationLevelFixture;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>The named test levels of the solvability tests, each built fresh so no test shares state.</summary>
    public static class ValidationFixtures
    {
        /// <summary>
        /// The shipped level-05 with the sixth block of the designer's 2026-09-28 draft (DEC-175): a Red 1x2 on the top
        /// left. It is solvable in six player moves, but the step-shortest search stores more than 250,000 positions
        /// before it finds a solution. Built in the draft's block and gate order, which sets the search order.
        /// </summary>
        public static LevelData LevelFiveWithSixthBlock()
        {
            Vector2Int[] triple = { Vector2Int.zero, new Vector2Int(1, 0), new Vector2Int(2, 0) };
            Vector2Int[] square = { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) };
            return new LevelData("level-05", "Level 5", 5, 6, 300,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 0, 2, "Pink"),
                    new LevelDoorData("d2", BoardSide.Top, 3, 2, "LightGreen"),
                    new LevelDoorData("d3", BoardSide.Left, 0, 2, "Orange"),
                    new LevelDoorData("d4", BoardSide.Right, 0, 2, "Red"),
                    new LevelDoorData("d5", BoardSide.Bottom, 1, 3, "LightBlue")
                },
                new[]
                {
                    new LevelBlockData("b1", "LightBlue", new Vector2Int(2, 3),
                        new[] { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(0, 2) }),
                    new LevelBlockData("b2", "LightGreen", new Vector2Int(0, 2), square),
                    new LevelBlockData("b3", "Pink", new Vector2Int(3, 2), square),
                    new LevelBlockData("b4", "Orange", new Vector2Int(1, 1), triple),
                    new LevelBlockData("b5", "Red", new Vector2Int(1, 0), triple),
                    new LevelBlockData("b6", "Red", new Vector2Int(0, 4),
                        new[] { Vector2Int.zero, new Vector2Int(0, 1) })
                });
        }

        /// <summary>
        /// A Yellow single on its Bottom gate leaves at once, while a Red and an Orange Ice block (clearsToThaw 2, which
        /// PRE-2 allows) each wait for two exits: after the Yellow one only one has happened, so the search is exhausted
        /// with only the Red and Orange blocks never leaving (DEC-176).
        /// </summary>
        public static LevelData TwoIceBlocksBesideAFreeBlock()
        {
            return Create(new[]
            {
                "...",
                "A.B",
                ".C."
            }, new[] { Door(BoardSide.Top, 0, 1, 'A'), Door(BoardSide.Top, 2, 1, 'B'), Door(BoardSide.Bottom, 1, 1, 'C') },
                new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Ice(2) } }, { 'B', new[] { Ice(2) } } });
        }

        /// <summary>
        /// Two Red singles and a Yellow single fill a one-row pocket with the Red gate at the right end and the Yellow
        /// gate at the left: alone each slides to its gate (PRE-1 passes), but together none can move (PRE-3, DEC-176).
        /// Block order: the Red at (0,1), the Yellow at (2,1), the Red at (1,1).
        /// </summary>
        public static LevelData ThreeBlocksWedgedInARow()
        {
            return Create(new[]
            {
                "###",
                "AaC",
                "###"
            }, new[] { Door(BoardSide.Right, 1, 1, 'A'), Door(BoardSide.Left, 1, 1, 'C') });
        }

        /// <summary>
        /// A Red Vertical Arrow single in a one-row corridor whose Red gate is at the far right: its axis rules out
        /// both a slide toward the gate and an exit through it, so the axis-aware PRE-1 refuses it before any search.
        /// </summary>
        public static LevelData ArrowWedgedInARow()
        {
            return Create(new[]
            {
                "###",
                "A..",
                "###"
            }, new[] { Door(BoardSide.Right, 1, 1, 'A') },
                new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Arrow("Vertical") } } });
        }

        /// <summary>Five singles of five colours in one row and no gate; only for naming blocks.</summary>
        public static LevelData FiveSinglesInARow()
        {
            return Create(new[] { "ABCDE" }, new LevelDoorData[0]);
        }

        /// <summary>One Red single block that slides left and up to a one-cell Red gate on the top edge.</summary>
        public static LevelData SingleBlockToTopGate()
        {
            return Create(new[]
            {
                "....",
                ".A..",
                "....",
                "...."
            }, new[] { Door(BoardSide.Top, 0, 1, 'A') });
        }

        /// <summary>Red and Orange blocks in opposite corners each travel the board's height to a gate on the far edge.</summary>
        public static LevelData TwoBlocksCrossing()
        {
            return Create(new[]
            {
                "A...",
                "....",
                "....",
                "...B"
            }, new[] { Door(BoardSide.Bottom, 0, 1, 'A'), Door(BoardSide.Top, 3, 1, 'B') });
        }

        /// <summary>The Orange block must leave first to thaw the Red Ice block (clearsToThaw 1).</summary>
        public static LevelData IceThawedByOtherExit()
        {
            return Create(new[]
            {
                "...",
                "A.B",
                "..."
            }, new[] { Door(BoardSide.Top, 0, 1, 'A'), Door(BoardSide.Top, 2, 1, 'B') },
                new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Ice(1) } } });
        }

        /// <summary>A Red block with only an Orange gate on the board (PRE-1, no gate of its colour).</summary>
        public static LevelData BlockWithoutGateOfItsColour()
        {
            return Create(new[]
            {
                "...",
                ".A.",
                "B.."
            }, new[] { Door(BoardSide.Bottom, 0, 1, 'B') });
        }

        /// <summary>A 2 x 2 Red block with only an Orange gate on the board (PRE-1, no gate; it needs 2 cells).</summary>
        public static LevelData SquareWithoutGateOfItsColour()
        {
            return Create(new[]
            {
                "....",
                ".AA.",
                ".AA.",
                "B..."
            }, new[] { Door(BoardSide.Bottom, 0, 1, 'B') });
        }

        /// <summary>
        /// A Red L whose notch faces its 2-wide top gate, with an Orange single in the notch: Red's exit is
        /// PathBlocked until Orange slides right and leaves through its right gate (the swept-cell mask).
        /// </summary>
        public static LevelData NotchBlockedExit()
        {
            return Create(new[]
            {
                "A..",
                "AB.",
                "AA."
            }, new[] { Door(BoardSide.Top, 0, 2, 'A'), Door(BoardSide.Right, 1, 1, 'B') });
        }

        /// <summary>A two-cell Red block whose only Red gate is one cell wide (PRE-1, gate too narrow).</summary>
        public static LevelData BlockWiderThanItsGate()
        {
            return Create(new[]
            {
                "....",
                ".AA.",
                "...."
            }, new[] { Door(BoardSide.Top, 1, 1, 'A') });
        }

        /// <summary>An Ice block that needs two other exits while the level has only one other block (PRE-2).</summary>
        public static LevelData IceCountAboveOtherBlocks()
        {
            return Create(new[]
            {
                "...",
                "A.B",
                "..."
            }, new[] { Door(BoardSide.Top, 0, 1, 'A'), Door(BoardSide.Top, 2, 1, 'B') },
                new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Ice(2) } } });
        }

        /// <summary>A one-cell corridor: Red must leave at the top past Orange, Orange at the bottom past Red.</summary>
        public static LevelData MutualBlockInCorridor()
        {
            return Create(new[]
            {
                "#B#",
                "#.#",
                "#A#"
            }, new[] { Door(BoardSide.Top, 1, 1, 'A'), Door(BoardSide.Bottom, 1, 1, 'B') });
        }

        /// <summary>Two Ice blocks that each wait for the other's exit; both pass PRE-2.</summary>
        public static LevelData TwoIceBlocksWaitingOnEachOther()
        {
            return Create(new[]
            {
                "...",
                "A.B",
                "..."
            }, new[] { Door(BoardSide.Top, 0, 1, 'A'), Door(BoardSide.Top, 2, 1, 'B') },
                new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Ice(1) } }, { 'B', new[] { Ice(1) } } });
        }

        /// <summary>
        /// SingleBlockToTopGate with an Arrow on its Red block: reaching the gate needs a slide along X and an exit
        /// along Y, so neither axis clears it.
        /// </summary>
        public static LevelData ArrowSingleNeedingBothAxes(string axis)
        {
            return Create(new[]
            {
                "....",
                ".A..",
                "....",
                "...."
            }, new[] { Door(BoardSide.Top, 0, 1, 'A') },
                new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Arrow(axis) } } });
        }

        /// <summary>
        /// A Red I3 in the middle of the board with one Red gate on the given side, covering the block's lanes on
        /// that side. vertical lays the I3 along Y on a 3 x 5 board, else along X on a 5 x 3 board; arrowAxis
        /// ("Horizontal", "Vertical") adds an Arrow effect, null none.
        /// </summary>
        public static LevelData TripleWithOneGate(bool vertical, BoardSide gateSide, string arrowAxis)
        {
            string[] rows = vertical
                ? new[] { "...", ".A.", ".A.", ".A.", "..." }
                : new[] { ".....", ".AAA.", "....." };

            // The block covers lane 1 on the sides across its length and lanes 1-3 on the sides along it.
            bool gateAlongBlock = vertical ? gateSide == BoardSide.Left || gateSide == BoardSide.Right
                : gateSide == BoardSide.Top || gateSide == BoardSide.Bottom;
            LevelDoorData gate = Door(gateSide, 1, gateAlongBlock ? 3 : 1, 'A');
            return Create(rows, new[] { gate }, arrowAxis == null
                ? null
                : new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Arrow(arrowAxis) } } });
        }

        /// <summary>
        /// A one-cell corridor: the Red block leaves at the top only after the Orange Arrow block above it clears the
        /// column through its right gate. A Horizontal Orange can; a Vertical one would need a slide across its axis.
        /// </summary>
        public static LevelData ArrowBlockingColumn(string orangeAxis)
        {
            return Create(new[]
            {
                "#.#",
                ".B.",
                "#A#"
            }, new[] { Door(BoardSide.Top, 1, 1, 'A'), Door(BoardSide.Right, 1, 1, 'B') },
                new Dictionary<char, LevelEffectData[]> { { 'B', new[] { Arrow(orangeAxis) } } });
        }

        /// <summary>
        /// Red, Orange and Yellow singles on the top row, each flush under a one-cell top gate of its colour: every
        /// block can leave at once, so all six exit orders solve it in three exits.
        /// </summary>
        public static LevelData ThreeSinglesAtTheirGates()
        {
            return Create(new[]
            {
                "ABC",
                "...",
                "..."
            }, new[] { Door(BoardSide.Top, 0, 1, 'A'), Door(BoardSide.Top, 1, 1, 'B'), Door(BoardSide.Top, 2, 1, 'C') });
        }

        /// <summary>Five singles on the top row, each flush under a one-cell top gate of its colour: 120 exit orders.</summary>
        public static LevelData FiveSinglesAtTheirGates()
        {
            return Create(new[]
            {
                "ABCDE",
                ".....",
                "....."
            }, new[]
            {
                Door(BoardSide.Top, 0, 1, 'A'), Door(BoardSide.Top, 1, 1, 'B'), Door(BoardSide.Top, 2, 1, 'C'),
                Door(BoardSide.Top, 3, 1, 'D'), Door(BoardSide.Top, 4, 1, 'E')
            });
        }

        /// <summary>
        /// Red leaves right from (2,1), where Orange stands; Orange leaves at the top from (2,2). Orange first takes
        /// four steps (up, out, Red right, out); Red first takes six, because Orange must step down and come back.
        /// </summary>
        public static LevelData TwoOrdersOfDifferentLength()
        {
            return Create(new[]
            {
                "...",
                ".AB",
                "..."
            }, new[] { Door(BoardSide.Right, 1, 1, 'A'), Door(BoardSide.Top, 2, 1, 'B') });
        }

        /// <summary>
        /// A Red Ice block (clearsToThaw 2) under its top gate between an Orange and a Yellow single that leave at once;
        /// it thaws after both exits and then steps up and out.
        /// </summary>
        public static LevelData IceThawedByTwoExits()
        {
            return Create(new[]
            {
                "B.C",
                ".A.",
                "..."
            }, new[] { Door(BoardSide.Top, 0, 1, 'B'), Door(BoardSide.Top, 1, 1, 'A'), Door(BoardSide.Top, 2, 1, 'C') },
                new Dictionary<char, LevelEffectData[]> { { 'A', new[] { Ice(2) } } });
        }

        /// <summary>Seventeen single blocks on an 8 x 10 board, one more than the state key holds.</summary>
        public static LevelData SeventeenBlocks()
        {
            return Create(new[]
            {
                "ABCDEFGH",
                "........",
                "IJKabcde",
                "........",
                "f.......",
                "........",
                "........",
                "........",
                "........",
                "........"
            }, new[] { Door(BoardSide.Top, 0, 1, 'A') });
        }

        /// <summary>Every fixture the check can search, for the determinism, eager-versus-plain and parity tests.</summary>
        public static LevelData[] CreateSearchable()
        {
            return new[]
            {
                SingleBlockToTopGate(),
                TwoBlocksCrossing(),
                IceThawedByOtherExit(),
                MutualBlockInCorridor(),
                TwoIceBlocksWaitingOnEachOther(),
                NotchBlockedExit(),
                TripleWithOneGate(false, BoardSide.Left, "Horizontal"),
                TripleWithOneGate(true, BoardSide.Top, "Vertical"),
                TripleWithOneGate(false, BoardSide.Top, "Horizontal"),
                ArrowBlockingColumn("Horizontal"),
                ArrowBlockingColumn("Vertical")
            };
        }
    }
}
