namespace ColorBlockJam.Hub.Navigation
{
    /// <summary>
    /// Opens Hub menus by string ID, so a tab names its menu in data and a new menu needs no navigator or presenter
    /// change (DEC-169). Menus share the overlay stack with Settings and the pre-level popup, so Back closes them.
    /// </summary>
    public interface IHubMenuNavigator
    {
        /// <summary>True when a menu is registered under <paramref name="menuId"/>.</summary>
        bool HasMenu(string menuId);

        /// <summary>Pushes and shows the menu; false when it is not registered or already open.</summary>
        bool OpenMenu(string menuId);
    }
}
