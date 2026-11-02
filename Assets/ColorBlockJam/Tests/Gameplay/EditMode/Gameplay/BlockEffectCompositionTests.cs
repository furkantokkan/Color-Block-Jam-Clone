using System;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    public sealed class BlockEffectCompositionTests
    {
        private LevelData m_authored;
        private AttemptLevel m_level;

        [SetUp]
        public void SetUp()
        {
            m_authored = new LevelData("ice-test", "Ice", 3, 3, 60, Array.Empty<LevelDoorData>(), new[]
            {
                new LevelBlockData("a", "Red", Vector2Int.zero, new[] { Vector2Int.zero },
                    new[] { new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw", "2") }) }),
                new LevelBlockData("b", "Blue", new Vector2Int(2, 2), new[] { Vector2Int.zero })
            });
            Assert.That(BoardLayout.TryCreate(m_authored, out BoardLayout layout, out string error), Is.True, error);
            Assert.That(AttemptLevelConverter.TryConvert(m_authored, layout, out m_level, out error), Is.True, error);
        }

        [Test]
        public void RestartPublishesFreshStateWithoutChangingAuthoredMetadata()
        {
            var source = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(null) });
            source.Prepare(m_level, m_authored);
            var hooks = new EffectAttemptHookSource(source);
            hooks.Create(m_level);
            BlockEffectAttempt first = source.CurrentAttempt;
            ((IceEffectState)first.GetAt(0)).Rules.OnBlockExited(new BlockExitedEvent(1, 0, default, default, ExitOutcome.Leave));
            Assert.That(((IceEffectState)first.GetAt(0)).Rules.GetRemaining(0), Is.EqualTo(1));
            hooks.Create(m_level);
            Assert.That(source.CurrentAttempt.Generation, Is.GreaterThan(first.Generation));
            Assert.That(source.CurrentAttempt.GetAt(0), Is.Not.SameAs(first.GetAt(0)));
            Assert.That(((IceEffectState)source.CurrentAttempt.GetAt(0)).Rules.GetRemaining(0), Is.EqualTo(2));
            Assert.That(m_authored.GetBlockAt(0).TryGetEffect(out IceEffect authored), Is.True);
            Assert.That(authored.ClearsToThaw, Is.EqualTo(2));
        }

        [Test]
        public void LaterModuleFailureCannotPublishOrKeepPreviousAttempt()
        {
            var failing = new FailingModule();
            var source = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(null), failing });
            source.Prepare(m_level, m_authored);
            var hooks = new EffectAttemptHookSource(source);
            hooks.Create(m_level);
            failing.Fail = true;
            Assert.Throws<InvalidOperationException>(() => hooks.Create(m_level));
            Assert.That(source.CurrentAttempt, Is.Null);
        }

        [Test]
        public void EqualIdDoesNotAuthorizeDifferentLevelIdentityAndPrepareFailureClearsState()
        {
            var source = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(null) });
            source.Prepare(m_level, m_authored);
            var hooks = new EffectAttemptHookSource(source);
            hooks.Create(m_level);
            Assert.That(AttemptLevelConverter.TryConvert(m_authored, m_level.Layout, out AttemptLevel other, out _), Is.True);
            Assert.Throws<InvalidOperationException>(() => hooks.Create(other));
            Assert.That(source.CurrentAttempt, Is.Null);
            Assert.Throws<ArgumentNullException>(() => source.Prepare(m_level, null));
            Assert.That(source.Context, Is.Null);
        }

        private sealed class FailingModule : IBlockEffectModule
        {
            public bool Fail;
            public string TypeId => "test";
            public IBlockEffectLevel Read(LevelData authored) => new Metadata(authored.BlockCount);
            public IBlockEffectState AddHooks(IBlockEffectLevel level, AttemptHooksBuilder hooks)
                => Fail ? throw new InvalidOperationException("Injected later module failure") : new State();
            public IBlockEffectView CreateView(BlockEffectViewContext context) => null;
            private sealed class Metadata : IBlockEffectLevel
            {
                public Metadata(int count) { BlockCount = count; }
                public string TypeId => "test";
                public int BlockCount { get; }
            }
            private sealed class State : IBlockEffectState { public string TypeId => "test"; }
        }
    }
}
