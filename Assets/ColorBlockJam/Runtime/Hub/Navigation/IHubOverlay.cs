namespace ColorBlockJam.Hub.Navigation
{
    /// <summary>
    /// One Hub overlay the navigator shows and hides; its presenter owns everything inside it.
    /// </summary>
    public interface IHubOverlay
    {
        /// <summary>Shows the overlay above everything below it; its scrim blocks input underneath.</summary>
        void Show();

        /// <summary>Hides the overlay and stops its own presentation work.</summary>
        void Hide();
    }
}
