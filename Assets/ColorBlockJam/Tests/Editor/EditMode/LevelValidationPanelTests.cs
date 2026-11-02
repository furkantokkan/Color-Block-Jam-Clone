using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's validation panel (TASK-20260928-091128541-cb48c84a): its place under the tool groups
    /// with Save Level, Validate Level and Test Level, its two-line summary and steady height, Validate Level's listed
    /// solutions five to a page, the list's lifetime and the kept result's reuse, no listing for Save Level, Test Level
    /// or save-on-close, rows disabled in Play Mode, and the solution playback's frames, hidden outlines and stop points
    /// on a stepped clock. Contract Revision 1 adds the active row's Pause, Step and Stop controls, the Auto Play and
    /// Validate Before Save toggles with their designer-window preferences, and the LevelEditor scene opening after a
    /// canvas press; Revision 2 the safety stop, the drawn Pause icon, the ← and → keys and the key-naming tooltips.
    /// Every write, dialog, progress bar, Play Mode start or end, scene step and update subscription goes
    /// to a fake; no test changes the user's level files, and the two preference keys are put back after each test.
    /// </summary>
    public sealed class LevelValidationPanelTests
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string k_guid = "validation-panel-fixture-json";

        private LevelDatabaseWindow m_window;
        private ProjectScopeInstaller m_projectScope;
        private LevelData m_publishedBefore;
        private readonly List<string> m_calls = new List<string>();
        private readonly List<string> m_titles = new List<string>();
        private readonly List<string> m_progress = new List<string>();
        private readonly List<LevelData> m_frames = new List<LevelData>();
        private readonly List<EditorApplication.CallbackFunction> m_updates = new List<EditorApplication.CallbackFunction>();
        // The background Validate Level poll's fake update list (DEC-175); the tests run it by hand.
        private readonly List<EditorApplication.CallbackFunction> m_validationUpdates =
            new List<EditorApplication.CallbackFunction>();
        private bool m_cancelCheck;
        private bool m_cancelListing;
        private bool m_writeFails;
        private int m_clearedProgress;
        // The playback's fake clock and the end of the frame it shows, summed the way the playback sums them.
        private double m_now;
        private double m_frameEnd;
        // The fake LevelEditor scene: whether it counts as loaded, and how often the window's scene step ran.
        private bool m_sceneLoaded;
        private int m_sceneOpens;
        // The user's two preference values, put back after each test.
        private bool m_hadAutoPlayPref;
        private bool m_autoPlayPref;
        private bool m_hadValidatePref;
        private bool m_validatePref;

        [SetUp]
        public void ClearRecords()
        {
            Assume.That(LevelTestPlay.IsActive, Is.False, "A test play of this Editor session is still running.");
            m_hadAutoPlayPref = EditorPrefs.HasKey(LevelDatabaseWindow.k_autoPlayPrefKey);
            m_autoPlayPref = EditorPrefs.GetBool(LevelDatabaseWindow.k_autoPlayPrefKey, true);
            m_hadValidatePref = EditorPrefs.HasKey(LevelDatabaseWindow.k_validateBeforeSavePrefKey);
            m_validatePref = EditorPrefs.GetBool(LevelDatabaseWindow.k_validateBeforeSavePrefKey, true);
            m_sceneLoaded = false;
            m_sceneOpens = 0;
            m_publishedBefore = LevelDatabaseWindow.LastPublishedLevel;
            // NUnit shares one fixture instance between the tests.
            m_calls.Clear();
            m_titles.Clear();
            m_progress.Clear();
            m_frames.Clear();
            m_updates.Clear();
            m_validationUpdates.Clear();
            m_cancelCheck = false;
            m_cancelListing = false;
            m_writeFails = false;
            m_clearedProgress = 0;
            m_now = 100.0;
            m_frameEnd = m_now;
            // Owner: this fixture. Stop point: CloseWindow.
            LevelDatabaseWindow.SolutionFramePublished += RecordFrame;
        }

        [TearDown]
        public void CloseWindow()
        {
            LevelDatabaseWindow.SolutionFramePublished -= RecordFrame;
            if (m_window != null)
            {
                m_window.DiscardChanges();
                m_window.Close();
                m_window = null;
                RestorePublishedLevel(m_publishedBefore);
            }

            LevelEditorTestAccess.DestroyProjectScope(m_projectScope);
            m_projectScope = null;

            RestorePref(LevelDatabaseWindow.k_autoPlayPrefKey, m_hadAutoPlayPref, m_autoPlayPref);
            RestorePref(LevelDatabaseWindow.k_validateBeforeSavePrefKey, m_hadValidatePref, m_validatePref);
        }

        [Test]
        public void ThePanelIsPinnedUnderTheToolGroupsAndHoldsTheThreeButtons()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);

            VisualElement column = root.Q("tools-column");
            ScrollView tools = root.Q<ScrollView>("tools-pane");
            VisualElement panel = root.Q(LevelValidationPanel.k_name);
            Assert.That(column.IndexOf(tools), Is.Zero);
            Assert.That(column.IndexOf(panel), Is.EqualTo(1));
            Assert.That(panel.GetFirstAncestorOfType<ScrollView>(), Is.Null, "The panel never scrolls with the groups.");
            Assert.That(panel.Q<ScrollView>(), Is.Null);
            Assert.That(ChildNames(panel), Is.EqualTo(new[]
            {
                LevelValidationPanel.k_summaryName, LevelValidationPanel.k_solutionsName, LevelValidationPanel.k_pagerName,
                LevelValidationPanel.k_optionsName, LevelValidationPanel.k_buttonsName
            }), "The Auto Play and Validate Before Save row sits right above the buttons (contract Revision 1).");
            Assert.That(ChildNames(panel.Q(LevelValidationPanel.k_optionsName)), Is.EqualTo(new[]
            {
                LevelValidationPanel.k_autoPlayName, LevelValidationPanel.k_validateBeforeSaveName
            }));
            Assert.That(m_window.ValidationPanel.AutoPlayToggle.text, Is.EqualTo("Auto Play"));
            Assert.That(m_window.ValidationPanel.AutoPlayToggle.value, Is.True, "A window a test creates plays at once.");
            Assert.That(m_window.ValidationPanel.ValidateBeforeSaveToggle.text, Is.EqualTo("Validate Before Save"));
            Assert.That(m_window.ValidationPanel.ValidateBeforeSaveToggle.value, Is.True,
                "It shows the window's flag, which this fixture turns on.");
            Assert.That(ChildNames(panel.Q(LevelValidationPanel.k_buttonsName)),
                Is.EqualTo(new[] { "save-level", "validate-level", "test-level", "open-level-editor-scene" }));
            AssertButton(root, "save-level", "Save Level", "Validate the draft and write it to the JSON source.");
            AssertButton(root, "validate-level", "Validate Level", LevelDatabaseWindow.k_validateTooltip);
            AssertButton(root, "test-level", "Test Level",
                "Save the level and play it in the Game scene. Stop Play Mode to come back to this level and keep " +
                "editing it.");
            VisualElement bottomBar = root.Q("bottom-bar");
            Assert.That(ChildNames(bottomBar), Is.EqualTo(new[] { "status" }), "The bottom bar keeps only the status line.");
            Assert.That(panel.Q<Label>(LevelValidationPanel.k_summaryName).text, Is.EqualTo(LevelValidationPanel.k_listHint));

            // In view between the groups and the bottom bar, and still there when the groups scroll.
            Assert.That(panel.worldBound.yMin, Is.GreaterThanOrEqualTo(tools.worldBound.yMax - 0.5f));
            Assert.That(panel.worldBound.yMax, Is.LessThanOrEqualTo(bottomBar.worldBound.yMin + 0.5f));
            Rect before = panel.worldBound;
            tools.scrollOffset = new Vector2(0f, 200f);
            ValidateLayout(root);
            Assert.That(panel.worldBound, Is.EqualTo(before));
        }

        [TestCase(240f)]
        [TestCase(280f)]
        [TestCase(400f)]
        public void FiveRowSlotsAndATwoLineSummaryKeepThePanelHeightForEveryAnswer(float toolsWidth)
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            // The fixed pane takes the width a splitter drag would give it, whatever width the user stored.
            SetToolsWidth(root, toolsWidth);
            VisualElement panel = root.Q(LevelValidationPanel.k_name);
            Label summary = panel.Q<Label>(LevelValidationPanel.k_summaryName);
            VisualElement solutions = root.Q(LevelValidationPanel.k_solutionsName);
            float panelHeight = panel.layout.height;

            Assert.That(solutions.childCount, Is.EqualTo(5));
            float height = Row(0).layout.height;
            for (int slot = 0; slot < 5; slot++)
            {
                Assert.That(Row(slot).visible, Is.False, "No list yet.");
                Assert.That(Row(slot).layout.height, Is.EqualTo(height).Within(0.5f));
            }

            // The summary is two lines high: two lines of its text fit, three do not.
            Assert.That(MeasureLines(summary, 2), Is.LessThanOrEqualTo(summary.contentRect.height + 0.5f));
            Assert.That(MeasureLines(summary, 3), Is.GreaterThan(summary.contentRect.height + 0.5f));
            AssertSummary(panel, LevelValidationPanel.k_listHint, panelHeight, "The hint.");

            Validate(root);
            ValidateLayout(root);

            Assert.That(Row(0).visible, Is.True);
            Assert.That(Row(0).layout.height, Is.EqualTo(height).Within(0.5f));
            AssertSummary(panel, "Solvable in 3 moves · 6 solutions", panelHeight,
                "The panel does not jump when the list arrives.");

            // The active row's controls take the place of its ▶ (contract Revision 1), so neither the row nor the panel
            // grows while a solution plays or waits paused, and the toggles' row does not change with an answer.
            Assert.That(panel.Q(LevelValidationPanel.k_optionsName), Is.Not.Null);
            PlayRow(0);
            ValidateLayout(root);
            Assert.That(Row(0).layout.height, Is.EqualTo(height).Within(0.5f));
            AssertSummary(panel, "Solvable in 3 moves · 6 solutions", panelHeight, "The playback controls.");
            Click(Control(0, LevelValidationPanel.k_pauseName));
            ValidateLayout(root);
            AssertSummary(panel, "Solvable in 3 moves · 6 solutions", panelHeight, "A paused playback.");

            // A Cannot be solved sentence that needs more than two lines at 240 px, cut after two with the whole
            // sentence in the tooltip.
            UseDatabase(NoGate());
            Validate(root);
            ValidateLayout(root);
            string unsolvable = m_window.ValidationPanel.Summary;
            Assert.That(unsolvable, Does.StartWith("Cannot be solved: "));
            AssertSummary(panel, unsolvable, panelHeight, "A long Cannot be solved sentence.");
            if (toolsWidth <= 240f)
            {
                Assert.That(MeasureWrapped(summary, unsolvable), Is.GreaterThan(summary.contentRect.height + 0.5f),
                    "At 240 px the sentence needs more than two lines, so a growing summary would move the panel.");
            }

            // A long Not checked sentence, as the window shows an answer without a list.
            const string notChecked = "Not checked: this level uses Star blocks, which the check does not cover yet.";
            m_window.ValidationPanel.ShowAnswer(notChecked);
            ValidateLayout(root);
            AssertSummary(panel, notChecked, panelHeight, "A long Not checked sentence.");
        }

        [TestCase(240f)]
        [TestCase(280f)]
        [TestCase(400f)]
        public void TheButtonRowWrapsInsteadOfClippingALabel(float toolsWidth)
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            Validate(root);
            SetToolsWidth(root, toolsWidth);

            VisualElement row = root.Q(LevelValidationPanel.k_buttonsName);
            Button save = root.Q<Button>("save-level");
            Button validate = root.Q<Button>("validate-level");
            Button test = root.Q<Button>("test-level");
            foreach (Button button in new[] { save, validate, test })
            {
                Vector2 text = button.MeasureTextSize(button.text, 0f, VisualElement.MeasureMode.Undefined, 0f,
                    VisualElement.MeasureMode.Undefined);
                IResolvedStyle style = button.resolvedStyle;
                float needed = text.x + style.paddingLeft + style.paddingRight + style.borderLeftWidth +
                               style.borderRightWidth;
                Assert.That(button.layout.width, Is.GreaterThanOrEqualTo(needed - 0.5f), $"{button.name}'s label fits.");
                Assert.That(button.layout.xMax, Is.LessThanOrEqualTo(row.layout.width + 0.5f), $"{button.name} fits.");
                Assert.That(button.layout.height, Is.EqualTo(LevelDatabaseWindow.k_bottomButtonHeight).Within(0.5f));
            }

            if (toolsWidth <= 240f)
            {
                Assert.That(validate.layout.y, Is.GreaterThan(save.layout.y), "A 240 px pane takes one button per line.");
                Assert.That(test.layout.y, Is.GreaterThan(validate.layout.y));
            }
            else if (toolsWidth <= 280f)
            {
                Assert.That(validate.layout.y, Is.EqualTo(save.layout.y).Within(0.5f));
                Assert.That(test.layout.y, Is.GreaterThan(save.layout.y), "Test Level wraps to the next line.");
            }
            else
            {
                Assert.That(test.layout.y, Is.EqualTo(save.layout.y).Within(0.5f), "All three share one line.");
            }

            List<string> overflows = new List<string>();
            CollectOverflows(root.Q(LevelValidationPanel.k_name), 3, overflows);
            Assert.That(overflows, Is.Empty, "Panel rows wider than their parent.");
        }

        [Test]
        public void ValidateLevelListsTheSolutionsFiveToAPage()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);

            Validate(root);

            LevelValidationPanel panel = m_window.ValidationPanel;
            Assert.That(panel.Summary, Is.EqualTo("Solvable in 3 moves · 6 solutions"));
            Assert.That(Status(root), Is.EqualTo("Solvable in 3 moves."), "The status line keeps the result sentence.");
            string[][] orders =
            {
                new[] { "B1", "B2", "B3" }, new[] { "B1", "B3", "B2" }, new[] { "B2", "B1", "B3" },
                new[] { "B2", "B3", "B1" }, new[] { "B3", "B1", "B2" }, new[] { "B3", "B2", "B1" }
            };
            for (int slot = 0; slot < 5; slot++)
            {
                Button row = Row(slot);
                Assert.That(row.visible, Is.True);
                Assert.That(row.Q<Label>(LevelValidationPanel.k_numberName).text, Is.EqualTo("#" + (slot + 1)));
                Assert.That(row.Q<Label>(LevelValidationPanel.k_movesName).text, Is.EqualTo("3 moves"));
                Assert.That(ShownChips(row), Is.EqualTo(orders[slot]), "#" + (slot + 1));
                Assert.That(row.tooltip, Does.StartWith("Exit order: " + string.Join(" → ", orders[slot]) + "."));
                Assert.That(row.Q<Label>(LevelValidationPanel.k_glyphName).text, Is.EqualTo(LevelValidationPanel.k_playGlyph));
            }

            // Each chip is the block's canvas label on the colour the canvas draws for it.
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            List<Label> chips = Row(0).Query<Label>(className: LevelValidationPanel.k_chipClass).ToList();
            Assert.That(chips[0].style.backgroundColor.value, Is.EqualTo(canvas.GetPaletteColor(PaletteColor.Red)));
            Assert.That(chips[1].style.backgroundColor.value, Is.EqualTo(canvas.GetPaletteColor(PaletteColor.Orange)));
            Assert.That(chips[2].style.backgroundColor.value, Is.EqualTo(canvas.GetPaletteColor(PaletteColor.Yellow)));
            Assert.That(Ellipsis(Row(0)), Is.False, "Three chips fit a row.");

            // Entry #1 is the check's own solution, and no two entries share an exit order.
            LevelCheckResult listed = LevelEditorTestAccess.Get<LevelCheckResult>(m_window, "m_listedResult");
            Assert.That(listed.Solutions[0].Steps, Is.EqualTo(LevelSolvabilityCheck.Run(ThreeSingles()).Steps));
            HashSet<string> tooltips = new HashSet<string>();
            for (int entry = 0; entry < listed.Solutions.Count; entry++)
            {
                Assert.That(tooltips.Add(LevelValidationPanel.FormatOrder(ThreeSingles(), listed.Solutions[entry].ExitOrder)),
                    Is.True, "#" + (entry + 1));
            }

            Assert.That(panel.Pager.visible, Is.True, "Six entries need a second page.");
            Assert.That(panel.PageText.text, Is.EqualTo("1 / 2"));
            Assert.That(panel.PreviousPageButton.enabledSelf, Is.False);
            Assert.That(panel.NextPageButton.enabledSelf, Is.True);

            Click(panel.NextPageButton);

            Assert.That(panel.PageText.text, Is.EqualTo("2 / 2"));
            Assert.That(Row(0).Q<Label>(LevelValidationPanel.k_numberName).text, Is.EqualTo("#6"));
            Assert.That(ShownChips(Row(0)), Is.EqualTo(orders[5]));
            for (int slot = 1; slot < 5; slot++)
            {
                Assert.That(Row(slot).visible, Is.False, "An empty slot keeps its height but shows nothing.");
            }

            Assert.That(panel.PreviousPageButton.enabledSelf, Is.True);
            Assert.That(panel.NextPageButton.enabledSelf, Is.False);

            // Each press shows page 1; the unchanged draft reuses the kept list without a search.
            int runs = m_window.ValidationFlow.RunCount;
            int listings = m_window.ValidationFlow.ListRunCount;
            Validate(root);

            Assert.That(panel.PageText.text, Is.EqualTo("1 / 2"));
            Assert.That(Row(0).Q<Label>(LevelValidationPanel.k_numberName).text, Is.EqualTo("#1"));
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(runs));
            Assert.That(m_window.ValidationFlow.ListRunCount, Is.EqualTo(listings));
        }

        [Test]
        public void MoreThanTwentyExitOrdersShowTwentyEntriesOnFourPages()
        {
            VisualElement root = OpenWindow();
            UseDatabase(FiveSingles());

            Validate(root);

            LevelValidationPanel panel = m_window.ValidationPanel;
            Assert.That(panel.Summary, Is.EqualTo("Solvable in 5 moves · 20+ solutions"));
            Assert.That(panel.Pages.Count, Is.EqualTo(20));
            Assert.That(panel.PageText.text, Is.EqualTo("1 / 4"));
            Click(panel.NextPageButton);
            Click(panel.NextPageButton);
            Click(panel.NextPageButton);

            Assert.That(panel.PageText.text, Is.EqualTo("4 / 4"));
            Assert.That(Row(0).Q<Label>(LevelValidationPanel.k_numberName).text, Is.EqualTo("#16"));
            Assert.That(Row(4).Q<Label>(LevelValidationPanel.k_numberName).text, Is.EqualTo("#20"));
            Assert.That(Row(4).tooltip, Does.StartWith("Exit order: B1 → B5 → B2 → B4 → B3."));
            Assert.That(panel.NextPageButton.enabledSelf, Is.False);
        }

        [Test]
        public void ALongOrderEndsWithAnEllipsisAndTheTooltipNamesTheWholeOrder()
        {
            VisualElement root = OpenWindow();
            UseDatabase(FiveSingles());
            Validate(root);

            SetToolsWidth(root, 240f);

            string[] order = { "B1", "B2", "B3", "B4", "B5" };
            List<string> shown = ShownChips(Row(0));
            Assert.That(shown.Count, Is.InRange(1, 4), "A 240 px row cannot take five chips.");
            for (int index = 0; index < shown.Count; index++)
            {
                Assert.That(shown[index], Is.EqualTo(order[index]), "The first chips of the order show.");
            }

            Assert.That(Ellipsis(Row(0)), Is.True);
            Assert.That(Row(0).tooltip, Does.StartWith("Exit order: B1 → B2 → B3 → B4 → B5."));

            SetToolsWidth(root, 400f);

            Assert.That(ShownChips(Row(0)), Is.EqualTo(new[] { "B1", "B2", "B3", "B4", "B5" }));
            Assert.That(Ellipsis(Row(0)), Is.False);
        }

        [Test]
        public void TheChipsThatFitFollowTheRowWidth()
        {
            float five = 5 * LevelValidationPanel.k_chipWidth + 4 * LevelValidationPanel.k_arrowWidth;

            Assert.That(LevelValidationPanel.GetVisibleChipCount(5, five), Is.EqualTo(5));
            Assert.That(LevelValidationPanel.GetVisibleChipCount(5, float.NaN), Is.EqualTo(5), "Not laid out yet.");
            Assert.That(LevelValidationPanel.GetVisibleChipCount(0, 100f), Is.Zero);
            int fitting = LevelValidationPanel.GetVisibleChipCount(5, five - 1f);
            Assert.That(fitting, Is.LessThan(5));
            Assert.That(fitting * (LevelValidationPanel.k_chipWidth + LevelValidationPanel.k_arrowWidth) +
                        LevelValidationPanel.k_ellipsisWidth, Is.LessThanOrEqualTo(five - 1f), "The chips and \"…\" fit.");
            Assert.That(LevelValidationPanel.GetVisibleChipCount(5, 10f), Is.Zero, "Only \"…\" fits.");
        }

        [Test]
        public void PagesHoldFiveEntriesAndThePagerShowsOnlyForMore()
        {
            LevelSolutionPages pages = new LevelSolutionPages();

            pages.Reset(0);
            Assert.That(pages.PageCount, Is.EqualTo(1));
            Assert.That(pages.ShowsPager, Is.False);
            Assert.That(pages.GetEntryIndex(0), Is.EqualTo(-1));

            pages.Reset(5);
            Assert.That(pages.ShowsPager, Is.False);
            Assert.That(pages.GetEntryIndex(4), Is.EqualTo(4));

            pages.Reset(6);
            Assert.That(pages.PageCount, Is.EqualTo(2));
            Assert.That(pages.ShowsPager, Is.True);
            Assert.That(pages.CanGoBack, Is.False);
            Assert.That(pages.Previous(), Is.False);
            Assert.That(pages.Next(), Is.True);
            Assert.That(pages.PageText, Is.EqualTo("2 / 2"));
            Assert.That(pages.GetEntryIndex(0), Is.EqualTo(5));
            Assert.That(pages.GetEntryIndex(1), Is.EqualTo(-1));
            Assert.That(pages.GetEntryIndex(5), Is.EqualTo(-1), "Only five slots.");
            Assert.That(pages.CanGoNext, Is.False);
            Assert.That(pages.Next(), Is.False);

            pages.Reset(20);
            Assert.That(pages.Page, Is.Zero, "A new list starts on page 1.");
            Assert.That(pages.PageText, Is.EqualTo("1 / 4"));
        }

        [Test]
        public void AListingBudgetTooSmallKeepsTheVerdictWithAtLeastKSolutions()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            m_window.ValidationFlow.Options = new LevelCheckOptions { MaxListPositions = 1 };

            Validate(root);

            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo("Solvable in 3 moves · at least 1 solution"));
            Assert.That(Status(root), Is.EqualTo("Solvable in 3 moves."));
            Assert.That(Row(0).visible, Is.True);
            Assert.That(Row(1).visible, Is.False);
        }

        [Test]
        public void CancelDuringTheListingKeepsTheVerdictAndTheEntriesFoundSoFar()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            m_window.ValidationFlow.ProgressDelaySeconds = 0;
            m_cancelListing = true;

            Validate(root);

            Assert.That(m_progress, Has.Count.EqualTo(2), "One line per phase.");
            Assert.That(m_progress[0], Does.StartWith("Checked "));
            Assert.That(m_progress[1], Does.StartWith(LevelValidationFlow.k_listingProgressPrefix)
                .And.EndWith("Cancel stops the listing."));
            Assert.That(m_clearedProgress, Is.EqualTo(1), "One bar for both phases, cleared once.");
            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo("Solvable in 3 moves · at least 1 solution"));
            Assert.That(Status(root), Is.EqualTo("Solvable in 3 moves."));
            Assert.That(ShownChips(Row(0)), Is.EqualTo(new[] { "B1", "B2", "B3" }), "The check's own solution stays.");

            // The kept verdict and its partial list are reused, and Save Level saves without a question.
            Validate(root);
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1));
            Assert.That(m_window.ValidationFlow.ListRunCount, Is.EqualTo(1));
            Click(root.Q<Button>("save-level"));
            Assert.That(m_titles, Is.Empty);
            Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
        }

        [Test]
        public void CancelDuringTheCheckListsNothingAndKeepsNothing()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            m_window.ValidationFlow.ProgressDelaySeconds = 0;
            m_cancelCheck = true;

            Validate(root);

            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo("Not checked: the check was cancelled."));
            Assert.That(Row(0).visible, Is.False);
            Assert.That(m_window.ValidationFlow.ListRunCount, Is.Zero);

            m_cancelCheck = false;
            Validate(root);
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(2), "A cancelled check is never reused.");
            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo("Solvable in 3 moves · 6 solutions"));
        }

        [Test]
        public void OtherAnswersShowTheirSentenceWithAnEmptyList()
        {
            VisualElement root = OpenWindow();
            UseDatabase(NoGate());
            Validate(root);
            AssertAnswerWithoutList(Does.StartWith("Cannot be solved: "));

            UseDatabase(OneSlide());
            // The block needs a slide before it can leave, so one position is not enough.
            m_window.ValidationFlow.Options = new LevelCheckOptions { MaxPositions = 1 };
            Validate(root);
            AssertAnswerWithoutList(Does.StartWith("Not confirmed: the check stopped after "));

            m_window.ValidationFlow.Options = null;
            m_window.ValidationFlow.IsPlaying = () => true;
            LevelEditorTestAccess.Invoke(m_window, "ValidateLevel");
            AssertAnswerWithoutList(Is.EqualTo("Not checked: the Editor is in Play Mode."));
            Assert.That(m_window.ValidationFlow.ListRunCount, Is.Zero);
        }

        [Test]
        public void SaveLevelTestLevelAndSaveOnCloseNeverList()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());

            EditTimer(root, 75);
            Click(root.Q<Button>("save-level"));
            Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1));

            EditTimer(root, 80);
            Click(root.Q<Button>("test-level"));
            Assert.That(m_calls, Is.EqualTo(new[] { "write", "write", "start" }));
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(2));

            EditTimer(root, 85);
            m_window.SaveChanges();
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(3));

            Assert.That(m_window.ValidationFlow.ListRunCount, Is.Zero, "Only Validate Level lists.");
            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo(LevelValidationPanel.k_listHint));
            Assert.That(m_titles, Is.Empty);
        }

        [Test]
        public void AfterASaveCheckValidateLevelRunsOnlyTheListing()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            EditTimer(root, 75);
            // The write fails, so the draft stays as it was checked and the verdict stays kept, without a list.
            m_writeFails = true;
            Click(root.Q<Button>("save-level"));
            Assume.That(m_calls, Is.EqualTo(new[] { "write" }));
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1));
            Assert.That(m_window.ValidationFlow.ListRunCount, Is.Zero);

            Validate(root);

            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1), "The kept verdict is not checked again.");
            Assert.That(m_window.ValidationFlow.ListRunCount, Is.EqualTo(1), "Only the listing searched.");
            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo("Solvable in 3 moves · 6 solutions"));
        }

        [TestCase("same-content save")]
        [TestCase("edit")]
        [TestCase("undo")]
        [TestCase("json switch")]
        [TestCase("level switch")]
        public void TheListStaysForTheSameContentAndClearsOnAnyOtherChange(string change)
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles(), IceBetweenTwo());
            LevelValidationPanel panel = m_window.ValidationPanel;
            if (change == "undo")
            {
                // The listed draft is an edit, so an undo back to the saved level changes the content.
                EditTimer(root, 75);
            }

            Validate(root);
            string listed = panel.Summary;
            Assume.That(Row(0).visible, Is.True, "A list shows.");

            switch (change)
            {
                case "same-content save":
                    // Save Level reloads the same content: the list stays, though the kept verdict is dropped (DEC-144).
                    Click(root.Q<Button>("save-level"));
                    Assume.That(m_calls, Is.EqualTo(new[] { "write" }));
                    Assert.That(panel.Summary, Is.EqualTo(listed));
                    Assert.That(Row(0).visible, Is.True);
                    Validate(root);
                    Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(2),
                        "The save's reload invalidated the verdict.");
                    Assert.That(panel.Summary, Is.EqualTo(listed), "The same level lists the same solutions.");
                    break;
                case "edit":
                    EditTimer(root, 75);
                    AssertHint("An edit clears the list.");
                    break;
                case "undo":
                    // An undo that changes the content lands in OnUndoRedo with the draft out of step.
                    LevelEditorTestAccess.Get<LevelDraft>(m_window, "m_draft").Replace(ThreeSingles());
                    LevelEditorTestAccess.Invoke(m_window, "OnUndoRedo");
                    AssertHint("An undo that changes the content clears the list.");
                    break;
                case "json switch":
                    LevelEditorTestAccess.Set(m_window, "m_loadedGuid", "another-level-json");
                    LevelEditorTestAccess.Invoke(m_window, "LoadSelectedDraft");
                    AssertHint("A JSON switch clears the list, even to the same content.");
                    break;
                case "level switch":
                    LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 1);
                    LevelEditorTestAccess.Invoke(m_window, "LoadSelectedDraft");
                    AssertHint("A level switch clears the list.");
                    break;
                default:
                    Assert.Fail("Unknown change: " + change);
                    break;
            }
        }

        [Test]
        public void APlayedSolutionShowsEveryStepAndExitAndThenTheDraftAgain()
        {
            VisualElement root = OpenWindow();
            UseDatabase(IceBetweenTwo());
            ValidateLayout(root);
            Validate(root);
            Assume.That(m_window.ValidationPanel.Summary, Is.EqualTo("Solvable in 3 moves · 2 solutions"));
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            string draftBefore = JsonUtility.ToJson(m_window.GetDraftLevel());
            string fileBefore = DatabaseJson();
            int undoGroup = Undo.GetCurrentGroup();
            string undoName = Undo.GetCurrentGroupName();
            LevelCanvasSelection selection = LevelEditorTestAccess.Get<LevelCanvasSelection>(m_window, "m_canvasSelection");
            int selectedBlock = LevelEditorTestAccess.Get<int>(m_window, "m_selectedBlockIndex");
            LevelEditorTool tool = LevelEditorTestAccess.Get<LevelEditorToolState>(m_window, "m_toolState").Tool;
            LevelData published = LevelDatabaseWindow.LastPublishedLevel;
            int runs = m_window.ValidationFlow.RunCount;
            int listings = m_window.ValidationFlow.ListRunCount;
            Assume.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.Block));

            PlayRow(0);

            // The start board: Orange and Yellow leave first, then the thawed Red steps up and leaves.
            Assert.That(m_updates, Has.Count.EqualTo(1), "The playback subscribes one update callback.");
            AssertRowEnd(0, true, "The playing row shows its controls in place of ▶.");
            AssertPauseControl(0, false, "It plays, so its first control pauses.");
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.None), "No selection outline plays.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 2", "b2 (0,2)", "b3 (2,2)");

            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 1", "b3 (2,2)");
            AssertStaysUntil(LevelSolutionPlayback.k_exitSeconds, 1);
            AssertCanvasBlocks(canvas, "b1 (1,1)");
            AssertStaysUntil(LevelSolutionPlayback.k_exitSeconds, 2);
            AssertCanvasBlocks(canvas, "b1 (1,2)");
            AssertStaysUntil(LevelSolutionPlayback.k_stepSeconds, 3);
            Assert.That(canvas.Model.BlockCount, Is.Zero, "The cleared board.");
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True, "The cleared board holds a while.");
            AssertStaysUntil(LevelSolutionPlayback.k_clearedSeconds, 4);

            // The end: the draft is back on the canvas with its selection, and the preview gets it on the next update.
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.False);
            Assert.That(m_updates, Is.Empty, "The update callback is unsubscribed.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 2", "b2 (0,2)", "b3 (2,2)");
            Assert.That(canvas.Selection, Is.EqualTo(selection));
            AssertRowEnd(0, false, "The row shows ▶ again.");
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.True);

            // Five frames went to the preview's event, the cleared board included; none became LastPublishedLevel.
            Assert.That(m_frames, Has.Count.EqualTo(5));
            Assert.That(m_frames[0].BlockCount, Is.EqualTo(3));
            Assert.That(m_frames[4].BlockCount, Is.Zero);
            Assert.That(LevelDatabaseWindow.LastPublishedLevel, Is.SameAs(published));

            // Nothing changed: the draft, its Undo history, the dirty state, the selection, the tool, the file and the
            // kept result, which Validate Level reuses without a search.
            Assert.That(JsonUtility.ToJson(m_window.GetDraftLevel()), Is.EqualTo(draftBefore));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
            Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo(undoName));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(LevelEditorTestAccess.Get<LevelCanvasSelection>(m_window, "m_canvasSelection"), Is.EqualTo(selection));
            Assert.That(LevelEditorTestAccess.Get<int>(m_window, "m_selectedBlockIndex"), Is.EqualTo(selectedBlock));
            Assert.That(LevelEditorTestAccess.Get<LevelEditorToolState>(m_window, "m_toolState").Tool, Is.EqualTo(tool));
            Assert.That(DatabaseJson(), Is.EqualTo(fileBefore));
            Assert.That(m_calls, Is.Empty);
            Validate(root);
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(runs));
            Assert.That(m_window.ValidationFlow.ListRunCount, Is.EqualTo(listings));

            LevelEditorTestAccess.Invoke(m_window, "PublishDraft");
            Assert.That(JsonUtility.ToJson(LevelDatabaseWindow.LastPublishedLevel), Is.EqualTo(draftBefore),
                "The preview gets the draft again.");
        }

        // Contract Revision 1: the active row's Stop button replaces a click on the playing row, and every stop point also
        // stops a paused playback.
        [TestCase("stop button", false)]
        [TestCase("stop button", true)]
        [TestCase("canvas press", false)]
        [TestCase("canvas press", true)]
        [TestCase("edit", false)]
        [TestCase("edit", true)]
        [TestCase("undo", false)]
        [TestCase("undo", true)]
        [TestCase("level switch", false)]
        [TestCase("level switch", true)]
        [TestCase("json switch", false)]
        [TestCase("json switch", true)]
        [TestCase("save", false)]
        [TestCase("save", true)]
        [TestCase("validate", false)]
        [TestCase("validate", true)]
        [TestCase("test", false)]
        [TestCase("test", true)]
        [TestCase("play mode", false)]
        [TestCase("play mode", true)]
        [TestCase("disable", false)]
        [TestCase("disable", true)]
        public void EveryStopPointStopsAtOnceAndShowsTheDraft(string stopPoint, bool paused)
        {
            VisualElement root = OpenWindow();
            UseDatabase(IceBetweenTwo(), ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            string draftBefore = JsonUtility.ToJson(m_window.GetDraftLevel());
            PlayRow(0);
            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            Assume.That(canvas.Model.BlockCount, Is.EqualTo(2), "A frame after the first exit shows.");
            if (paused)
            {
                Click(Control(0, LevelValidationPanel.k_pauseName));
                Assume.That(m_window.SolutionPlayback.IsPaused, Is.True);
                Assume.That(m_updates, Is.Empty);
            }

            LevelData shownDraft = m_window.GetDraftLevel();

            switch (stopPoint)
            {
                case "stop button":
                    Click(Control(0, LevelValidationPanel.k_stopName));
                    break;
                case "canvas press":
                    // A right press on a block would remove it; while a solution plays it only stops the playback.
                    Vector2 block = canvas.LocalToWorld(canvas.Geometry.GetCellRect(new Vector2Int(1, 1)).center);
                    SendPointer(root, EventType.MouseDown, block, 1);
                    SendPointer(root, EventType.MouseUp, block, 1);
                    Assert.That(JsonUtility.ToJson(m_window.GetDraftLevel()), Is.EqualTo(draftBefore), "It edits nothing.");
                    Assert.That(m_window.hasUnsavedChanges, Is.False);
                    break;
                case "edit":
                    EditTimer(root, 75);
                    shownDraft = m_window.GetDraftLevel();
                    break;
                case "undo":
                    LevelEditorTestAccess.Invoke(m_window, "OnUndoRedo");
                    break;
                case "level switch":
                    LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 1);
                    LevelEditorTestAccess.Invoke(m_window, "LoadSelectedDraft");
                    shownDraft = m_window.GetDraftLevel();
                    Assume.That(shownDraft.Id, Is.EqualTo("p3"));
                    break;
                case "json switch":
                    LevelEditorTestAccess.Set(m_window, "m_loadedGuid", "another-level-json");
                    LevelEditorTestAccess.Invoke(m_window, "LoadSelectedDraft");
                    break;
                case "save":
                    Click(root.Q<Button>("save-level"));
                    Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
                    break;
                case "validate":
                    Click(root.Q<Button>("validate-level"));
                    break;
                case "test":
                    Click(root.Q<Button>("test-level"));
                    Assert.That(m_calls, Is.EqualTo(new[] { "start" }));
                    break;
                case "play mode":
                    LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.ExitingEditMode);
                    break;
                case "disable":
                    LevelEditorTestAccess.Invoke(m_window, "OnDisable");
                    break;
            }

            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, stopPoint);
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.False, stopPoint);
            Assert.That(m_updates, Is.Empty, "The update callback is unsubscribed.");
            Assert.That(m_window.ValidationPanel.ActiveEntry, Is.EqualTo(-1));
            AssertRowEnd(0, false, stopPoint);
            AssertCanvasShows(canvas, shownDraft);
            if (stopPoint == "disable")
            {
                // The window's scheduled publishes end with it, so the preview gets the draft at once.
                Assert.That(JsonUtility.ToJson(LevelDatabaseWindow.LastPublishedLevel), Is.EqualTo(draftBefore));
                Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.False);
                LevelEditorTestAccess.Invoke(m_window, "OnEnable");
            }
            else if (stopPoint == "test")
            {
                Assert.That(LevelDatabaseWindow.LastPublishedLevel, Is.Null, "Test Level withdraws the preview.");
            }
            else
            {
                Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.True,
                    "The preview gets the draft on the next update.");
            }
        }

        [Test]
        public void AnotherRowPlaysItsEntryFromTheStart()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            PlayRow(0);
            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            m_frames.Clear();
            Assume.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.False);

            PlayRow(1);

            Assert.That(m_window.SolutionPlayback.EntryIndex, Is.EqualTo(1));
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero, "From its start.");
            Assert.That(m_updates, Has.Count.EqualTo(1), "One update callback, not two.");
            Assert.That(m_frames, Has.Count.EqualTo(1));
            Assert.That(m_frames[0].BlockCount, Is.EqualTo(3));
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.False,
                "No draft publish lands between the two entries: one frame stream.");
            AssertRowEnd(0, false, "The first row shows ▶ again.");
            AssertRowEnd(1, true, "The controls move to the new active row.");

            // #2 is B1, B3, B2: after B1, B3 leaves next.
            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            AssertStaysUntil(LevelSolutionPlayback.k_exitSeconds, 1);
            AssertCanvasBlocks(root.Q<LevelCanvasElement>(), "b2 (1,2)");
        }

        [Test]
        public void InPlayModeTheRowsAreDisabledAndAClickPlaysNothing()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            LevelValidationPanel panel = m_window.ValidationPanel;
            Assume.That(Row(0).enabledSelf, Is.True);
            Assume.That(Row(0).tooltip, Is.EqualTo("Exit order: B1 → B2 → B3. " + LevelValidationPanel.k_playTooltip));
            bool playing = true;
            m_window.ValidationFlow.IsPlaying = () => playing;

            // Entering Play Mode refreshes the buttons; the rows follow Validate Level's Play Mode rule.
            LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.ExitingEditMode);

            Assert.That(root.Q<Button>("validate-level").enabledSelf, Is.False);
            for (int slot = 0; slot < 5; slot++)
            {
                Assert.That(Row(slot).enabledSelf, Is.False, "#" + (slot + 1));
            }

            Assert.That(Row(0).tooltip, Is.EqualTo("Exit order: B1 → B2 → B3. " + LevelValidationPanel.k_playModeTooltip));
            Click(Row(0));
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.False, "No playback starts in Play Mode.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(m_frames, Is.Empty);
            Assert.That(panel.ActiveEntry, Is.EqualTo(-1));
            Assert.That(Row(0).Q<Label>(LevelValidationPanel.k_glyphName).text, Is.EqualTo(LevelValidationPanel.k_playGlyph));

            // Another page keeps its rows disabled.
            Click(panel.NextPageButton);
            Assert.That(Row(0).enabledSelf, Is.False);
            Assert.That(Row(0).tooltip, Does.EndWith(LevelValidationPanel.k_playModeTooltip));
            Click(panel.PreviousPageButton);

            // Back in Edit Mode a row plays again.
            playing = false;
            LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.EnteredEditMode);

            Assert.That(Row(0).enabledSelf, Is.True);
            Assert.That(Row(0).tooltip, Is.EqualTo("Exit order: B1 → B2 → B3. " + LevelValidationPanel.k_playTooltip));
            PlayRow(0);
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True);
            Assert.That(m_updates, Has.Count.EqualTo(1));
        }

        [Test]
        public void APenEditOutlineHidesWhileASolutionPlaysAndComesBackAfter()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            // The Pen edits b2, as after a Pen click on it; set directly, so the piece library's content does not matter.
            LevelEditorTestAccess.Invoke(m_window, "SetTool", LevelEditorTool.Pen);
            LevelEditorTestAccess.Set(m_window, "m_penEditBlockId", "b2");
            LevelEditorTestAccess.Invoke(m_window, "SyncTool");
            Assert.That(canvas.IsPenEditing, Is.True);
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b2"), "The canvas outlines b2 and shows its green cells.");

            PlayRow(0);

            Assert.That(canvas.FreehandBlockId, Is.Null, "A playback frame shows no Pen edit outline or green cells.");
            Assert.That(canvas.IsPenEditing, Is.True, "The Pen edit itself goes on.");
            LevelEditorTestAccess.Invoke(m_window, "SyncTool");
            Assert.That(canvas.FreehandBlockId, Is.Null, "A tool refresh while it plays keeps them hidden.");

            // The playback's end shows the Pen edit again as it was.
            RunUpdatesAt(m_now + 10.0);
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.False);
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b2"));
            Assert.That(canvas.IsPenEditing, Is.True);

            // So does a stop, now through the row's Stop button (contract Revision 1).
            PlayRow(0);
            Assume.That(canvas.FreehandBlockId, Is.Null);
            Click(Control(0, LevelValidationPanel.k_stopName));
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.False);
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b2"));
        }

        [Test]
        public void ThePlaybackTimesEachFrameByItsKindAndStopsWithoutFinishing()
        {
            LevelData level = IceBetweenTwo();
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);
            List<LevelData> shown = new List<LevelData>();
            int finished = 0;
            LevelSolutionPlayback playback = new LevelSolutionPlayback(shown.Add, () => finished++)
            {
                Clock = () => m_now,
                AddUpdate = m_updates.Add,
                RemoveUpdate = callback => m_updates.Remove(callback)
            };

            Assert.That(LevelSolutionPlayback.GetFrameSeconds(result.Steps, 0), Is.EqualTo(LevelSolutionPlayback.k_startSeconds));
            Assert.That(LevelSolutionPlayback.GetFrameSeconds(result.Steps, 1), Is.EqualTo(LevelSolutionPlayback.k_exitSeconds));
            Assert.That(LevelSolutionPlayback.GetFrameSeconds(result.Steps, 3), Is.EqualTo(LevelSolutionPlayback.k_stepSeconds));
            Assert.That(LevelSolutionPlayback.GetFrameSeconds(result.Steps, 4), Is.EqualTo(LevelSolutionPlayback.k_clearedSeconds));
            // Contract Revision 1's slower pace.
            Assert.That(LevelSolutionPlayback.k_startSeconds, Is.EqualTo(0.4));
            Assert.That(LevelSolutionPlayback.k_stepSeconds, Is.EqualTo(0.25));
            Assert.That(LevelSolutionPlayback.k_exitSeconds, Is.EqualTo(0.4));
            Assert.That(LevelSolutionPlayback.k_clearedSeconds, Is.EqualTo(1.0));

            playback.Play(0, level, result.Steps);
            Assert.That(playback.FrameCount, Is.EqualTo(result.Steps.Count + 1));
            Assert.That(shown, Has.Count.EqualTo(1));
            Assert.That(shown[0], Is.SameAs(level), "The start board is the level itself.");
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(JsonUtility.ToJson(IceBetweenTwo())), "It is not changed.");

            // A slow update skips to the frame whose time has come instead of slowing the pace.
            m_now += LevelSolutionPlayback.k_startSeconds + LevelSolutionPlayback.k_exitSeconds + 0.05;
            playback.Tick();
            Assert.That(playback.FrameIndex, Is.EqualTo(2));
            Assert.That(shown, Has.Count.EqualTo(2));

            Assert.That(playback.Stop(), Is.True);
            Assert.That(m_updates, Is.Empty);
            Assert.That(playback.Stop(), Is.False, "Nothing plays any more.");
            m_now += 10.0;
            playback.Tick();
            Assert.That(finished, Is.Zero, "A stop never runs the end callback.");

            playback.Play(0, level, result.Steps);
            m_now += 10.0;
            playback.Tick();
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(playback.IsPlaying, Is.False);
            Assert.That(m_updates, Is.Empty);
        }

        [Test]
        public void SolutionFramesAreRaisedOnlyByPlaybackAndThePublishedLevelStaysTheDraft()
        {
            VisualElement root = OpenWindow();
            UseDatabase(OneSlide());
            ValidateLayout(root);
            Validate(root);
            LevelData published = LevelDatabaseWindow.LastPublishedLevel;
            Assume.That(published, Is.Not.Null);
            Assert.That(m_frames, Is.Empty, "Validate Level raises no frame.");

            PlayRow(0);
            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            AssertCanvasBlocks(root.Q<LevelCanvasElement>(), "b1 (0,1)");
            AssertStaysUntil(LevelSolutionPlayback.k_stepSeconds, 1);
            Assert.That(root.Q<LevelCanvasElement>().Model.BlockCount, Is.Zero);
            AssertStaysUntil(LevelSolutionPlayback.k_clearedSeconds, 2);

            Assert.That(m_frames, Has.Count.EqualTo(3), "The start, the slide and the cleared board.");
            Assert.That(m_frames[1].GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(0, 1)));
            Assert.That(m_frames[2].BlockCount, Is.Zero);
            Assert.That(LevelDatabaseWindow.LastPublishedLevel, Is.SameAs(published));
        }

        [Test]
        public void ThePlaybackPausesResumesAndStepsAndAStepOnTheClearedBoardEndsIt()
        {
            LevelData level = IceBetweenTwo();
            LevelCheckResult result = LevelSolvabilityCheck.Run(level);
            List<LevelData> shown = new List<LevelData>();
            int finished = 0;
            LevelSolutionPlayback playback = new LevelSolutionPlayback(shown.Add, () => finished++)
            {
                Clock = () => m_now,
                AddUpdate = m_updates.Add,
                RemoveUpdate = callback => m_updates.Remove(callback)
            };

            playback.Play(0, level, result.Steps);
            Assert.That(playback.IsPlaying, Is.True);
            Assert.That(playback.Pause(), Is.True);
            Assert.That(playback.IsActive, Is.True);
            Assert.That(playback.IsPaused, Is.True);
            Assert.That(m_updates, Is.Empty, "Pause removes the update callback.");
            Assert.That(playback.Pause(), Is.False, "It is paused already.");
            m_now += 10.0;
            playback.Tick();
            Assert.That(playback.FrameIndex, Is.Zero, "A paused playback keeps its frame.");

            // Resume: the next frame follows the shown frame's full time from the Resume (summed as the playback sums it).
            double end = m_now + LevelSolutionPlayback.k_startSeconds;
            Assert.That(playback.Resume(), Is.True);
            Assert.That(m_updates, Has.Count.EqualTo(1));
            Assert.That(playback.Resume(), Is.False, "It plays already.");
            m_now = end - 0.01;
            playback.Tick();
            Assert.That(playback.FrameIndex, Is.Zero);
            m_now = end;
            playback.Tick();
            Assert.That(playback.FrameIndex, Is.EqualTo(1));

            // Step pauses first, then shows exactly one more frame a call.
            Assert.That(playback.Step(), Is.True);
            Assert.That(playback.IsPaused, Is.True);
            Assert.That(m_updates, Is.Empty);
            Assert.That(playback.FrameIndex, Is.EqualTo(2));
            Assert.That(shown, Has.Count.EqualTo(3));
            while (playback.FrameIndex < playback.FrameCount - 1)
            {
                Assert.That(playback.Step(), Is.True);
            }

            Assert.That(shown, Has.Count.EqualTo(playback.FrameCount));
            Assert.That(shown[shown.Count - 1].BlockCount, Is.Zero, "The last frame is the cleared board.");
            Assert.That(finished, Is.Zero);

            // StepForward (the → key, contract Revision 2 as amended) stops on the cleared board: no frame, no end.
            int shownBefore = shown.Count;
            Assert.That(playback.StepForward(), Is.False);
            Assert.That(playback.IsActive, Is.True);
            Assert.That(playback.IsPaused, Is.True);
            Assert.That(shown, Has.Count.EqualTo(shownBefore));
            Assert.That(finished, Is.Zero, "It never ends the playback.");
            Assert.That(m_updates, Is.Empty);

            // StepBack (the ← key, contract Revision 2) shows the previous frame, stays paused and subscribes nothing.
            int last = playback.FrameIndex;
            Assert.That(playback.StepBack(), Is.True);
            Assert.That(playback.FrameIndex, Is.EqualTo(last - 1));
            Assert.That(shown[shown.Count - 1].BlockCount, Is.Not.Zero, "The frame before the cleared board shows again.");
            Assert.That(playback.IsPaused, Is.True);
            Assert.That(m_updates, Is.Empty);
            Assert.That(playback.Step(), Is.True);
            Assert.That(playback.FrameIndex, Is.EqualTo(last));

            Assert.That(playback.Step(), Is.True);
            Assert.That(playback.IsActive, Is.False, "A Step on the cleared board ends the playback.");
            Assert.That(finished, Is.EqualTo(1), "As its end after the last frame's time would.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(playback.Step(), Is.False, "Nothing is active.");

            // A paused start (Auto Play off) shows the start board and subscribes nothing; a stop while paused.
            playback.Play(0, level, result.Steps, paused: true);
            Assert.That(playback.IsPaused, Is.True);
            Assert.That(playback.FrameIndex, Is.Zero);
            Assert.That(shown[shown.Count - 1], Is.SameAs(level), "The start board shows at once.");
            Assert.That(m_updates, Is.Empty);
            int shownCount = shown.Count;
            Assert.That(playback.StepBack(), Is.False, "Nothing comes before the start board.");
            Assert.That(playback.FrameIndex, Is.Zero);
            Assert.That(shown, Has.Count.EqualTo(shownCount));
            Assert.That(playback.Stop(), Is.True);
            Assert.That(playback.IsActive, Is.False);
            Assert.That(playback.IsPaused, Is.False);
            Assert.That(m_updates, Is.Empty);
            Assert.That(finished, Is.EqualTo(1), "A stop never runs the end callback.");
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(JsonUtility.ToJson(IceBetweenTwo())), "The level is unchanged.");
        }

        [Test]
        public void TheActiveRowShowsPauseStepAndStopInPlaceOfItsGlyph()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            LevelValidationPanel panel = m_window.ValidationPanel;
            string stateBefore = CaptureUnchangedState();
            for (int slot = 0; slot < 5; slot++)
            {
                AssertRowEnd(slot, false, "No solution shows yet.");
            }

            PlayRow(1);

            Assert.That(panel.ActiveEntry, Is.EqualTo(1));
            Assert.That(panel.IsPaused, Is.False);
            AssertRowEnd(1, true, "Only the active row shows the controls.");
            foreach (int slot in new[] { 0, 2, 3, 4 })
            {
                AssertRowEnd(slot, false, "#" + (slot + 1) + " keeps ▶.");
            }

            AssertPauseControl(1, false, "Playing, the first control pauses.");
            Assert.That(Control(1, LevelValidationPanel.k_pauseName).tooltip, Is.EqualTo(LevelValidationPanel.k_pauseTooltip));
            Assert.That(Control(1, LevelValidationPanel.k_stepName).text, Is.EqualTo(LevelValidationPanel.k_stepGlyph));
            Assert.That(Control(1, LevelValidationPanel.k_stepName).tooltip, Is.EqualTo(LevelValidationPanel.k_stepTooltip));
            Assert.That(Control(1, LevelValidationPanel.k_stopName).text, Is.EqualTo(LevelValidationPanel.k_stopGlyph));
            Assert.That(Control(1, LevelValidationPanel.k_stopName).tooltip, Is.EqualTo(LevelValidationPanel.k_stopTooltip));

            Click(Control(1, LevelValidationPanel.k_pauseName));

            Assert.That(panel.IsPaused, Is.True);
            Assert.That(Control(1, LevelValidationPanel.k_pauseName).text, Is.EqualTo(LevelValidationPanel.k_playGlyph),
                "Paused, the first control resumes.");
            Assert.That(Control(1, LevelValidationPanel.k_pauseName).tooltip,
                Is.EqualTo(LevelValidationPanel.k_resumeTooltip));

            // Another page does not show the controls on a row of another entry, and they come back with their entry.
            Click(panel.NextPageButton);
            AssertRowEnd(0, false, "#6 is not active.");
            Assert.That(m_window.SolutionPlayback.IsActive, Is.True, "Paging changes nothing.");
            Click(panel.PreviousPageButton);
            AssertRowEnd(1, true, "Back on page 1.");
            Assert.That(Control(1, LevelValidationPanel.k_pauseName).text, Is.EqualTo(LevelValidationPanel.k_playGlyph));

            Click(Control(1, LevelValidationPanel.k_stopName));

            Assert.That(panel.ActiveEntry, Is.EqualTo(-1));
            Assert.That(m_window.SolutionPlayback.IsActive, Is.False);
            for (int slot = 0; slot < 5; slot++)
            {
                AssertRowEnd(slot, false, "Stopped, every row shows ▶.");
            }

            Assert.That(CaptureUnchangedState(), Is.EqualTo(stateBefore),
                "Play, Pause, paging and Stop changed no draft, Undo, dirty state, selection, tool, kept result or file.");
        }

        [Test]
        public void TheActiveRowRefitsItsChipsToTheWidthItsControlsLeave()
        {
            VisualElement root = OpenWindow();
            UseDatabase(FiveSingles());
            Validate(root);
            SetToolsWidth(root, 280f);
            Assume.That(ShownChips(Row(0)), Has.Count.EqualTo(5), "A five-block order fits a 280 px row.");
            float wide = Row(0).Q(LevelValidationPanel.k_orderName).layout.width;

            PlayRow(0);
            ValidateLayout(root);

            VisualElement order = Row(0).Q(LevelValidationPanel.k_orderName);
            Assert.That(order.layout.width, Is.LessThan(wide), "The controls take width from the order.");
            int fitting = LevelValidationPanel.GetVisibleChipCount(5, order.layout.width);
            Assert.That(fitting, Is.LessThan(5));
            Assert.That(ShownChips(Row(0)), Has.Count.EqualTo(fitting));
            Assert.That(Ellipsis(Row(0)), Is.True, "The cut order ends with \"…\".");
            Assert.That(ShownChips(Row(1)), Has.Count.EqualTo(5), "The other rows keep their width.");
            List<string> overflows = new List<string>();
            CollectOverflows(Row(0), 2, overflows);
            Assert.That(overflows, Is.Empty, "The row's labels, order, controls and chips fit.");

            Click(Control(0, LevelValidationPanel.k_stopName));
            ValidateLayout(root);

            Assert.That(ShownChips(Row(0)), Has.Count.EqualTo(5), "Stopped, the row takes its width back.");
            Assert.That(Ellipsis(Row(0)), Is.False);
        }

        [Test]
        public void PauseKeepsTheFrameAndResumeShowsTheNextAfterItsFullTime()
        {
            VisualElement root = OpenWindow();
            UseDatabase(IceBetweenTwo());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            string draftBefore = JsonUtility.ToJson(m_window.GetDraftLevel());
            string fileBefore = DatabaseJson();
            int undoGroup = Undo.GetCurrentGroup();
            string stateBefore = CaptureUnchangedState();
            PlayRow(0);
            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 1", "b3 (2,2)");

            Click(Control(0, LevelValidationPanel.k_pauseName));

            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True);
            Assert.That(m_updates, Is.Empty, "Pause removes the update callback, so nothing ticks.");
            RunUpdatesAt(m_now + 60.0);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(1), "The frame stays.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 1", "b3 (2,2)");
            Assert.That(m_frames, Has.Count.EqualTo(2));

            Click(Control(0, LevelValidationPanel.k_pauseName));

            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True, "The same control resumes.");
            Assert.That(m_updates, Has.Count.EqualTo(1));
            // The shown frame, an exit's, gets its full time again from the Resume.
            m_frameEnd = m_now;
            AssertStaysUntil(LevelSolutionPlayback.k_exitSeconds, 1);
            AssertCanvasBlocks(canvas, "b1 (1,1)");

            // Nothing changed: the draft, its Undo history, the dirty state and the file.
            Assert.That(JsonUtility.ToJson(m_window.GetDraftLevel()), Is.EqualTo(draftBefore));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(DatabaseJson(), Is.EqualTo(fileBefore));
            Assert.That(m_calls, Is.Empty);
            Assert.That(CaptureUnchangedState(), Is.EqualTo(stateBefore),
                "Pause and Resume changed no selection, tool or kept result either.");
        }

        [Test]
        public void StepShowsOneFrameAtATimeAndAStepOnTheClearedBoardShowsTheDraft()
        {
            VisualElement root = OpenWindow();
            UseDatabase(IceBetweenTwo());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            string draftBefore = JsonUtility.ToJson(m_window.GetDraftLevel());
            string stateBefore = CaptureUnchangedState();
            PlayRow(0);
            Assume.That(m_window.SolutionPlayback.IsPlaying, Is.True);

            Click(Control(0, LevelValidationPanel.k_stepName));

            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True, "Step pauses a playing solution first.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(1), "Then it shows exactly one more frame.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 1", "b3 (2,2)");
            Assert.That(m_window.ValidationPanel.IsPaused, Is.True);
            Assert.That(Control(0, LevelValidationPanel.k_pauseName).text, Is.EqualTo(LevelValidationPanel.k_playGlyph));

            Click(Control(0, LevelValidationPanel.k_stepName));
            AssertCanvasBlocks(canvas, "b1 (1,1)");
            Click(Control(0, LevelValidationPanel.k_stepName));
            AssertCanvasBlocks(canvas, "b1 (1,2)");
            Click(Control(0, LevelValidationPanel.k_stepName));
            Assert.That(canvas.Model.BlockCount, Is.Zero, "The cleared board.");
            Assert.That(m_window.SolutionPlayback.IsActive, Is.True, "It waits on the cleared board.");
            Assert.That(m_frames, Has.Count.EqualTo(5));

            Click(Control(0, LevelValidationPanel.k_stepName));

            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "A Step on the cleared board ends the playback.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(m_frames, Has.Count.EqualTo(5), "No frame follows the cleared board.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 2", "b2 (0,2)", "b3 (2,2)");
            Assert.That(m_window.ValidationPanel.ActiveEntry, Is.EqualTo(-1));
            AssertRowEnd(0, false, "The row shows ▶ again.");
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.True,
                "The preview gets the draft on the next update.");
            Assert.That(JsonUtility.ToJson(m_window.GetDraftLevel()), Is.EqualTo(draftBefore));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(CaptureUnchangedState(), Is.EqualTo(stateBefore),
                "Step changed no draft, Undo, dirty state, selection, tool, kept result, file, write or start.");
        }

        [Test]
        public void AClickOnTheActiveRowChangesNothingAndAControlClickNeverReachesTheRow()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            int rowClicks = 0;
            int rowPointerEvents = 0;
            void CountRowClick(int entry) => rowClicks++;
            EventCallback<PointerDownEvent> countDown = _ => rowPointerEvents++;
            EventCallback<PointerUpEvent> countUp = _ => rowPointerEvents++;
            // Owner: this test. Stop point: the finally block.
            m_window.ValidationPanel.SolutionClicked += CountRowClick;
            try
            {
                PlayRow(0);
                AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
                int frames = m_frames.Count;

                // The active row's body: nothing restarts, stops or pauses.
                rowClicks = 0;
                Click(Row(0));
                Assert.That(rowClicks, Is.EqualTo(1), "The row reported its click.");
                Assert.That(m_window.SolutionPlayback.EntryIndex, Is.Zero);
                Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(1), "It did not start again.");
                Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True);
                Assert.That(m_updates, Has.Count.EqualTo(1));
                Assert.That(m_frames, Has.Count.EqualTo(frames), "No frame was shown again.");

                // A control pressed through the event system acts, and the row never hears it.
                rowClicks = 0;
                Submit(Control(0, LevelValidationPanel.k_pauseName));
                Assert.That(m_window.SolutionPlayback.IsPaused, Is.True, "Pause acted.");
                Assert.That(rowClicks, Is.Zero, "The Pause click did not reach the row.");

                // A pointer press and release on a control never reach the row button around it (the three blocks'
                // playback has four frames, so this Step, whether or not it acts, leaves the solution showing).
                ValidateLayout(root);
                Row(0).RegisterCallback(countDown);
                Row(0).RegisterCallback(countUp);
                Vector2 step = Control(0, LevelValidationPanel.k_stepName).worldBound.center;
                SendPointer(root, EventType.MouseDown, step, 0);
                SendPointer(root, EventType.MouseUp, step, 0);
                Assert.That(rowPointerEvents, Is.Zero, "The row saw no pointer event of the control.");
                Assert.That(rowClicks, Is.Zero);
                Assert.That(m_window.SolutionPlayback.EntryIndex, Is.Zero, "The entry did not change.");

                int frame = m_window.SolutionPlayback.FrameIndex;
                Submit(Control(0, LevelValidationPanel.k_stepName));
                Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(frame + 1), "Step acted.");
                Assert.That(rowClicks, Is.Zero, "The Step click did not reach the row.");

                Submit(Control(0, LevelValidationPanel.k_stopName));
                Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "Stop acted.");
                Assert.That(rowClicks, Is.Zero);
            }
            finally
            {
                m_window.ValidationPanel.SolutionClicked -= CountRowClick;
                Row(0).UnregisterCallback(countDown);
                Row(0).UnregisterCallback(countUp);
            }
        }

        [Test]
        public void AutoPlayOffShowsTheStartBoardPausedUntilStepOrResume()
        {
            VisualElement root = OpenWindow();
            UseDatabase(IceBetweenTwo());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Toggle autoPlay = m_window.ValidationPanel.AutoPlayToggle;
            Assume.That(autoPlay.value, Is.True);

            autoPlay.value = false;

            Assert.That(m_window.AutoPlaysSolutions, Is.False, "The toggle sets the window's choice.");

            PlayRow(0);

            Assert.That(m_window.SolutionPlayback.IsActive, Is.True);
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True, "It waits on the start board.");
            Assert.That(m_updates, Is.Empty, "Nothing ticks.");
            Assert.That(m_frames, Has.Count.EqualTo(1), "The start board shows, in the preview too.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 2", "b2 (0,2)", "b3 (2,2)");
            AssertRowEnd(0, true, "The row shows its controls.");
            Assert.That(Control(0, LevelValidationPanel.k_pauseName).text, Is.EqualTo(LevelValidationPanel.k_playGlyph),
                "Its first control resumes.");
            RunUpdatesAt(m_now + 60.0);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero);

            // Each Step shows one more frame and stays paused.
            Click(Control(0, LevelValidationPanel.k_stepName));
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(1));
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 1", "b3 (2,2)");
            Click(Control(0, LevelValidationPanel.k_stepName));
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(2));
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True);
            Assert.That(m_updates, Is.Empty);

            // Resume plays on from there.
            Click(Control(0, LevelValidationPanel.k_pauseName));
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True);
            m_frameEnd = m_now;
            AssertStaysUntil(LevelSolutionPlayback.k_exitSeconds, 2);
            AssertCanvasBlocks(canvas, "b1 (1,2)");

            // Auto Play on again: a row click plays at once.
            Click(Control(0, LevelValidationPanel.k_stopName));
            autoPlay.value = true;
            Assert.That(m_window.AutoPlaysSolutions, Is.True);
            PlayRow(1);
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True);
            Assert.That(m_updates, Has.Count.EqualTo(1));
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero);

            // The window's own value shows in the toggle.
            m_window.AutoPlaysSolutions = false;
            Assert.That(autoPlay.value, Is.False);
        }

        [Test]
        public void ValidateBeforeSaveOffSavesTestsAndClosesWithoutTheCheck()
        {
            VisualElement root = OpenWindow();
            UseDatabase(NoGate());
            Toggle validateBeforeSave = m_window.ValidationPanel.ValidateBeforeSaveToggle;
            Assume.That(validateBeforeSave.value, Is.True, "It shows the window's flag, which this fixture turns on.");

            validateBeforeSave.value = false;

            Assert.That(m_window.ChecksSolvabilityOnSave, Is.False, "The toggle sets the window's flag.");
            EditTimer(root, 75);
            Click(root.Q<Button>("save-level"));
            Assert.That(m_calls, Is.EqualTo(new[] { "write" }), "Save Level writes without the check.");
            EditTimer(root, 80);
            Click(root.Q<Button>("test-level"));
            Assert.That(m_calls, Is.EqualTo(new[] { "write", "write", "start" }),
                "Test Level saves and starts without it.");
            EditTimer(root, 85);
            m_window.SaveChanges();
            Assert.That(m_calls, Is.EqualTo(new[] { "write", "write", "start", "write" }), "So does save-on-close.");
            Assert.That(m_window.ValidationFlow.RunCount, Is.Zero, "No check ran.");
            Assert.That(m_titles, Is.Empty, "No warning showed.");

            validateBeforeSave.value = true;

            Assert.That(m_window.ChecksSolvabilityOnSave, Is.True);
            EditTimer(root, 90);
            Click(root.Q<Button>("save-level"));
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1), "DEC-144's check runs again.");
            Assert.That(m_titles, Is.EqualTo(new[] { "Level Cannot Be Solved" }), "And its warning.");
            Assert.That(m_calls, Has.Count.EqualTo(4), "Keep Editing writes nothing.");

            // The window's flag shows in the toggle.
            m_window.ChecksSolvabilityOnSave = false;
            Assert.That(validateBeforeSave.value, Is.False);
        }

        [Test]
        public void ADesignerWindowReadsAndStoresBothTogglesAndATestWindowNeverTouchesEditorPrefs()
        {
            string autoPlayKey = LevelDatabaseWindow.k_autoPlayPrefKey;
            string validateKey = LevelDatabaseWindow.k_validateBeforeSavePrefKey;
            // Stored choices unlike a test window's own values: it never reads them.
            EditorPrefs.SetBool(autoPlayKey, false);
            EditorPrefs.SetBool(validateKey, true);
            VisualElement root = OpenWindow(false);
            UseDatabase(ThreeSingles());
            LevelValidationPanel panel = m_window.ValidationPanel;

            Assert.That(m_window.IsDesignerWindow, Is.False);
            Assert.That(m_window.AutoPlaysSolutions, Is.True, "A test window plays at once, as before.");
            Assert.That(m_window.ChecksSolvabilityOnSave, Is.False, "A test window saves without the check, as before.");
            Assert.That(panel.AutoPlayToggle.value, Is.True);
            Assert.That(panel.ValidateBeforeSaveToggle.value, Is.False);

            // Nor does it write them: its changes stay its own.
            EditorPrefs.DeleteKey(autoPlayKey);
            EditorPrefs.DeleteKey(validateKey);
            panel.AutoPlayToggle.value = false;
            panel.ValidateBeforeSaveToggle.value = true;
            panel.AutoPlayToggle.value = true;
            Assert.That(m_window.AutoPlaysSolutions, Is.True);
            Assert.That(m_window.ChecksSolvabilityOnSave, Is.True);
            Assert.That(EditorPrefs.HasKey(autoPlayKey), Is.False, "A test window stores no Auto Play choice.");
            Assert.That(EditorPrefs.HasKey(validateKey), Is.False, "A test window stores no Validate Before Save choice.");

            // Open's designer window starts from the stored choices.
            EditorPrefs.SetBool(autoPlayKey, false);
            EditorPrefs.SetBool(validateKey, false);
            m_window.SetDesignerWindow();

            Assert.That(m_window.IsDesignerWindow, Is.True);
            Assert.That(m_window.AutoPlaysSolutions, Is.False);
            Assert.That(m_window.ChecksSolvabilityOnSave, Is.False);
            Assert.That(panel.AutoPlayToggle.value, Is.False, "The toggles show them.");
            Assert.That(panel.ValidateBeforeSaveToggle.value, Is.False);

            // And stores each change.
            panel.AutoPlayToggle.value = true;
            Assert.That(EditorPrefs.GetBool(autoPlayKey, false), Is.True);
            panel.ValidateBeforeSaveToggle.value = true;
            Assert.That(EditorPrefs.GetBool(validateKey, false), Is.True);
            panel.AutoPlayToggle.value = false;
            Assert.That(EditorPrefs.GetBool(autoPlayKey, true), Is.False);
            Assert.That(m_window.AutoPlaysSolutions, Is.False);
            Assert.That(m_window.ChecksSolvabilityOnSave, Is.True);

            // Code that sets the window's values stores nothing.
            m_window.AutoPlaysSolutions = true;
            Assert.That(EditorPrefs.GetBool(autoPlayKey, true), Is.False);

            // With nothing stored, both are on.
            EditorPrefs.DeleteKey(autoPlayKey);
            EditorPrefs.DeleteKey(validateKey);
            m_window.SetDesignerWindow();
            Assert.That(m_window.AutoPlaysSolutions, Is.True);
            Assert.That(m_window.ChecksSolvabilityOnSave, Is.True);
            Assert.That(EditorPrefs.HasKey(autoPlayKey), Is.False, "Reading stores nothing.");
            Assert.That(root.Q(LevelValidationPanel.k_name), Is.SameAs(panel));
        }

        [Test]
        public void AFinishedCanvasPressInADesignerWindowOpensTheClosedLevelEditorSceneOnce()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome.Opened);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Vector2 b2 = CellCenter(canvas, new Vector2Int(1, 2));

            // A right press removes a block, as it always did.
            SendPointer(root, EventType.MouseDown, b2, 1);

            Assert.That(m_window.GetDraftLevel().BlockCount, Is.EqualTo(2), "The press edits as before.");
            Assert.That(IsSceneOpenScheduled(), Is.False, "Nothing opens while the press goes on.");

            SendPointer(root, EventType.MouseUp, b2, 1);

            Assert.That(IsSceneOpenScheduled(), Is.True, "Its end schedules the scene for the next editor update.");
            Assert.That(m_sceneOpens, Is.Zero, "Not before that update.");
            // A second press before that update schedules nothing more.
            Vector2 b1 = CellCenter(canvas, new Vector2Int(0, 2));
            SendPointer(root, EventType.MouseDown, b1, 1);
            SendPointer(root, EventType.MouseUp, b1, 1);
            Assert.That(m_window.GetDraftLevel().BlockCount, Is.EqualTo(1));

            RunSceneOpenUpdate();

            Assert.That(m_sceneOpens, Is.EqualTo(1), "The scene step ran once.");
            Assert.That(IsSceneOpenScheduled(), Is.False);

            // With the scene loaded, a press opens nothing.
            Vector2 b3 = CellCenter(canvas, new Vector2Int(2, 2));
            SendPointer(root, EventType.MouseDown, b3, 1);
            SendPointer(root, EventType.MouseUp, b3, 1);
            Assert.That(m_window.GetDraftLevel().BlockCount, Is.Zero);
            Assert.That(IsSceneOpenScheduled(), Is.False);
            Assert.That(m_sceneOpens, Is.EqualTo(1));
        }

        [Test]
        public void TheSceneWaitsUntilACanvasDragEnds()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome.Opened);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Vector2 start = CellCenter(canvas, new Vector2Int(0, 2));
            Vector2 end = CellCenter(canvas, new Vector2Int(0, 1));

            // The Select tool drags b1 one cell; the canvas holds the pointer while it drags.
            SendPointer(root, EventType.MouseDown, start, 0);
            SendPointer(root, EventType.MouseDrag, end, 0);

            Assert.That(IsSceneOpenScheduled(), Is.False, "Nothing opens during the drag.");

            SendPointer(root, EventType.MouseUp, end, 0);

            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(0, 1)),
                "The drag moves the block as before.");
            Assert.That(IsSceneOpenScheduled(), Is.True, "The drag's end schedules the scene.");
            Assert.That(m_sceneOpens, Is.Zero);
            RunSceneOpenUpdate();
            Assert.That(m_sceneOpens, Is.EqualTo(1));
        }

        [Test]
        public void ACancelledScenePromptAsksNoMoreUntilTheWindowIsOpenedAgain()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome.Cancelled);
            Vector2 empty = CellCenter(root.Q<LevelCanvasElement>(), new Vector2Int(1, 0));

            SendPointer(root, EventType.MouseDown, empty, 0);
            SendPointer(root, EventType.MouseUp, empty, 0);
            RunSceneOpenUpdate();
            Assert.That(m_sceneOpens, Is.EqualTo(1), "The save prompt was asked and cancelled.");

            SendPointer(root, EventType.MouseDown, empty, 0);
            SendPointer(root, EventType.MouseUp, empty, 0);
            Assert.That(IsSceneOpenScheduled(), Is.False, "Cancel stops the prompts.");

            // Open, the designer entry point, asks again.
            m_window.SetDesignerWindow();
            SendPointer(root, EventType.MouseDown, empty, 0);
            SendPointer(root, EventType.MouseUp, empty, 0);
            Assert.That(IsSceneOpenScheduled(), Is.True);
            RunSceneOpenUpdate();
            Assert.That(m_sceneOpens, Is.EqualTo(2));
        }

        [Test]
        public void ATestWindowAPlaybackStopAndPlayModeOpenNoScene()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles(), IceBetweenTwo());
            ValidateLayout(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Vector2 b3 = CellCenter(canvas, new Vector2Int(2, 2));

            // A window a test creates never switches scenes; its press edits as before.
            SendPointer(root, EventType.MouseDown, b3, 1);
            SendPointer(root, EventType.MouseUp, b3, 1);
            Assert.That(m_window.GetDraftLevel().BlockCount, Is.EqualTo(2));
            Assert.That(IsSceneOpenScheduled(), Is.False, "A test window schedules nothing.");

            // In a designer's window, a press that stops a playback only stops it.
            UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome.Opened);
            Validate(root);
            PlayRow(0);
            string draftBefore = JsonUtility.ToJson(m_window.GetDraftLevel());
            Vector2 b1 = CellCenter(canvas, new Vector2Int(0, 2));
            SendPointer(root, EventType.MouseDown, b1, 1);
            SendPointer(root, EventType.MouseUp, b1, 1);
            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "The press stopped the playback.");
            Assert.That(JsonUtility.ToJson(m_window.GetDraftLevel()), Is.EqualTo(draftBefore), "It edited nothing.");
            Assert.That(IsSceneOpenScheduled(), Is.False, "And opens nothing.");

            // In or entering Play Mode nothing opens either.
            m_window.ValidationFlow.IsPlaying = () => true;
            SendPointer(root, EventType.MouseDown, b1, 1);
            SendPointer(root, EventType.MouseUp, b1, 1);
            Assert.That(IsSceneOpenScheduled(), Is.False, "Play Mode opens no scene.");
            Assert.That(m_sceneOpens, Is.Zero);
        }

        // The paused variants of AnotherRowPlaysItsEntryFromTheStart: a paused entry (Auto Play on) and an entry stepped
        // with Auto Play off give way to the other row's start board, with no draft between them.
        [TestCase(true)]
        [TestCase(false)]
        public void AnotherRowReplacesAPausedEntryWithItsStartBoardAndNoDraftBetween(bool autoPlay)
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            m_window.AutoPlaysSolutions = autoPlay;
            PlayRow(0);
            if (autoPlay)
            {
                AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
                Click(Control(0, LevelValidationPanel.k_pauseName));
            }
            else
            {
                Click(Control(0, LevelValidationPanel.k_stepName));
            }

            Assume.That(m_window.SolutionPlayback.IsPaused, Is.True);
            Assume.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(1));
            Assume.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.False);
            m_frames.Clear();

            PlayRow(1);

            Assert.That(m_window.SolutionPlayback.EntryIndex, Is.EqualTo(1));
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero, "From its start.");
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.EqualTo(!autoPlay),
                autoPlay ? "Auto Play on plays it at once." : "Auto Play off waits on its start board.");
            Assert.That(m_updates, Has.Count.EqualTo(autoPlay ? 1 : 0));
            Assert.That(m_frames, Has.Count.EqualTo(1), "One frame stream: the new start board only.");
            Assert.That(m_frames[0].BlockCount, Is.EqualTo(3));
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.False,
                "No draft publish lands between the two entries.");
            AssertRowEnd(0, false, "The first row shows ▶ again.");
            AssertRowEnd(1, true, "The controls move to the new active row.");
            AssertPauseControl(1, !autoPlay, autoPlay ? "It plays: Pause." : "It waits: Resume.");
        }

        [Test]
        public void ACanvasDragCancelledWhileTheButtonIsHeldSchedulesNothingUntilItsRelease()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome.Opened);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Vector2 start = CellCenter(canvas, new Vector2Int(0, 2));
            Vector2 over = CellCenter(canvas, new Vector2Int(0, 1));

            SendPointer(root, EventType.MouseDown, start, 0);
            SendPointer(root, EventType.MouseDrag, over, 0);
            // Esc cancels the drag while the button is still held; the canvas lets the pointer go, and the next pointer
            // event reports that capture loss.
            SendKey(canvas, KeyCode.Escape);
            SendPointer(root, EventType.MouseDrag, over, 0);

            Assert.That(IsSceneOpenScheduled(), Is.False, "The canvas's own capture loss ends no press.");
            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(0, 2)),
                "Esc moved nothing.");

            SendPointer(root, EventType.MouseUp, over, 0);

            Assert.That(IsSceneOpenScheduled(), Is.True, "The release ends the press.");
            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(0, 2)));
            Assert.That(m_sceneOpens, Is.Zero);
            RunSceneOpenUpdate();
            Assert.That(m_sceneOpens, Is.EqualTo(1));
        }

        [Test]
        public void ACanvasPressReleasedOutsideTheCanvasEndsNoLaterPress()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome.Opened);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Vector2 outside = root.Q<Label>(LevelValidationPanel.k_summaryName).worldBound.center;
            Vector2 b2 = CellCenter(canvas, new Vector2Int(1, 2));

            // A right press removes b2 and is released outside the canvas, which never hears the release.
            SendPointer(root, EventType.MouseDown, b2, 1);
            SendPointer(root, EventType.MouseUp, outside, 1);

            Assert.That(m_window.GetDraftLevel().BlockCount, Is.EqualTo(2), "The press edited as before.");
            Assert.That(IsSceneOpenScheduled(), Is.False, "A release outside the canvas ends nothing.");

            // A later press starts outside the canvas and is released over it: it is no canvas press.
            SendPointer(root, EventType.MouseDown, outside, 0);
            SendPointer(root, EventType.MouseUp, b2, 0);

            Assert.That(IsSceneOpenScheduled(), Is.False, "The earlier canvas press was forgotten.");
            Assert.That(m_sceneOpens, Is.Zero);

            // A press on the canvas, now on b2's empty cell, still opens the scene when it ends.
            SendPointer(root, EventType.MouseDown, b2, 0);
            SendPointer(root, EventType.MouseUp, b2, 0);

            Assert.That(IsSceneOpenScheduled(), Is.True);
        }

        [Test]
        public void OnDisableRemovesAPendingSceneOpening()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome.Opened);
            Vector2 empty = CellCenter(root.Q<LevelCanvasElement>(), new Vector2Int(1, 0));
            SendPointer(root, EventType.MouseDown, empty, 0);
            SendPointer(root, EventType.MouseUp, empty, 0);
            Assume.That(IsSceneOpenScheduled(), Is.True);
            Assert.That(HasSceneOpenUpdate(), Is.True, "The one-shot waits on EditorApplication.update.");

            LevelEditorTestAccess.Invoke(m_window, "OnDisable");

            Assert.That(HasSceneOpenUpdate(), Is.False, "OnDisable removed it from EditorApplication.update.");
            Assert.That(IsSceneOpenScheduled(), Is.False);
            Assert.That(m_sceneOpens, Is.Zero, "It never ran.");
            LevelEditorTestAccess.Invoke(m_window, "OnEnable");
        }

        [Test]
        public void APressOnAToolsButtonStopsTheSolutionFirstAndTheButtonThenActsOnTheDraft()
        {
            // Contract Revision 2, safety stop.
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            Undo.IncrementCurrentGroup();
            EditTimer(root, 75);
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            PlayRow(0);
            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            Assume.That(canvas.Model.BlockCount, Is.EqualTo(2), "A frame shows.");
            Button undo = root.Q<ScrollView>("tools-pane").Q<Button>("undo");
            int pressesOnUndo = 0;
            EventCallback<PointerDownEvent> countPress = _ => pressesOnUndo++;
            // Owner: this test. Stop point: the finally block.
            undo.RegisterCallback(countPress);
            try
            {
                RightPress(undo);
            }
            finally
            {
                undo.UnregisterCallback(countPress);
            }

            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "The press stopped the solution first.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(canvas.Model.BlockCount, Is.EqualTo(3), "The canvas shows the draft again.");
            Assert.That(pressesOnUndo, Is.EqualTo(1), "The press still reached the button.");

            Click(undo);

            Assert.That(m_window.GetDraftLevel().TimerSeconds, Is.EqualTo(60), "Undo acted on the restored draft.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void APressOnATopBarFieldStopsTheSolutionAndTheFieldThenEditsTheDraft()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            PlayRow(0);
            IntegerField timer = root.Q("top-bar").Q<IntegerField>("timer-seconds");
            int pressesOnTimer = 0;
            EventCallback<PointerDownEvent> countPress = _ => pressesOnTimer++;
            // Owner: this test. Stop point: the finally block.
            timer.RegisterCallback(countPress);
            try
            {
                RightPress(timer);
            }
            finally
            {
                timer.UnregisterCallback(countPress);
            }

            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "The press stopped the solution first.");
            Assert.That(pressesOnTimer, Is.EqualTo(1), "The press still reached the field.");

            EditTimer(root, 75);

            Assert.That(m_window.GetDraftLevel().TimerSeconds, Is.EqualTo(75), "The field edited the restored draft.");
            Assert.That(m_window.hasUnsavedChanges, Is.True);
        }

        [Test]
        public void AShortcutKeyStopsTheSolutionFirstAndThenActs()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            PlayRow(0);
            Assume.That(LevelEditorTestAccess.Get<int>(m_window, "m_selectedBlockIndex"), Is.Zero, "b1 is selected.");

            // ↓ is no step key: it stops the solution, then nudges the selected block on the restored draft.
            SendKey(canvas, KeyCode.DownArrow);

            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "The key stopped the solution first.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(0, 1)),
                "The shortcut then nudged b1.");
            Assert.That(m_window.hasUnsavedChanges, Is.True);
        }

        [Test]
        public void ThePanelsOwnElementsAndKeysOnThemKeepTheirRules()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            LevelValidationPanel panel = m_window.ValidationPanel;
            PlayRow(0);

            RightPress(Control(0, LevelValidationPanel.k_pauseName));
            RightPress(Row(1));
            RightPress(panel.NextPageButton);
            RightPress(panel.AutoPlayToggle);
            RightPress(panel.ValidateBeforeSaveToggle);
            SendKey(Row(0), KeyCode.Q);

            Assert.That(m_window.SolutionPlayback.IsActive, Is.True, "A press or key inside the panel is no safety stop.");
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True);
            Assert.That(m_window.SolutionPlayback.EntryIndex, Is.Zero);

            // The pager and the toggles act as before and stop nothing either.
            Click(panel.NextPageButton);
            Click(panel.PreviousPageButton);
            panel.AutoPlayToggle.value = false;
            panel.AutoPlayToggle.value = true;
            Assert.That(m_window.SolutionPlayback.IsActive, Is.True);
            AssertRowEnd(0, true, "Its row keeps the controls.");
        }

        [Test]
        public void APieceEditorChangeOrALibraryCommitStopsTheSolution()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();

            PlayRow(0);
            LevelEditorTestAccess.Invoke(m_window, "OnLibraryCommitted");
            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "A piece library commit stops it.");
            Assert.That(canvas.Model.BlockCount, Is.EqualTo(3));

            PlayRow(0);
            Click(Control(0, LevelValidationPanel.k_pauseName));
            Assume.That(m_window.SolutionPlayback.IsPaused, Is.True);
            LevelEditorTestAccess.Invoke(m_window, "OnLevelPieceChanged",
                new ColorBlockJam.Editor.BoardKit.LevelPieceRequest("another-level-scope", "piece-1",
                    new[] { Vector2Int.zero }));
            Assert.That(m_window.SolutionPlayback.IsActive, Is.False, "A Piece Editor change stops a paused one too.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(canvas.Model.BlockCount, Is.EqualTo(3));
        }

        [Test]
        public void ThePauseControlDrawsTwoBarsWhilePlayingAndReadsResumeWhilePaused()
        {
            // Contract Revision 2: the Pause icon is two drawn bars, not a font glyph.
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            PlayRow(0);
            ValidateLayout(root);

            AssertPauseControl(0, false, "Playing.");
            Button pause = Control(0, LevelValidationPanel.k_pauseName);
            List<VisualElement> bars = pause.Query(className: LevelValidationPanel.k_pauseBarClass).ToList();
            foreach (VisualElement bar in bars)
            {
                Assert.That(bar.layout.width, Is.EqualTo(LevelValidationPanel.k_pauseBarWidth).Within(0.5f));
                Assert.That(bar.layout.height, Is.EqualTo(LevelValidationPanel.k_pauseBarHeight).Within(0.5f));
                Assert.That(pause.worldBound.Contains(bar.worldBound.min), Is.True, "Each bar sits inside the control.");
                Assert.That(pause.worldBound.Contains(bar.worldBound.max - new Vector2(0.01f, 0.01f)), Is.True);
            }

            Assert.That(bars[1].worldBound.xMin, Is.GreaterThan(bars[0].worldBound.xMax), "Two bars side by side.");
            Assert.That(Control(0, LevelValidationPanel.k_stepName).text, Is.EqualTo("▶|"));
            Assert.That(Control(0, LevelValidationPanel.k_stopName).text, Is.EqualTo("■"));

            Click(pause);
            ValidateLayout(root);

            AssertPauseControl(0, true, "Paused, the control reads ▶ and draws no bars.");
            Click(pause);
            AssertPauseControl(0, false, "Resumed.");
        }

        [Test]
        public void TheArrowKeysStepThroughAShownSolutionAndKeepTheirMeaningWithoutOne()
        {
            // Contract Revision 2: → does what Step does, ← shows the previous frame; both are consumed, so the selected
            // block (b1, which could move either way) never nudges while a solution shows.
            VisualElement root = OpenWindow();
            UseDatabase(IceBetweenTwo());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assume.That(LevelEditorTestAccess.Get<int>(m_window, "m_selectedBlockIndex"), Is.Zero, "b1 is selected.");
            Vector2Int origin = new Vector2Int(1, 1);

            // ← on the start board of a playing solution does nothing: it keeps playing and subscribes nothing more.
            PlayRow(1);
            SendKey(canvas, KeyCode.LeftArrow);
            Assert.That(m_window.SolutionPlayback.IsPlaying, Is.True);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero);
            Assert.That(m_updates, Has.Count.EqualTo(1));
            Assert.That(m_frames, Has.Count.EqualTo(1));

            // → pauses and shows the next frame, as the Step control does.
            SendKey(canvas, KeyCode.RightArrow);
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(1));
            Assert.That(m_updates, Is.Empty);
            AssertPauseControl(1, true, "The row shows the paused state.");

            // ← shows the previous frame and stays paused; on the start board it does nothing.
            SendKey(canvas, KeyCode.LeftArrow);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero);
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True);
            Assert.That(m_updates, Is.Empty, "Stepping back never subscribes the update callback.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 2", "b2 (0,2)", "b3 (2,2)");
            SendKey(canvas, KeyCode.LeftArrow);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero);
            Assert.That(m_frames, Has.Count.EqualTo(3), "The start, frame 1 and the start again; nothing more.");
            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(origin), "No key nudged b1.");

            // → through the frames up to the cleared board, where a held → stays (the amended AC-21): it shows no more
            // frame and never ends the playback, so its repeats never reach a nudge.
            for (int frame = 1; frame <= 4; frame++)
            {
                SendKey(canvas, KeyCode.RightArrow);
                Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(frame));
            }

            Assert.That(canvas.Model.BlockCount, Is.Zero, "The cleared board.");
            int frames = m_frames.Count;
            for (int repeat = 0; repeat < 5; repeat++)
            {
                SendKey(canvas, KeyCode.RightArrow);
            }

            Assert.That(m_window.SolutionPlayback.IsActive, Is.True, "→ never ends the playback.");
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(4), "The cleared board stays.");
            Assert.That(canvas.Model.BlockCount, Is.Zero);
            Assert.That(m_frames, Has.Count.EqualTo(frames), "No frame follows the cleared board.");
            Assert.That(m_updates, Is.Empty);
            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(origin), "No key nudged b1.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);

            // The Stop control ends it (as would the Step control, another row or any other key).
            Click(Control(1, LevelValidationPanel.k_stopName));
            Assert.That(m_window.SolutionPlayback.IsActive, Is.False);
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 2", "b2 (0,2)", "b3 (2,2)");
            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(origin));

            // With no solution shown, → nudges the selected block as before.
            SendKey(canvas, KeyCode.RightArrow);
            Assert.That(m_window.GetDraftLevel().GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(m_window.hasUnsavedChanges, Is.True);
        }

        [Test]
        public void TheRightArrowPausesOnAPlayingClearedBoardAndOnlyTheStepControlEndsThere()
        {
            // The amended AC-21: on the cleared board → pauses and shows nothing more; the Step control still ends it.
            VisualElement root = OpenWindow();
            UseDatabase(IceBetweenTwo());
            ValidateLayout(root);
            Validate(root);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            PlayRow(0);
            AssertStaysUntil(LevelSolutionPlayback.k_startSeconds, 0);
            AssertStaysUntil(LevelSolutionPlayback.k_exitSeconds, 1);
            AssertStaysUntil(LevelSolutionPlayback.k_exitSeconds, 2);
            AssertStaysUntil(LevelSolutionPlayback.k_stepSeconds, 3);
            Assume.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(4));
            Assume.That(m_window.SolutionPlayback.IsPlaying, Is.True, "The cleared board holds while it plays.");
            int frames = m_frames.Count;

            SendKey(canvas, KeyCode.RightArrow);

            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True, "→ pauses on the cleared board.");
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(4), "It shows no further frame.");
            Assert.That(m_updates, Is.Empty);
            RunUpdatesAt(m_now + 60.0);
            Assert.That(m_window.SolutionPlayback.IsActive, Is.True, "So the cleared board's time cannot end it either.");
            Assert.That(m_frames, Has.Count.EqualTo(frames));
            AssertPauseControl(0, true, "The row shows the paused state.");

            Click(Control(0, LevelValidationPanel.k_stepName));

            Assert.That(m_window.SolutionPlayback.IsActive, Is.False,
                "The Step control still ends the playback on the cleared board.");
            AssertCanvasBlocks(canvas, "b1 (1,1) ice 2", "b2 (0,2)", "b3 (2,2)");
        }

        [Test]
        public void AnArrowKeyOnAPanelElementStepsTooAndNeverStopsTheSolution()
        {
            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            PlayRow(0);

            SendKey(Control(0, LevelValidationPanel.k_stopName), KeyCode.RightArrow);

            Assert.That(m_window.SolutionPlayback.IsActive, Is.True, "The key stepped; it did not press Stop.");
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(1));
            Assert.That(m_window.SolutionPlayback.IsPaused, Is.True);

            SendKey(Row(0), KeyCode.LeftArrow);

            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.Zero);
            Assert.That(m_window.SolutionPlayback.IsActive, Is.True);
        }

        [Test]
        public void TheControlTooltipsNameTheArrowKeys()
        {
            foreach (string tooltip in new[]
                     {
                         LevelValidationPanel.k_pauseTooltip, LevelValidationPanel.k_resumeTooltip,
                         LevelValidationPanel.k_stepTooltip, LevelValidationPanel.k_stopTooltip
                     })
            {
                Assert.That(tooltip, Does.Contain("←").And.Contain("→"), tooltip);
            }

            VisualElement root = OpenWindow();
            UseDatabase(ThreeSingles());
            ValidateLayout(root);
            Validate(root);
            PlayRow(0);
            Assert.That(Control(0, LevelValidationPanel.k_pauseName).tooltip, Is.EqualTo(LevelValidationPanel.k_pauseTooltip));
            Assert.That(Control(0, LevelValidationPanel.k_stepName).tooltip, Is.EqualTo(LevelValidationPanel.k_stepTooltip));
            Assert.That(Control(0, LevelValidationPanel.k_stopName).tooltip, Is.EqualTo(LevelValidationPanel.k_stopTooltip));
            Click(Control(0, LevelValidationPanel.k_pauseName));
            Assert.That(Control(0, LevelValidationPanel.k_pauseName).tooltip,
                Is.EqualTo(LevelValidationPanel.k_resumeTooltip));
        }

        [Test]
        public void ABackgroundValidateShowsItsProgressAndThenItsAnswer()
        {
            // DEC-175: in a designer's window Validate Level searches on a worker thread. The summary and the status line
            // show its progress, the button reads Cancel, no modal progress bar opens, and the answer arrives through the
            // Editor update poll.
            VisualElement root = OpenWindow();
            UseDatabase(LevelFiveWithSixthBlock());
            ValidateLayout(root);
            UseBackgroundValidation();
            Button validate = root.Q<Button>("validate-level");

            Validate(root);

            Assert.That(m_window.ValidationFlow.IsRunning, Is.True);
            Assert.That(validate.text, Is.EqualTo(LevelDatabaseWindow.k_cancelValidateText));
            Assert.That(validate.enabledSelf, Is.True, "Cancel stays pressable.");
            Assert.That(m_validationUpdates, Has.Count.EqualTo(1));
            Assert.That(m_window.ValidationPanel.Summary, Is.Not.EqualTo(LevelValidationPanel.k_listHint));
            Assert.That(Status(root), Is.EqualTo(m_window.ValidationPanel.Summary),
                "The status line shows the same progress line.");
            Assert.That(Row(0).visible, Is.False, "No list shows while it runs.");
            Assert.That(m_progress, Is.Empty, "No modal progress bar opens.");

            RunValidationUntilAnswered();

            Assert.That(m_window.ValidationPanel.Summary, Does.StartWith("Solvable in "),
                "The draft that read Not confirmed at 250,000 positions is confirmed.");
            Assert.That(Status(root), Does.StartWith("Solvable in "));
            Assert.That(Row(0).visible, Is.True);
            Assert.That(validate.text, Is.EqualTo(LevelDatabaseWindow.k_validateText));
            Assert.That(m_validationUpdates, Is.Empty, "The poll unsubscribes with the answer.");
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1));

            // The kept answer comes back at once, without a run.
            Validate(root);
            Assert.That(m_window.ValidationFlow.IsRunning, Is.False);
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1));
            Assert.That(m_validationUpdates, Is.Empty);
        }

        [Test]
        public void TheCancelButtonStopsABackgroundCheckAndItsAnswerIsNotKept()
        {
            VisualElement root = OpenWindow();
            UseDatabase(LevelFiveWithSixthBlock());
            ValidateLayout(root);
            UseBackgroundValidation();
            Validate(root);

            // The same button reads Cancel now.
            Validate(root);
            RunValidationUntilAnswered();

            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo("Not checked: the check was cancelled."));
            Assert.That(root.Q<Button>("validate-level").text, Is.EqualTo(LevelDatabaseWindow.k_validateText));
            Assert.That(Row(0).visible, Is.False);

            // A cancelled check is not kept, so the next press runs again.
            Validate(root);
            Assert.That(m_window.ValidationFlow.IsRunning, Is.True);
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(2));
        }

        [Test]
        public void AnEditStopsABackgroundRunAndTheHintShowsAgain()
        {
            VisualElement root = OpenWindow();
            UseDatabase(LevelFiveWithSixthBlock());
            ValidateLayout(root);
            UseBackgroundValidation();
            Validate(root);
            Assume.That(m_window.ValidationFlow.IsRunning, Is.True);

            EditTimer(root, 75);

            Assert.That(m_window.ValidationFlow.IsRunning, Is.False, "Its answer would be stale.");
            AssertHint("An edit stops the run.");
            Assert.That(m_validationUpdates, Is.Empty);
            Assert.That(root.Q<Button>("validate-level").text, Is.EqualTo(LevelDatabaseWindow.k_validateText));
            Assert.That(m_window.ValidationFlow.TryFinishRun(), Is.False, "The stopped run never answers.");
        }

        [Test]
        public void SaveLevelWaitsForTheBackgroundRunOfTheSameDraftInsteadOfCheckingAgain()
        {
            VisualElement root = OpenWindow();
            UseDatabase(LevelFiveWithSixthBlock());
            ValidateLayout(root);
            UseBackgroundValidation();
            Validate(root);
            Assume.That(m_window.ValidationFlow.IsRunning, Is.True);

            Click(root.Q<Button>("save-level"));

            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1), "Save Level waited for the run.");
            Assert.That(m_window.ValidationFlow.IsRunning, Is.False);
            Assert.That(m_titles, Is.Empty, "A Solvable level saves without the warning.");
            Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
            Assert.That(m_clearedProgress, Is.EqualTo(1), "The wait's progress bar is cleared once.");
            Assert.That(m_validationUpdates, Is.Empty);
            Assert.That(m_window.ValidationPanel.Summary, Does.StartWith("Solvable in "),
                "The run's answer reached the panel, and the same-content reload kept it.");
        }

        // A designer's window validates in the background (DEC-175); a small listing budget keeps these tests short.
        private void UseBackgroundValidation()
        {
            m_window.ValidationFlow.RunsInBackground = true;
            m_window.ValidationFlow.Options = new LevelCheckOptions { MaxListPositions = 2000 };
        }

        // Runs the fake poll, as the Editor's updates would, until the background run has answered.
        private void RunValidationUntilAnswered()
        {
            System.Diagnostics.Stopwatch waited = System.Diagnostics.Stopwatch.StartNew();
            while (m_validationUpdates.Count > 0)
            {
                Assert.That(waited.Elapsed.TotalSeconds, Is.LessThan(60.0), "The background run never answered.");
                EditorApplication.CallbackFunction[] updates = m_validationUpdates.ToArray();
                for (int index = 0; index < updates.Length; index++)
                {
                    updates[index]();
                }

                System.Threading.Thread.Sleep(10);
            }
        }

        // The shipped level-05 plus the designer's sixth block (a Red 1x2 top left), in the draft's order: solvable in
        // six moves, but the search stores more than 250,000 positions first (DEC-175). ValidationFixtures has the same.
        private static LevelData LevelFiveWithSixthBlock()
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

        // One Red block one cell from the left edge's Red gate: a slide and an exit, one solution of one move.
        private static LevelData OneSlide()
        {
            return new LevelData("p1", "Slide", 4, 4, 60,
                new[] { new LevelDoorData("d1", BoardSide.Left, 1, 1, "Red") },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(1, 1), new[] { Vector2Int.zero }) });
        }

        // A Red block on a board with no gate (PRE-1).
        private static LevelData NoGate()
        {
            return new LevelData("p0", "No gate", 4, 4, 60, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }

        // Red, Orange and Yellow singles under top gates of their colours: all six exit orders, three moves each.
        private static LevelData ThreeSingles()
        {
            return Singles("p3", "Three", "Red", "Orange", "Yellow");
        }

        // Five singles under top gates of their colours: 120 exit orders, so the list is full at 20 and reads 20+.
        private static LevelData FiveSingles()
        {
            return Singles("p5", "Five", "Red", "Orange", "Yellow", "LightGreen", "LightBlue");
        }

        private static LevelData Singles(string id, string name, params string[] colors)
        {
            LevelDoorData[] doors = new LevelDoorData[colors.Length];
            LevelBlockData[] blocks = new LevelBlockData[colors.Length];
            for (int index = 0; index < colors.Length; index++)
            {
                doors[index] = new LevelDoorData("d" + (index + 1), BoardSide.Top, index, 1, colors[index]);
                blocks[index] = new LevelBlockData("b" + (index + 1), colors[index], new Vector2Int(index, 2),
                    new[] { Vector2Int.zero });
            }

            return new LevelData(id, name, Mathf.Max(3, colors.Length), 3, 60, doors, blocks);
        }

        // A Red Ice block (clearsToThaw 2) under its top gate between an Orange and a Yellow single that leave at once:
        // it thaws after both exits, steps up and leaves. Entry #1 is Orange, Yellow, Red; #2 is Yellow, Orange, Red.
        private static LevelData IceBetweenTwo()
        {
            LevelEffectData ice = new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "2") });
            return new LevelData("pi", "Ice", 3, 3, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 0, 1, "Orange"), new LevelDoorData("d2", BoardSide.Top, 1, 1, "Red"),
                    new LevelDoorData("d3", BoardSide.Top, 2, 1, "Yellow")
                },
                new[]
                {
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1), new[] { Vector2Int.zero }, new[] { ice }),
                    new LevelBlockData("b2", "Orange", new Vector2Int(0, 2), new[] { Vector2Int.zero }),
                    new LevelBlockData("b3", "Yellow", new Vector2Int(2, 2), new[] { Vector2Int.zero })
                });
        }

        private void RecordFrame(LevelData frame)
        {
            m_frames.Add(frame);
        }

        private void Validate(VisualElement root)
        {
            Click(root.Q<Button>("validate-level"));
        }

        private Button Row(int slot)
        {
            return m_window.ValidationPanel.GetRow(slot);
        }

        // A row click starts the playback at the fake clock's time, where the first frame's time starts.
        private void PlayRow(int slot)
        {
            m_frameEnd = m_now;
            Click(Row(slot));
        }

        // The shown frame stays until just before its time ends, and when it ends the next frame shows or, after the
        // last one, the playback ends (the caller checks which).
        private void AssertStaysUntil(double seconds, int frame)
        {
            m_frameEnd += seconds;
            RunUpdatesAt(m_frameEnd - 0.01);
            Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(frame),
                $"Frame {frame} still shows just before its {seconds} s end.");
            RunUpdatesAt(m_frameEnd);
            if (m_window.SolutionPlayback.IsPlaying)
            {
                Assert.That(m_window.SolutionPlayback.FrameIndex, Is.EqualTo(frame + 1),
                    $"Frame {frame + 1} shows when frame {frame}'s time ends.");
            }
        }

        // Runs the subscribed update callbacks at a time, as the Editor's next update would.
        private void RunUpdatesAt(double time)
        {
            m_now = time;
            EditorApplication.CallbackFunction[] updates = m_updates.ToArray();
            for (int index = 0; index < updates.Length; index++)
            {
                updates[index]();
            }
        }

        private void AssertHint(string message)
        {
            Assert.That(m_window.ValidationPanel.Summary, Is.EqualTo(LevelValidationPanel.k_listHint), message);
            Assert.That(Row(0).visible, Is.False, message);
            Assert.That(m_window.ValidationPanel.Pager.visible, Is.False, message);
        }

        // The summary reads the answer with all of it in its tooltip and stays two lines high, so the panel keeps its
        // height.
        private static void AssertSummary(VisualElement panel, string text, float panelHeight, string message)
        {
            Label summary = panel.Q<Label>(LevelValidationPanel.k_summaryName);
            Assert.That(summary.text, Is.EqualTo(text), message);
            Assert.That(summary.tooltip, Is.EqualTo(text), message + " The tooltip holds the whole sentence.");
            Assert.That(summary.layout.height,
                Is.EqualTo(LevelValidationPanel.k_summaryLines * LevelValidationPanel.k_summaryLineHeight).Within(0.5f),
                message);
            Assert.That(panel.layout.height, Is.EqualTo(panelHeight).Within(0.5f), message);
        }

        // The height of that many lines of text in the summary's font, one short word a line.
        private static float MeasureLines(Label summary, int lines)
        {
            string text = "Ag";
            for (int line = 1; line < lines; line++)
            {
                text += "\nAg";
            }

            return summary.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f,
                VisualElement.MeasureMode.Undefined).y;
        }

        // The height a sentence needs when it wraps at the summary's width.
        private static float MeasureWrapped(Label summary, string text)
        {
            return summary.MeasureTextSize(text, summary.contentRect.width, VisualElement.MeasureMode.Exactly, 0f,
                VisualElement.MeasureMode.Undefined).y;
        }

        private void AssertAnswerWithoutList(IResolveConstraint summary)
        {
            Assert.That(m_window.ValidationPanel.Summary, summary);
            for (int slot = 0; slot < 5; slot++)
            {
                Assert.That(Row(slot).visible, Is.False);
            }

            Assert.That(m_window.ValidationPanel.Pager.visible, Is.False);
        }

        // Each expected block reads "id (x,y)" with " ice N" for a frozen block, in model order.
        private static void AssertCanvasBlocks(LevelCanvasElement canvas, params string[] expected)
        {
            List<string> blocks = new List<string>();
            for (int index = 0; index < canvas.Model.BlockCount; index++)
            {
                LevelCanvasBlock block = canvas.Model.GetBlock(index);
                Vector2Int cell = block.GetCell(0);
                string text = block.Id + " (" + cell.x + "," + cell.y + ")";
                if (block.TryGetEffect(out IceEffect ice))
                {
                    text += " ice " + ice.ClearsToThaw;
                }

                blocks.Add(text);
            }

            Assert.That(blocks, Is.EqualTo(expected));
        }

        private static void AssertCanvasShows(LevelCanvasElement canvas, LevelData level)
        {
            LevelCanvasModel expected = LevelCanvasModel.Create(level);
            Assert.That(canvas.Model.BlockCount, Is.EqualTo(expected.BlockCount), "The canvas shows the draft.");
            for (int index = 0; index < expected.BlockCount; index++)
            {
                Assert.That(canvas.Model.GetBlock(index).Id, Is.EqualTo(expected.GetBlock(index).Id));
                Assert.That(canvas.Model.GetBlock(index).GetCell(0), Is.EqualTo(expected.GetBlock(index).GetCell(0)));
            }
        }

        private static void AssertButton(VisualElement root, string name, string text, string tooltip)
        {
            Button button = root.Q<Button>(name);
            Assert.That(button.text, Is.EqualTo(text));
            Assert.That(button.tooltip, Is.EqualTo(tooltip));
            Assert.That(button.style.height.value.value, Is.EqualTo(LevelDatabaseWindow.k_bottomButtonHeight));
            Assert.That(button.style.fontSize.value.value, Is.EqualTo(13f));
        }

        // The chip labels a row shows, left to right.
        private static List<string> ShownChips(Button row)
        {
            List<string> labels = new List<string>();
            foreach (Label chip in row.Query<Label>(className: LevelValidationPanel.k_chipClass).ToList())
            {
                if (chip.style.display.value != DisplayStyle.None)
                {
                    labels.Add(chip.text);
                }
            }

            return labels;
        }

        private static bool Ellipsis(Button row)
        {
            Label ellipsis = row.Q<Label>(className: LevelValidationPanel.k_ellipsisClass);
            return ellipsis != null && ellipsis.style.display.value != DisplayStyle.None;
        }

        private static List<string> ChildNames(VisualElement parent)
        {
            List<string> names = new List<string>();
            foreach (VisualElement child in parent.Children())
            {
                names.Add(child.name);
            }

            return names;
        }

        // The fixed pane takes this width, as a splitter drag would give it.
        private static void SetToolsWidth(VisualElement root, float width)
        {
            ValidateLayout(root);
            root.Q("tools-column").style.width = width;
            ValidateLayout(root);
        }

        private static void CollectOverflows(VisualElement parent, int depth, List<string> overflows)
        {
            foreach (VisualElement child in parent.hierarchy.Children())
            {
                if (child.layout.xMax > parent.layout.width + 0.5f)
                {
                    overflows.Add($"{child.GetType().Name} '{child.name}' ends at {child.layout.xMax:0} inside " +
                                  $"'{parent.name}' of width {parent.layout.width:0}");
                }

                if (depth > 1)
                {
                    CollectOverflows(child, depth - 1, overflows);
                }
            }
        }

        private static void EditTimer(VisualElement root, int seconds)
        {
            root.Q<IntegerField>("timer-seconds").value = seconds;
            Assume.That(root.Q<Button>("save-level").enabledSelf, Is.True);
        }

        private static string Status(VisualElement root)
        {
            return root.Q<Label>("status").text;
        }

        // The fixture database's levels as the level JSON they would be written as; equal text means an unchanged file.
        private string DatabaseJson()
        {
            Assert.That(LevelDatabaseJson.TryWrite(LevelSaveController.CopyLevels(LevelEditorTestAccess.GetLevels(m_window)), out string json,
                out string error), Is.True, error);
            return json;
        }

        // Every seam is a fake. ChecksSolvabilityOnSave stands in for Open, the designer entry point that turns on
        // Save Level's solvability check, unless a test needs a window's own start values; the playback runs on the
        // fixture's clock and update list; Stop Test and the LevelEditor scene step only record (the scene step never
        // runs in a window a test creates unless the test makes it a designer's window).
        private VisualElement OpenWindow(bool checksSolvability = true)
        {
            m_window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            if (checksSolvability)
            {
                m_window.ChecksSolvabilityOnSave = true;
            }

            m_window.StopTestPlay = () => m_calls.Add("stop");
            m_window.IsLevelEditorSceneLoaded = () => m_sceneLoaded;
            m_window.OpenLevelEditorScene = () =>
            {
                m_sceneOpens++;
                return LevelEditorWorkspace.SceneOutcome.Cancelled;
            };
            m_window.ShowEffectEditor = _ => { };
            m_window.IsPieceEditorOpen = () => false;
            m_window.ShowLevelPieceInEditor = _ => { };
            m_window.ShowLibraryPieceInEditor = _ => { };
            m_window.CommitTemplatesToLibrary = _ => "No template is committed in this test.";
            m_window.WriteLevels = levels =>
            {
                m_calls.Add("write");
                if (m_writeFails || !LevelDatabaseJson.TryWrite(levels, out string json, out _))
                {
                    return false;
                }

                LevelEditorTestAccess.LoadLevels(m_window, json);
                return true;
            };
            m_window.StartTestPlay = request =>
            {
                m_calls.Add("start");
                return null;
            };
            m_window.ConfirmUnsolvableSave = (title, message) =>
            {
                m_titles.Add(title);
                return false;
            };
            m_window.ValidationFlow.IsPlaying = () => false;
            m_window.ValidationFlow.ShowProgress = (info, fraction) =>
            {
                m_progress.Add(info);
                return info.StartsWith(LevelValidationFlow.k_listingProgressPrefix) ? m_cancelListing : m_cancelCheck;
            };
            m_window.ValidationFlow.ClearProgress = () => m_clearedProgress++;
            m_window.SolutionPlayback.Clock = () => m_now;
            m_window.SolutionPlayback.AddUpdate = m_updates.Add;
            m_window.SolutionPlayback.RemoveUpdate = callback => m_updates.Remove(callback);
            m_window.AddValidationUpdate = m_validationUpdates.Add;
            m_window.RemoveValidationUpdate = callback => m_validationUpdates.Remove(callback);
            m_window.ShowUtility();
            m_window.position = new Rect(40f, 40f, 960f, 640f);
            // Tests call CreateGUI directly, as the other editor window tests do.
            LevelEditorTestAccess.Invoke(m_window, "CreateGUI");
            return m_window.rootVisualElement;
        }

        // An in-memory level JSON under a fixture GUID stands in for the project's, with its first level open as saved.
        private void UseDatabase(params LevelData[] levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error), Is.True, error);
            LevelEditorTestAccess.DestroyProjectScope(m_projectScope);

            // The stand-in installer names the fixture JSON; the window's own catalog holds its levels (DEC-192, DEC-194).
            m_projectScope = LevelEditorTestAccess.CreateProjectScope(k_guid);
            LevelEditorTestAccess.Set(m_window, "m_projectScope", m_projectScope);
            LevelEditorTestAccess.LoadLevels(m_window, json);
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", k_guid);
            LevelEditorTestAccess.Set(m_window, "m_sourceValid", true);
            LevelEditorTestAccess.SetLoadedSource(m_window, json);
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 0);
            LevelDraft draft = LevelEditorTestAccess.Get<LevelDraft>(m_window, "m_draft");
            draft.Replace(levels[0]);
            LevelEditorTestAccess.Set(m_window, "m_savedDraftJson", JsonUtility.ToJson(draft.Snapshot()));
            LevelEditorTestAccess.Invoke(m_window, "OnDraftChanged");
            LevelEditorTestAccess.Invoke(m_window, "PublishDraft");
            Assume.That(m_window.hasUnsavedChanges, Is.False, "The fixture opens as saved.");
        }

        private static void Click(Button button)
        {
            // Runs the registered click action without relying on OS pointer input.
            typeof(Clickable).GetMethod("SimulateSingleClick", k_members)
                .Invoke(button.clickable, new object[] { null, 0 });
        }

        private static void SendPointer(VisualElement root, EventType type, Vector2 position, int button)
        {
            Event mouse = new Event { type = type, mousePosition = position, button = button, clickCount = 1 };
            EventBase pointer = type == EventType.MouseDown ? PointerDownEvent.GetPooled(mouse)
                : type == EventType.MouseUp ? PointerUpEvent.GetPooled(mouse)
                : (EventBase)PointerMoveEvent.GetPooled(mouse);
            using (pointer)
            {
                root.SendEvent(pointer);
            }
        }

        // A button press through the event system, as a keyboard submit sends it: the event goes to the button and would
        // bubble from there.
        private static void Submit(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        // One of a row's playback controls (k_pauseName, k_stepName or k_stopName).
        private Button Control(int slot, string controlName)
        {
            Button control = Row(slot).Q<Button>(controlName);
            Assert.That(control, Is.Not.Null, controlName);
            return control;
        }

        // A row ends with ▶, or, while its entry shows, with Pause (Resume while paused), Step and Stop in its place. A
        // slot without an entry shows neither control.
        private void AssertRowEnd(int slot, bool active, string message)
        {
            Button row = Row(slot);
            Assert.That(row.Q(LevelValidationPanel.k_controlsName).style.display.value,
                Is.EqualTo(active ? DisplayStyle.Flex : DisplayStyle.None), message);
            Label glyph = row.Q<Label>(LevelValidationPanel.k_glyphName);
            Assert.That(glyph.style.display.value, Is.EqualTo(active ? DisplayStyle.None : DisplayStyle.Flex), message);
            if (!active && row.visible)
            {
                Assert.That(glyph.text, Is.EqualTo(LevelValidationPanel.k_playGlyph), message);
            }
        }

        // The active row's first control (contract Revision 2): two drawn bars and no text glyph while its solution plays,
        // ▶ and no bars while it is paused, each with its tooltip.
        private void AssertPauseControl(int slot, bool paused, string message)
        {
            Button pause = Control(slot, LevelValidationPanel.k_pauseName);
            VisualElement icon = pause.Q(LevelValidationPanel.k_pauseIconName);
            Assert.That(icon, Is.Not.Null, message);
            Assert.That(icon.Query(className: LevelValidationPanel.k_pauseBarClass).ToList(), Has.Count.EqualTo(2), message);
            Assert.That(icon.style.display.value, Is.EqualTo(paused ? DisplayStyle.None : DisplayStyle.Flex), message);
            Assert.That(pause.text, Is.EqualTo(paused ? LevelValidationPanel.k_playGlyph : string.Empty), message);
            Assert.That(pause.tooltip,
                Is.EqualTo(paused ? LevelValidationPanel.k_resumeTooltip : LevelValidationPanel.k_pauseTooltip), message);
        }

        private bool IsSceneOpenScheduled()
        {
            return LevelEditorTestAccess.Get<bool>(m_window, "m_sceneOpenScheduled");
        }

        // Whether EditorApplication.update holds this window's one-shot scene opening, read from the field's own
        // invocation list.
        private bool HasSceneOpenUpdate()
        {
            System.Delegate update = (System.Delegate)typeof(EditorApplication)
                .GetField(nameof(EditorApplication.update), BindingFlags.Static | BindingFlags.Public).GetValue(null);
            if (update == null)
            {
                return false;
            }

            foreach (System.Delegate callback in update.GetInvocationList())
            {
                if (callback.Method.Name == "OpenSceneAfterPress" && ReferenceEquals(callback.Target, m_window))
                {
                    return true;
                }
            }

            return false;
        }

        // What a playback control may not change (AC-14), one line per value: the draft, its Undo group, the dirty state,
        // the selection, the tool, the kept result's search counts, the level file and the fixture's writes and starts.
        private string CaptureUnchangedState()
        {
            LevelCanvasSelection selection = LevelEditorTestAccess.Get<LevelCanvasSelection>(m_window, "m_canvasSelection");
            return string.Join("\n", new[]
            {
                "draft " + JsonUtility.ToJson(m_window.GetDraftLevel()),
                "undo " + Undo.GetCurrentGroup() + " " + Undo.GetCurrentGroupName(),
                "dirty " + m_window.hasUnsavedChanges,
                "selection " + selection.Kind + " " + selection.Index + " " + selection.Id,
                "selected block " + LevelEditorTestAccess.Get<int>(m_window, "m_selectedBlockIndex"),
                "tool " + LevelEditorTestAccess.Get<LevelEditorToolState>(m_window, "m_toolState").Tool,
                "runs " + m_window.ValidationFlow.RunCount,
                "listings " + m_window.ValidationFlow.ListRunCount,
                "file " + DatabaseJson(),
                "calls " + string.Join(",", m_calls)
            });
        }

        // A right-button press and release sent to one element along its whole path from the window root, as the pointer
        // would reach it. The right button starts no button's click, so the element's own action runs only where a test
        // asks for it; a probe on the element shows whether the press still reached it.
        private static void RightPress(VisualElement target)
        {
            Vector2 position = target.worldBound.center;
            foreach (EventType type in new[] { EventType.MouseDown, EventType.MouseUp })
            {
                Event mouse = new Event { type = type, mousePosition = position, button = 1, clickCount = 1 };
                EventBase pointer = type == EventType.MouseDown
                    ? PointerDownEvent.GetPooled(mouse)
                    : (EventBase)PointerUpEvent.GetPooled(mouse);
                using (pointer)
                {
                    pointer.target = target;
                    target.SendEvent(pointer);
                }
            }
        }

        // Sends a key press to one element after focusing it: UI Toolkit routes keyboard events to the focused element,
        // and the window's shortcut handler hears them on their way down.
        private static void SendKey(VisualElement target, KeyCode key)
        {
            target.Focus();
            using (KeyDownEvent down = KeyDownEvent.GetPooled('\0', key, EventModifiers.None))
            {
                down.target = target;
                target.SendEvent(down);
            }
        }

        // The window's one-shot editor update after a canvas press, run by hand as the Editor's next update would.
        private void RunSceneOpenUpdate()
        {
            LevelEditorTestAccess.Invoke(m_window, "OpenSceneAfterPress");
        }

        // Makes the fixture's window a designer's, as Open does, with a fake LevelEditor scene step: it counts each run,
        // answers with the given outcome, and an opened scene counts as loaded. Auto Play is set on, whatever the stored
        // choice, so a row click plays at once.
        private void UseDesignerSceneStep(LevelEditorWorkspace.SceneOutcome outcome)
        {
            m_sceneLoaded = false;
            m_window.OpenLevelEditorScene = () =>
            {
                m_sceneOpens++;
                m_sceneLoaded = outcome == LevelEditorWorkspace.SceneOutcome.Opened;
                return outcome;
            };
            m_window.SetDesignerWindow();
            m_window.AutoPlaysSolutions = true;
            Assume.That(m_window.IsDesignerWindow, Is.True);
        }

        private static Vector2 CellCenter(LevelCanvasElement canvas, Vector2Int cell)
        {
            return canvas.LocalToWorld(canvas.Geometry.GetCellRect(cell).center);
        }

        private static void RestorePref(string key, bool had, bool value)
        {
            if (had)
            {
                EditorPrefs.SetBool(key, value);
            }
            else
            {
                EditorPrefs.DeleteKey(key);
            }
        }

        private static void ValidateLayout(VisualElement root)
        {
            object panel = root.panel;
            panel.GetType().GetMethod("ValidateLayout", k_members).Invoke(panel, null);
        }

        // Puts back the snapshot a test window replaced, so an open LevelEditor scene preview shows the user's level again.
        private static void RestorePublishedLevel(LevelData previous)
        {
            typeof(LevelDatabaseWindow).GetProperty(nameof(LevelDatabaseWindow.LastPublishedLevel)).SetValue(null, previous);
            System.Action<LevelData> listeners = (System.Action<LevelData>)typeof(LevelDatabaseWindow)
                .GetField(nameof(LevelDatabaseWindow.DraftLevelPublished), BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            listeners?.Invoke(previous);
        }
    }
}
