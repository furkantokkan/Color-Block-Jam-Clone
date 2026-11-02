using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Builds throwaway currency and booster definitions, catalogs and icons for the player-data tests through
    /// SerializedObject, builds save services and seed JSON the tests share, and disposes every service and
    /// destroys every object it created.
    /// </summary>
    internal sealed class PlayerTestFixtures : IDisposable
    {
        private const float k_timeoutSeconds = 10f;

        /// <summary>The revision every <see cref="Seed"/> snapshot starts at, so a seed that lists every catalog
        /// key makes the load write nothing and the caller's first write moves the revision to SeedRevision + 1.</summary>
        public const long SeedRevision = 3;

        private readonly List<Object> m_created = new List<Object>();
        private readonly List<PlayerSaveService> m_services = new List<PlayerSaveService>();

        public Sprite CreateIcon()
        {
            Texture2D texture = Track(new Texture2D(4, 4));
            return Track(Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)));
        }

        public CurrencyDefinition CreateCurrency(string id, string displayName, int startingAmount, int maxAmount,
            CurrencyDisplay display = CurrencyDisplay.Amount, bool withIcon = true)
        {
            CurrencyDefinition currency = Track(ScriptableObject.CreateInstance<CurrencyDefinition>());
            currency.name = displayName;
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(currency);
            serialized.FindProperty("m_id").stringValue = id;
            serialized.FindProperty("m_displayName").stringValue = displayName;
            serialized.FindProperty("m_icon").objectReferenceValue = withIcon ? CreateIcon() : null;
            serialized.FindProperty("m_startingAmount").intValue = startingAmount;
            serialized.FindProperty("m_maxAmount").intValue = maxAmount;
            serialized.FindProperty("m_homeDisplay").intValue = (int)display;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return currency;
        }

        public CurrencyCatalog CreateCatalog(params CurrencyDefinition[] currencies)
        {
            CurrencyCatalog catalog = Track(ScriptableObject.CreateInstance<CurrencyCatalog>());
            SetArray(catalog, "m_definitions", currencies);
            return catalog;
        }

        public BoosterDefinition CreateBooster(BoosterId id, int initialCharges, int price, CurrencyDefinition currency,
            int purchaseAmount = 1)
        {
            ExtraTimeBoosterDefinition booster = Track(ScriptableObject.CreateInstance<ExtraTimeBoosterDefinition>());
            booster.name = id.ToString();
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(booster);
            serialized.FindProperty("m_id").intValue = (int)id;
            serialized.FindProperty("m_displayName").stringValue = id.ToString();
            serialized.FindProperty("m_icon").objectReferenceValue = CreateIcon();
            serialized.FindProperty("m_useContexts").intValue = (int)BoosterUseContext.InGame;
            serialized.FindProperty("m_initialCharges").intValue = initialCharges;
            serialized.FindProperty("m_purchasePrice").intValue = price;
            serialized.FindProperty("m_purchaseCurrency").objectReferenceValue = currency;
            serialized.FindProperty("m_purchaseAmount").intValue = purchaseAmount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return booster;
        }

        public BoosterCatalog CreateBoosterCatalog(params BoosterDefinition[] boosters)
        {
            BoosterCatalog catalog = Track(ScriptableObject.CreateInstance<BoosterCatalog>());
            SetArray(catalog, "m_definitions", boosters);
            return catalog;
        }

        /// <summary>Creates a save service against the given store and disposes it with this fixture.</summary>
        public PlayerSaveService CreateService(IPlayerSnapshotStore store, CurrencyCatalog currencies,
            BoosterCatalog boosters)
        {
            PlayerSaveService service = new PlayerSaveService(store, currencies, boosters);
            m_services.Add(service);
            return service;
        }

        /// <summary>
        /// Builds seed JSON at <see cref="SeedRevision"/> through <see cref="PlayerSnapshotJson.Write"/> instead of
        /// hand-concatenated strings, so every seeded save round-trips through the same rules the service reads.
        /// </summary>
        public static string Seed(int currentLevelIndex, IReadOnlyDictionary<string, int> currencies,
            IReadOnlyDictionary<string, int> boosterStock)
        {
            PlayerSnapshot snapshot = new PlayerSnapshot(SeedRevision, currentLevelIndex, currencies, boosterStock,
                PlayerSettings.Default);
            return PlayerSnapshotJson.Write(snapshot);
        }

        public void Dispose()
        {
            // A gated save ignores its cancellation token and holds the service lock, so a caller releases any
            // pending gate on its stores before this dispose runs.
            for (int index = 0; index < m_services.Count; index++)
            {
                m_services[index].Dispose();
            }

            m_services.Clear();

            for (int index = m_created.Count - 1; index >= 0; index--)
            {
                if (m_created[index] != null)
                {
                    Object.DestroyImmediate(m_created[index]);
                }
            }

            m_created.Clear();
        }

        public static IEnumerator Await(Task task)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the task finished in time");
            task.GetAwaiter().GetResult();
        }

        public static string CreateTemporaryDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "cbj-player-save-" + Guid.NewGuid().ToString("N"));
        }

        public static void DeleteDirectory(string directory)
        {
            if (directory != null && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private T Track<T>(T created) where T : Object
        {
            m_created.Add(created);
            return created;
        }

        private static void SetArray(Object target, string property, Object[] values)
        {
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(target);
            UnityEditor.SerializedProperty array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// A store for service tests: the same JSON rules as the memory store, with a gate that holds saves open and a
    /// switch that makes saves fail.
    /// </summary>
    internal sealed class ScriptedPlayerSnapshotStore : IPlayerSnapshotStore
    {
        public ScriptedPlayerSnapshotStore(string json = null)
        {
            Inner = new MemoryPlayerSnapshotStore(json);
        }

        public MemoryPlayerSnapshotStore Inner { get; }

        /// <summary>When set, each save waits for this task before it completes.</summary>
        public TaskCompletionSource<bool> Gate { get; set; }

        public bool FailSaves { get; set; }

        public int SaveAttempts { get; private set; }

        public Task<PlayerSnapshot> LoadAsync(CancellationToken cancellationToken)
        {
            return Inner.LoadAsync(cancellationToken);
        }

        public async Task SaveAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken)
        {
            SaveAttempts++;
            if (Gate != null)
            {
                await Gate.Task;
            }

            if (FailSaves)
            {
                throw new IOException("Injected save failure.");
            }

            await Inner.SaveAsync(snapshot, cancellationToken);
        }
    }

    /// <summary>The file steps at which FaultyFileOperations fails.</summary>
    internal enum FileFault
    {
        None,
        TemporaryWrite,
        Flush,
        Replace
    }

    /// <summary>
    /// Real file operations that fail at one chosen step: a torn temporary write, a write whose flush fails, or a
    /// replace that fails before it changes anything (AC-6.12 fault injection).
    /// </summary>
    internal sealed class FaultyFileOperations : IFileOperations
    {
        private readonly SystemFileOperations m_system = new SystemFileOperations();

        public FileFault Fault { get; set; }

        public bool Exists(string path)
        {
            return m_system.Exists(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            return m_system.ReadAllBytes(path);
        }

        public void WriteAndFlush(string path, byte[] bytes)
        {
            if (Fault == FileFault.TemporaryWrite)
            {
                byte[] torn = new byte[bytes.Length / 2];
                Array.Copy(bytes, torn, torn.Length);
                File.WriteAllBytes(path, torn);
                throw new IOException("Injected fault during the temporary write.");
            }

            if (Fault == FileFault.Flush)
            {
                File.WriteAllBytes(path, bytes);
                throw new IOException("Injected fault during the flush.");
            }

            m_system.WriteAndFlush(path, bytes);
        }

        public void Replace(string source, string destination, string backup)
        {
            if (Fault == FileFault.Replace)
            {
                throw new IOException("Injected fault during the replace.");
            }

            m_system.Replace(source, destination, backup);
        }

        public void Move(string source, string destination)
        {
            m_system.Move(source, destination);
        }

        public void Delete(string path)
        {
            m_system.Delete(path);
        }

        public void CreateDirectory(string path)
        {
            m_system.CreateDirectory(path);
        }
    }
}
