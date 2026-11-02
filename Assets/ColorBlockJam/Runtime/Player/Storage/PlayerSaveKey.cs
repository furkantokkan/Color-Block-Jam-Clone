using System;
using System.Security.Cryptography;
using System.Text;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// The two 32-byte keys of the player-save cipher: one for AES-256 and one for HMAC-SHA256. Derive runs
    /// PBKDF2-SHA256 (10,000 iterations) over the device ID with a constant app salt (design 4.5); the salt ships in
    /// the app, so the key stops casual reading, editing and copying to another device, not a determined attacker.
    /// </summary>
    public sealed class PlayerSaveKey
    {
        public const int KeySize = 32;
        public const int Iterations = 10000;

        private static readonly byte[] s_salt = Encoding.UTF8.GetBytes("ColorBlockJam.PlayerSave.v1");

        private readonly byte[] m_encryptionKey;
        private readonly byte[] m_macKey;

        public PlayerSaveKey(byte[] encryptionKey, byte[] macKey)
        {
            if (encryptionKey == null || encryptionKey.Length != KeySize)
            {
                throw new ArgumentException($"The encryption key must be {KeySize} bytes.", nameof(encryptionKey));
            }

            if (macKey == null || macKey.Length != KeySize)
            {
                throw new ArgumentException($"The MAC key must be {KeySize} bytes.", nameof(macKey));
            }

            m_encryptionKey = (byte[])encryptionKey.Clone();
            m_macKey = (byte[])macKey.Clone();
        }

        /// <summary>A copy of the AES-256 key.</summary>
        public byte[] GetEncryptionKey()
        {
            return (byte[])m_encryptionKey.Clone();
        }

        /// <summary>A copy of the HMAC-SHA256 key.</summary>
        public byte[] GetMacKey()
        {
            return (byte[])m_macKey.Clone();
        }

        /// <summary>Derives both keys from the device ID. Costly by design; the cipher runs it once, off the main thread.</summary>
        public static PlayerSaveKey Derive(string deviceId)
        {
            byte[] password = Encoding.UTF8.GetBytes(deviceId ?? string.Empty);
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, s_salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] material = pbkdf2.GetBytes(KeySize * 2);
                byte[] encryptionKey = new byte[KeySize];
                byte[] macKey = new byte[KeySize];
                Buffer.BlockCopy(material, 0, encryptionKey, 0, KeySize);
                Buffer.BlockCopy(material, KeySize, macKey, 0, KeySize);
                return new PlayerSaveKey(encryptionKey, macKey);
            }
        }
    }
}
