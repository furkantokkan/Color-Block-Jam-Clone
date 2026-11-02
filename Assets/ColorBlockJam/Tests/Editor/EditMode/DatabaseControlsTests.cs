using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Editor.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the shared editor controls and that the editor rows fit inside their windows.
    /// </summary>
    public sealed class DatabaseControlsTests
    {
        // Near the Piece Editor's 670 px minimum.
        private const float k_pieceEditorWidth = 700f;
        // The Level Editor's minimum width.
        private const float k_levelEditorWidth = 960f;

        [Test]
        public void StepperMovesOneChoiceAndStopsAtTheEnds()
        {
            DropdownField field = new DropdownField(new List<string> { "1. A", "2. B", "3. C" }, 0);
            ChoiceStepper stepper = new ChoiceStepper(field, "Previous", "Next");

            stepper.Step(1);
            Assert.That(field.index, Is.EqualTo(1));
            stepper.Step(1);
            stepper.Step(1);
            Assert.That(field.index, Is.EqualTo(2), "Down stops at the last choice.");

            stepper.Step(-1);
            stepper.Step(-1);
            stepper.Step(-1);
            Assert.That(field.index, Is.Zero, "Up stops at the first choice.");
        }

        [Test]
        public void AddedButtonsFollowTheArrowsInTheRow()
        {
            DropdownField field = new DropdownField(new List<string> { "New piece", "Single" }, 0);
            ChoiceStepper stepper = new ChoiceStepper(field, "Previous", "Next");

            Button added = stepper.AddCreateButton("new-item", "New item", () => { });

            Assert.That(added.parent, Is.SameAs(stepper));
            Assert.That(stepper.IndexOf(added), Is.EqualTo(3), "Field, Up and Down come first.");
            Assert.That(stepper.Q<Button>("new-item"), Is.SameAs(added));
            UiTestInput.AssertShowsIcon(added, ActionIcons.k_add);
        }

        [Test]
        public void StepperRunsTheFieldChangeCallback()
        {
            DropdownField field = new DropdownField(new List<string> { "1. A", "2. B" }, 0);
            ChoiceStepper stepper = new ChoiceStepper(field, "Previous", "Next");
            string changedTo = null;
            field.RegisterValueChangedCallback(change => changedTo = change.newValue);
            // A shown utility window gives the field a panel, so it sends change events.
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                host.ShowUtility();
                host.rootVisualElement.Add(stepper);

                stepper.Step(1);

                Assert.That(changedTo, Is.EqualTo("2. B"));
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void FieldRowStretchesTheFieldAndKeepsButtonsAfterIt()
        {
            DropdownField field = new DropdownField(new List<string> { "Default" }, 0);
            FieldActionsRow row = new FieldActionsRow(field);

            Button delete = row.AddDeleteButton("delete-item", "Delete", () => { });

            Assert.That(row.IndexOf(field), Is.Zero);
            Assert.That(row.IndexOf(delete), Is.EqualTo(1));
            Assert.That(field.style.flexGrow.value, Is.EqualTo(1f));
            Assert.That(delete.style.flexShrink.value, Is.Zero, "The button keeps its width when the row narrows.");
            UiTestInput.AssertShowsIcon(delete, ActionIcons.k_delete);
        }

        [Test]
        public void NamePopupCreatesTheSuggestedNameAfterClosing()
        {
            List<string> calls = new List<string>();
            DatabaseNamePopup popup = new DatabaseNamePopup("New Database", "Database2", _ => null,
                name => calls.Add("create " + name));
            // A shown utility window gives the popup content a panel, so its name field sends change events.
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                host.ShowUtility();
                popup.Build(host.rootVisualElement, () => calls.Add("close"));
                Assert.That(host.rootVisualElement.Q<TextField>("database-name").value, Is.EqualTo("Database2"));

                Assert.That(popup.HandleKey(KeyCode.Return), Is.True);

                Assert.That(calls, Is.EqualTo(new[] { "close", "create Database2" }),
                    "The popup closes before the owner can open a dialog.");
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void NamePopupKeepsAnInvalidNameOpenAndShowsWhy()
        {
            string created = null;
            bool closed = false;
            DatabaseNamePopup popup = new DatabaseNamePopup("New Database", "Database2",
                name => name.Contains(" ") ? "No spaces." : null, name => created = name);
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                host.ShowUtility();
                popup.Build(host.rootVisualElement, () => closed = true);
                VisualElement root = host.rootVisualElement;
                Button create = root.Q<Button>("create");

                root.Q<TextField>("database-name").value = "My Database";
                Assert.That(root.Q<Label>("database-name-error").text, Is.EqualTo("No spaces."));
                Assert.That(create.enabledSelf, Is.False);
                popup.HandleKey(KeyCode.Return);
                Assert.That(created, Is.Null, "Enter does not create an invalid name.");
                Assert.That(closed, Is.False, "The popup stays open so the name can be fixed.");

                root.Q<TextField>("database-name").value = "MyDatabase";
                Assert.That(root.Q<Label>("database-name-error").text, Is.Empty);
                Assert.That(create.enabledSelf, Is.True);
                Click(create);
                Assert.That(created, Is.EqualTo("MyDatabase"));
                Assert.That(closed, Is.True);
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void NamePopupEscapeClosesWithoutCreating()
        {
            string created = null;
            bool closed = false;
            DatabaseNamePopup popup = new DatabaseNamePopup("New Database", "Database2", _ => null,
                name => created = name);
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                host.ShowUtility();
                popup.Build(host.rootVisualElement, () => closed = true);
                Assert.That(popup.HandleKey(KeyCode.A), Is.False, "Letters belong to the name field.");

                Assert.That(popup.HandleKey(KeyCode.Escape), Is.True);

                Assert.That(closed, Is.True);
                Assert.That(created, Is.Null);
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void LockingChoicesKeepsActionButtonsUsable()
        {
            DropdownField field = new DropdownField(new List<string> { "No Levels" }, 0);
            ChoiceStepper stepper = new ChoiceStepper(field, "Previous", "Next");
            Button create = stepper.AddCreateButton("create-item", "New item", () => { });

            stepper.SetChoicesEnabled(false);

            Assert.That(field.enabledSelf, Is.False);
            Assert.That(create.enabledInHierarchy, Is.True, "New stays usable while an empty list is locked.");
        }

        [Test]
        public void LevelEditorPutsNewAndDeleteBesideTargetLevel()
        {
            EditorWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            try
            {
                OpenWithGui(window, k_levelEditorWidth);
                VisualElement root = window.rootVisualElement;
                ChoiceStepper levelRow = root.Q<ChoiceStepper>();

                Assert.That(root.Q<Button>("create-level").parent, Is.SameAs(levelRow));
                Assert.That(root.Q<Button>("delete-level").parent, Is.SameAs(levelRow));
                Assert.That(root.Query<Button>().Where(button => button.text == "Create Level").ToList(), Is.Empty);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void LevelEditorPopupSuggestsAFreeNameAndRejectsATakenOne()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                OpenWithGui(window, k_levelEditorWidth);
                host.ShowUtility();
                bool closed = false;
                DatabaseNamePopup popup = window.CreateDatabasePopup();
                popup.Build(host.rootVisualElement, () => closed = true);
                VisualElement root = host.rootVisualElement;
                TextField nameField = root.Q<TextField>("database-name");
                Button create = root.Q<Button>("create");
                Assert.That(nameField.value, Does.StartWith("LevelDatabase"));
                Assert.That(create.enabledSelf, Is.True, "The suggested name is free.");

                // The default database already uses this name (DEC-196); nothing is written for it.
                nameField.value = LevelDatabaseAddressables.DefaultDatabaseName;
                popup.HandleKey(KeyCode.Return);

                Assert.That(create.enabledSelf, Is.False);
                Assert.That(root.Q<Label>("database-name-error").text, Does.Contain("already exists"));
                Assert.That(closed, Is.False, "The popup stays open so the name can be fixed.");
            }
            finally
            {
                host.Close();
                window.Close();
            }
        }

        [Test]
        public void PieceEditorRowsFitTheWindowWidth()
        {
            AssertRowsFit(ScriptableObject.CreateInstance<PieceEditorWindow>(), k_pieceEditorWidth, 2);
        }

        [Test]
        public void LevelEditorRowsFitTheWindowWidth()
        {
            // The level editor's tools pane nests its rows in groups, and its top and bottom bars and the validation
            // panel under the tools sit outside it. Depth 3 reaches the + and × beside Level Database, the Target Level
            // stepper's arrows and buttons, and the panel's solution rows, pager and button row.
            AssertRowsFit(ScriptableObject.CreateInstance<LevelDatabaseWindow>(), k_levelEditorWidth, 3, "top-bar",
                "bottom-bar", "validation-panel");
        }

        private static void Click(Button button)
        {
            // Runs the registered click action without relying on OS pointer input.
            typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null, 0 });
        }

        private static void OpenWithGui(EditorWindow window, float width)
        {
            window.ShowUtility();
            window.position = new Rect(40f, 40f, width, 900f);
            // Tests call CreateGUI directly, as the other editor window tests do.
            window.GetType().GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(window, null);
        }

        private static void AssertRowsFit(EditorWindow window, float width, int depth, params string[] barNames)
        {
            try
            {
                OpenWithGui(window, width);
                // Layout normally runs on repaint; validate it now to read the final sizes.
                object panel = window.rootVisualElement.panel;
                panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(panel, null);

                List<string> overflows = new List<string>();
                VisualElement content = window.rootVisualElement.Q<ScrollView>().contentContainer;
                CollectOverflows(content, depth, overflows);
                foreach (string barName in barNames)
                {
                    CollectOverflows(window.rootVisualElement.Q(barName), depth, overflows);
                }

                Assert.That(overflows, Is.Empty, "Elements wider than their parent row.");
            }
            finally
            {
                window.Close();
            }
        }

        private static void CollectOverflows(VisualElement parent, int depth, List<string> overflows)
        {
            foreach (VisualElement child in parent.hierarchy.Children())
            {
                if (child.layout.xMax > parent.layout.width + 0.5f)
                {
                    overflows.Add($"{child.GetType().Name} '{child.name}' ends at {child.layout.xMax:0} " +
                                  $"inside '{parent.name}' of width {parent.layout.width:0}");
                }

                if (depth > 1)
                {
                    CollectOverflows(child, depth - 1, overflows);
                }
            }
        }
    }
}
