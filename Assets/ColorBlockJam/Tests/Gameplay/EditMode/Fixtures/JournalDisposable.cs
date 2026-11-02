using System;
using System.Collections.Generic;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>Journals its disposal, so a test sees when Teardown disposes the attempt's disposables.</summary>
    public sealed class JournalDisposable : IDisposable
    {
        private readonly List<string> m_journal;
        private readonly string m_entry;

        public JournalDisposable(List<string> journal, string entry)
        {
            m_journal = journal;
            m_entry = entry;
        }

        public void Dispose()
        {
            m_journal.Add(m_entry);
        }
    }
}
