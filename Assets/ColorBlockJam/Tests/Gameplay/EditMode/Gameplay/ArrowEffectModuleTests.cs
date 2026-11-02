using System;
using System.Collections;
using System.Reflection;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The Arrow effect module (BLK-01, docs/Block-Effects-Architecture.md): Read copies each authored block's axis in
    /// converted block order on Home, AddHooks binds the axis constraint and the exit gate only when some block has an
    /// axis and returns a state with no changing values, and the registered module list (Ice before Arrow) publishes
    /// the Arrow hooks through the Game scene's EffectAttemptHookSource for every attempt.
    /// </summary>
    public sealed class ArrowEffectModuleTests
    {
        [Test]
        public void ReadCopiesEachBlocksAxisInConvertedOrder()
        {
            ArrowEffectModule module = new ArrowEffectModule();
            ArrowEffectLevel level = (ArrowEffectLevel)module.Read(CreateLevel());

            Assert.That(module.TypeId, Is.EqualTo("arrow"));
            Assert.That(level.TypeId, Is.EqualTo("arrow"));
            Assert.That(level.BlockCount, Is.EqualTo(3));
            Assert.That(level.HasArrow, Is.True);
            Assert.That(level.TryGetAxis(0, out LevelEffectAxis first), Is.True);
            Assert.That(first, Is.EqualTo(LevelEffectAxis.Horizontal), "the I3");
            Assert.That(level.TryGetAxis(1, out _), Is.False, "the plain single has no axis");
            Assert.That(level.TryGetAxis(2, out LevelEffectAxis third), Is.True);
            Assert.That(third, Is.EqualTo(LevelEffectAxis.Vertical), "the L3");
            Assert.That(module.Read(CreateLevel(withArrows: false)) is ArrowEffectLevel plain && !plain.HasArrow, Is.True);
        }

        [Test]
        public void AddHooksBindsTheAxisConstraintAndTheExitGateOnlyWhenSomeBlockHasAnAxis()
        {
            ArrowEffectModule module = new ArrowEffectModule();

            AttemptHooksBuilder plain = new AttemptHooksBuilder();
            IBlockEffectState plainState = module.AddHooks(module.Read(CreateLevel(withArrows: false)), plain);
            Assert.That(plainState, Is.TypeOf<ArrowEffectState>());
            Assert.That(plainState.TypeId, Is.EqualTo("arrow"));
            Assert.That(Count(plain, "m_axisConstraints"), Is.Zero, "a level without Arrow binds no hook");
            Assert.That(Count(plain, "m_exitGates"), Is.Zero);

            AttemptHooksBuilder arrows = new AttemptHooksBuilder();
            module.AddHooks(module.Read(CreateLevel()), arrows);
            Assert.That(Count(arrows, "m_axisConstraints"), Is.EqualTo(1));
            Assert.That(Count(arrows, "m_exitGates"), Is.EqualTo(1));
            Assert.That(Count(arrows, "m_dragGates"), Is.Zero, "an Arrow block is never frozen");
            Assert.That(Count(arrows, "m_listeners"), Is.Zero, "the axes are fixed, so nothing listens");

            Assert.Throws<ArgumentException>(() => module.AddHooks(new IceEffectLevel(new int[3]), new AttemptHooksBuilder()));
        }

        [Test]
        public void TheRegisteredListPublishesTheArrowHooksForEveryAttempt()
        {
            LevelData authored = CreateLevel();
            AttemptLevel level = Convert(authored);
            BlockEffectContextSource source = new BlockEffectContextSource(
                new IBlockEffectModule[] { new IceEffectModule(null), new ArrowEffectModule() });
            source.Prepare(level, authored);
            EffectAttemptHookSource hooks = new EffectAttemptHookSource(source);

            AttemptHooks first = hooks.Create(level);
            BlockEffectAttempt attempt = source.CurrentAttempt;
            Assert.That(attempt.GetAt(1), Is.TypeOf<ArrowEffectState>(), "Arrow follows Ice in the list");
            BoardState board = new BoardState(level.Model);
            Assert.That(first.GetAxes(board, 0), Is.EqualTo(AxisMask.Horizontal));
            Assert.That(first.GetAxes(board, 1), Is.EqualTo(AxisMask.Both));
            Assert.That(first.GetAxes(board, 2), Is.EqualTo(AxisMask.Vertical));

            // The I3 at (1, 4) sits flush below a matching Top door: perpendicular to its Horizontal axis.
            Assert.That(ExitRules.Evaluate(board, first, 0, board.GetOrigin(0), Side.Top, out ExitCandidate _),
                Is.EqualTo(ExitVerdict.Vetoed));
            Assert.That(ExitRules.Evaluate(board, AttemptHooks.CreateEmpty(), 0, board.GetOrigin(0), Side.Top,
                out ExitCandidate _), Is.EqualTo(ExitVerdict.Fits));

            // Restart: a fresh attempt reads the same fixed axes.
            AttemptHooks restarted = hooks.Create(level);
            Assert.That(source.CurrentAttempt.Generation, Is.GreaterThan(attempt.Generation));
            Assert.That(restarted.GetAxes(new BoardState(level.Model), 0), Is.EqualTo(AxisMask.Horizontal));
            Assert.That(authored.GetBlockAt(0).TryGetEffect(out ArrowEffect arrow), Is.True);
            Assert.That(arrow.Axis, Is.EqualTo(LevelEffectAxis.Horizontal), "the authored data is unchanged");
        }

        // A 6 x 6 board: block 0 a red Horizontal I3 at (1, 5) under a red Top door over columns 1-3; block 1 a blue
        // plain single at (5, 0); block 2 an orange Vertical L3 at (0, 0).
        private static LevelData CreateLevel(bool withArrows = true)
        {
            return new LevelData("arrow-module", "Arrow module", 6, 6, 60,
                new[] { new LevelDoorData("top", BoardSide.Top, 1, 3, "Red") },
                new[]
                {
                    new LevelBlockData("line", "Red", new Vector2Int(1, 5),
                        new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) },
                        withArrows ? new[] { Arrow("Horizontal") } : Array.Empty<LevelEffectData>()),
                    new LevelBlockData("plain", "Blue", new Vector2Int(5, 0), new[] { Vector2Int.zero }),
                    new LevelBlockData("ell", "Orange", Vector2Int.zero,
                        new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up },
                        withArrows ? new[] { Arrow("Vertical") } : Array.Empty<LevelEffectData>())
                });
        }

        private static LevelEffectData Arrow(string axis)
        {
            return new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis) });
        }

        private static AttemptLevel Convert(LevelData data)
        {
            Assert.That(BoardLayout.TryCreate(data, out BoardLayout layout, out string error), Is.True, error);
            Assert.That(AttemptLevelConverter.TryConvert(data, layout, out AttemptLevel level, out error), Is.True,
                error);
            return level;
        }

        // The builder keeps one private list per hook port; the count shows what AddHooks registered.
        private static int Count(AttemptHooksBuilder builder, string field)
        {
            FieldInfo info = typeof(AttemptHooksBuilder).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, field);
            return ((ICollection)info.GetValue(builder)).Count;
        }
    }
}
