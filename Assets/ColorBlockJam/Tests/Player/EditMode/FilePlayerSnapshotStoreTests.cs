using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Verifies the encrypted file store in a temporary directory: atomic write with a backup, recovery of the last
    /// committed snapshot after a fault at the temporary write, the flush or the replace, backup fallback for a
    /// corrupt main file, stale temporary cleanup, and the cipher container (player-data-currencies AC-5, AC-13).
    /// </summary>
    public sealed class FilePlayerSnapshotStoreTests
    {
        private string m_directory;
        private PlayerSaveCipher m_cipher;

        [SetUp]
        public void SetUp()
        {
            m_directory = PlayerTestFixtures.CreateTemporaryDirectory();
            m_cipher = new PlayerSaveCipher(CreateKey(1));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerTestFixtures.DeleteDirectory(m_directory);
        }

        [UnityTest]
        public IEnumerator ASaveReplacesTheFileAndKeepsThePreviousOneAsBackup()
        {
            FilePlayerSnapshotStore store = CreateStore(new SystemFileOperations());
            yield return PlayerTestFixtures.Await(store.SaveAsync(Snapshot(1, 10), CancellationToken.None));

            Assert.That(File.Exists(store.FilePath), Is.True);
            Assert.That(File.Exists(store.BackupPath), Is.False, "the first save moves the file into place");
            Assert.That(File.Exists(store.TemporaryPath), Is.False);

            yield return PlayerTestFixtures.Await(store.SaveAsync(Snapshot(2, 20), CancellationToken.None));

            Assert.That(ReadFile(store.FilePath).Currencies["coin"], Is.EqualTo(20));
            Assert.That(ReadFile(store.BackupPath).Currencies["coin"], Is.EqualTo(10));
            Assert.That(File.Exists(store.TemporaryPath), Is.False);

            Task<PlayerSnapshot> load = CreateStore(new SystemFileOperations()).LoadAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(load);
            Assert.That(load.Result.Revision, Is.EqualTo(2));
            Assert.That(load.Result.Currencies["coin"], Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator AnEmptyDirectoryLoadsNothing()
        {
            Task<PlayerSnapshot> load = CreateStore(new SystemFileOperations()).LoadAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(load);

            Assert.That(load.Result, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AFaultAtTheTemporaryWriteKeepsTheLastCommittedSnapshot()
        {
            return AssertFaultKeepsTheLastCommittedSnapshot(FileFault.TemporaryWrite);
        }

        [UnityTest]
        public IEnumerator AFaultAtTheFlushKeepsTheLastCommittedSnapshot()
        {
            return AssertFaultKeepsTheLastCommittedSnapshot(FileFault.Flush);
        }

        [UnityTest]
        public IEnumerator AFaultAtTheReplaceKeepsTheLastCommittedSnapshot()
        {
            return AssertFaultKeepsTheLastCommittedSnapshot(FileFault.Replace);
        }

        private IEnumerator AssertFaultKeepsTheLastCommittedSnapshot(FileFault fault)
        {
            FaultyFileOperations files = new FaultyFileOperations();
            FilePlayerSnapshotStore store = CreateStore(files);
            yield return PlayerTestFixtures.Await(store.SaveAsync(Snapshot(1, 10), CancellationToken.None));
            yield return PlayerTestFixtures.Await(store.SaveAsync(Snapshot(2, 20), CancellationToken.None));

            files.Fault = fault;
            Task failed = store.SaveAsync(Snapshot(3, 30), CancellationToken.None);
            yield return WaitFor(failed);
            Assert.That(failed.IsFaulted, Is.True, "the injected fault fails the save");
            Assert.That(failed.Exception.InnerException, Is.TypeOf<IOException>());

            Task<PlayerSnapshot> load = CreateStore(new SystemFileOperations()).LoadAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(load);

            Assert.That(load.Result.Revision, Is.EqualTo(2), "the complete previous snapshot loads");
            Assert.That(load.Result.Currencies["coin"], Is.EqualTo(20));
            Assert.That(File.Exists(store.TemporaryPath), Is.False, "the stale temporary file is deleted");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ACorruptMainFileLoadsTheBackupWithOneWarning()
        {
            FilePlayerSnapshotStore store = CreateStore(new SystemFileOperations());
            yield return PlayerTestFixtures.Await(store.SaveAsync(Snapshot(1, 10), CancellationToken.None));
            yield return PlayerTestFixtures.Await(store.SaveAsync(Snapshot(2, 20), CancellationToken.None));
            File.WriteAllText(store.FilePath, "not a save");

            LogAssert.Expect(LogType.Warning, new Regex("loaded from its backup"));
            Task<PlayerSnapshot> load = CreateStore(new SystemFileOperations()).LoadAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(load);

            Assert.That(load.Result.Revision, Is.EqualTo(1));
            Assert.That(load.Result.Currencies["coin"], Is.EqualTo(10));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AStaleTemporaryFileIsIgnoredAndDeleted()
        {
            FilePlayerSnapshotStore store = CreateStore(new SystemFileOperations());
            yield return PlayerTestFixtures.Await(store.SaveAsync(Snapshot(1, 10), CancellationToken.None));
            File.WriteAllBytes(store.TemporaryPath, m_cipher.Encrypt(PlayerSnapshotJson.Write(Snapshot(9, 90))));

            Task<PlayerSnapshot> load = CreateStore(new SystemFileOperations()).LoadAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(load);

            Assert.That(load.Result.Revision, Is.EqualTo(1), "an uncommitted temporary file is never read");
            Assert.That(File.Exists(store.TemporaryPath), Is.False);
        }

        [UnityTest]
        public IEnumerator TheFileIsAnEncryptedContainerWithAFreshIvPerSave()
        {
            FilePlayerSnapshotStore store = CreateStore(new SystemFileOperations());
            PlayerSnapshot snapshot = Snapshot(1, 10);
            yield return PlayerTestFixtures.Await(store.SaveAsync(snapshot, CancellationToken.None));
            byte[] first = File.ReadAllBytes(store.FilePath);
            yield return PlayerTestFixtures.Await(store.SaveAsync(snapshot, CancellationToken.None));
            byte[] second = File.ReadAllBytes(store.FilePath);

            Assert.That(Encoding.ASCII.GetString(first, 0, 4), Is.EqualTo("CBJS"));
            Assert.That(first[4], Is.EqualTo(PlayerSaveCipher.ContainerVersion));
            string text = Encoding.UTF8.GetString(first);
            Assert.That(text, Does.Not.Contain("formatVersion").And.Not.Contain("\"coin\"").And.Not.Contain("revision"));
            Assert.That(second, Is.Not.EqualTo(first), "the same snapshot encrypts differently each save");
        }

        [Test]
        public void TheCipherRoundTripsEverySnapshot()
        {
            string[] samples =
            {
                PlayerSnapshotJson.Write(PlayerSnapshot.Empty),
                PlayerSnapshotJson.Write(Snapshot(5, 999999999)),
                PlayerSnapshotJsonTests.UnknownKeysJson,
                PlayerSnapshotJson.Write(Snapshot(1, 0).WithSettings(new PlayerSettings(false, false, false)))
            };

            for (int index = 0; index < samples.Length; index++)
            {
                byte[] container = m_cipher.Encrypt(samples[index]);
                Assert.That(m_cipher.TryDecrypt(container, out string json), Is.True);
                Assert.That(json, Is.EqualTo(samples[index]));
            }
        }

        [Test]
        public void TamperedTruncatedOrForeignContainersAreRejectedWithoutAnException()
        {
            byte[] container = m_cipher.Encrypt(PlayerSnapshotJson.Write(Snapshot(1, 10)));
            for (int index = 0; index < container.Length; index += 7)
            {
                byte[] flipped = (byte[])container.Clone();
                flipped[index] ^= 0x01;
                Assert.That(m_cipher.TryDecrypt(flipped, out _), Is.False, $"byte {index} flipped");
            }

            byte[] truncated = new byte[container.Length - 1];
            System.Array.Copy(container, truncated, truncated.Length);
            Assert.That(m_cipher.TryDecrypt(truncated, out _), Is.False);
            Assert.That(m_cipher.TryDecrypt(new byte[10], out _), Is.False);
            Assert.That(m_cipher.TryDecrypt(null, out _), Is.False);
            Assert.That(new PlayerSaveCipher(CreateKey(2)).TryDecrypt(container, out _), Is.False, "another device key");
        }

        [Test]
        public void TheDeviceKeyIsStablePerDeviceAndDiffersBetweenDevices()
        {
            PlayerSaveKey first = PlayerSaveKey.Derive("device-a");
            PlayerSaveKey again = PlayerSaveKey.Derive("device-a");
            PlayerSaveKey other = PlayerSaveKey.Derive("device-b");

            Assert.That(again.GetEncryptionKey(), Is.EqualTo(first.GetEncryptionKey()));
            Assert.That(again.GetMacKey(), Is.EqualTo(first.GetMacKey()));
            Assert.That(first.GetMacKey(), Is.Not.EqualTo(first.GetEncryptionKey()));
            Assert.That(other.GetEncryptionKey(), Is.Not.EqualTo(first.GetEncryptionKey()));

            PlayerSaveCipher device = PlayerSaveCipher.FromDeviceId("device-a");
            byte[] container = device.Encrypt("{}");
            Assert.That(new PlayerSaveCipher(first).TryDecrypt(container, out string json) && json == "{}", Is.True);
            Assert.That(PlayerSaveCipher.FromDeviceId("device-b").TryDecrypt(container, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator AnUnreadableSaveTriesTheBackupThenStartsFromDefaultsWithOneWarning()
        {
            FilePlayerSnapshotStore writer = CreateStore(new SystemFileOperations());
            yield return PlayerTestFixtures.Await(writer.SaveAsync(Snapshot(1, 10), CancellationToken.None));
            yield return PlayerTestFixtures.Await(writer.SaveAsync(Snapshot(2, 20), CancellationToken.None));
            byte[] main = File.ReadAllBytes(writer.FilePath);
            main[main.Length / 2] ^= 0x10;
            File.WriteAllBytes(writer.FilePath, main);

            using (PlayerTestFixtures fixtures = new PlayerTestFixtures())
            {
                CurrencyDefinition coin = fixtures.CreateCurrency("coin", "Coins", 0, 999999999);
                CurrencyCatalog currencies = fixtures.CreateCatalog(coin);
                BoosterCatalog boosters = fixtures.CreateBoosterCatalog(fixtures.CreateBooster(BoosterId.Rocket, 1, 10, coin));

                // A flipped byte in the main file: the backup loads.
                LogAssert.Expect(LogType.Warning, new Regex("loaded from its backup"));
                PlayerSaveService fromBackup = new PlayerSaveService(CreateStore(new SystemFileOperations()), currencies, boosters);
                yield return PlayerTestFixtures.Await(fromBackup.LoadAsync(CancellationToken.None));
                Assert.That(fromBackup.GetBalance(coin).Value, Is.EqualTo(10));
                fromBackup.Dispose();

                // Another device's key: neither file authenticates, so defaults load and are saved.
                LogAssert.Expect(LogType.Warning, new Regex("cannot be read"));
                FilePlayerSnapshotStore foreign = new FilePlayerSnapshotStore(m_directory, new SystemFileOperations(),
                    new PlayerSaveCipher(CreateKey(3)));
                PlayerSaveService fromDefaults = new PlayerSaveService(foreign, currencies, boosters);
                yield return PlayerTestFixtures.Await(fromDefaults.LoadAsync(CancellationToken.None));
                Assert.That(fromDefaults.GetBalance(coin).Value, Is.EqualTo(0));
                Assert.That(fromDefaults.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1));
                Assert.That(fromDefaults.WritesRefused, Is.False);
                fromDefaults.Dispose();
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator ANewerFormatInTheFileIsNeverOverwritten()
        {
            string newer = PlayerSnapshotJsonTests.UnknownKeysJson.Replace("\"formatVersion\": 1", "\"formatVersion\": 2");
            Directory.CreateDirectory(m_directory);
            FilePlayerSnapshotStore store = CreateStore(new SystemFileOperations());
            byte[] original = m_cipher.Encrypt(newer);
            File.WriteAllBytes(store.FilePath, original);

            using (PlayerTestFixtures fixtures = new PlayerTestFixtures())
            {
                CurrencyDefinition coin = fixtures.CreateCurrency("coin", "Coins", 0, 999999999);
                PlayerSaveService service = new PlayerSaveService(store, fixtures.CreateCatalog(coin),
                    fixtures.CreateBoosterCatalog(fixtures.CreateBooster(BoosterId.Rocket, 1, 10, coin)));
                LogAssert.Expect(LogType.Error, new Regex("format version 2"));
                yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

                Assert.That(service.WritesRefused, Is.True);
                Assert.That(File.ReadAllBytes(store.FilePath), Is.EqualTo(original));
                Assert.That(File.Exists(store.BackupPath), Is.False);
                service.Dispose();
            }
        }

        private FilePlayerSnapshotStore CreateStore(IFileOperations files)
        {
            return new FilePlayerSnapshotStore(m_directory, files, m_cipher);
        }

        private PlayerSnapshot ReadFile(string path)
        {
            Assert.That(m_cipher.TryDecrypt(File.ReadAllBytes(path), out string json), Is.True, path);
            return PlayerSnapshotJson.Read(json);
        }

        private static PlayerSnapshot Snapshot(long revision, int coins)
        {
            return new PlayerSnapshot(revision, 0, new Dictionary<string, int> { ["life"] = 5, ["coin"] = coins },
                new Dictionary<string, int> { ["Rocket"] = 1 }, PlayerSettings.Default);
        }

        private static PlayerSaveKey CreateKey(byte seed)
        {
            byte[] encryption = new byte[PlayerSaveKey.KeySize];
            byte[] mac = new byte[PlayerSaveKey.KeySize];
            for (int index = 0; index < encryption.Length; index++)
            {
                encryption[index] = (byte)(seed + index);
                mac[index] = (byte)(seed * 31 + index * 7);
            }

            return new PlayerSaveKey(encryption, mac);
        }

        private static IEnumerator WaitFor(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the task finished in time");
        }
    }
}
