namespace ColorBlockJam.Hub.Navigation
{
    /// <summary>
    /// Opens and closes Hub overlays as one stack, so presenters and Back input never show screens directly.
    /// </summary>
    public interface IHubNavigator
    {
        /// <summary>The number of overlays currently open.</summary>
        int OpenCount { get; }

        /// <summary>Opens Settings (task 3); false when it is not registered or already open.</summary>
        bool OpenSettings();

        /// <summary>Opens the pre-level popup (task 4); false when it is not registered or already open.</summary>
        bool OpenPreLevel();

        /// <summary>Pushes and shows a registered overlay; false when unregistered or already open.</summary>
        bool Open(HubOverlayId id);

        /// <summary>Closes the given overlay only when it is on top; false otherwise.</summary>
        bool Close(HubOverlayId id);

        /// <summary>Closes the top overlay; false when none is open (Back on Home does nothing).</summary>
        bool CloseTop();

        bool IsOpen(HubOverlayId id);
    }
}
