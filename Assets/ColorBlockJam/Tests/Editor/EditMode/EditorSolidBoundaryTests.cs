using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Independent owners exercise persistence, draft access and registered warning boundaries.</summary>
    public sealed class EditorSolidBoundaryTests
    {
        private const string k_scratchPrefix = "Assets/__EditorSolidBoundaryTests_";
        private string m_scratch;
        private LevelEffectEditorWindow m_window;
        private EffectStackingRules m_viewRules;

        [TearDown]
        public void DeleteOnlyTestOwnedObjects()
        {
            if (m_window != null)
            {
                UnityEngine.Object.DestroyImmediate(m_window);
            }

            if (m_viewRules != null)
            {
                UnityEngine.Object.DestroyImmediate(m_viewRules);
            }

            if (m_scratch != null)
            {
                Assert.That(m_scratch, Does.StartWith(k_scratchPrefix));
                Assert.That(m_scratch.Substring(k_scratchPrefix.Length), Does.Match("^[a-f0-9]{32}$"));
                AssetDatabase.DeleteAsset(m_scratch);
                Assert.That(AssetDatabase.IsValidFolder(m_scratch), Is.False);
                m_scratch = null;
            }
        }

        [Test]
        public void CreatingRulesDoesNotSaveAnUnrelatedDirtyAsset()
        {
            CreateScratch();
            EffectStackingRules unrelated = CreateDirtyUnrelatedRules(out string unrelatedPath, out string savedText);
            string path = m_scratch + "/CreatedRules.asset";

            EffectStackingRules created = EffectStackingRulesStore.LoadOrCreate(path);

            Assert.That(AssetDatabase.LoadAssetAtPath<EffectStackingRules>(path), Is.SameAs(created));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(created.CanStack(LevelEffectTarget.Block, "arrow", "ice"), Is.False);
            Assert.That(EditorUtility.IsDirty(created), Is.False);
            AssertUnrelatedUnsaved(unrelated, unrelatedPath, savedText);
        }

        [Test]
        public void SavingAndReloadingRulesPreservesValuesAndGuidWithoutSavingOtherAssets()
        {
            CreateScratch();
            string path = m_scratch + "/Rules.asset";
            EffectStackingRules rules = EffectStackingRulesStore.LoadOrCreate(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            EffectStackingRules unrelated = CreateDirtyUnrelatedRules(out string unrelatedPath, out string savedText);
            rules.SetCanStack(LevelEffectTarget.Block, "arrow", "ice", true);
            rules.SetCanStack(LevelEffectTarget.Door, "gate-a", "gate-b", true);

            EffectStackingRulesStore.Save(rules);
            Assert.That(EditorUtility.IsDirty(rules), Is.False);
            AssertUnrelatedUnsaved(unrelated, unrelatedPath, savedText);
            Resources.UnloadAsset(rules);
            rules = EffectStackingRulesStore.LoadOrCreate(path);

            Assert.That(rules.CanStack(LevelEffectTarget.Block, "ice", "arrow"), Is.True);
            Assert.That(rules.CanStack(LevelEffectTarget.Door, "gate-b", "gate-a"), Is.True);
            Assert.That(rules.CanStack(LevelEffectTarget.Door, "arrow", "ice"), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            AssertUnrelatedUnsaved(unrelated, unrelatedPath, savedText);

            rules.SetCanStack(LevelEffectTarget.Block, "ice", "arrow", false);
            EffectStackingRulesStore.Save(rules);
            Resources.UnloadAsset(rules);
            rules = EffectStackingRulesStore.LoadOrCreate(path);
            Assert.That(rules.CanStack(LevelEffectTarget.Block, "arrow", "ice"), Is.False);
            Assert.That(rules.CanStack(LevelEffectTarget.Door, "gate-a", "gate-b"), Is.True);
            AssertUnrelatedUnsaved(unrelated, unrelatedPath, savedText);
        }

        [Test]
        public void AnIndependentDraftOwnerDisplaysItsSelectionAndChanges()
        {
            FakeDraftOwner owner = new FakeDraftOwner(CreateLevel());
            FakeDraftSource source = CreateWindow(owner);
            Assert.That(m_window.Source, Is.SameAs(owner));
            AssertTitle("Editing Red B1");
            Assert.That(m_window.rootVisualElement.Q<Label>("effects-empty"), Is.Not.Null);

            owner.CanvasSelection = LevelCanvasSelection.ForDoor(0, "d1");
            source.Raise(owner);
            AssertTitle("Editing Red Gate D1");
            Assert.That(m_window.rootVisualElement.Q<Label>("effects-note").text,
                Is.EqualTo(LevelEffectEditorWindow.k_gateEffectsText));

            owner.CanvasSelection = LevelCanvasSelection.None;
            source.Raise(owner);
            Assert.That(m_window.rootVisualElement.Q<Label>("effect-editor-title").style.display.value,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(m_window.rootVisualElement.Q<Label>("effect-editor-message").text,
                Is.EqualTo(LevelEffectEditorWindow.k_selectHint));
        }

        [Test]
        public void AnAcceptedCommandIsForwardedOnceWithUndoNameAndReadBackFromItsOwner()
        {
            FakeDraftOwner owner = new FakeDraftOwner(CreateLevel());
            LevelData before = owner.Draft;
            FakeDraftSource source = CreateWindow(owner);
            owner.Applied = () => source.Raise(owner);

            m_window.AddEffect("arrow");

            Assert.That(owner.RequestCount, Is.EqualTo(1));
            Assert.That(owner.LastUndoName, Is.EqualTo("Add Arrow Effect"));
            Assert.That(owner.Draft, Is.Not.SameAs(before));
            Assert.That(before.GetBlockAt(0).EffectCount, Is.Zero);
            Assert.That(owner.Draft.GetBlockAt(0).GetEffectAt(0).Type, Is.EqualTo("arrow"));
            Assert.That(m_window.rootVisualElement.Q<PopupField<string>>("effect-0-axis").value,
                Is.EqualTo("Horizontal"));
            Assert.That(m_window.rootVisualElement.Q<Label>("effect-editor-issue").text, Is.Empty);
        }

        [Test]
        public void ARefusedOwnerRequestKeepsTheDraftAndDisplaysItsIssue()
        {
            FakeDraftOwner owner = new FakeDraftOwner(CreateLevel()) { Refusal = "Draft is temporarily read-only." };
            LevelData before = owner.Draft;
            CreateWindow(owner);

            m_window.AddEffect("arrow");

            Assert.That(owner.RequestCount, Is.EqualTo(1));
            Assert.That(owner.LastUndoName, Is.EqualTo("Add Arrow Effect"));
            Assert.That(owner.Draft, Is.SameAs(before));
            Assert.That(owner.Draft.GetBlockAt(0).EffectCount, Is.Zero);
            Label issue = m_window.rootVisualElement.Q<Label>("effect-editor-issue");
            Assert.That(issue.text, Is.EqualTo(owner.Refusal));
            Assert.That(issue.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_window.rootVisualElement.Q<Label>("effects-empty"), Is.Not.Null);
        }

        [Test]
        public void ARefusedCommandKeepsTheAcceptedDraftAndCanBeFollowedByAnotherEdit()
        {
            FakeDraftOwner owner = new FakeDraftOwner(CreateLevel());
            CreateWindow(owner);
            m_window.AddEffect("arrow");
            LevelData accepted = owner.Draft;

            m_window.AddEffect("arrow");

            Assert.That(owner.RequestCount, Is.EqualTo(2));
            Assert.That(owner.Draft, Is.SameAs(accepted));
            Assert.That(owner.Draft.GetBlockAt(0).EffectCount, Is.EqualTo(1));
            Assert.That(m_window.rootVisualElement.Q<Label>("effect-editor-issue").text, Is.Not.Empty);

            m_viewRules.SetCanStack(LevelEffectTarget.Block, "arrow", "ice", true);
            m_window.AddEffect("ice");
            Assert.That(owner.RequestCount, Is.EqualTo(3));
            Assert.That(owner.Draft.GetBlockAt(0).EffectCount, Is.EqualTo(2));
            Assert.That(m_window.rootVisualElement.Q<Label>("effect-editor-issue").text, Is.Empty);
        }

        [Test]
        public void ChangedSourcesAndAnEmptyOwnerHandOverThroughTheSourceBoundary()
        {
            FakeDraftOwner first = new FakeDraftOwner(CreateLevel());
            FakeDraftOwner second = new FakeDraftOwner(CreateLevel("b2", "Blue"));
            FakeDraftSource source = CreateWindow(first);
            source.Owners.Add(second);

            source.Raise(second);
            Assert.That(m_window.Source, Is.SameAs(second));
            AssertTitle("Editing Blue B2");

            second.Draft = null;
            source.Raise(second);
            Assert.That(source.LastExcluded, Is.SameAs(second));
            Assert.That(m_window.Source, Is.SameAs(first));
            AssertTitle("Editing Red B1");

            first.IsAvailable = false;
            second.Draft = CreateLevel("b2", "Blue");
            m_window.Follow(first);
            Assert.That(m_window.Source, Is.SameAs(second));
            AssertTitle("Editing Blue B2");
        }

        [Test]
        public void ReplacingAndDestroyingTheWindowUnsubscribesAndDisposesItsSources()
        {
            FakeDraftOwner first = new FakeDraftOwner(CreateLevel());
            FakeDraftSource oldSource = CreateWindow(first);
            FakeDraftOwner second = new FakeDraftOwner(CreateLevel("b2", "Blue"));
            FakeDraftSource newSource = new FakeDraftSource(second);

            m_window.SetSources(newSource);

            Assert.That(oldSource.DisposeCount, Is.EqualTo(1));
            Assert.That(oldSource.ListenerCount, Is.Zero);
            Assert.That(newSource.ListenerCount, Is.EqualTo(1));
            oldSource.Raise(first);
            Assert.That(m_window.Source, Is.SameAs(second));
            AssertTitle("Editing Blue B2");

            UnityEngine.Object.DestroyImmediate(m_window);
            m_window = null;
            Assert.That(newSource.DisposeCount, Is.EqualTo(1));
            Assert.That(newSource.ListenerCount, Is.Zero);
            newSource.Raise(second);
        }

        [Test]
        public void IndependentWarningsKeepRegistrationOrderMessageIdAndWorldCells()
        {
            LevelData level = CreateLevel();
            FakeWarning first = new FakeWarning("Independent first warning.");
            FakeWarning silent = new FakeWarning(null);
            FakeWarning last = new FakeWarning("Independent final warning.");

            List<LevelIssue> issues = LevelDraftIssues.Collect(level, new ILevelBlockWarning[] { first, silent, last });

            Assert.That(level.Validate(out string error), Is.True, error);
            Assert.That(issues.Count, Is.EqualTo(2));
            Assert.That(LevelDraftIssues.HasError(issues), Is.False);
            Assert.That(issues[0].Message, Is.EqualTo(first.Message));
            Assert.That(issues[1].Message, Is.EqualTo(last.Message));
            foreach (LevelIssue issue in issues)
            {
                Assert.That(issue.Severity, Is.EqualTo(LevelIssueSeverity.Warning));
                Assert.That(issue.EntityId, Is.EqualTo("b1"));
                Assert.That(issue.Cells, Is.EqualTo(new[] { new Vector2Int(1, 2), new Vector2Int(2, 2) }));
                Assert.That(issue.HasSegment, Is.False);
            }

            foreach (FakeWarning warning in new[] { first, silent, last })
            {
                Assert.That(warning.CallCount, Is.EqualTo(1));
                Assert.That(warning.Level, Is.SameAs(level));
                Assert.That(warning.Block, Is.SameAs(level.GetBlockAt(0)));
                Assert.That(warning.Subject, Is.EqualTo("Block b1"));
            }
        }

        [TestCase("Red", "can never exit: no Red door is on the board.")]
        [TestCase("Unrecognised", "uses 'Unrecognised', which is not a gate colour.")]
        public void GeneralColorWarningsTakePriorityOverRegisteredWarnings(string color, string message)
        {
            LevelData level = CreateLevel("b1", color, "Blue");
            FakeWarning warning = new FakeWarning("Specific warning must not hide the general problem.");

            List<LevelIssue> issues = LevelDraftIssues.Collect(level, new ILevelBlockWarning[] { warning });

            Assert.That(warning.CallCount, Is.Zero);
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Warning));
            Assert.That(issues[0].Message, Is.EqualTo("Block b1 " + message));
            Assert.That(issues[0].EntityId, Is.EqualTo("b1"));
            Assert.That(issues[0].Cells, Is.EqualTo(new[] { new Vector2Int(1, 2), new Vector2Int(2, 2) }));
        }

        [Test]
        public void ValidationErrorsRemainAheadOfRegisteredWarnings()
        {
            LevelData level = new LevelData("L1", "", 5, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 1, 2, "Red") },
                new[] { new LevelBlockData("b1", "Red", Vector2Int.one, new[] { Vector2Int.zero }) });
            FakeWarning warning = new FakeWarning("Independent warning.");

            List<LevelIssue> issues = LevelDraftIssues.Collect(level, new ILevelBlockWarning[] { warning });

            Assert.That(issues.Count, Is.EqualTo(2));
            Assert.That(issues[0].Severity, Is.EqualTo(LevelIssueSeverity.Error));
            Assert.That(issues[0].Message, Is.EqualTo("The level needs a name."));
            Assert.That(issues[1].Message, Is.EqualTo(warning.Message));
            Assert.That(issues[1].Severity, Is.EqualTo(LevelIssueSeverity.Warning));
            Assert.That(LevelDraftIssues.HasError(issues), Is.True);
        }

        [Test]
        public void NoSelectedLevelDoesNotInvokeRegisteredWarnings()
        {
            FakeWarning warning = new FakeWarning("Independent warning.");
            Assert.That(LevelDraftIssues.Collect(null, new ILevelBlockWarning[] { warning }), Is.Empty);
            Assert.That(warning.CallCount, Is.Zero);
        }

        private void CreateScratch()
        {
            string folder = "__EditorSolidBoundaryTests_" + Guid.NewGuid().ToString("N");
            m_scratch = "Assets/" + folder;
            AssetDatabase.CreateFolder("Assets", folder);
        }

        private EffectStackingRules CreateDirtyUnrelatedRules(out string path, out string savedText)
        {
            path = m_scratch + "/UnrelatedRules.asset";
            EffectStackingRules unrelated = EffectStackingRulesStore.LoadOrCreate(path);
            savedText = File.ReadAllText(path);
            unrelated.SetCanStack(LevelEffectTarget.Block, "arrow", "ice", true);
            EditorUtility.SetDirty(unrelated);
            Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
            return unrelated;
        }

        private static void AssertUnrelatedUnsaved(EffectStackingRules unrelated, string path, string savedText)
        {
            Assert.That(EditorUtility.IsDirty(unrelated), Is.True, "The unrelated edit is still pending.");
            Assert.That(File.ReadAllText(path), Is.EqualTo(savedText), "Persistence never flushes another asset.");
        }

        private FakeDraftSource CreateWindow(FakeDraftOwner owner)
        {
            FakeDraftSource source = new FakeDraftSource(owner);
            m_window = ScriptableObject.CreateInstance<LevelEffectEditorWindow>();
            m_viewRules = ScriptableObject.CreateInstance<EffectStackingRules>();
            m_window.StackingRules = m_viewRules;
            m_window.SetSources(source);
            // Build only this test-owned UI tree; no utility window, layout or user selection changes.
            typeof(LevelEffectEditorWindow).GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_window, null);
            m_window.Follow(owner);
            return source;
        }

        private void AssertTitle(string title)
        {
            Label label = m_window.rootVisualElement.Q<Label>("effect-editor-title");
            Assert.That(label.text, Is.EqualTo(title));
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        private static LevelData CreateLevel(string id = "b1", string color = "Red", string doorColor = null)
        {
            return new LevelData("L1", "Level 1", 5, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 1, 2, doorColor ?? color) },
                new[] { new LevelBlockData(id, color, new Vector2Int(1, 2),
                    new[] { Vector2Int.zero, Vector2Int.right }) });
        }

        private sealed class FakeDraftOwner : ILevelDraftEditor
        {
            internal LevelData Draft;
            internal string Refusal;
            internal int RequestCount;
            internal string LastUndoName;
            internal Action Applied;
            public bool IsAvailable { get; set; } = true;
            public LevelCanvasSelection CanvasSelection { get; set; }

            internal FakeDraftOwner(LevelData level)
            {
                Draft = level;
                CanvasSelection = LevelCanvasSelection.ForBlock(0, level.GetBlockAt(0).Id);
            }

            public LevelData GetDraftLevel() => Draft;

            public bool TryApplyEditRequest(string undoName, LevelEditRequest request, out string issue)
            {
                RequestCount++;
                LastUndoName = undoName;
                if (Refusal != null)
                {
                    issue = Refusal;
                    return false;
                }

                if (!request(Draft, out LevelData result, out issue))
                {
                    return false;
                }

                Draft = result;
                Applied?.Invoke();
                return true;
            }
        }

        private sealed class FakeDraftSource : ILevelDraftEditorSource
        {
            internal readonly List<FakeDraftOwner> Owners = new List<FakeDraftOwner>();
            internal int DisposeCount;
            internal ILevelDraftEditor LastExcluded;
            public event Action<ILevelDraftEditor> Changed;
            internal int ListenerCount => Changed?.GetInvocationList().Length ?? 0;

            internal FakeDraftSource(FakeDraftOwner owner) => Owners.Add(owner);

            public ILevelDraftEditor Find(ILevelDraftEditor excluded)
            {
                LastExcluded = excluded;
                foreach (FakeDraftOwner owner in Owners)
                {
                    if (owner != excluded && owner.IsAvailable && owner.Draft != null)
                    {
                        return owner;
                    }
                }

                return null;
            }

            public void Dispose() => DisposeCount++;
            internal void Raise(ILevelDraftEditor owner) => Changed?.Invoke(owner);
        }

        private sealed class FakeWarning : ILevelBlockWarning
        {
            internal readonly string Message;
            internal int CallCount;
            internal LevelData Level;
            internal LevelBlockData Block;
            internal string Subject;

            internal FakeWarning(string message) => Message = message;

            public string GetWarning(LevelData level, LevelBlockData block, string subject)
            {
                CallCount++;
                Level = level;
                Block = block;
                Subject = subject;
                return Message;
            }
        }
    }
}
