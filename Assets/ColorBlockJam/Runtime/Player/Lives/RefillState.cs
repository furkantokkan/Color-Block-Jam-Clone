namespace ColorBlockJam.Player.Lives
{
    /// <summary>One currency balance after a refill step, with the start of the unit that is refilling now.</summary>
    public readonly struct RefillState
    {
        public RefillState(int amount, bool isRefilling, long startUtcSeconds)
        {
            Amount = amount;
            IsRefilling = isRefilling;
            StartUtcSeconds = startUtcSeconds;
        }

        public int Amount { get; }

        /// <summary>True while the balance is below its maximum and a timer runs.</summary>
        public bool IsRefilling { get; }

        /// <summary>UTC Unix seconds at which the unit now refilling started; 0 when not refilling.</summary>
        public long StartUtcSeconds { get; }
    }
}
