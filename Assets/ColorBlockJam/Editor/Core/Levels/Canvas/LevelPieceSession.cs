using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// One template footprint change Re-stamp maps blocks from (DEC-193): the blocks whose cells form the old
    /// footprint's key take the template's newer footprint, in their own rotation or mirror.
    /// </summary>
    [Serializable]
    internal sealed class LevelFootprintChange
    {
        [SerializeField] private string m_name;
        [SerializeField] private Vector2Int[] m_fromCells = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] m_toCells = Array.Empty<Vector2Int>();

        public LevelFootprintChange(string name, IReadOnlyList<Vector2Int> fromCells, IReadOnlyList<Vector2Int> toCells)
        {
            m_name = name;
            m_fromCells = LevelCells.Copy(fromCells);
            m_toCells = LevelCells.Copy(toCells);
        }

        /// <summary>The template's name, which its Re-stamp offer shows.</summary>
        public string Name => m_name;

        /// <summary>The earlier footprint, in library orientation.</summary>
        public IReadOnlyList<Vector2Int> FromCells => m_fromCells ?? Array.Empty<Vector2Int>();

        /// <summary>The template's footprint now, in library orientation.</summary>
        public IReadOnlyList<Vector2Int> ToCells => m_toCells ?? Array.Empty<Vector2Int>();

        public ShapeKey FromKey => ShapeKey.TryCreate(FromCells, out ShapeKey key) ? key : default;
        public ShapeKey ToKey => ShapeKey.TryCreate(ToCells, out ShapeKey key) ? key : default;

        /// <summary>Points the change at the template's newest footprint, so its blocks go there in one Re-stamp.</summary>
        public void Retarget(string name, IReadOnlyList<Vector2Int> toCells)
        {
            m_name = name;
            m_toCells = LevelCells.Copy(toCells);
        }
    }

    /// <summary>
    /// A Pen template footprint (DEC-125) after Save Level wrote it to a level database: pending until the piece
    /// library holds it, with the last commit's refusal, and the footprint this window last committed, which the next
    /// save accepts as the library's base. The template is the library record its key named when the edit began.
    /// </summary>
    [Serializable]
    internal sealed class LevelTemplateState
    {
        [SerializeField] private string m_databaseGuid;
        [SerializeField] private int m_templateKeyCode;
        [SerializeField] private string m_displayName;
        [SerializeField] private Vector2Int[] m_baseCells = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] m_cells = Array.Empty<Vector2Int>();
        [SerializeField] private bool m_isPending;
        [SerializeField] private string m_issue;
        [SerializeField] private Vector2Int[] m_committedCells = Array.Empty<Vector2Int>();

        public LevelTemplateState(string databaseGuid, ShapeKey templateKey)
        {
            m_databaseGuid = databaseGuid;
            m_templateKeyCode = templateKey.Code;
        }

        public string DatabaseGuid => m_databaseGuid;

        /// <summary>The key the piece library held for the template when the edit began.</summary>
        public ShapeKey TemplateKey => ShapeKey.TryFromCode(m_templateKeyCode, out ShapeKey key) ? key : default;

        /// <summary>True while the saved levels hold the footprint and the piece library does not yet.</summary>
        public bool IsPending => m_isPending;
        /// <summary>Why the last library commit failed, or null.</summary>
        public string Issue => string.IsNullOrEmpty(m_issue) ? null : m_issue;
        /// <summary>The footprint this window last wrote to the piece library, or null.</summary>
        public IReadOnlyList<Vector2Int> CommittedCells => m_committedCells != null && m_committedCells.Length > 0
            ? m_committedCells
            : null;

        /// <summary>The pending footprint as an edit; null when nothing is pending.</summary>
        public LevelTemplateEdit GetPendingEdit()
        {
            return m_isPending && TemplateKey.IsValid && m_cells != null && m_cells.Length > 0 && m_baseCells != null
                   && m_baseCells.Length > 0
                ? new LevelTemplateEdit(TemplateKey, m_displayName, m_baseCells, m_cells)
                : null;
        }

        /// <summary>
        /// Records a saved footprint the library still lacks; a footprint already pending keeps its first base, so the
        /// next save still knows where the library started.
        /// </summary>
        public void SetPending(LevelTemplateEdit edit)
        {
            if (!m_isPending)
            {
                m_baseCells = LevelCells.Copy(edit.BaseCells);
            }

            m_displayName = edit.DisplayName;
            m_cells = LevelCells.Copy(edit.Cells);
            m_isPending = true;
            m_issue = null;
        }

        /// <summary>The library holds the pending footprint now; it is remembered as this window's last commit.</summary>
        public void MarkCommitted()
        {
            if (m_isPending)
            {
                m_committedCells = LevelCells.Copy(m_cells);
            }

            m_isPending = false;
            m_issue = null;
        }

        public void SetIssue(string issue)
        {
            m_issue = issue;
        }

        /// <summary>
        /// The piece library keeps its own footprint: nothing is pending any more, and the last commit stays
        /// remembered.
        /// </summary>
        public void Forget()
        {
            m_isPending = false;
            m_issue = null;
        }
    }

    /// <summary>
    /// The Level Editor's memory of level pieces across a domain reload: the level pieces this session tracks by token
    /// and the one freehand strokes draw, the template footprint changes Re-stamp maps blocks from, and the Pen
    /// template footprints Save Level wrote to levels but not yet to the piece library (DEC-125, DEC-193).
    /// </summary>
    [Serializable]
    internal sealed class LevelPieceSession
    {
        private const string k_piecePrefix = "piece:";

        [SerializeField] private List<LevelFootprintChange> m_footprintChanges = new List<LevelFootprintChange>();
        [SerializeField] private List<LevelNamedPiece> m_namedPieces = new List<LevelNamedPiece>();
        // Tokens are "piece:<scope>:<session>-<n>": the session tag keeps a token a closed window's Piece Editor still
        // shows from naming a piece of a new session.
        [SerializeField] private string m_sessionTag;
        [SerializeField] private int m_lastPieceNumber;
        [SerializeField] private string m_drawingToken;
        [SerializeField] private List<LevelTemplateState> m_templates = new List<LevelTemplateState>();

        /// <summary>The template footprints of this level database that the piece library still lacks.</summary>
        public List<LevelTemplateEdit> GetPendingTemplates(string databaseGuid)
        {
            List<LevelTemplateEdit> edits = new List<LevelTemplateEdit>();
            for (int index = 0; index < m_templates.Count; index++)
            {
                LevelTemplateEdit edit = string.Equals(m_templates[index].DatabaseGuid, databaseGuid,
                    StringComparison.Ordinal)
                    ? m_templates[index].GetPendingEdit()
                    : null;
                if (edit != null)
                {
                    edits.Add(edit);
                }
            }

            return edits;
        }

        /// <summary>The template state of this level database and template key, or null.</summary>
        public LevelTemplateState FindTemplate(string databaseGuid, ShapeKey templateKey)
        {
            for (int index = 0; index < m_templates.Count; index++)
            {
                LevelTemplateState state = m_templates[index];
                if (string.Equals(state.DatabaseGuid, databaseGuid, StringComparison.Ordinal)
                    && state.TemplateKey == templateKey)
                {
                    return state;
                }
            }

            return null;
        }

        /// <summary>Records a template footprint Save Level wrote to this level database.</summary>
        public void SetPendingTemplate(string databaseGuid, LevelTemplateEdit edit)
        {
            LevelTemplateState state = FindTemplate(databaseGuid, edit.TemplateKey);
            if (state == null)
            {
                state = new LevelTemplateState(databaseGuid, edit.TemplateKey);
                m_templates.Add(state);
            }

            state.SetPending(edit);
        }

        /// <summary>Stops writing this level database's saved footprint of this template to the library.</summary>
        public void ForgetPendingTemplate(string databaseGuid, ShapeKey templateKey)
        {
            FindTemplate(databaseGuid, templateKey)?.Forget();
        }

        /// <summary>
        /// Forgets everything this session keeps for a deleted level database: its template footprints and the level
        /// pieces of its levels, so neither keeps the window unsaved nor blocks a template delete.
        /// </summary>
        public void ForgetDatabase(string databaseGuid)
        {
            m_templates.RemoveAll(state => string.Equals(state.DatabaseGuid, databaseGuid, StringComparison.Ordinal));
            for (int index = m_namedPieces.Count - 1; index >= 0; index--)
            {
                if (TrySplitLevelScope(m_namedPieces[index].Scope, out string pieceDatabase, out _)
                    && string.Equals(pieceDatabase, databaseGuid, StringComparison.Ordinal))
                {
                    RemoveNamedPiece(m_namedPieces[index]);
                }
            }
        }

        /// <summary>
        /// Starts over as a new session: no level pieces, template footprints or remembered footprint changes. A
        /// window layout file restores the state of the window it was saved from, which is never the current work.
        /// </summary>
        public void Clear()
        {
            m_footprintChanges.Clear();
            m_namedPieces.Clear();
            m_templates.Clear();
            m_sessionTag = null;
            m_lastPieceNumber = 0;
            m_drawingToken = null;
        }

        /// <summary>The template footprints of every level database that the piece library still lacks.</summary>
        public int CountPendingTemplates()
        {
            int count = 0;
            for (int index = 0; index < m_templates.Count; index++)
            {
                count += m_templates[index].IsPending ? 1 : 0;
            }

            return count;
        }

        /// <summary>
        /// The first level database other than <paramref name="databaseGuid"/> with a footprint waiting for the piece
        /// library, or null.
        /// </summary>
        public string FindOtherPendingDatabase(string databaseGuid)
        {
            for (int index = 0; index < m_templates.Count; index++)
            {
                LevelTemplateState state = m_templates[index];
                if (state.IsPending && !string.Equals(state.DatabaseGuid, databaseGuid, StringComparison.Ordinal))
                {
                    return state.DatabaseGuid;
                }
            }

            return null;
        }

        /// <summary>True when a level database has a footprint of this template waiting for the piece library.</summary>
        public bool HasPendingTemplate(ShapeKey templateKey)
        {
            for (int index = 0; index < m_templates.Count; index++)
            {
                if (m_templates[index].IsPending && m_templates[index].TemplateKey == templateKey)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The footprint this window last committed for this level database and template, or null.</summary>
        public IReadOnlyList<Vector2Int> GetCommittedTemplate(string databaseGuid, ShapeKey templateKey)
        {
            return FindTemplate(databaseGuid, templateKey)?.CommittedCells;
        }

        /// <summary>The level pieces this session tracks, of every level scope.</summary>
        public IReadOnlyList<LevelNamedPiece> NamedPieces => m_namedPieces;

        /// <summary>Adds a level piece with a new session-unique token; the caller made its name unique.</summary>
        public LevelNamedPiece AddNamedPiece(string scope, string displayName)
        {
            if (string.IsNullOrEmpty(m_sessionTag))
            {
                m_sessionTag = Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            m_lastPieceNumber++;
            string token = k_piecePrefix + scope + ":" + m_sessionTag + "-" + m_lastPieceNumber;
            LevelNamedPiece piece = new LevelNamedPiece(scope, token, displayName);
            m_namedPieces.Add(piece);
            return piece;
        }

        /// <summary>The piece with this Piece Editor token, or null.</summary>
        public LevelNamedPiece FindNamedPiece(string token)
        {
            for (int index = 0; token != null && index < m_namedPieces.Count; index++)
            {
                if (string.Equals(m_namedPieces[index].Token, token, StringComparison.Ordinal))
                {
                    return m_namedPieces[index];
                }
            }

            return null;
        }

        /// <summary>
        /// The piece of this scope whose strokes draw this block, or null; the newest wins, so a piece made for the
        /// block later (Make a separate piece) is the one that goes on drawing it.
        /// </summary>
        public LevelNamedPiece FindNamedPieceForBlock(string scope, string blockId)
        {
            for (int index = m_namedPieces.Count - 1; blockId != null && index >= 0; index--)
            {
                LevelNamedPiece piece = m_namedPieces[index];
                if (string.Equals(piece.Scope, scope, StringComparison.Ordinal)
                    && string.Equals(piece.BlockId, blockId, StringComparison.Ordinal))
                {
                    return piece;
                }
            }

            return null;
        }

        /// <summary>The first piece of this scope whose footprint forms this key, or null.</summary>
        public LevelNamedPiece FindNamedPieceForKey(string scope, ShapeKey key)
        {
            for (int index = 0; key.IsValid && index < m_namedPieces.Count; index++)
            {
                LevelNamedPiece piece = m_namedPieces[index];
                if (string.Equals(piece.Scope, scope, StringComparison.Ordinal) && piece.Key == key)
                {
                    return piece;
                }
            }

            return null;
        }

        /// <summary>True when a tracked piece of any scope already has this name, ignoring case.</summary>
        public bool HasNamedDisplayName(string displayName)
        {
            for (int index = 0; index < m_namedPieces.Count; index++)
            {
                if (string.Equals(m_namedPieces[index].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Forgets a tracked piece, for example once the piece library holds its key.</summary>
        public void RemoveNamedPiece(LevelNamedPiece piece)
        {
            m_namedPieces.Remove(piece);
            if (piece != null && string.Equals(piece.Token, m_drawingToken, StringComparison.Ordinal))
            {
                m_drawingToken = null;
            }
        }

        /// <summary>The piece freehand strokes draw, or null.</summary>
        public LevelNamedPiece DrawingPiece => FindNamedPiece(m_drawingToken);

        /// <summary>Makes freehand strokes draw this piece.</summary>
        public void SetDrawingPiece(LevelNamedPiece piece)
        {
            m_drawingToken = piece?.Token;
        }

        /// <summary>
        /// A level's identity across level databases, whose level ids repeat: the database JSON GUID, "/", the level
        /// id. Level piece requests carry it as their level id, and every token embeds it.
        /// </summary>
        public static string GetLevelScope(string databaseGuid, string levelId)
        {
            return databaseGuid + "/" + levelId;
        }

        /// <summary>Splits a level scope at its first "/" (a GUID has none); false for an unscoped id.</summary>
        public static bool TrySplitLevelScope(string scope, out string databaseGuid, out string levelId)
        {
            int slash = scope != null ? scope.IndexOf('/') : -1;
            databaseGuid = slash > 0 ? scope.Substring(0, slash) : null;
            levelId = slash > 0 ? scope.Substring(slash + 1) : scope;
            return slash > 0;
        }

        /// <summary>The template footprint changes Re-stamp maps blocks from, oldest first.</summary>
        public IReadOnlyList<LevelFootprintChange> FootprintChanges => m_footprintChanges;

        /// <summary>
        /// Remembers every template the new catalog gave new cells: the template of <paramref name="previous"/> whose
        /// key <paramref name="current"/> lacks, and the template of <paramref name="current"/> with its name that
        /// <paramref name="previous"/> lacked, so blocks with the earlier footprint can be re-stamped in their own
        /// orientation. A renamed and reshaped template, or a deleted one, is not remembered; a failed read records
        /// nothing.
        /// </summary>
        public void RememberFootprints(LevelShapeCatalog previous, LevelShapeCatalog current)
        {
            if (previous == null || previous.Error != null || current == null || current.Error != null)
            {
                return;
            }

            for (int index = 0; index < previous.Count; index++)
            {
                LevelShapeChoice before = previous.Get(index);
                if (current.IndexOf(before.Key) >= 0)
                {
                    continue;
                }

                LevelShapeChoice after = current.FindByName(before.DisplayName);
                if (after != null && previous.IndexOf(after.Key) < 0)
                {
                    RememberFootprint(after.DisplayName, before.Cells, after.Cells);
                }
            }
        }

        /// <summary>
        /// Remembers that blocks with <paramref name="fromCells"/>' key belong to the template now shaped
        /// <paramref name="toCells"/>. A newer change of the same footprint replaces the remembered one, and every
        /// earlier footprint of the template points at the newest one, so one Re-stamp takes each block there.
        /// </summary>
        public void RememberFootprint(string name, IReadOnlyList<Vector2Int> fromCells, IReadOnlyList<Vector2Int> toCells)
        {
            if (!ShapeKey.TryCreate(fromCells, out ShapeKey fromKey) || !ShapeKey.TryCreate(toCells, out ShapeKey toKey)
                || fromKey == toKey)
            {
                return;
            }

            for (int index = m_footprintChanges.Count - 1; index >= 0; index--)
            {
                LevelFootprintChange change = m_footprintChanges[index];
                if (change.ToKey == fromKey)
                {
                    change.Retarget(name, toCells);
                }

                // A change back to its own footprint, or an older record of the same footprint, goes.
                if (change.FromKey == change.ToKey || change.FromKey == fromKey)
                {
                    m_footprintChanges.RemoveAt(index);
                }
            }

            m_footprintChanges.Add(new LevelFootprintChange(name, fromCells, toCells));
        }

        /// <summary>The remembered change of blocks with this key, or null.</summary>
        public LevelFootprintChange FindFootprintChange(ShapeKey fromKey)
        {
            for (int index = 0; fromKey.IsValid && index < m_footprintChanges.Count; index++)
            {
                if (m_footprintChanges[index].FromKey == fromKey)
                {
                    return m_footprintChanges[index];
                }
            }

            return null;
        }

        /// <summary>The earlier footprints of the template shaped like this key now, in library orientation.</summary>
        public List<IReadOnlyList<Vector2Int>> GetPreviousFootprints(ShapeKey templateKey)
        {
            List<IReadOnlyList<Vector2Int>> footprints = new List<IReadOnlyList<Vector2Int>>();
            for (int index = 0; templateKey.IsValid && index < m_footprintChanges.Count; index++)
            {
                if (m_footprintChanges[index].ToKey == templateKey)
                {
                    footprints.Add(m_footprintChanges[index].FromCells);
                }
            }

            return footprints;
        }

        /// <summary>
        /// Drops the remembered change of blocks with this key once no block of these levels, the whole active
        /// database, has it.
        /// </summary>
        public void ForgetUnlessUsed(ShapeKey fromKey, IReadOnlyList<LevelData> levels)
        {
            LevelFootprintChange change = FindFootprintChange(fromKey);
            if (change == null)
            {
                return;
            }

            for (int index = 0; levels != null && index < levels.Count; index++)
            {
                if (LevelBlockQueries.HasBlock(levels[index], fromKey))
                {
                    return;
                }
            }

            m_footprintChanges.Remove(change);
        }
    }
}
