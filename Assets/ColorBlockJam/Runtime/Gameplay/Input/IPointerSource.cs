namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>
    /// The gameplay pointer port, polled once per Playing tick (DEC-114). The session enables it only
    /// while Playing; tests use a fake.
    /// </summary>
    public interface IPointerSource
    {
        void Enable();

        void Disable();

        PointerSample Read();
    }
}
