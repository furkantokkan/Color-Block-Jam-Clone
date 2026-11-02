namespace ColorBlockJam.Runtime.Currencies
{
    /// <summary>
    /// The stable save IDs of the two shipped currencies (DEC-128), declared once so Player, Hub and Gameplay code
    /// reference the same string instead of redeclaring it in each assembly.
    /// </summary>
    public static class CurrencyIds
    {
        public const string Coin = "coin";
        public const string Life = "life";
    }
}
