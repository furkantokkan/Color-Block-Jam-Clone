using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Editor.Search;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Paints, previews and saves one piece footprint of the piece library or of one level. The window keeps the draft,
    /// its undo history and the level-piece link, and composes the paint grid (<see cref="PieceGridView"/>) and the 3D
    /// preview (<see cref="PiecePreviewRenderer"/>) (DEC-125, DEC-126). The library is the one fixed JSON (DEC-195).
    /// </summary>
    public sealed class PieceEditorWindow : EditorWindow
    {
        private const float k_previewSize = 320f;
        private const int k_maxCells = PieceGridDraft.MaxSize * PieceGridDraft.MaxSize;
        private const string k_introText = "Paint around the center; empty margins are dropped on save.";
        private const string k_levelPieceText = "Level piece: edits reach the level live.";
        private const string k_shortcutText = "Ctrl+Z undo · Ctrl+Shift+Z redo";
        // The Level Editor's Tools choice, shared by every Piece Editor (SetLevelTool). Editor-only view state: a
        // domain reload resets it to Select together with the Level Editor's own tool state.
        private static LevelEditorTool s_levelTool = LevelEditorTool.Select;
        private const string k_saveTooltip = "Save the name and footprint to the piece library JSON.";
        private const string k_templateTooltip = "Add the level piece to the piece library JSON as a new template.";
        [SerializeField] private PieceGridDraft m_draft = new PieceGridDraft();
        [SerializeField] private PieceGridUndoState m_gridUndo;
        [SerializeField] private PieceGridDraft m_savedGrid = new PieceGridDraft();
        [SerializeField] private string m_savedName = "New Piece";
        [SerializeField] private string m_displayName = "New Piece";
        [SerializeField] private int m_selected = -1;
        [SerializeField] private string m_databasePath;
        [SerializeField] private bool m_dirty;
        [SerializeField] private PaletteColor m_previewColor = PaletteColor.Purple;
        // Level-piece mode edits a piece that belongs to one level. Serialized, so the mode, the request and the draft
        // survive a domain reload.
        [SerializeField] private bool m_levelPieceMode;
        [SerializeField] private LevelPieceRequest m_levelPiece;
        // The ShapeKey code of a library template requested before the window built its GUI, 0 for none; the first
        // database load shows it. Stored as its code because Unity does not serialize ShapeKey.
        [SerializeField] private int m_pendingLibraryKey;
        private readonly List<Vector2Int> m_cells = new List<Vector2Int>(k_maxCells);
        private readonly List<BlockShapeLibrary.Shape> m_shapes = new List<BlockShapeLibrary.Shape>();
        private string m_loadedJson;
        private ObjectField m_sourceField;
        private DropdownField m_libraryField;
        private ChoiceStepper m_libraryStepper;
        private BlockShapeLibrary m_library;
        private TextField m_nameField;
        private SliderInt m_sizeField;
        private Button m_deleteButton;
        private Button m_saveButton;
        private Button m_saveTemplateButton;
        private GamePalette m_palette;
        private HelpBox m_status;
        private string m_statusMessage = string.Empty;
        private PieceGridView m_grid;
        private Image m_image;
        // Created on the first preview; OnDisable disposes it.
        private PiecePreviewRenderer m_previewRenderer;
        // A Name edit the level has not received yet; committing the edit sends it.
        private bool m_nameEdited;

        /// <summary>
        /// Editor-tool event raised in level-piece mode for each change that reaches the level: the end of a stroke
        /// that changed cells, a rotate or mirror click, a grid undo or redo, and a committed name edit. It carries normalized cells
        /// and the shown name, never an empty grid or a blank name. It is raised before the stroke's undo group
        /// closes, so a listener's undo record joins that group. Each subscriber names its stop point and
        /// unsubscribes there.
        /// </summary>
        internal static event Action<LevelPieceRequest> LevelPieceChanged;

        /// <summary>
        /// Runs before "Delete selected piece" of the piece library, with the template's key. When it
        /// handled the delete (deleted, cancelled, refused or failed) it returns the status to show, which says what
        /// happened; null keeps this window's own question and delete. The Level Editor's route sets it (DEC-126); it
        /// is a plain delegate, so BoardKit names no Level Editor type.
        /// </summary>
        internal static Func<ShapeKey, string> DeleteLibraryPiece { get; set; }

        /// <summary>Opens or focuses the Piece Editor on the piece library template with this key.</summary>
        internal static void ShowLibraryPiece(ShapeKey key) =>
            GetWindow<PieceEditorWindow>("Piece Editor").LoadLibraryPiece(key);

        /// <summary>Opens or focuses the Piece Editor in level-piece mode on a piece that belongs to one level.</summary>
        internal static void ShowLevelPiece(LevelPieceRequest request) => ShowLevelPiece(request, true);

        /// <summary>
        /// Shows <paramref name="request"/> in level-piece mode. With <paramref name="focus"/> false an open Piece
        /// Editor updates without taking the focus, so drawing in the Level Editor continues; a closed one opens.
        /// </summary>
        internal static void ShowLevelPiece(LevelPieceRequest request, bool focus) =>
            GetWindow<PieceEditorWindow>("Piece Editor", focus).LoadLevelPiece(request);

        /// <summary>
        /// The Level Editor's Tools choice also drives the Piece Editor grid: Pen paints, Eraser erases, the rotate
        /// and mirror tools turn the whole piece on a click, and Select keeps left-paint / right-erase.
        /// </summary>
        internal static void SetLevelTool(LevelEditorTool tool)
        {
            if (s_levelTool == tool) return;
            s_levelTool = tool;
            if (!HasOpenInstances<PieceEditorWindow>()) return;
            foreach (PieceEditorWindow window in Resources.FindObjectsOfTypeAll<PieceEditorWindow>())
                if (window.m_status != null) window.SetStatus(window.m_statusMessage);
        }

        private void OnEnable()
        {
            if (m_gridUndo == null)
            {
                m_gridUndo = CreateInstance<PieceGridUndoState>();
                m_gridUndo.hideFlags = HideFlags.HideAndDontSave;
                m_gridUndo.Grid.CopyFrom(m_draft);
            }
            Undo.undoRedoPerformed += OnUndoRedo;
            // Owner: this window instance. Stop point: OnDisable.
            ShapeLibraryAuthoring.LibraryCommitted += OnLibraryCommitted;
        }

        private void OnUndoRedo()
        {
            if (m_libraryField == null) return;
            if (!m_gridUndo.Grid.Matches(m_draft))
            {
                m_grid.CancelStroke();
                m_draft.CopyFrom(m_gridUndo.Grid);
                // The grid size is not saved with the piece, so only the cells decide unsaved changes.
                m_dirty = m_displayName != m_savedName || !m_draft.HasSameCells(m_savedGrid);
                SyncFields(); DrawGrid(); RefreshPreview();
                SendLevelPiece();
            }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            m_palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            m_library = BlockShapeLibrary.LoadProject();
            minSize = new Vector2(670, 580);
            var scroll = new ScrollView();
            rootVisualElement.Add(scroll);
            m_status = new HelpBox(string.Empty, HelpBoxMessageType.Info)
            {
                name = "piece-info"
            };
            m_status.style.marginTop = 8;
            m_status.style.marginBottom = 8;
            SetStatus(string.Empty);
            scroll.Add(m_status);
            m_sourceField = new ObjectField("JSON Source") { name = "json-source", objectType = typeof(TextAsset), allowSceneObjects = false };
            m_sourceField.SetEnabled(false);
            scroll.Add(m_sourceField);
            m_libraryField = new DropdownField("Piece library", Choices(), 0) { name = "piece-library" };
            SearchableDropdown.Enable(m_libraryField);
            m_libraryField.RegisterValueChangedCallback(evt =>
            {
                if (!CanDiscardDraft())
                {
                    m_libraryField.SetValueWithoutNotify(m_libraryField.choices[m_selected + 1]);
                    return;
                }
                LoadPiece(m_libraryField.index - 1);
            });
            m_libraryStepper = new ChoiceStepper(m_libraryField, "Previous piece", "Next piece");
            m_libraryStepper.AddCreateButton("new-piece", "New piece", NewPiece);
            m_deleteButton = m_libraryStepper.AddDeleteButton("delete-piece", "Delete selected piece", DeletePiece);
            scroll.Add(m_libraryStepper);
            // The template's key stays internal: it is derived from the cells (DEC-193).
            m_nameField = new TextField("Name") { value = m_displayName, name = "piece-name" };
            m_nameField.RegisterValueChangedCallback(evt => { m_displayName = evt.newValue; m_dirty = true; m_nameEdited = true; });
            // A level piece takes a new name when the edit is committed, not on every keystroke.
            m_nameField.RegisterCallback<FocusOutEvent>(_ => CommitName());
            m_nameField.RegisterCallback<KeyDownEvent>(OnNameKeyDown, TrickleDown.TrickleDown);
            scroll.Add(m_nameField);
            var color = new EnumField("Preview color", m_previewColor) { name = "preview-color" };
            color.RegisterValueChangedCallback(evt => { m_previewColor = (PaletteColor)evt.newValue; DrawGrid(); RefreshPreview(); });
            scroll.Add(color);
            // One slider for a square grid: 7 means a 7 x 7 area around (0, 0).
            m_sizeField = new SliderInt("Grid size", 1, PieceGridDraft.MaxSize)
            {
                name = "grid-size", showInputField = true, value = m_draft.Size
            };
            m_sizeField.RegisterValueChangedCallback(evt => SetGridSize(evt.newValue));
            scroll.Add(m_sizeField);
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexWrap = Wrap.Wrap;
            scroll.Add(body);
            var gridViewport = new VisualElement { name = "grid-viewport" };
            gridViewport.style.width = k_previewSize;
            gridViewport.style.height = k_previewSize;
            gridViewport.style.flexShrink = 0;
            gridViewport.style.alignItems = Align.Center;
            gridViewport.style.justifyContent = Justify.Center;
            gridViewport.style.marginRight = 12;
            body.Add(gridViewport);
            var gridRoot = new VisualElement { name = "piece-grid", focusable = true };
            gridRoot.style.flexShrink = 0;
            gridViewport.Add(gridRoot);
            m_grid = new PieceGridView(gridRoot, k_previewSize, () => s_levelTool, TransformPiece, OnCellPainted,
                OnStrokeEnded);
            m_image = new Image { name = "piece-preview", scaleMode = ScaleMode.ScaleToFit };
            m_image.style.width = k_previewSize; m_image.style.height = k_previewSize;
            body.Add(m_image);
            var actions = new VisualElement { name = "editing-actions" };
            actions.style.flexDirection = FlexDirection.Row;
            // Hidden in level-piece mode, where each edit reaches the level as it is made (SyncFields).
            m_saveButton = CreateButton(() => Save(), "Save piece", "save-piece");
            m_saveButton.tooltip = k_saveTooltip;
            actions.Add(m_saveButton);
            m_saveTemplateButton = CreateButton(SaveAsTemplate, "Save as template", "save-template");
            m_saveTemplateButton.tooltip = k_templateTooltip;
            actions.Add(m_saveTemplateButton);
            actions.Add(CreateButton(ClearGrid, "Clear grid", "clear-grid"));
            scroll.Add(actions);
            LoadDatabase(File.Exists(BoardKitAssets.ShapesJsonPath) ? BoardKitAssets.ShapesJsonPath : null);
        }

        // Three short lines at most: what this grid edits, the last message and the active tool, then the shortcuts.
        private void SetStatus(string message)
        {
            m_statusMessage = message ?? string.Empty;
            string intro = m_levelPieceMode ? k_levelPieceText : k_introText;
            string detail = string.IsNullOrEmpty(m_statusMessage) ? GetToolHint() : m_statusMessage + " · " + GetToolHint();
            m_status.text = intro + "\n" + detail + "\n" + k_shortcutText;
        }

        private static string GetToolHint()
        {
            switch (s_levelTool)
            {
                case LevelEditorTool.Pen: return "Pen: drag paints";
                case LevelEditorTool.Eraser: return "Eraser: drag erases";
                case LevelEditorTool.RotateCounterClockwise: return "Click to rotate left";
                case LevelEditorTool.RotateClockwise: return "Click to rotate right";
                case LevelEditorTool.MirrorHorizontal: return "Click to mirror horizontally";
                case LevelEditorTool.MirrorVertical: return "Click to mirror vertically";
                default: return "Left-drag paints, right-drag erases";
            }
        }

        private static Button CreateButton(Action action, string text, string name)
        {
            var button = new Button(action) { text = text, name = name };
            button.style.height = 32;
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            button.style.marginTop = 5; button.style.marginBottom = 5;
            return button;
        }

        private List<string> Choices()
        {
            var choices = new List<string> { "New piece" };
            // Numbered like Target Level, so two pieces with the same name stay separate choices.
            for (int i = 0; i < m_shapes.Count; i++)
                choices.Add($"{i + 1}. {m_shapes[i].DisplayName}");
            return choices;
        }

        // The file and library errors the Piece Editor shows as a status message instead of throwing.
        private static bool IsAuthoringError(Exception error) => error is ArgumentException ||
            error is InvalidOperationException || error is IOException || error is UnauthorizedAccessException;

        // Shows the piece library JSON at <paramref name="path"/>, or none when the file is missing.
        private void LoadDatabase(string path)
        {
            // Set before the read, so a library that cannot be read still names the path that was tried.
            m_databasePath = path;
            try
            {
                string json = path == null ? null : File.ReadAllText(path);
                var shapes = json == null ? null : BlockShapeLibraryJson.Read(json);
                m_shapes.Clear();
                if (shapes != null) m_shapes.AddRange(shapes);
                m_loadedJson = json;
                m_sourceField.SetValueWithoutNotify(path == null ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                m_libraryField.choices = Choices();
                m_libraryStepper.SetChoicesEnabled(path != null);
                if (m_levelPieceMode) ShowLevelPieceDraft();
                else if (m_pendingLibraryKey != 0) LoadPendingLibraryPiece();
                else LoadPiece(-1);
                if (path == null)
                    SetStatus($"The piece library {BoardKitAssets.ShapesJsonPath} is missing. Run Color Block Jam > Art > Build Board Kit Assets.");
            }
            catch (Exception error) when (IsAuthoringError(error))
            {
                SetStatus(error.Message);
            }
        }

        private bool IsActiveDatabase() => m_library != null && m_databasePath != null &&
            string.Equals(m_library.JsonPath, m_databasePath, StringComparison.Ordinal);

        private void SyncFields()
        {
            m_libraryField.SetValueWithoutNotify(m_libraryField.choices[m_selected + 1]);
            m_nameField.SetValueWithoutNotify(m_displayName);
            m_sizeField.SetValueWithoutNotify(m_draft.Size);
            m_deleteButton.SetEnabled(!m_levelPieceMode && m_selected >= 0 && m_databasePath != null);
            m_saveButton.style.display = m_levelPieceMode ? DisplayStyle.None : DisplayStyle.Flex;
            m_saveButton.SetEnabled(m_databasePath != null);
            m_saveTemplateButton.style.display = m_levelPieceMode ? DisplayStyle.Flex : DisplayStyle.None;
            m_saveTemplateButton.SetEnabled(m_databasePath != null);
        }

        private void LoadPiece(int index)
        {
            try
            {
                if (index >= 0) m_draft.Load(m_shapes[index].Cells);
                else
                {
                    m_draft = new PieceGridDraft();
                    m_draft.Set(0, 0, true);
                }
                // Any library piece or New piece leaves level-piece mode.
                m_levelPieceMode = false;
                m_selected = index;
                m_displayName = index >= 0 ? m_shapes[index].DisplayName : "New Piece";
                m_dirty = false;
                ResetGridHistory();
                SyncFields(); DrawGrid(); RefreshPreview();
            }
            catch (ArgumentException error) { SetStatus(error.Message); }
        }

        /// <summary>
        /// Loads the template with this key from the piece library after the unsaved-draft question. Before
        /// the window has built its GUI, the first database load shows it.
        /// </summary>
        internal void LoadLibraryPiece(ShapeKey key)
        {
            if (m_libraryField == null)
            {
                m_levelPieceMode = false;
                m_pendingLibraryKey = key.Code;
                return;
            }

            if (!CanDiscardDraft()) return;
            int index = IndexOfKey(key);
            if (index < 0)
            {
                SetStatus(GetMissingTemplateText());
                return;
            }
            LoadPiece(index);
        }

        /// <summary>
        /// Enters level-piece mode on <paramref name="request"/>, with its name in the Name field ("New Piece" when it
        /// has none), after the unsaved-draft question for a library draft. A request equal to the shown level piece
        /// changes nothing. A request without cells starts from one cell, like New piece. Before the window has built
        /// its GUI, the first database load shows it.
        /// </summary>
        internal void LoadLevelPiece(LevelPieceRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string name = string.IsNullOrEmpty(request.Name) ? "New Piece" : request.Name;
            // The Level Editor mirrors changes back; one that shows nothing new keeps the draft and its undo history.
            if (IsShownLevelPiece(request, name)) return;
            if (m_libraryField != null && !CanDiscardDraft()) return;

            var draft = new PieceGridDraft();
            try
            {
                if (request.Cells.Count == 0) draft.Set(0, 0, true);
                else draft.Load(request.Cells);
            }
            catch (ArgumentException error)
            {
                if (m_status != null) SetStatus(error.Message);
                return;
            }

            EndPaint();
            m_levelPieceMode = true;
            m_levelPiece = request;
            m_pendingLibraryKey = 0;
            m_draft = draft;
            m_selected = -1;
            m_displayName = name;
            m_nameEdited = false;
            m_dirty = false;
            if (m_libraryField == null) return;
            ResetGridHistory();
            SyncFields(); DrawGrid(); RefreshPreview();
        }

        // True when level-piece mode already shows this piece with this name and footprint.
        private bool IsShownLevelPiece(LevelPieceRequest request, string name)
        {
            if (!m_levelPieceMode || m_levelPiece == null || request.LevelId != m_levelPiece.LevelId ||
                request.PieceId != m_levelPiece.PieceId || name != m_displayName) return false;
            m_draft.ReadCells(m_cells);
            return IsSameFootprint(m_cells, request.Cells);
        }

        // Compares two footprints as cell sets, ignoring their offsets.
        private static bool IsSameFootprint(IReadOnlyList<Vector2Int> left, IReadOnlyList<Vector2Int> right)
        {
            if (left.Count != right.Count) return false;
            if (left.Count == 0) return true;
            Vector2Int offset = GetMin(right) - GetMin(left);
            for (int i = 0; i < left.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < right.Count && !found; j++) found = right[j] == left[i] + offset;
                if (!found) return false;
            }
            return true;
        }

        private static Vector2Int GetMin(IReadOnlyList<Vector2Int> cells)
        {
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            for (int i = 0; i < cells.Count; i++) min = Vector2Int.Min(min, cells[i]);
            return min;
        }

        // Shows the level piece being edited without replacing its draft, after a GUI rebuild or a database change.
        private void ShowLevelPieceDraft()
        {
            m_selected = -1;
            SyncFields(); DrawGrid(); RefreshPreview();
        }

        private void LoadPendingLibraryPiece()
        {
            ShapeKey.TryFromCode(m_pendingLibraryKey, out ShapeKey key);
            m_pendingLibraryKey = 0;
            int index = IndexOfKey(key);
            LoadPiece(index);
            if (index < 0) SetStatus(GetMissingTemplateText());
        }

        private int IndexOfKey(ShapeKey key)
        {
            for (int i = 0; key.IsValid && i < m_shapes.Count; i++)
                if (m_shapes[i].Key == key) return i;
            return -1;
        }

        private int IndexOfName(string displayName)
        {
            for (int i = 0; i < m_shapes.Count; i++)
                if (string.Equals(m_shapes[i].DisplayName, displayName, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private string GetMissingTemplateText() => $"No template of that shape in {DatabaseName()}.";

        private string DatabaseName() =>
            m_databasePath == null ? "no piece library" : Path.GetFileNameWithoutExtension(m_databasePath);

        // Keeps the cached JSON current after a commit from anywhere, such as another window or a library undo, so the
        // next Save is not refused as changed on disk. The draft and the name stay as they are. The selection follows
        // its template's key, or its name when the commit changed the template's footprint and so its key.
        private void OnLibraryCommitted()
        {
            if (m_libraryField == null || !IsActiveDatabase() || m_library.LoadedJson == null) return;
            BlockShapeLibrary.Shape selected = m_selected >= 0 && m_selected < m_shapes.Count ? m_shapes[m_selected] : null;
            m_loadedJson = m_library.LoadedJson;
            m_shapes.Clear();
            m_shapes.AddRange(BlockShapeLibraryJson.Read(m_loadedJson));
            m_libraryField.choices = Choices();
            int index = selected == null ? -1 : IndexOfKey(selected.Key);
            m_selected = index >= 0 || selected == null ? index : IndexOfName(selected.DisplayName);
            SyncFields();
        }

        private void DrawGrid() => m_grid.Draw(m_draft, m_palette.GetColor(m_previewColor));

        // A stroke changes one cell; its first change opens the stroke's undo step before the draft changes.
        private void OnCellPainted(int x, int y, bool filled, bool firstChange)
        {
            if (firstChange) RecordGridChange(filled ? "Paint piece cells" : "Erase piece cells");
            m_draft.Set(x, y, filled); m_dirty = true;
            m_gridUndo.Grid.CopyFrom(m_draft);
        }

        private void OnStrokeEnded(bool changed)
        {
            RefreshPreview();
            // Sent before the group closes, so a listener's undo record joins this stroke's undo step.
            if (changed) SendLevelPiece();
            Undo.IncrementCurrentGroup();
        }

        // The grid exists once the GUI is built; before that no stroke can be running.
        private void EndPaint() => m_grid?.EndPaint();

        private void OnPointerUp(PointerUpEvent evt) => EndPaint();
        private void OnPointerCancel(PointerCancelEvent evt) => EndPaint();
        private void OnLostFocus() => EndPaint();

        private void RecordGridChange(string label)
        {
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(m_gridUndo, label);
        }

        private void ResetGridHistory()
        {
            m_grid.CancelStroke();
            Undo.ClearUndo(m_gridUndo);
            m_gridUndo.Grid.CopyFrom(m_draft);
            m_savedGrid.CopyFrom(m_draft); m_savedName = m_displayName;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (focusedWindow != this || evt.keyCode != KeyCode.Z || !(evt.ctrlKey || evt.commandKey) || evt.altKey) return;
            // Text fields keep their own editing shortcuts.
            for (var element = evt.target as VisualElement; element != null; element = element.parent)
                if (element is TextField) return;
            EndPaint();
            Undo.FlushUndoRecordObjects();
            if (evt.shiftKey) Undo.PerformRedo(); else Undo.PerformUndo();
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        /// <summary>
        /// The Grid size slider: resizes the square area as one undo step. The footprint is unchanged, so nothing
        /// reaches the level or the dirty state; a size that would cut off painted cells is refused.
        /// </summary>
        internal void SetGridSize(int size)
        {
            EndPaint();
            if (size == m_draft.Size) return;
            var resized = new PieceGridDraft();
            resized.CopyFrom(m_draft);
            if (!resized.TrySetSize(size))
            {
                m_sizeField.SetValueWithoutNotify(m_draft.Size);
                SetStatus($"Erase the cells outside {size}×{size} first");
                return;
            }
            RecordGridChange("Resize piece grid");
            m_draft.CopyFrom(resized);
            m_gridUndo.Grid.CopyFrom(m_draft);
            m_sizeField.SetValueWithoutNotify(m_draft.Size);
            DrawGrid();
            Undo.IncrementCurrentGroup();
        }

        private void ClearGrid()
        {
            EndPaint();
            m_draft.ReadCells(m_cells);
            if (m_cells.Count == 0) return;
            RecordGridChange("Clear piece grid");
            m_draft.Clear(); m_gridUndo.Grid.CopyFrom(m_draft); m_dirty = true;
            DrawGrid(); RefreshPreview();
            SendLevelPiece();
        }

        private void RefreshPreview()
        {
            if (m_status == null) return;
            m_draft.ReadCells(m_cells);
            try
            {
                if (m_previewRenderer == null) m_previewRenderer = new PiecePreviewRenderer();
                // Render throws for an empty, duplicate or disconnected footprint, so the status shows why.
                m_image.image = m_previewRenderer.Render(m_cells, m_palette.GetAtlasColor(m_previewColor));
                SetStatus($"{m_cells.Count} cells");
            }
            catch (ArgumentException error) { m_image.image = null; SetStatus(error.Message); }
        }

        /// <summary>
        /// A Level Editor rotate or mirror tool's click on the grid: turns or mirrors the whole piece as one undo step.
        /// Any other tool changes nothing.
        /// </summary>
        internal void TransformPiece(LevelEditorTool tool)
        {
            if (!PieceGridView.IsTransformTool(tool)) return;
            EndPaint();
            m_draft.ReadCells(m_cells);
            if (m_cells.Count == 0) return;
            bool rotate = tool == LevelEditorTool.RotateClockwise || tool == LevelEditorTool.RotateCounterClockwise;
            RecordGridChange(rotate ? "Rotate piece grid" : "Mirror piece grid");
            Vector2Int[] cells = tool == LevelEditorTool.RotateClockwise ? LevelPieceTools.RotateClockwise(m_cells)
                : tool == LevelEditorTool.RotateCounterClockwise ? LevelPieceTools.RotateCounterClockwise(m_cells)
                : tool == LevelEditorTool.MirrorHorizontal ? LevelPieceTools.FlipHorizontal(m_cells)
                : LevelPieceTools.FlipVertical(m_cells);
            m_draft.Load(cells);
            m_gridUndo.Grid.CopyFrom(m_draft);
            m_dirty = true; SyncFields(); DrawGrid(); RefreshPreview();
            SendLevelPiece();
        }

        /// <summary>
        /// Starts on a new piece in a window a layout file restored (Color Block Jam/Level Editor loads one): the layout
        /// holds the level piece or draft of the window it was saved from, never the current work. Before the window
        /// has built its GUI, the first database load shows the new piece.
        /// </summary>
        internal void ForgetLayoutPiece()
        {
            m_levelPieceMode = false;
            m_levelPiece = null;
            m_pendingLibraryKey = 0;
            m_dirty = false;
            if (m_libraryField != null)
            {
                LoadPiece(-1);
            }
        }

        // A level piece's valid edits have already reached its level, so leaving it never asks.
        private bool CanDiscardDraft() => m_levelPieceMode || !m_dirty ||
            EditorUtility.DisplayDialog("Unsaved piece", "Discard the current grid changes?", "Discard", "Keep editing");

        private void NewPiece()
        {
            if (!CanDiscardDraft()) return;
            LoadPiece(-1);
            SetStatus("New draft. Save piece adds it to the piece library.");
        }

        // A committed Name edit sends the level piece with its new name.
        private void CommitName()
        {
            if (m_levelPieceMode && m_nameEdited) SendLevelPiece();
        }

        private void OnNameKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) CommitName();
        }

        /// <summary>
        /// Sends the level piece to its level after a change in level-piece mode. The level takes only a footprint
        /// with cells and a piece with a name, so a change that leaves either empty stays here and the status says why.
        /// </summary>
        private void SendLevelPiece()
        {
            if (!m_levelPieceMode) return;
            m_draft.ReadCells(m_cells);
            if (m_cells.Count == 0)
            {
                SetStatus("Paint at least one cell. The level keeps its last footprint.");
                return;
            }
            if (string.IsNullOrWhiteSpace(m_displayName))
            {
                SetStatus("Enter a piece name. The level keeps its last footprint and name.");
                return;
            }

            m_levelPiece = new LevelPieceRequest(m_levelPiece.LevelId, m_levelPiece.PieceId,
                ShapeLibraryAuthoring.Normalize(m_cells), m_displayName);
            m_nameEdited = false;
            RaiseLevelPieceChanged(m_levelPiece);
            // A listener's Undo.RecordObject joins the current undo group now, not at the editor's next flush.
            Undo.FlushUndoRecordObjects();
        }

        private static void RaiseLevelPieceChanged(LevelPieceRequest request)
        {
            Action<LevelPieceRequest> listeners = LevelPieceChanged;
            if (listeners == null) return;

            // A failing listener must not stop the other windows or leave this edit half done.
            foreach (Action<LevelPieceRequest> listener in listeners.GetInvocationList())
            {
                try { listener(request); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        private void SaveAsTemplate()
        {
            // Level-piece mode has no library selection, so Save adds a new library piece.
            if (!Save()) return;
            m_levelPieceMode = false;
            SyncFields();
            SetStatus("Saved the level piece as a library template.");
        }

        private bool Save()
        {
            EndPaint();
            try
            {
                if (m_databasePath == null) throw new InvalidOperationException($"The piece library {BoardKitAssets.ShapesJsonPath} is missing.");
                CheckActiveDatabase();
                m_draft.ReadCells(m_cells);
                // ShapeLibraryAuthoring owns the identity and normalization rules, shared with every other library
                // write; the default key adds a new template.
                ShapeKey existingKey = m_selected >= 0 ? m_shapes[m_selected].Key : default;
                int index = ShapeLibraryAuthoring.SavePiece(m_library, existingKey, m_displayName, m_cells,
                    m_loadedJson);
                m_loadedJson = m_library.LoadedJson;
                m_shapes.Clear(); m_shapes.AddRange(m_library.Shapes);
                m_selected = index; m_dirty = false;
                ResetGridHistory();
                m_libraryField.choices = Choices();
                SyncFields();
                SetStatus("Saved the piece to the piece library.");
                return true;
            }
            catch (Exception error) when (IsAuthoringError(error))
            { SetStatus(error.Message); return false; }
        }

        private void DeletePiece()
        {
            if (m_levelPieceMode || m_selected < 0) return;
            string name = m_shapes[m_selected].DisplayName;
            ShapeKey key = m_shapes[m_selected].Key;
            // Level blocks that use the template get the Level Editor's delete question (DEC-126, DEC-193).
            string routed = IsActiveDatabase() && DeleteLibraryPiece != null ? DeleteLibraryPiece(key) : null;
            if (routed != null)
            {
                if (IndexOfKey(key) < 0)
                {
                    m_libraryField.choices = Choices();
                    LoadPiece(-1);
                }

                SetStatus(routed);
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete piece", $"Remove '{name}' from the piece library?", "Delete", "Cancel")) return;
            try
            {
                var next = new List<BlockShapeLibrary.Shape>(m_shapes);
                next.RemoveAt(m_selected);
                m_loadedJson = SaveRecords(next, "Delete piece");
                m_shapes.Clear(); m_shapes.AddRange(next);
            }
            catch (Exception error) when (IsAuthoringError(error))
            { SetStatus(error.Message); return; }
            m_libraryField.choices = Choices();
            LoadPiece(-1);
            SetStatus("Removed the piece from the piece library.");
        }

        private string SaveRecords(IReadOnlyList<BlockShapeLibrary.Shape> records, string label)
        {
            CheckActiveDatabase();
            ShapeLibraryAuthoring.Commit(m_library, records, label, expectedDisk: m_loadedJson);
            return m_library.LoadedJson;
        }

        private void CheckActiveDatabase()
        {
            if (!IsActiveDatabase())
                throw new InvalidOperationException("The piece library changed. Reopen Piece Editor.");
        }

        private void OnDisable()
        {
            EndPaint();
            Undo.undoRedoPerformed -= OnUndoRedo;
            ShapeLibraryAuthoring.LibraryCommitted -= OnLibraryCommitted;
            m_previewRenderer?.Dispose();
            m_previewRenderer = null;
        }

        private void OnDestroy()
        {
            if (m_gridUndo == null) return;
            Undo.ClearUndo(m_gridUndo);
            DestroyImmediate(m_gridUndo);
        }
    }
}
