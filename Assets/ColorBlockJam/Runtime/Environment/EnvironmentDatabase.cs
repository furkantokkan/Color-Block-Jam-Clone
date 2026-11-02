using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// The only source of the wall rule set, piece prefabs, atlas colours and piece scales
    /// the play-area environment uses. The Game scene installer binds it.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentDatabase", menuName = "Color Block Jam/Environment/Database")]
    public sealed class EnvironmentDatabase : ScriptableObject
    {
        [SerializeField] private EnvironmentRuleSet m_ruleSet;
        [SerializeField] private BoardPieceView m_floorPiece;
        [SerializeField] private BoardPieceView m_floorBasePiece;
        // A flat top for occupied quadrants with no exposed edge; shares the wall pools.
        [SerializeField] private BoardPieceView m_blockFillPiece;
        // The Blender-authored cap in Wall's mesh space that closes both ends of a straight wall run (DEC-139).
        [SerializeField] private BoardPieceView m_brickEndPiece;
        // The Blender-authored corner unit in Corner_1's mesh space, both arm ends closed and rounded (DEC-139).
        [SerializeField] private BoardPieceView m_cornerEndPiece;
        // Corner_End with its joined arm left open, so a corner merges into one neighbouring wall run (DEC-139).
        [SerializeField] private BoardPieceView m_cornerJoinPiece;
        [SerializeField] private BoardPieceView m_doorPiece;
        // The Blender-authored gate end cap in Door_1x1's mesh space that closes both ends of a gate (DEC-139).
        [SerializeField] private BoardPieceView m_doorEndPiece;
        [SerializeField] private BoardPieceView m_doorArrowPiece;
        // Names the atlas cells of the colours below (DEC-202).
        [SerializeField] private GamePalette m_palette;

        // The wall kit prefabs are calibrated to a full cell; the play area uses
        // them as half-cell quadrant pieces so rings stay thin (references 02, 17).
        [SerializeField] private Vector3 m_wallPieceScale = new Vector3(0.5f, 1f, 0.5f);
        [SerializeField] private Vector3 m_doorPieceScale = new Vector3(1f, 1f, 0.5f);
        [SerializeField] private Vector3 m_doorArrowScale = new Vector3(0.5f, 1f, 0.5f);

        // Every placement is tinted by the composer; pieces carry no colour of their own (DEC-123).
        [SerializeField] private PaletteColor m_floorColor = PaletteColor.Ground;
        [SerializeField] private PaletteColor m_wallColor = PaletteColor.Wall;

        // The grey square under each floor tile shows through the gaps at their
        // rounded corners, like the darker seams between tiles in the references.
        [SerializeField] private PaletteColor m_floorBaseColor = PaletteColor.Wall;

        // Barrier board objects (catalog OBJ-05) are raised wall pieces; this tint sets them apart
        // from the fixed walls when a designer wants that. It defaults to the wall colour.
        [SerializeField] private PaletteColor m_barrierColor = PaletteColor.Wall;

        public EnvironmentRuleSet RuleSet => m_ruleSet;
        public BoardPieceView FloorPiece => m_floorPiece;
        public BoardPieceView FloorBasePiece => m_floorBasePiece;
        public BoardPieceView BlockFillPiece => m_blockFillPiece;
        public BoardPieceView BrickEndPiece => m_brickEndPiece;
        public BoardPieceView CornerEndPiece => m_cornerEndPiece;
        public BoardPieceView CornerJoinPiece => m_cornerJoinPiece;
        public BoardPieceView DoorPiece => m_doorPiece;
        public BoardPieceView DoorEndPiece => m_doorEndPiece;
        public BoardPieceView DoorArrowPiece => m_doorArrowPiece;
        public Vector3 WallPieceScale => m_wallPieceScale;
        public Vector3 DoorPieceScale => m_doorPieceScale;
        public Vector3 DoorArrowScale => m_doorArrowScale;

        // Every colour is a cell of the shared atlas that the palette names (DEC-202).
        public AtlasColor GetDoorColor(PaletteColor color)
        {
            return m_palette.GetAtlasColor(color);
        }

        /// <summary>Door arrows read as a light tint of their door colour (references 02, 17); the atlas bakes the blend.</summary>
        public AtlasColor GetDoorArrowColor(PaletteColor color)
        {
            return m_palette.GetLightAtlasColor(color);
        }

        public AtlasColor GetFloorColor()
        {
            return m_palette.GetAtlasColor(m_floorColor);
        }

        public AtlasColor GetWallColor()
        {
            return m_palette.GetAtlasColor(m_wallColor);
        }

        public AtlasColor GetFloorBaseColor()
        {
            return m_palette.GetAtlasColor(m_floorBaseColor);
        }

        public AtlasColor GetBarrierColor()
        {
            return m_palette.GetAtlasColor(m_barrierColor);
        }

        /// <summary>Adds every distinct prefab once, including the solid top, brick end, corner end, corner join and door end, for the environment pools.</summary>
        public void GetPieces(List<BoardPieceView> output)
        {
            output.Clear();
            AddDistinct(output, m_floorPiece);
            AddDistinct(output, m_floorBasePiece);
            AddDistinct(output, m_blockFillPiece);
            AddDistinct(output, m_brickEndPiece);
            AddDistinct(output, m_cornerEndPiece);
            AddDistinct(output, m_cornerJoinPiece);
            AddDistinct(output, m_doorPiece);
            AddDistinct(output, m_doorEndPiece);
            AddDistinct(output, m_doorArrowPiece);
            for (int index = 0; index < m_ruleSet.RuleCount; index++)
            {
                AddDistinct(output, m_ruleSet.GetRule(index).Output);
            }
        }

        public bool Validate(out string error)
        {
            if (m_ruleSet == null || m_floorPiece == null || m_floorBasePiece == null || m_blockFillPiece == null
                || m_brickEndPiece == null || m_cornerEndPiece == null || m_cornerJoinPiece == null || m_doorPiece == null || m_doorEndPiece == null || m_doorArrowPiece == null || m_palette == null)
            {
                error = "The environment database needs a rule set, floor, floor base, block fill, brick end, corner end, corner join, door, door end and arrow pieces and a palette.";
                return false;
            }

            if (!m_palette.Validate(out error) || !m_ruleSet.Validate(out error))
            {
                return false;
            }

            if (!IsPositive(m_wallPieceScale) || !IsPositive(m_doorPieceScale) || !IsPositive(m_doorArrowScale))
            {
                error = "Environment piece scales must be positive.";
                return false;
            }

            error = null;
            return true;
        }

        private static void AddDistinct(List<BoardPieceView> output, BoardPieceView piece)
        {
            if (!output.Contains(piece))
            {
                output.Add(piece);
            }
        }

        private static bool IsPositive(Vector3 scale)
        {
            return scale.x > 0f && scale.y > 0f && scale.z > 0f;
        }
    }
}
