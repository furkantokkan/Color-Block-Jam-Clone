using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor workspace launcher and the Effect Editor (TASK-20260925-100308276-f4a07e8a, redesigned
    /// as a title plus Inspector-like effect cards by TASK-20260926-170647550-7683988f): the launcher's scene and window
    /// steps through replaced calls, and an Effect Editor window that follows a test Level Editor window's selection and
    /// edits its draft through the command path. Every window is created by the test; no scene opens, no user window
    /// changes and nothing is saved.
    /// </summary>
    public sealed class LevelEffectEditorTests
    {
        private const double k_waitSeconds = 5.0;
        private const BindingFlags k_internalMembers = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string k_otherScene = "Assets/ColorBlockJam/Scenes/Game.unity";

        // The field sections the redesign removed (user request of 2026-09-26): none of them may come back.
        private static readonly string[] s_removedControls =
        {
            "block-section", "block-properties", "block-id", "block-colors", "block-piece-type", "block-origin-x",
            "block-origin-y", "block-nudge", "block-transform", "door-section", "door-id", "door-side", "door-start",
            "door-length", "door-colors", "object-section", "object-id", "object-type", "object-cells"
        };

        private LevelDatabaseWindow m_levelWindow;
        private LevelEffectEditorWindow m_effectWindow;
        private LevelData m_previousPublished;
        private readonly List<LevelDatabaseWindow> m_shownEffectEditors = new List<LevelDatabaseWindow>();

        [SetUp]
        public void RememberPublishedLevel()
        {
            m_previousPublished = LevelDatabaseWindow.LastPublishedLevel;
            m_shownEffectEditors.Clear();
        }

        [TearDown]
        public void CloseWindows()
        {
            if (m_effectWindow != null)
            {
                m_effectWindow.Close();
                m_effectWindow = null;
            }

            if (m_levelWindow != null)
            {
                // A test never saves, so drop any pending edit instead of opening the Save/Discard prompt.
                m_levelWindow.DiscardChanges();
                m_levelWindow.Close();
                m_levelWindow = null;
            }

            typeof(LevelDatabaseWindow).GetProperty(nameof(LevelDatabaseWindow.LastPublishedLevel))
                .SetValue(null, m_previousPublished);
            Action<LevelData> listeners = (Action<LevelData>)typeof(LevelDatabaseWindow)
                .GetField(nameof(LevelDatabaseWindow.DraftLevelPublished), BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            listeners?.Invoke(m_previousPublished);
        }

        [Test]
        public void TheLauncherOwnsTheLevelEditorMenuItem()
        {
            MenuItem launcher = typeof(LevelEditorWorkspace).GetMethod(nameof(LevelEditorWorkspace.Open), Type.EmptyTypes)
                ?.GetCustomAttribute<MenuItem>();
            Assert.That(launcher?.menuItem, Is.EqualTo("Color Block Jam/Level Editor"));
            Assert.That(typeof(LevelDatabaseWindow).GetMethod(nameof(LevelDatabaseWindow.Open))
                .GetCustomAttribute<MenuItem>(), Is.Null, "The window's Open is no second menu item.");
        }

        [Test]
        public void CancelAtTheSavePromptKeepsEverything()
        {
            List<string> calls = new List<string>();
            LevelEditorWorkspace.SceneOutcome outcome = LevelEditorWorkspace.Open(CreateSteps(calls, k_otherScene, false,
                false));

            Assert.That(outcome, Is.EqualTo(LevelEditorWorkspace.SceneOutcome.Cancelled));
            Assert.That(calls, Is.EqualTo(new[] { "save" }), "No scene and no window after Cancel.");
        }

        [Test]
        public void CancelAtTheUnsavedWindowsPromptKeepsEverything()
        {
            List<string> calls = new List<string>();
            LevelEditorWorkspace.SceneOutcome outcome = LevelEditorWorkspace.Open(CreateSteps(calls, k_otherScene, false,
                true, false));

            Assert.That(outcome, Is.EqualTo(LevelEditorWorkspace.SceneOutcome.Cancelled));
            Assert.That(calls, Is.EqualTo(new[] { "save", "windows" }), "No scene, no layout and no window after Cancel.");
        }

        [Test]
        public void TheLauncherOpensTheSceneSingleThenTheSavedLayout()
        {
            List<string> calls = new List<string>();
            LevelEditorWorkspace.SceneOutcome outcome = LevelEditorWorkspace.Open(CreateSteps(calls, k_otherScene, false,
                true));

            Assert.That(outcome, Is.EqualTo(LevelEditorWorkspace.SceneOutcome.Opened));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "save", "windows", "scene " + LevelEditorWorkspace.k_scenePath, "layout " + LevelEditorWorkspace.k_layoutPath,
                "forget", "level"
            }), "The layout brings every window, then the state it restored is dropped; the Level Editor opens through " +
                "Open last, so it keeps the focus.");
        }

        [Test]
        public void ASecondRunChangesNoSceneAndLaysTheEditorOutAgain()
        {
            List<string> calls = new List<string>();
            LevelEditorWorkspace.SceneOutcome outcome = LevelEditorWorkspace.Open(CreateSteps(calls,
                LevelEditorWorkspace.k_scenePath, false, true));

            Assert.That(outcome, Is.EqualTo(LevelEditorWorkspace.SceneOutcome.AlreadyActive));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "windows", "layout " + LevelEditorWorkspace.k_layoutPath, "forget", "level"
            }), "No scene save prompt either.");
        }

        [Test]
        public void PlayModeRefusesOnlyTheSceneStepWithAMessage()
        {
            List<string> calls = new List<string>();
            LevelEditorWorkspace.SceneOutcome outcome = LevelEditorWorkspace.Open(CreateSteps(calls, k_otherScene, true,
                true));

            Assert.That(outcome, Is.EqualTo(LevelEditorWorkspace.SceneOutcome.RefusedInPlayMode));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "windows", "layout " + LevelEditorWorkspace.k_layoutPath, "forget", "level",
                "message " + LevelEditorWorkspace.k_playModeMessage
            }));
        }

        [Test]
        public void AMissingLayoutOpensTheLevelEditorWithAMessage()
        {
            List<string> calls = new List<string>();
            LevelEditorWorkspace.SceneOutcome outcome = LevelEditorWorkspace.Open(CreateSteps(calls,
                LevelEditorWorkspace.k_scenePath, false, true, true, false));

            Assert.That(outcome, Is.EqualTo(LevelEditorWorkspace.SceneOutcome.AlreadyActive));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "windows", "layout " + LevelEditorWorkspace.k_layoutPath, "level",
                "message " + LevelEditorWorkspace.k_missingLayoutMessage
            }), "No layout loaded, so no restored state is dropped.");
        }

        [Test]
        public void TheSceneStepAloneAsksTheSavePromptThenOpensTheSceneAndNothingElse()
        {
            // The step a designer's Level Editor runs after a canvas press (TASK-20260928-091128541-cb48c84a, contract
            // Revision 1): the launcher's own scene step, without its window prompt, layout or window.
            List<string> calls = new List<string>();
            LevelEditorWorkspace.SceneOutcome outcome = LevelEditorWorkspace.OpenScene(CreateSteps(calls, k_otherScene,
                false, true));

            Assert.That(outcome, Is.EqualTo(LevelEditorWorkspace.SceneOutcome.Opened));
            Assert.That(calls, Is.EqualTo(new[] { "save", "scene " + LevelEditorWorkspace.k_scenePath }));
        }

        [Test]
        public void TheSceneStepAloneStopsAtCancelAndLeavesAnActiveOrPlayingSceneAlone()
        {
            List<string> calls = new List<string>();
            Assert.That(LevelEditorWorkspace.OpenScene(CreateSteps(calls, k_otherScene, false, false)),
                Is.EqualTo(LevelEditorWorkspace.SceneOutcome.Cancelled));
            Assert.That(calls, Is.EqualTo(new[] { "save" }), "Cancel at the save prompt opens nothing.");

            calls.Clear();
            Assert.That(LevelEditorWorkspace.OpenScene(CreateSteps(calls, LevelEditorWorkspace.k_scenePath, false, true)),
                Is.EqualTo(LevelEditorWorkspace.SceneOutcome.AlreadyActive));
            Assert.That(calls, Is.Empty, "An active LevelEditor scene asks nothing.");

            calls.Clear();
            Assert.That(LevelEditorWorkspace.OpenScene(CreateSteps(calls, k_otherScene, true, true)),
                Is.EqualTo(LevelEditorWorkspace.SceneOutcome.RefusedInPlayMode));
            Assert.That(calls, Is.Empty, "In Play Mode it asks, opens and shows nothing.");
        }

        [Test]
        public void TheProjectHoldsTheSavedLevelEditorLayout()
        {
            string layout = System.IO.Path.GetFullPath(LevelEditorWorkspace.k_layoutPath);
            Assert.That(System.IO.File.Exists(layout), Is.True, "The launcher loads the layout saved in the project folder.");
            string text = System.IO.File.ReadAllText(layout);
            foreach (string window in new[] { "LevelDatabaseWindow", "PieceEditorWindow", "LevelEffectEditorWindow" })
            {
                Assert.That(text, Does.Contain("m_Name: " + window), $"The layout holds the {window}.");
            }
        }

        [Test]
        public void TitlesNameTheColourAndTheCanvasLabel()
        {
            // User request of 2026-09-27: "Editing Green B2"; multi-word colours read as words.
            Assert.That(LevelEffectEditorWindow.GetBlockTitle(new LevelBlockData("b12", "DarkGreen",
                Vector2Int.zero, new[] { Vector2Int.zero })), Is.EqualTo("Editing Dark Green B12"));
            Assert.That(LevelEffectEditorWindow.GetGateTitle(new LevelDoorData("d4", BoardSide.Left, 0, 1, "LightBlue")),
                Is.EqualTo("Editing Light Blue Gate D4"));
            Assert.That(LevelEffectEditorWindow.GetObjectTitle(new LevelCellObjectData("barrier-3",
                new[] { Vector2Int.zero }, LevelEffectCommandsTests.Effect("barrier"))), Is.EqualTo("Editing Barrier 3"));
            Assert.That(LevelEffectEditorWindow.GetObjectTitle(new LevelCellObjectData("gate-lock",
                new[] { Vector2Int.zero }, LevelEffectCommandsTests.Effect("barrier"))), Is.EqualTo("Editing Barrier GATE-LOCK"),
                "An id not named after its type keeps the canvas form.");
        }

        [Test]
        public void TheColorBlockJamMenuKeepsOnlyTheLevelEditorItsSceneAndPlayTheGameAtTheTop()
        {
            // User request of 2026-09-28: the saved layout brings the Piece and Effect Editors, so they are no menu items.
            // Play The Game (PlayTheGameLauncher) joined them later as the README's way to run the game.
            SortedSet<string> topItems = new SortedSet<string>(StringComparer.Ordinal);
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                foreach (MenuItem item in method.GetCustomAttributes<MenuItem>())
                {
                    const string k_menu = "Color Block Jam/";
                    if (item.menuItem.StartsWith(k_menu, StringComparison.Ordinal)
                        && item.menuItem.IndexOf('/', k_menu.Length) < 0)
                    {
                        topItems.Add(item.menuItem);
                    }
                }
            }

            Assert.That(topItems, Is.EqualTo(new[]
            {
                "Color Block Jam/Level Editor", "Color Block Jam/Open Level Editor Scene", "Color Block Jam/Play The Game"
            }), "Only the Art, Data and Levels submenus sit beside them.");
        }

        [UnityTest]
        public IEnumerator TheEffectEditorFollowsTheCanvasSelectionWithATitleAndCards()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;

            // The Level Editor selects its first block by default, and the canvas outlines it.
            AssertTitle(root, "Editing Red B1");
            foreach (string removed in s_removedControls)
            {
                Assert.That(root.Q(removed), Is.Null, $"{removed} is gone: only a title and effects show.");
            }

            Assert.That(root.Q<Label>("effects-empty"), Is.Not.Null, "b1 has no effect yet.");
            Assert.That(root.Q<Button>("effect-add").enabledSelf, Is.True);

            Select(LevelCanvasSelection.ForDoor(0, "d1"));
            AssertTitle(root, "Editing Red Gate D1");
            Assert.That(root.Q<Label>("effects-note").text, Is.EqualTo(LevelEffectEditorWindow.k_gateEffectsText));
            Assert.That(root.Q<Label>("effects-empty"), Is.Null, "A gate without effects shows only the scope note.");
            Assert.That(root.Q("effect-add").style.display.value, Is.EqualTo(DisplayStyle.None), "No gate effect to add.");

            Select(LevelCanvasSelection.ForObstacle(0, "barrier-1"));
            AssertTitle(root, "Editing Barrier 1");
            Assert.That(root.Q<LevelEffectCard>("effect-0").Title, Is.EqualTo("Barrier"), "Its one effect card shows.");
            Assert.That(root.Q<Button>("effect-0-remove"), Is.Null, "A board object keeps its one effect.");
            Assert.That(root.Q("effect-add").style.display.value, Is.EqualTo(DisplayStyle.None));

            Select(LevelCanvasSelection.ForBlock(1, "b2"));
            AssertTitle(root, "Editing Blue B2");
            Assert.That(root.Q<LevelEffectCard>("effect-0").Title, Is.EqualTo("Ice"));
            Assert.That(root.Q<IntegerField>("effect-0-clearsToThaw").value, Is.EqualTo(2));

            // A level without blocks, gates or board objects leaves nothing selected.
            LoadFixture(new LevelData("L2", "Empty", 3, 3, 60, Array.Empty<LevelDoorData>(), Array.Empty<LevelBlockData>()));
            AssertTitle(root, null);
            Assert.That(root.Q<Label>("effect-editor-message").text, Is.EqualTo(LevelEffectEditorWindow.k_selectHint));
        }

        [UnityTest]
        public IEnumerator AddEffectListsTheRegistryBlockEffectsAndDisablesTheOnesTheBlockHas()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            Select(LevelCanvasSelection.ForBlock(0, "b1"));

            Button add = root.Q<Button>("effect-add");
            Assert.That(add.text, Is.EqualTo(LevelEffectEditorWindow.k_addEffectText));
            Assert.That(add.style.alignSelf.value, Is.EqualTo(Align.Center), "Wide and centred, like Add Component.");
            // Arrow and Ice may share a block here, so both stay listed (Effect Constraints, DEC-177).
            EffectStackingRules rules = ScriptableObject.CreateInstance<EffectStackingRules>();
            rules.SetCanStack(LevelEffectTarget.Block, "arrow", "ice", true);
            m_effectWindow.StackingRules = rules;
            try
            {
                Select(LevelCanvasSelection.ForBlock(0, "b1"));
                AssertChoices(("arrow", true), ("ice", true));

                m_effectWindow.AddEffect("arrow");
                Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(0).Type, Is.EqualTo("arrow"));
                Assert.That(root.Q<LevelEffectCard>("effect-0").IsExpanded, Is.True, "A new card starts expanded.");
                AssertChoices(("arrow", false), ("ice", true));

                m_effectWindow.AddEffect("ice");
                AssertChoices(("arrow", false), ("ice", false));
                Assert.That(add.enabledSelf, Is.False, "Every effect a block can take is on it.");

                Select(LevelCanvasSelection.ForDoor(0, "d1"));
                Assert.That(m_effectWindow.AddEffectChoices, Is.Empty, "Gates take no new effect.");

                Select(LevelCanvasSelection.ForBlock(1, "b2"));
                Assert.That(m_effectWindow.AddEffectChoices, Is.Not.Empty);
                // An empty-space click clears the selection (a None selection alone gets the automatic default block).
                LevelEditorTestAccess.Invoke(m_levelWindow, "ClearSelectionOnEmptySpace", null);
                Assert.That(root.Q<Label>("effect-editor-title").style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(m_effectWindow.AddEffectChoices, Is.Empty, "Nothing shown, nothing to add.");
            }
            finally
            {
                m_effectWindow.StackingRules = null;
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [UnityTest]
        public IEnumerator AddEffectHidesAnEffectThatCannotStackWithTheBlocksEffects()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            // No pair is allowed: every block takes at most one effect (the Effect Constraints default).
            EffectStackingRules rules = ScriptableObject.CreateInstance<EffectStackingRules>();
            m_effectWindow.StackingRules = rules;
            try
            {
                Select(LevelCanvasSelection.ForBlock(0, "b1"));
                AssertChoices(("arrow", true), ("ice", true));

                m_effectWindow.AddEffect("arrow");
                AssertChoices(("arrow", false));
                Assert.That(root.Q<Button>("effect-add").enabledSelf, Is.False, "Ice cannot join Arrow, so nothing is left.");
            }
            finally
            {
                m_effectWindow.StackingRules = null;
                UnityEngine.Object.DestroyImmediate(rules);
            }
        }

        [UnityTest]
        public IEnumerator ACollapsedCardStaysCollapsedThroughEditsAndSelectionChanges()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            Select(LevelCanvasSelection.ForBlock(1, "b2"));
            Assert.That(root.Q<LevelEffectCard>("effect-0").IsExpanded, Is.True);

            root.Q<LevelEffectCard>("effect-0").IsExpanded = false;
            Assert.That(root.Q<Foldout>("effect-0" + LevelEffectCard.k_foldoutSuffix).contentContainer.style.display.value,
                Is.EqualTo(DisplayStyle.None), "Folding hides the card's rows.");

            // An edit rebuilds the cards; the folded one stays folded, and a field change inside it never unfolds it.
            root.Q<IntegerField>("effect-0-clearsToThaw").value = 3;
            Assert.That(GetDraft().GetBlockAt(1).GetEffectAt(0).FindValue("clearsToThaw"), Is.EqualTo("3"));
            Assert.That(root.Q<LevelEffectCard>("effect-0").IsExpanded, Is.False);

            Select(LevelCanvasSelection.ForBlock(0, "b1"));
            Select(LevelCanvasSelection.ForBlock(1, "b2"));
            Assert.That(root.Q<LevelEffectCard>("effect-0").IsExpanded, Is.False, "The window remembers it.");

            root.Q<LevelEffectCard>("effect-0").IsExpanded = true;
            Select(LevelCanvasSelection.ForBlock(0, "b1"));
            Select(LevelCanvasSelection.ForBlock(1, "b2"));
            Assert.That(root.Q<LevelEffectCard>("effect-0").IsExpanded, Is.True);
        }

        [UnityTest]
        public IEnumerator AnAddedEffectShowsItsCardExpandedEvenWhenItsTypeWasFolded()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            // Folding b2's Ice folds Ice on every block, like the Inspector folds a component type.
            Select(LevelCanvasSelection.ForBlock(1, "b2"));
            root.Q<LevelEffectCard>("effect-0").IsExpanded = false;

            Select(LevelCanvasSelection.ForBlock(0, "b1"));
            m_effectWindow.AddEffect("ice");
            Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(0).Type, Is.EqualTo("ice"));
            Assert.That(root.Q<LevelEffectCard>("effect-0").IsExpanded, Is.True, "The added card starts expanded.");
        }

        [UnityTest]
        public IEnumerator AnEffectEditIsOneUndoStepWithTheDirtyMarkerAndOnePublish()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            Select(LevelCanvasSelection.ForBlock(0, "b1"));
            int publishCount = 0;
            void OnPublished(LevelData level) => publishCount++;
            LevelDatabaseWindow.DraftLevelPublished += OnPublished;
            try
            {
                publishCount = 0;
                m_effectWindow.AddEffect("arrow");
                Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.EqualTo(1));
                Assert.That(m_levelWindow.hasUnsavedChanges, Is.True, "The dirty marker follows the draft.");
                RunPendingPublish();
                yield return null;
                Assert.That(publishCount, Is.EqualTo(1), "One edit reaches the preview once.");

                Undo.PerformUndo();
                Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.Zero, "One undo step.");
                Assert.That(root.Q<LevelEffectCard>("effect-0"), Is.Null, "The card follows the undo.");
                Assert.That(m_levelWindow.hasUnsavedChanges, Is.False);
            }
            finally
            {
                LevelDatabaseWindow.DraftLevelPublished -= OnPublished;
            }
        }

        [UnityTest]
        public IEnumerator ARefusedEditShowsTheCanvasMessageInBothWindowsAndChangesNothing()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            Select(LevelCanvasSelection.ForBlock(0, "b1"));
            m_effectWindow.AddEffect("ice");
            string before = JsonUtility.ToJson(GetDraft());

            root.Q<IntegerField>("effect-0-clearsToThaw").value = 25;
            AssertRefused(root,
                "Level L1 block b1 effect ice key clearsToThaw has value '25'; expected an integer from 1 to 20.", before);
            Assert.That(root.Q<IntegerField>("effect-0-clearsToThaw").value, Is.EqualTo(1), "The field snaps back.");

            Select(LevelCanvasSelection.ForDoor(0, "d1"));
            Assert.That(root.Q<Label>("effect-editor-issue").style.display.value, Is.EqualTo(DisplayStyle.None),
                "A refusal belongs to the entity it was about.");
        }

        [UnityTest]
        public IEnumerator BlockEffectsAreAddedChangedAndRemovedThroughTheDraft()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            Select(LevelCanvasSelection.ForBlock(0, "b1"));

            m_effectWindow.AddEffect("arrow");
            Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.EqualTo(1));
            Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(0).FindValue("axis"), Is.EqualTo("Horizontal"));
            Assert.That(root.Q<LevelEffectCard>("effect-0").Title, Is.EqualTo("Arrow"));
            Assert.That(root.Q<DropdownField>("effect-0-axis").value, Is.EqualTo("Horizontal"));

            root.Q<DropdownField>("effect-0-axis").value = "Vertical";
            Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(0).FindValue("axis"), Is.EqualTo("Vertical"));

            m_effectWindow.AddEffect("ice");
            Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.EqualTo(2));
            Assert.That(root.Q<IntegerField>("effect-1-clearsToThaw").value, Is.EqualTo(1));

            string before = JsonUtility.ToJson(GetDraft());
            root.Q<IntegerField>("effect-1-clearsToThaw").value = 25;
            AssertRefused(root,
                "Level L1 block b1 effect ice key clearsToThaw has value '25'; expected an integer from 1 to 20.", before);
            Assert.That(root.Q<IntegerField>("effect-1-clearsToThaw").value, Is.EqualTo(1));
            root.Q<IntegerField>("effect-1-clearsToThaw").value = 7;
            Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(1).FindValue("clearsToThaw"), Is.EqualTo("7"));

            // Remove sits in the card header.
            Button remove = root.Q<Button>("effect-0-remove");
            Assert.That(remove.parent, Is.SameAs(root.Q<LevelEffectCard>("effect-0")));
            Press(remove);
            Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.EqualTo(1));
            Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(0).Type, Is.EqualTo("ice"));

            Undo.PerformUndo();
            Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.EqualTo(2), "Remove is one undo step.");
            Undo.PerformUndo();
            Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(1).FindValue("clearsToThaw"), Is.EqualTo("1"));
            Undo.PerformUndo();
            Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.EqualTo(1));
            Undo.PerformUndo();
            Assert.That(GetDraft().GetBlockAt(0).GetEffectAt(0).FindValue("axis"), Is.EqualTo("Horizontal"));
            Undo.PerformUndo();
            Assert.That(GetDraft().GetBlockAt(0).EffectCount, Is.Zero);
            Assert.That(root.Q<Label>("effects-empty"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TheBarrierStartsOpenParamIsAddedChangedAndRemoved()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            VisualElement root = m_effectWindow.rootVisualElement;
            Select(LevelCanvasSelection.ForObstacle(0, "barrier-1"));

            // A newly painted barrier has no params: the toggle shows the default, closed, marked as the default.
            Toggle startsOpen = root.Q<Toggle>("effect-0-startsOpen");
            Assert.That(startsOpen.enabledSelf, Is.True);
            Assert.That(startsOpen.value, Is.False);
            Assert.That(root.Q<Label>("effect-0-startsOpen-default"), Is.Not.Null);
            Assert.That(root.Q<Button>("effect-0-startsOpen-remove"), Is.Null);

            startsOpen.value = true;
            Assert.That(GetDraft().GetCellObjectAt(0).Effect.FindValue("startsOpen"), Is.EqualTo("true"),
                "A change adds the param.");
            Assert.That(root.Q<Label>("effect-0-startsOpen-default"), Is.Null);
            root.Q<Toggle>("effect-0-startsOpen").value = false;
            Assert.That(GetDraft().GetCellObjectAt(0).Effect.FindValue("startsOpen"), Is.EqualTo("false"));

            Press(root.Q<Button>("effect-0-startsOpen-remove"));
            Assert.That(GetDraft().GetCellObjectAt(0).Effect.ParamCount, Is.Zero);
            Assert.That(root.Q<Label>("effect-0-startsOpen-default"), Is.Not.Null);
            // The toggle's bool change bubbles through the card's foldout; after the Remove rebuilt the card, it must
            // still be expanded, so neither the true nor the false change was taken as a fold.
            Assert.That(root.Q<LevelEffectCard>("effect-0").IsExpanded, Is.True, "The toggle never folds its card.");
            Undo.PerformUndo();
            Assert.That(GetDraft().GetCellObjectAt(0).Effect.FindValue("startsOpen"), Is.EqualTo("false"));
            Undo.PerformUndo();
            Assert.That(GetDraft().GetCellObjectAt(0).Effect.FindValue("startsOpen"), Is.EqualTo("true"));
            Undo.PerformUndo();
            Assert.That(GetDraft().GetCellObjectAt(0).Effect.ParamCount, Is.Zero);
            Assert.That(root.Q<Toggle>("effect-0-startsOpen").value, Is.False, "The toggle follows the undo.");

            // barrier-2 is open under b2: closing it is refused with the validation's message.
            Select(LevelCanvasSelection.ForObstacle(1, "barrier-2"));
            string before = JsonUtility.ToJson(GetDraft());
            root.Q<Toggle>("effect-0-startsOpen").value = false;
            AssertRefused(root, "Level L1 cell object barrier-2 effect barrier starts closed on block b2 at (1, 3).",
                before);
            Assert.That(root.Q<Toggle>("effect-0-startsOpen").value, Is.True);
        }

        [UnityTest]
        public IEnumerator AClickOpensAClosedEffectEditorOnlyFromADesignerLevelEditor()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            Assert.That(m_levelWindow.OpensEffectEditor, Is.False, "A window a test creates never opens one.");

            Select(LevelCanvasSelection.ForDoor(0, "d1"));
            Assert.That(m_shownEffectEditors, Is.Empty);

            // Open, the designer entry point, sets it; the flag stands in for that here.
            m_levelWindow.OpensEffectEditor = true;
            Select(LevelCanvasSelection.ForBlock(1, "b2"));
            Assert.That(m_shownEffectEditors, Is.EqualTo(new[] { m_levelWindow }));
            Assert.That(m_effectWindow.rootVisualElement.Q<Label>("effect-editor-title").text, Is.EqualTo("Editing Blue B2"),
                "An open Effect Editor follows the click.");

            // With an Effect Editor open, the real opener adds no window.
            int open = Resources.FindObjectsOfTypeAll<LevelEffectEditorWindow>().Length;
            LevelEffectEditorWindow.ShowBeside(m_levelWindow);
            yield return null;
            yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll<LevelEffectEditorWindow>().Length, Is.EqualTo(open));
        }

        [UnityTest]
        public IEnumerator AClosingLevelEditorHandsTheEffectEditorOver()
        {
            yield return ShowWindows(LevelEffectCommandsTests.CreateLevel());
            LevelDatabaseWindow closing = m_levelWindow;
            Assert.That((m_effectWindow.Source as LevelDraftEditor)?.Window, Is.SameAs(closing));

            closing.DiscardChanges();
            closing.Close();
            m_levelWindow = null;
            yield return null;

            VisualElement root = m_effectWindow.rootVisualElement;
            LevelDatabaseWindow source = (m_effectWindow.Source as LevelDraftEditor)?.Window;
            if (source != null && source.GetDraftLevel() != null)
            {
                Assert.That(source, Is.Not.SameAs(closing), "Another open Level Editor with a level takes over.");
            }
            else
            {
                Assert.That(new[] { LevelEffectEditorWindow.k_noLevelText, LevelEffectEditorWindow.k_noLevelEditorText },
                    Does.Contain(root.Q<Label>("effect-editor-message").text));
                AssertTitle(root, null);
            }
        }

        private LevelEditorWorkspace.Steps CreateSteps(List<string> calls, string activeScene, bool playing,
            bool saveAnswer, bool windowsAnswer = true, bool layoutLoads = true)
        {
            return new LevelEditorWorkspace.Steps
            {
                IsPlaying = () => playing,
                GetActiveScenePath = () => activeScene,
                SaveModifiedScenes = () =>
                {
                    calls.Add("save");
                    return saveAnswer;
                },
                SaveOrDiscardUnsavedWindows = () =>
                {
                    calls.Add("windows");
                    return windowsAnswer;
                },
                OpenSceneSingle = path => calls.Add("scene " + path),
                LoadLayout = path =>
                {
                    calls.Add("layout " + path);
                    return layoutLoads;
                },
                ForgetLayoutState = () => calls.Add("forget"),
                ShowMessage = message => calls.Add("message " + message),
                OpenLevelEditor = () => calls.Add("level")
            };
        }

        // A test Level Editor with the fixture in its draft as the saved baseline, then a test Effect Editor that
        // follows it; neither window is one of the user's.
        private IEnumerator ShowWindows(LevelData fixture)
        {
            m_levelWindow = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            m_levelWindow.ShowEffectEditor = window => m_shownEffectEditors.Add(window);
            m_levelWindow.ShowUtility();
            m_levelWindow.position = new Rect(40f, 40f, 960f, 640f);
            double end = EditorApplication.timeSinceStartup + k_waitSeconds;
            while (m_levelWindow.rootVisualElement.Q<LevelCanvasElement>() == null
                   && EditorApplication.timeSinceStartup < end)
            {
                yield return null;
            }

            Assume.That(GetField<LevelDraft>("m_draft"), Is.Not.Null, "The active database has a loaded level.");
            LoadFixture(fixture);

            m_effectWindow = ScriptableObject.CreateInstance<LevelEffectEditorWindow>();
            m_effectWindow.ShowUtility();
            m_effectWindow.position = new Rect(1010f, 40f, 380f, 900f);
            end = EditorApplication.timeSinceStartup + k_waitSeconds;
            while (m_effectWindow.rootVisualElement.Q("effects-section") == null
                   && EditorApplication.timeSinceStartup < end)
            {
                yield return null;
            }

            Assert.That(m_effectWindow.rootVisualElement.Q("effects-section"), Is.Not.Null);
            m_effectWindow.Follow(new LevelDraftEditor(m_levelWindow));
            ValidateLayout(m_effectWindow.rootVisualElement);
            Undo.IncrementCurrentGroup();
        }

        // Replaces the window's draft with a known level as its saved state, as a Reload would, and refreshes it.
        private void LoadFixture(LevelData level)
        {
            GetField<LevelDraft>("m_draft").Replace(level);
            LevelEditorTestAccess.Set(m_levelWindow, "m_savedDraftJson", JsonUtility.ToJson(GetField<LevelDraft>("m_draft").Snapshot()));
            LevelEditorTestAccess.Invoke(m_levelWindow, "OnDraftChanged", null);
        }

        // The Select tool's click handler: the canvas raises SelectionRequested with this selection.
        private void Select(LevelCanvasSelection selection)
        {
            LevelEditorTestAccess.Invoke(m_levelWindow, "SelectOnCanvas", new object[] { selection });
        }

        private void RunPendingPublish()
        {
            LevelEditorTestAccess.Invoke(m_levelWindow, "PublishDraft", null);
        }

        private LevelData GetDraft()
        {
            return m_levelWindow.GetDraftLevel();
        }

        private T GetField<T>(string name)
        {
            return (T)LevelEditorTestAccess.Get(m_levelWindow, name);
        }

        private void AssertChoices(params (string type, bool enabled)[] expected)
        {
            IReadOnlyList<LevelEffectChoice> choices = m_effectWindow.AddEffectChoices;
            Assert.That(choices.Count, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(choices[index].Type, Is.EqualTo(expected[index].type));
                Assert.That(choices[index].IsEnabled, Is.EqualTo(expected[index].enabled), expected[index].type);
            }
        }

        private void AssertRefused(VisualElement root, string issue, string draftJson)
        {
            Assert.That(root.Q<Label>("effect-editor-issue").text, Is.EqualTo(issue));
            Assert.That(root.Q<Label>("effect-editor-issue").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_levelWindow.rootVisualElement.Q<Label>("status").text, Is.EqualTo(issue),
                "The Level Editor's status line shows the same message.");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(draftJson), "The draft is unchanged.");
        }

        // The title names the selection and the effects show with it; null expects neither.
        private static void AssertTitle(VisualElement root, string title)
        {
            ValidateLayout(root);
            Label label = root.Q<Label>("effect-editor-title");
            Assert.That(label.style.display.value, Is.EqualTo(title != null ? DisplayStyle.Flex : DisplayStyle.None));
            if (title != null)
            {
                Assert.That(label.text, Is.EqualTo(title));
            }

            Assert.That(root.Q("effects-section").style.display.value,
                Is.EqualTo(title != null ? DisplayStyle.Flex : DisplayStyle.None));
        }

        private static void Press(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static void ValidateLayout(VisualElement root)
        {
            object panel = root.panel;
            panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(panel, null);
        }
    }
}
