using System.Collections.Generic;
using ColorBlockJam.Editor.Search;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies dropdown search filtering and keyboard picking in the search popup.
    /// </summary>
    public sealed class ChoiceSearchTests
    {
        private static readonly string[] s_levels =
        {
            "1. Tutorial", "2. Two Doors", "3. Ice Corner", "4. Arrow Lane", "5. Final Jam"
        };

        [Test]
        public void EmptyQueryKeepsEveryChoiceInListOrder()
        {
            List<int> matches = new List<int>();

            ChoiceSearch.Filter(s_levels, "   ", matches);

            Assert.That(matches, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        }

        [Test]
        public void QueryIgnoresCaseAndDropsChoicesThatDoNotMatch()
        {
            List<int> matches = new List<int>();

            ChoiceSearch.Filter(s_levels, "ICE", matches);
            Assert.That(matches, Is.EqualTo(new[] { 2 }));

            ChoiceSearch.Filter(s_levels, "zzz", matches);
            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void EqualScoresKeepListOrder()
        {
            List<int> matches = new List<int>();

            ChoiceSearch.Filter(new[] { "Level B", "Level A" }, "level", matches);

            Assert.That(matches, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void EnterPicksTheBestMatchAndClosesThePopup()
        {
            string picked = null;
            bool closed = false;
            ChoiceSearchPopup popup = new ChoiceSearchPopup(s_levels, 0, 300f, choice => picked = choice);
            // A shown utility window gives the popup content a panel, so the search box sends change events.
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                host.ShowUtility();
                popup.Build(host.rootVisualElement, () => closed = true);
                host.rootVisualElement.Q<ToolbarSearchField>().value = "arrow";
                Assert.That(host.rootVisualElement.Q<ListView>().itemsSource.Count, Is.EqualTo(1));

                Assert.That(popup.HandleKey(KeyCode.Return), Is.True);

                Assert.That(picked, Is.EqualTo("4. Arrow Lane"));
                Assert.That(closed, Is.True, "Picking closes the popup.");
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void ArrowsStartAtTheCurrentChoiceAndEscapeClosesWithoutPicking()
        {
            string picked = null;
            bool closed = false;
            ChoiceSearchPopup popup = new ChoiceSearchPopup(s_levels, 2, 300f, choice => picked = choice);
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                host.ShowUtility();
                popup.Build(host.rootVisualElement, () => closed = true);
                ListView list = host.rootVisualElement.Q<ListView>();
                Assert.That(list.selectedIndex, Is.EqualTo(2));

                popup.HandleKey(KeyCode.DownArrow);
                Assert.That(list.selectedIndex, Is.EqualTo(3));
                for (int press = 0; press < 5; press++)
                {
                    popup.HandleKey(KeyCode.UpArrow);
                }

                Assert.That(list.selectedIndex, Is.Zero, "Up stops at the first row.");
                Assert.That(popup.HandleKey(KeyCode.A), Is.False, "Letters belong to the search box.");

                Assert.That(popup.HandleKey(KeyCode.Escape), Is.True);
                Assert.That(picked, Is.Null);
                Assert.That(closed, Is.True, "Escape closes the popup.");
            }
            finally
            {
                host.Close();
            }
        }
    }
}
