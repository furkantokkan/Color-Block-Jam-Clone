using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// The booster side of a session test: throwaway definitions of all six boosters with the asset contexts
    /// (Time Freeze, Rocket, Hammer and Vacuum in-game, Starting Rocket and Extra Time pre-level, DEC-167), their catalog, the in-memory
    /// player ports and a purchase host. It creates coordinators and preparers over a SessionFixture and destroys
    /// everything it created.
    /// </summary>
    public sealed class BoosterFixture : IDisposable
    {
        // Enough editor frames for a yielded debit to run; the save has no real I/O.
        private const int k_maxFrames = 300;

        private readonly List<Object> m_created = new List<Object>();
        private readonly List<BoosterUseCoordinator> m_coordinators = new List<BoosterUseCoordinator>();

        public BoosterFixture(int initialStock = 1)
        {
            TimeFreeze = Create<TimeFreezeBoosterDefinition>(BoosterId.TimeFreeze, BoosterUseContext.InGame);
            Rocket = Create<TargetedBoosterDefinition>(BoosterId.Rocket, BoosterUseContext.InGame);
            StartingRocket = Create<TargetedBoosterDefinition>(BoosterId.StartingRocket, BoosterUseContext.PreLevel);
            Hammer = Create<TargetedBoosterDefinition>(BoosterId.Hammer, BoosterUseContext.InGame);
            Vacuum = Create<TargetedBoosterDefinition>(BoosterId.Vacuum, BoosterUseContext.InGame);
            ExtraTime = Create<ExtraTimeBoosterDefinition>(BoosterId.ExtraTime, BoosterUseContext.PreLevel);
            Catalog = Track(ScriptableObject.CreateInstance<BoosterCatalog>());
            SerializedObject catalog = new SerializedObject(Catalog);
            SerializedProperty definitions = catalog.FindProperty("m_definitions");
            BoosterDefinition[] all = { TimeFreeze, Rocket, StartingRocket, Hammer, Vacuum, ExtraTime };
            definitions.arraySize = all.Length;
            for (int index = 0; index < all.Length; index++)
            {
                definitions.GetArrayElementAtIndex(index).objectReferenceValue = all[index];
            }

            catalog.ApplyModifiedPropertiesWithoutUndo();
            Store = new FakeBoosterStore(initialStock);
            Host = new FakePurchaseHost();
        }

        public TimeFreezeBoosterDefinition TimeFreeze { get; }

        public TargetedBoosterDefinition Rocket { get; }

        public TargetedBoosterDefinition StartingRocket { get; }

        public TargetedBoosterDefinition Hammer { get; }

        public TargetedBoosterDefinition Vacuum { get; }

        public ExtraTimeBoosterDefinition ExtraTime { get; }

        public BoosterCatalog Catalog { get; }

        public FakeBoosterStore Store { get; }

        public FakePurchaseHost Host { get; }

        /// <summary>A coordinator over the fixture's session and targeting; the fixture must have been built with targeting.</summary>
        public BoosterUseCoordinator CreateCoordinator(SessionFixture session)
        {
            if (session.Targeting == null)
            {
                throw new InvalidOperationException("Build the session fixture with targeting for a coordinator.");
            }

            BoosterUseCoordinator coordinator = new BoosterUseCoordinator(session.Session, Store, Store, Store, Catalog,
                session.Targeting, Host);
            m_coordinators.Add(coordinator);
            return coordinator;
        }

        public StartingBoosterPreparer CreatePreparer()
        {
            return new StartingBoosterPreparer(Store, Store, Catalog);
        }

        /// <summary>Yields editor frames until the coordinator's debit reached the session.</summary>
        public static IEnumerator WaitForSave(BoosterUseCoordinator coordinator)
        {
            for (int frame = 0; frame < k_maxFrames && !coordinator.PendingSave.IsCompleted; frame++)
            {
                yield return null;
            }

            Assert.That(coordinator.PendingSave.IsCompleted, Is.True, "the debit finished in time");
        }

        public void Dispose()
        {
            for (int index = 0; index < m_coordinators.Count; index++)
            {
                m_coordinators[index].Dispose();
            }

            m_coordinators.Clear();
            Store.Dispose();
            for (int index = m_created.Count - 1; index >= 0; index--)
            {
                if (m_created[index] != null)
                {
                    Object.DestroyImmediate(m_created[index]);
                }
            }

            m_created.Clear();
        }

        private T Create<T>(BoosterId id, BoosterUseContext contexts) where T : BoosterDefinition
        {
            T definition = Track(ScriptableObject.CreateInstance<T>());
            definition.name = id.ToString();
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("m_id").intValue = (int)id;
            serialized.FindProperty("m_displayName").stringValue = id.ToString();
            serialized.FindProperty("m_useContexts").intValue = (int)contexts;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private T Track<T>(T created) where T : Object
        {
            m_created.Add(created);
            return created;
        }
    }
}
