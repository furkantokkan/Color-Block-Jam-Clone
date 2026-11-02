using System;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// This tick's board events for the view sync phase, stored in an array allocated once per
    /// attempt. The session clears it at the start of every tick (design E), so it never grows.
    /// </summary>
    public sealed class BoardEventLog
    {
        private readonly BoardEvent[] m_events;

        public BoardEventLog(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "The log needs room for at least one event.");
            }

            m_events = new BoardEvent[capacity];
        }

        public int Count { get; private set; }

        public int Capacity => m_events.Length;

        public ref readonly BoardEvent GetAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref m_events[index];
        }

        /// <summary>Appends an event; a full log is a sizing bug, so it throws instead of dropping or growing.</summary>
        public void Add(in BoardEvent e)
        {
            ThrowIfNoRoom(1);

            m_events[Count] = e;
            Count++;
        }

        /// <summary>Throws the one full-log message when adding `needed` more events would exceed capacity, so a handler can check before its first board write.</summary>
        internal void ThrowIfNoRoom(int needed)
        {
            if (Count + needed > Capacity)
            {
                throw new InvalidOperationException("The board event log is full; clear it before the next command.");
            }
        }

        public void Clear()
        {
            Count = 0;
        }
    }
}
