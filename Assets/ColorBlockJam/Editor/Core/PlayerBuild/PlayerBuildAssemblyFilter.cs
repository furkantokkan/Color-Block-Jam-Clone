using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ColorBlockJam.Editor.Build
{
    /// <summary>
    /// Keeps development-only assemblies (MCP for Unity's runtime) out of player builds
    /// (Performance Rule 6, DEC-185).
    /// </summary>
    public sealed class PlayerBuildAssemblyFilter : IFilterBuildAssemblies
    {
        private static readonly string[] k_devOnlyAssemblies = { "MCPForUnity.Runtime.dll" };

        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies) => Filter(assemblies);

        internal static string[] Filter(IReadOnlyList<string> assemblies)
        {
            var kept = new List<string>(assemblies.Count);

            for (var i = 0; i < assemblies.Count; i++)
            {
                var assembly = assemblies[i];
                var fileName = Path.GetFileName(assembly);
                var dropped = false;

                for (var j = 0; j < k_devOnlyAssemblies.Length; j++)
                {
                    if (string.Equals(fileName, k_devOnlyAssemblies[j], StringComparison.OrdinalIgnoreCase))
                    {
                        dropped = true;
                        break;
                    }
                }

                if (dropped)
                {
                    Debug.Log($"PlayerBuildAssemblyFilter: dropped '{assembly}' from the player build.");
                }
                else
                {
                    kept.Add(assembly);
                }
            }

            return kept.ToArray();
        }
    }
}
