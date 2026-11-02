using System;
using System.Reflection;
using NUnit.Framework;

namespace Haptics.Tests
{
    /// <summary>Verifies the reusable output's native style boundary, Editor safety and dependency isolation.</summary>
    public sealed class DeviceHapticOutputTests
    {
        [TestCase(HapticStyle.None, 0)]
        [TestCase(HapticStyle.Selection, 1)]
        [TestCase(HapticStyle.Light, 2)]
        [TestCase(HapticStyle.Medium, 3)]
        [TestCase(HapticStyle.Rigid, 4)]
        [TestCase(HapticStyle.Heavy, 5)]
        [TestCase(HapticStyle.Success, 6)]
        [TestCase(HapticStyle.Failure, 7)]
        public void StyleValue_MatchesTheNativeBoundary(HapticStyle style, int value)
        {
            Assert.That((int)style, Is.EqualTo(value));
        }

        [Test]
        public void EditorOutput_AllStylesAndStop_AreSafeWithoutNativeLibraries()
        {
            using (var output = new DeviceHapticOutput())
            {
                foreach (HapticStyle style in Enum.GetValues(typeof(HapticStyle)))
                {
                    Assert.DoesNotThrow(() => output.Play(style));
                }

                Assert.DoesNotThrow(output.Stop);
            }
        }

        [TestCase(-1)]
        [TestCase(8)]
        [TestCase(int.MaxValue)]
        public void Play_UnknownStyle_StaysSilent(int value)
        {
            using (var output = new DeviceHapticOutput())
            {
                Assert.DoesNotThrow(() => output.Play((HapticStyle)value));
            }
        }

        [Test]
        public void Dispose_RepeatedAndFollowedByPlayback_IsSafe()
        {
            var output = new DeviceHapticOutput();
            output.Dispose();

            Assert.DoesNotThrow(output.Dispose);
            Assert.DoesNotThrow(() => output.Play(HapticStyle.Heavy));
            Assert.DoesNotThrow(output.Stop);
        }

        [Test]
        public void RuntimeAssembly_ReferencesNoGameOrFrameworkAssembly()
        {
            Assembly assembly = typeof(DeviceHapticOutput).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo("Haptics"));
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                Assert.That(reference.Name.StartsWith("ColorBlockJam", StringComparison.Ordinal), Is.False,
                    reference.Name);
                Assert.That(reference.Name.StartsWith("Onity", StringComparison.Ordinal), Is.False,
                    reference.Name);
            }
        }
    }
}
