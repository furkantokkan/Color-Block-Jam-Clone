using System.IO;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>Performs the player-save file steps on the real file system with System.IO.</summary>
    public sealed class SystemFileOperations : IFileOperations
    {
        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public void WriteAndFlush(string path, byte[] bytes)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);

                // true also flushes the operating system buffers, so the bytes are on the device before the replace.
                stream.Flush(true);
            }
        }

        public void Replace(string source, string destination, string backup)
        {
            File.Replace(source, destination, backup);
        }

        public void Move(string source, string destination)
        {
            File.Move(source, destination);
        }

        public void Delete(string path)
        {
            File.Delete(path);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }
    }
}
