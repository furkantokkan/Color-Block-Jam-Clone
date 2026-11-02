using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Pins every LevelEffectTypes constant to its LevelEffectRegistry entry (AD-14).</summary>
    public sealed class LevelEffectTypesTests
    {
        [TestCase(LevelEffectTypes.Arrow)]
        [TestCase(LevelEffectTypes.Ice)]
        [TestCase(LevelEffectTypes.Barrier)]
        public void ConstantMatchesRegisteredDefinition(string typeId)
        {
            Assert.That(LevelEffectRegistry.TryFind(typeId, out LevelEffectDefinition definition), Is.True, typeId);
            Assert.That(definition.TypeId, Is.EqualTo(typeId));
        }
    }
}
