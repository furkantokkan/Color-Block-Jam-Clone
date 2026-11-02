using System;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// Raised when saved player data declares a formatVersion newer than this build reads, so the service keeps
    /// defaults in memory and refuses writes instead of overwriting newer data (design 4.2).
    /// </summary>
    public sealed class PlayerSnapshotVersionException : Exception
    {
        public PlayerSnapshotVersionException(long formatVersion)
            : base($"The player save uses format version {formatVersion}; this build reads version "
                   + $"{PlayerSnapshotJson.FormatVersion} or older.")
        {
            FormatVersion = formatVersion;
        }

        public long FormatVersion { get; }
    }
}
