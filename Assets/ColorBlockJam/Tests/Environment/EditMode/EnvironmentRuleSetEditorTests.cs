using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Environment;
using ColorBlockJam.Environment.Editor;
using ColorBlockJam.Presentation.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    public sealed class EnvironmentRuleSetEditorTests
    {
        private EnvironmentRuleSet m_ruleSet;
        private EnvironmentRuleSetEditor m_editor;
        private VisualElement m_root;
        private BoardPieceView m_wall;

        [SetUp]
        public void SetUp()
        {
            EnvironmentRuleSet asset = AssetDatabase.LoadAssetAtPath<EnvironmentRuleSet>(EnvironmentRuleSetTests.RuleSetPath);
            m_wall = asset.GetRule(1).Output;
            m_ruleSet = EnvironmentRuleSetTests.CreateRuleSet(m_wall, true,
                EnvironmentNeighbor.Any, EnvironmentNeighbor.Void, EnvironmentNeighbor.Any, EnvironmentNeighbor.Any,
                EnvironmentNeighbor.Floor, EnvironmentNeighbor.Any, EnvironmentNeighbor.Any, EnvironmentNeighbor.Any);
            EditorUtility.ClearDirty(m_ruleSet);
            m_editor = (EnvironmentRuleSetEditor)UnityEditor.Editor.CreateEditor(m_ruleSet, typeof(EnvironmentRuleSetEditor));
            m_root = m_editor.CreateInspectorGUI();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_editor);
            Undo.ClearUndo(m_ruleSet);
            Object.DestroyImmediate(m_ruleSet);
        }

        [Test]
        public void EachRuleShowsAColouredThreeByThreeGridTheOutputAndTheRotatedToggle()
        {
            List<VisualElement> rules = m_root.Query(className: EnvironmentRuleSetEditor.RuleClass).ToList();
            Assert.That(rules.Count, Is.EqualTo(1));
            VisualElement rule = rules[0];

            List<Button> cells = rule.Query<Button>(className: EnvironmentRuleSetEditor.CellClass).ToList();
            Assert.That(cells.Count, Is.EqualTo(8), "Eight neighbour buttons around a fixed centre.");
            Assert.That(rule.Query<Label>(className: EnvironmentRuleSetEditor.CentreClass).ToList().Count, Is.EqualTo(1));
            Assert.That(cells[1].text, Is.EqualTo("Void"));
            Assert.That(cells[1].ClassListContains(EnvironmentRuleSetEditor.VoidClass), Is.True);
            Assert.That(cells[4].text, Is.EqualTo("Floor"));
            Assert.That(cells[4].ClassListContains(EnvironmentRuleSetEditor.FloorClass), Is.True);
            Assert.That(cells[0].ClassListContains(EnvironmentRuleSetEditor.AnyClass), Is.True);
            StringAssert.Contains("Upper neighbour: Void", cells[1].tooltip);
            StringAssert.Contains("Any", cells[1].tooltip);
            Assert.That(rule.Query<Toggle>().ToList().Count, Is.EqualTo(1));
            Assert.That(rule.Query<UnityEditor.UIElements.PropertyField>().ToList().Count, Is.EqualTo(1));
        }

        [Test]
        public void CyclingANeighbourGoesAnyFloorVoidMarksDirtyAndUndoes()
        {
            Assert.That(EnvironmentRuleSetEditor.GetNext(EnvironmentNeighbor.Any), Is.EqualTo(EnvironmentNeighbor.Floor));
            Assert.That(EnvironmentRuleSetEditor.GetNext(EnvironmentNeighbor.Floor), Is.EqualTo(EnvironmentNeighbor.Void));
            Assert.That(EnvironmentRuleSetEditor.GetNext(EnvironmentNeighbor.Void), Is.EqualTo(EnvironmentNeighbor.Any));

            Undo.IncrementCurrentGroup();
            m_editor.CycleNeighbor(0, 0);

            Assert.That(m_ruleSet.GetRule(0).GetNeighbor(0), Is.EqualTo(EnvironmentNeighbor.Floor));
            Assert.That(EditorUtility.IsDirty(m_ruleSet), Is.True);
            Button cell = m_root.Query<Button>(className: EnvironmentRuleSetEditor.CellClass).First();
            Assert.That(cell.ClassListContains(EnvironmentRuleSetEditor.FloorClass), Is.True, "The grid redraws.");

            Undo.PerformUndo();
            Assert.That(m_ruleSet.GetRule(0).GetNeighbor(0), Is.EqualTo(EnvironmentNeighbor.Any));
        }

        [Test]
        public void RulesCanBeAddedReorderedAndRemovedWithUndo()
        {
            Undo.IncrementCurrentGroup();
            m_editor.AddRule();
            Assert.That(m_ruleSet.RuleCount, Is.EqualTo(2));
            Assert.That(m_ruleSet.GetRule(1).Output, Is.Null);
            Assert.That(m_ruleSet.GetRule(1).IsRotated, Is.True);
            for (int neighbor = 0; neighbor < EnvironmentRuleSet.NeighborCount; neighbor++)
            {
                Assert.That(m_ruleSet.GetRule(1).GetNeighbor(neighbor), Is.EqualTo(EnvironmentNeighbor.Any));
            }

            Assert.That(m_ruleSet.Validate(out _), Is.False, "A new empty rule needs an output and a check.");
            Assert.That(m_root.Query(className: EnvironmentRuleSetEditor.RuleClass).ToList().Count, Is.EqualTo(2));

            Undo.IncrementCurrentGroup();
            m_editor.MoveRule(1, 0);
            Assert.That(m_ruleSet.GetRule(0).Output, Is.Null);
            Assert.That(m_ruleSet.GetRule(1).Output, Is.SameAs(m_wall));

            Undo.IncrementCurrentGroup();
            m_editor.RemoveRule(0);
            Assert.That(m_ruleSet.RuleCount, Is.EqualTo(1));
            Assert.That(m_ruleSet.GetRule(0).Output, Is.SameAs(m_wall));

            Undo.PerformUndo();
            Assert.That(m_ruleSet.RuleCount, Is.EqualTo(2));
            Undo.PerformUndo();
            Undo.PerformUndo();
            Assert.That(m_ruleSet.RuleCount, Is.EqualTo(1));
            Assert.That(m_ruleSet.Validate(out string error), Is.True, error);
        }

        // The Output field and the Rotated toggle edit the asset through their bindings,
        // without the inspector's own buttons, so the status box must track the asset.
        [UnityTest]
        public IEnumerator StatusFollowsEditsMadeThroughTheBoundFields()
        {
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.Show();
                window.rootVisualElement.Add(m_root);
                HelpBox status = m_root.Q<HelpBox>(className: EnvironmentRuleSetEditor.StatusClass);
                Assert.That(status, Is.Not.Null);
                Assert.That(status.style.display.value, Is.EqualTo(DisplayStyle.None), "The rule is valid.");

                SetOutput(null);
                yield return WaitForStatus(window, status, DisplayStyle.Flex);
                Assert.That(status.style.display.value, Is.EqualTo(DisplayStyle.Flex), "Clearing the output shows the error.");
                StringAssert.Contains("output piece prefab", status.text);

                SetOutput(m_wall);
                yield return WaitForStatus(window, status, DisplayStyle.None);
                Assert.That(status.style.display.value, Is.EqualTo(DisplayStyle.None), "Assigning an output hides it.");
            }
            finally
            {
                window.Close();
            }
        }

        private void SetOutput(BoardPieceView output)
        {
            SerializedObject serialized = new SerializedObject(m_ruleSet);
            serialized.FindProperty("m_rules").GetArrayElementAtIndex(0).FindPropertyRelative("m_output")
                .objectReferenceValue = output;
            serialized.ApplyModifiedProperties();
        }

        private static IEnumerator WaitForStatus(EditorWindow window, HelpBox status, DisplayStyle expected)
        {
            for (int frame = 0; frame < 300 && status.style.display.value != expected; frame++)
            {
                window.Repaint();
                yield return null;
            }
        }
    }
}
