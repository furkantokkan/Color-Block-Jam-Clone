using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// Encrypts and authenticates the player-save JSON (design 4.5): AES-256-CBC with PKCS7 and a random IV per
    /// save, then HMAC-SHA256 over magic | version | IV | ciphertext, in the container
    /// "CBJS" | 1 | IV(16) | ciphertext | MAC(32). The MAC is checked in constant time before decryption; any wrong
    /// magic, version, length, MAC or padding makes TryDecrypt return false and never throws.
    /// </summary>
    public sealed class PlayerSaveCipher
    {
        public const byte ContainerVersion = 1;

        private const int k_magicLength = 4;
        private const int k_headerLength = k_magicLength + 1;
        private const int k_ivLength = 16;
        private const int k_blockLength = 16;
        private const int k_macLength = 32;

        private static readonly byte[] s_magic = { (byte)'C', (byte)'B', (byte)'J', (byte)'S' };
        private static readonly UTF8Encoding s_encoding = new UTF8Encoding(false, true);

        // Derived on first use, inside the store's thread-pool work, so PBKDF2 never runs on the main thread.
        private readonly Lazy<PlayerSaveKey> m_key;

        public PlayerSaveCipher(PlayerSaveKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            m_key = new Lazy<PlayerSaveKey>(() => key, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private PlayerSaveCipher(string deviceId)
        {
            m_key = new Lazy<PlayerSaveKey>(() => PlayerSaveKey.Derive(deviceId), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>A cipher whose key is derived from the device ID once, on its first encryption or decryption.</summary>
        public static PlayerSaveCipher FromDeviceId(string deviceId)
        {
            return new PlayerSaveCipher(deviceId);
        }

        public byte[] Encrypt(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            PlayerSaveKey key = m_key.Value;
            byte[] plain = s_encoding.GetBytes(json);
            byte[] iv = new byte[k_ivLength];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(iv);
            }

            byte[] cipherText;
            using (Aes aes = CreateAes(key))
            using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, iv))
            {
                cipherText = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            }

            byte[] container = new byte[k_headerLength + k_ivLength + cipherText.Length + k_macLength];
            Buffer.BlockCopy(s_magic, 0, container, 0, k_magicLength);
            container[k_magicLength] = ContainerVersion;
            Buffer.BlockCopy(iv, 0, container, k_headerLength, k_ivLength);
            Buffer.BlockCopy(cipherText, 0, container, k_headerLength + k_ivLength, cipherText.Length);
            int macOffset = container.Length - k_macLength;
            byte[] mac = ComputeMac(key, container, macOffset);
            Buffer.BlockCopy(mac, 0, container, macOffset, k_macLength);
            return container;
        }

        /// <summary>The JSON inside an authentic container, or false for anything else (never throws).</summary>
        public bool TryDecrypt(byte[] container, out string json)
        {
            json = null;
            int minimumLength = k_headerLength + k_ivLength + k_blockLength + k_macLength;
            if (container == null || container.Length < minimumLength)
            {
                return false;
            }

            int cipherLength = container.Length - k_headerLength - k_ivLength - k_macLength;
            if (cipherLength % k_blockLength != 0)
            {
                return false;
            }

            for (int index = 0; index < k_magicLength; index++)
            {
                if (container[index] != s_magic[index])
                {
                    return false;
                }
            }

            if (container[k_magicLength] != ContainerVersion)
            {
                return false;
            }

            try
            {
                PlayerSaveKey key = m_key.Value;
                int macOffset = container.Length - k_macLength;
                byte[] expected = ComputeMac(key, container, macOffset);
                if (!FixedTimeEquals(expected, container, macOffset))
                {
                    return false;
                }

                byte[] iv = new byte[k_ivLength];
                Buffer.BlockCopy(container, k_headerLength, iv, 0, k_ivLength);
                using (Aes aes = CreateAes(key))
                using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, iv))
                {
                    byte[] plain = decryptor.TransformFinalBlock(container, k_headerLength + k_ivLength, cipherLength);
                    json = s_encoding.GetString(plain);
                    return true;
                }
            }
            catch (Exception exception) when (exception is CryptographicException || exception is ArgumentException)
            {
                // DecoderFallbackException (invalid UTF-8) is an ArgumentException.
                json = null;
                return false;
            }
        }

        private static Aes CreateAes(PlayerSaveKey key)
        {
            Aes aes = Aes.Create();
            aes.KeySize = PlayerSaveKey.KeySize * 8;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key.GetEncryptionKey();
            return aes;
        }

        private static byte[] ComputeMac(PlayerSaveKey key, byte[] container, int length)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key.GetMacKey()))
            {
                return hmac.ComputeHash(container, 0, length);
            }
        }

        // Compares every byte regardless of where the first difference is, so timing reveals nothing about the MAC.
        private static bool FixedTimeEquals(byte[] expected, byte[] container, int offset)
        {
            int difference = 0;
            for (int index = 0; index < k_macLength; index++)
            {
                difference |= expected[index] ^ container[offset + index];
            }

            return difference == 0;
        }
    }
}
