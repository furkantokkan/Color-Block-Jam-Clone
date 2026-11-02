namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// The file steps the player save uses, behind one port so tests can fail each step (AC-6.12 fault injection).
    /// SystemFileOperations is the only production implementation.
    /// </summary>
    public interface IFileOperations
    {
        bool Exists(string path);

        byte[] ReadAllBytes(string path);

        /// <summary>Creates or overwrites the file and flushes it to the storage device before returning.</summary>
        void WriteAndFlush(string path, byte[] bytes);

        /// <summary>Replaces destination with source and keeps the previous destination as backup.</summary>
        void Replace(string source, string destination, string backup);

        void Move(string source, string destination);

        void Delete(string path);

        void CreateDirectory(string path);
    }
}
