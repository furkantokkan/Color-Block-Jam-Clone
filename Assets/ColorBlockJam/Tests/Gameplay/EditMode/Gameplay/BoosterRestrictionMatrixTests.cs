using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The booster restriction matrix (DEC-171): cells toggle by target kind and block effect, only block effects and
    /// board booster rows are accepted, the hook builder turns closed cells into one gate per attempt, and the shipped
    /// asset closes Rocket on Ice only.
    /// </summary>
    public sealed class BoosterRestrictionMatrixTests
    {
        private const string k_shippedPath = "Assets/ColorBlockJam/Data/Boosters/BoosterRestrictionMatrix.asset";

        private static readonly string[] s_board =
        {
            "AA..",
            "....",
            "..BB"
        };

        private BoosterRestrictionMatrix m_matrix;

        [SetUp]
        public void SetUp()
        {
            m_matrix = ScriptableObject.CreateInstance<BoosterRestrictionMatrix>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_matrix);
        }

        [Test]
        public void CellsToggleAndRejectNonBlockEffectsAndNonBoardRows()
        {
            m_matrix.SetRestricted(BoosterTargetKind.Cell, "ice", true);
            m_matrix.SetRestricted(BoosterTargetKind.Cell, "ice", true);
            Assert.That(m_matrix.Count, Is.EqualTo(1), "closing a closed cell adds nothing");
            Assert.That(m_matrix.IsRestricted(BoosterTargetKind.Cell, "ice"), Is.True);
            Assert.That(m_matrix.IsRestricted(BoosterTargetKind.Block, "ice"), Is.False);
            Assert.That(m_matrix.Validate(out string error), Is.True, error);

            m_matrix.SetRestricted(BoosterTargetKind.Cell, "ice", false);
            Assert.That(m_matrix.Count, Is.EqualTo(0));

            Assert.Throws<ArgumentException>(() => m_matrix.SetRestricted(BoosterTargetKind.Cell, "no-such-effect", true));
            Assert.Throws<ArgumentOutOfRangeException>(() => m_matrix.SetRestricted(BoosterTargetKind.None, "ice", true));
        }

        [Test]
        public void ClosedCellsBecomeOneGateForPresentEffects()
        {
            m_matrix.SetRestricted(BoosterTargetKind.Cell, "ice", true);
            m_matrix.SetRestricted(BoosterTargetKind.ColorFromBlock, "arrow", true);
            IceRules ice = new IceRules(new[] { 1, 0 });
            IBlockEffectState[] states = { new IceEffectState(ice), new ArrowEffectState(null) };

            AttemptHooksBuilder builder = new AttemptHooksBuilder();
            BoosterRestrictionHooks.Add(m_matrix, states, builder);
            AttemptHooks hooks = builder.Build();
            BoardState board = new BoardState(BoardFixture.Create(s_board), hooks);

            Assert.That(board.AllowsBoosterTarget(CommandKind.RemoveCell, 0), Is.False, "Rocket on a frozen block");
            Assert.That(board.AllowsBoosterTarget(CommandKind.RemoveCell, 1), Is.True, "Rocket on a plain block");
            Assert.That(board.AllowsBoosterTarget(CommandKind.RemoveBlock, 0), Is.True, "Hammer stays open");
            Assert.That(board.AllowsBoosterTarget(CommandKind.RemoveColor, 0), Is.True,
                "a level without Arrow blocks has no Arrow presence");
        }

        [Test]
        public void AnEmptyOrMissingMatrixRestrictsNothing()
        {
            IBlockEffectState[] states = { new IceEffectState(new IceRules(new[] { 1, 0 })) };
            AttemptHooksBuilder builder = new AttemptHooksBuilder();
            BoosterRestrictionHooks.Add(null, states, builder);
            BoosterRestrictionHooks.Add(m_matrix, states, builder);
            BoardState board = new BoardState(BoardFixture.Create(s_board), builder.Build());

            Assert.That(board.AllowsBoosterTarget(CommandKind.RemoveCell, 0), Is.True);
        }

        [Test]
        public void ShippedMatrixClosesRocketOnIceOnly()
        {
            BoosterRestrictionMatrix shipped = AssetDatabase.LoadAssetAtPath<BoosterRestrictionMatrix>(k_shippedPath);

            Assert.That(shipped, Is.Not.Null, k_shippedPath);
            Assert.That(shipped.Validate(out string error), Is.True, error);
            Assert.That(shipped.Count, Is.EqualTo(1));
            Assert.That(shipped.IsRestricted(BoosterTargetKind.Cell, "ice"), Is.True);
            Assert.That(shipped.IsRestricted(BoosterTargetKind.Block, "ice"), Is.False);
            Assert.That(shipped.IsRestricted(BoosterTargetKind.ColorFromBlock, "ice"), Is.False);
        }
    }
}
