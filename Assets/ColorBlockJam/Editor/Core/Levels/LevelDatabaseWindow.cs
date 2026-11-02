using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Editor.Search;
using ColorBlockJam.Environment;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Level Editor window: a tools column beside the level selection bar and level canvas, with edits kept in a
    /// private undoable draft until Save Level writes the canonical JSON (layout: docs/Level-Editor-Design.md).
    /// </summary>
    public sealed class LevelDatabaseWindow : EditorWindow, ILevelEditorShortcutTarget
    {
        internal const string k_splitViewDataKey = "cbj-level-editor-split";
        // View data alone did not restore the splitter after a close and reopen in 2022.3, so the width is kept here too.
        internal const string k_toolsWidthPrefKey = "ColorBlockJam.LevelEditor.ToolsWidth";
        // The validation panel's dragged height (user request of 2026-09-28); 0 keeps its natural height.
        internal const string k_validationHeightPrefKey = "ColorBlockJam.LevelEditor.ValidationHeight";
        // The summary, the pager, the toggles and the buttons stay visible; the list scrolls below that.
        private const float k_validationMinHeight = 150f;
        private const float k_toolsMinVisibleHeight = 80f;
        private const float k_validationDefaultHeight = 260f;
        // The validation panel's two toggles in a designer's window (contract Revision 1 of
        // TASK-20260928-091128541-cb48c84a): read by Open, written on each change; both default to on.
        internal const string k_autoPlayPrefKey = "ColorBlockJam.LevelEditor.AutoPlaySolutions";
        internal const string k_validateBeforeSavePrefKey = "ColorBlockJam.LevelEditor.ValidateBeforeSave";
        // TwoPaneSplitView's splitter handle in Unity 2022.3; only a drag that ends on it stores the tools width.
        private const string k_splitterHandleName = "unity-dragline-anchor";
        private const float k_minWindowWidth = 960f;
        private const float k_minWindowHeight = 640f;
        internal const float k_toolsWidth = 280f;
        private const float k_toolsMinWidth = 240f;
        private const float k_rightPaneMinWidth = 480f;
        private const float k_topLabelWidth = 96f;
        private const float k_toolsLabelWidth = 90f;
        private const float k_iconButtonWidth = 24f;
        private const float k_toolButtonSize = 34f;
        private const float k_actionButtonHeight = 24f;
        private const float k_actionIconSize = 16f;
        private const float k_actionFontSize = 11f;
        private const int k_minTimerSeconds = 10;
        private const int k_maxTimerSeconds = 300;
        private const float k_timerLabelWidth = 40f;
        private const float k_timerValueWidth = 48f;
        private const string k_unsavedStatus = "Unsaved level changes. Press Save Level to write the JSON source.";
        private const string k_draftChangesMessage = "The selected level has unsaved changes. Save them to the JSON source?";
        private const string k_saveTooltip = "Validate the draft and write it to the JSON source.";
        internal const string k_saveBlockedTooltip =
            "Fix the errors in Issues first, such as overlapping blocks. Save Level writes only a valid level.";
        internal const string k_testText = "Test Level";
        private const string k_testTooltip = "Save the level and play it in the Game scene. Stop Play Mode to come " +
                                             "back to this level and keep editing it.";
        internal const string k_testBlockedTooltip =
            "Fix the errors in Issues first, such as overlapping blocks. Test Level plays only a valid level.";
        // The button's tooltip while Play Mode starts or stops, when it waits.
        internal const string k_testPlayingTooltip = "Stop Play Mode to test the level again.";
        // While Play Mode runs, Test Level's button reads Stop Test (contract Revision 1).
        internal const string k_stopTestText = "Stop Test";
        internal const string k_stopTestTooltip = "Stop the test: Play Mode ends, Unity reopens the LevelEditor scene " +
                                                  "and this window comes back to the tested level.";
        // Validate Level (DEC-144) sits between Save Level and Test Level and never saves or changes the draft.
        internal const string k_validateTooltip = "Check whether this level can be solved without boosters.";
        internal const string k_validateBlockedTooltip =
            "Fix the errors in Issues first, such as overlapping blocks. Validate Level checks only a valid level.";
        internal const string k_validatePlayingTooltip = "Validate Level is unavailable in Play Mode. Stop Play Mode " +
                                                         "to check the level.";
        // In a designer's window Validate Level runs in the background, and its button reads Cancel meanwhile (DEC-175).
        internal const string k_validateText = "Validate Level";
        internal const string k_cancelValidateText = "Cancel";
        internal const string k_cancelValidateTooltip = "Stop Validate Level. A check stopped early gives no answer; a " +
                                                        "listing stopped early keeps the solutions found so far. " +
                                                        "Editing the level also stops it.";
        // The progress line changes at most this often while a background Validate Level runs.
        private const double k_validationProgressSeconds = 0.1;
        // Open Level Editor Scene sits after Test Level and runs the workspace launcher's scene step on request.
        internal const string k_openSceneText = "Level Editor Scene";
        private const string k_openSceneTooltip = "Open the LevelEditor scene, which shows this level in 3D. Unity " +
                                                  "asks first to save other modified scenes.";
        private const string k_openScenePlayingTooltip = "Stop Play Mode to open the LevelEditor scene.";
        // Save Level and Test Level are the window's main actions, so they are larger than the other buttons (user
        // request of 2026-09-27).
        internal const float k_bottomButtonHeight = 30f;
        private const float k_bottomButtonMinWidth = 120f;
        private const float k_bottomButtonFontSize = 13f;
        // The JSON Source row's Restore button names both of its jobs.
        private const string k_restoreTooltip = "Restore: with unsaved changes, discard them and return the level to " +
                                                "its last saved state; otherwise bring back the JSON version that the " +
                                                "last save replaced.";
        private const float k_obstacleTileWidth = 58f;
        private const float k_obstacleSwatchSize = 24f;
        /// <summary>The hint a plain Pen click on the board gives when no block is being edited.</summary>
        internal const string k_penHint = LevelPieceEditController.k_penHint;
        /// <summary>The hint Color Switch gives when no block or gate is chosen.</summary>
        internal const string k_switchHint = "Click a block, then press Color Switch.";
        /// <summary>The name of the how-to info box above the canvas.</summary>
        internal const string k_hintBoxName = "tool-hint";
        internal const string k_barrierTooltip = "Barrier: closed cells are walls and open cells are floor; the " +
                                                 "barrier flips after every cleared block. Left drag paints one " +
                                                 "barrier per connected stroke, which starts closed, and joins one it " +
                                                 "touches; the Eraser or a right drag removes cells.";
        internal const string k_voidTooltip = "Void: cuts board cells out of the play area, so walls wrap a hole or " +
                                              "a notch in the edge. Left drag paints void on free floor that keeps " +
                                              "the rest connected; the Eraser or a right drag restores floor.";

        // The project scope installer on ProjectContext.prefab, which names the level JSON the game plays (DEC-194).
        [SerializeField] private ProjectScopeInstaller m_projectScope;
        [SerializeField] private int m_selectedIndex = -1;

        private DropdownField m_databaseChoiceField;
        private List<LevelDatabaseAddressables.Entry> m_entries;
        private ObjectField m_sourceField;
        private Button m_createDatabaseButton;
        private Button m_deleteDatabaseButton;
        private DropdownField m_targetField;
        private ChoiceStepper m_targetStepper;
        private TextField m_levelNameField;
        private IntegerField m_timerField;
        private Label m_timerText;
        private VisualElement m_draftTracker;
        private DropdownField m_blockChoiceField;
        private DropdownField m_pieceTypeField;
        private Button m_rotateClockwiseButton;
        private Button m_rotateCounterClockwiseButton;
        private Button m_flipBlockButton;
        private Button m_flipVerticalButton;
        private LevelShapePalette m_shapePalette;
        // The session survives the domain reload after Save as template; the controller borrows this exact instance.
        [SerializeField] private LevelPieceSession m_pieceSession = new LevelPieceSession();
        private LevelPieceEditController m_pieceEditController;
        private VisualElement m_shapeBanner;
        private Label m_shapeBannerText;
        private Button m_restampButton;
        // The Pen template banner under the top bar (DEC-125): what a template edit reaches, its Discard, and Keep
        // library footprint for a saved footprint the library still lacks.
        private VisualElement m_templateBanner;
        private Label m_templateBannerText;
        private Button m_discardTemplateButton;
        private Button m_separateTemplateButton;
        private Button m_keepTemplateButton;
        // The catalog the palette tiles were built from, so an unchanged catalog never rebuilds them.
        private LevelShapeCatalog m_paletteCatalog;
        // The last refused Piece Editor change or Re-stamp; the banner shows it until the next draft change.
        private string m_shapeRefusal;
        // True while the draft's issues list holds an error (LevelData.Validate fails), such as blocks a re-stamp left
        // overlapping (contract D3-B); Save Level stays disabled until none is left.
        private bool m_draftHasErrors;
        private LevelColorSwatches m_colorSwatches;
        private Button m_colorSwitchButton;
        private Button m_colorShuffleButton;
        // Draws Color Shuffle orders and Color Switch partners. The draft keeps each result, so no seed has to survive
        // a reload.
        private readonly System.Random m_colorRandom = new System.Random();
        private readonly LevelEditorToolState m_toolState = new LevelEditorToolState();
        private Button m_selectToolButton;
        private Button m_penToolButton;
        private Button m_eraserToolButton;
        private Button m_barrierObstacleButton;
        private VisualElement m_barrierObstacleSwatch;
        private Button m_voidObstacleButton;
        private VisualElement m_voidObstacleSwatch;
        private int m_selectedBlockIndex;
        // True only when the designer chose the selected block (canvas click or Selected Block dropdown). The
        // automatic first-block default and a just-placed block are not choices, so a swatch never recolours them.
        private bool m_blockChosen;
        // True only when the designer clicked the selected gate; a just-painted gate is selected but not chosen, so a
        // swatch picked for the next piece never recolours it (a gate's colour decides which blocks exit).
        private bool m_doorChosen;
        // True after a click on empty space cleared the selection (user order of 2026-09-26): no automatic first-block
        // default comes back until the designer picks a block, gate or barrier again or another level is loaded.
        private bool m_selectionCleared;
        private LevelCanvasElement m_canvas;
        // The canvas and its hint box: a press here while a solution shows only stops it (OnCanvasPressStarted).
        private VisualElement m_canvasSlot;
        // Rule-set wall glyphs for the canvas, composed once per draft change from the environment database.
        private readonly LevelCanvasWalls m_canvasWalls = new LevelCanvasWalls();
        private readonly List<LevelWallGlyph> m_wallGlyphs = new List<LevelWallGlyph>();
        private EnvironmentDatabase m_environmentDatabase;
        private LevelEditorIssuesList m_issuesList;
        // A board shrink that would remove content waits here, hatched on the canvas, until Remove or Cancel.
        private LevelData m_pendingResize;
        private VisualElement m_resizeConfirm;
        private Label m_resizeConfirmText;
        // The canvas selection; a block selection mirrors m_selectedBlockIndex, a door selection clears it.
        private LevelCanvasSelection m_canvasSelection;
        private Button m_createLevelButton;
        private Button m_deleteLevelButton;
        private Button m_saveLevelButton;
        private Button m_validateLevelButton;
        // Runs the solvability check for Validate Level and Save Level and keeps its result until the draft changes.
        private readonly LevelValidationFlow m_validationFlow = new LevelValidationFlow();
        // True while PollValidationRun is subscribed to EditorApplication.update for a background Validate Level run.
        private bool m_pollsValidation;
        private double m_validationProgressShownAt;
        // The panel under the tool groups: Validate Level's summary and listed solutions, and the three main buttons
        // (TASK-20260928-091128541-cb48c84a).
        private LevelValidationPanel m_validationPanel;
        private LevelEditorShortcuts m_shortcuts;
        // Validate Level's answer in the panel. A change of what it was made for shows the hint again
        // (ClearValidationAnswerIfChanged); the kept verdict follows the flow's own revision rule (DEC-144).
        private readonly LevelListedAnswer m_listedAnswer = new LevelListedAnswer();
        // Plays a listed solution on the canvas and, through SolutionFramePublished, in the scene preview. Created in
        // OnEnable; every stop point goes through StopSolutionPlayback.
        private LevelSolutionPlayback m_solutionPlayback;
        // The pointer of a canvas press whose end may open the LevelEditor scene, or -1 (contract Revision 1).
        private int m_scenePressPointerId = -1;
        // True while a one-shot editor update waits to open the LevelEditor scene after a canvas press.
        private bool m_sceneOpenScheduled;
        private Button m_testLevelButton;
        private Button m_openSceneButton;
        // The level a Test Level play started with; back in Edit Mode it names the level this window returns to. It is
        // serialized so it survives a domain reload on entering Play Mode.
        [SerializeField] private string m_testedLevelName;
        private Button m_reloadButton;
        private Button m_restoreButton;
        private Label m_status;
        // How-to guidance over the top of the canvas, kept apart from the status line's action results (user request
        // of 2026-09-26); the next status or tool change clears it.
        private HelpBox m_hintBox;
        private string m_hint;
        private readonly LevelDraftController m_draftController = new LevelDraftController();
        private LevelSaveController m_saveController;
        private bool m_refreshing;
        private bool m_hasDatabasePlaceholder;
        private bool m_publishScheduled;

        /// <summary>
        /// Editor-tool event with a snapshot of the draft level, or null when no valid
        /// level is selected or a Test Level play starts. Each subscriber names its own stop point and unsubscribes there.
        /// </summary>
        public static event Action<LevelData> DraftLevelPublished;
        public static LevelData LastPublishedLevel { get; private set; }

        /// <summary>
        /// Editor-tool event with each board a solution playback shows on the canvas: the start board, the board after
        /// every one-cell step and every exit, and the cleared board, which has no block. It is separate from
        /// DraftLevelPublished: a frame never becomes LastPublishedLevel, and when the playback ends or stops, the window
        /// publishes its draft again on the next editor update. Each subscriber names its own stop point and
        /// unsubscribes there.
        /// </summary>
        public static event Action<LevelData> SolutionFramePublished;

        /// <summary>
        /// Asks the designer to confirm a piece library delete; returns true to delete. Tests replace it so no modal
        /// dialog opens.
        /// </summary>
        internal Func<string, bool> ConfirmPieceDelete { get; set; } = message =>
            EditorUtility.DisplayDialog("Delete Piece", message, "Delete", "Cancel");

        /// <summary>
        /// Asks Delete template, Delete template and its blocks or Cancel for a template that blocks of the active
        /// database use (DEC-126). Tests replace it so no modal dialog opens.
        /// </summary>
        internal Func<LevelPieceDeleteQuestion, LevelPieceDeleteChoice> ChoosePieceDelete { get; set; } = AskPieceDelete;

        /// <summary>
        /// Deletes a template, by its key, from the piece library after the level write (DEC-126); returns null or the
        /// reason. Null uses the project piece library's undoable delete; tests replace it to make the delete fail.
        /// </summary>
        internal Func<ShapeKey, string> DeletePieceFromLibrary { get; set; }

        /// <summary>
        /// The other level databases' blocks of a template key, named in the template delete question as unchanged; the
        /// arguments are the active database GUID and the key. Tests replace it.
        /// </summary>
        internal Func<string, ShapeKey, List<LevelPieceDeletePlan.OtherDatabase>> CountOtherDatabaseUses { get; set; } =
            LevelPieceDeleteRoute.CountOtherDatabaseFiles;

        /// <summary>True once the window built its GUI, so it can run a template delete for the Piece Editor.</summary>
        internal bool HasGui => m_status != null;

        /// <summary>
        /// The template delete question's default dialog: three buttons when both options are possible (Enter is Delete
        /// template; Esc and the close box are Cancel), two when one is, none when neither is.
        /// </summary>
        internal static LevelPieceDeleteChoice AskPieceDelete(LevelPieceDeleteQuestion question)
        {
            const string k_deleteTemplate = "Delete template";
            const string k_deleteBlocks = "Delete template and its blocks";
            if (question.CanDeleteTemplate && question.CanDeleteBlocks)
            {
                switch (EditorUtility.DisplayDialogComplex("Delete Piece", question.Message, k_deleteTemplate, "Cancel",
                            k_deleteBlocks))
                {
                    case 0:
                        return LevelPieceDeleteChoice.DeleteTemplate;
                    case 2:
                        return LevelPieceDeleteChoice.DeleteBlocks;
                    default:
                        return LevelPieceDeleteChoice.Cancel;
                }
            }

            if (question.CanDeleteTemplate)
            {
                return EditorUtility.DisplayDialog("Delete Piece", question.Message, k_deleteTemplate, "Cancel")
                    ? LevelPieceDeleteChoice.DeleteTemplate
                    : LevelPieceDeleteChoice.Cancel;
            }

            return question.CanDeleteBlocks
                   && EditorUtility.DisplayDialog("Delete Piece", question.Message, k_deleteBlocks, "Cancel")
                ? LevelPieceDeleteChoice.DeleteBlocks
                : LevelPieceDeleteChoice.Cancel;
        }

        /// <summary>
        /// The Piece Editor link: whether a Piece Editor window is open, and how a piece is shown there. Tests replace
        /// them so no Piece Editor window opens or asks to discard its draft.
        /// </summary>
        internal Func<bool> IsPieceEditorOpen { get; set; } = () => HasOpenInstances<PieceEditorWindow>();
        internal Action<ShapeKey> ShowLibraryPieceInEditor { get; set; } = PieceEditorWindow.ShowLibraryPiece;
        // Mirrors keep the focus here, so drawing in the Level Editor continues while the Piece Editor follows.
        internal Action<LevelPieceRequest> ShowLevelPieceInEditor { get; set; } =
            request => PieceEditorWindow.ShowLevelPiece(request, false);

        /// <summary>
        /// Writes Save Level's pending template footprints to the piece library after the level JSON is written
        /// (DEC-125); returns null on success or a designer message. Null uses the project piece library
        /// (LevelTemplateLibraryCommit.TryReplaceFootprints); tests replace it so no library asset is written.
        /// </summary>
        internal Func<IReadOnlyList<LevelTemplateEdit>, string> CommitTemplatesToLibrary { get; set; }

        /// <summary>
        /// Writes every level of the active database in one atomic replace (Save Level, a template delete, Restore
        /// Previous Save); returns false after showing why. A false that also marks the source invalid means the JSON was
        /// written but could not be reloaded (SaveLevels). Null uses the active level JSON (SaveLevels); tests replace it
        /// so no level JSON is written.
        /// </summary>
        internal Func<IReadOnlyList<LevelData>, bool> WriteLevels { get; set; }

        /// <summary>
        /// Asks whether Save Level may leave the other levels that cannot take a new template footprint on the old one
        /// (their blocks keep the template's name, DEC-193); true saves. Cancel is the default answer. Tests replace
        /// it so no modal dialog opens.
        /// </summary>
        internal Func<string, bool> ConfirmTemplateSave { get; set; } = message =>
            EditorUtility.DisplayDialogComplex("Save Level", message, "Cancel", "Keep Editing",
                "Save and keep their old footprint") == 2;

        /// <summary>
        /// Asks whether Save Level may write a level the solvability check proved unsolvable or could not confirm
        /// (DEC-144); the arguments are the title and the message, and true saves. Cancel is the default answer: Enter
        /// picks Cancel, and Esc and the close box pick Keep Editing, so only Save Anyway saves. Tests replace it so no
        /// modal dialog opens.
        /// </summary>
        internal Func<string, string, bool> ConfirmUnsolvableSave { get; set; } = (title, message) =>
            EditorUtility.DisplayDialogComplex(title, message, "Cancel", "Keep Editing", "Save Anyway") == 2;

        /// <summary>The solvability check's owner: its progress, Play Mode and budget seams, and its run count.</summary>
        internal LevelValidationFlow ValidationFlow => m_validationFlow;

        /// <summary>The validation panel under the tool groups, or null before the GUI is built.</summary>
        internal LevelValidationPanel ValidationPanel => m_validationPanel;

        /// <summary>The solution playback: its clock and update seams, its entry and its frame.</summary>
        internal LevelSolutionPlayback SolutionPlayback => m_solutionPlayback;

        /// <summary>
        /// Asks whether Restore may discard the draft's unsaved changes; true discards. Cancel is the default answer:
        /// Enter picks Cancel, and Esc and the close box pick Keep Editing, so only an explicit Discard click discards.
        /// Tests replace it so no modal dialog opens.
        /// </summary>
        internal Func<string, bool> ConfirmDiscardChanges { get; set; } = message =>
            EditorUtility.DisplayDialogComplex("Discard Unsaved Changes", message, "Cancel", "Keep Editing",
                "Discard") == 2;

        /// <summary>
        /// Asks what happens to the draft's unsaved changes before the window leaves the level: another level or
        /// database, a new or deleted level or database, or Reload. Enter picks Save, and Esc and the close box pick
        /// Cancel, so only an explicit Discard click drops the changes. Tests replace it so no modal dialog opens.
        /// </summary>
        internal Func<LevelLeaveChoice> ChooseLeaveDraft { get; set; } = AskLeaveDraft;

        private static LevelLeaveChoice AskLeaveDraft()
        {
            switch (EditorUtility.DisplayDialogComplex("Unsaved Level", "Save this level before leaving it?", "Save",
                        "Cancel", "Discard"))
            {
                case 0:
                    return LevelLeaveChoice.Save;
                case 2:
                    return LevelLeaveChoice.Discard;
                default:
                    return LevelLeaveChoice.Cancel;
            }
        }

        /// <summary>
        /// Starts Test Level's Play Mode in the Game scene and returns null, or returns why nothing started (DEC-127).
        /// Tests replace it so no Play Mode begins.
        /// </summary>
        internal Func<LevelTestPlayRequest, string> StartTestPlay { get; set; } = request =>
            LevelTestPlay.Start(request, new LevelTestPlay.Steps());

        /// <summary>
        /// Stop Test's step (contract Revision 1): ends Play Mode. Unity then reopens the scenes Play Mode closed, and
        /// OnPlayModeStateChanged brings the window back to the tested level (DEC-127). Tests replace it so no Play Mode
        /// ends.
        /// </summary>
        internal Action StopTestPlay { get; set; } = EditorApplication.ExitPlaymode;

        /// <summary>
        /// True while the Editor is in Play Mode, also while it is leaving it (EditorApplication.isPlaying). With the
        /// validation flow's IsPlaying, which also covers entering it, it tells a running Play Mode from a transition.
        /// Tests replace it.
        /// </summary>
        internal Func<bool> IsInPlayMode { get; set; } = () => EditorApplication.isPlaying;

        /// <summary>Whether the LevelEditor scene is loaded; a canvas press opens it when it is not. Tests replace it.</summary>
        internal Func<bool> IsLevelEditorSceneLoaded { get; set; } = () =>
            SceneManager.GetSceneByPath(LevelEditorWorkspace.k_scenePath).isLoaded;

        /// <summary>
        /// Subscribes the poll of a background Validate Level run (DEC-175). Tests replace it and run the poll themselves.
        /// </summary>
        internal Action<EditorApplication.CallbackFunction> AddValidationUpdate { get; set; } =
            callback => EditorApplication.update += callback;

        /// <summary>Unsubscribes the background run's poll. Tests replace it with AddValidationUpdate.</summary>
        internal Action<EditorApplication.CallbackFunction> RemoveValidationUpdate { get; set; } =
            callback => EditorApplication.update -= callback;

        /// <summary>
        /// The LevelEditor scene step a finished canvas press runs in a designer's window (contract Revision 1): the
        /// workspace launcher's scene step alone, which runs Unity's save prompt for modified scenes and opens the scene
        /// single, never saving the LevelEditor scene. Tests replace it so no scene changes.
        /// </summary>
        internal Func<LevelEditorWorkspace.SceneOutcome> OpenLevelEditorScene { get; set; } = () =>
            LevelEditorWorkspace.OpenScene(new LevelEditorWorkspace.Steps());

        /// <summary>
        /// Editor-only event raised with this window after every canvas selection or draft change, and when it closes.
        /// Owner: the Level Editor window type; its subscriber, the Effect Editor, unsubscribes in its OnDisable.
        /// </summary>
        internal static event Action<LevelDatabaseWindow> SelectionChanged;

        /// <summary>The block, gate or board object selected on the canvas.</summary>
        internal LevelCanvasSelection CanvasSelection => m_canvasSelection;

        /// <summary>
        /// Opens a closed Effect Editor after a designer's canvas click, without taking the focus. Tests replace it so
        /// no window opens.
        /// </summary>
        internal Action<LevelDatabaseWindow> ShowEffectEditor { get; set; } = LevelEffectEditorWindow.ShowBeside;

        // Set by Open, the designer entry points; a window a test creates keeps it off, so its clicks open no window.
        [SerializeField] private bool m_opensEffectEditor;

        // Validate Before Save (DEC-144, contract Revision 1): whether Save Level, Test Level and save-on-close run the
        // solvability check first. Open sets it from the designer's last choice (on by default); a window a test
        // creates keeps it off, so its Save Level runs no check and the fixtures' saves stay as they were.
        [SerializeField] private bool m_checksSolvabilityOnSave;

        // Auto Play (contract Revision 1): a row click plays its solution at once when on and shows its start board
        // paused when off. Open sets it from the designer's last choice; a window a test creates keeps it on.
        [SerializeField] private bool m_autoPlaysSolutions = true;

        // Set by Open, the designer entry points (contract Revision 1): the panel's two toggles start from EditorPrefs
        // and store each change there, and a finished canvas press opens a closed LevelEditor scene. A window a test
        // creates keeps it off, so it never reads or writes EditorPrefs and never switches scenes.
        [SerializeField] private bool m_isDesignerWindow;

        // Set when the designer cancels Unity's save prompt before the LevelEditor scene opens after a canvas press: no
        // canvas press asks again until the window is opened through Open again.
        [SerializeField] private bool m_sceneOpenDeclined;

        internal bool OpensEffectEditor
        {
            get => m_opensEffectEditor;
            set => m_opensEffectEditor = value;
        }

        /// <summary>
        /// Validate Before Save: whether Save Level, Test Level and save-on-close run the solvability check first
        /// (DEC-144). The panel's toggle shows it; setting it here stores nothing in EditorPrefs.
        /// </summary>
        internal bool ChecksSolvabilityOnSave
        {
            get => m_checksSolvabilityOnSave;
            set
            {
                m_checksSolvabilityOnSave = value;
                m_validationPanel?.SetValidateBeforeSave(value);
            }
        }

        /// <summary>
        /// Auto Play: whether a row click plays its solution at once or shows its start board paused. The panel's toggle
        /// shows it; setting it here stores nothing in EditorPrefs.
        /// </summary>
        internal bool AutoPlaysSolutions
        {
            get => m_autoPlaysSolutions;
            set
            {
                m_autoPlaysSolutions = value;
                m_validationPanel?.SetAutoPlay(value);
            }
        }

        /// <summary>True for a window opened through Open (or <see cref="SetDesignerWindow"/>).</summary>
        internal bool IsDesignerWindow => m_isDesignerWindow;

        /// <summary>
        /// Opens or focuses the Level Editor. The Color Block Jam/Level Editor menu item belongs to the workspace
        /// launcher (<see cref="LevelEditorWorkspace"/>), which calls this.
        /// </summary>
        public static void Open()
        {
            LevelDatabaseWindow window = GetWindow<LevelDatabaseWindow>("Level Editor");
            window.m_opensEffectEditor = true;
            window.SetDesignerWindow();
            // Validate Level runs off the main thread, so a long check never freezes the Editor (DEC-175). Only Open
            // turns it on (OnEnable keeps it after a reload), so a test that calls SetDesignerWindow keeps the
            // synchronous check.
            window.m_validationFlow.RunsInBackground = true;
            window.Show();
        }

        /// <summary>
        /// Makes this the designer's window, as Open does (contract Revision 1): Auto Play and Validate Before Save take
        /// the designer's last choices from EditorPrefs (both on by default) and each change is stored there, a finished
        /// canvas press opens a closed LevelEditor scene, and an earlier Cancel of that scene's save prompt is forgotten.
        /// Tests call it on their own window in place of Open.
        /// </summary>
        internal void SetDesignerWindow()
        {
            m_isDesignerWindow = true;
            m_sceneOpenDeclined = false;
            m_autoPlaysSolutions = EditorPrefs.GetBool(k_autoPlayPrefKey, true);
            m_checksSolvabilityOnSave = EditorPrefs.GetBool(k_validateBeforeSavePrefKey, true);
            m_validationPanel?.SetAutoPlay(m_autoPlaysSolutions);
            m_validationPanel?.SetValidateBeforeSave(m_checksSolvabilityOnSave);
        }

        /// <summary>
        /// Starts a new piece session in a window a layout file restored (Color Block Jam/Level Editor loads one). The
        /// layout holds the state of the window it was saved from, never the current work, which the launcher settled
        /// before the layout replaced the windows; kept, its level pieces would show as tiles and reserve their names.
        /// </summary>
        internal void ForgetLayoutSession()
        {
            m_pieceSession.Clear();
            LevelData level = HasGui ? m_draftController.Snapshot() : null;
            if (level != null)
            {
                RefreshCustomPieces(level);
                UpdateUnsavedChanges();
            }
        }

        /// <summary>A copy of the draft level, or null when no level is open.</summary>
        internal LevelData GetDraftLevel()
        {
            return m_draftController.Snapshot();
        }

        /// <summary>
        /// The Effect Editor's command entry point: runs the request on the current draft and applies its result like a
        /// canvas gesture (one undo group, the dirty marker, one preview publish), or shows its refusal in the status
        /// line. False, with the refusal or null when nothing changed, when the draft stays as it was.
        /// </summary>
        internal bool TryApplyEditRequest(string undoName, LevelEditRequest request, out string issue)
        {
            LevelData level = m_draftController.Snapshot();
            LevelData result = null;
            issue = level == null ? "Select a level first." : null;
            bool accepted = level != null && request(level, out result, out issue);
            ApplyCommand(undoName, accepted, result, issue);
            return accepted;
        }

        private void OnEnable()
        {
            m_pieceEditController ??= new LevelPieceEditController(m_draftController, m_pieceSession);
            m_saveController ??= new LevelSaveController(m_draftController, m_pieceEditController);
            m_solutionPlayback ??= new LevelSolutionPlayback(ShowSolutionFrame, RestoreAfterSolutionPlayback);
            // Set here, not only in Open, so a window recreated by a layout or domain reload keeps it.
            minSize = new Vector2(k_minWindowWidth, k_minWindowHeight);
            saveChangesMessage = k_draftChangesMessage;
            // Owner: this window instance. Stop point: OnDisable.
            Undo.undoRedoPerformed += OnUndoRedo;
            // The Piece Editor link. Owner: this window instance. Stop point: OnDisable. LibraryCommitted also fires
            // inside this window's own palette delete, so its handler only reads and schedules draft edits.
            PieceEditorWindow.LevelPieceChanged += OnLevelPieceChanged;
            ShapeLibraryAuthoring.LibraryCommitted += OnLibraryCommitted;
            // Test Level's Play Mode round trip. Owner: this window instance. Stop point: OnDisable.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // A designer's window restored by a domain or layout reload validates in the background again (DEC-175). A
            // new window gets it from Open, which runs after this.
            m_validationFlow.RunsInBackground = m_isDesignerWindow;
            // A background Validate Level answer. Owner: this window instance. Stop point: OnDisable.
            m_validationFlow.RunFinished += OnValidationRunFinished;
        }

        private void CreateGUI()
        {
            // A rebuilt GUI starts without a playing solution or a listed answer (the list is not kept across it), and
            // without a canvas press in progress.
            StopSolutionPlayback();
            ForgetValidationAnswer();
            m_scenePressPointerId = -1;
            m_projectScope = LevelDatabaseAddressables.LoadProjectScope();
            m_environmentDatabase = LevelCanvasWalls.LoadDatabase();
            m_pendingResize = null;

            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;

            float toolsWidth = Mathf.Max(k_toolsMinWidth, EditorPrefs.GetFloat(k_toolsWidthPrefKey, k_toolsWidth));
            TwoPaneSplitView split = new TwoPaneSplitView(0, toolsWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "level-editor-split",
                viewDataKey = k_splitViewDataKey
            };
            split.style.flexGrow = 1;
            VisualElement toolsColumn = CreateToolsColumn();
            split.Add(toolsColumn);
            split.Add(CreateSelectionPane());
            // Stores the width only when a splitter drag ends, so a width the split forces on a narrow window is
            // never saved. The handle captures the pointer while dragging, so it receives the release itself.
            // Owner: the splitter handle; stop point: the handle is dropped with its window GUI.
            // The split's own handle, a direct child: the tools column holds a second, vertical one with the same name.
            GetOwnSplitterHandle(split)?.RegisterCallback<PointerUpEvent>(_ => SaveDraggedToolsWidth(toolsColumn));
            root.Add(split);
            root.Add(CreateBottomBar());
            m_colorSwatches.SetColors(m_canvas.GetPaletteColor);
            m_validationPanel.SetColors(m_canvas.GetPaletteColor);
            Color barrierWall = m_canvas.GetPaletteColor(PaletteColor.Wall);
            m_barrierObstacleSwatch.style.backgroundColor = new Color(barrierWall.r, barrierWall.g, barrierWall.b, 0.8f);
            SetBorderColor(m_voidObstacleSwatch, barrierWall);
            SyncTool();
            // Keyboard shortcuts for the whole window. Owner: the root, which outlives a GUI rebuild; the unregister
            // keeps one handler when CreateGUI runs again. Stop point: the root is dropped with the window.
            root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            // Every press in the window, first of all: the safety stop of a shown solution (contract Revision 2), and it
            // forgets a noted canvas press whose release never reached the canvas; the canvas slot, further down the
            // same trickle, notes a new canvas press again. Owner, stop point and the unregister as for the key handler
            // above.
            root.UnregisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);

            RefreshLevelSelection();
        }

        // Back from the Piece Editor: Piece Type and the shape tiles show the library as it is now.
        private void OnFocus()
        {
            RefreshBlockTools();
        }

        private void OnDisable()
        {
            // A window close or a domain reload stops a playing solution, which unsubscribes its update callback. The
            // publish its stop schedules would be dropped below, so the scene preview gets the draft back at once.
            if (StopSolutionPlayback())
            {
                PublishDraft();
            }

            Undo.undoRedoPerformed -= OnUndoRedo;
            PieceEditorWindow.LevelPieceChanged -= OnLevelPieceChanged;
            ShapeLibraryAuthoring.LibraryCommitted -= OnLibraryCommitted;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            // A close or a domain reload stops a background Validate Level run and waits for its worker thread, so no
            // search outlives the window or its assemblies.
            m_validationFlow.RunFinished -= OnValidationRunFinished;
            if (m_pollsValidation)
            {
                RemoveValidationUpdate(PollValidationRun);
                m_pollsValidation = false;
            }

            m_validationFlow.Shutdown();
            // Pending library pieces alone keep no draft: they live in the serialized piece session.
            if (hasUnsavedChanges && HasDraftChanges())
            {
                // A window close already asked Save/Discard/Cancel and cleared the flag; only a script, domain or
                // layout reload gets here with pending edits. The same window restores the kept draft in its next
                // level load; a layout reload recreates the window, so that draft is dropped with the session.
                KeepDraftForReload();
            }

            EditorApplication.update -= PublishDraft;
            m_publishScheduled = false;
            // A LevelEditor scene opening that a canvas press scheduled never outlives the window.
            EditorApplication.update -= OpenSceneAfterPress;
            m_sceneOpenScheduled = false;
            m_scenePressPointerId = -1;
            DestroyDraft();
            // With no draft left, the Effect Editor hands over to another open Level Editor or says none is open.
            SelectionChanged?.Invoke(this);
        }

        /// <summary>
        /// Unity calls this from the Save/Discard/Cancel prompt when a window with unsaved changes closes.
        /// A failed save keeps the flag set, so the window stays open.
        /// </summary>
        public override void SaveChanges()
        {
            // Save Level also retries the pending template commits; one still pending keeps the window open, including a
            // template footprint of another level database, which only that database's Save Level writes.
            if (SaveLevel() && m_pieceSession.CountPendingTemplates() == 0)
            {
                base.SaveChanges();
            }
        }

        /// <summary>
        /// The window's unsaved state for Unity's close prompt: the draft differs from the saved level, or template
        /// footprints Save Level wrote to the levels of any level database are not in the piece library yet, as after a
        /// failed Save Level commit. Closing then asks Save/Discard/Cancel, and Save retries the active database's
        /// commits. Level pieces are saved with their level and never wait for the piece library (DEC-180).
        /// </summary>
        private void UpdateUnsavedChanges()
        {
            bool draftChanged = HasDraftChanges();
            int templates = CountPendingTemplates();
            int elsewhere = m_pieceSession.CountPendingTemplates() - templates;
            hasUnsavedChanges = draftChanged || templates > 0 || elsewhere > 0;
            UpdateRestoreButton(draftChanged);
            string message = draftChanged ? k_draftChangesMessage : null;
            if (templates > 0)
            {
                message = JoinSentences(message, $"{templates} template footprint(s) are not in the piece library " +
                                                 "yet. Save Level retries writing them.");
            }

            if (elsewhere > 0)
            {
                string database = GetDatabaseName(m_pieceSession.FindOtherPendingDatabase(m_saveController.LoadedGuid));
                message = JoinSentences(message, $"{elsewhere} template footprint(s) of {database} are not in the " +
                                                 "piece library yet. Open that level database and Save Level to " +
                                                 "write them.");
            }

            saveChangesMessage = message ?? k_draftChangesMessage;
        }

        private static string JoinSentences(string first, string second)
        {
            return first != null ? first + " " + second : second;
        }

        // Template footprints Save Level wrote to the active database's levels that the piece library still lacks.
        private int CountPendingTemplates()
        {
            return m_pieceSession.GetPendingTemplates(m_saveController.LoadedGuid).Count;
        }

        // A level database's file name by its JSON GUID; an unknown GUID is shown as it is.
        private string GetDatabaseName(string guid)
        {
            for (int index = 0; m_entries != null && index < m_entries.Count; index++)
            {
                if (string.Equals(m_entries[index].Guid, guid, StringComparison.Ordinal))
                {
                    return m_entries[index].Name;
                }
            }

            return guid;
        }

        private VisualElement CreateSelectionPane()
        {
            VisualElement pane = new VisualElement { name = "selection-pane" };
            pane.style.minWidth = k_rightPaneMinWidth;
            pane.style.flexGrow = 1;

            VisualElement topBar = new VisualElement { name = "top-bar" };
            topBar.style.flexShrink = 0;
            topBar.style.paddingLeft = 8;
            topBar.style.paddingRight = 8;
            topBar.style.paddingTop = 6;
            topBar.style.paddingBottom = 6;
            topBar.style.borderBottomWidth = 1;
            topBar.style.borderBottomColor = new Color(0f, 0f, 0f, 0.35f);
            pane.Add(topBar);

            m_databaseChoiceField = new DropdownField("Level Database");
            m_databaseChoiceField.RegisterValueChangedCallback(change =>
            {
                if (m_refreshing)
                {
                    return;
                }

                int nextIndex = m_databaseChoiceField.index
                    - (m_hasDatabasePlaceholder ? 1 : 0);
                if (nextIndex < 0 || nextIndex >= m_entries.Count)
                {
                    RefreshLevelSelection();
                    return;
                }

                LevelDatabaseAddressables.Entry selected = m_entries[nextIndex];

                if (!AskToLeaveDraft())
                {
                    // Cancel, or a declined or failed save (DEC-144): the dropdown goes back and the draft keeps its
                    // edits and the save's status, as the Target Level field does. A reload would replace the draft.
                    m_databaseChoiceField.SetValueWithoutNotify(change.previousValue);
                    return;
                }

                // Only a database's own Save Level retries its template footprints, so leaving it says so.
                string leftName = GetDatabaseName(m_saveController.LoadedGuid);
                int leftTemplates = CountPendingTemplates();
                if (!LevelDatabaseAddressables.TrySetActive(
                        m_projectScope, selected.Guid, out string error))
                {
                    RefreshLevelSelection();
                    ShowError(error);
                    return;
                }

                m_selectedIndex = -1;
                string note = RefreshLevelSelection();
                SetStatus($"Active game database: {selected.Name}." + note + (leftTemplates > 0
                    ? $" {leftTemplates} template footprint(s) of {leftName} are still not in the piece library; " +
                      "switch back and Save Level to write them."
                    : string.Empty));
            });

            m_sourceField = new ObjectField("JSON Source")
            {
                objectType = typeof(TextAsset),
                allowSceneObjects = false
            };
            m_sourceField.SetEnabled(false);
            m_reloadButton = CreateIconButton(
                "refresh-json", "Refresh", "⟳", "Reload JSON: read the active JSON source from disk again.",
                ReloadDatabase);
            m_restoreButton = CreateIconButton(
                "restore-backup", "UndoHistory", "⤺", k_restoreTooltip, RestoreLevel);

            // Add and Delete act on the database list, so they sit beside its dropdown; Add asks for the name in a popup.
            FieldActionsRow databaseField = new FieldActionsRow(m_databaseChoiceField);
            m_createDatabaseButton = databaseField.AddCreateButton(
                "create-database",
                "New Level Database: create an empty level database (a folder with its manifest) and make it the "
                + "active game database.",
                () => CreateDatabasePopup().Show(m_createDatabaseButton));
            m_deleteDatabaseButton = databaseField.AddDeleteButton(
                "delete-database",
                "Delete the selected Level Database folder and every level in it. The default database is protected.",
                DeleteDatabase);

            VisualElement sourceRow = MakeRow("database-row");
            AddStretched(sourceRow, databaseField, 1f, k_topLabelWidth);
            AddStretched(sourceRow, m_sourceField, 1f, k_topLabelWidth);
            sourceRow.Add(m_reloadButton);
            sourceRow.Add(m_restoreButton);
            topBar.Add(sourceRow);

            m_targetField = new DropdownField("Target Level");
            SetLabelWidth(m_targetField, k_topLabelWidth);
            SearchableDropdown.Enable(m_targetField);
            m_targetField.RegisterValueChangedCallback(change =>
            {
                if (m_refreshing || !m_saveController.SourceValid || Levels.Count == 0)
                {
                    return;
                }

                int nextIndex = m_targetField.index;
                if (nextIndex == m_selectedIndex)
                {
                    return;
                }

                if (!AskToLeaveDraft())
                {
                    m_targetField.SetValueWithoutNotify(m_targetField.choices[m_selectedIndex]);
                    return;
                }

                m_selectedIndex = nextIndex;
                LoadSelectedDraft();
                // A save on the way out rebuilt the choices inside this callback while the old level was still
                // selected (OnSaveProgress), so the field is set again to the level that opened.
                if (m_selectedIndex < m_targetField.choices.Count)
                {
                    m_targetField.SetValueWithoutNotify(m_targetField.choices[m_selectedIndex]);
                }
            });
            m_targetStepper = new ChoiceStepper(m_targetField, "Previous level", "Next level");
            m_createLevelButton = m_targetStepper.AddCreateButton("create-level", "New level", CreateLevel);
            m_deleteLevelButton = m_targetStepper.AddDeleteButton("delete-level", "Delete selected level", DeleteLevel);

            // Delayed: one commit per edit, so one undo step and one preview publish.
            m_levelNameField = new TextField("Level Name") { name = "level-name", isDelayed = true };
            m_levelNameField.RegisterValueChangedCallback(change => SetDraftString("m_level.m_name", change.newValue));

            VisualElement levelRow = MakeRow("level-row");
            AddStretched(levelRow, m_targetStepper, 3f, 0f);
            AddStretched(levelRow, m_levelNameField, 2f, 80f);
            AddTimerField(levelRow);
            topBar.Add(levelRow);

            // Directly under the top bar: a template edit's reach, in view while the Pen draws (DEC-125). It does not
            // take the keyboard focus from the canvas.
            pane.Add(CreateTemplateBanner());
            pane.Add(CreateResizeConfirm());
            pane.Add(CreateShapeBanner());

            VisualElement canvasSlot = new VisualElement { name = "canvas-slot" };
            m_canvasSlot = canvasSlot;
            canvasSlot.style.flexGrow = 1;
            canvasSlot.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
            m_canvas = new LevelCanvasElement();
            m_canvas.SetPalette(AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath));
            // Owner: this window's GUI; stop point: the canvas is dropped with it (root.Clear or window close).
            m_canvas.SelectionRequested += SelectOnCanvas;
            m_canvas.EmptySpaceClicked += ClearSelectionOnEmptySpace;
            m_canvas.PlaceRequested += PlaceBlock;
            m_canvas.MoveRequested += MoveBlock;
            m_canvas.TransformRequested += TransformClickedBlock;
            m_canvas.RemoveRequested += RemoveBlock;
            m_canvas.DoorStrokeRequested += PaintDoors;
            m_canvas.DoorRemoveRequested += RemoveDoor;
            m_canvas.DoorSegmentRemoveRequested += RemoveDoorSegment;
            m_canvas.ResizeRequested += RequestResize;
            m_canvas.CornerRequested += CutOrFillCorner;
            m_canvas.ObstacleStrokeRequested += ApplyObstacleStroke;
            m_canvas.PieceStrokeRequested += PlacePieceStroke;
            m_canvas.PieceCellRemoveRequested += RemovePieceCell;
            m_canvas.PenBlockPicked += EditBlockWithPen;
            m_canvas.GateCornerRefused += () => SetStatus("A gate cannot sit on a corner; paint it along one side.");
            // While a solution shows, playing or paused, a press anywhere on the canvas only stops it and reaches no
            // canvas tool, so it edits nothing. Any other press is noted, and when it ends a designer's window may open
            // the LevelEditor scene (contract Revision 1). A press ends at its pointer up. An element that captured the
            // pointer receives that up alone: the canvas during a drag, stroke or pan, so the canvas itself reports it
            // too; one of the canvas's buttons, whose release of the pointer reports it. The canvas's own capture loss
            // is no end: Esc or a new model can cancel a drag while the button is still held, and the canvas's pointer
            // up still ends that press. Each report of the same press after the first finds no noted press. Owner: the
            // canvas slot and the canvas; stop point: they are dropped with the window GUI.
            canvasSlot.RegisterCallback<PointerDownEvent>(OnCanvasPressStarted, TrickleDown.TrickleDown);
            canvasSlot.RegisterCallback<PointerUpEvent>(evt => EndCanvasPress(evt.pointerId), TrickleDown.TrickleDown);
            m_canvas.RegisterCallback<PointerUpEvent>(evt => EndCanvasPress(evt.pointerId));
            canvasSlot.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (!ReferenceEquals(evt.target, m_canvas))
                {
                    EndCanvasPress(evt.pointerId);
                }
            }, TrickleDown.TrickleDown);
            canvasSlot.Add(m_canvas);
            canvasSlot.Add(CreateHintBox());
            pane.Add(canvasSlot);

            m_issuesList = new LevelEditorIssuesList();
            // Owner: this window's GUI; stop point: the list is dropped with it.
            m_issuesList.IssueSelected += issue => m_canvas?.SetIssueHighlight(issue);
            pane.Add(m_issuesList);

            // Holds the whole-draft change tracker. Owner: this element; stop point: Unbind on the next draft load.
            m_draftTracker = new VisualElement { name = "draft-tracker" };
            pane.Add(m_draftTracker);
            return pane;
        }

        // Color Switch and Color Shuffle: each recolours the level on the board as one undo step and leaves the Colors
        // swatches, badges and Pen keys as they are (user decisions of 2026-09-26 and 2026-09-27,
        // TASK-20260926-145804549-da16f02f; Reset Order was removed). The row sits under the swatches in the Colors
        // group; the buttons share its width and wrap to a second line when the tools pane is too narrow.
        private VisualElement CreateColorOrderRow()
        {
            VisualElement row = MakeRow("color-order-actions");
            row.style.flexWrap = Wrap.Wrap;
            m_colorSwitchButton = CreateActionButton("color-switch", LevelEditorIcons.k_colorSwitch, "Color Switch",
                "Color Switch: click a block or gate first. Its colour trades places with a random other colour, one " +
                "the level does not use when there is one, in the whole level (blocks, gates and effect colours), so " +
                "every block of that colour changes. Every block keeps matching its gate. One undo step.",
                SwitchColor);
            m_colorShuffleButton = CreateActionButton("color-shuffle", LevelEditorIcons.k_colorShuffle, "Color Shuffle",
                "Color Shuffle: gives the level's colours a random new mix on the board, one colour for another. Every " +
                "block keeps matching its gate and the layout stays. One undo step.",
                ShuffleColors);
            row.Add(m_colorSwitchButton);
            row.Add(m_colorShuffleButton);
            return row;
        }

        // A button with one of the Level Editor's white icons before its name, for the colour order actions; it grows
        // with its row but never below its content. A missing icon leaves the name alone.
        private static Button CreateActionButton(string name, string iconName, string text, string tooltip, Action action)
        {
            Button button = new Button(action) { name = name, tooltip = tooltip };
            button.style.flexGrow = 1;
            button.style.flexShrink = 0;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.height = k_actionButtonHeight;
            button.style.marginLeft = 0;
            button.style.marginRight = 2;
            button.style.marginBottom = 2;
            // Tight padding keeps the three names on one line of a 330 px tools pane even while its scrollbar shows
            // (a 289 px row); a narrower pane wraps them.
            button.style.paddingLeft = 3;
            button.style.paddingRight = 3;
            Texture2D icon = LevelEditorIcons.Load(iconName);
            if (icon != null)
            {
                Image image = new Image
                {
                    image = icon,
                    tintColor = LevelEditorIcons.NeutralTint,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.style.width = k_actionIconSize;
                image.style.height = k_actionIconSize;
                image.style.flexShrink = 0;
                image.style.marginRight = 3;
                button.Add(image);
            }

            Label label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            label.style.fontSize = k_actionFontSize;
            button.Add(label);
            return button;
        }

        // The info box over the top of the canvas that tells the designer what to do next (user request of
        // 2026-09-26). It floats over the board's empty top margin, so showing it never moves the board under the
        // pointer, and it ignores the pointer, so clicks reach the canvas.
        private VisualElement CreateHintBox()
        {
            m_hintBox = new HelpBox(string.Empty, HelpBoxMessageType.Info) { name = k_hintBoxName };
            m_hintBox.style.position = Position.Absolute;
            m_hintBox.style.top = 6;
            m_hintBox.style.left = 8;
            m_hintBox.style.right = 8;
            m_hintBox.style.display = DisplayStyle.None;
            m_hintBox.Query().ForEach(element => element.pickingMode = PickingMode.Ignore);
            return m_hintBox;
        }

        // A how-to hint, such as the answer to a Pen click that could not act; shown until the next status or tool
        // change, and hidden by null.
        private void ShowHint(string hint)
        {
            m_hint = hint;
            if (m_hintBox == null)
            {
                return;
            }

            m_hintBox.text = hint ?? string.Empty;
            m_hintBox.style.display = hint != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // The warning while the Pen edits a template or a template footprint waits for Save Level: how many blocks the
        // change reaches here and in the database's other levels, the save's conflicts, and Discard (DEC-125).
        private VisualElement CreateTemplateBanner()
        {
            m_templateBanner = MakeRow("template-banner");
            m_templateBanner.style.alignItems = Align.Center;
            m_templateBanner.style.marginTop = 0;
            m_templateBanner.style.paddingLeft = 6;
            m_templateBanner.style.paddingRight = 6;
            m_templateBanner.style.paddingTop = 2;
            m_templateBanner.style.paddingBottom = 2;
            m_templateBanner.style.backgroundColor = new Color(0.12f, 0.38f, 0.55f, 0.45f);
            m_templateBanner.style.display = DisplayStyle.None;
            m_templateBannerText = new Label { name = "template-banner-text" };
            m_templateBannerText.style.flexGrow = 1;
            m_templateBannerText.style.flexShrink = 1;
            m_templateBannerText.style.whiteSpace = WhiteSpace.Normal;
            m_templateBanner.Add(m_templateBannerText);
            m_discardTemplateButton = new Button(DiscardTemplateEdits)
            {
                name = "template-banner-discard",
                text = "Discard template change",
                tooltip = "Give this level's blocks of the changed template their library footprint again, in their " +
                          "own rotation or mirror (one undo step). The piece library is not changed."
            };
            m_discardTemplateButton.style.flexShrink = 0;
            m_templateBanner.Add(m_discardTemplateButton);
            m_separateTemplateButton = new Button(MakeSeparatePiece)
            {
                name = "template-banner-separate",
                text = "Make a separate piece",
                tooltip = "Keep the block the Pen edits as a new level piece of its own; the template's other blocks go " +
                          "back where they were and the template does not change (one undo step)."
            };
            m_separateTemplateButton.style.flexShrink = 0;
            m_templateBanner.Add(m_separateTemplateButton);
            m_keepTemplateButton = new Button(KeepLibraryFootprints)
            {
                name = "template-banner-keep",
                text = "Keep library footprint",
                tooltip = "Stop writing the saved template footprint to the piece library, which keeps its own. The " +
                          "saved levels keep their blocks, which are level pieces then until you Re-stamp them in " +
                          "each level. This is not an undo step."
            };
            m_keepTemplateButton.style.flexShrink = 0;
            m_templateBanner.Add(m_keepTemplateButton);
            return m_templateBanner;
        }

        // The inline question for a shrink that removes content, so the hatch stays visible while the designer decides.
        private VisualElement CreateResizeConfirm()
        {
            m_resizeConfirm = MakeRow("resize-confirm");
            m_resizeConfirm.style.alignItems = Align.Center;
            m_resizeConfirm.style.paddingLeft = 6;
            m_resizeConfirm.style.paddingRight = 6;
            m_resizeConfirm.style.backgroundColor = new Color(0.55f, 0.16f, 0.12f, 0.45f);
            m_resizeConfirm.style.display = DisplayStyle.None;
            m_resizeConfirmText = new Label { name = "resize-confirm-text" };
            m_resizeConfirmText.style.flexGrow = 1;
            m_resizeConfirmText.style.whiteSpace = WhiteSpace.Normal;
            m_resizeConfirm.Add(m_resizeConfirmText);
            m_resizeConfirm.Add(new Button(ConfirmResize)
            {
                name = "resize-confirm-apply",
                text = "Apply",
                tooltip = "Resize the board and remove or shorten the hatched content (one undo step)."
            });
            m_resizeConfirm.Add(new Button(CancelResize)
            {
                name = "resize-confirm-cancel",
                text = "Cancel",
                tooltip = "Keep the board as it is (Esc)."
            });
            return m_resizeConfirm;
        }

        // Refused level-piece edits and the Re-stamp offer for blocks that still have a template's earlier footprint.
        private VisualElement CreateShapeBanner()
        {
            m_shapeBanner = MakeRow("shape-banner");
            m_shapeBanner.style.alignItems = Align.Center;
            m_shapeBanner.style.paddingLeft = 6;
            m_shapeBanner.style.paddingRight = 6;
            m_shapeBanner.style.backgroundColor = new Color(0.55f, 0.42f, 0.1f, 0.45f);
            m_shapeBanner.style.display = DisplayStyle.None;
            m_shapeBannerText = new Label { name = "shape-banner-text" };
            m_shapeBannerText.style.flexGrow = 1;
            m_shapeBannerText.style.flexShrink = 1;
            m_shapeBannerText.style.whiteSpace = WhiteSpace.Normal;
            m_shapeBanner.Add(m_shapeBannerText);
            m_restampButton = new Button(RestampChangedPieces)
            {
                name = "shape-banner-restamp",
                text = "Re-stamp",
                tooltip = "Give this level's blocks with a template's earlier footprint the template's footprint now, " +
                          "in their own rotation or mirror (one undo step). Nothing changes if any block would leave " +
                          "the board; blocks left overlapping are struck through until you move them apart."
            };
            m_restampButton.style.flexShrink = 0;
            m_shapeBanner.Add(m_restampButton);
            return m_shapeBanner;
        }

        // The split's fixed pane (user order of 2026-09-28, TASK-20260928-091128541-cb48c84a): the tool groups scroll
        // as before, and the validation panel stays pinned under them, so it never scrolls with the groups.
        private VisualElement CreateToolsColumn()
        {
            // A vertical split (user request of 2026-09-28): the same Unity splitter as the Effect Constraints window,
            // with its resize cursor, trades height between the tool groups and the validation panel (the fixed pane).
            // Its content keeps the groups at index 0 and the panel at index 1.
            float storedHeight = m_isDesignerWindow ? EditorPrefs.GetFloat(k_validationHeightPrefKey, 0f) : 0f;
            TwoPaneSplitView column = new TwoPaneSplitView(1,
                storedHeight >= k_validationMinHeight ? storedHeight : k_validationDefaultHeight,
                TwoPaneSplitViewOrientation.Vertical) { name = "tools-column" };
            column.style.minWidth = k_toolsMinWidth;
            VisualElement tools = CreateToolsPane();
            tools.style.minHeight = k_toolsMinVisibleHeight;
            column.Add(tools);
            VisualElement panel = CreateValidationPanel();
            panel.style.minHeight = k_validationMinHeight;
            column.Add(panel);
            // Stores the height only when a splitter drag ends, as the tools width does. Owner: the handle; stop point:
            // the handle is dropped with the window GUI.
            GetOwnSplitterHandle(column)?.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (m_isDesignerWindow && panel.layout.height >= k_validationMinHeight)
                {
                    EditorPrefs.SetFloat(k_validationHeightPrefKey, panel.layout.height);
                }
            });
            return column;
        }

        // Validate Level's summary and listed solutions, the Auto Play and Validate Before Save toggles, then Save Level,
        // Validate Level and Test Level (Stop Test while Play Mode runs), which left the bottom bar for this panel; the
        // bottom bar keeps the status line.
        private VisualElement CreateValidationPanel()
        {
            m_saveLevelButton = CreateBottomButton("save-level", "Save Level", k_saveTooltip, () => { SaveLevel(); });
            m_validateLevelButton = CreateBottomButton("validate-level", k_validateText, k_validateTooltip,
                ValidateLevel);
            m_testLevelButton = CreateBottomButton("test-level", k_testText, k_testTooltip, TestOrStopLevel);
            m_openSceneButton = CreateBottomButton("open-level-editor-scene", k_openSceneText, k_openSceneTooltip,
                OpenSceneFromButton);
            m_validationPanel = new LevelValidationPanel(m_saveLevelButton, m_validateLevelButton, m_testLevelButton,
                m_openSceneButton);
            m_validationPanel.SetAutoPlay(m_autoPlaysSolutions);
            m_validationPanel.SetValidateBeforeSave(m_checksSolvabilityOnSave);
            // Owner: this window's GUI; stop point: the panel is dropped with it.
            m_validationPanel.SolutionClicked += PlaySolution;
            m_validationPanel.PauseResumeClicked += PauseOrResumeSolution;
            m_validationPanel.StepClicked += StepSolution;
            m_validationPanel.StopClicked += StopSolution;
            m_validationPanel.AutoPlayChanged += SetAutoPlay;
            m_validationPanel.ValidateBeforeSaveChanged += SetValidateBeforeSave;
            return m_validationPanel;
        }

        // The designer's Auto Play choice; a designer's window also keeps it in EditorPrefs for its next Open.
        private void SetAutoPlay(bool on)
        {
            m_autoPlaysSolutions = on;
            if (m_isDesignerWindow)
            {
                EditorPrefs.SetBool(k_autoPlayPrefKey, on);
            }
        }

        // The designer's Validate Before Save choice; a designer's window also keeps it in EditorPrefs for its next Open.
        private void SetValidateBeforeSave(bool on)
        {
            m_checksSolvabilityOnSave = on;
            if (m_isDesignerWindow)
            {
                EditorPrefs.SetBool(k_validateBeforeSavePrefKey, on);
            }
        }

        private VisualElement CreateToolsPane()
        {
            ScrollView tools = new ScrollView { name = "tools-pane" };
            tools.style.minWidth = k_toolsMinWidth;
            tools.contentContainer.style.paddingLeft = 6;
            tools.contentContainer.style.paddingRight = 6;
            tools.contentContainer.style.paddingTop = 4;

            tools.Add(CreateToolsGroup());
            // Undo, Redo and the nudge arrows sit right under the tool and transform rows (user request of 2026-09-26).
            tools.Add(CreateEditGroup());
            tools.Add(CreateColorsGroup());
            // Gates use the Pen on the board edge; obstacles are separate from the board boundary.
            tools.Add(CreateBlocksGroup());
            tools.Add(CreateObstaclesGroup());
            return tools;
        }

        private static VisualElement GetOwnSplitterHandle(TwoPaneSplitView split)
        {
            for (int index = 0; index < split.hierarchy.childCount; index++)
            {
                VisualElement child = split.hierarchy[index];
                if (child.name == k_splitterHandleName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SaveDraggedToolsWidth(VisualElement fixedPane)
        {
            // The splitter sets the fixed pane's inline width on every drag step; layout can lag one frame behind.
            StyleLength dragged = fixedPane.style.width;
            float width = dragged.keyword == StyleKeyword.Undefined ? dragged.value.value : fixedPane.layout.width;
            if (width >= k_toolsMinWidth)
            {
                EditorPrefs.SetFloat(k_toolsWidthPrefKey, width);
            }
        }

        private Foldout CreateBlocksGroup()
        {
            Foldout group = CreateGroup("blocks-group", "Blocks");
            m_pieceEditController.Library = BlockShapeLibrary.LoadProject();

            m_shapePalette = new LevelShapePalette();
            // A rebuilt GUI has empty tiles, whatever catalog the previous palette showed.
            m_paletteCatalog = null;
            // Owner: this window's GUI; stop point: the palette is dropped with it.
            m_shapePalette.ShapePicked += ArmShape;
            m_shapePalette.ShapeDeleteRequested += DeleteShape;
            m_shapePalette.CustomPieceDeleteRequested += DeleteCustomPiece;
            m_shapePalette.CustomPiecePicked += ArmCustomPiece;
            m_shapePalette.PieceCreateRequested += CreateNamedPiece;
            group.Add(m_shapePalette);

            m_blockChoiceField = new DropdownField("Selected Block");
            SetLabelWidth(m_blockChoiceField, k_toolsLabelWidth);
            m_blockChoiceField.RegisterValueChangedCallback(change =>
            {
                m_selectedBlockIndex = m_blockChoiceField.index;
                m_blockChosen = m_selectedBlockIndex >= 0;
                // A block picked here replaces a door selected on the canvas; RefreshBlockTools syncs the canvas.
                m_canvasSelection = LevelCanvasSelection.None;
                // Only a Pen click starts an edit (user revision of 2026-09-26), so another block chosen here ends one.
                if (!TryGetSelectedBlock(out _, out LevelBlockData chosen)
                    || !string.Equals(chosen.Id, m_pieceEditController.PenEditBlockId, StringComparison.Ordinal))
                {
                    m_pieceEditController.EndPenEdit();
                }

                RefreshBlockTools();
            });
            group.Add(m_blockChoiceField);

            m_pieceTypeField = new DropdownField("Piece Type");
            SetLabelWidth(m_pieceTypeField, k_toolsLabelWidth);
            SearchableDropdown.Enable(m_pieceTypeField);
            m_pieceTypeField.RegisterValueChangedCallback(_ => ApplyPieceType());
            group.Add(m_pieceTypeField);
            // Hidden on the user's request of 2026-09-26: the canvas outline shows the selection. Since
            // TASK-20260926-170647550-7683988f the Effect Editor shows effects only, so no window shows Piece Type.
            // Both fields still mirror the selection, which the Level tests read.
            m_blockChoiceField.style.display = DisplayStyle.None;
            m_pieceTypeField.style.display = DisplayStyle.None;
            return group;
        }

        private Foldout CreateObstaclesGroup()
        {
            Foldout group = CreateGroup("obstacles-group", "Obstacles");
            VisualElement tiles = MakeToolRow("obstacle-tiles");
            // Only the official Barrier (user decision of 2026-09-25); its startsOpen param belongs to the Effect Editor.
            m_barrierObstacleButton = CreateObstacleButton("barrier", "Barrier", k_barrierTooltip, ArmObstacleBarrier,
                out m_barrierObstacleSwatch);
            // Hatched like the canvas overlay. Owner: the swatch; stop point: it is dropped with the window GUI.
            m_barrierObstacleSwatch.generateVisualContent += DrawBarrierSwatchHatch;
            tiles.Add(m_barrierObstacleButton);
            // Void cuts the board outline (holes and edge notches, like reference 02); the walls follow automatically.
            m_voidObstacleButton = CreateObstacleButton("void", "Void", k_voidTooltip, ArmObstacleVoid,
                out m_voidObstacleSwatch);
            // An empty cell framed by the wall colour, the look of a void on the canvas.
            m_voidObstacleSwatch.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            m_voidObstacleSwatch.style.borderLeftWidth = 2;
            m_voidObstacleSwatch.style.borderRightWidth = 2;
            m_voidObstacleSwatch.style.borderTopWidth = 2;
            m_voidObstacleSwatch.style.borderBottomWidth = 2;
            tiles.Add(m_voidObstacleButton);
            group.Add(tiles);
            return group;
        }

        private static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }

        // Light diagonal lines over the Barrier tile's swatch, the look of a closed barrier on the canvas.
        private static void DrawBarrierSwatchHatch(MeshGenerationContext context)
        {
            Rect rect = context.visualElement.contentRect;
            Painter2D painter = context.painter2D;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.45f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            for (float offset = 6f; offset < rect.width + rect.height; offset += 6f)
            {
                painter.MoveTo(new Vector2(rect.xMin + Mathf.Max(0f, offset - rect.height), rect.yMin + Mathf.Min(offset, rect.height)));
                painter.LineTo(new Vector2(rect.xMin + Mathf.Min(offset, rect.width), rect.yMin + Mathf.Max(0f, offset - rect.width)));
            }

            painter.Stroke();
        }

        private static Button CreateObstacleButton(string id, string title, string tooltip, Action clicked,
            out VisualElement swatch)
        {
            var button = new Button(clicked) { name = "obstacle-" + id, tooltip = tooltip };
            button.style.flexShrink = 0;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.width = k_obstacleTileWidth;
            button.style.marginLeft = 0;
            button.style.marginRight = 4;
            button.style.marginTop = 0;
            button.style.marginBottom = 4;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 2;
            button.style.borderLeftWidth = 2;
            button.style.borderRightWidth = 2;
            button.style.borderTopWidth = 2;
            button.style.borderBottomWidth = 2;
            swatch = new VisualElement { name = "obstacle-" + id + "-swatch", pickingMode = PickingMode.Ignore };
            swatch.style.width = k_obstacleSwatchSize;
            swatch.style.height = k_obstacleSwatchSize;
            swatch.style.borderTopLeftRadius = 3;
            swatch.style.borderTopRightRadius = 3;
            swatch.style.borderBottomLeftRadius = 3;
            swatch.style.borderBottomRightRadius = 3;
            button.Add(swatch);
            var label = new Label(title) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 10;
            label.style.marginTop = 2;
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            button.Add(label);
            return button;
        }

        // Seven exclusive canvas tools as icon buttons: Select, Pen and Eraser (keys 1-3), then under a divider the
        // rotate and mirror tools, which act on the clicked block without a prior selection (user request of
        // 2026-09-25).
        private Foldout CreateToolsGroup()
        {
            Foldout group = CreateGroup("tools-group", "Tools");
            VisualElement toolRow = MakeToolRow("canvas-tools");
            m_selectToolButton = CreateToolButton("tool-select", LevelEditorIcons.k_cursor, "Select",
                "Select (1): click a block or door to select it, drag a block to move it. With a piece picked in Blocks, " +
                "click a free cell to place it; Esc or Select drops the piece. Right click removes with any tool.",
                () => SetTool(LevelEditorTool.Select));
            m_penToolButton = CreateToolButton("tool-pen", LevelEditorIcons.k_pen, "Pen",
                "Pen (2): click or drag on the wall ring to paint a gate (1 to 3 cells) in the selected colour. " +
                "Click a block to edit it: click or drag on its green cells to add them, right click takes one off, " +
                "and a swatch recolours it. With the Pen, keys 1-9, 0 and - pick the colour whose Colors badge shows " +
                "that number (0 is 10, - is 11). Editing a template changes all its blocks, and Save Level writes it " +
                "to the piece library. + New piece draws a new one. A right click on a gate takes one gate cell off; " +
                "the Eraser removes the whole gate. " +
                "With Select, click a gate, then a swatch recolours it.",
                () => SetTool(LevelEditorTool.Pen));
            m_eraserToolButton = CreateToolButton("tool-eraser", LevelEditorIcons.k_eraser, "Eraser",
                "Eraser (3): click a block or a gate to remove it; click or drag over walls to restore floor.",
                () => SetTool(LevelEditorTool.Eraser));
            toolRow.Add(m_selectToolButton);
            toolRow.Add(m_penToolButton);
            toolRow.Add(m_eraserToolButton);
            group.Add(toolRow);

            VisualElement divider = new VisualElement { name = "tools-divider" };
            divider.style.height = 1;
            divider.style.marginTop = 6;
            divider.style.marginBottom = 2;
            divider.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            group.Add(divider);

            // The keys stay immediate: R / F act on the armed piece's ghost when a piece is armed, else the selected block.
            // A button acts on an armed piece at once and keeps it armed; otherwise it picks up its tool.
            VisualElement transforms = MakeToolRow("block-actions");
            m_rotateCounterClockwiseButton = CreateToolButton("rotate-block-ccw", LevelEditorIcons.k_rotateLeft, "↺",
                "Rotate counter-clockwise: click a block to turn it. With a piece armed, turns that piece and keeps it "
                + "armed. Shift+R turns the armed piece, else the selected block.",
                () => SetTool(LevelEditorTool.RotateCounterClockwise));
            m_rotateClockwiseButton = CreateToolButton("rotate-block-cw", LevelEditorIcons.k_rotateRight, "↻",
                "Rotate clockwise: click a block to turn it. With a piece armed, turns that piece and keeps it armed. "
                + "R turns the armed piece, else the selected block.",
                () => SetTool(LevelEditorTool.RotateClockwise));
            m_flipBlockButton = CreateToolButton("flip-block-h", LevelEditorIcons.k_reflectHorizontal, "H",
                "Mirror horizontally: click a block to mirror it. With a piece armed, mirrors that piece and keeps it "
                + "armed. F mirrors the armed piece, else the selected block.",
                () => SetTool(LevelEditorTool.MirrorHorizontal));
            m_flipVerticalButton = CreateToolButton("flip-block-v", LevelEditorIcons.k_reflectVertical, "V",
                "Mirror vertically: click a block to mirror it. With a piece armed, mirrors that piece and keeps it "
                + "armed. Shift+F mirrors the armed piece, else the selected block.",
                () => SetTool(LevelEditorTool.MirrorVertical));
            transforms.Add(m_rotateCounterClockwiseButton);
            transforms.Add(m_rotateClockwiseButton);
            transforms.Add(m_flipBlockButton);
            transforms.Add(m_flipVerticalButton);
            group.Add(transforms);
            return group;
        }

        private Foldout CreateColorsGroup()
        {
            Foldout group = CreateGroup("colors-group", "Colors");
            group.tooltip = "Colour for new blocks and gates. It also recolours a block or gate you clicked with " +
                            "Select, or the block the Pen edits. [ / ] cycles. With the Pen, keys 1-9, 0 and - pick " +
                            "the colour whose badge shows that number (0 is 10, - is 11).";
            m_colorSwatches = new LevelColorSwatches();
            // Owner: this window's GUI; stop point: the swatches are dropped with it.
            m_colorSwatches.ColorPicked += PickSwatch;
            group.Add(m_colorSwatches);
            group.Add(CreateColorOrderRow());
            return group;
        }

        private Foldout CreateEditGroup()
        {
            Foldout group = CreateGroup("edit-group", "Edit");

            VisualElement historyRow = MakeRow("edit-history");
            AddStretched(historyRow, new Button(UndoEdit) { name = "undo", text = "Undo", tooltip = "Undo (Ctrl+Z)." },
                1f, 0f);
            AddStretched(historyRow, new Button(RedoEdit)
            {
                name = "redo",
                text = "Redo",
                tooltip = "Redo (Ctrl+Y or Ctrl+Shift+Z)."
            }, 1f, 0f);
            group.Add(historyRow);
            // No nudge buttons: the arrow keys move the selected block (user request of 2026-09-26). No Delete
            // button: the Eraser, a right click and the Delete key remove a block or gate (user request).
            return group;
        }

        // The level timer beside Level Name: delayed and unbound like the name, clamped to 10-300 on commit, with its
        // m:ss reading refilled from the draft (SyncDraftFields).
        private void AddTimerField(VisualElement row)
        {
            m_timerField = new IntegerField("Timer")
            {
                name = "timer-seconds",
                isDelayed = true,
                tooltip = "Level time limit, 10-300 seconds."
            };
            m_timerField.RegisterValueChangedCallback(change => SetDraftInt(
                "m_level.m_timerSeconds", Mathf.Clamp(change.newValue, k_minTimerSeconds, k_maxTimerSeconds),
                "Set Level Timer"));
            // Compact: the value has at most three digits.
            SetLabelWidth(m_timerField, k_timerLabelWidth);

            // A stopwatch before the Timer label draws attention to each level's own time limit (user request). It
            // sits inside the field, so the top bar keeps its Level Name, Timer, m:ss order. A warm accent, not the
            // neutral tool tint, so it stands out on both skins.
            const float k_timerIconSize = 16f;
            const float k_timerIconGap = 3f;
            float iconWidth = 0f;
            Texture2D timerIcon = LevelEditorIcons.Load(LevelEditorIcons.k_timer);
            if (timerIcon != null)
            {
                Image icon = new Image
                {
                    name = "timer-icon",
                    image = timerIcon,
                    tintColor = EditorGUIUtility.isProSkin ? new Color(1f, 0.76f, 0.2f) : new Color(0.8f, 0.42f, 0f),
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                icon.style.width = k_timerIconSize;
                icon.style.height = k_timerIconSize;
                icon.style.flexShrink = 0;
                icon.style.alignSelf = Align.Center;
                icon.style.marginRight = k_timerIconGap;
                m_timerField.Insert(0, icon);
                iconWidth = k_timerIconSize + k_timerIconGap;
            }

            m_timerField.style.width = iconWidth + k_timerLabelWidth + k_timerValueWidth;
            m_timerField.style.flexShrink = 0;
            row.Add(m_timerField);

            m_timerText = new Label { name = "timer-text" };
            m_timerText.style.width = 36;
            m_timerText.style.flexShrink = 0;
            m_timerText.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(m_timerText);
        }

        private static Foldout CreateGroup(string name, string title)
        {
            Foldout group = new Foldout { name = name, text = title, value = true };
            group.style.marginBottom = 4;
            return group;
        }

        // The full-width status line under both panes; every status message, the check's result sentence included,
        // shows here. The three main buttons sit in the validation panel.
        private VisualElement CreateBottomBar()
        {
            VisualElement bar = MakeRow("bottom-bar");
            bar.style.flexShrink = 0;
            bar.style.marginTop = 0;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 6;
            bar.style.paddingRight = 8;
            bar.style.paddingTop = 4;
            bar.style.paddingBottom = 4;
            bar.style.borderTopWidth = 1;
            bar.style.borderTopColor = new Color(0f, 0f, 0f, 0.35f);

            m_status = new Label { name = "status" };
            m_status.style.whiteSpace = WhiteSpace.Normal;
            m_status.style.flexGrow = 1;
            m_status.style.flexShrink = 1;
            m_status.style.flexBasis = 0;
            bar.Add(m_status);
            return bar;
        }

        // One of the validation panel's three larger buttons, side by side in its last row; a narrow pane wraps them.
        private static Button CreateBottomButton(string name, string text, string tooltip, Action action)
        {
            Button button = new Button(action) { name = name, text = text, tooltip = tooltip };
            button.style.flexShrink = 0;
            button.style.minWidth = k_bottomButtonMinWidth;
            button.style.height = k_bottomButtonHeight;
            button.style.fontSize = k_bottomButtonFontSize;
            button.style.marginLeft = 0;
            button.style.marginRight = 4;
            button.style.marginTop = 0;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            return button;
        }

        private static Button CreateIconButton(string name, string iconName, string fallbackText, string tooltip, Action action)
        {
            // FindTexture returns null without a console message when the built-in icon is missing.
            Button button = CreateIconButton(name, EditorGUIUtility.FindTexture(iconName), Color.white, fallbackText,
                tooltip, action);
            button.style.width = k_iconButtonWidth;
            return button;
        }

        // A square tools-column button with one of the Level Editor's white icons in the skin's neutral tint; a
        // wrapping row of them never clips at a narrow tools width.
        private static Button CreateToolButton(string name, string iconName, string fallbackText, string tooltip, Action action)
        {
            Button button = CreateIconButton(name, LevelEditorIcons.Load(iconName), LevelEditorIcons.NeutralTint,
                fallbackText, tooltip, action);
            button.style.width = k_toolButtonSize;
            button.style.height = k_toolButtonSize;
            button.style.marginLeft = 0;
            button.style.marginRight = 4;
            button.style.marginTop = 0;
            button.style.marginBottom = 4;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            button.style.paddingLeft = 4;
            button.style.paddingRight = 4;
            button.style.borderLeftWidth = 2;
            button.style.borderRightWidth = 2;
            button.style.borderTopWidth = 2;
            button.style.borderBottomWidth = 2;
            button.style.fontSize = 9;
            return button;
        }

        // A row of square tool buttons that wraps to the next line, like the colour swatches.
        private static VisualElement MakeToolRow(string name)
        {
            VisualElement row = MakeRow(name);
            row.style.flexWrap = Wrap.Wrap;
            return row;
        }

        // A null icon shows the fallback text instead.
        private static Button CreateIconButton(string name, Texture2D icon, Color tint, string fallbackText, string tooltip,
            Action action)
        {
            Button button = new Button(action) { name = name, tooltip = tooltip };
            button.style.flexShrink = 0;
            if (icon == null)
            {
                button.text = fallbackText;
                return button;
            }

            button.style.paddingLeft = 2;
            button.style.paddingRight = 2;
            button.Add(new Image
            {
                image = icon,
                tintColor = tint,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            });
            return button;
        }

        // Coalesces loads and edits into one publish on the next editor tick. It uses update, not delayCall:
        // Unity skips delayCall while the Editor is in the background, so the preview would wait for focus.
        private void SchedulePublish()
        {
            if (m_publishScheduled)
            {
                return;
            }

            m_publishScheduled = true;
            // Owner: this window instance. Stop point: PublishDraft unsubscribes itself on its one run,
            // or OnDisable unsubscribes a publish that has not run yet.
            EditorApplication.update += PublishDraft;
        }

        private void PublishDraft()
        {
            // Unsubscribes before the listeners run, so a throwing listener cannot repeat the publish every tick.
            EditorApplication.update -= PublishDraft;
            m_publishScheduled = false;
            LastPublishedLevel = m_draftController.Snapshot();
            DraftLevelPublished?.Invoke(LastPublishedLevel);
        }

        private static VisualElement MakeRow(string name)
        {
            VisualElement row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 4;
            return row;
        }

        // A zero basis sizes the element from the free width, so a row of fields never overflows the pane.
        private static void AddStretched(VisualElement row, VisualElement element, float grow, float labelWidth)
        {
            element.style.flexGrow = grow;
            element.style.flexShrink = 1;
            element.style.flexBasis = 0;
            if (labelWidth > 0f)
            {
                SetLabelWidth(element, labelWidth);
            }

            row.Add(element);
        }

        private static void SetLabelWidth(VisualElement field, float width)
        {
            Label label = field?.Q<Label>(className: BaseField<int>.labelUssClassName);
            if (label != null)
            {
                label.style.minWidth = width;
                label.style.width = width;
            }
        }

        // Reads the active level JSON and the piece library again and opens the selected level. Returns the note on the
        // saved template footprints no level holds any more (ForgetUnheldTemplates), already in the status line, or an
        // empty string; a caller that sets its own status appends it.
        private string RefreshLevelSelection()
        {
            // Every load or reload reads the piece library again, even when its loaded text is unchanged.
            RefreshShapeCatalog(true);
            m_refreshing = true;
            m_entries = LevelDatabaseAddressables.GetEntries();
            List<string> databaseChoices = new List<string>();
            int activeIndex = -1;
            for (int index = 0; index < m_entries.Count; index++)
            {
                databaseChoices.Add(m_entries[index].Name);
                if (m_entries[index].Guid == ActiveGuid)
                {
                    activeIndex = index;
                }
            }

            m_hasDatabasePlaceholder = activeIndex < 0;
            if (databaseChoices.Count == 0)
            {
                databaseChoices.Add("No Databases");
            }
            else if (m_hasDatabasePlaceholder)
            {
                databaseChoices.Insert(0, "Select Database");
            }

            m_databaseChoiceField.choices = databaseChoices;
            m_databaseChoiceField.SetValueWithoutNotify(
                activeIndex >= 0 ? databaseChoices[activeIndex] : databaseChoices[0]);
            m_databaseChoiceField.SetEnabled(m_projectScope != null && m_entries.Count > 0);
            string activePath = LevelDatabaseAddressables.GetActivePath(m_projectScope);
            m_sourceField.SetValueWithoutNotify(
                string.IsNullOrEmpty(activePath) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(activePath));
            m_saveController.LoadSource(ActiveGuid, m_entries, out string loadError);
            UpdateLevelChoices();
            m_refreshing = false;

            LoadSelectedDraft();
            if (m_projectScope != null && !m_saveController.SourceValid)
            {
                SetStatus(loadError);
            }

            string note = ForgetUnheldTemplates();
            if (note.Length > 0)
            {
                SetStatus(m_status.text + note);
            }

            return note;
        }

        /// <summary>
        /// After the level JSON was read again, for example by Restore Previous Save or Reload JSON after the file was
        /// reverted: a saved template footprint that no level of the database holds any more is no longer written to
        /// the piece library, so a later Save Level neither re-applies it nor refuses the levels that lost it. Returns
        /// the status note, or an empty string. An unreadable database keeps every footprint.
        /// </summary>
        private string ForgetUnheldTemplates()
        {
            string note = m_saveController.ForgetUnheldTemplates();
            if (note.Length > 0)
            {
                OnDraftChanged();
            }

            return note;
        }

        // Rebuilds the Target Level choices and the enable rules from the loaded database, without touching the draft.
        private void UpdateLevelChoices()
        {
            List<string> choices = new List<string>();
            if (m_saveController.SourceValid)
            {
                for (int index = 0; index < Levels.Count; index++)
                {
                    LevelData level = Levels.GetAt(index);
                    string name = level == null || string.IsNullOrWhiteSpace(level.Name)
                        ? "Untitled"
                        : level.Name;
                    choices.Add($"{index + 1}. {name}");
                }
            }

            if (choices.Count == 0)
            {
                choices.Add("No Levels");
                m_selectedIndex = -1;
            }
            else if (m_selectedIndex < 0 || m_selectedIndex >= choices.Count)
            {
                m_selectedIndex = 0;
            }

            m_targetField.choices = choices;
            m_targetField.SetValueWithoutNotify(choices[Mathf.Max(0, m_selectedIndex)]);
            m_targetStepper.SetChoicesEnabled(m_saveController.SourceValid && Levels.Count > 0);
            m_createDatabaseButton.SetEnabled(m_projectScope != null);
            m_deleteDatabaseButton.SetEnabled(CanDeleteActiveDatabase());
            m_createLevelButton.SetEnabled(m_saveController.SourceValid);
            m_deleteLevelButton.SetEnabled(m_saveController.SourceValid && m_selectedIndex >= 0);
            UpdateSaveButton();
            m_reloadButton.SetEnabled(m_projectScope != null);
            UpdateRestoreButton(HasDraftChanges());
        }

        // Restore has a job with unsaved draft changes (discard them) or with a backup (Restore Previous Save), and needs
        // a readable source for either. It follows every draft change through UpdateUnsavedChanges.
        private void UpdateRestoreButton(bool draftChanged)
        {
            m_restoreButton?.SetEnabled(m_saveController.SourceValid
                && (draftChanged || LevelDatabaseFiles.HasBackup(GetBackupFolder())));
        }

        private void LoadSelectedDraft()
        {
            SchedulePublish();
            DestroyDraft();
            // A newly loaded level starts on the default selection, which the designer has not chosen, and the Pen
            // edits nothing until a block is clicked.
            m_blockChosen = false;
            m_pieceEditController.EndPenEdit();
            m_selectionCleared = false;
            if (!m_saveController.SourceValid || m_selectedIndex < 0 || m_selectedIndex >= Levels.Count)
            {
                OnDraftChanged();
                SetStatus(m_projectScope == null
                    ? LevelDatabaseAddressables.MissingProjectScopeMessage
                    : "The JSON database is empty. Create a level to begin.");
                return;
            }

            m_draftController.Load(Levels.GetAt(m_selectedIndex));
            // A draft kept through a script or domain reload takes the saved copy's place before anything binds to it.
            string reloadStatus = RestoreDraftAfterReload();

            // Catches draft changes made outside this window's own edits. Edits and undo made by this window already
            // refreshed, so an unchanged draft is skipped: one change, one publish.
            // Owner: m_draftTracker; stop point: Unbind in the next LoadSelectedDraft.
            m_draftTracker.TrackSerializedObjectValue(m_draftController.SerializedDraft, _ =>
            {
                if (m_draftController.SerializedDraft != null && IsDraftOutOfSync())
                {
                    OnDraftChanged();
                }
            });
            // Level Name and Timer are unbound and filled here, so they show the draft values even when a
            // window recreated after a domain reload has not processed its bindings yet.
            OnDraftChanged();
            SetStatus(reloadStatus ?? GetEditingStatus());
        }

        // Keeps the unsaved draft in the editor session for this window, just before a script or domain reload
        // disables it. Only Save Level writes the JSON (TASK-20260925-172236747-9ac18e21).
        private void KeepDraftForReload()
        {
            m_draftController.KeepForReload(GetDraftReloadContext());
        }

        // The first level load after a reload takes back the draft kept by KeepDraftForReload. It replaces the saved
        // copy only on the same database, level index and level id of an unchanged JSON source text; otherwise the
        // saved level stays, as before. Returns the status to show, or null when this window kept no draft.
        private string RestoreDraftAfterReload()
        {
            string mismatch = m_draftController.RestoreAfterReload(GetDraftReloadContext());
            return mismatch ?? (HasDraftChanges() ? k_unsavedStatus : null);
        }

        private LevelDraftReloadContext GetDraftReloadContext()
        {
            LevelData saved = m_saveController.SourceValid && m_selectedIndex >= 0 && m_selectedIndex < Levels.Count
                ? Levels.GetAt(m_selectedIndex)
                : null;
            return new LevelDraftReloadContext(GetInstanceID(), m_saveController.LoadedGuid, m_selectedIndex, saved?.Id,
                m_saveController.LoadedSourceHash);
        }

        private void DestroyDraft()
        {
            m_draftTracker?.Unbind();
            m_draftController.Clear();
        }

        // Single refresh point after any draft change: fields, dirty marker, block tools and preview publish.
        private void OnDraftChanged()
        {
            // Any draft change, undo/redo, level or JSON switch stops a playing solution first, so the canvas and the
            // scene preview show the draft (after a switch, the new level).
            StopSolutionPlayback();
            // An edit, undo/redo, level or JSON switch makes the last Validate Level result stale (DEC-144), and stops a
            // background run, whose answer would be too (DEC-175).
            bool wasValidating = m_validationFlow.IsRunning;
            m_validationFlow.Invalidate();
            if (wasValidating)
            {
                StopValidationPolling();
                m_validationPanel?.ShowHint();
            }

            LevelData level = m_draftController.ReadAndMarkSynchronized();
            ClearValidationAnswerIfChanged(level);
            SyncDraftFields(level);
            UpdateUnsavedChanges();
            UpdateDirtyStatus();
            // A shrink waiting for its answer was computed from the old draft.
            ClearPendingResize();
            m_shapeRefusal = null;
            SyncCanvas(level);
            // A stroke, its undo or a Discard changes the pending template footprints the tiles and checks use.
            SyncEditCatalog();
            UpdateWallsAndIssues(level);
            RefreshCustomPieces(level);
            RefreshBlockTools();
            RefreshColorOrder();
            SchedulePublish();
        }

        // The swatch badges follow the draft's colour order; without a level the colour actions are off.
        private void RefreshColorOrder()
        {
            LevelColorOrder order = m_draftController.Draft != null ? m_draftController.Draft.ColorOrder : LevelColorOrder.Default;
            m_colorSwatches?.SetOrder(order);
            bool hasLevel = m_draftController.SerializedDraft != null;
            m_colorSwitchButton?.SetEnabled(hasLevel);
            m_colorShuffleButton?.SetEnabled(hasLevel);
        }

        // Redraws the canvas from the draft and finds the stored selection in it again (by id, else index).
        // A block selection that is gone falls back to RefreshBlockTools' index clamp, as before the canvas.
        private void SyncCanvas(LevelData level)
        {
            LevelCanvasModel model = LevelCanvasModel.Create(level);
            string chosenId = m_canvasSelection.Id;
            m_canvasSelection = model.Resolve(m_canvasSelection);
            if (m_canvasSelection.Kind == LevelCanvasSelectionKind.Block)
            {
                m_selectedBlockIndex = m_canvasSelection.Index;
            }
            else if (m_canvasSelection.Kind == LevelCanvasSelectionKind.Door
                     || m_canvasSelection.Kind == LevelCanvasSelectionKind.Obstacle)
            {
                m_selectedBlockIndex = -1;
            }

            // A chosen block that is gone, or replaced by its index neighbour, leaves the default in charge.
            m_blockChosen &= m_canvasSelection.Kind == LevelCanvasSelectionKind.Block
                             && string.Equals(m_canvasSelection.Id, chosenId, StringComparison.Ordinal);
            m_doorChosen &= m_canvasSelection.Kind == LevelCanvasSelectionKind.Door
                            && string.Equals(m_canvasSelection.Id, chosenId, StringComparison.Ordinal);
            m_canvas?.SetModel(model);
        }

        // Select tool: a click on a block or door selects it on the canvas and in the Selected Block dropdown.
        private void SelectOnCanvas(LevelCanvasSelection selection)
        {
            if (TryPickGateColor(selection))
            {
                return;
            }

            m_canvasSelection = selection;
            m_selectedBlockIndex = selection.Kind == LevelCanvasSelectionKind.Block ? selection.Index : -1;
            m_blockChosen = selection.Kind == LevelCanvasSelectionKind.Block;
            m_doorChosen = selection.Kind == LevelCanvasSelectionKind.Door;
            RefreshBlockTools();
            if (m_opensEffectEditor && selection.Kind != LevelCanvasSelectionKind.None)
            {
                ShowEffectEditor?.Invoke(this);
            }
        }

        // A left or right click on empty space with Select or the plain Pen (user order of 2026-09-26): it ends a Pen
        // edit and clears the selection, which then gets no automatic default until something is picked again.
        private void ClearSelectionOnEmptySpace()
        {
            bool editing = IsPenEditing(out _);
            m_pieceEditController.EndPenEdit();
            m_canvasSelection = LevelCanvasSelection.None;
            m_selectedBlockIndex = -1;
            m_blockChosen = false;
            m_doorChosen = false;
            m_selectionCleared = true;
            SyncTool();
            RefreshBlockTools();
            if (editing)
            {
                SetStatus("Editing ended; nothing is selected.");
            }
            else if (IsPlainPen())
            {
                ShowHint(k_penHint);
            }
        }

        // Select with an armed piece: a gate click takes that gate's colour for the next placed block (user order of
        // 2026-09-26); the piece stays armed, the gate is not selected and the draft does not change.
        private bool TryPickGateColor(LevelCanvasSelection selection)
        {
            LevelData level = m_draftController.Snapshot();
            if (m_toolState.Tool != LevelEditorTool.Select || !m_toolState.HasArmedShape
                || selection.Kind != LevelCanvasSelectionKind.Door || level == null
                || selection.Index < 0 || selection.Index >= level.DoorCount
                || !ColorBlockJam.Runtime.Layout.BoardLayout.TryParseDoorColor(level.GetDoorAt(selection.Index).ColorId,
                    out PaletteColor color))
            {
                return false;
            }

            m_toolState.SetColor(color);
            SyncTool();
            SetStatus($"Placing in {color}, the colour of gate {level.GetDoorAt(selection.Index).Id}.");
            return true;
        }

        private void OnUndoRedo()
        {
            if (m_status == null)
            {
                return;
            }

            // Every undo or redo stops a playing solution; one that changes the draft then reaches the canvas below.
            StopSolutionPlayback();
            // An undone piece delete (the library's own undo) shows its tile again; unchanged text is a no-op.
            RefreshShapeCatalog(false);

            // Undoing a database switch restores the project scope installer's level JSON. A switch made outside this
            // window never drops unsaved edits silently: they stay until Reload JSON asks Save/Discard/Cancel.
            bool databaseChanged = !string.Equals(ActiveGuid, m_saveController.LoadedGuid, StringComparison.Ordinal);
            if (databaseChanged && !HasDraftChanges())
            {
                m_selectedIndex = -1;
                RefreshLevelSelection();
                return;
            }

            // Unrelated undo steps (scenes, other assets) leave the draft as it was.
            if (m_draftController.SerializedDraft != null && IsDraftOutOfSync())
            {
                OnDraftChanged();
                MirrorUndoneNamedPiece();
            }

            if (databaseChanged)
            {
                SetStatus("The active level JSON changed outside the Level Editor. " +
                          "Press Reload JSON to save or discard this level's changes and load it.");
            }
        }

        private bool IsDraftOutOfSync()
        {
            return m_draftController.IsOutOfSync();
        }

        private void SyncDraftFields(LevelData level)
        {
            bool hasLevel = level != null;
            m_levelNameField.SetValueWithoutNotify(hasLevel ? level.Name : string.Empty);
            m_timerField.SetValueWithoutNotify(hasLevel ? level.TimerSeconds : 0);
            m_timerText.text = hasLevel ? FormatTimer(level.TimerSeconds) : string.Empty;
            m_levelNameField.SetEnabled(hasLevel);
            m_timerField.SetEnabled(hasLevel);
        }

        /// <summary>Formats a timer as m:ss, for example 90 as 1:30.</summary>
        internal static string FormatTimer(int seconds)
        {
            int clamped = Mathf.Max(0, seconds);
            return $"{clamped / 60}:{clamped % 60:D2}";
        }

        /// <summary>
        /// Adds (delta &gt; 0) or removes lines on one side. A change that removes blocks or gates is hatched on the
        /// canvas and waits for Remove or Cancel; anything else applies at once as one undo step.
        /// </summary>
        private void RequestResize(BoardSide side, int delta)
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null)
            {
                return;
            }

            ClearPendingResize();
            if (!LevelEditCommands.TryResizeBoard(level, side, delta, out LevelData result, out LevelResizeReport report,
                    out string issue))
            {
                SyncDraftFields(level);
                SetStatus(issue);
                return;
            }

            if (!report.HasRemovals)
            {
                ApplyCommand("Resize Level Board", true, result, null);
                return;
            }

            m_pendingResize = result;
            // The draft stays until Apply; the question names what changes.
            m_canvas?.SetHatch(report.RemovedCells, report.RemovedDoorSegments);
            m_resizeConfirmText.text = report.Describe();
            m_resizeConfirm.style.display = DisplayStyle.Flex;
        }

        private void ConfirmResize()
        {
            LevelData result = m_pendingResize;
            ClearPendingResize();
            if (result != null)
            {
                ApplyCommand("Resize Level Board", true, result, null);
            }

            // The Apply button took the focus; the canvas gets it back so the shortcuts keep working, as after Cancel.
            m_canvas?.Focus();
        }

        // Cancel keeps the board.
        private void CancelResize()
        {
            ClearPendingResize();
            m_canvas?.Focus();
        }

        private bool ClearPendingResize()
        {
            bool pending = m_pendingResize != null;
            m_pendingResize = null;
            if (m_resizeConfirm != null)
            {
                m_resizeConfirm.style.display = DisplayStyle.None;
            }

            m_canvas?.SetHatch(null, null);
            return pending;
        }

        // Walls from the rule set and the issues list follow every draft change; a missing or broken environment
        // database is one issue and draws no walls.
        private void UpdateWallsAndIssues(LevelData level)
        {
            string wallIssue = null;
            if (level == null || !m_canvasWalls.TryCompose(level, m_environmentDatabase, m_wallGlyphs, out wallIssue))
            {
                m_wallGlyphs.Clear();
            }

            m_canvas?.SetWalls(m_wallGlyphs);
            // A block's piece is the key of its cells (DEC-193), so no issue compares blocks with the piece library.
            List<LevelIssue> issues = LevelDraftIssues.Collect(level);
            if (level != null && wallIssue != null)
            {
                issues.Add(LevelIssue.ForCells(LevelIssueSeverity.Warning, "Walls: " + wallIssue, null,
                    Array.Empty<Vector2Int>()));
            }

            m_issuesList?.SetIssues(issues);
            m_canvas?.SetIssueHighlight(null);
            m_draftHasErrors = LevelDraftIssues.HasError(issues);
            UpdateSaveButton();
            UpdateShapeBanner(level);
        }

        // Save Level needs an open level and a draft whose issues list has no error. An error is a LevelData.Validate
        // failure, such as blocks a re-stamp left overlapping (contract D3-B), which SaveLevel would refuse anyway.
        // Test Level saves first, so it needs the same in Edit Mode. While Play Mode runs it reads Stop Test and stays
        // enabled (contract Revision 1); while Play Mode starts or stops it waits.
        private void UpdateSaveButton()
        {
            if (m_saveLevelButton == null)
            {
                return;
            }

            bool canSave = m_saveController.SourceValid && m_selectedIndex >= 0 && !m_draftHasErrors;
            m_saveLevelButton.SetEnabled(canSave);
            m_saveLevelButton.tooltip = m_draftHasErrors ? k_saveBlockedTooltip : k_saveTooltip;
            bool running = IsPlayModeRunning();
            bool editMode = IsInEditMode();
            m_testLevelButton.text = running ? k_stopTestText : k_testText;
            m_testLevelButton.SetEnabled(running || (canSave && editMode));
            m_testLevelButton.tooltip = running ? k_stopTestTooltip
                : !editMode ? k_testPlayingTooltip
                : m_draftHasErrors ? k_testBlockedTooltip
                : k_testTooltip;
            // Validate Level follows Save Level's conditions and, like Test Level, waits while Play Mode runs. While a
            // background run goes on it reads Cancel and stays enabled (DEC-175).
            bool checkPlaying = m_validationFlow.IsPlaying();
            bool validating = m_validationFlow.IsRunning;
            m_validateLevelButton.text = validating ? k_cancelValidateText : k_validateText;
            m_validateLevelButton.SetEnabled(validating || (canSave && !checkPlaying));
            m_validateLevelButton.tooltip = validating ? k_cancelValidateTooltip
                : checkPlaying ? k_validatePlayingTooltip
                : m_draftHasErrors ? k_validateBlockedTooltip
                : k_validateTooltip;
            // Opening a scene needs Edit Mode.
            m_openSceneButton.SetEnabled(editMode);
            m_openSceneButton.tooltip = editMode ? k_openSceneTooltip : k_openScenePlayingTooltip;
            // The listed solutions wait with it, so no playback starts in Play Mode or runs into it.
            m_validationPanel?.SetRowsEnabled(!checkPlaying);
        }

        // The status note after a re-stamp: the blocks it left overlapping, which Save Level waits for (D3-B).
        private static string GetOverlapNote(LevelData level)
        {
            return LevelPieceEditController.GetOverlapNote(level);
        }

        /// <summary>
        /// Shows the last refused level-piece edit and the Re-stamp offer: how many blocks of the open level still have
        /// an earlier footprint of a template whose footprint the piece library changed (DEC-193: those blocks are
        /// level pieces until re-stamped). Nothing changes until the designer re-stamps (D2-A).
        /// </summary>
        private void UpdateShapeBanner(LevelData level)
        {
            if (m_shapeBanner == null)
            {
                return;
            }

            UpdateOtherLevels();
            string restamp = m_pieceEditController.DescribeRestampOffer(m_saveController.LoadedGuid, level);
            string text = JoinLines(m_shapeRefusal, restamp);
            m_shapeBannerText.text = text ?? string.Empty;
            m_shapeBanner.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
            m_restampButton.style.display = restamp != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string JoinLines(string first, string second)
        {
            return first != null && second != null ? first + "\n" + second : first ?? second;
        }

        // Reads the other levels again only when the loaded JSON, the catalog or the open level changed. Only an open
        // level proves this domain loaded the database: a domain reload restores m_saveController.SourceValid before the database
        // is loaded again, and reading it then throws. They are checked against the library with the template
        // footprints Save Level already wrote to them, not the open level's unsaved ones.
        private void UpdateOtherLevels()
        {
            m_pieceEditController.UpdateOtherLevels(GetPieceSource());
        }

        /// <summary>
        /// Lists the Level pieces section: the open level's level pieces, then this session's unplaced ones, then the
        /// other levels', each under its level name (DEC-193). The palette is rebuilt only when the list changed.
        /// </summary>
        private void RefreshCustomPieces(LevelData level)
        {
            if (m_shapePalette != null && m_pieceEditController.RefreshCustomPieces(GetPieceSource(), level))
            {
                m_shapePalette.SetCustomPieces(m_pieceEditController.CustomPieces);
            }
        }

        // The catalog the open level is edited against: the piece library with the template footprints Save Level
        // wrote to this database but not yet to the library, then the draft's pending ones (DEC-125). Tiles, Select
        // placement, Piece Type, Re-stamp and the level piece names use it; library reads, remembered footprints and
        // library commits keep the real library.
        private LevelShapeCatalog GetEditCatalog()
        {
            return m_pieceEditController.GetEditCatalog(m_saveController.LoadedGuid);
        }

        // After a draft change: the tiles show the pending template footprints, and an armed template whose footprint
        // an undo or Discard changed is dropped, so Select never places an outdated footprint. An armed level piece
        // stays armed.
        private void SyncEditCatalog()
        {
            LevelShapeCatalog previous = m_paletteCatalog;
            LevelShapeCatalog catalog = GetEditCatalog();
            SetPaletteCatalog(catalog);
            ShapeKey armed = m_toolState.ArmedKey;
            if (armed.IsValid && previous != null && previous.IndexOf(armed) >= 0 && catalog.IndexOf(armed) < 0)
            {
                m_toolState.Select();
                SyncTool();
            }
        }

        // Rebuilds the shape tiles only for another catalog.
        private void SetPaletteCatalog(LevelShapeCatalog catalog)
        {
            if (m_shapePalette == null || ReferenceEquals(catalog, m_paletteCatalog))
            {
                return;
            }

            m_paletteCatalog = catalog;
            m_shapePalette.SetCatalog(catalog);
        }

        private void SetDraftInt(string propertyPath, int value, string undoName)
        {
            if (m_draftController.SerializedDraft == null)
            {
                return;
            }

            m_draftController.SetInt(propertyPath, value, undoName);
            // Refresh even for a no-op, so a clamped or refused value snaps back in its field.
            OnDraftChanged();
        }

        private void SetDraftString(string propertyPath, string value)
        {
            if (m_draftController.SerializedDraft == null)
            {
                return;
            }

            m_draftController.SetString(propertyPath, value, "Rename Level");
            OnDraftChanged();
        }

        private void RefreshBlockTools()
        {
            if (m_blockChoiceField == null || m_pieceTypeField == null)
                return;

            RefreshShapeCatalog(false);
            SerializedProperty blocks = null;
            if (m_draftController.SerializedDraft != null)
            {
                m_draftController.SerializedDraft.Update();
                blocks = m_draftController.SerializedDraft.FindProperty("m_level.m_blocks");
            }

            var blockChoices = new List<string>();
            for (int index = 0; blocks != null && index < blocks.arraySize; index++)
            {
                string id = blocks.GetArrayElementAtIndex(index).FindPropertyRelative("m_id").stringValue;
                blockChoices.Add($"{index + 1}. {id}");
            }

            // A door or board object selected on the canvas leaves no block selected; otherwise the first block is the
            // default.
            bool doorSelected = m_canvasSelection.Kind == LevelCanvasSelectionKind.Door;
            bool otherSelected = doorSelected || m_canvasSelection.Kind == LevelCanvasSelectionKind.Obstacle;
            if (m_selectedBlockIndex >= blockChoices.Count)
            {
                m_selectedBlockIndex = blockChoices.Count - 1;
                m_blockChosen = false;
            }

            // Any selection made since the clear (a pick, a placed block, a drawn piece) ends it.
            if (m_selectedBlockIndex >= 0 || otherSelected)
            {
                m_selectionCleared = false;
            }

            if (m_selectedBlockIndex < 0 && blockChoices.Count > 0 && !otherSelected && !m_selectionCleared)
            {
                m_selectedBlockIndex = 0;
                m_blockChosen = false;
            }
            m_blockChoiceField.choices = blockChoices.Count > 0 ? blockChoices : new List<string> { "No Blocks" };
            m_blockChoiceField.SetValueWithoutNotify(otherSelected
                ? (doorSelected ? "Door " : "Obstacle ") + m_canvasSelection.Id
                : m_selectedBlockIndex >= 0 ? blockChoices[m_selectedBlockIndex]
                : blockChoices.Count > 0 ? "Nothing selected" : "No Blocks");
            m_blockChoiceField.SetEnabled(blockChoices.Count > 0);

            if (m_selectedBlockIndex >= 0)
            {
                string id = blocks.GetArrayElementAtIndex(m_selectedBlockIndex).FindPropertyRelative("m_id").stringValue;
                m_canvasSelection = LevelCanvasSelection.ForBlock(m_selectedBlockIndex, id);
            }
            else if (!otherSelected)
            {
                m_canvasSelection = LevelCanvasSelection.None;
            }

            // A playing or paused solution's frames show no selection outline; the playback's end or stop shows it again.
            m_canvas?.SetSelection(IsShowingSolution ? LevelCanvasSelection.None : m_canvasSelection);

            // Piece Type lists the templates by name and the selected block's piece by its name (DEC-193: a template's
            // key shows its name, any other key is a level piece of this level). The catalog tolerates an unreadable
            // piece library: Piece Type then lists only the current piece.
            TryGetSelectedBlock(out LevelData selectedLevel, out LevelBlockData selectedBlock);
            LevelShapeCatalog pieceCatalog = GetEditCatalog();
            var pieceChoices = new List<string>();
            for (int index = 0; index < pieceCatalog.Count; index++)
                pieceChoices.Add(pieceCatalog.Get(index).DisplayName);
            string currentType = null;
            if (selectedBlock != null)
            {
                LevelShapeChoice template = pieceCatalog.Find(selectedBlock.Key);
                currentType = template != null
                    ? template.DisplayName
                    : m_pieceEditController.GetPieceName(m_saveController.LoadedGuid, selectedLevel, selectedBlock.Key)
                      + " (level piece)";
            }

            if (currentType != null && !pieceChoices.Contains(currentType))
                pieceChoices.Insert(0, currentType);
            if (pieceChoices.Count == 0)
                pieceChoices.Add("No Pieces");

            m_pieceTypeField.choices = pieceChoices;
            m_pieceTypeField.SetValueWithoutNotify(currentType ?? pieceChoices[0]);
            m_pieceTypeField.SetEnabled(selectedBlock != null && pieceCatalog.Count > 0);
            m_pieceTypeField.tooltip = pieceCatalog.Error;
            // The Pen's edit target, its tile outline, the tool info box and the template banner follow every selection
            // and draft change.
            SyncPieceDrawing();
            // Every selection and draft change ends here, so the Effect Editor follows both.
            SelectionChanged?.Invoke(this);
        }

        private void ApplyPieceType()
        {
            // The library may have changed since the dropdown was filled; the choice uses its current cells, or a
            // template's pending footprint (DEC-125).
            RefreshShapeCatalog(false);
            LevelShapeChoice shape = GetEditCatalog().FindByName(m_pieceTypeField.value);
            if (shape != null)
            {
                ReplaceSelectedCells(shape.Cells, "Change Block Piece Type", shape.Key);
            }
            else
            {
                // A shape that left the library snaps the dropdown back to the block's type.
                RefreshBlockTools();
            }
        }

        // R and Shift+R: the armed piece's ghost when a piece is armed, else the selected block.
        private void RotateSelection(bool clockwise)
        {
            if (m_toolState.RotateArmed(clockwise))
            {
                SyncTool();
            }
            else if (m_selectedBlockIndex >= 0)
            {
                RotateBlock(m_selectedBlockIndex, clockwise);
            }
        }

        // F and Shift+F: the armed piece's ghost when a piece is armed, else the selected block.
        private void FlipSelection(bool vertical)
        {
            if (m_toolState.FlipArmed(vertical))
            {
                SyncTool();
            }
            else if (m_selectedBlockIndex >= 0)
            {
                MirrorBlock(m_selectedBlockIndex, vertical);
            }
        }

        // A rotate or mirror tool's click: the clicked block becomes the designer's chosen selection, then changes.
        private void TransformClickedBlock(int blockIndex, LevelEditorTool tool)
        {
            LevelData level = m_draftController.Snapshot();
            LevelBlockData block = level != null && blockIndex >= 0 && blockIndex < level.BlockCount
                ? level.GetBlockAt(blockIndex)
                : null;
            if (block == null)
            {
                return;
            }

            m_selectedBlockIndex = blockIndex;
            m_canvasSelection = LevelCanvasSelection.ForBlock(blockIndex, block.Id);
            m_blockChosen = true;
            switch (tool)
            {
                case LevelEditorTool.RotateCounterClockwise:
                    RotateBlock(blockIndex, false);
                    break;
                case LevelEditorTool.RotateClockwise:
                    RotateBlock(blockIndex, true);
                    break;
                case LevelEditorTool.MirrorHorizontal:
                    MirrorBlock(blockIndex, false);
                    break;
                case LevelEditorTool.MirrorVertical:
                    MirrorBlock(blockIndex, true);
                    break;
            }
        }

        // Turns one block 90 degrees in place (LevelPieceTools.GetTurnOriginShift); the selection and its chosen flag
        // stay as they are (R keeps the default selection a default, so a later swatch never recolours it).
        private void RotateBlock(int blockIndex, bool clockwise)
        {
            LevelData level = m_draftController.Snapshot();
            LevelBlockData block = level != null && blockIndex >= 0 && blockIndex < level.BlockCount
                ? level.GetBlockAt(blockIndex) : null;
            if (block == null || block.CellCount == 0)
            {
                return;
            }

            bool accepted = LevelEditCommands.TryRotateBlock(level, blockIndex, clockwise,
                out LevelData result, out string issue);

            ApplyCommand("Rotate Block", accepted, result, issue);
        }

        // Mirrors one block; like RotateBlock, it leaves the selection and its chosen flag as they are.
        private void MirrorBlock(int blockIndex, bool vertical)
        {
            if (TryGetBlockCells(blockIndex, out _, out Vector2Int[] cells))
            {
                ReplaceBlockCells(blockIndex,
                    vertical ? LevelPieceTools.FlipVertical(cells) : LevelPieceTools.FlipHorizontal(cells), "Mirror Block");
            }
        }

        // The block's cells in the current draft; false when the index names no block with cells.
        private bool TryGetBlockCells(int blockIndex, out LevelBlockData block, out Vector2Int[] cells)
        {
            LevelData level = m_draftController.Snapshot();
            block = level != null && blockIndex >= 0 && blockIndex < level.BlockCount ? level.GetBlockAt(blockIndex) : null;
            cells = block != null && block.CellCount > 0 ? LevelRecordCopy.GetCells(block) : null;
            return cells != null;
        }

        private void ReplaceSelectedCells(IReadOnlyList<Vector2Int> cells, string undoName, ShapeKey pieceKey = default)
        {
            ReplaceBlockCells(m_selectedBlockIndex, cells, undoName, pieceKey);
        }

        // Rotate, Mirror and Piece Type share one refusal: the new footprint must stay on the board and free. A Piece
        // Type choice passes its piece's key: the block then belongs to that piece, as a placed block does, so the
        // piece's next change moves it too (DEC-193).
        private void ReplaceBlockCells(int blockIndex, IReadOnlyList<Vector2Int> cells, string undoName,
            ShapeKey pieceKey = default)
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null || blockIndex < 0 || blockIndex >= level.BlockCount)
            {
                return;
            }

            bool accepted = LevelEditCommands.TryReplaceBlockCells(
                level, blockIndex, cells, out LevelData result, out string issue);
            Action<LevelDraft> joinPiece = accepted && pieceKey.IsValid
                ? m_pieceEditController.JoinPlacedBlock(GetOpenLevelScope(), pieceKey, level.GetBlockAt(blockIndex).Id)
                : null;
            ApplyCommand(undoName, accepted, result, issue, joinPiece);
        }

        private bool TryGetSelectedBlock(out LevelData level, out LevelBlockData block)
        {
            level = m_draftController.Snapshot();
            block = level != null && m_selectedBlockIndex >= 0 && m_selectedBlockIndex < level.BlockCount
                ? level.GetBlockAt(m_selectedBlockIndex)
                : null;
            return block != null;
        }

        // Select with an armed piece: a click on a free cell adds the piece in the tool colour with the next free b{n} id
        // and selects nothing, then disarms it and returns to plain Select (user orders of 2026-09-26 and 2026-09-29;
        // place-once since 2026-09-30); a refused placement keeps the piece armed. The ghost sits at the hovered cell
        // minus the armed anchor, the cell the pointer holds. A Pen click here has no block to draw on, so it says what to do.
        private void PlaceBlock(Vector2Int hoveredCell)
        {
            if (m_toolState.Tool == LevelEditorTool.Pen)
            {
                ShowHint(k_penHint);
                return;
            }

            Vector2Int origin = hoveredCell - m_toolState.ArmedAnchor;
            // A shape the Piece Editor changed since it was armed is disarmed here instead of placed.
            RefreshShapeCatalog(false);
            if (m_toolState.Tool != LevelEditorTool.Select || !m_toolState.HasArmedShape)
            {
                return;
            }

            LevelData level = m_draftController.Snapshot();
            bool accepted = LevelEditCommands.TryPlaceBlock(level, m_toolState.ArmedCells, m_toolState.ColorId,
                origin, out LevelData result, out string issue);
            Action<LevelDraft> joinPiece = null;
            if (accepted)
            {
                int index = result.BlockCount - 1;
                string blockId = result.GetBlockAt(index).Id;
                // Keep nothing selected: a swatch sets the tool colour rather than recolouring the new block.
                // Mirror the empty-space clear so no default selection comes back.
                m_canvasSelection = LevelCanvasSelection.None;
                m_selectedBlockIndex = -1;
                m_blockChosen = false;
                m_doorChosen = false;
                m_selectionCleared = true;
                // The new block belongs to the piece it was placed as, so that piece's next change moves it too.
                joinPiece = m_pieceEditController.JoinPlacedBlock(GetOpenLevelScope(), m_toolState.ArmedKey, blockId);
            }

            ApplyCommand("Place Block", accepted, result, issue, joinPiece);
            if (accepted)
            {
                SetTool(LevelEditorTool.Select);
            }
        }

        // A Select-tool drag released offset cells away from where it started.
        private void MoveBlock(int blockIndex, Vector2Int offset)
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null || blockIndex < 0 || blockIndex >= level.BlockCount || level.GetBlockAt(blockIndex) == null)
            {
                return;
            }

            bool accepted = LevelEditCommands.TryMoveBlock(level, blockIndex,
                level.GetBlockAt(blockIndex).Origin + offset, out LevelData result, out string issue);
            ApplyCommand("Move Block", accepted, result, issue);
        }

        private void NudgeSelectedBlock(Vector2Int step)
        {
            if (TryGetSelectedBlock(out LevelData level, out LevelBlockData block))
            {
                bool accepted = LevelEditCommands.TryMoveBlock(level, m_selectedBlockIndex, block.Origin + step,
                    out LevelData result, out string issue);
                ApplyCommand("Nudge Block", accepted, result, issue);
            }
        }

        // Right click or the Eraser on a block, or Delete on the selection.
        private void RemoveBlock(int blockIndex)
        {
            LevelData level = m_draftController.Snapshot();
            bool accepted = LevelEditCommands.TryRemoveBlock(level, blockIndex, out LevelData result, out string issue);
            if (accepted && blockIndex == m_selectedBlockIndex)
            {
                // The removed block's index now names its neighbour; the default selection takes over instead.
                m_canvasSelection = LevelCanvasSelection.None;
                m_selectedBlockIndex = -1;
            }

            ApplyCommand("Remove Block", accepted, result, issue);
        }

        // Delete and the Delete button: the selected gate, else the selected block.
        private void DeleteSelection()
        {
            if (m_canvasSelection.Kind == LevelCanvasSelectionKind.Door)
            {
                RemoveDoor(m_canvasSelection.Index);
            }
            else if (m_selectedBlockIndex >= 0)
            {
                RemoveBlock(m_selectedBlockIndex);
            }
        }

        /// <summary>
        /// A Pen stroke on the wall ring: each crossed segment places or extends a gate of the selected colour, as
        /// one undo step; segments the same colour already holds are kept. The painted gate becomes the selection.
        /// </summary>
        private void PaintDoors(BoardSide side, IReadOnlyList<int> segments)
        {
            LevelData current = m_draftController.Snapshot();
            if (current == null || segments == null || m_toolState.Tool != LevelEditorTool.Pen)
            {
                return;
            }

            string colorId = m_toolState.ColorId;
            bool changed = false;
            int doorIndex = -1;
            string issue = null;
            for (int index = 0; index < segments.Count; index++)
            {
                int held = FindDoorAt(current, side, segments[index]);
                if (held >= 0 && string.Equals(current.GetDoorAt(held).ColorId, colorId, StringComparison.Ordinal))
                {
                    doorIndex = held;
                    continue;
                }

                if (!LevelEditCommands.TryPlaceOrExtendDoor(current, side, segments[index], colorId,
                        out LevelData next, out int placed, out issue))
                {
                    break;
                }

                current = next;
                doorIndex = placed;
                changed = true;
            }

            if (doorIndex >= 0)
            {
                m_canvasSelection = LevelCanvasSelection.ForDoor(doorIndex, current.GetDoorAt(doorIndex).Id);
                m_selectedBlockIndex = -1;
                m_blockChosen = false;
                m_doorChosen = false;
                // The gate is the selection now, so the Pen edit ends and the next swatch picks the next gate's colour
                // without recolouring the block.
                m_pieceEditController.EndPenEdit();
            }

            if (changed)
            {
                ApplyCommand("Paint Gate", true, current, null);
            }
            else
            {
                RefreshBlockTools();
            }

            if (issue != null)
            {
                SetStatus(issue);
            }
        }

        private static int FindDoorAt(LevelData level, BoardSide side, int segment)
        {
            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                if (door != null && door.Side == side && segment >= door.Start && segment < door.Start + door.Length)
                {
                    return index;
                }
            }

            return -1;
        }

        // A corner button: cut the board's corner cell into a void, or fill it back, as one undo step.
        private void CutOrFillCorner(Vector2Int cell, bool cut)
        {
            LevelData level = m_draftController.Snapshot();
            LevelData result;
            string issue;
            bool accepted = cut
                ? LevelEditCommands.TryCutCorner(level, cell, out result, out issue)
                : LevelEditCommands.TryFillCorner(level, cell, out result, out issue);
            ApplyCommand(cut ? "Cut Corner" : "Fill Corner", accepted, result, issue);
        }

        /// <summary>One obstacle stroke commits one draft snapshot for Undo/Redo.</summary>
        private void ApplyObstacleStroke(IReadOnlyList<Vector2Int> cells, bool paint)
        {
            LevelData level = m_draftController.Snapshot();
            LevelData result;
            string issue;
            bool changed;
            if (m_toolState.IsBarrierArmed)
                changed = LevelBarrierCommands.TryApplyStroke(level, cells, paint, out result, out issue);
            else if (m_toolState.IsVoidArmed)
                // A left drag cuts floor into void, a right drag restores it; refused cells are skipped and named.
                changed = LevelEditCommands.TryApplyVoidStroke(level, cells, paint, out result, out issue);
            else
            {
                // The Eraser removes barrier cells and walls, preserving every unrelated object and effect.
                changed = LevelBarrierCommands.TryApplyStroke(level, cells, false, out result, out issue);
                LevelData current = changed ? result : level;

                if (LevelEditCommands.TryApplyVoidStroke(current, cells, false, out LevelData unwalled, out string wallIssue))
                {
                    result = unwalled;
                    changed = true;
                }
                issue = issue ?? wallIssue;
            }
            ApplyCommand(paint ? "Paint " + m_toolState.ObstacleBrush : "Erase obstacle", changed, result, issue);
            if (changed && issue != null) SetStatus(issue);
        }

        // Right click or the Eraser on a gate, or Delete on a selected gate.
        private void RemoveDoor(int doorIndex)
        {
            LevelData level = m_draftController.Snapshot();
            bool accepted = LevelEditCommands.TryRemoveDoor(level, doorIndex, out LevelData result, out string issue);
            if (accepted && m_canvasSelection.Kind == LevelCanvasSelectionKind.Door && m_canvasSelection.Index == doorIndex)
            {
                m_canvasSelection = LevelCanvasSelection.None;
            }

            ApplyCommand("Remove Gate", accepted, result, issue);
        }

        // A Pen right click on a gate takes only the clicked segment off, as one undo step
        // (TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove); the Eraser removes the whole gate.
        private void RemoveDoorSegment(int doorIndex, BoardSide side, int segment)
        {
            LevelData level = m_draftController.Snapshot();
            bool accepted = LevelEditCommands.TryRemoveDoorSegment(level, doorIndex, side, segment,
                out LevelData result, out string issue);
            if (accepted && result.DoorCount < level.DoorCount && m_canvasSelection.Kind == LevelCanvasSelectionKind.Door
                && m_canvasSelection.Index == doorIndex)
            {
                // The last segment took the selected gate with it.
                m_canvasSelection = LevelCanvasSelection.None;
            }

            ApplyCommand("Remove Gate Part", accepted, result, issue);
        }

        // A swatch or [ / ] sets the colour for new blocks; it also recolours the block the Pen targets (the "+" piece's
        // block while it draws freehand, else the block the plain Pen edits; template or not: colour is per block), or
        // with the Select tool a block the designer chose. The automatic default selection is never recoloured (design
        // section 2: otherwise tool state only), because a block's colour decides its exit door.
        private void PickColor(PaletteColor color)
        {
            m_toolState.SetColor(color);
            SyncTool();
            LevelData level = m_draftController.Snapshot();
            string targetId = GetFreehandBlockId(out _);
            int blockIndex = targetId != null ? LevelPieceEditController.FindBlockIndex(level, targetId) : -1;
            // Only a gate the designer clicked is recoloured; a gate selected by painting is not a choice.
            if (blockIndex < 0 && m_canvasSelection.Kind == LevelCanvasSelectionKind.Door)
            {
                if (m_toolState.Tool == LevelEditorTool.Select && m_doorChosen)
                {
                    RecolorSelectedDoor();
                }

                return;
            }

            if (blockIndex < 0 && m_toolState.Tool == LevelEditorTool.Select && m_blockChosen
                && TryGetSelectedBlock(out _, out _))
            {
                blockIndex = m_selectedBlockIndex;
            }

            if (blockIndex < 0
                || string.Equals(level.GetBlockAt(blockIndex).ColorId, m_toolState.ColorId, StringComparison.Ordinal))
            {
                return;
            }

            bool accepted = LevelEditCommands.TryRecolorBlock(level, blockIndex, m_toolState.ColorId,
                out LevelData result, out string issue);
            ApplyCommand("Recolour Block", accepted, result, issue);
        }

        private void RecolorSelectedDoor()
        {
            LevelData level = m_draftController.Snapshot();
            int doorIndex = m_canvasSelection.Index;
            if (level == null || doorIndex < 0 || doorIndex >= level.DoorCount
                || string.Equals(level.GetDoorAt(doorIndex).ColorId, m_toolState.ColorId, StringComparison.Ordinal))
            {
                return;
            }

            bool accepted = LevelEditCommands.TryRecolorDoor(level, doorIndex, m_toolState.ColorId,
                out LevelData result, out string issue);
            ApplyCommand("Recolour Gate", accepted, result, issue);
        }

        // `[` / `]` step through the colour order, the swatches' grid order (palette order without a draft).
        private void CycleColor(int step)
        {
            LevelColorOrder order = m_draftController.Draft != null ? m_draftController.Draft.ColorOrder : LevelColorOrder.Default;
            int next = (order.IndexOf(m_toolState.Color) + step) % LevelColorOrder.k_count;
            PickColor(order.GetColorAt(next < 0 ? next + LevelColorOrder.k_count : next));
        }

        // Swatches and shape tiles cannot take focus, so a click on them would leave no element to receive the
        // shortcuts; the canvas takes it, as after a click on the board.
        private void PickSwatch(PaletteColor color)
        {
            m_canvas?.Focus();
            PickColor(color);
        }

        // A Pen colour key (TASK-20260926-180414067-b923c8a9): the colour whose badge shows the key's 1-based position
        // in the draft's colour order (palette order without a draft), picked as its swatch click picks it.
        private void PickColorAtPosition(int colorPosition)
        {
            LevelColorOrder order = m_draftController.Draft != null ? m_draftController.Draft.ColorOrder : LevelColorOrder.Default;
            PickColor(order.GetColorAt(colorPosition - 1));
        }

        // Color Shuffle: a random one-to-one recolour that moves at least one colour the level uses.
        private void ShuffleColors()
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null)
            {
                return;
            }

            LevelColorOrder from = m_draftController.Draft.ColorOrder;
            RecolorLevel("Shuffle Colours", level, from, LevelColorRemap.Shuffle(level, from, m_colorRandom));
        }

        // Color Switch (user orders of 2026-09-26 and 2026-09-27): the colour of the clicked block or gate trades places
        // with a random other gate colour, one the level does not use when there is one, in the whole level, so a red
        // block switches every red. The tool colour follows it, so the outlined swatch marks the new colour.
        private void SwitchColor()
        {
            LevelData level = m_draftController.Snapshot();
            if (level == null)
            {
                return;
            }

            if (!TryGetChosenColor(level, out PaletteColor current))
            {
                ShowHint(k_switchHint);
                return;
            }

            PaletteColor partner = LevelColorRemap.PickSwitchPartner(level, current, m_colorRandom);
            LevelColorOrder from = m_draftController.Draft.ColorOrder;
            RecolorLevel("Switch Colour", level, from, from.Swap(current, partner));
            m_toolState.SetColor(partner);
            SyncTool();
        }

        // The colour of the element the designer clicked, as a swatch recolours it (PickColor): the block the Pen edits,
        // else with Select a chosen block or gate. False for no choice or a colour outside the 11 gate colours.
        private bool TryGetChosenColor(LevelData level, out PaletteColor color)
        {
            color = default;
            string colorId = null;
            int blockIndex = IsPenEditing(out string editedId) ? LevelPieceEditController.FindBlockIndex(level, editedId) : -1;
            if (blockIndex >= 0)
            {
                colorId = level.GetBlockAt(blockIndex).ColorId;
            }
            else if (m_toolState.Tool == LevelEditorTool.Select && m_canvasSelection.Kind == LevelCanvasSelectionKind.Door)
            {
                int doorIndex = m_canvasSelection.Index;
                if (m_doorChosen && doorIndex >= 0 && doorIndex < level.DoorCount)
                {
                    colorId = level.GetDoorAt(doorIndex).ColorId;
                }
            }
            else if (m_toolState.Tool == LevelEditorTool.Select && m_blockChosen
                     && TryGetSelectedBlock(out _, out LevelBlockData block))
            {
                colorId = block.ColorId;
            }

            return colorId != null
                   && ColorBlockJam.Runtime.Layout.BoardLayout.TryParseDoorColor(colorId, out color)
                   && (int)color < LevelColorOrder.k_count;
        }

        // One undo step recolours the level on the board by the map between two orders; the draft's own order, and with
        // it the swatches, badges and Pen keys, stays (user order of 2026-09-27). The status names what changed.
        private void RecolorLevel(string undoName, LevelData level, LevelColorOrder from, LevelColorOrder to)
        {
            ApplyCommand(undoName, true, LevelColorRemap.Apply(level, from, to), null);
            SetStatus(DescribeRecolor(level, from, to));
        }

        // "Colours: Red → Purple, Blue → Yellow." for the colours this level uses.
        private static string DescribeRecolor(LevelData level, LevelColorOrder from, LevelColorOrder to)
        {
            bool[] used = LevelColorRemap.GetUsedColors(level);
            List<string> changes = new List<string>();
            for (int value = 0; value < used.Length; value++)
            {
                PaletteColor color = (PaletteColor)value;
                PaletteColor mapped = from.MapTo(to, color);
                if (used[value] && mapped != color)
                {
                    changes.Add($"{color} → {mapped}");
                }
            }

            return changes.Count == 0
                ? "Colour order changed; none of this level's colours moved."
                : "Colours: " + string.Join(", ", changes) + ".";
        }

        private void ArmShape(LevelShapeChoice shape)
        {
            m_canvas?.Focus();
            // A tile of a replaced catalog arms the template as the library defines it now, or nothing; a template with
            // a pending footprint places that footprint (DEC-125).
            RefreshShapeCatalog(false);
            LevelShapeCatalog catalog = GetEditCatalog();
            int index = shape != null ? catalog.IndexOfTemplate(shape.TemplateKey, shape.DisplayName) : -1;
            LevelShapeChoice current = index >= 0 ? catalog.Get(index) : null;
            m_toolState.Arm(current);
            SyncTool();
            // The linked Piece Editor shows the same template; its own prompt guards an unsaved draft there.
            if (current != null && IsPieceEditorOpen())
            {
                ShowLibraryPieceInEditor(current.TemplateKey);
            }
        }

        // A Level pieces tile arms Select with the level piece and, while the Piece Editor is open, shows it there in
        // level-piece mode, so edits there reach this piece's blocks.
        private void ArmCustomPiece(LevelShapeChoice piece)
        {
            m_canvas?.Focus();
            if (piece == null || piece.Cells.Count == 0)
            {
                return;
            }

            m_toolState.Arm(piece);
            SyncTool();
            string scope = GetOpenLevelScope();
            if (scope != null && IsPieceEditorOpen())
            {
                MirrorLevelPiece(m_pieceEditController.GetTileMirror(scope, m_draftController.Snapshot(), piece));
            }
        }

        /// <summary>
        /// The "+" New piece tile (user decision of 2026-09-25): a new level piece of the open level named "&lt;Level
        /// Name&gt; Piece &lt;n&gt;", a name no template, level of the database or other piece of this session uses. The
        /// Pen then draws it freehand, and the Piece Editor shows it in level-piece mode. It stays in the level; the
        /// Piece Editor's Save as template adds it to the piece library (DEC-180).
        /// </summary>
        private void CreateNamedPiece()
        {
            m_canvas?.Focus();
            RefreshShapeCatalog(false);
            LevelNamedPiece piece = m_pieceEditController.CreateNamedPiece(GetPieceSource());
            if (piece == null)
            {
                SetStatus("Open a level first: a new piece belongs to the open level.");
                return;
            }

            m_toolState.ArmFreehand();
            SyncTool();
            SetStatus($"New piece {piece.DisplayName}: click a free cell, then click or drag next to it to grow the " +
                      "piece; right click takes a cell off. It belongs to this level; Save as template in the Piece " +
                      "Editor adds it to the piece library. Esc cancels a drag.");
            MirrorLevelPiece(new LevelPieceRequest(piece.Scope, piece.Token, Array.Empty<Vector2Int>(),
                piece.DisplayName));
        }

        /// <summary>
        /// A released freehand click or drag grows the named piece the "+" tile made (user feedback of 2026-09-25:
        /// click-to-grow). The first one places the piece's block with its cells in the selected colour; each later
        /// one adds its new cells to that block when the piece stays one edge-connected footprint within the learning
        /// limits, and the piece's other blocks follow the new footprint in their own orientation. Each is one undo
        /// step, keeps the block's id, colour and effects and the piece's name, and is mirrored into the Piece Editor;
        /// otherwise the status says why and nothing changes.
        /// </summary>
        private void PlacePieceStroke(IReadOnlyList<Vector2Int> cells)
        {
            RefreshShapeCatalog(false);
            ApplyPieceEdit(m_pieceEditController.Draw(GetPieceSource(), m_toolState, cells));
        }

        /// <summary>
        /// The library template a Pen edit of this block changes (DEC-125): the edit catalog's template of the block's
        /// key, with any pending footprint. Null for a level piece, and for the block the Pen started editing as one.
        /// </summary>
        private LevelShapeChoice GetEditedTemplate(LevelBlockData block)
        {
            return m_pieceEditController.GetEditedTemplate(m_saveController.LoadedGuid, block);
        }

        /// <summary>
        /// A freehand right click on the growing piece's block takes that one cell off as one undo step, and the
        /// piece's other blocks follow. Refused with a message when the piece would split. A right click on the last
        /// cell removes the block, so a piece no other block uses leaves the Level pieces list (user request of
        /// 2026-09-26).
        /// </summary>
        private void RemovePieceCell(Vector2Int cell)
        {
            RefreshShapeCatalog(false);
            ApplyPieceEdit(m_pieceEditController.RemoveCell(GetPieceSource(), m_toolState, cell));
        }

        // After a click, drag or right click changed the piece: it takes its block's footprint, the block is selected,
        // the status says what changed and names blocks left overlapping, and the Piece Editor shows the piece (once
        // per change, never per move).
        private void OnPieceCellsChanged(string scope, LevelNamedPiece piece, string message)
        {
            LevelPieceRequest mirror = m_pieceEditController.UpdateDrawnPiece(m_saveController.LoadedGuid, scope, piece);
            LevelData level = m_draftController.Snapshot();
            int drawn = LevelPieceEditController.FindBlockIndex(level, piece.BlockId);
            m_canvasSelection = LevelCanvasSelection.ForBlock(drawn, piece.BlockId);
            m_selectedBlockIndex = drawn;
            // Only an explicitly Pen-edited block takes a swatch; the "+" drawing block is not a selection.
            m_blockChosen = !m_toolState.IsFreehandArmed;
            RefreshCustomPieces(level);
            RefreshBlockTools();
            SetStatus(message + GetOverlapNote(level));
            MirrorLevelPiece(mirror);
        }

        private void ApplyPieceEdit(LevelPieceEdit edit)
        {
            if (edit == null)
            {
                return;
            }

            if (edit.Level != null)
            {
                if (edit.RemovedBlockIndex >= 0 && edit.RemovedBlockIndex == m_selectedBlockIndex)
                {
                    m_canvasSelection = LevelCanvasSelection.None;
                    m_selectedBlockIndex = -1;
                }

                ApplyCommand(edit.UndoName, true, edit.Level, null, edit.ApplyMetadata);
            }

            if (edit.Piece != null)
            {
                OnPieceCellsChanged(edit.Scope, edit.Piece, edit.Message);
                return;
            }

            switch (edit.Feedback)
            {
                case LevelPieceFeedback.Hint:
                    ShowHint(edit.Message);
                    break;
                case LevelPieceFeedback.Refusal:
                    ShowShapeRefusal(edit.Message);
                    break;
                case LevelPieceFeedback.CommandRefused:
                    ApplyCommand(null, false, null, edit.Message);
                    break;
                default:
                    if (edit.Message != null)
                    {
                        SetStatus(edit.Message);
                    }
                    break;
            }
        }

        // Shows a level piece in the Piece Editor and remembers it as what that window shows.
        private void MirrorLevelPiece(LevelPieceRequest request)
        {
            m_pieceEditController.RememberMirror(request);
            ShowLevelPieceInEditor(request);
        }

        /// <summary>
        /// After an undo or redo here: the named piece the Piece Editor shows (else the one freehand clicks grow) takes
        /// its block's footprint again, and when that differs from what the Piece Editor shows, it is mirrored without
        /// the focus. The Piece Editor's draft is not part of this window's undo record, so without this its next
        /// change would build on the undone cells. The Piece Editor ignores a request equal to what it shows, so its
        /// own undo, which already reverted both windows, sends nothing back.
        /// </summary>
        private void MirrorUndoneNamedPiece()
        {
            if (!IsPieceEditorOpen())
            {
                return;
            }

            LevelPieceRequest request = m_pieceEditController.GetUndoMirror(m_saveController.LoadedGuid,
                GetOpenLevelScope());
            if (request != null)
            {
                MirrorLevelPiece(request);
            }
        }

        /// <summary>
        /// Tells the canvas which block Pen clicks grow and right clicks shrink: the "+" piece's block while it draws
        /// freehand, else the block the plain Pen edits (user order of 2026-09-26: a Pen click on a block starts
        /// editing it). Freehand mode on the canvas covers both, so they share its ghost, green cells, outline, strokes
        /// and cell removal. The palette outlines that block's piece and the template banner follows.
        /// </summary>
        private void SyncPieceDrawing()
        {
            LevelData level = m_draftController.Snapshot();
            m_pieceEditController.RemoveMissingPenEdit(level);

            string blockId = GetFreehandBlockId(out bool editing);
            int drawn = LevelPieceEditController.FindBlockIndex(level, blockId);
            m_canvas?.SetFreehand(m_toolState.IsFreehandArmed || editing);
            m_canvas?.SetPenEditing(editing);
            // A playing or paused solution's frames show no Pen edit outline or green cells; its end or stop shows them
            // again.
            m_canvas?.SetFreehandBlock(IsShowingSolution ? null : blockId);
            m_shapePalette?.SetEditing(drawn >= 0 ? level.GetBlockAt(drawn).Key : default);
            UpdateTemplateBanner(level, editing ? drawn : -1);
        }

        // The block the canvas draws freehand for: the "+" piece's block while it draws, else the block the plain Pen
        // edits, or null. editing is true while the plain Pen edits a block.
        private string GetFreehandBlockId(out bool editing)
        {
            editing = IsPenEditing(out string editBlockId);
            return m_toolState.IsFreehandArmed ? GetDrawingBlockId() : editBlockId;
        }

        /// <summary>
        /// The template banner under the top bar (DEC-125): while the Pen edits a template block, what a change
        /// reaches; for each pending footprint, what Save Level writes; for a footprint a failed commit left out of
        /// the piece library, the retry; then the save's conflicts and the levels that could not take it. Discard
        /// shows while the draft holds a pending footprint, and Keep library footprint while only saved ones wait.
        /// </summary>
        private void UpdateTemplateBanner(LevelData level, int editedIndex)
        {
            if (m_templateBanner == null)
            {
                return;
            }

            LevelShapeChoice edited = editedIndex >= 0 ? GetEditedTemplate(level.GetBlockAt(editedIndex)) : null;
            List<LevelTemplateEdit> saved = m_pieceSession.GetPendingTemplates(m_saveController.LoadedGuid);
            IReadOnlyList<LevelTemplateEdit> draft = m_draftController.Draft != null
                ? m_draftController.Draft.TemplateEdits
                : (IReadOnlyList<LevelTemplateEdit>)Array.Empty<LevelTemplateEdit>();
            if (level == null || (edited == null && saved.Count == 0 && draft.Count == 0))
            {
                // The text clears and the buttons hide too, so none keeps a state from the last pending footprint.
                m_templateBannerText.text = string.Empty;
                m_templateBanner.style.display = DisplayStyle.None;
                m_discardTemplateButton.style.display = DisplayStyle.None;
                m_separateTemplateButton.style.display = DisplayStyle.None;
                m_keepTemplateButton.style.display = DisplayStyle.None;
                return;
            }

            UpdateOtherLevels();
            LevelTemplateSavePlan plan = GetBannerPlan(level, saved, draft);
            string text = null;
            if (edited != null
                && m_draftController.Draft.FindTemplateEdit(edited.TemplateKey, edited.DisplayName) == null
                && FindEdit(saved, edited.TemplateKey) == null)
            {
                text = $"Editing template {edited.DisplayName}: a change reaches all " +
                       $"{CountBlocksUsing(level, edited.Key)} blocks here" +
                       $"{DescribeOtherBlocks(edited.TemplateKey, edited.DisplayName)} when you Save Level, and the " +
                       "piece library.";
            }

            for (int index = 0; index < draft.Count; index++)
            {
                LevelTemplateEdit edit = draft[index];
                text = JoinLines(text, $"Template {edit.DisplayName} changed: Save Level writes the " +
                                       $"{CountBlocksUsing(level, edit.Key)} block(s) here" +
                                       $"{DescribeOtherBlocks(edit.TemplateKey, edit.DisplayName, "re-stamps")}, then " +
                                       "updates the piece library. Other level databases keep the old footprint.");
            }

            for (int index = 0; index < saved.Count; index++)
            {
                LevelTemplateState state =
                    m_pieceSession.FindTemplate(m_saveController.LoadedGuid, saved[index].TemplateKey);
                text = JoinLines(text, state.Issue != null
                    ? $"{saved[index].DisplayName} is in the saved levels but not in the piece library yet: " +
                      $"{state.Issue} Save Level again to retry."
                    : $"{saved[index].DisplayName} is in the saved levels but not in the piece library yet. Save " +
                      "Level writes it there.");
            }

            for (int index = 0; plan != null && index < plan.Refusals.Count; index++)
            {
                text = JoinLines(text, plan.Refusals[index]);
            }

            if (plan != null && plan.Unfit.Count > 0)
            {
                text = JoinLines(text, "Save Level asks before leaving these levels on the old footprint: " +
                                       string.Join(" ", plan.Unfit));
            }

            m_templateBannerText.text = text ?? string.Empty;
            m_templateBanner.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
            m_discardTemplateButton.style.display = draft.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            // Make a separate piece needs the Pen on a block of a template with a pending change in this draft.
            bool separable = edited != null && m_draftController.Draft != null
                                            && m_draftController.Draft.FindTemplateEdit(edited.TemplateKey,
                                                edited.DisplayName) != null;
            m_separateTemplateButton.style.display = separable ? DisplayStyle.Flex : DisplayStyle.None;
            // An unsaved change is discarded first: it is measured from the saved footprint Keep would give up.
            m_keepTemplateButton.style.display = saved.Count > 0 && draft.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        /// <summary>
        /// The banner's Keep library footprint: the template footprints an earlier Save Level wrote to this database's
        /// levels are no longer written to the piece library, which keeps its own, so a commit that cannot succeed
        /// (the library changed elsewhere, the record is gone) stops blocking Save Level. The saved levels keep their
        /// blocks, which are level pieces then; the session remembers them, so Re-stamp in each level gives them the
        /// library footprint again. The session is not undoable, so neither is this.
        /// </summary>
        private void KeepLibraryFootprints()
        {
            string message = m_pieceEditController.KeepLibraryFootprints(m_saveController.LoadedGuid);
            if (message != null)
            {
                OnDraftChanged();
                SetStatus(message);
            }
        }

        // " and M in K other levels of <database>" (or "re-stamps M in K ..."), or nothing while the database is
        // unreadable.
        private string DescribeOtherBlocks(ShapeKey templateKey, string templateName, string verb = null)
        {
            return m_pieceEditController.DescribeOtherBlocks(GetPieceSource(), GetActiveDatabaseName(), templateKey,
                templateName, verb);
        }

        // The banner's dry run of Save Level's template plan, made again only when the draft, the pending footprints,
        // the library or the other levels changed. Null when no footprint is pending.
        private LevelTemplateSavePlan GetBannerPlan(LevelData level, List<LevelTemplateEdit> saved,
            IReadOnlyList<LevelTemplateEdit> draft)
        {
            return m_pieceEditController.GetBannerPlan(m_saveController.LoadedGuid, level, saved, draft);
        }

        private static LevelTemplateEdit FindEdit(IReadOnlyList<LevelTemplateEdit> edits, ShapeKey templateKey)
        {
            for (int index = 0; index < edits.Count; index++)
            {
                if (edits[index].TemplateKey == templateKey)
                {
                    return edits[index];
                }
            }

            return null;
        }

        /// <summary>
        /// The banner's Discard template change: every block of each changed template in the open level gets the saved
        /// footprint again (the library's, or the one an earlier Save Level wrote to this database while the library
        /// still lacks it) and the pending footprints are dropped, as one undo step. A block the edit began with goes
        /// back exactly where it was (D-001); a block placed during the edit is re-stamped in its own rotation or mirror,
        /// refused as a whole, naming the blocks, when one would leave the board or cover a void or a board object.
        /// </summary>
        private void DiscardTemplateEdits()
        {
            ApplyPieceEdit(m_pieceEditController.DiscardTemplateEdits(m_saveController.LoadedGuid));
        }

        /// <summary>
        /// The banner's Make a separate piece (user request of 2026-09-26): the block the Pen edits keeps its current
        /// cells and origin as a new named level piece of its own, every other block of its template goes back as
        /// Discard puts it, and the template's pending footprint is dropped, as one undo step. The template and the
        /// piece library do not change; the Pen keeps editing the block, now as a level piece.
        /// </summary>
        private void MakeSeparatePiece()
        {
            RefreshShapeCatalog(false);
            ApplyPieceEdit(m_pieceEditController.MakeSeparatePiece(GetPieceSource(), m_toolState));
        }

        // The plain Pen: no freehand drawing and no Barrier brush.
        private bool IsPlainPen()
        {
            return m_toolState.Tool == LevelEditorTool.Pen && !m_toolState.IsFreehandArmed
                   && m_toolState.ObstacleBrush == LevelObstacleBrush.None;
        }

        // True while the plain Pen edits a block of the open level that a Pen click made its target; blockId names it.
        // No other selection is ever a target.
        private bool IsPenEditing(out string blockId)
        {
            return m_pieceEditController.IsPenEditing(m_toolState, out blockId);
        }

        /// <summary>
        /// A Pen left click on a block that is not the one being edited (the canvas's PenBlockPicked): it becomes the
        /// Pen's edit target, selected and chosen, so its free neighbours show green, its piece tile is outlined and a
        /// swatch recolours it. A "+" New piece drawing is left for the plain Pen. The Effect Editor is not opened.
        /// </summary>
        private void EditBlockWithPen(int blockIndex)
        {
            LevelBlockData block = m_pieceEditController.StartPenEdit(m_saveController.LoadedGuid, blockIndex, m_toolState);
            if (block == null)
            {
                return;
            }

            m_selectedBlockIndex = blockIndex;
            m_canvasSelection = LevelCanvasSelection.ForBlock(blockIndex, block.Id);
            m_blockChosen = true;
            m_doorChosen = false;
            SyncTool();
            RefreshBlockTools();
            SetStatus(DescribePenEdit(block));
        }

        // The status when the Pen starts editing a block: a template or a level piece.
        private string DescribePenEdit(LevelBlockData block)
        {
            return m_pieceEditController.DescribePenEdit(m_saveController.LoadedGuid, block);
        }

        // The block id of the piece freehand clicks grow in the open level, or null.
        private string GetDrawingBlockId()
        {
            return m_pieceEditController.GetDrawingBlockId(GetOpenLevelScope());
        }

        // The open level's scope, its database JSON GUID and level id, because level ids repeat across databases;
        // null with no level open.
        private string GetOpenLevelScope()
        {
            return GetPieceSource().GetScope(m_draftController.Snapshot());
        }

        private LevelPieceSource GetPieceSource()
        {
            return new LevelPieceSource(Levels, m_saveController.LoadedGuid, m_saveController.LoadedSourceHash, m_selectedIndex, m_saveController.SourceValid);
        }

        // "<database name>/<level id>" for a status line; an unknown GUID is shown as it is.
        private string DescribeLevelScope(string scope)
        {
            if (!LevelPieceSession.TrySplitLevelScope(scope, out string guid, out string levelId))
            {
                return scope;
            }

            for (int index = 0; m_entries != null && index < m_entries.Count; index++)
            {
                if (string.Equals(m_entries[index].Guid, guid, StringComparison.Ordinal))
                {
                    return m_entries[index].Name + "/" + levelId;
                }
            }

            return scope;
        }

        /// <summary>
        /// A level-piece change in the Piece Editor (PieceEditorWindow.LevelPieceChanged: a stroke, a rotate or mirror
        /// click, a grid undo or redo, or a committed name), for the level piece its session token names (DEC-193). A
        /// change for another level is ignored. New cells re-stamp every block of the piece's key in its own rotation or
        /// mirror, or nothing changes when any would leave the board or cover a void or a board object (D3-A); blocks
        /// left overlapping are named in the status (D3-B). A new name goes into the level's pieces table. A piece the
        /// designer had armed is armed again with its new footprint, as the template it now is when the library holds
        /// it. The level edit joins the Piece Editor's open undo group, so one Undo reverts both; when the level
        /// already matches, as after that group's own undo, nothing is recorded.
        /// </summary>
        private void OnLevelPieceChanged(LevelPieceRequest request)
        {
            if (m_status == null || request == null || m_draftController.Draft == null)
            {
                return;
            }

            // A Piece Editor change delivered here stops a shown solution first (contract Revision 2, safety stop).
            StopSolutionPlayback();
            if (!string.Equals(request.LevelId, GetOpenLevelScope(), StringComparison.Ordinal))
            {
                SetStatus($"This piece belongs to {DescribeLevelScope(request.LevelId)}; open that level to edit it.");
                return;
            }

            RefreshShapeCatalog(false);
            LevelPieceEditorEdit edit = m_pieceEditController.PlanEditorChange(GetPieceSource(), request);
            if (edit == null)
            {
                return;
            }

            if (edit.Accepted)
            {
                ApplyJoinedCommand(edit.Before, edit.After);
                LevelShapeChoice shape =
                    m_pieceEditController.CompleteEditorChange(m_saveController.LoadedGuid, edit);
                if (m_toolState.HasArmedShape && m_toolState.ArmedKey == edit.PreviousKey)
                {
                    m_toolState.Arm(shape);
                    SyncTool();
                }
            }

            if (edit.RefreshNamedViews)
            {
                RefreshNamedPieceViews();
            }

            if (edit.IsRefusal)
            {
                ShowShapeRefusal(edit.Message);
            }
            else
            {
                SetStatus(edit.Message);
            }
        }

        // A level piece's new name or footprint: its tile and the Piece Type label show it.
        private void RefreshNamedPieceViews()
        {
            RefreshCustomPieces(m_draftController.Snapshot());
            RefreshBlockTools();
        }

        /// <summary>
        /// Applies a level edit a Piece Editor change caused. LevelPieceChanged is raised before the Piece Editor's
        /// undo group closes, so the record joins that group and one Undo reverts the piece and its blocks together;
        /// the group is neither opened nor closed here. A null or unchanged result records nothing.
        /// </summary>
        private void ApplyJoinedCommand(LevelData current, LevelData result)
        {
            if (m_draftController.ApplyJoined(current, result))
            {
                OnDraftChanged();
            }
        }

        private static int CountBlocksUsing(LevelData level, ShapeKey key)
        {
            return LevelPieceEditController.CountBlocksUsing(level, key);
        }

        // A refused Piece Editor change or Re-stamp: the status and the banner name it; the draft is unchanged.
        private void ShowShapeRefusal(string message)
        {
            m_shapeRefusal = message;
            UpdateShapeBanner(m_draftController.Snapshot());
            SetStatus(message);
        }

        /// <summary>
        /// Every successful piece library commit, including this window's own palette delete and a library undo. It
        /// runs inside the commit, so it only re-reads the library and this window's state: the library remembers the
        /// templates' earlier footprints for Re-stamp (RefreshCatalog), and the tracked level pieces whose key the
        /// commit added (Save as template) leave the session, since their key is a template everywhere now (DEC-193).
        /// </summary>
        private void OnLibraryCommitted()
        {
            if (m_status == null)
            {
                return;
            }

            // A piece library commit stops a shown solution first (contract Revision 2, safety stop).
            StopSolutionPlayback();
            LevelShapeCatalog previous = m_pieceEditController.Catalog;
            RefreshShapeCatalog(false);
            if (ReferenceEquals(previous, m_pieceEditController.Catalog))
            {
                return;
            }

            if (m_pieceEditController.DropNamedPiecesInLibrary(previous))
            {
                RefreshCustomPieces(m_draftController.Snapshot());
                // A "+" piece the commit made a template leaves nothing to draw, so freehand drawing ends on the plain
                // Pen instead of asking for + New piece at every click.
                if (m_toolState.IsFreehandArmed && m_pieceSession.DrawingPiece == null)
                {
                    m_toolState.SelectPen();
                }

                SyncTool();
                RefreshBlockTools();
                UpdateUnsavedChanges();
            }
        }

        /// <summary>
        /// The banner's Re-stamp: every block of the open level with an earlier footprint of a template gets the
        /// template's footprint now, in the rotation or mirror that mapped the earlier one onto it, all in one undo
        /// step. Refused as a whole, with the blocks listed, when any would leave the board or cover a void or a board
        /// object (D3-A); blocks left overlapping are named in the status (D3-B).
        /// </summary>
        private void RestampChangedPieces()
        {
            if (m_draftController.Draft == null)
            {
                return;
            }

            RefreshShapeCatalog(false);
            LevelPieceEdit edit = m_pieceEditController.RestampChangedPieces(m_saveController.LoadedGuid,
                out IReadOnlyList<ShapeKey> changedKeys);
            if (edit?.Level == null)
            {
                ApplyPieceEdit(edit);
                return;
            }

            ApplyCommand(edit.UndoName, true, edit.Level, null);
            m_pieceEditController.ForgetUnusedFootprints(changedKeys, GetDatabaseLevels());
            SetStatus(edit.Message);
        }

        // The open level from the draft, then every other level of the active database; each database read clones.
        private List<LevelData> GetDatabaseLevels()
        {
            return GetPieceSource().GetLevels(m_draftController.Snapshot());
        }

        private void ArmObstacleBarrier()
        {
            m_canvas?.Focus();
            m_toolState.ArmBarrier();
            SyncTool();
        }

        private void ArmObstacleVoid()
        {
            m_canvas?.Focus();
            m_toolState.ArmVoid();
            SyncTool();
        }

        /// <summary>Reloads a changed library and disarms missing or changed shapes before another stamp.</summary>
        private void RefreshShapeCatalog(bool force)
        {
            LevelShapeCatalog previous = m_pieceEditController.Catalog;
            if (!m_pieceEditController.RefreshCatalog(force))
            {
                return;
            }
            SetPaletteCatalog(GetEditCatalog());
            // The Re-stamp banner and the Level pieces section follow every library read.
            LevelData level = m_draftController.Snapshot();
            if (m_issuesList != null)
            {
                UpdateWallsAndIssues(level);
            }

            RefreshCustomPieces(level);
            // The edited piece's tile outline and the template banner's conflicts follow the new library too.
            SyncPieceDrawing();
            // An armed template the library changed or deleted is disarmed, so Select never places an outdated
            // footprint. A level piece is no library entry, so a library change never disarms it, and one the library
            // now holds, as after this window's own template commit or Save as template, stays armed as that template.
            ShapeKey armed = m_toolState.ArmedKey;
            if (armed.IsValid && previous.IndexOf(armed) >= 0 && m_pieceEditController.Catalog.IndexOf(armed) < 0)
            {
                m_toolState.Select();
                SyncTool();
                SetStatus("The piece library changed. Pick the shape again.");
            }
        }

        private void SetTool(LevelEditorTool tool)
        {
            switch (tool)
            {
                case LevelEditorTool.Pen:
                    // The Pen never holds a piece to place (user order of 2026-09-26: pieces are placed with Select).
                    // A Pen that draws freehand or holds the Barrier brush keeps it.
                    if (m_toolState.Tool == LevelEditorTool.Pen && (m_toolState.IsFreehandArmed
                                                                    || m_toolState.ObstacleBrush != LevelObstacleBrush.None))
                    {
                        break;
                    }

                    // Picking up the Pen starts no edit, whatever is selected: only a Pen click on a block does
                    // (user revision of 2026-09-26).
                    m_toolState.SelectPen();
                    break;
                case LevelEditorTool.Eraser:
                    m_toolState.SelectEraser();
                    break;
                case LevelEditorTool.RotateCounterClockwise:
                case LevelEditorTool.RotateClockwise:
                case LevelEditorTool.MirrorHorizontal:
                case LevelEditorTool.MirrorVertical:
                    // With a piece armed the button turns or mirrors its ghost and Select keeps placing it (user requests
                    // of 2026-09-26); otherwise it picks up the tool.
                    if (!TransformArmedPiece(tool))
                    {
                        m_toolState.SelectTransform(tool);
                    }

                    break;
                default:
                    m_toolState.Select();
                    break;
            }

            SyncTool();
        }

        // A transform button's action on the armed piece; false when no piece is armed.
        private bool TransformArmedPiece(LevelEditorTool tool)
        {
            switch (tool)
            {
                case LevelEditorTool.RotateCounterClockwise:
                    return m_toolState.RotateArmed(false);
                case LevelEditorTool.RotateClockwise:
                    return m_toolState.RotateArmed(true);
                case LevelEditorTool.MirrorHorizontal:
                    return m_toolState.FlipArmed(false);
                case LevelEditorTool.MirrorVertical:
                    return m_toolState.FlipArmed(true);
                default:
                    return false;
            }
        }

        // Shows the tool state on the canvas ghost and badge, the shape tiles, the swatches, the tool buttons and the
        // Wall tile.
        private void SyncTool()
        {
            // Esc, Select, the Eraser, the transform tools, "+" New piece and the Barrier brush end a Pen edit.
            if (!IsPlainPen())
            {
                m_pieceEditController.EndPenEdit();
            }

            // A tool change makes a hint stale.
            if (m_hint != null)
            {
                ShowHint(null);
            }

            m_canvas?.SetTool(m_toolState.Tool, m_toolState.GhostCells);
            m_canvas?.SetObstacleBrush(m_toolState.ObstacleBrush);
            m_canvas?.SetToolColor(m_toolState.Color);
            m_shapePalette?.SetArmed(m_toolState.ArmedKey);
            SyncPieceDrawing();
            m_shapePalette?.SetFreehandArmed(m_toolState.IsFreehandArmed);
            m_colorSwatches?.SetSelected(m_toolState.Color);
            SetToolHighlight(m_selectToolButton, m_toolState.Tool == LevelEditorTool.Select);
            SetToolHighlight(m_penToolButton, m_toolState.Tool == LevelEditorTool.Pen);
            SetToolHighlight(m_eraserToolButton, m_toolState.Tool == LevelEditorTool.Eraser);
            SetToolHighlight(m_rotateCounterClockwiseButton, m_toolState.Tool == LevelEditorTool.RotateCounterClockwise);
            SetToolHighlight(m_rotateClockwiseButton, m_toolState.Tool == LevelEditorTool.RotateClockwise);
            SetToolHighlight(m_flipBlockButton, m_toolState.Tool == LevelEditorTool.MirrorHorizontal);
            SetToolHighlight(m_flipVerticalButton, m_toolState.Tool == LevelEditorTool.MirrorVertical);
            SetToolHighlight(m_barrierObstacleButton, m_toolState.IsBarrierArmed);
            SetToolHighlight(m_voidObstacleButton, m_toolState.IsVoidArmed);
            // The Tools choice also drives the Piece Editor grid.
            PieceEditorWindow.SetLevelTool(m_toolState.Tool);
        }

        /// <summary>
        /// A template tile's ×. A template no block of the active database uses, or any template while that database
        /// cannot be read, is deleted after the designer confirms, through the Piece Editor's library transaction,
        /// which records the library's own undo; re-adding the same shape brings its key back (DEC-193). A template
        /// that blocks use asks Delete template, Delete template and its blocks or Cancel first (DeleteUsedShape,
        /// DEC-126).
        /// </summary>
        private void DeleteShape(LevelShapeChoice shape)
        {
            RefreshShapeCatalog(false);
            ShapeKey key = shape != null ? shape.TemplateKey : default;
            int index = m_pieceEditController.Catalog.IndexOf(key);
            if (index < 0 || m_pieceEditController.Library == null)
            {
                SetStatus("That piece is no longer in the piece library.");
                return;
            }

            if (RefusePendingTemplateDelete(shape))
            {
                return;
            }

            // The × that was clicked is rebuilt below, so the canvas takes the keys again, as after a swatch click.
            m_canvas?.Focus();
            if (CountTemplateUses(key) > 0)
            {
                DeleteUsedShape(shape);
                return;
            }

            int uses = CountBlocksUsing(key);
            string usage = uses < 0
                ? "The level database could not be read, so blocks that use it were not counted."
                : "No block in this level database uses it.";
            string message = $"Delete {shape.DisplayName} from the piece library?\n\n{usage}";
            if (!ConfirmPieceDelete(message))
            {
                return;
            }

            try
            {
                ShapeLibraryAuthoring.Delete(m_pieceEditController.Library, index);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException
                                              || exception is IOException || exception is UnauthorizedAccessException)
            {
                // The same authoring failures the Piece Editor reports: Play Mode, stale disk, read-only files.
                SetStatus($"Could not delete {shape.DisplayName}: {exception.Message}");
                return;
            }

            if (m_toolState.ArmedKey == key)
            {
                m_toolState.Select();
            }

            RefreshShapeCatalog(true);
            SyncTool();
            RefreshBlockTools();
            SetStatus($"Deleted {shape.DisplayName} from the piece library.");
        }

        /// <summary>
        /// The Piece Editor's "Delete selected piece" for a template of the active piece database, by its key, through
        /// LevelPieceDeleteRoute (DEC-126): the pending template refusal, then, when blocks of the active level
        /// database use the template, the tile ×'s question and order. Returns the status this window shows
        /// afterwards, which the Piece Editor shows too; null when no block uses the template or the database cannot be
        /// read, so the Piece Editor keeps its own question and delete.
        /// </summary>
        internal string DeleteLibraryPiece(ShapeKey key)
        {
            RefreshShapeCatalog(false);
            LevelShapeChoice shape = m_pieceEditController.Catalog.Find(key);
            if (shape == null || m_pieceEditController.Library == null)
            {
                return null;
            }

            if (!RefusePendingTemplateDelete(shape))
            {
                if (CountTemplateUses(key) == 0)
                {
                    return null;
                }

                DeleteUsedShape(shape);
            }

            return m_status != null ? m_status.text : string.Empty;
        }

        // Save Level would have no record to write a pending footprint to (DEC-125); true after showing the refusal.
        private bool RefusePendingTemplateDelete(LevelShapeChoice shape)
        {
            if ((m_draftController.Draft == null
                 || m_draftController.Draft.FindTemplateEdit(shape.TemplateKey, shape.DisplayName) == null)
                && !m_pieceSession.HasPendingTemplate(shape.TemplateKey))
            {
                return false;
            }

            SetStatus($"{shape.DisplayName} has a template change waiting for Save Level. Save Level or discard " +
                      "the change before deleting the piece.");
            return true;
        }

        // The blocks of the active database that use a template, in the open draft plus in the saved levels; 0 when the
        // database cannot be read, which keeps the two-button delete.
        private int CountTemplateUses(ShapeKey key)
        {
            int draftUses = CountBlocksUsing(key);
            return draftUses < 0 ? 0 : draftUses + LevelPieceDeletePlan.CountBlocks(CopyLevels(), key);
        }

        /// <summary>
        /// The delete of a template that blocks of the active database use (DEC-126). The library check and the plan
        /// run before the question. An accepted choice writes every level of the database in one atomic write, then
        /// changes the open draft as one undo step whose saved baseline moves with it (or opens the nearest level when
        /// Delete template and its blocks deleted the open one), then deletes the template through the undoable library
        /// delete. A failed write changes nothing; a failed library delete keeps the written levels, which are valid,
        /// and the status says to delete again.
        /// </summary>
        private void DeleteUsedShape(LevelShapeChoice shape)
        {
            ShapeKey key = shape.TemplateKey;
            string issue = LevelPieceDeleteRoute.GetLibraryIssue(m_pieceEditController.Library, key);
            if (issue != null)
            {
                SetStatus($"Deleted nothing. {shape.DisplayName} cannot be deleted from the piece library now: {issue}");
                return;
            }

            LevelData draft = m_draftController.Snapshot();
            LevelPieceDeletePlan plan = LevelPieceDeletePlan.Create(new LevelPieceDeletePlan.Input
            {
                TemplateKey = key,
                DisplayName = shape.DisplayName,
                DatabaseName = GetActiveDatabaseName(),
                SavedLevels = CopyLevels(),
                OpenIndex = draft != null ? m_selectedIndex : -1,
                Draft = draft,
                DraftHasChanges = HasDraftChanges(),
                OtherDatabases = CountOtherDatabaseUses?.Invoke(m_saveController.LoadedGuid, key)
            });
            LevelPieceDeleteResult result = LevelPieceDeleteOperation.Delete(plan, ChoosePieceDelete,
                WritePieceDeleteLevels, choice => ApplyPieceDeleteToDraft(plan, choice),
                () => (DeletePieceFromLibrary ?? DeleteFromProjectLibrary)(key));
            switch (result.Status)
            {
                case LevelPieceDeleteStatus.Refused:
                    SetStatus(plan.GetRefusalStatus());
                    return;
                case LevelPieceDeleteStatus.Cancelled:
                    SetStatus(LevelPieceDeletePlan.k_cancelledStatus);
                    return;
                case LevelPieceDeleteStatus.WriteFailed:
                    SetStatus("Deleted nothing. " + (string.IsNullOrEmpty(result.Error)
                        ? "The level JSON could not be written." : result.Error));
                    return;
                case LevelPieceDeleteStatus.NotReloaded:
                    SetStatus(plan.GetNotReloadedStatus(result.Choice, result.Error));
                    return;
            }

            if (result.Status == LevelPieceDeleteStatus.Deleted && m_toolState.ArmedKey == key)
            {
                m_toolState.Select();
            }

            RefreshShapeCatalog(true);
            SyncTool();
            RefreshBlockTools();
            SetStatus(result.Status == LevelPieceDeleteStatus.Deleted
                ? plan.GetDoneStatus(result.Choice)
                : plan.GetLibraryFailedStatus(result.Choice, result.Error));
        }

        // The write adapter invalidates cached levels even when JSON was written but could not be reloaded.
        // The operation stops before the draft/library change; this window presents the appropriate failure.
        private LevelFileWriteResult WritePieceDeleteLevels(IReadOnlyList<LevelData> levels)
        {
            string before = m_status != null ? m_status.text : null;
            LevelFileWriteResult file = WriteForSave(levels);
            if (file.Reloaded || file.JsonWritten)
            {
                m_pieceEditController.InvalidateOtherLevels();
            }

            string error = m_status != null && m_status.text != before ? m_status.text : null;
            return new LevelFileWriteResult(file.JsonWritten, file.Reloaded, error);
        }

        // After the write: the open level's draft takes the same change as one undo step, and its saved baseline moves
        // with it, so a clean draft stays clean and unsaved edits keep their undo history. When Delete Blocks deleted
        // the open level, the nearest remaining level opens, as after Delete Level.
        private void ApplyPieceDeleteToDraft(LevelPieceDeletePlan plan, LevelPieceDeleteChoice choice)
        {
            if (plan.DeletesOpenLevel && choice == LevelPieceDeleteChoice.DeleteBlocks)
            {
                m_selectedIndex = plan.GetNearestWrittenIndex(m_selectedIndex, choice);
                m_refreshing = true;
                UpdateLevelChoices();
                m_refreshing = false;
                LoadSelectedDraft();
                return;
            }

            if (m_selectedIndex >= 0)
            {
                m_selectedIndex = plan.GetWrittenIndex(m_selectedIndex, choice);
            }

            m_refreshing = true;
            UpdateLevelChoices();
            m_refreshing = false;
            if (m_draftController.Draft == null)
            {
                return;
            }

            LevelData baseline = m_draftController.GetSavedLevel();
            if (baseline != null)
            {
                m_draftController.SetSavedBaseline(plan.Apply(baseline, choice));
            }

            LevelData draft = m_draftController.Draft.Snapshot();
            LevelData result = plan.Apply(draft, choice);
            if (ReferenceEquals(result, draft))
            {
                OnDraftChanged();
                return;
            }

            if (choice == LevelPieceDeleteChoice.DeleteBlocks)
            {
                m_canvasSelection = LevelCanvasSelection.None;
                m_selectedBlockIndex = -1;
            }

            ApplyCommand(choice == LevelPieceDeleteChoice.DeleteTemplate ? "Name Template Blocks" : "Delete Piece Blocks",
                true, result, null);
        }

        // The project piece library's undoable delete of a template.
        private string DeleteFromProjectLibrary(ShapeKey key)
        {
            return LevelPieceDeleteRoute.DeleteLibraryRecord(m_pieceEditController.Library, key);
        }

        /// <summary>
        /// A Level pieces tile's × (user request of 2026-09-26): after the designer confirms, every block of the open
        /// level with the piece's key is removed as one undo step, so its tile goes with them and Undo brings both back.
        /// A piece no block uses is forgotten. A piece only other levels use is not changed from here: other levels are
        /// not in this level's undoable draft, and each is written only by its own Save Level.
        /// </summary>
        private void DeleteCustomPiece(LevelShapeChoice piece)
        {
            LevelData level = m_draftController.Snapshot();
            if (piece == null || level == null)
            {
                return;
            }

            // The × that was clicked is rebuilt below, so the canvas takes the keys again, as after a swatch click.
            m_canvas?.Focus();
            int uses = CountBlocksUsing(level, piece.Key);
            string otherLevels = GetOtherLevelsUsing(piece.Key);
            if (uses == 0 && otherLevels != null)
            {
                SetStatus($"{piece.DisplayName} is used only by {otherLevels}. Open that level to delete its blocks.");
                return;
            }

            string message = uses > 0
                ? $"Delete {piece.DisplayName} and its {uses} block(s) from {level.Name}?\n\nUndo brings them back."
                : $"Delete {piece.DisplayName}? No block uses it.";
            if (otherLevels != null)
            {
                message += $"\n\n{otherLevels} still use it and keep their blocks.";
            }

            if (!ConfirmPieceDelete(message))
            {
                return;
            }

            LevelNamedPiece drawing = m_pieceSession.DrawingPiece;
            if (m_toolState.ArmedKey == piece.Key || (m_toolState.IsFreehandArmed && drawing != null && drawing.Key == piece.Key))
            {
                m_toolState.Select();
            }

            LevelPieceEdit edit = m_pieceEditController.DeleteCustomPiece(GetOpenLevelScope(), piece, uses);
            if (edit.Level == null)
            {
                SyncTool();
                RefreshCustomPieces(level);
                SetStatus(edit.Message);
                return;
            }

            m_canvasSelection = LevelCanvasSelection.None;
            m_selectedBlockIndex = -1;
            ApplyCommand(edit.UndoName, true, edit.Level, null);
            SyncTool();
            SetStatus(edit.Message);
        }

        // The names of the other levels of the active database that have a block with this key, or null.
        private string GetOtherLevelsUsing(ShapeKey key)
        {
            List<string> names = null;
            for (int levelIndex = 0; m_saveController.SourceValid && levelIndex < Levels.Count; levelIndex++)
            {
                LevelData other = levelIndex != m_selectedIndex ? Levels.GetAt(levelIndex) : null;
                if (other != null && CountBlocksUsing(other, key) > 0)
                {
                    (names ??= new List<string>()).Add(other.Name);
                }
            }

            return names != null ? string.Join(", ", names) : null;
        }

        // Blocks of every level in the active database, with the selected level read from the draft; -1 when the
        // database could not be read.
        private int CountBlocksUsing(ShapeKey key)
        {
            return GetPieceSource().CountBlocksUsing(key, m_draftController.Snapshot());
        }

        // The active tool gets a blue fill inside a white square frame; the others keep the plain button look.
        private static void SetToolHighlight(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = active ? new StyleColor(new Color(0.24f, 0.42f, 0.66f)) : StyleKeyword.Null;
            StyleColor border = active ? new StyleColor(Color.white) : StyleKeyword.Null;
            button.style.borderLeftColor = border;
            button.style.borderRightColor = border;
            button.style.borderTopColor = border;
            button.style.borderBottomColor = border;
        }

        /// <summary>
        /// Applies one accepted edit command as one undo group on the draft, the single undo target
        /// (design section 4); a refused command changes nothing and shows its designer message. alsoApply changes the
        /// draft's pending template footprints in the same step, so Undo moves them with the level (DEC-125).
        /// </summary>
        private void ApplyCommand(string undoName, bool accepted, LevelData level, string issue,
            Action<LevelDraft> alsoApply = null)
        {
            if (!accepted || m_draftController.Draft == null)
            {
                // Also snaps a refused Piece Type choice back in its dropdown.
                RefreshBlockTools();
                if (issue != null)
                {
                    SetStatus(issue);
                }

                return;
            }

            m_draftController.Apply(undoName, level, alsoApply);
            OnDraftChanged();
        }

        private void UndoEdit()
        {
            m_canvas?.CancelDrag();
            Undo.PerformUndo();
        }

        private void RedoEdit()
        {
            m_canvas?.CancelDrag();
            Undo.PerformRedo();
        }

        // Window shortcuts (design section 2) through LevelEditorShortcuts; a key it runs goes no further.
        private void OnKeyDown(KeyDownEvent evt)
        {
            m_shortcuts ??= new LevelEditorShortcuts(this);
            if (m_shortcuts.Handle(evt))
            {
                evt.StopImmediatePropagation();
                evt.PreventDefault();
            }
        }

        bool ILevelEditorShortcutTarget.IsShowingSolution => IsShowingSolution;

        bool ILevelEditorShortcutTarget.IsPenActive => m_toolState.Tool == LevelEditorTool.Pen;

        bool ILevelEditorShortcutTarget.IsInValidationPanel(VisualElement element) =>
            IsWithin(element, m_validationPanel);

        void ILevelEditorShortcutTarget.StepSolution(bool forward)
        {
            if (forward)
                StepSolutionForward();
            else
                StepSolutionBack();
        }

        void ILevelEditorShortcutTarget.StopSolutionPlayback() => StopSolutionPlayback();

        void ILevelEditorShortcutTarget.UndoEdit()
        {
            Undo.FlushUndoRecordObjects();
            UndoEdit();
        }

        void ILevelEditorShortcutTarget.RedoEdit()
        {
            Undo.FlushUndoRecordObjects();
            RedoEdit();
        }

        void ILevelEditorShortcutTarget.PickColorAtPosition(int position) => PickColorAtPosition(position);

        void ILevelEditorShortcutTarget.NudgeSelection(Vector2Int step) => NudgeSelectedBlock(step);

        void ILevelEditorShortcutTarget.DeleteSelection() => DeleteSelection();

        void ILevelEditorShortcutTarget.RotateSelection(bool clockwise) => RotateSelection(clockwise);

        void ILevelEditorShortcutTarget.FlipSelection(bool vertical) => FlipSelection(vertical);

        void ILevelEditorShortcutTarget.SetTool(LevelEditorTool tool) => SetTool(tool);

        void ILevelEditorShortcutTarget.CancelCurrentAction()
        {
            if (m_pendingResize != null)
                CancelResize();
            else if (m_canvas == null || !m_canvas.CancelDrag())
                SetTool(LevelEditorTool.Select);
        }

        void ILevelEditorShortcutTarget.ResetView() => m_canvas?.ResetView();

        void ILevelEditorShortcutTarget.CycleColor(int step) => CycleColor(step);

        private bool HasDraftChanges()
        {
            return m_draftController.HasChanges(m_pieceEditController.Catalog);
        }

        private void UpdateDirtyStatus()
        {
            if (HasDraftChanges())
            {
                SetStatus(k_unsavedStatus);
            }
            else if (m_draftController.Draft != null && m_status != null && m_status.text == k_unsavedStatus)
            {
                // Undo, redo or a re-entered value brought back the saved level; other messages stay.
                SetStatus(GetEditingStatus());
            }
        }

        private string GetEditingStatus()
        {
            return $"Editing {Levels.GetAt(m_selectedIndex).Name}. Save Level writes the JSON source.";
        }

        private bool AskToLeaveDraft()
        {
            if (!HasDraftChanges())
            {
                return true;
            }

            switch (ChooseLeaveDraft())
            {
                case LevelLeaveChoice.Save:
                    return SaveLevel();
                case LevelLeaveChoice.Discard:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Builds the popup that the + button opens; tests drive it without opening a window.
        /// </summary>
        internal DatabaseNamePopup CreateDatabasePopup()
        {
            return new DatabaseNamePopup("New Level Database", GetSuggestedDatabaseName(),
                LevelDatabaseAddressables.GetNewNameError, CreateDatabase);
        }

        private void CreateDatabase(string name)
        {
            if (!AskToLeaveDraft())
            {
                return;
            }

            if (m_projectScope == null)
            {
                ShowError(LevelDatabaseAddressables.MissingProjectScopeMessage);
                return;
            }

            BoardKitAssets.Folder(LevelDatabaseAddressables.DatabaseDirectory);

            if (!LevelDatabaseAddressables.TryCreate(name, out LevelDatabaseAddressables.Entry entry, out string error))
            {
                ShowError(error);
                return;
            }

            if (!LevelDatabaseAddressables.TrySetActive(m_projectScope, entry.Guid, out error))
            {
                // The database exists now, so the dropdown lists it although the active one stays.
                RefreshLevelSelection();
                ShowError(error);
                return;
            }

            m_selectedIndex = -1;
            RefreshLevelSelection();
            SetStatus($"Created {entry.Name}. It is now the active game database.");
        }

        private void DeleteDatabase()
        {
            if (!CanDeleteActiveDatabase() || !AskToLeaveDraft())
            {
                return;
            }

            string name = GetActiveDatabaseName();
            string fallback = LevelDatabaseAddressables.DefaultDatabaseName;
            if (!EditorUtility.DisplayDialog(
                    "Delete Level Database",
                    $"Delete {name} and every level in it? {fallback} becomes the active game database.",
                    "Delete Database",
                    "Cancel"))
            {
                return;
            }

            string deletedGuid = ActiveGuid;
            if (!LevelDatabaseAddressables.TryDelete(m_projectScope, deletedGuid, out string error))
            {
                RefreshLevelSelection();
                ShowError(error);
                return;
            }

            // Footprints waiting for a deleted database's Save Level could never be written now.
            m_pieceSession.ForgetDatabase(deletedGuid);
            m_selectedIndex = -1;
            RefreshLevelSelection();
            SetStatus($"Deleted {name}. {fallback} is now the active game database.");
        }

        private bool CanDeleteActiveDatabase()
        {
            string path = LevelDatabaseAddressables.GetActivePath(m_projectScope);
            return !string.IsNullOrEmpty(path)
                && !string.Equals(path, LevelDatabaseAddressables.DefaultManifestPath, StringComparison.Ordinal);
        }

        private static string GetSuggestedDatabaseName()
        {
            int number = 2;
            while (Directory.Exists($"{LevelDatabaseAddressables.DatabaseDirectory}/LevelDatabase{number}"))
            {
                number++;
            }

            return $"LevelDatabase{number}";
        }

        private void CreateLevel()
        {
            if (!m_saveController.SourceValid || !AskToLeaveDraft())
            {
                return;
            }

            int number = Levels.Count + 1;
            string id;
            do
            {
                id = $"level-{number:D3}";
                number++;
            } while (Levels.IndexOf(id) >= 0);

            LevelData level = CreateStarterLevel(id, $"Level {number - 1}");
            List<LevelData> levels = CopyLevels();
            levels.Add(level);
            if (!SaveLevels(levels))
            {
                return;
            }

            m_selectedIndex = levels.Count - 1;
            RefreshLevelSelection();
            SetStatus($"Created a valid starter level in {GetActiveDatabaseName()}.");
        }

        /// <summary>
        /// Seeds Create Level's starter door and block with an exact PaletteColor
        /// name, because BoardLayout.TryParseDoorColor compares names ordinally.
        /// </summary>
        internal static LevelData CreateStarterLevel(string id, string name)
        {
            return new LevelData(
                id, name, 3, 3, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 1, 1, "Red") },
                new[]
                {
                    new LevelBlockData(
                        "b1", "Red", new Vector2Int(1, 1),
                        new[] { Vector2Int.zero })
                });
        }

        private void DeleteLevel()
        {
            if (!m_saveController.SourceValid || m_selectedIndex < 0 || !AskToLeaveDraft())
            {
                return;
            }

            string name = Levels.GetAt(m_selectedIndex).Name;
            if (!EditorUtility.DisplayDialog("Delete Level", $"Delete {name} from this database?", "Delete", "Cancel"))
            {
                return;
            }

            List<LevelData> levels = CopyLevels();
            levels.RemoveAt(m_selectedIndex);
            if (!SaveLevels(levels))
            {
                return;
            }

            m_selectedIndex = Mathf.Min(m_selectedIndex, levels.Count - 1);
            RefreshLevelSelection();
            SetStatus($"Deleted {name} from {GetActiveDatabaseName()}.");
        }

        private bool SaveLevel()
        {
            // Save Level, and Test Level and save-on-close through it, stop a playing solution first.
            StopSolutionPlayback();
            LevelSaveValidation validation = m_saveController.Validate(m_selectedIndex);
            if (validation.Candidate == null)
            {
                if (validation.Error != null)
                {
                    if (validation.ShowDialog)
                    {
                        ShowError(validation.Error);
                    }
                    else
                    {
                        SetStatus(validation.Error);
                    }
                }

                return false;
            }

            // The solvability check comes after stage 1 and the barrier gate and before any plan or write (DEC-144).
            string checkNote = null;
            if (m_checksSolvabilityOnSave && !ConfirmSolvability(validation.Candidate, out checkNote))
            {
                return false;
            }

            RefreshShapeCatalog(false);
            LevelSavePlan plan = m_saveController.Prepare(m_selectedIndex, validation.Candidate);
            if (plan.Templates != null && !ConfirmTemplatePlan(plan.Templates))
            {
                return false;
            }

            LevelSaveResult result = m_saveController.Save(plan, GetActiveDatabaseName(), WriteForSave,
                CommitTemplatesToLibrary, OnSaveProgress);
            if (checkNote != null && result.File.Reloaded)
            {
                SetStatus(m_status != null && m_status.text.Length > 0 ? m_status.text + " " + checkNote : checkNote);
            }

            return result.File.Reloaded;
        }

        // Validate Level (DEC-144): shows whether the open draft can be solved without boosters, and the validation
        // panel lists its distinct solutions on page 1 (TASK-20260928-091128541-cb48c84a). It never saves and never
        // changes the draft, its Undo history or the dirty state.
        // In a designer's window the search runs on a worker thread (DEC-175): the Editor stays usable, the panel's
        // summary and the status line show its progress, and the button reads Cancel until the answer arrives.
        private void ValidateLevel()
        {
            if (m_validationFlow.IsRunning)
            {
                // The button reads Cancel while a run goes on; its answer still arrives through OnValidationRunFinished.
                m_validationFlow.CancelRun();
                return;
            }

            StopSolutionPlayback();
            LevelData level = GetDraftLevel();
            if (level == null)
            {
                return;
            }

            SetValidationNames(level);
            LevelValidationOutcome outcome;
            if (!m_validationFlow.RunsInBackground)
            {
                outcome = m_validationFlow.CheckAndList(level);
            }
            else if (!m_validationFlow.StartCheckAndList(level, out outcome))
            {
                ForgetValidationAnswer();
                StartValidationPolling();
                return;
            }

            SetStatus(outcome.Text);
            ShowValidationAnswer(level, outcome);
        }

        // Subscribes the progress poll for a background run and shows its first line at once.
        private void StartValidationPolling()
        {
            if (!m_pollsValidation)
            {
                m_pollsValidation = true;
                // update, not delayCall or a panel schedule, so the progress and the answer also arrive while the Editor
                // is in the background. Owner: this window instance. Stop point: StopValidationPolling, called on the
                // run's answer, a draft change, Play Mode, and OnDisable.
                AddValidationUpdate(PollValidationRun);
            }

            ShowValidationProgress();
            UpdateSaveButton();
        }

        private void StopValidationPolling()
        {
            if (m_pollsValidation)
            {
                RemoveValidationUpdate(PollValidationRun);
                m_pollsValidation = false;
            }

            UpdateSaveButton();
        }

        // Every Editor update while a background run goes on: takes its answer once it has ended, otherwise refreshes the
        // progress line at most ten times a second.
        private void PollValidationRun()
        {
            if (m_validationFlow.TryFinishRun())
            {
                return;
            }

            if (!m_validationFlow.IsRunning)
            {
                StopValidationPolling();
                return;
            }

            if (EditorApplication.timeSinceStartup - m_validationProgressShownAt >= k_validationProgressSeconds)
            {
                ShowValidationProgress();
            }
        }

        private void ShowValidationProgress()
        {
            string text = m_validationFlow.GetRunProgressText();
            if (text == null)
            {
                return;
            }

            m_validationProgressShownAt = EditorApplication.timeSinceStartup;
            m_validationPanel?.ShowProgress(text);
            SetStatus(text);
        }

        // A background run's answer for the unchanged draft, from the poll or from Save Level's wait: the panel and the
        // status line show it as a synchronous Validate Level would.
        private void OnValidationRunFinished(LevelData level, LevelValidationOutcome outcome)
        {
            StopValidationPolling();
            SetStatus(outcome.Text);
            ShowValidationAnswer(level, outcome);
        }

        // The panel shows Validate Level's answer for this level: a listed Solvable level's summary and entries, any
        // other answer's sentence with an empty list. It stays while the draft keeps this content, level and JSON.
        private void ShowValidationAnswer(LevelData level, LevelValidationOutcome outcome)
        {
            m_listedAnswer.Set(level, m_saveController.LoadedGuid, m_selectedIndex, outcome.Result);
            if (m_validationPanel == null)
            {
                return;
            }

            if (outcome.Result != null && outcome.Result.Solutions.Count > 0)
            {
                m_validationPanel.ShowSolutions(outcome.SummaryText, level, outcome.Result.Solutions);
            }
            else
            {
                m_validationPanel.ShowAnswer(outcome.Text);
            }
        }

        // The listed answer lives while the draft keeps the content, level and JSON source it was made for, so a save
        // that reloads the same content keeps it; any other draft change shows the hint again.
        private void ClearValidationAnswerIfChanged(LevelData level)
        {
            if (!m_listedAnswer.IsEmpty && !m_listedAnswer.IsFor(level, m_saveController.LoadedGuid, m_selectedIndex))
            {
                ForgetValidationAnswer();
            }
        }

        private void ForgetValidationAnswer()
        {
            m_listedAnswer.Clear();
            m_validationPanel?.ShowHint();
        }

        /// <summary>True while a listed solution shows on the canvas, playing or paused.</summary>
        private bool IsShowingSolution => m_solutionPlayback != null && m_solutionPlayback.IsActive;

        // A row click shows that listed entry from its start board: with Auto Play on it plays at once, with it off it
        // waits paused for Step or Resume (contract Revision 1). A click on the active row's body changes nothing; its
        // controls pause, step and stop it. In Play Mode, where the rows are disabled, nothing starts (UpdateSaveButton's
        // Play Mode rule).
        private void PlaySolution(int entry)
        {
            if (IsShowingSolution && m_solutionPlayback.EntryIndex == entry)
            {
                return;
            }

            // A click that cannot play stops what shows, as every other stop point does.
            LevelCheckResult listed = m_listedAnswer.Result;
            if (m_validationFlow.IsPlaying() || listed == null || entry < 0 || entry >= listed.Solutions.Count)
            {
                StopSolutionPlayback();
                return;
            }

            // Play replaces a playing or paused entry itself, so the canvas and the preview go from its frame straight
            // to the new start board: no draft restore and no draft publish land between the two entries.
            m_solutionPlayback.Play(entry, m_listedAnswer.Level, listed.Solutions[entry].Steps,
                paused: !m_autoPlaysSolutions);
            // The frames show neither the selection outline nor a Pen edit's outline and green cells.
            m_canvas?.SetSelection(LevelCanvasSelection.None);
            m_canvas?.SetFreehandBlock(null);
            ShowPlaybackState();
        }

        // The active row's Pause or Resume button: Pause keeps the shown frame and removes the update callback; Resume
        // shows the next frame after the shown frame's full time.
        private void PauseOrResumeSolution()
        {
            if (!IsShowingSolution)
            {
                return;
            }

            if (m_solutionPlayback.IsPaused)
            {
                m_solutionPlayback.Resume();
            }
            else
            {
                m_solutionPlayback.Pause();
            }

            ShowPlaybackState();
        }

        // The active row's Step button: one more frame, paused; a Step on the last frame ends the playback, and the
        // draft shows again (RestoreAfterSolutionPlayback, the playback's end callback).
        private void StepSolution()
        {
            if (!IsShowingSolution)
            {
                return;
            }

            m_solutionPlayback.Step();
            if (IsShowingSolution)
            {
                ShowPlaybackState();
            }
        }

        // → while a solution shows (contract Revision 2, amended for the safety order): the next frame, paused; on the
        // cleared board it only pauses and never ends the playback.
        private void StepSolutionForward()
        {
            if (IsShowingSolution)
            {
                m_solutionPlayback.StepForward();
                ShowPlaybackState();
            }
        }

        // ← while a solution shows (contract Revision 2): the previous frame, paused; on the start board nothing happens.
        private void StepSolutionBack()
        {
            if (IsShowingSolution && m_solutionPlayback.StepBack())
            {
                ShowPlaybackState();
            }
        }

        // The active row's Stop button, one of the playback's stop points.
        private void StopSolution()
        {
            StopSolutionPlayback();
        }

        // The panel shows the active entry's controls, Pause or Resume by the playback's state, or ▶ on every row.
        private void ShowPlaybackState()
        {
            m_validationPanel?.SetPlayback(m_solutionPlayback.EntryIndex, m_solutionPlayback.IsPaused);
        }

        // One playback frame on the canvas and, through its own event, in the scene preview. It never goes through
        // SyncCanvas, so the stored selection is not resolved against it, and it never becomes LastPublishedLevel.
        private void ShowSolutionFrame(LevelData frame)
        {
            m_canvas?.SetModel(LevelCanvasModel.Create(frame));
            SolutionFramePublished?.Invoke(frame);
        }

        /// <summary>
        /// Every playback stop point calls this: the playback stops at once, playing or paused, with no update callback
        /// left, and the canvas and the scene preview show the draft again. False when no solution showed.
        /// </summary>
        private bool StopSolutionPlayback()
        {
            if (m_solutionPlayback == null || !m_solutionPlayback.Stop())
            {
                return false;
            }

            RestoreAfterSolutionPlayback();
            return true;
        }

        // After the playback's end or a stop: the canvas shows the draft with the selection and any Pen edit as they
        // are, the rows show their play glyph, and the scene preview gets the draft on the next editor update.
        private void RestoreAfterSolutionPlayback()
        {
            m_canvas?.SetModel(LevelCanvasModel.Create(m_draftController.Snapshot()));
            m_canvas?.SetSelection(m_canvasSelection);
            m_canvas?.SetFreehandBlock(GetFreehandBlockId(out _));
            ShowPlaybackState();
            SchedulePublish();
        }

        // A press on the canvas. While a solution shows, playing or paused, it stops it and goes no further
        // (PreventDefault also withholds the compatibility mouse event that the canvas's edge buttons would act on), and
        // it opens no scene. Any other press reaches the canvas tools as before, and its end is awaited.
        private void OnCanvasPressStarted(PointerDownEvent evt)
        {
            if (StopSolutionPlayback())
            {
                m_scenePressPointerId = -1;
                evt.StopImmediatePropagation();
                evt.PreventDefault();
                return;
            }

            m_scenePressPointerId = evt.pointerId;
        }

        // Any press in the window, before anything under it sees the press. A press outside the validation panel stops a
        // shown solution first (contract Revision 2, safety stop) and then goes on to do its normal work on the restored
        // draft; it is never consumed here. The canvas slot and the panel keep their own rules: a canvas press stops
        // the solution in OnCanvasPressStarted and edits nothing, and the panel's rows, controls, pager, toggles and
        // buttons act as before. Every press also forgets a canvas press released outside the canvas, so a later press
        // that starts elsewhere never ends it.
        private void OnWindowPointerDown(PointerDownEvent evt)
        {
            m_scenePressPointerId = -1;
            VisualElement target = evt.target as VisualElement;
            if (IsShowingSolution && !IsWithin(target, m_canvasSlot) && !IsWithin(target, m_validationPanel))
            {
                StopSolutionPlayback();
            }
        }

        // The element itself or one of its ancestors is the given one.
        private static bool IsWithin(VisualElement element, VisualElement ancestor)
        {
            return element != null && ancestor != null && (element == ancestor || ancestor.Contains(element));
        }

        // The end of a noted canvas press (contract Revision 1): in a designer's window, with the LevelEditor scene
        // closed and the Editor in Edit Mode, the scene opens on the next editor update, never while the press or a
        // drag goes on.
        private void EndCanvasPress(int pointerId)
        {
            if (pointerId != m_scenePressPointerId)
            {
                return;
            }

            m_scenePressPointerId = -1;
            if (CanOpenLevelEditorScene())
            {
                ScheduleSceneOpen();
            }
        }

        // A designer's window whose scene prompt was not cancelled, with the LevelEditor scene closed and the Editor in
        // Edit Mode.
        private bool CanOpenLevelEditorScene()
        {
            return m_isDesignerWindow && !m_sceneOpenDeclined && IsInEditMode() && !IsLevelEditorSceneLoaded();
        }

        private void ScheduleSceneOpen()
        {
            if (m_sceneOpenScheduled)
            {
                return;
            }

            m_sceneOpenScheduled = true;
            // update, not delayCall, which Unity skips while the Editor is in the background (as SchedulePublish).
            // Owner: this window instance. Stop point: OpenSceneAfterPress unsubscribes itself on its one run, or
            // OnDisable unsubscribes one that has not run yet.
            EditorApplication.update += OpenSceneAfterPress;
        }

        // The one-shot after a canvas press: the workspace launcher's scene step (Unity's save prompt for modified
        // scenes, then the LevelEditor scene single; it never saves that scene), unless the scene opened, Play Mode
        // began or the designer cancelled meanwhile. Cancel stops these prompts until the window is opened through Open
        // again.
        private void OpenSceneAfterPress()
        {
            EditorApplication.update -= OpenSceneAfterPress;
            m_sceneOpenScheduled = false;
            if (CanOpenLevelEditorScene() && OpenLevelEditorScene() == LevelEditorWorkspace.SceneOutcome.Cancelled)
            {
                m_sceneOpenDeclined = true;
            }
        }

        // Save Level's solvability step: a proven unsolvable or unconfirmed level asks first, and a cancelled check
        // cancels the save. False writes nothing and keeps the draft dirty. The note is a Not checked reason for the
        // status line after the save message, or null.
        private bool ConfirmSolvability(LevelData candidate, out string note)
        {
            SetValidationNames(candidate);
            LevelValidationOutcome outcome = m_validationFlow.Check(candidate);
            note = outcome.SaveNote;
            bool declined = outcome.IsCancelled
                            || (outcome.NeedsConfirmation && !ConfirmUnsolvableSave(
                                LevelValidationFlow.GetWarningTitle(outcome),
                                LevelValidationFlow.GetWarningMessage(outcome, candidate.Name)));
            if (declined)
            {
                note = null;
                SetStatus(LevelValidationFlow.k_saveCancelledPrefix + outcome.Text);
            }

            return !declined;
        }

        // The check names blocks by their piece: the templates' names and this level's piece names, read here on the
        // main thread into the lookup a background check reads on its worker thread (DEC-175, DEC-193).
        private void SetValidationNames(LevelData level)
        {
            List<LevelData> others = new List<LevelData>();
            for (int index = 0; m_saveController.SourceValid && index < Levels.Count; index++)
            {
                if (index != m_selectedIndex)
                {
                    others.Add(Levels.GetAt(index));
                }
            }

            m_validationFlow.ShapeNames =
                m_pieceEditController.CreateShapeNameLookup(m_saveController.LoadedGuid, level, others);
        }

        // Open Level Editor Scene: the same scene step a canvas press runs (Unity's save prompt, then the scene single;
        // it never saves the LevelEditor scene). An explicit press asks even after an earlier prompt was cancelled.
        private void OpenSceneFromButton()
        {
            if (IsLevelEditorSceneLoaded())
            {
                SetStatus("The LevelEditor scene is already open.");
                return;
            }

            switch (OpenLevelEditorScene())
            {
                case LevelEditorWorkspace.SceneOutcome.Opened:
                    m_sceneOpenDeclined = false;
                    SetStatus("Opened the LevelEditor scene. It shows the selected level in 3D.");
                    break;
                case LevelEditorWorkspace.SceneOutcome.RefusedInPlayMode:
                    SetStatus(k_openScenePlayingTooltip);
                    break;
                case LevelEditorWorkspace.SceneOutcome.Cancelled:
                    SetStatus("The LevelEditor scene did not open.");
                    break;
            }
        }

        // Test Level's button (contract Revision 1): Stop Test while Play Mode runs, Test Level otherwise.
        private void TestOrStopLevel()
        {
            if (IsPlayModeRunning())
            {
                StopTest();
                return;
            }

            TestLevel();
        }

        // Stop Test ends Play Mode through its step. Unity then reopens the LevelEditor scene, and OnPlayModeStateChanged
        // comes back to the tested level as after any test play (DEC-127). The button waits while Play Mode stops.
        private void StopTest()
        {
            StopTestPlay();
            UpdateSaveButton();
        }

        // Play Mode runs and is not about to stop.
        private bool IsPlayModeRunning()
        {
            return IsInPlayMode() && m_validationFlow.IsPlaying();
        }

        // Neither in Play Mode nor entering or leaving it.
        private bool IsInEditMode()
        {
            return !IsInPlayMode() && !m_validationFlow.IsPlaying();
        }

        // Test Level (PRD 11.5, DEC-127): unsaved changes go through Save Level first, so the Game scene plays exactly
        // what the JSON source holds; then the Game scene plays this level, found by its stable ID.
        private void TestLevel()
        {
            // Stops a playing solution first; the publish its stop schedules is dropped below when Play Mode starts.
            StopSolutionPlayback();
            if (m_selectedIndex < 0 || !m_saveController.SourceValid)
            {
                return;
            }

            // The game loads the active level JSON, so a switch made outside this window is reloaded first.
            if (!string.Equals(ActiveGuid, m_saveController.LoadedGuid, StringComparison.Ordinal))
            {
                SetStatus("The active level JSON changed outside the Level Editor. Press Reload JSON before testing " +
                          "a level.");
                return;
            }

            if (HasDraftChanges() && !SaveLevel())
            {
                return;
            }

            LevelData level = Levels.GetAt(m_selectedIndex);
            string refusal = StartTestPlay(new LevelTestPlayRequest(ActiveGuid, level.Id));
            if (refusal != null)
            {
                SetStatus(refusal);
                return;
            }

            m_testedLevelName = level.Name;
            // Play Mode begins on the next Editor update. The scene preview leaves now, and no publish waits to bring
            // it back before then, so its DontSave objects never join the Game scene; OnPlayModeStateChanged publishes
            // the draft again in Edit Mode.
            EditorApplication.update -= PublishDraft;
            m_publishScheduled = false;
            LastPublishedLevel = null;
            DraftLevelPublished?.Invoke(null);
            UpdateSaveButton();
            SetStatus($"Testing {level.Name} in the Game scene. Stop Play Mode to come back to it.");
        }

        // Test Level's button follows the Play Mode state (Stop Test while it runs, waiting while it starts or stops).
        // After a test play, Edit Mode shows the scene preview again and hands the focus back to this window, still on
        // the tested level (PRD 11.5).
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Leaving Edit Mode stops a playing or paused solution, so no playback outlives Edit Mode, and a background
            // Validate Level run, which Play Mode refuses as it refuses a new check (DEC-175).
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                StopSolutionPlayback();
                if (m_validationFlow.IsRunning)
                {
                    m_validationFlow.StopRun();
                    StopValidationPolling();
                    m_validationPanel?.ShowHint();
                }
            }

            UpdateSaveButton();
            if (state != PlayModeStateChange.EnteredEditMode || string.IsNullOrEmpty(m_testedLevelName))
            {
                return;
            }

            SchedulePublish();
            SetStatus($"Back from testing {m_testedLevelName}. Keep editing it, or press Test Level again.");
            m_testedLevelName = null;
            Focus();
        }

        // Controls follow each saved-model boundary before a synchronous library commit can re-enter or reload.
        private void OnSaveProgress(LevelSaveProgress progress)
        {
            switch (progress.Stage)
            {
                case LevelSaveStage.LevelWritten:
                    m_refreshing = true;
                    UpdateLevelChoices();
                    m_refreshing = false;
                    OnDraftChanged();
                    break;
                case LevelSaveStage.TemplatesCommitted:
                    RefreshShapeCatalog(false);
                    OnDraftChanged();
                    UpdateUnsavedChanges();
                    break;
            }

            if (progress.Message != null)
            {
                SetStatus(progress.Message);
            }
        }

        // A conflict refuses the whole save with the reasons in the status line and the banner; other levels that
        // cannot take a new footprint are named in one question whose default is Cancel. False writes nothing.
        private bool ConfirmTemplatePlan(LevelTemplateSavePlan plan)
        {
            if (plan.Refusals.Count > 0)
            {
                // The banner lists the same conflicts until they are resolved.
                SyncPieceDrawing();
                SetStatus("Save Level wrote nothing. " + string.Join(" ", plan.Refusals));
                return false;
            }

            if (plan.Unfit.Count == 0)
            {
                return true;
            }

            string message = "These levels cannot take the new template footprint:\n\n" +
                             string.Join("\n", plan.Unfit) +
                             "\n\nSave the other levels and the piece library, and leave these levels on the old " +
                             "footprint? Their blocks are level pieces then, keeping the template's name, until you " +
                             "Re-stamp them in each level.";
            if (ConfirmTemplateSave(message))
            {
                return true;
            }

            SetStatus("Save Level wrote nothing: " + string.Join(" ", plan.Unfit));
            return false;
        }

        private void ReloadDatabase()
        {
            if (m_projectScope == null || !AskToLeaveDraft())
            {
                return;
            }

            string note = RefreshLevelSelection();
            if (m_saveController.SourceValid)
            {
                SetStatus($"Reloaded {GetActiveDatabaseName()} from disk." + note);
            }
        }

        // The JSON Source row's Restore (TASK-20260926-233610537-9279a211): with unsaved draft changes it discards them
        // back to the saved level; without any it is Restore Previous Save.
        private void RestoreLevel()
        {
            if (m_saveController.SourceValid && HasDraftChanges())
            {
                DiscardDraftChanges();
                return;
            }

            RestorePreviousSave();
        }

        // After a Discard answer the selected level loads again from the loaded source, as a level switch does after
        // AskToLeaveDraft's Discard: the pending template edits, the colour order and the draft's undo history go with
        // the old draft. Nothing is written to disk. Cancel changes nothing.
        private void DiscardDraftChanges()
        {
            string name = Levels.GetAt(m_selectedIndex).Name;
            if (!ConfirmDiscardChanges($"Discard your unsaved changes to {name} and return it to its last saved state?"))
            {
                return;
            }

            LoadSelectedDraft();
            SetStatus($"Discarded unsaved changes: {name} is back to its saved state.");
        }

        private void RestorePreviousSave()
        {
            if (!m_saveController.SourceValid || !AskToLeaveDraft())
            {
                return;
            }

            if (!m_saveController.ReadBackup(ActiveGuid, out LevelData[] levels, out string error))
            {
                ShowError(error);
                return;
            }

            // WriteForSave is SaveLevels unless a test replaced WriteLevels.
            if (!WriteForSave(levels).Reloaded)
            {
                return;
            }

            // A template footprint the restored levels no longer hold is dropped here, not re-applied by the next save.
            string note = RefreshLevelSelection();
            SetStatus("Restored the previous saved level JSON. The replaced version is now the backup." + note);
        }

        private string GetBackupFolder()
        {
            return GetBackupFolder(ActiveGuid);
        }

        /// <summary>
        /// The one previous version of the whole database a save keeps (DEC-196); the Piece Editor route writes the same
        /// folder.
        /// </summary>
        internal static string GetBackupFolder(string sourceGuid)
        {
            return LevelSaveController.GetBackupFolder(sourceGuid);
        }

        // The loaded source's saved levels: the save controller's own catalog, apart from the game's (DEC-192).
        private LevelCatalog Levels => m_saveController.Levels;

        // The level JSON the game plays, or null without a project scope installer (DEC-194).
        private string ActiveGuid => m_projectScope != null ? m_projectScope.LevelJsonGuid : null;

        private List<LevelData> CopyLevels()
        {
            return LevelSaveController.CopyLevels(Levels);
        }

        private bool SaveLevels(IReadOnlyList<LevelData> levels)
        {
            return WriteSource(levels).Reloaded;
        }

        private LevelFileWriteResult WriteSource(IReadOnlyList<LevelData> levels)
        {
            LevelFileWriteResult result = m_saveController.WriteLevels(ActiveGuid, levels);
            if (!result.Reloaded)
            {
                ShowError(result.Error);
            }

            return result;
        }

        // The existing bool seam may report a written file whose reload failed by invalidating the source.
        private LevelFileWriteResult WriteForSave(IReadOnlyList<LevelData> levels)
        {
            if (WriteLevels == null)
            {
                return WriteSource(levels);
            }

            string before = m_status != null ? m_status.text : null;
            bool sourceWasValid = m_saveController.SourceValid;
            bool reloaded = WriteLevels(levels);
            string error = m_status != null && m_status.text != before ? m_status.text : null;
            return new LevelFileWriteResult(reloaded || (sourceWasValid && !m_saveController.SourceValid), reloaded, error);
        }

        private void ShowError(string message)
        {
            SetStatus(message);
            EditorUtility.DisplayDialog("Level Database", message, "OK");
        }

        private void SetStatus(string message)
        {
            if (m_status != null)
            {
                m_status.text = message;
            }

            // A new result makes a one-off hint stale.
            if (m_hint != null)
            {
                ShowHint(null);
            }
        }

        private string GetActiveDatabaseName()
        {
            return LevelDatabaseAddressables.GetDatabaseName(LevelDatabaseAddressables.GetActivePath(m_projectScope));
        }

    }
}
