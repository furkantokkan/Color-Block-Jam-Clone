using System;
using System.Collections.Generic;
using System.Globalization;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The validation panel pinned under the Level Editor's tool groups, which shows the window's validation state and
    /// forwards its clicks: Validate Level's summary, the listed solutions five to a page with a pager, the Auto Play and
    /// Validate Before Save toggles, and the Save Level, Validate Level, Test Level and Open Level Editor Scene buttons in
    /// one wrapping row. A row click raises <see cref="SolutionClicked"/>; the active row's Pause or Resume, Step and
    /// Stop buttons and the two toggles raise their own events. In Play Mode the window disables the rows. It holds no draft state.
    /// </summary>
    internal sealed class LevelValidationPanel : VisualElement
    {
        internal const string k_name = "validation-panel";
        internal const string k_summaryName = "validation-summary";
        internal const string k_solutionsName = "validation-solutions";
        internal const string k_pagerName = "validation-pager";
        internal const string k_optionsName = "validation-options";
        internal const string k_buttonsName = "validation-buttons";
        internal const string k_previousPageName = "validation-page-previous";
        internal const string k_nextPageName = "validation-page-next";
        internal const string k_pageTextName = "validation-page";
        internal const string k_rowNamePrefix = "validation-solution-";
        internal const string k_numberName = "validation-solution-number";
        internal const string k_movesName = "validation-solution-moves";
        internal const string k_orderName = "validation-solution-order";
        internal const string k_glyphName = "validation-solution-glyph";
        internal const string k_controlsName = "validation-solution-controls";
        internal const string k_pauseName = "validation-solution-pause";
        internal const string k_stepName = "validation-solution-step";
        internal const string k_stopName = "validation-solution-stop";
        internal const string k_autoPlayName = "validation-auto-play";
        internal const string k_validateBeforeSaveName = "validation-before-save";
        internal const string k_chipClass = "validation-solution-chip";
        internal const string k_arrowClass = "validation-solution-arrow";
        internal const string k_ellipsisClass = "validation-solution-ellipsis";
        /// <summary>The summary before the first Validate Level and after a change clears the list.</summary>
        internal const string k_listHint = "Validate Level lists the solutions.";
        /// <summary>A row tooltip's second sentence while its solution can play.</summary>
        internal const string k_playTooltip = "Click to show this solution on the canvas: it plays at once with Auto " +
                                              "Play on and waits on its start board with Auto Play off. Its row then " +
                                              "shows Pause, Step and Stop.";
        /// <summary>A row tooltip's second sentence in Play Mode, where the rows, like Validate Level, wait.</summary>
        internal const string k_playModeTooltip = "Solutions do not play in Play Mode. Stop Play Mode to play this " +
                                                  "solution.";
        internal const string k_autoPlayText = "Auto Play";
        internal const string k_validateBeforeSaveText = "Validate Before Save";
        internal const string k_autoPlayTooltip = "On: a click on a solution plays it at once. Off: the click shows its " +
                                                  "start board paused; Step shows one more frame and Resume plays on.";
        internal const string k_validateBeforeSaveTooltip = "On: Save Level, Test Level and closing the window with " +
                                                            "unsaved changes first check whether the level can be " +
                                                            "solved, and ask before they save a level that cannot be " +
                                                            "solved or confirmed. Off: they save without the check.";
        // The control tooltips name the ← and → keys (contract Revision 2), which work while a solution shows and the
        // Level Editor has the keyboard focus outside a text field.
        internal const string k_pauseTooltip = "Pause the solution on this frame. ← and → step through the frames.";
        internal const string k_resumeTooltip = "Resume the solution from the next frame. ← and → step through the " +
                                                "frames.";
        // Only the Step control ends the solution on its last frame; the → key stops there (contract Revision 2).
        internal const string k_stepTooltip = "Show the next frame; on the last frame this button shows the level " +
                                              "again. → and ← show the next and the previous frame and stay on the " +
                                              "solution.";
        internal const string k_stopTooltip = "Stop the solution and show the level again. ← and → step through the " +
                                              "frames while it shows.";
        // The summary is two lines high whatever it reads, so the panel keeps its height; a longer sentence is cut
        // after two lines, and the summary's tooltip and the status line hold all of it.
        internal const float k_summaryFontSize = 12f;
        internal const float k_summaryLineHeight = 15f;
        internal const int k_summaryLines = 2;
        internal const string k_playGlyph = "▶";
        internal const string k_stopGlyph = "■";
        // Step uses a glyph the Editor's Inter font has: it has no U+23ED (next), which would show as an empty box.
        internal const string k_stepGlyph = "▶|";
        // Pause is drawn, not typed (contract Revision 2): the font has no U+23F8, and its ‖ reads as one thin bar. Two
        // bars of the glyphs' height and colour sit in the control while its solution plays.
        internal const string k_pauseIconName = "validation-solution-pause-icon";
        internal const string k_pauseBarClass = "validation-solution-pause-bar";
        internal const float k_pauseBarWidth = 3f;
        internal const float k_pauseBarHeight = 9f;
        private const float k_pauseBarGap = 2f;
        internal const string k_arrowText = "→";
        internal const string k_ellipsisText = "…";
        // Fixed chip, arrow and ellipsis widths make the chips that fit a row plain arithmetic (GetVisibleChipCount).
        // At the default 280 px tools width a five-block order fits whole; at 240 px it shows three chips and "…". The
        // active row's three controls take 45 px more than the ▶ glyph they replace, so its order refits to fewer
        // chips (the order element takes the row's remaining width, and its GeometryChangedEvent refits).
        internal const float k_chipWidth = 22f;
        internal const float k_arrowWidth = 12f;
        internal const float k_ellipsisWidth = 14f;
        internal const float k_controlSize = 18f;
        private const float k_controlGap = 1f;
        private const float k_rowHeight = 22f;
        private const float k_rowGap = 2f;
        private const float k_chipHeight = 16f;
        private const float k_numberWidth = 22f;
        private const float k_movesWidth = 50f;
        private const float k_glyphWidth = 12f;
        private const float k_pageButtonWidth = 24f;
        private const float k_rowFontSize = 11f;
        private const float k_chipFontSize = 10f;
        private static readonly Color s_unknownColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color s_dividerColor = new Color(0f, 0f, 0f, 0.35f);

        private readonly LevelSolutionPages m_pages = new LevelSolutionPages();
        private readonly Label m_summary;
        private readonly Row[] m_rows = new Row[LevelSolutionPages.k_pageSize];
        private readonly VisualElement m_pager;
        private readonly Button m_previousPage;
        private readonly Button m_nextPage;
        private readonly Label m_pageText;
        private readonly Toggle m_autoPlay;
        private readonly Toggle m_validateBeforeSave;
        private Func<PaletteColor, Color> m_getColor;
        private LevelData m_level;
        private IReadOnlyList<LevelSolution> m_solutions = Array.Empty<LevelSolution>();
        private int m_activeEntry = -1;
        private bool m_paused;
        private bool m_rowsEnabled = true;

        /// <summary>The window's buttons go into the last row in this order.</summary>
        public LevelValidationPanel(Button saveLevel, Button validateLevel, Button testLevel, Button openScene)
        {
            name = k_name;
            style.flexShrink = 0;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;
            style.paddingBottom = 2;
            style.borderTopWidth = 1;
            style.borderTopColor = s_dividerColor;

            m_summary = new Label(k_listHint) { name = k_summaryName };
            m_summary.style.whiteSpace = WhiteSpace.Normal;
            m_summary.style.fontSize = k_summaryFontSize;
            m_summary.style.height = k_summaryLines * k_summaryLineHeight;
            m_summary.style.flexShrink = 0;
            m_summary.style.overflow = Overflow.Hidden;
            m_summary.style.marginBottom = 4;
            m_summary.style.paddingLeft = 0;
            m_summary.style.paddingRight = 0;
            m_summary.style.paddingTop = 0;
            m_summary.style.paddingBottom = 0;
            Add(m_summary);

            // Five slots of one height: a slot without an entry hides its content but keeps its height, so the panel
            // does not jump when a list arrives or a page is shorter.
            // The window's divider can resize the panel (user request of 2026-09-28): the list takes the height left and
            // scrolls when the panel is shorter than its five rows.
            ScrollView solutions = new ScrollView(ScrollViewMode.Vertical) { name = k_solutionsName };
            solutions.style.flexGrow = 1;
            solutions.style.flexShrink = 1;
            solutions.style.minHeight = 0;
            for (int slot = 0; slot < m_rows.Length; slot++)
            {
                m_rows[slot] = CreateRow(slot);
                solutions.Add(m_rows[slot].Button);
            }

            Add(solutions);

            // The pager keeps its height while hidden, for the same reason.
            m_pager = new VisualElement { name = k_pagerName };
            m_pager.style.flexDirection = FlexDirection.Row;
            m_pager.style.alignItems = Align.Center;
            m_pager.style.flexShrink = 0;
            m_pager.style.marginTop = 2;
            // Owner: these buttons; stop point: they are dropped with the window GUI.
            m_previousPage = new Button(() => PreviousPage())
            {
                name = k_previousPageName,
                text = "◀",
                tooltip = "Previous page of solutions."
            };
            m_previousPage.style.width = k_pageButtonWidth;
            m_previousPage.style.flexShrink = 0;
            m_pageText = new Label { name = k_pageTextName };
            m_pageText.style.flexGrow = 1;
            m_pageText.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_nextPage = new Button(() => NextPage())
            {
                name = k_nextPageName,
                text = "▶",
                tooltip = "Next page of solutions."
            };
            m_nextPage.style.width = k_pageButtonWidth;
            m_nextPage.style.flexShrink = 0;
            m_pager.Add(m_previousPage);
            m_pager.Add(m_pageText);
            m_pager.Add(m_nextPage);
            Add(m_pager);

            // Auto Play and Validate Before Save share one row above the buttons; a pane too narrow for both wraps it.
            // The row does not change with an answer, so the panel keeps its height.
            VisualElement options = new VisualElement { name = k_optionsName };
            options.style.flexDirection = FlexDirection.Row;
            options.style.flexWrap = Wrap.Wrap;
            options.style.flexShrink = 0;
            options.style.marginTop = 4;
            m_autoPlay = CreateOption(k_autoPlayName, k_autoPlayText, k_autoPlayTooltip);
            m_validateBeforeSave = CreateOption(k_validateBeforeSaveName, k_validateBeforeSaveText,
                k_validateBeforeSaveTooltip);
            // Raised only by the designer's change; the window's own values arrive without notification (SetAutoPlay,
            // SetValidateBeforeSave). Owner: these toggles; stop point: they are dropped with the window GUI.
            m_autoPlay.RegisterValueChangedCallback(change => AutoPlayChanged?.Invoke(change.newValue));
            m_validateBeforeSave.RegisterValueChangedCallback(change =>
                ValidateBeforeSaveChanged?.Invoke(change.newValue));
            options.Add(m_autoPlay);
            options.Add(m_validateBeforeSave);
            Add(options);

            // The buttons keep their size; a pane too narrow for all of them wraps them instead of clipping a label.
            VisualElement buttons = new VisualElement { name = k_buttonsName };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.flexShrink = 0;
            buttons.style.marginTop = 4;
            buttons.Add(saveLevel);
            buttons.Add(validateLevel);
            buttons.Add(testLevel);
            buttons.Add(openScene);
            Add(buttons);

            ShowHint();
        }

        /// <summary>Raised with a listed entry's index when its row is clicked. Subscriber: the Level Editor window.</summary>
        internal event Action<int> SolutionClicked;

        /// <summary>Raised by the active row's Pause or Resume button. Subscriber: the Level Editor window.</summary>
        internal event Action PauseResumeClicked;

        /// <summary>Raised by the active row's Step button. Subscriber: the Level Editor window.</summary>
        internal event Action StepClicked;

        /// <summary>Raised by the active row's Stop button. Subscriber: the Level Editor window.</summary>
        internal event Action StopClicked;

        /// <summary>Raised when the designer switches Auto Play. Subscriber: the Level Editor window.</summary>
        internal event Action<bool> AutoPlayChanged;

        /// <summary>Raised when the designer switches Validate Before Save. Subscriber: the Level Editor window.</summary>
        internal event Action<bool> ValidateBeforeSaveChanged;

        internal string Summary => m_summary.text;
        internal LevelSolutionPages Pages => m_pages;
        internal VisualElement Pager => m_pager;
        internal Button PreviousPageButton => m_previousPage;
        internal Button NextPageButton => m_nextPage;
        internal Label PageText => m_pageText;
        internal Toggle AutoPlayToggle => m_autoPlay;
        internal Toggle ValidateBeforeSaveToggle => m_validateBeforeSave;
        /// <summary>The entry whose row shows the playback controls, playing or paused, or -1.</summary>
        internal int ActiveEntry => m_activeEntry;
        /// <summary>True while the active entry is shown paused, so its first control reads Resume.</summary>
        internal bool IsPaused => m_paused;

        /// <summary>The row button of a slot on the current page.</summary>
        internal Button GetRow(int slot)
        {
            return m_rows[slot].Button;
        }

        /// <summary>
        /// How many exit-order chips fit a row width: all of them joined by arrows when they fit, else as many as fit
        /// before a final arrow and "…". A width not laid out yet shows them all.
        /// </summary>
        internal static int GetVisibleChipCount(int count, float width)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (float.IsNaN(width) || count * k_chipWidth + (count - 1) * k_arrowWidth <= width)
            {
                return count;
            }

            int fitting = Mathf.FloorToInt((width - k_ellipsisWidth) / (k_chipWidth + k_arrowWidth));
            return Mathf.Clamp(fitting, 0, count - 1);
        }

        /// <summary>A block's canvas label: its id in upper case.</summary>
        internal static string GetBlockLabel(LevelData level, int blockIndex)
        {
            string id = level.GetBlockAt(blockIndex)?.Id;
            return id != null ? id.ToUpperInvariant() : string.Empty;
        }

        /// <summary>The whole exit order by canvas label, for example "B2 → B1 → B3".</summary>
        internal static string FormatOrder(LevelData level, IReadOnlyList<int> order)
        {
            string[] labels = new string[order.Count];
            for (int index = 0; index < labels.Length; index++)
            {
                labels[index] = GetBlockLabel(level, order[index]);
            }

            return string.Join(" " + k_arrowText + " ", labels);
        }

        /// <summary>"1 move" or "5 moves" (DEC-122 player moves).</summary>
        internal static string FormatMoves(int moves)
        {
            return moves.ToString(CultureInfo.InvariantCulture) + (moves == 1 ? " move" : " moves");
        }

        /// <summary>Paints the chips with the colours the canvas draws.</summary>
        internal void SetColors(Func<PaletteColor, Color> getColor)
        {
            m_getColor = getColor;
            RefreshRows();
        }

        /// <summary>Shows the window's Auto Play value without raising <see cref="AutoPlayChanged"/>.</summary>
        internal void SetAutoPlay(bool on)
        {
            m_autoPlay.SetValueWithoutNotify(on);
        }

        /// <summary>Shows the window's Validate Before Save value without raising <see cref="ValidateBeforeSaveChanged"/>.</summary>
        internal void SetValidateBeforeSave(bool on)
        {
            m_validateBeforeSave.SetValueWithoutNotify(on);
        }

        /// <summary>The hint and an empty list: before the first Validate Level and after a change clears the list.</summary>
        internal void ShowHint()
        {
            ShowAnswer(k_listHint);
        }

        /// <summary>An answer without solutions (not Solvable, not checked): its sentence and an empty list.</summary>
        internal void ShowAnswer(string summary)
        {
            ShowSolutions(summary, null, Array.Empty<LevelSolution>());
        }

        /// <summary>
        /// A background Validate Level run's progress line over an empty list (DEC-175). It is refreshed about ten times
        /// a second, so after the first call it only changes the summary text.
        /// </summary>
        internal void ShowProgress(string text)
        {
            if (m_level != null || m_solutions.Count > 0)
            {
                ShowAnswer(text);
                return;
            }

            m_summary.text = text ?? string.Empty;
            m_summary.tooltip = m_summary.text;
        }

        /// <summary>Validate Level's listed solutions of this level on page 1, under the summary line.</summary>
        internal void ShowSolutions(string summary, LevelData level, IReadOnlyList<LevelSolution> solutions)
        {
            m_summary.text = summary ?? string.Empty;
            // The summary shows two lines at most; its tooltip holds the whole sentence.
            m_summary.tooltip = m_summary.text;
            m_level = level;
            m_solutions = level != null && solutions != null ? solutions : Array.Empty<LevelSolution>();
            m_activeEntry = -1;
            m_paused = false;
            m_pages.Reset(m_solutions.Count);
            RefreshRows();
        }

        /// <summary>
        /// Shows which entry plays or waits: its row gets Pause (Resume while paused), Step and Stop in place of its ▶,
        /// and every other row keeps ▶. An entry of -1 shows ▶ on every row.
        /// </summary>
        internal void SetPlayback(int entry, bool paused)
        {
            m_activeEntry = entry;
            m_paused = entry >= 0 && paused;
            for (int slot = 0; slot < m_rows.Length; slot++)
            {
                RefreshPlayback(m_rows[slot]);
            }
        }

        /// <summary>
        /// Enables the rows, or disables them while the Editor plays or is about to, as Validate Level is then; their
        /// tooltips say why. The window refuses a row click in Play Mode too.
        /// </summary>
        internal void SetRowsEnabled(bool enabled)
        {
            if (enabled == m_rowsEnabled)
            {
                return;
            }

            m_rowsEnabled = enabled;
            for (int slot = 0; slot < m_rows.Length; slot++)
            {
                RefreshRowAction(m_rows[slot]);
            }
        }

        /// <summary>The next page; false at the last one.</summary>
        internal bool NextPage()
        {
            if (!m_pages.Next())
            {
                return false;
            }

            RefreshRows();
            return true;
        }

        /// <summary>The previous page; false at the first one.</summary>
        internal bool PreviousPage()
        {
            if (!m_pages.Previous())
            {
                return false;
            }

            RefreshRows();
            return true;
        }

        private Row CreateRow(int slot)
        {
            Row row = new Row { Entry = -1 };
            // A button, so a row shows the skin's hover and press feedback. Owner: this row; stop point: it is dropped
            // with the window GUI.
            row.Button = new Button(() => OnRowClicked(row)) { name = k_rowNamePrefix + slot };
            Button button = row.Button;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.FlexStart;
            button.style.flexShrink = 0;
            button.style.height = k_rowHeight;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = k_rowGap;
            button.style.paddingLeft = 4;
            button.style.paddingRight = 4;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;

            row.Number = CreateRowLabel(k_numberName, k_numberWidth, TextAnchor.MiddleLeft);
            row.Number.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Moves = CreateRowLabel(k_movesName, k_movesWidth, TextAnchor.MiddleLeft);
            row.Order = new VisualElement { name = k_orderName, pickingMode = PickingMode.Ignore };
            row.Order.style.flexDirection = FlexDirection.Row;
            row.Order.style.alignItems = Align.Center;
            // A zero basis sizes the order from the row's free width, never from its chips, so refitting the chips
            // never changes the width that decided them.
            row.Order.style.flexGrow = 1;
            row.Order.style.flexShrink = 1;
            row.Order.style.flexBasis = 0;
            row.Order.style.overflow = Overflow.Hidden;
            row.Order.style.marginLeft = 3;
            row.Order.style.marginRight = 3;
            // Refits the chips when the tools pane width changes or the playback controls take or give back width.
            // Owner: this row; stop point: it is dropped with the window GUI.
            row.Order.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (!Mathf.Approximately(evt.oldRect.width, evt.newRect.width))
                {
                    FitChips(row);
                }
            });
            row.Glyph = CreateRowLabel(k_glyphName, k_glyphWidth, TextAnchor.MiddleCenter);
            row.Glyph.style.fontSize = k_chipFontSize;
            CreateControls(row);
            button.Add(row.Number);
            button.Add(row.Moves);
            button.Add(row.Order);
            button.Add(row.Glyph);
            button.Add(row.Controls);
            return row;
        }

        // The active row's Pause or Resume, Step and Stop buttons, hidden until its solution shows. They sit inside the
        // row button. Each control's own Clickable and Button already stop its pointer presses and its
        // NavigationSubmitEvent; the container is a backup that keeps any remaining press, click or submit from reaching
        // the row, so a control click never counts as a row click.
        private void CreateControls(Row row)
        {
            VisualElement controls = new VisualElement { name = k_controlsName, pickingMode = PickingMode.Ignore };
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            controls.style.flexShrink = 0;
            controls.style.display = DisplayStyle.None;
            // Owner: this row's controls; stop point: they are dropped with the window GUI.
            controls.RegisterCallback<PointerDownEvent>(StopAtControls);
            controls.RegisterCallback<PointerUpEvent>(StopAtControls);
            controls.RegisterCallback<MouseDownEvent>(StopAtControls);
            controls.RegisterCallback<MouseUpEvent>(StopAtControls);
            controls.RegisterCallback<ClickEvent>(StopAtControls);
            controls.RegisterCallback<NavigationSubmitEvent>(StopAtControls);
            // Owner: these buttons; stop point: they are dropped with the window GUI.
            row.PauseButton = CreateControl(k_pauseName, string.Empty, k_pauseTooltip, () => PauseResumeClicked?.Invoke());
            row.PauseIcon = CreatePauseIcon(row.PauseButton);
            row.StepButton = CreateControl(k_stepName, k_stepGlyph, k_stepTooltip, () => StepClicked?.Invoke());
            row.StopButton = CreateControl(k_stopName, k_stopGlyph, k_stopTooltip, () => StopClicked?.Invoke());
            controls.Add(row.PauseButton);
            controls.Add(row.StepButton);
            controls.Add(row.StopButton);
            row.Controls = controls;
        }

        private static void StopAtControls(EventBase evt)
        {
            evt.StopPropagation();
        }

        // The Pause control's icon: two bars of the other glyphs' height, centred in the control and coloured with its
        // text colour once it is laid out (white until then, as in the dark skin).
        private static VisualElement CreatePauseIcon(Button control)
        {
            control.style.flexDirection = FlexDirection.Row;
            control.style.alignItems = Align.Center;
            control.style.justifyContent = Justify.Center;
            VisualElement icon = new VisualElement { name = k_pauseIconName, pickingMode = PickingMode.Ignore };
            icon.style.flexDirection = FlexDirection.Row;
            icon.style.alignItems = Align.Center;
            icon.style.flexShrink = 0;
            for (int index = 0; index < 2; index++)
            {
                VisualElement bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList(k_pauseBarClass);
                bar.style.width = k_pauseBarWidth;
                bar.style.height = k_pauseBarHeight;
                bar.style.flexShrink = 0;
                bar.style.marginLeft = index == 0 ? 0f : k_pauseBarGap;
                bar.style.backgroundColor = Color.white;
                icon.Add(bar);
            }

            // Owner: this icon; stop point: it is dropped with the window GUI.
            icon.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                Color color = control.resolvedStyle.color;
                for (int index = 0; index < icon.childCount; index++)
                {
                    icon[index].style.backgroundColor = color;
                }
            });
            control.Add(icon);
            return icon;
        }

        private static Button CreateControl(string controlName, string glyph, string tooltip, Action action)
        {
            Button control = new Button(action) { name = controlName, text = glyph, tooltip = tooltip };
            control.style.width = k_controlSize;
            control.style.height = k_controlSize;
            control.style.minWidth = 0;
            control.style.minHeight = 0;
            control.style.flexShrink = 0;
            control.style.marginLeft = k_controlGap;
            control.style.marginRight = 0;
            control.style.marginTop = 0;
            control.style.marginBottom = 0;
            control.style.paddingLeft = 0;
            control.style.paddingRight = 0;
            control.style.paddingTop = 0;
            control.style.paddingBottom = 0;
            control.style.fontSize = k_chipFontSize;
            control.style.unityFontStyleAndWeight = FontStyle.Bold;
            control.style.unityTextAlign = TextAnchor.MiddleCenter;
            control.style.whiteSpace = WhiteSpace.NoWrap;
            return control;
        }

        // One of the panel's two toggles: a check box with its name after it, which keeps a narrow row compact.
        private static Toggle CreateOption(string toggleName, string text, string tooltip)
        {
            Toggle toggle = new Toggle { name = toggleName, text = text, tooltip = tooltip };
            toggle.style.flexShrink = 0;
            toggle.style.marginLeft = 0;
            toggle.style.marginRight = 12;
            toggle.style.marginTop = 0;
            toggle.style.marginBottom = 0;
            return toggle;
        }

        private static Label CreateRowLabel(string labelName, float width, TextAnchor alignment)
        {
            Label label = new Label { name = labelName, pickingMode = PickingMode.Ignore };
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.fontSize = k_rowFontSize;
            label.style.unityTextAlign = alignment;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            return label;
        }

        private void OnRowClicked(Row row)
        {
            if (row.Entry >= 0)
            {
                SolutionClicked?.Invoke(row.Entry);
            }
        }

        // Shows the current page: each slot's entry, or an empty slot that keeps its height, and the pager.
        private void RefreshRows()
        {
            for (int slot = 0; slot < m_rows.Length; slot++)
            {
                FillRow(m_rows[slot], m_pages.GetEntryIndex(slot));
            }

            m_pager.visible = m_pages.ShowsPager;
            m_pageText.text = m_pages.PageText;
            m_previousPage.SetEnabled(m_pages.CanGoBack);
            m_nextPage.SetEnabled(m_pages.CanGoNext);
        }

        private void FillRow(Row row, int entry)
        {
            row.Entry = entry;
            row.Order.Clear();
            if (entry < 0 || m_level == null)
            {
                row.Entry = -1;
                row.OrderLength = 0;
                row.Number.text = string.Empty;
                row.Moves.text = string.Empty;
                RefreshPlayback(row);
                RefreshRowAction(row);
                row.Button.visible = false;
                return;
            }

            LevelSolution solution = m_solutions[entry];
            IReadOnlyList<int> order = solution.ExitOrder;
            row.OrderLength = order.Count;
            row.Number.text = "#" + (entry + 1).ToString(CultureInfo.InvariantCulture);
            row.Moves.text = FormatMoves(solution.MoveCount);
            RefreshRowAction(row);
            // Children in pairs: chip i, then the arrow after it; the last arrow only leads to the final "…".
            for (int index = 0; index < order.Count; index++)
            {
                row.Order.Add(CreateChip(order[index]));
                row.Order.Add(CreateOrderText(k_arrowClass, k_arrowText, k_arrowWidth));
            }

            row.Order.Add(CreateOrderText(k_ellipsisClass, k_ellipsisText, k_ellipsisWidth));
            row.Button.visible = true;
            RefreshPlayback(row);
            FitChips(row);
        }

        // Shows the chips that fit the order's laid-out width, joined by arrows, and "…" after them when some are cut.
        private static void FitChips(Row row)
        {
            int count = row.OrderLength;
            if (count == 0 || row.Order.childCount != count * 2 + 1)
            {
                return;
            }

            int shown = GetVisibleChipCount(count, row.Order.layout.width);
            bool cut = shown < count;
            for (int index = 0; index < count; index++)
            {
                SetShown(row.Order[index * 2], index < shown);
                SetShown(row.Order[index * 2 + 1], index < shown && (cut || index < count - 1));
            }

            SetShown(row.Order[count * 2], cut);
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // An entry's row ends with ▶; the active entry's row shows Pause (Resume while paused), Step and Stop in its
        // place. A slot without an entry shows neither. The first control draws its two Pause bars while the solution
        // plays and reads ▶ while it is paused.
        private void RefreshPlayback(Row row)
        {
            bool active = row.Entry >= 0 && row.Entry == m_activeEntry;
            row.Glyph.text = row.Entry < 0 ? string.Empty : k_playGlyph;
            SetShown(row.Glyph, !active);
            SetShown(row.Controls, active);
            row.PauseButton.text = m_paused ? k_playGlyph : string.Empty;
            SetShown(row.PauseIcon, !m_paused);
            row.PauseButton.tooltip = m_paused ? k_resumeTooltip : k_pauseTooltip;
        }

        // Whether the row can be clicked, and its tooltip: the whole exit order, then how to play it or why it cannot
        // play now. A slot without an entry has no tooltip.
        private void RefreshRowAction(Row row)
        {
            row.Button.SetEnabled(m_rowsEnabled);
            row.Button.tooltip = row.Entry < 0
                ? string.Empty
                : "Exit order: " + FormatOrder(m_level, m_solutions[row.Entry].ExitOrder) + ". " +
                  (m_rowsEnabled ? k_playTooltip : k_playModeTooltip);
        }

        // One exit-order chip: the block's canvas label on the colour the canvas draws for it.
        private Label CreateChip(int blockIndex)
        {
            LevelBlockData block = m_level.GetBlockAt(blockIndex);
            Label chip = CreateOrderText(k_chipClass, GetBlockLabel(m_level, blockIndex), k_chipWidth);
            chip.style.height = k_chipHeight;
            chip.style.fontSize = k_chipFontSize;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.color = Color.black;
            chip.style.textOverflow = TextOverflow.Ellipsis;
            chip.style.borderTopLeftRadius = 3;
            chip.style.borderTopRightRadius = 3;
            chip.style.borderBottomLeftRadius = 3;
            chip.style.borderBottomRightRadius = 3;
            chip.style.backgroundColor = m_getColor != null && block != null
                                         && BoardLayout.TryParseDoorColor(block.ColorId, out PaletteColor color)
                ? m_getColor(color)
                : s_unknownColor;
            return chip;
        }

        private static Label CreateOrderText(string className, string text, float width)
        {
            Label label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.fontSize = k_chipFontSize;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            return label;
        }

        /// <summary>
        /// One row slot's elements (its playback controls included), the entry it shows (-1 for none) and that entry's
        /// exit-order length.
        /// </summary>
        private sealed class Row
        {
            internal Button Button { get; set; }
            internal Label Number { get; set; }
            internal Label Moves { get; set; }
            internal VisualElement Order { get; set; }
            internal Label Glyph { get; set; }
            internal VisualElement Controls { get; set; }
            internal Button PauseButton { get; set; }
            internal VisualElement PauseIcon { get; set; }
            internal Button StepButton { get; set; }
            internal Button StopButton { get; set; }
            internal int Entry { get; set; }
            internal int OrderLength { get; set; }
        }
    }
}
