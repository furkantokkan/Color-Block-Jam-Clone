using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Where one block of a template sat and which cells it had when a Pen template edit began, so Discard puts it back
    /// exactly (defect D-001 of the pen-edit task).
    /// </summary>
    [Serializable]
    internal sealed class LevelTemplateBlockOrigin
    {
        [SerializeField] private string m_blockId;
        [SerializeField] private Vector2Int m_origin;
        [SerializeField] private Vector2Int[] m_cells = Array.Empty<Vector2Int>();

        public LevelTemplateBlockOrigin(string blockId, Vector2Int origin, IReadOnlyList<Vector2Int> cells)
        {
            m_blockId = blockId;
            m_origin = origin;
            m_cells = LevelCells.Copy(cells);
        }

        // The Unity serializer and JsonUtility create the record through this constructor.
        private LevelTemplateBlockOrigin()
        {
        }

        public string BlockId => m_blockId;
        public Vector2Int Origin => m_origin;
        /// <summary>The block's own cells from its origin, exactly as the level stored them.</summary>
        public IReadOnlyList<Vector2Int> Cells => m_cells ?? Array.Empty<Vector2Int>();
    }

    /// <summary>
    /// One pending template footprint change: the key the piece library held for the template when the edit began
    /// (DEC-193), its display name, its footprint when the edit began and the pending footprint, both normalized and
    /// in library orientation, and the blocks of the open level that belong to the template while it is pending.
    /// Serialized so it rides the undoable LevelDraft and the window's reload copy (docs/Level-Editor-Design.md, Pen
    /// template edits).
    /// </summary>
    [Serializable]
    internal sealed class LevelTemplateEdit
    {
        // The key as its code, because Unity serializes the edit inside the Level Editor's draft.
        [SerializeField] private int m_templateKeyCode;
        [SerializeField] private string m_displayName;
        [SerializeField] private Vector2Int[] m_baseCells = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] m_cells = Array.Empty<Vector2Int>();
        [SerializeField] private LevelTemplateBlockOrigin[] m_originals = Array.Empty<LevelTemplateBlockOrigin>();
        // Blocks that joined the template after the edit began: placed from its tile or edited as it.
        [SerializeField] private string[] m_addedIds = Array.Empty<string>();

        /// <summary>
        /// Copies both footprints and shifts each to start at cell (0, 0). Originals, when given, are the template's
        /// blocks as they were when the edit began (<see cref="CaptureOriginals"/>); addedIds are the blocks that joined
        /// the template since.
        /// </summary>
        public LevelTemplateEdit(ShapeKey templateKey, string displayName, IReadOnlyList<Vector2Int> baseCells,
            IReadOnlyList<Vector2Int> cells, IReadOnlyList<LevelTemplateBlockOrigin> originals = null,
            IReadOnlyList<string> addedIds = null)
        {
            if (!templateKey.IsValid)
            {
                throw new ArgumentException("A template edit needs the template's key.", nameof(templateKey));
            }

            m_templateKeyCode = templateKey.Code;
            m_displayName = string.IsNullOrWhiteSpace(displayName) ? templateKey.ToString() : displayName;
            m_baseCells = LevelCustomPiece.Normalize(baseCells);
            m_cells = LevelCustomPiece.Normalize(cells);
            m_originals = new LevelTemplateBlockOrigin[originals?.Count ?? 0];
            for (int index = 0; index < m_originals.Length; index++)
            {
                m_originals[index] = originals[index];
            }

            m_addedIds = new string[addedIds?.Count ?? 0];
            for (int index = 0; index < m_addedIds.Length; index++)
            {
                m_addedIds[index] = addedIds[index];
            }
        }

        // The Unity serializer and JsonUtility create the edit through this constructor.
        private LevelTemplateEdit()
        {
        }

        /// <summary>
        /// The key the piece library held for the template when the edit began; the default key for a stored code
        /// that is no piece's key.
        /// </summary>
        public ShapeKey TemplateKey => ShapeKey.TryFromCode(m_templateKeyCode, out ShapeKey key) ? key : default;

        public string DisplayName => string.IsNullOrWhiteSpace(m_displayName) ? TemplateKey.ToString() : m_displayName;
        /// <summary>The library footprint when the edit began, from (0, 0).</summary>
        public IReadOnlyList<Vector2Int> BaseCells => m_baseCells ?? Array.Empty<Vector2Int>();
        /// <summary>The pending footprint, from (0, 0), in library orientation.</summary>
        public IReadOnlyList<Vector2Int> Cells => m_cells ?? Array.Empty<Vector2Int>();
        /// <summary>The key of the footprint when the edit began.</summary>
        public ShapeKey BaseKey => ShapeKey.TryCreate(BaseCells, out ShapeKey key) ? key : default;
        /// <summary>The key of the pending footprint: the key the template's blocks carry while the edit is pending.</summary>
        public ShapeKey Key => ShapeKey.TryCreate(Cells, out ShapeKey key) ? key : default;
        /// <summary>The template's blocks as they were when the edit began; empty when the edit did not record them.</summary>
        public IReadOnlyList<LevelTemplateBlockOrigin> Originals =>
            m_originals ?? Array.Empty<LevelTemplateBlockOrigin>();

        /// <summary>
        /// True when Cells are BaseCells turned or mirrored (the same key): the edit changes nothing and is dropped, so
        /// a stroke chain that ends on a turned or mirrored base never rewrites the library record in a new orientation.
        /// </summary>
        public bool IsNoChange => Key.IsValid && Key == BaseKey;

        /// <summary>
        /// The ids of the blocks that belong to the template while the edit is pending (DEC-193): its blocks when
        /// the edit began, then the blocks placed from its tile or edited as it. Only these follow a stroke or go back
        /// on Discard; a block that merely shares the pending key is not the template's.
        /// </summary>
        public List<string> GetMemberIds()
        {
            List<string> ids = new List<string>(Originals.Count + (m_addedIds?.Length ?? 0));
            for (int index = 0; index < Originals.Count; index++)
            {
                AddId(ids, Originals[index]?.BlockId);
            }

            for (int index = 0; m_addedIds != null && index < m_addedIds.Length; index++)
            {
                AddId(ids, m_addedIds[index]);
            }

            return ids;
        }

        /// <summary>True when the block belongs to the template while the edit is pending.</summary>
        public bool IsMember(string blockId)
        {
            return LevelBlockQueries.ContainsId(GetMemberIds(), blockId);
        }

        /// <summary>The same template, base and originals with another pending footprint.</summary>
        public LevelTemplateEdit WithCells(IReadOnlyList<Vector2Int> cells)
        {
            return new LevelTemplateEdit(TemplateKey, DisplayName, BaseCells, cells, Originals, m_addedIds);
        }

        /// <summary>The same edit with this block among the template's blocks; the same instance when it is one.</summary>
        public LevelTemplateEdit WithMember(string blockId)
        {
            if (string.IsNullOrEmpty(blockId) || IsMember(blockId))
            {
                return this;
            }

            List<string> added = new List<string>(m_addedIds ?? Array.Empty<string>()) { blockId };
            return new LevelTemplateEdit(TemplateKey, DisplayName, BaseCells, Cells, Originals, added);
        }

        /// <summary>
        /// The same template, pending footprint and blocks measured from another base, without originals: after an
        /// undo past a Save the blocks go to the saved footprint, not back to where the older base had them.
        /// </summary>
        public LevelTemplateEdit WithBase(IReadOnlyList<Vector2Int> baseCells)
        {
            return new LevelTemplateEdit(TemplateKey, DisplayName, baseCells, Cells, null, GetMemberIds());
        }

        private static void AddId(List<string> ids, string id)
        {
            if (!string.IsNullOrEmpty(id) && !LevelBlockQueries.ContainsId(ids, id))
            {
                ids.Add(id);
            }
        }

        /// <summary>
        /// The id, origin and cells of every block of the level whose cells are the base footprint turned or mirrored
        /// (its key), in block order: the template's blocks when the edit began.
        /// </summary>
        public static LevelTemplateBlockOrigin[] CaptureOriginals(LevelData level, IReadOnlyList<Vector2Int> baseCells)
        {
            List<LevelTemplateBlockOrigin> originals = new List<LevelTemplateBlockOrigin>();
            if (!ShapeKey.TryCreate(baseCells, out ShapeKey baseKey))
            {
                return originals.ToArray();
            }

            for (int index = 0; level != null && index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (block != null && block.Key == baseKey)
                {
                    originals.Add(new LevelTemplateBlockOrigin(block.Id, block.Origin, LevelRecordCopy.GetCells(block)));
                }
            }

            return originals.ToArray();
        }

        /// <summary>
        /// Maps a block's footprint after a stroke into library orientation: t = FindTransform(current,
        /// Normalize(blockBefore)), the transform Re-stamp gives a block shaped like blockBefore, then
        /// Normalize(Transform(Normalize(blockAfter), inverse of t)). Both block lists must use one frame (board cells,
        /// or cells from one origin); a turn or mirror is linear, so the cells the stroke added or took off land where
        /// they belong on the library footprint. Inverting Re-stamp's own transform, not taking the lowest one from the
        /// block to the footprint, matters for a symmetric footprint: a re-stamp with t then gives every block shaped
        /// like blockBefore exactly blockAfter's footprint. Null when blockBefore is not a rotation or mirror of
        /// current or either list is empty.
        /// </summary>
        public static Vector2Int[] ToLibraryOrientation(IReadOnlyList<Vector2Int> current,
            IReadOnlyList<Vector2Int> blockBefore, IReadOnlyList<Vector2Int> blockAfter)
        {
            if (current == null || current.Count == 0 || blockBefore == null || blockBefore.Count == 0
                || blockAfter == null || blockAfter.Count == 0)
            {
                return null;
            }

            int transform = LevelCustomPiece.FindTransform(current, LevelCustomPiece.Normalize(blockBefore));
            if (transform < 0)
            {
                return null;
            }

            Vector2Int[] turned = LevelCustomPiece.Transform(LevelCustomPiece.Normalize(blockAfter),
                GetInverse(transform));
            return LevelCustomPiece.Normalize(turned);
        }

        // The transform that undoes one of LevelCustomPiece.Transform's: n clockwise turns are undone by 4 - n turns,
        // and a mirror followed by turns is a reflection, which undoes itself.
        private static int GetInverse(int transform)
        {
            int turns = LevelCustomPiece.k_transformCount / 2;
            return transform < turns ? (turns - transform) % turns : transform;
        }
    }
}
