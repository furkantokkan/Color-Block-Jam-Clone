namespace ColorBlockJam.Runtime.Currencies
{
    /// <summary>
    /// How the Home top bar shows one currency: the compact balance in its holder (Coins), or the count on its icon
    /// with a status text in the holder (Lives). Stored by value in currency assets; never renumber.
    /// </summary>
    public enum CurrencyDisplay
    {
        Amount = 0,
        CountOnIcon = 1
    }
}
