using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>One wall or corner piece of the environment rule set, flattened to a square for the 2D canvas.</summary>
    internal readonly struct LevelWallGlyph
    {
        /// <summary>Quadrant centre in board cell units: cell (x, y)'s centre is (x, y) and +y is the top row.</summary>
        public readonly Vector2 Center;
        /// <summary>Side of the square quadrant, in cells.</summary>
        public readonly float Size;
        public readonly bool IsCorner;
        /// <summary>The composer's yaw in degrees: 0, 90, 180 or 270, turning clockwise seen from above.</summary>
        public readonly int Yaw;
        /// <summary>
        /// For a corner, the (+-1, +-1) direction of its rounded quadrant corner on the y-up board; (0, 0) for a
        /// straight wall.
        /// </summary>
        public readonly Vector2Int RoundedCorner;

        public LevelWallGlyph(Vector2 center, float size, bool isCorner, int yaw, Vector2Int roundedCorner)
        {
            Center = center;
            Size = size;
            IsCorner = isCorner;
            Yaw = yaw;
            RoundedCorner = roundedCorner;
        }
    }

    /// <summary>
    /// Gives the Level Editor canvas the wall and corner pieces that the play area's own EnvironmentComposer and
    /// rule set build for a draft level, so the canvas never runs a second wall algorithm (PROJECT.md DEC-100).
    /// </summary>
    internal sealed class LevelCanvasWalls
    {
        // Same asset as LevelPreviewOwner; the Editor assembly must not reference the LevelPreview assembly. Shares
        // its path with EnvironmentWallBrickAssets through BoardKitAssets instead of declaring it a second time.
        internal const string k_databasePath = BoardKitAssets.EnvironmentDatabasePath;
        // The composer's quadrant grid puts cell (x, y)'s centre at grid vertex (2x, 2y): a quadrant is half a cell.
        private const float k_quadrantSize = 0.5f;

        private readonly List<EnvironmentPiecePlacement> m_placements = new List<EnvironmentPiecePlacement>();
        private readonly List<BoardPieceView> m_wallPieces = new List<BoardPieceView>();
        private readonly List<LevelDoorData> m_doors = new List<LevelDoorData>();
        private readonly List<Vector2Int> m_voidCells = new List<Vector2Int>();
        private EnvironmentDatabase m_database;
        private EnvironmentComposer m_composer;

        public static EnvironmentDatabase LoadDatabase()
        {
            return AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(k_databasePath);
        }

        /// <summary>
        /// The rounded quadrant corner of Corner_1 at this yaw: (+1, +1) at yaw 0, turned clockwise like the rule
        /// matcher, which maps (x, y) to (y, -x) per 90 degrees (docs/Environment-Layout.md).
        /// </summary>
        public static Vector2Int GetRoundedCorner(int yaw)
        {
            switch (yaw)
            {
                case 0: return new Vector2Int(1, 1);
                case 90: return new Vector2Int(1, -1);
                case 180: return new Vector2Int(-1, -1);
                case 270: return new Vector2Int(-1, 1);
                default: throw new ArgumentOutOfRangeException(nameof(yaw), yaw, "Rule-set yaws are 0, 90, 180 or 270.");
            }
        }

        /// <summary>
        /// Clears output and fills it with one glyph per wall and corner placement the composer makes for this
        /// level's board size and its valid doors. Returns false with a designer message, and no glyphs, when there
        /// is no level or the environment database is missing or invalid.
        /// </summary>
        public bool TryCompose(LevelData level, EnvironmentDatabase database, List<LevelWallGlyph> output,
            out string issue)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            output.Clear();
            if (level == null)
            {
                issue = "Select a level first.";
                return false;
            }

            if (database == null)
            {
                issue = "The environment database asset is missing.";
                return false;
            }

            if (!database.Validate(out issue) || !TryCreateLayout(level, out BoardLayout layout, out issue))
            {
                return false;
            }

            try
            {
                if (m_composer == null || m_database != database)
                {
                    m_composer = new EnvironmentComposer(database);
                    m_database = database;
                }

                m_composer.Compose(layout, m_placements);
            }
            catch (ArgumentException exception)
            {
                issue = exception.Message;
                return false;
            }
            catch (InvalidOperationException exception)
            {
                issue = exception.Message;
                return false;
            }

            // Read on every compose: a designer can edit the rule set while the window stays open.
            SetWallPieces(database.RuleSet);
            for (int index = 0; index < m_placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = m_placements[index];
                if (m_wallPieces.Contains(placement.Piece))
                {
                    output.Add(CreateGlyph(placement));
                }
            }

            issue = null;
            return true;
        }

        // Walls depend only on the board size, the void cells and the doors, never on blocks, so this uses
        // BoardLayout's size, void and door overload, as BoardLayout.TryCreate(LevelData) does. An invalid door is only
        // dropped from the layout: the issues list names it, and the rest of the ring stays.
        private bool TryCreateLayout(LevelData level, out BoardLayout layout, out string issue)
        {
            m_voidCells.Clear();
            for (int index = 0; index < level.VoidCellCount; index++)
            {
                m_voidCells.Add(level.GetVoidCellAt(index));
            }

            m_doors.Clear();
            for (int index = 0; index < level.DoorCount; index++)
            {
                m_doors.Add(level.GetDoorAt(index));
            }

            if (BoardLayout.TryCreate(level.Width, level.Height, m_voidCells, m_doors, out layout,
                    out issue))
            {
                return true;
            }

            // Keep each door, in level order, that still gives a valid layout with the doors kept before it; of two
            // overlapping doors the first keeps its opening, the one LevelData.Validate does not name.
            m_doors.Clear();
            for (int index = 0; index < level.DoorCount; index++)
            {
                m_doors.Add(level.GetDoorAt(index));
                if (!BoardLayout.TryCreate(level.Width, level.Height, m_voidCells, m_doors, out _,
                        out _))
                {
                    m_doors.RemoveAt(m_doors.Count - 1);
                }
            }

            // Only a board below 1 x 1 or an invalid void cell list (off the board or repeated) fails here.
            return BoardLayout.TryCreate(level.Width, level.Height, m_voidCells, m_doors, out layout,
                out issue);
        }

        private void SetWallPieces(EnvironmentRuleSet ruleSet)
        {
            m_wallPieces.Clear();
            for (int index = 0; index < ruleSet.RuleCount; index++)
            {
                BoardPieceView piece = ruleSet.GetRule(index).Output;
                if (!m_wallPieces.Contains(piece))
                {
                    m_wallPieces.Add(piece);
                }
            }
        }

        private static LevelWallGlyph CreateGlyph(EnvironmentPiecePlacement placement)
        {
            Vector2 center = new Vector2(placement.GridPosition.x * k_quadrantSize,
                placement.GridPosition.y * k_quadrantSize);
            bool isCorner = IsCornerPiece(placement.Piece);
            return new LevelWallGlyph(center, k_quadrantSize, isCorner, placement.Yaw,
                isCorner ? GetRoundedCorner(placement.Yaw) : Vector2Int.zero);
        }

        // The rule set has two outputs, Wall and Corner_1, and Corner_1 serves both the inner and the outer corner
        // rule (docs/Environment-Layout.md). The prefab name tells a corner from a straight wall without depending
        // on rule order; the yaw alone then gives the rounded side for both corner kinds.
        private static bool IsCornerPiece(BoardPieceView piece)
        {
            return piece.name.IndexOf("Corner", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
