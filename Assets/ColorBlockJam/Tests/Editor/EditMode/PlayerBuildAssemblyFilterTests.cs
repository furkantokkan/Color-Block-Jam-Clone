using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Build;
using NUnit.Framework;
using UnityEditor.Build;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies PlayerBuildAssemblyFilter.Filter as a pure function: it drops MCP for Unity's runtime
    /// assembly by file name only, regardless of directory or casing, leaves every other assembly path
    /// untouched and in order, and the type is a valid IFilterBuildAssemblies for Unity to instantiate
    /// (Performance Rule 6, DEC-185).
    /// </summary>
    public sealed class PlayerBuildAssemblyFilterTests
    {
        [Test]
        public void DropsMcpForUnityRuntimeDll_FromItsUsualBuildPath()
        {
            var assemblies = new List<string>
            {
                "Library/ScriptAssemblies/ColorBlockJam.Runtime.dll",
                "Library/ScriptAssemblies/MCPForUnity.Runtime.dll",
                "Library/ScriptAssemblies/Assembly-CSharp.dll"
            };

            var result = PlayerBuildAssemblyFilter.Filter(assemblies);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Library/ScriptAssemblies/ColorBlockJam.Runtime.dll",
                    "Library/ScriptAssemblies/Assembly-CSharp.dll"
                },
                result);
        }

        [Test]
        public void DropsMcpForUnityRuntimeDll_AnyCasingAndDirectory()
        {
            var assemblies = new List<string>
            {
                "Other/Path/mcpforunity.runtime.dll",
                "MCPFORUNITY.RUNTIME.DLL",
                "ColorBlockJam.Editor.dll"
            };

            var result = PlayerBuildAssemblyFilter.Filter(assemblies);

            CollectionAssert.AreEqual(new[] { "ColorBlockJam.Editor.dll" }, result);
        }

        [Test]
        public void KeepsUnrelatedAssemblies_Untouched()
        {
            var assemblies = new List<string>
            {
                "ColorBlockJam.Runtime.dll",
                "Assembly-CSharp.dll",
                "MCPForUnity.Editor.dll"
            };

            var result = PlayerBuildAssemblyFilter.Filter(assemblies);

            CollectionAssert.AreEqual(assemblies, result);
        }

        [Test]
        public void PreservesOrder_WhenSomeEntriesAreDropped()
        {
            var assemblies = new List<string>
            {
                "First.dll",
                "MCPForUnity.Runtime.dll",
                "Second.dll",
                "MCPForUnity.Runtime.dll",
                "Third.dll"
            };

            var result = PlayerBuildAssemblyFilter.Filter(assemblies);

            CollectionAssert.AreEqual(new[] { "First.dll", "Second.dll", "Third.dll" }, result);
        }

        [Test]
        public void ReturnsTheSameList_WhenNothingMatches()
        {
            var assemblies = new List<string> { "ColorBlockJam.Runtime.dll", "ColorBlockJam.Domain.dll" };

            var result = PlayerBuildAssemblyFilter.Filter(assemblies);

            CollectionAssert.AreEqual(assemblies, result);
        }

        [Test]
        public void ImplementsIFilterBuildAssemblies_WithAPublicParameterlessConstructor()
        {
            var type = typeof(PlayerBuildAssemblyFilter);

            Assert.IsTrue(typeof(IFilterBuildAssemblies).IsAssignableFrom(type));
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
        }
    }
}
