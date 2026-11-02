using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Owns piece catalogs, level piece names, editing identities and the session's piece operations. A piece is the
    /// ShapeKey of its cells (DEC-193): a key the piece library holds is that template, any other key a level piece
    /// named in its level. It works on snapshots and returns edits for the window to apply through its draft owner; it
    /// knows no UI controls, dialogs or Editor subscriptions.
    /// </summary>
    internal sealed class LevelPieceEditController
    {
        internal const string k_penHint = "Click a block with the Pen to edit it, or press + New piece.";
        private readonly LevelDraftController m_draftController;
        private readonly LevelPieceSession m_pieceSession;
        private BlockShapeLibrary m_shapeLibrary;
        private LevelShapeCatalog m_shapeCatalog = LevelShapeCatalog.Missing;
        private string m_shapeCatalogJson;
        private readonly List<LevelShapeChoice> m_customPieces = new List<LevelShapeChoice>();
        private LevelPieceRequest m_lastMirror;
        private string m_penEditBlockId;
        // True while the Pen edits a block that was a level piece when the edit began, so a stroke that passes through
        // a template's shape keeps editing it as that level piece instead of starting a template edit.
        private bool m_penEditsLevelPiece;
        // The blocks of the level piece the Pen edits, recorded when the edit starts: the blocks keyed like the edited
        // block at that moment (DEC-193). Only these follow its strokes, whatever key a stroke passes through.
        private readonly List<string> m_penMemberIds = new List<string>();
        private readonly List<LevelShapeChoice> m_otherLevelPieces = new List<LevelShapeChoice>();
        private readonly List<LevelTemplateSavePlan.OtherLevel> m_otherLevels = new List<LevelTemplateSavePlan.OtherLevel>();
        // The same other levels as plain level data, the name lookups' input.
        private readonly List<LevelData> m_otherLevelData = new List<LevelData>();
        private string m_otherLevelsSource;
        private LevelShapeCatalog m_otherLevelsCatalog;
        private int m_otherLevelsIndex = -1;
        private int m_otherLevelsVersion;
        private readonly CatalogOverlay m_editCatalog = new CatalogOverlay();
        private readonly CatalogOverlay m_savedCatalog = new CatalogOverlay();
        private LevelTemplateSavePlan m_bannerPlan;
        private string m_bannerPlanKey;
        private LevelShapeCatalog m_bannerPlanLibrary;

        internal LevelPieceEditController(LevelDraftController draft, LevelPieceSession session)
        {
            m_draftController = draft;
            m_pieceSession = session;
        }

        internal BlockShapeLibrary Library { get => m_shapeLibrary; set => m_shapeLibrary = value; }
        internal LevelShapeCatalog Catalog => m_shapeCatalog;
        internal IReadOnlyList<LevelShapeChoice> CustomPieces => m_customPieces;
        internal string PenEditBlockId => m_penEditBlockId;

        internal List<LevelTemplateEdit> GetPendingTemplates(string databaseGuid)
        {
            return m_pieceSession.GetPendingTemplates(databaseGuid);
        }

        internal void MoveTemplateEditsToSession(string databaseGuid)
        {
            for (int index = 0; index < m_draftController.Draft.TemplateEdits.Count; index++)
            {
                m_pieceSession.SetPendingTemplate(databaseGuid, m_draftController.Draft.TemplateEdits[index]);
            }

            m_draftController.Draft.SetTemplateEdits(null);
        }

        internal void MarkTemplateCommit(string databaseGuid, IReadOnlyList<LevelTemplateEdit> pending, string issue)
        {
            for (int index = 0; index < pending.Count; index++)
            {
                LevelTemplateState state = m_pieceSession.FindTemplate(databaseGuid, pending[index].TemplateKey);
                if (state == null)
                {
                    continue;
                }

                if (issue == null)
                {
                    state.MarkCommitted();
                }
                else
                {
                    state.SetIssue(issue);
                }
            }
        }

        internal string ForgetUnheldTemplates(string databaseGuid, LevelCatalog levels, bool sourceValid)
        {
            List<LevelTemplateEdit> saved = m_pieceSession.GetPendingTemplates(databaseGuid);
            if (saved.Count == 0 || !sourceValid || levels == null)
            {
                return string.Empty;
            }

            List<LevelTemplateEdit> dropped = new List<LevelTemplateEdit>();
            for (int index = 0; index < saved.Count; index++)
            {
                bool held = false;
                for (int levelIndex = 0; !held && levelIndex < levels.Count; levelIndex++)
                {
                    held = LevelBlockQueries.HasBlock(levels.GetAt(levelIndex), saved[index].Key);
                }

                if (!held)
                {
                    m_pieceSession.ForgetPendingTemplate(databaseGuid, saved[index].TemplateKey);
                    dropped.Add(saved[index]);
                }
            }

            return dropped.Count == 0 ? string.Empty
                : $" No level holds the new footprint of {JoinTemplateNames(dropped)} any more, so Save Level no " +
                  "longer writes it to the piece library.";
        }

        internal bool RefreshCatalog(bool force)
        {
            string loadedJson = m_shapeLibrary != null ? m_shapeLibrary.LoadedJson : null;
            if (!force && m_shapeCatalog.Error == null && loadedJson != null
                && ReferenceEquals(loadedJson, m_shapeCatalogJson))
            {
                return false;
            }

            LevelShapeCatalog previous = m_shapeCatalog;
            m_shapeCatalog = LevelShapeCatalog.Read(m_shapeLibrary);
            m_shapeCatalogJson = m_shapeLibrary != null ? m_shapeLibrary.LoadedJson : null;
            m_pieceSession.RememberFootprints(previous, m_shapeCatalog);
            return true;
        }

        internal void InvalidateOtherLevels()
        {
            m_otherLevelsIndex = -1;
        }

        internal void EndPenEdit()
        {
            m_penEditBlockId = null;
            m_penEditsLevelPiece = false;
            m_penMemberIds.Clear();
        }

        internal void RemoveMissingPenEdit(LevelData level)
        {
            if (m_penEditBlockId != null && FindBlockIndex(level, m_penEditBlockId) < 0)
            {
                EndPenEdit();
            }
        }

        internal void RememberMirror(LevelPieceRequest request)
        {
            m_lastMirror = request;
        }

        /// <summary>
        /// Reads the database's other levels again when the loaded JSON, the catalog or the open level changed, and
        /// lists their level pieces: the first-seen keys the saved catalog lacks, in level and block order, each under
        /// the name its level gives it.
        /// </summary>
        internal void UpdateOtherLevels(LevelPieceSource source)
        {
            if (m_draftController.Draft == null)
            {
                m_otherLevelPieces.Clear();
                m_otherLevels.Clear();
                m_otherLevelData.Clear();
                m_otherLevelsVersion++;
                m_otherLevelsSource = null;
                m_otherLevelsCatalog = null;
                m_otherLevelsIndex = -1;
                return;
            }

            LevelShapeCatalog catalog = GetSavedCatalog(source.Guid);
            if (ReferenceEquals(m_otherLevelsSource, source.SourceHash)
                && ReferenceEquals(m_otherLevelsCatalog, catalog)
                && m_otherLevelsIndex == source.SelectedIndex)
            {
                return;
            }

            m_otherLevelsSource = source.SourceHash;
            m_otherLevelsCatalog = catalog;
            m_otherLevelsIndex = source.SelectedIndex;
            m_otherLevelsVersion++;
            m_otherLevelPieces.Clear();
            m_otherLevels.Clear();
            m_otherLevelData.Clear();
            if (!source.IsValid || source.Levels == null)
            {
                return;
            }

            m_otherLevels.AddRange(LevelTemplateSavePlan.GetOtherLevels(source.Levels, source.SelectedIndex));
            for (int index = 0; index < m_otherLevels.Count; index++)
            {
                m_otherLevelData.Add(m_otherLevels[index].Level);
            }

            for (int index = 0; index < m_otherLevelData.Count; index++)
            {
                LevelData other = m_otherLevelData[index];
                LevelPieceName[] names = LevelPieceNames.Resolve(other, m_otherLevelData, catalog, null,
                    GetReservedNames());
                for (int name = 0; name < names.Length; name++)
                {
                    if (IndexOfKey(m_otherLevelPieces, names[name].Key) < 0)
                    {
                        m_otherLevelPieces.Add(new LevelShapeChoice(names[name].Name,
                            GetFirstBlockCells(other, names[name].Key)));
                    }
                }
            }
        }

        /// <summary>
        /// Lists the Level pieces section: the open level's level pieces in block order, then this session's pieces of
        /// the open level that no block uses yet (drawn only in the Piece Editor), then the other levels' pieces; each
        /// under its level name (LevelPieceNames). The palette is rebuilt only when the list changed.
        /// </summary>
        internal bool RefreshCustomPieces(LevelPieceSource source, LevelData level)
        {
            UpdateOtherLevels(source);
            List<LevelShapeChoice> pieces = new List<LevelShapeChoice>();
            string scope = source.GetScope(level);
            LevelShapeCatalog catalog = GetEditCatalog(source.Guid);
            if (level != null && scope != null)
            {
                SyncNamedPieces(scope, level);
                LevelPieceName[] names = ResolveNames(scope, level, catalog);
                for (int index = 0; index < names.Length; index++)
                {
                    pieces.Add(new LevelShapeChoice(names[index].Name, GetTileCells(scope, level, names[index].Key)));
                }

                for (int index = 0; index < m_pieceSession.NamedPieces.Count; index++)
                {
                    LevelNamedPiece piece = m_pieceSession.NamedPieces[index];
                    if (string.Equals(piece.Scope, scope, StringComparison.Ordinal) && !piece.WasPlaced
                        && piece.Key.IsValid && catalog.IndexOf(piece.Key) < 0 && IndexOfKey(pieces, piece.Key) < 0)
                    {
                        pieces.Add(new LevelShapeChoice(GetEffectiveName(level, catalog, piece), piece.Cells));
                    }
                }
            }

            for (int index = 0; index < m_otherLevelPieces.Count; index++)
            {
                LevelShapeChoice other = m_otherLevelPieces[index];
                if (catalog.IndexOf(other.Key) < 0 && IndexOfKey(pieces, other.Key) < 0)
                {
                    pieces.Add(other);
                }
            }

            if (HasSamePieces(pieces, m_customPieces))
            {
                return false;
            }

            m_customPieces.Clear();
            m_customPieces.AddRange(pieces);
            return true;
        }

        /// <summary>
        /// The pieces table Save Level writes for this level (DEC-193): LevelPieceNames.Resolve against the piece library
        /// with its pending template footprints, the database's other levels and this session's piece names.
        /// </summary>
        internal LevelPieceName[] ResolvePieceNames(string databaseGuid, LevelData level,
            IReadOnlyList<LevelData> otherLevels)
        {
            string scope = level != null ? LevelPieceSession.GetLevelScope(databaseGuid, level.Id) : null;
            return LevelPieceNames.Resolve(level, otherLevels, GetEditCatalog(databaseGuid),
                key => m_pieceSession.FindNamedPieceForKey(scope, key)?.DisplayName, GetReservedNames());
        }

        /// <summary>
        /// A name for every piece the level check may name (DEC-144, DEC-193): the templates' and the level's own
        /// pieces', read into a dictionary here on the main thread. The check runs the returned lookup on its worker
        /// thread (DEC-175), which only reads that dictionary and never a Unity object.
        /// </summary>
        internal Func<ShapeKey, string> CreateShapeNameLookup(string databaseGuid, LevelData level,
            IReadOnlyList<LevelData> otherLevels)
        {
            Dictionary<ShapeKey, string> names = new Dictionary<ShapeKey, string>();
            LevelShapeCatalog catalog = GetEditCatalog(databaseGuid);
            for (int index = 0; catalog.Error == null && index < catalog.Count; index++)
            {
                LevelShapeChoice template = catalog.Get(index);
                if (template.Key.IsValid && !names.ContainsKey(template.Key))
                {
                    names.Add(template.Key, template.DisplayName);
                }
            }

            LevelPieceName[] pieces = ResolvePieceNames(databaseGuid, level, otherLevels);
            for (int index = 0; index < pieces.Length; index++)
            {
                if (!names.ContainsKey(pieces[index].Key))
                {
                    names.Add(pieces[index].Key, pieces[index].Name);
                }
            }

            return key => names.TryGetValue(key, out string name) ? name : null;
        }

        /// <summary>
        /// The name designers see for a piece in the open level: the template's name for a template key, else the
        /// level's name for it (LevelPieceNames.Resolve), else a session piece's or another level's, else "n-cell
        /// piece".
        /// </summary>
        internal string GetPieceName(string databaseGuid, LevelData level, ShapeKey key)
        {
            LevelShapeCatalog catalog = GetEditCatalog(databaseGuid);
            LevelShapeChoice template = catalog.Find(key);
            if (template != null)
            {
                return template.DisplayName;
            }

            string scope = level != null ? LevelPieceSession.GetLevelScope(databaseGuid, level.Id) : null;
            if (scope != null)
            {
                LevelPieceName[] names = ResolveNames(scope, level, catalog);
                for (int index = 0; index < names.Length; index++)
                {
                    if (names[index].Key == key)
                    {
                        return names[index].Name;
                    }
                }
            }

            string sessionName = m_pieceSession.FindNamedPieceForKey(scope, key)?.DisplayName;
            if (sessionName != null)
            {
                return sessionName;
            }

            int other = IndexOfKey(m_otherLevelPieces, key);
            if (other >= 0)
            {
                return m_otherLevelPieces[other].DisplayName;
            }

            return key.IsValid ? $"{key.Decode().Length}-cell piece" : "level piece";
        }

        internal LevelShapeCatalog GetEditCatalog(string databaseGuid)
        {
            List<LevelTemplateEdit> edits = m_pieceSession.GetPendingTemplates(databaseGuid);
            if (m_draftController.Draft != null)
            {
                for (int index = 0; index < m_draftController.Draft.TemplateEdits.Count; index++)
                {
                    edits.Add(m_draftController.Draft.TemplateEdits[index]);
                }
            }

            return m_editCatalog.Get(m_shapeCatalog, edits);
        }

        internal LevelShapeCatalog GetSavedCatalog(string databaseGuid)
        {
            return m_savedCatalog.Get(m_shapeCatalog, m_pieceSession.GetPendingTemplates(databaseGuid));
        }

        // The template's footprint in the saved catalog: the library's, or the one Save Level wrote to this database.
        private IReadOnlyList<Vector2Int> GetSavedFootprint(string databaseGuid, ShapeKey templateKey, string name)
        {
            LevelShapeCatalog saved = GetSavedCatalog(databaseGuid);
            int index = saved.IndexOfTemplate(templateKey, name);
            return index >= 0 ? saved.Get(index).Cells : null;
        }

        // True when the key is a template's in the edit catalog, a pending footprint included.
        private bool IsTemplateKey(string databaseGuid, ShapeKey key)
        {
            return GetEditCatalog(databaseGuid).IndexOf(key) >= 0;
        }

        internal static bool IsSameFootprint(IReadOnlyList<Vector2Int> a, IReadOnlyList<Vector2Int> b)
        {
            return a != null && b != null && a.Count > 0 && a.Count == b.Count
                   && LevelCustomPiece.FindTransform(a, b) >= 0;
        }

        internal static bool HasSamePieces(List<LevelShapeChoice> a, List<LevelShapeChoice> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int index = 0; index < a.Count; index++)
            {
                if (a[index].Key != b[index].Key
                    || !string.Equals(a[index].DisplayName, b[index].DisplayName, StringComparison.Ordinal)
                    || !a[index].HasSameCells(b[index]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static int FindBlockIndex(LevelData level, string blockId)
        {
            for (int index = 0; level != null && index < level.BlockCount; index++)
            {
                if (string.Equals(level.GetBlockAt(index)?.Id, blockId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        // Forwards to the one shared "blocks with this key" query so every copy of this loop answers the same way.
        internal static int CountBlocksUsing(LevelData level, ShapeKey key)
        {
            return LevelBlockQueries.CountBlocks(level, key);
        }

        internal static List<Vector2Int> GetBoardCells(LevelBlockData block)
        {
            List<Vector2Int> cells = new List<Vector2Int>(block.CellCount + 1);
            for (int index = 0; index < block.CellCount; index++)
            {
                cells.Add(block.Origin + block.GetCellAt(index));
            }

            return cells;
        }

        /// <summary>
        /// Redraws the block at <paramref name="drawn"/> with these board cells in its colour and re-stamps the
        /// piece's other blocks, the ones <paramref name="memberIds"/> lists that still have the drawn block's previous
        /// key, in their own orientation. A block that merely shares that key, such as another piece's block the drawing
        /// passes through, never moves (DEC-193). Refused as a whole with the reason, naming the piece
        /// <paramref name="pieceName"/>.
        /// </summary>
        internal static bool TrySetDrawnCells(LevelData level, int drawn, IReadOnlyList<Vector2Int> cells,
            string pieceName, IReadOnlyList<string> memberIds, out LevelData result, out string issue)
        {
            LevelBlockData block = level.GetBlockAt(drawn);
            ShapeKey previous = block.Key;
            Vector2Int[] previousCells = LevelCustomPiece.Normalize(LevelRecordCopy.GetCells(block));
            Vector2Int origin = LevelShapeThumbnail.GetBounds(cells).min;
            Vector2Int[] footprint = LevelCustomPiece.Normalize(cells);
            if (!LevelShapeRestamp.TryRedrawBlock(level, drawn, footprint, origin, block.ColorId, out result,
                    out issue))
            {
                return false;
            }

            // The redrawn block has the new key, so only the piece's other blocks of the previous key move.
            return memberIds == null || memberIds.Count == 0 || CountBlocksUsing(result, previous) == 0
                   || LevelShapeRestamp.TryRestamp(result, previous, previousCells, footprint, pieceName, out result,
                       out _, out issue, onlyBlockIds: memberIds);
        }

        internal LevelNamedPiece AddNewNamedPiece(LevelPieceSource source, LevelData level, string scope)
        {
            RefreshCatalog(false);
            HashSet<string> taken = GetTakenNames(source, level);
            return m_pieceSession.AddNamedPiece(scope, LevelNamedPiece.GetDefaultName(level.Name, taken.Contains));
        }

        // Every name a new piece must not repeat, ignoring case: the templates', the database's tables', the session's
        // and the names the Level pieces section shows now, some of which Save Level has not written yet.
        private HashSet<string> GetTakenNames(LevelPieceSource source, LevelData level)
        {
            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; m_shapeCatalog.Error == null && index < m_shapeCatalog.Count; index++)
            {
                taken.Add(m_shapeCatalog.Get(index).DisplayName);
            }

            List<LevelData> levels = source.GetLevels(level);
            for (int index = 0; index < levels.Count; index++)
            {
                for (int name = 0; levels[index] != null && name < levels[index].PieceNameCount; name++)
                {
                    string text = levels[index].GetPieceNameAt(name)?.Name;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        taken.Add(text);
                    }
                }
            }

            foreach (string name in GetReservedNames())
            {
                taken.Add(name);
            }

            for (int index = 0; index < m_customPieces.Count; index++)
            {
                taken.Add(m_customPieces[index].DisplayName);
            }

            return taken;
        }

        internal LevelShapeChoice FindCustomPiece(ShapeKey key)
        {
            int index = IndexOfKey(m_customPieces, key);
            return index >= 0 ? m_customPieces[index] : null;
        }

        internal LevelTemplateSavePlan CreateTemplatePlan(string databaseGuid, LevelData candidate,
            IReadOnlyList<LevelTemplateSavePlan.OtherLevel> others, IReadOnlyList<LevelTemplateEdit> saved,
            IReadOnlyList<LevelTemplateEdit> draft)
        {
            return LevelTemplateSavePlan.Create(candidate, others, draft, m_shapeCatalog,
                templateKey => m_pieceSession.GetCommittedTemplate(databaseGuid, templateKey),
                m_pieceSession.GetPreviousFootprints, saved);
        }

        internal LevelTemplateSavePlan GetBannerPlan(string databaseGuid, LevelData level, List<LevelTemplateEdit> saved,
            IReadOnlyList<LevelTemplateEdit> draft)
        {
            if (saved.Count + draft.Count == 0)
            {
                m_bannerPlan = null;
                m_bannerPlanKey = null;
                return null;
            }

            string key = JsonUtility.ToJson(level) + CatalogOverlay.GetKey(saved) + "#" + CatalogOverlay.GetKey(draft) +
                         "#" + m_otherLevelsVersion;
            if (m_bannerPlan == null || !ReferenceEquals(m_bannerPlanLibrary, m_shapeCatalog)
                || !string.Equals(key, m_bannerPlanKey, StringComparison.Ordinal))
            {
                m_bannerPlan = CreateTemplatePlan(databaseGuid, level, m_otherLevels, saved, draft);
                m_bannerPlanKey = key;
                m_bannerPlanLibrary = m_shapeCatalog;
            }

            return m_bannerPlan;
        }

        /// <summary>
        /// " and M in K other levels of &lt;database&gt;" (or ", re-stamps M in K ..."): the other levels' blocks of
        /// the template's saved footprint; nothing while the database is unreadable.
        /// </summary>
        internal string DescribeOtherBlocks(LevelPieceSource source, string databaseName, ShapeKey templateKey,
            string templateName, string verb = null)
        {
            if (!source.IsValid || source.Levels == null)
            {
                return string.Empty;
            }

            LevelShapeCatalog saved = GetSavedCatalog(source.Guid);
            int record = saved.IndexOfTemplate(templateKey, templateName);
            ShapeKey savedKey = record >= 0 ? saved.Get(record).Key : templateKey;
            int blocks = 0;
            int levels = 0;
            for (int index = 0; index < m_otherLevels.Count; index++)
            {
                int count = CountBlocksUsing(m_otherLevels[index].Level, savedKey);
                blocks += count;
                levels += count > 0 ? 1 : 0;
            }

            return verb != null
                ? $", {verb} {blocks} in {levels} other levels of {databaseName}"
                : $" and {blocks} in {levels} other levels of {databaseName}";
        }

        /// <summary>
        /// Forgets the tracked level pieces whose key this library commit added (the Piece Editor saved them as
        /// templates, DEC-180): the key is a template everywhere now. True when a piece was forgotten.
        /// </summary>
        internal bool DropNamedPiecesInLibrary(LevelShapeCatalog previous)
        {
            bool dropped = false;
            for (int index = m_pieceSession.NamedPieces.Count - 1; index >= 0; index--)
            {
                LevelNamedPiece piece = m_pieceSession.NamedPieces[index];
                ShapeKey key = piece.Key;
                bool added = previous == null || previous.Error != null || previous.IndexOf(key) < 0;
                if (key.IsValid && m_shapeCatalog.IndexOf(key) >= 0 && added)
                {
                    m_pieceSession.RemoveNamedPiece(piece);
                    dropped = true;
                }
            }

            return dropped;
        }

        /// <summary>
        /// The template a Pen edit of this block changes (DEC-125): the edit catalog's template of the block's key, with
        /// any pending footprint. Null for a level piece, and for the block the Pen started editing as a level piece,
        /// which stays one even when a stroke makes it a template's shape.
        /// </summary>
        internal LevelShapeChoice GetEditedTemplate(string databaseGuid, LevelBlockData block)
        {
            if (block == null || block.CellCount == 0
                || (m_penEditsLevelPiece && string.Equals(block.Id, m_penEditBlockId, StringComparison.Ordinal)))
            {
                return null;
            }

            return GetEditCatalog(databaseGuid).Find(block.Key);
        }

        internal string DescribePenEdit(string databaseGuid, LevelBlockData block)
        {
            LevelShapeChoice template = GetEditedTemplate(databaseGuid, block);
            if (template != null)
            {
                return $"Editing {block.Id}, a block of template {template.DisplayName}: click a green cell to add " +
                       $"it, right click takes one off. Every {template.DisplayName} block follows.";
            }

            return $"Editing {block.Id}, a block of level piece " +
                   $"{GetPieceName(databaseGuid, m_draftController.Snapshot(), block.Key)}: click a green cell to add " +
                   "it, right click takes one off. Its other blocks follow.";
        }

        internal bool IsPenEditing(LevelEditorToolState tool, out string blockId)
        {
            blockId = null;
            if (m_penEditBlockId == null || tool.Tool != LevelEditorTool.Pen || tool.IsFreehandArmed
                || tool.ObstacleBrush != LevelObstacleBrush.None
                || FindBlockIndex(m_draftController.Snapshot(), m_penEditBlockId) < 0)
            {
                return false;
            }

            blockId = m_penEditBlockId;
            return true;
        }

        internal LevelBlockData StartPenEdit(string databaseGuid, int blockIndex, LevelEditorToolState tool)
        {
            LevelData level = m_draftController.Snapshot();
            LevelBlockData block = level != null && blockIndex >= 0 && blockIndex < level.BlockCount
                ? level.GetBlockAt(blockIndex)
                : null;
            if (block == null || tool.Tool != LevelEditorTool.Pen)
            {
                return null;
            }

            if (tool.IsFreehandArmed)
            {
                tool.SelectPen();
            }

            EndPenEdit();
            m_penEditsLevelPiece = GetEditedTemplate(databaseGuid, block) == null;
            m_penEditBlockId = block.Id;
            // The piece's blocks are taken now, at rest, before a stroke can give its key to another piece's block.
            m_penMemberIds.AddRange(LevelBlockQueries.GetBlockIds(level, block.Key));
            return block;
        }

        internal string GetDrawingBlockId(string scope)
        {
            LevelNamedPiece piece = m_pieceSession.DrawingPiece;
            return piece != null && string.Equals(piece.Scope, scope, StringComparison.Ordinal) ? piece.BlockId : null;
        }

        /// <summary>
        /// The Piece Editor request for a Level pieces tile: the session's piece of the tile's key in this scope, made
        /// now when there is none, with the tile's cells and name, so the Piece Editor's changes reach its blocks. A
        /// tile click starts an edit of the piece, so its blocks are taken now: the blocks with the tile's key
        /// (DEC-193).
        /// </summary>
        internal LevelPieceRequest GetTileMirror(string scope, LevelData level, LevelShapeChoice choice)
        {
            LevelNamedPiece piece = m_pieceSession.FindNamedPieceForKey(scope, choice.Key);
            if (piece == null)
            {
                piece = m_pieceSession.AddNamedPiece(scope, choice.DisplayName);
                piece.SetCells(choice.Cells);
                int first = LevelBlockQueries.FindBlock(level, choice.Key);
                piece.BlockId = first >= 0 ? level.GetBlockAt(first).Id : null;
                piece.WasPlaced = first >= 0;
            }

            piece.SetMembers(LevelBlockQueries.GetBlockIds(level, choice.Key));
            return new LevelPieceRequest(scope, piece.Token, piece.Cells, choice.DisplayName);
        }

        /// <summary>
        /// A block placed from a tile, or given a piece through Piece Type, joins that piece (DEC-193): a template with a
        /// pending footprint in the draft records it in that edit, through the returned step that the change's undo step
        /// applies; a level piece this session tracks records it in the session. Null when no draft edit changes.
        /// </summary>
        internal Action<LevelDraft> JoinPlacedBlock(string scope, ShapeKey key, string blockId)
        {
            LevelTemplateEdit edit = m_draftController.Draft != null ? FindDraftEdit(key) : null;
            if (edit != null)
            {
                LevelTemplateEdit joined = edit.WithMember(blockId);
                return ReferenceEquals(joined, edit) ? null : draft => draft.SetTemplateEdit(joined);
            }

            if (scope != null)
            {
                m_pieceSession.FindNamedPieceForKey(scope, key)?.AddMember(blockId);
            }

            return null;
        }

        internal LevelPieceRequest GetUndoMirror(string databaseGuid, string scope)
        {
            if (scope == null)
            {
                return null;
            }

            // A stale piece, or another level's piece in the Piece Editor, is left as it is.
            LevelNamedPiece piece = m_lastMirror != null
                ? m_pieceSession.FindNamedPiece(m_lastMirror.PieceId)
                : m_pieceSession.DrawingPiece;
            if (piece == null || piece.Cells.Count == 0 || !string.Equals(piece.Scope, scope, StringComparison.Ordinal))
            {
                return null;
            }

            LevelData level = m_draftController.Snapshot();
            string name = GetEffectiveName(level, GetEditCatalog(databaseGuid), piece);
            if (m_lastMirror != null && string.Equals(m_lastMirror.PieceId, piece.Token, StringComparison.Ordinal)
                && string.Equals(m_lastMirror.Name, name, StringComparison.Ordinal)
                && m_lastMirror.Cells.Count == piece.Cells.Count
                && LevelCustomPiece.FindTransform(m_lastMirror.Cells, piece.Cells) == 0)
            {
                return null;
            }

            return new LevelPieceRequest(scope, piece.Token, piece.Cells, name);
        }


        internal LevelNamedPiece CreateNamedPiece(LevelPieceSource source)
        {
            LevelData level = m_draftController.Snapshot();
            string scope = source.GetScope(level);
            if (level == null || scope == null)
            {
                return null;
            }

            LevelNamedPiece piece = AddNewNamedPiece(source, level, scope);
            m_pieceSession.SetDrawingPiece(piece);
            return piece;
        }

        internal LevelPieceEdit Draw(LevelPieceSource source, LevelEditorToolState tool, IReadOnlyList<Vector2Int> cells)
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null || tool.Tool != LevelEditorTool.Pen || cells == null || cells.Count == 0)
            {
                return null;
            }

            if (!tool.IsFreehandArmed)
            {
                return GrowEditedBlock(source, tool, level, cells);
            }

            string scope = source.GetScope(level);
            LevelNamedPiece piece = m_pieceSession.DrawingPiece;
            if (piece == null || !string.Equals(piece.Scope, scope, StringComparison.Ordinal))
            {
                return LevelPieceEdit.Hint("Press + New piece to start a piece for this level.");
            }

            string issue = LevelShapeRestamp.GetBoardObjectIssue(level, Vector2Int.zero, cells);
            if (issue != null)
            {
                return LevelPieceEdit.Status("The stroke made no block. " + issue);
            }

            int drawn = piece.BlockId != null ? FindBlockIndex(level, piece.BlockId) : -1;
            return drawn < 0
                ? PlaceFirstCells(level, scope, piece, cells, tool.ColorId)
                : GrowPieceBlock(databaseGuid: source.Guid, level: level, scope: scope, piece: piece, template: null,
                    drawn: drawn, cells: cells, subject: GetEffectiveName(level, GetEditCatalog(source.Guid), piece));
        }

        private LevelPieceEdit GrowEditedBlock(LevelPieceSource source, LevelEditorToolState tool, LevelData level,
            IReadOnlyList<Vector2Int> cells)
        {
            int edited = IsPenEditing(tool, out string blockId) ? FindBlockIndex(level, blockId) : -1;
            if (edited < 0)
            {
                return LevelPieceEdit.Hint(k_penHint);
            }

            string subject = level.GetBlockAt(edited).Id;
            string issue = LevelShapeRestamp.GetBoardObjectIssue(level, Vector2Int.zero, cells);
            if (issue != null)
            {
                return LevelPieceEdit.Status($"{subject} changed nothing. {issue}");
            }

            LevelShapeChoice template = GetEditedTemplate(source.Guid, level.GetBlockAt(edited));
            if (template != null)
            {
                return GrowPieceBlock(databaseGuid: source.Guid, level: level, scope: null, piece: null,
                    template: template, drawn: edited, cells: cells, subject: subject);
            }

            if (!TryGetEditPiece(source, tool, level, out string scope, out LevelNamedPiece piece, out int drawn,
                    out bool created))
            {
                return null;
            }

            LevelPieceEdit edit = GrowPieceBlock(databaseGuid: source.Guid, level: level, scope: scope, piece: piece,
                template: null, drawn: drawn, cells: cells, subject: subject);
            ForgetUnusedEditPiece(piece, created, edit);
            return edit;
        }

        // The session's piece for the level piece block the Pen edits: the one that draws it, else one of its key,
        // else a new one under the level's name for the key (created). Its blocks are the ones the edit recorded when
        // it started (m_penMemberIds), so every stroke of the edit moves the same blocks.
        private bool TryGetEditPiece(LevelPieceSource source, LevelEditorToolState tool, LevelData level,
            out string scope, out LevelNamedPiece piece, out int drawn, out bool created)
        {
            scope = source.GetScope(level);
            piece = null;
            drawn = -1;
            created = false;
            if (scope == null || !IsPenEditing(tool, out string blockId) || (drawn = FindBlockIndex(level, blockId)) < 0)
            {
                return false;
            }

            LevelBlockData block = level.GetBlockAt(drawn);
            piece = m_pieceSession.FindNamedPieceForBlock(scope, block.Id)
                    ?? m_pieceSession.FindNamedPieceForKey(scope, block.Key);
            if (piece == null)
            {
                piece = m_pieceSession.AddNamedPiece(scope, GetPieceName(source.Guid, level, block.Key));
                created = true;
            }

            piece.SetCells(LevelCustomPiece.Normalize(LevelRecordCopy.GetCells(block)));
            piece.BlockId = block.Id;
            piece.WasPlaced = true;
            piece.SetMembers(m_penMemberIds);
            piece.AddMember(block.Id);
            return true;
        }

        // A piece made for an edit that changed nothing is forgotten again.
        private void ForgetUnusedEditPiece(LevelNamedPiece piece, bool created, LevelPieceEdit edit)
        {
            if (created && (edit == null || edit.Level == null))
            {
                m_pieceSession.RemoveNamedPiece(piece);
            }
        }

        private LevelPieceEdit GrowPieceBlock(string databaseGuid, LevelData level, string scope, LevelNamedPiece piece,
            LevelShapeChoice template, int drawn, IReadOnlyList<Vector2Int> cells, string subject)
        {
            List<Vector2Int> grown = GetBoardCells(level.GetBlockAt(drawn));
            int before = grown.Count;
            for (int index = 0; index < cells.Count; index++)
            {
                if (!grown.Contains(cells[index]))
                {
                    grown.Add(cells[index]);
                }
            }

            if (grown.Count == before)
            {
                return null;
            }

            if (!LevelCustomPiece.IsConnected(grown))
            {
                return LevelPieceEdit.Hint($"Click next to {subject}.");
            }

            if (!LevelCustomPiece.TryValidate(grown, out string issue))
            {
                return LevelPieceEdit.Status($"{subject} changed nothing. {issue}");
            }

            string changed = template != null ? subject : GetEffectiveName(level, GetEditCatalog(databaseGuid), piece);
            return ChangeCells(databaseGuid, level, scope, piece, template, drawn, grown, "Add Piece Cells",
                $"Added {grown.Count - before} cell(s) to {changed}.");
        }

        private LevelPieceEdit ChangeCells(string databaseGuid, LevelData level, string scope, LevelNamedPiece piece,
            LevelShapeChoice template, int drawn, IReadOnlyList<Vector2Int> cells, string undoName, string message)
        {
            if (template != null)
            {
                return ChangeTemplateCells(databaseGuid, level, drawn, template, cells, undoName, message);
            }

            // Only the piece's recorded blocks follow, so neither a template's blocks nor another level piece's ever
            // move when the drawing passes through their shape (DEC-193).
            ShapeKey previous = level.GetBlockAt(drawn).Key;
            string name = GetEffectiveName(level, GetEditCatalog(databaseGuid), piece);
            if (!TrySetDrawnCells(level, drawn, cells, name, piece.MemberIds, out LevelData result, out string issue))
            {
                return LevelPieceEdit.CommandRefused(issue);
            }

            // The level's name for the piece follows its blocks to the new footprint.
            ShapeKey key = result.GetBlockAt(drawn).Key;
            result = LevelPieceNames.CarryName(result, previous, IsTemplateKey(databaseGuid, key) ? default : key);
            return LevelPieceEdit.Change(undoName, result, message, scope, piece);
        }

        private LevelPieceEdit ChangeTemplateCells(string databaseGuid, LevelData level, int drawn,
            LevelShapeChoice template, IReadOnlyList<Vector2Int> cells, string undoName, string message)
        {
            IReadOnlyList<Vector2Int> saved = GetSavedFootprint(databaseGuid, template.TemplateKey, template.DisplayName)
                                              ?? template.Cells;
            LevelTemplateEdit existing = m_draftController.Draft.FindTemplateEdit(template.TemplateKey, template.DisplayName);
            bool continuing = existing != null && IsSameFootprint(existing.BaseCells, saved);
            // An edit an undo brought back from before a committed Save Level keeps the template's old key; the new
            // edit replaces it rather than standing beside it.
            ShapeKey replaced = existing != null && !continuing && existing.TemplateKey != template.TemplateKey
                ? existing.TemplateKey
                : default;
            // The template's blocks: the pending edit's, or at its first stroke the blocks keyed like the template now,
            // so a block that merely shares a key the edit passes through never follows (DEC-193).
            LevelTemplateBlockOrigin[] originals = continuing ? null : LevelTemplateEdit.CaptureOriginals(level, saved);
            ShapeKey savedKey = ShapeKey.TryCreate(saved, out ShapeKey key) ? key : default;
            List<string> members = continuing ? existing.GetMemberIds() : LevelBlockQueries.GetBlockIds(level, savedKey);
            if (!LevelTemplateCommands.TryEdit(level, drawn, cells, template.DisplayName, template.Cells, members,
                    out LevelData result, out Vector2Int[] libraryCells, out string issue))
            {
                return LevelPieceEdit.CommandRefused(issue);
            }

            LevelTemplateEdit edit = (continuing
                    ? existing.WithCells(libraryCells)
                    : new LevelTemplateEdit(template.TemplateKey, template.DisplayName, saved, libraryCells, originals))
                .WithMember(level.GetBlockAt(drawn).Id);
            string status = message + (edit.IsNoChange
                ? $" {template.DisplayName} is back to its saved footprint, turned or mirrored, so nothing is pending."
                : $" Every {template.DisplayName} block in this level follows; Save Level writes the new footprint " +
                  "to the other levels and the piece library.") + GetOverlapNote(result);
            return LevelPieceEdit.Change(undoName, result, status, templateEdit: edit, removedTemplateKey: replaced);
        }

        private static LevelPieceEdit PlaceFirstCells(LevelData level, string scope, LevelNamedPiece piece,
            IReadOnlyList<Vector2Int> cells, string colorId)
        {
            if (!LevelCustomPiece.TryValidate(cells, out string issue))
            {
                return LevelPieceEdit.Status("The stroke made no block. " + issue);
            }

            Vector2Int origin = LevelShapeThumbnail.GetBounds(cells).min;
            Vector2Int[] footprint = LevelCustomPiece.Normalize(cells);
            if (!LevelEditCommands.TryPlaceBlock(level, footprint, colorId, origin, out LevelData result, out issue))
            {
                return LevelPieceEdit.CommandRefused(issue);
            }

            piece.BlockId = result.GetBlockAt(result.BlockCount - 1).Id;
            piece.SetMembers(new[] { piece.BlockId });
            return LevelPieceEdit.Change("Draw Block", result,
                $"Drew {piece.DisplayName} as block {piece.BlockId}. Click next to it to grow it; it belongs to this " +
                "level, and Save as template in the Piece Editor adds it to the piece library.", scope, piece);
        }

        internal LevelPieceEdit RemoveCell(LevelPieceSource source, LevelEditorToolState tool, Vector2Int cell)
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null || tool.Tool != LevelEditorTool.Pen)
            {
                return null;
            }

            if (!tool.IsFreehandArmed)
            {
                int edited = IsPenEditing(tool, out string blockId) ? FindBlockIndex(level, blockId) : -1;
                LevelShapeChoice template = edited >= 0 ? GetEditedTemplate(source.Guid, level.GetBlockAt(edited)) : null;
                if (template != null)
                {
                    return RemoveBlockCell(databaseGuid: source.Guid, level: level, scope: null, piece: null,
                        template: template, drawn: edited, cell: cell, subject: blockId, ownPiece: false);
                }

                if (!TryGetEditPiece(source, tool, level, out string editScope, out LevelNamedPiece editPiece,
                        out int editDrawn, out bool created))
                {
                    return null;
                }

                LevelPieceEdit edit = RemoveBlockCell(databaseGuid: source.Guid, level: level, scope: editScope,
                    piece: editPiece, template: null, drawn: editDrawn, cell: cell,
                    subject: created
                        ? level.GetBlockAt(editDrawn).Id
                        : GetEffectiveName(level, GetEditCatalog(source.Guid), editPiece),
                    ownPiece: !created);
                ForgetUnusedEditPiece(editPiece, created, edit);
                return edit;
            }

            string scope = source.GetScope(level);
            LevelNamedPiece piece = m_pieceSession.DrawingPiece;
            int drawn = piece?.BlockId != null && string.Equals(piece.Scope, scope, StringComparison.Ordinal)
                ? FindBlockIndex(level, piece.BlockId)
                : -1;
            return drawn >= 0
                ? RemoveBlockCell(databaseGuid: source.Guid, level: level, scope: scope, piece: piece, template: null,
                    drawn: drawn, cell: cell, subject: GetEffectiveName(level, GetEditCatalog(source.Guid), piece),
                    ownPiece: true)
                : null;
        }

        private LevelPieceEdit RemoveBlockCell(string databaseGuid, LevelData level, string scope, LevelNamedPiece piece,
            LevelShapeChoice template, int drawn, Vector2Int cell, string subject, bool ownPiece)
        {
            LevelBlockData block = level.GetBlockAt(drawn);
            List<Vector2Int> remaining = GetBoardCells(block);
            if (!remaining.Remove(cell))
            {
                return null;
            }

            if (remaining.Count == 0)
            {
                if (!LevelEditCommands.TryRemoveBlock(level, drawn, out LevelData result, out string issue))
                {
                    return LevelPieceEdit.CommandRefused(issue);
                }

                string status = !ownPiece
                    ? $"Removed block {block.Id}."
                    : LevelBlockQueries.HasBlock(result, block.Key)
                        ? $"Removed block {block.Id}; other blocks still use {subject}."
                        : $"Deleted {subject}: no block uses it any more. Undo brings it back.";
                return LevelPieceEdit.Change("Remove Block", result, status, removedBlockIndex: drawn);
            }

            if (!LevelCustomPiece.IsConnected(remaining))
            {
                return LevelPieceEdit.Status($"Taking ({cell.x},{cell.y}) off would split {subject}.");
            }

            string changed = template != null ? subject : GetEffectiveName(level, GetEditCatalog(databaseGuid), piece);
            return ChangeCells(databaseGuid, level, scope, piece, template, drawn, remaining, "Remove Piece Cell",
                $"Took ({cell.x},{cell.y}) off {changed}.");
        }

        internal LevelPieceRequest UpdateDrawnPiece(string databaseGuid, string scope, LevelNamedPiece piece)
        {
            LevelData level = m_draftController.Snapshot();
            int drawn = FindBlockIndex(level, piece.BlockId);
            Vector2Int[] footprint = LevelCustomPiece.Normalize(LevelRecordCopy.GetCells(level.GetBlockAt(drawn)));
            piece.SetCells(footprint);
            return new LevelPieceRequest(scope, piece.Token, footprint,
                GetEffectiveName(level, GetEditCatalog(databaseGuid), piece));
        }

        /// <summary>
        /// The banner's Keep library footprint: the saved footprints stop waiting for the piece library, which keeps
        /// its own. The saved levels' blocks of each are level pieces now; the session remembers them, so Re-stamp in
        /// each level gives them the library footprint again.
        /// </summary>
        internal string KeepLibraryFootprints(string databaseGuid)
        {
            List<LevelTemplateEdit> saved = m_pieceSession.GetPendingTemplates(databaseGuid);
            if (saved.Count == 0 || (m_draftController.Draft != null && m_draftController.Draft.TemplateEdits.Count > 0))
            {
                return null;
            }

            for (int index = 0; index < saved.Count; index++)
            {
                LevelTemplateEdit edit = saved[index];
                m_pieceSession.ForgetPendingTemplate(databaseGuid, edit.TemplateKey);
                int record = m_shapeCatalog.IndexOfTemplate(edit.TemplateKey, edit.DisplayName);
                if (record >= 0)
                {
                    LevelShapeChoice template = m_shapeCatalog.Get(record);
                    m_pieceSession.RememberFootprint(template.DisplayName, edit.Cells, template.Cells);
                }
            }

            return $"The piece library keeps its footprint of {JoinTemplateNames(saved)}. Blocks saved with the " +
                   "new footprint are level pieces now; Re-stamp gives them the library footprint in each level.";
        }

        internal LevelPieceEdit DiscardTemplateEdits(string databaseGuid)
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null || m_draftController.Draft.TemplateEdits.Count == 0)
            {
                return null;
            }

            LevelData current = level;
            for (int index = 0; index < m_draftController.Draft.TemplateEdits.Count; index++)
            {
                LevelTemplateEdit toSaved = GetDiscardEdit(databaseGuid, m_draftController.Draft.TemplateEdits[index]);
                if (!LevelTemplateCommands.TryDiscard(current, toSaved, out LevelData next, out string issue))
                {
                    return LevelPieceEdit.Refusal("Discard template change changed nothing. " + issue);
                }

                current = next;
            }

            string names = JoinTemplateNames(m_draftController.Draft.TemplateEdits);
            return LevelPieceEdit.Change("Discard Template Change", current,
                $"Discarded the change to {names}. Undo brings it back." + GetOverlapNote(current),
                clearTemplateEdits: true);
        }

        private LevelTemplateEdit GetDiscardEdit(string databaseGuid, LevelTemplateEdit edit)
        {
            IReadOnlyList<Vector2Int> saved = GetSavedFootprint(databaseGuid, edit.TemplateKey, edit.DisplayName);
            return saved == null || IsSameFootprint(saved, edit.BaseCells) ? edit : edit.WithBase(saved);
        }

        internal LevelPieceEdit MakeSeparatePiece(LevelPieceSource source, LevelEditorToolState tool)
        {
            LevelData level = m_draftController.Snapshot();
            string scope = source.GetScope(level);
            int index = level != null && IsPenEditing(tool, out string blockId) ? FindBlockIndex(level, blockId) : -1;
            LevelBlockData block = index >= 0 ? level.GetBlockAt(index) : null;
            LevelTemplateEdit edit = block != null ? FindDraftEdit(block.Key) : null;
            if (scope == null || edit == null)
            {
                return LevelPieceEdit.Hint(
                    "Click the changed template block to keep with the Pen, then press Make a separate piece.");
            }

            LevelNamedPiece piece = AddNewNamedPiece(source, level, scope);
            if (!LevelTemplateCommands.TryDiscard(level, GetDiscardEdit(source.Guid, edit), out LevelData result,
                    out string issue, block.Id))
            {
                m_pieceSession.RemoveNamedPiece(piece);
                return LevelPieceEdit.Refusal("Make a separate piece changed nothing. " + issue);
            }

            piece.SetCells(LevelCustomPiece.Normalize(LevelRecordCopy.GetCells(block)));
            piece.BlockId = block.Id;
            piece.WasPlaced = true;
            // The separate piece has this one block; the Pen keeps editing it as that level piece.
            m_penEditsLevelPiece = true;
            m_penMemberIds.Clear();
            m_penMemberIds.Add(block.Id);
            piece.SetMembers(m_penMemberIds);
            return LevelPieceEdit.Change("Make Separate Piece", result,
                $"{block.Id} is now its own piece {piece.DisplayName}; the other {edit.DisplayName} blocks are back " +
                "where they were. Undo brings the template change back.", scope, piece,
                removedTemplateKey: edit.TemplateKey);
        }

        // The draft's pending template edit whose footprint forms this key, or null.
        private LevelTemplateEdit FindDraftEdit(ShapeKey key)
        {
            IReadOnlyList<LevelTemplateEdit> edits = m_draftController.Draft.TemplateEdits;
            for (int index = 0; key.IsValid && index < edits.Count; index++)
            {
                if (edits[index].Key == key)
                {
                    return edits[index];
                }
            }

            return null;
        }


        internal static string GetOverlapNote(LevelData level)
        {
            string blocks = LevelBlockOverlap.Describe(level);
            return blocks != null
                ? $" Blocks {blocks} overlap: move them apart. Save Level stays off until they do."
                : string.Empty;
        }

        internal static string JoinTemplateNames(IReadOnlyList<LevelTemplateEdit> edits)
        {
            string[] names = new string[edits.Count];
            for (int index = 0; index < names.Length; index++)
            {
                names[index] = edits[index].DisplayName;
            }

            return string.Join(", ", names);
        }


        internal LevelPieceEditorEdit PlanEditorChange(LevelPieceSource source, LevelPieceRequest request)
        {
            LevelData level = m_draftController.Snapshot();
            if (request == null || level == null)
            {
                return null;
            }

            string scope = source.GetScope(level);
            if (!string.Equals(request.LevelId, scope, StringComparison.Ordinal))
            {
                return LevelPieceEditorEdit.Rejected(
                    $"This piece belongs to {request.LevelId}; open that level to edit it.", false, false);
            }

            m_lastMirror = request;
            LevelNamedPiece piece = m_pieceSession.FindNamedPiece(request.PieceId);
            if (piece == null || !string.Equals(piece.Scope, scope, StringComparison.Ordinal))
            {
                return LevelPieceEditorEdit.Rejected(
                    "That piece is no longer a level piece of this level; click its tile to show it in the Piece " +
                    "Editor again.", false, false);
            }

            Vector2Int[] cells = new Vector2Int[request.Cells.Count];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = request.Cells[index];
            }

            return PlanNamedPieceChange(source.Guid, level, piece, cells, request.Name);
        }

        // A Piece Editor change of a level piece: new cells re-stamp every block of its current key in its own
        // orientation, a new name goes into the level's pieces table (DEC-193), and the level's name follows the key.
        private LevelPieceEditorEdit PlanNamedPieceChange(string databaseGuid, LevelData level, LevelNamedPiece piece,
            IReadOnlyList<Vector2Int> cells, string name)
        {
            LevelShapeCatalog catalog = GetEditCatalog(databaseGuid);
            string current = GetEffectiveName(level, catalog, piece);
            bool renamed = !string.IsNullOrWhiteSpace(name) && !string.Equals(name, current, StringComparison.Ordinal);
            if (renamed && name.Length > LevelPieceName.MaxNameLength)
            {
                return LevelPieceEditorEdit.Rejected(
                    $"{current} changed nothing: a piece name has 1 to {LevelPieceName.MaxNameLength} characters.",
                    true, true);
            }

            if (!LevelCustomPiece.TryValidate(cells, out string issue))
            {
                return LevelPieceEditorEdit.Rejected($"{current} changed nothing: {issue}", true, true);
            }

            Vector2Int[] footprint = LevelCustomPiece.Normalize(cells);
            ShapeKey previous = piece.Key;
            ShapeKey key = ShapeKey.TryCreate(footprint, out ShapeKey footprintKey) ? footprintKey : default;
            List<string> changedIds = new List<string>();
            LevelData result = level;
            if (previous.IsValid && previous != key)
            {
                if (!TryRestampPiece(level, piece, previous, footprint, current, changedIds, out result, out issue))
                {
                    return LevelPieceEditorEdit.Rejected($"{current} changed nothing. {issue}", true, true);
                }

                result = LevelPieceNames.CarryName(result, previous, catalog.IndexOf(key) >= 0 ? default : key);
            }

            if (renamed)
            {
                piece.DisplayName = name;
                if (LevelBlockQueries.HasBlock(result, key) && catalog.IndexOf(key) < 0)
                {
                    result = LevelPieceNames.SetName(result, key, name);
                }
            }

            LevelShapeChoice template = catalog.Find(key);
            string status = (renamed ? $"Renamed the piece {name}." : $"Updated {current}.")
                            + (template != null
                                ? $" It is the template {template.DisplayName} now; the Pen places it as that template."
                                : string.Empty)
                            + (changedIds.Count > 0
                                ? $" Re-stamped {changedIds.Count} block(s): {string.Join(", ", changedIds)}."
                                : string.Empty);
            return LevelPieceEditorEdit.Named(level, ReferenceEquals(result, level) ? null : result, piece, footprint,
                previous, status + GetOverlapNote(result));
        }

        // The piece's recorded blocks with its previous key take the new footprint. A block that merely shares that
        // key, a template's or another level piece's, is not the piece's and stays (DEC-193).
        private static bool TryRestampPiece(LevelData level, LevelNamedPiece piece, ShapeKey previous,
            IReadOnlyList<Vector2Int> footprint, string pieceName, List<string> changedIds, out LevelData result,
            out string issue)
        {
            if (piece.MemberIds.Count == 0)
            {
                result = level;
                issue = null;
                return true;
            }

            return LevelShapeRestamp.TryRestamp(level, previous, piece.Cells, footprint, pieceName, out result, out _,
                out issue, changedIds, piece.MemberIds);
        }

        /// <summary>
        /// Called after the draft's joined Undo and refresh, while the Piece Editor's group is still open: the piece
        /// takes its new footprint, and the piece to arm when the designer had armed it: the template it now is, or the
        /// level piece in its new orientation.
        /// </summary>
        internal LevelShapeChoice CompleteEditorChange(string databaseGuid, LevelPieceEditorEdit edit)
        {
            edit.NamedPiece.SetCells(edit.Cells);
            LevelShapeCatalog catalog = GetEditCatalog(databaseGuid);
            LevelShapeChoice template = catalog.Find(edit.NamedPiece.Key);
            return template != null
                ? new LevelShapeChoice(template.DisplayName, edit.Cells, template.TemplateKey)
                : new LevelShapeChoice(GetEffectiveName(m_draftController.Snapshot(), catalog, edit.NamedPiece),
                    edit.Cells);
        }

        /// <summary>
        /// The Re-stamp offer's banner line for the open level: its blocks with an earlier footprint of a template
        /// (LevelPieceSession.FootprintChanges), or null when none has one.
        /// </summary>
        internal string DescribeRestampOffer(string databaseGuid, LevelData level)
        {
            List<LevelFootprintChange> changes = GetRestampChanges(level, GetEditCatalog(databaseGuid));
            if (changes.Count == 0)
            {
                return null;
            }

            int blocks = 0;
            List<string> names = new List<string>();
            for (int index = 0; index < changes.Count; index++)
            {
                blocks += CountBlocksUsing(level, changes[index].FromKey);
                if (!names.Contains(changes[index].Name))
                {
                    names.Add(changes[index].Name);
                }
            }

            return $"{blocks} block(s) in this level still have an earlier footprint of template " +
                   $"{string.Join(", ", names)}. Nothing changes until you press Re-stamp.";
        }

        internal LevelPieceEdit RestampChangedPieces(string databaseGuid, out IReadOnlyList<ShapeKey> changedKeys)
        {
            changedKeys = Array.Empty<ShapeKey>();
            LevelData level = m_draftController.Snapshot();
            if (level == null)
            {
                return null;
            }

            List<LevelFootprintChange> changes = GetRestampChanges(level, GetEditCatalog(databaseGuid));
            LevelData current = level;
            int total = 0;
            List<string> refusals = null;
            List<ShapeKey> keys = new List<ShapeKey>();
            for (int index = 0; index < changes.Count; index++)
            {
                LevelFootprintChange change = changes[index];
                if (LevelShapeRestamp.TryRestamp(current, change.FromKey, change.FromCells, change.ToCells, change.Name,
                        out LevelData next, out int count, out string issue))
                {
                    current = next;
                    total += count;
                    keys.Add(change.FromKey);
                }
                else
                {
                    refusals ??= new List<string>();
                    refusals.Add(issue);
                }
            }

            if (refusals != null)
            {
                return LevelPieceEdit.Refusal("Re-stamp changed nothing. " + string.Join(" ", refusals));
            }

            if (total == 0)
            {
                return null;
            }

            changedKeys = keys;
            return LevelPieceEdit.Change("Re-stamp Blocks", current,
                $"Re-stamped {total} block(s) with the template's footprint. Undo restores them." +
                GetOverlapNote(current));
        }

        // The remembered footprint changes the open level's blocks can be re-stamped from: a block has the earlier
        // footprint, the template still has the newer one, and no other template holds the earlier one now.
        private List<LevelFootprintChange> GetRestampChanges(LevelData level, LevelShapeCatalog catalog)
        {
            List<LevelFootprintChange> changes = new List<LevelFootprintChange>();
            for (int index = 0; level != null && index < m_pieceSession.FootprintChanges.Count; index++)
            {
                LevelFootprintChange change = m_pieceSession.FootprintChanges[index];
                if (LevelBlockQueries.HasBlock(level, change.FromKey) && catalog.IndexOf(change.ToKey) >= 0
                    && catalog.IndexOf(change.FromKey) < 0)
                {
                    changes.Add(change);
                }
            }

            return changes;
        }

        internal void ForgetUnusedFootprints(IReadOnlyList<ShapeKey> keys, IReadOnlyList<LevelData> levels)
        {
            for (int index = 0; index < keys.Count; index++)
            {
                m_pieceSession.ForgetUnlessUsed(keys[index], levels);
            }
        }

        internal LevelPieceEdit DeleteCustomPiece(string scope, LevelShapeChoice piece, int uses)
        {
            LevelNamedPiece drawing = m_pieceSession.DrawingPiece;
            if (drawing != null && string.Equals(drawing.Scope, scope, StringComparison.Ordinal)
                && drawing.Key == piece.Key)
            {
                m_pieceSession.SetDrawingPiece(null);
            }

            if (uses == 0)
            {
                for (LevelNamedPiece named = m_pieceSession.FindNamedPieceForKey(scope, piece.Key);
                     named != null;
                     named = m_pieceSession.FindNamedPieceForKey(scope, piece.Key))
                {
                    m_pieceSession.RemoveNamedPiece(named);
                }

                return LevelPieceEdit.Status($"Deleted {piece.DisplayName}.");
            }

            LevelData level = m_draftController.Snapshot();
            LevelData result = level;
            for (int index = level.BlockCount - 1; index >= 0; index--)
            {
                if (level.GetBlockAt(index)?.Key == piece.Key)
                {
                    if (!LevelEditCommands.TryRemoveBlock(result, index, out LevelData next, out string issue))
                    {
                        return LevelPieceEdit.CommandRefused(issue);
                    }

                    result = next;
                }
            }

            return LevelPieceEdit.Change("Delete Level Piece", result,
                $"Deleted {piece.DisplayName} and its {uses} block(s). Undo brings them back.");
        }

        // The tracked pieces of this scope take their drawn block's footprint, and a piece a block of the level uses is
        // placed from now on.
        private void SyncNamedPieces(string scope, LevelData level)
        {
            for (int index = 0; index < m_pieceSession.NamedPieces.Count; index++)
            {
                LevelNamedPiece piece = m_pieceSession.NamedPieces[index];
                if (!string.Equals(piece.Scope, scope, StringComparison.Ordinal))
                {
                    continue;
                }

                int drawn = piece.BlockId != null ? FindBlockIndex(level, piece.BlockId) : -1;
                LevelBlockData block = drawn >= 0 ? level.GetBlockAt(drawn) : null;
                if (block != null && block.CellCount > 0)
                {
                    piece.SetCells(LevelCustomPiece.Normalize(LevelRecordCopy.GetCells(block)));
                }

                // A placed piece has a tile only while a block uses it, so erasing its last block deletes it from the
                // list and an undo brings it back (user request of 2026-09-26).
                if (LevelBlockQueries.HasBlock(level, piece.Key))
                {
                    piece.WasPlaced = true;
                }
            }
        }

        // The open level's piece names as Save Level would write them now.
        private LevelPieceName[] ResolveNames(string scope, LevelData level, LevelShapeCatalog catalog)
        {
            return LevelPieceNames.Resolve(level, m_otherLevelData, catalog,
                key => m_pieceSession.FindNamedPieceForKey(scope, key)?.DisplayName, GetReservedNames());
        }

        // A tracked piece's name: a template's for a template key, else the level's own or another level's name for
        // its key, else the name this session gave it.
        private string GetEffectiveName(LevelData level, LevelShapeCatalog catalog, LevelNamedPiece piece)
        {
            ShapeKey key = piece.Key;
            LevelShapeChoice template = catalog.Find(key);
            if (template != null)
            {
                return template.DisplayName;
            }

            return (key.IsValid ? LevelPieceNames.Find(level, key) ?? LevelPieceNames.Find(m_otherLevelData, key) : null)
                   ?? piece.DisplayName;
        }

        // The names this session gave its tracked pieces, which a new default name must not repeat.
        private IEnumerable<string> GetReservedNames()
        {
            for (int index = 0; index < m_pieceSession.NamedPieces.Count; index++)
            {
                string name = m_pieceSession.NamedPieces[index].DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return name;
                }
            }
        }

        // A tile's thumbnail cells: the tracked piece's footprint when one draws a block of the key, else the first
        // block's.
        private IReadOnlyList<Vector2Int> GetTileCells(string scope, LevelData level, ShapeKey key)
        {
            LevelNamedPiece piece = m_pieceSession.FindNamedPieceForKey(scope, key);
            return piece != null && piece.BlockId != null && FindBlockIndex(level, piece.BlockId) >= 0
                ? piece.Cells
                : GetFirstBlockCells(level, key);
        }

        // The first block of the key, its cells from (0, 0).
        private static IReadOnlyList<Vector2Int> GetFirstBlockCells(LevelData level, ShapeKey key)
        {
            int index = LevelBlockQueries.FindBlock(level, key);
            return index >= 0
                ? LevelCustomPiece.Normalize(LevelRecordCopy.GetCells(level.GetBlockAt(index)))
                : key.Decode();
        }

        private static int IndexOfKey(List<LevelShapeChoice> choices, ShapeKey key)
        {
            for (int index = 0; key.IsValid && index < choices.Count; index++)
            {
                if (choices[index].Key == key)
                {
                    return index;
                }
            }

            return -1;
        }


        private sealed class CatalogOverlay
        {
            private LevelShapeCatalog m_library;
            private string m_key;
            private LevelShapeCatalog m_result;

            public LevelShapeCatalog Get(LevelShapeCatalog library, IReadOnlyList<LevelTemplateEdit> edits)
            {
                string key = GetKey(edits);
                if (m_result == null || !ReferenceEquals(library, m_library)
                    || !string.Equals(key, m_key, StringComparison.Ordinal))
                {
                    m_library = library;
                    m_key = key;
                    m_result = LevelTemplateCommands.Overlay(library, edits);
                }

                return m_result;
            }

            /// <summary>The footprints as text: equal text, equal overlay.</summary>
            public static string GetKey(IReadOnlyList<LevelTemplateEdit> edits)
            {
                if (edits == null || edits.Count == 0)
                {
                    return string.Empty;
                }

                string[] parts = new string[edits.Count];
                for (int index = 0; index < parts.Length; index++)
                {
                    parts[index] = JsonUtility.ToJson(edits[index]) + "|";
                }

                return string.Concat(parts);
            }
        }

    }

    /// <summary>A Piece Editor request's plan and completion data; completion follows the draft's joined Undo.</summary>
    internal sealed class LevelPieceEditorEdit
    {
        internal bool Accepted { get; private set; }
        internal LevelData Before { get; private set; }
        internal LevelData After { get; private set; }
        internal string Message { get; private set; }
        internal bool IsRefusal { get; private set; }
        internal bool RefreshNamedViews { get; private set; }
        internal LevelNamedPiece NamedPiece { get; private set; }
        internal Vector2Int[] Cells { get; private set; }
        /// <summary>The piece's key before the change, so an armed copy of the piece is armed again.</summary>
        internal ShapeKey PreviousKey { get; private set; }

        private LevelPieceEditorEdit()
        {
        }

        internal static LevelPieceEditorEdit Rejected(string message, bool isRefusal = true, bool refreshNamed = false)
        {
            return new LevelPieceEditorEdit
            {
                Message = message, IsRefusal = isRefusal, RefreshNamedViews = refreshNamed
            };
        }

        internal static LevelPieceEditorEdit Named(LevelData before, LevelData after, LevelNamedPiece piece,
            Vector2Int[] cells, ShapeKey previousKey, string message)
        {
            return new LevelPieceEditorEdit
            {
                Accepted = true, Before = before, After = after, NamedPiece = piece, Cells = cells,
                PreviousKey = previousKey, RefreshNamedViews = true, Message = message
            };
        }
    }


    internal enum LevelPieceFeedback
    {
        Status,
        Hint,
        Refusal,
        CommandRefused
    }

    /// <summary>A planned canvas edit. The window applies Level and metadata together, then selects/mirrors Piece.</summary>
    internal sealed class LevelPieceEdit
    {
        internal LevelData Level { get; private set; }
        internal string UndoName { get; private set; }
        internal string Message { get; private set; }
        internal LevelPieceFeedback Feedback { get; private set; }
        internal string Scope { get; private set; }
        internal LevelNamedPiece Piece { get; private set; }
        internal int RemovedBlockIndex { get; private set; } = -1;
        private LevelTemplateEdit m_templateEdit;
        private bool m_clearTemplateEdits;
        private ShapeKey m_removedTemplateKey;

        private LevelPieceEdit()
        {
        }

        internal static LevelPieceEdit Change(string undoName, LevelData level, string message, string scope = null,
            LevelNamedPiece piece = null, LevelTemplateEdit templateEdit = null, bool clearTemplateEdits = false,
            ShapeKey removedTemplateKey = default, int removedBlockIndex = -1)
        {
            return new LevelPieceEdit
            {
                UndoName = undoName,
                Level = level,
                Message = message,
                Scope = scope,
                Piece = piece,
                m_templateEdit = templateEdit,
                m_clearTemplateEdits = clearTemplateEdits,
                m_removedTemplateKey = removedTemplateKey,
                RemovedBlockIndex = removedBlockIndex
            };
        }

        internal static LevelPieceEdit Status(string message)
        {
            return new LevelPieceEdit { Message = message };
        }

        internal static LevelPieceEdit Hint(string message)
        {
            return new LevelPieceEdit { Message = message, Feedback = LevelPieceFeedback.Hint };
        }

        internal static LevelPieceEdit Refusal(string message)
        {
            return new LevelPieceEdit { Message = message, Feedback = LevelPieceFeedback.Refusal };
        }

        internal static LevelPieceEdit CommandRefused(string message)
        {
            return new LevelPieceEdit { Message = message, Feedback = LevelPieceFeedback.CommandRefused };
        }

        internal void ApplyMetadata(LevelDraft draft)
        {
            if (m_templateEdit != null)
            {
                if (m_templateEdit.IsNoChange)
                {
                    draft.RemoveTemplateEdit(m_templateEdit.TemplateKey);
                }
                else
                {
                    draft.SetTemplateEdit(m_templateEdit);
                }
            }

            if (m_clearTemplateEdits)
            {
                draft.SetTemplateEdits(null);
            }

            if (m_removedTemplateKey.IsValid)
            {
                draft.RemoveTemplateEdit(m_removedTemplateKey);
            }
        }
    }


    /// <summary>A call's selected-source identity. The window remains its authority; caches retain only cache keys.</summary>
    internal readonly struct LevelPieceSource
    {
        internal LevelCatalog Levels { get; }
        internal string Guid { get; }

        /// <summary>The loaded database's content hash (DEC-196); the same instance while the same save is loaded.</summary>
        internal string SourceHash { get; }

        internal int SelectedIndex { get; }
        internal bool IsValid { get; }

        internal LevelPieceSource(LevelCatalog levels, string guid, string sourceHash, int selectedIndex, bool isValid)
        {
            Levels = levels;
            Guid = guid;
            SourceHash = sourceHash;
            SelectedIndex = selectedIndex;
            IsValid = isValid;
        }

        internal string GetScope(LevelData level)
        {
            return string.IsNullOrWhiteSpace(level?.Id) ? null : LevelPieceSession.GetLevelScope(Guid, level.Id);
        }

        internal List<LevelData> GetLevels(LevelData draft)
        {
            List<LevelData> levels = new List<LevelData>();
            if (draft == null)
            {
                return levels;
            }

            levels.Add(draft);
            for (int index = 0; IsValid && Levels != null && index < Levels.Count; index++)
            {
                if (index != SelectedIndex)
                {
                    levels.Add(Levels.GetAt(index));
                }
            }

            return levels;
        }

        internal int CountBlocksUsing(ShapeKey key, LevelData draft)
        {
            if (!IsValid || Levels == null)
            {
                return -1;
            }

            int count = 0;
            for (int index = 0; index < Levels.Count; index++)
            {
                LevelData level = index == SelectedIndex && draft != null ? draft : Levels.GetAt(index);
                count += LevelPieceEditController.CountBlocksUsing(level, key);
            }

            return count;
        }
    }
}
