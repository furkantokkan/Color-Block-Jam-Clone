using System;

namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// The applied commands of one attempt, in order, each with its turn and Playing-time stamp
    /// (CommandRecord): the move history a later task saves as JSON and a replay plays back. Its array
    /// is allocated when the attempt is built, so logging allocates nothing; a full log stops recording
    /// and marks itself truncated instead of growing (NFR-1).
    /// </summary>
    public sealed class CommandLog
    {
        private readonly CommandRecord[] m_records;

        public CommandLog(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "The log needs room for at least one command.");
            }

            m_records = new CommandRecord[capacity];
        }

        public int Count { get; private set; }

        public int Capacity => m_records.Length;

        /// <summary>True when a command arrived after the log was full; the log then no longer replays the attempt.</summary>
        public bool IsTruncated { get; private set; }

        public GameCommand GetAt(int index)
        {
            return GetRecordAt(index).Command;
        }

        /// <summary>The logged command with its turn, tick and Playing seconds.</summary>
        public ref readonly CommandRecord GetRecordAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref m_records[index];
        }

        /// <summary>Copies the logged commands, without stamps, into a new array for a replay; it allocates, so call it outside the Playing tick.</summary>
        public GameCommand[] ToArray()
        {
            GameCommand[] copy = new GameCommand[Count];
            for (int i = 0; i < Count; i++)
            {
                copy[i] = m_records[i].Command;
            }

            return copy;
        }

        /// <summary>Copies the logged records with their stamps into a new array; it allocates, so call it outside the Playing tick.</summary>
        public CommandRecord[] ToRecords()
        {
            CommandRecord[] copy = new CommandRecord[Count];
            Array.Copy(m_records, copy, Count);
            return copy;
        }

        internal void Add(in GameCommand command, int tick, float seconds)
        {
            if (Count == m_records.Length)
            {
                IsTruncated = true;
                return;
            }

            m_records[Count] = new CommandRecord(command, Count + 1, tick, seconds);
            Count++;
        }
    }
}
