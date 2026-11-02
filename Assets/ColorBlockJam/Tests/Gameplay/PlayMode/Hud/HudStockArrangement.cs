using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Hud
{
    /// <summary>
    /// Sets a booster's committed stock on the project's shared save service for one HUD PlayMode test and puts every
    /// changed value back afterwards, through the private commit-and-publish step that HubTestSupport.ArrangeStock and
    /// the copy's capture harness use (Editor play seeds no coins, so a real purchase cannot arrange it). A test
    /// therefore never depends on, or leaves behind, another suite's spending.
    /// </summary>
    /// <remarks>Owner: one test fixture. Stop point: <see cref="Restore"/> in its teardown.</remarks>
    internal sealed class HudStockArrangement
    {
        private static readonly MethodInfo s_publish =
            typeof(PlayerSaveService).GetMethod("Publish", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly Dictionary<BoosterId, int> m_original = new Dictionary<BoosterId, int>();
        private PlayerSaveService m_service;

        /// <summary>Sets the committed stock of <paramref name="id"/>; the first call per booster remembers its value.</summary>
        public void Arrange(PlayerSaveService service, BoosterId id, int stock)
        {
            Assert.That(s_publish, Is.Not.Null, "PlayerSaveService renamed or removed its private Publish method");
            Assert.That(service, Is.Not.Null);
            m_service = service;
            if (!m_original.ContainsKey(id))
            {
                service.Committed.TryGetBoosterStock(id, out int original);
                m_original.Add(id, original);
            }

            Publish(service, id, stock);
        }

        /// <summary>Puts back every value <see cref="Arrange"/> changed, after the test spent or kept it.</summary>
        public void Restore()
        {
            if (m_service != null)
            {
                foreach (KeyValuePair<BoosterId, int> original in m_original)
                {
                    Publish(m_service, original.Key, original.Value);
                }
            }

            m_original.Clear();
            m_service = null;
        }

        private static void Publish(PlayerSaveService service, BoosterId id, int stock)
        {
            PlayerSnapshot next = service.Committed.WithBoosterStock(id, stock).WithRevision(service.Committed.Revision + 1);
            s_publish.Invoke(service, new object[] { next });
        }
    }
}
