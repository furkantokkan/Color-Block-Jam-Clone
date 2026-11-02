using System;

namespace ColorBlockJam.Player.Lives
{
    /// <summary>The device clock. A player who moves it can move refills; tamper-proof time needs a server (DEC-174).</summary>
    public sealed class SystemUtcClock : IUtcClock
    {
        public long UtcNowSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
