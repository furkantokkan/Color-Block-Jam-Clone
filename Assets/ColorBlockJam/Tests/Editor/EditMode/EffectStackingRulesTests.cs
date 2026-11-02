using System;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// The Effect Constraints rules (DEC-177): nothing stacks until a pair is allowed, a pair reads the same both ways
    /// and per target, an effect never stacks with itself, and Allow All / Allow None cover one effect of one target.
    /// </summary>
    public sealed class EffectStackingRulesTests
    {
        private EffectStackingRules m_rules;

        [SetUp]
        public void SetUp()
        {
            m_rules = ScriptableObject.CreateInstance<EffectStackingRules>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_rules);
        }

        [Test]
        public void NothingStacksUntilAPairIsAllowed()
        {
            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "arrow", "ice"), Is.False);
            Assert.That(EffectStackingRules.CanJoin(m_rules, LevelEffectTarget.Block, new[] { "arrow" }, "ice"), Is.False);
            Assert.That(EffectStackingRules.CanJoin(null, LevelEffectTarget.Block, new[] { "arrow" }, "ice"), Is.False,
                "missing rules let nothing stack");
            Assert.That(EffectStackingRules.CanJoin(null, LevelEffectTarget.Block, Array.Empty<string>(), "ice"), Is.True,
                "the first effect always fits");
        }

        [Test]
        public void APairReadsTheSameBothWaysAndOnlyForItsTarget()
        {
            m_rules.SetCanStack(LevelEffectTarget.Block, "ice", "arrow", true);

            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "arrow", "ice"), Is.True);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "ice", "arrow"), Is.True);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Door, "arrow", "ice"), Is.False);

            m_rules.SetCanStack(LevelEffectTarget.Block, "arrow", "ice", false);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "ice", "arrow"), Is.False);
        }

        [Test]
        public void AnEffectNeverStacksWithItself()
        {
            Assert.That(EffectStackingRules.IsFixed("ice", "ice"), Is.True);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "ice", "ice"), Is.False);
            Assert.Throws<ArgumentException>(() => m_rules.SetCanStack(LevelEffectTarget.Block, "ice", "ice", true));
        }

        [Test]
        public void AllowAllAndAllowNoneCoverOneEffectOfOneTarget()
        {
            string[] types = { "arrow", "ice", "extra" };
            m_rules.SetCanStack(LevelEffectTarget.Door, "arrow", "ice", true);

            m_rules.SetAllFor(LevelEffectTarget.Block, "ice", types, true);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "ice", "arrow"), Is.True);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "ice", "extra"), Is.True);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Block, "arrow", "extra"), Is.False, "pairs without ice stay");
            Assert.That(m_rules.CountPartners(LevelEffectTarget.Block, "ice", types), Is.EqualTo(2), "ice itself never counts");

            m_rules.SetAllFor(LevelEffectTarget.Block, "ice", types, false);
            Assert.That(m_rules.CountPartners(LevelEffectTarget.Block, "ice", types), Is.Zero);
            Assert.That(m_rules.CanStack(LevelEffectTarget.Door, "arrow", "ice"), Is.True, "the other target is untouched");
        }
    }
}
