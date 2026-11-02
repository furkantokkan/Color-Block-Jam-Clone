using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using Onity.DI;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>Checks the lifecycle and assembly rules by reflection (NFR-L3, NFR-L6, NFR-11, V-E22).</summary>
    public sealed class GameplayArchitectureTests
    {
        private const BindingFlags k_declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Assembly s_domain = typeof(Cell).Assembly;
        private static readonly Assembly s_gameplay = typeof(LevelSession).Assembly;
        private static readonly Assembly s_runtime = typeof(LevelData).Assembly;

        [Test]
        public void ExactlyOneOnityTickableExists()
        {
            List<string> tickables = new List<string>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string name = assemblies[i].GetName().Name;
                if (!name.StartsWith("ColorBlockJam.", StringComparison.Ordinal) || name.Contains("Tests"))
                {
                    continue;
                }

                Type[] types = GetTypes(assemblies[i]);
                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type != null && !type.IsAbstract && typeof(IOnityTickable).IsAssignableFrom(type))
                    {
                        tickables.Add(type.FullName);
                    }
                }
            }

            Assert.That(tickables, Is.EqualTo(new[] { typeof(GameplayTickOwner).FullName }));
        }

        [Test]
        public void NoDomainOrGameplayTypeDeclaresAUnityFrameLoop()
        {
            List<string> loops = new List<string>();
            AddDeclaredMethods(s_domain, loops, IsFrameLoop);
            AddDeclaredMethods(s_gameplay, loops, IsFrameLoop);

            Assert.That(loops, Is.Empty);
        }

        [Test]
        public void NoDomainOrGameplayMethodIsAsyncVoid()
        {
            List<string> asyncVoids = new List<string>();
            AddDeclaredMethods(s_domain, asyncVoids, IsAsyncVoid);
            AddDeclaredMethods(s_gameplay, asyncVoids, IsAsyncVoid);

            Assert.That(asyncVoids, Is.Empty);
        }

        [Test]
        public void DomainReferencesNoEngineOrOnityAssembly()
        {
            AssemblyName[] references = s_domain.GetReferencedAssemblies();
            for (int i = 0; i < references.Length; i++)
            {
                string name = references[i].Name;
                Assert.That(name.StartsWith("Unity", StringComparison.Ordinal) || name.StartsWith("Onity", StringComparison.Ordinal),
                    Is.False, name);
            }
        }

        [Test]
        public void RuntimeReferencesNeitherDomainNorGameplay()
        {
            AssemblyName[] references = s_runtime.GetReferencedAssemblies();
            for (int i = 0; i < references.Length; i++)
            {
                Assert.That(references[i].Name, Is.Not.EqualTo(s_domain.GetName().Name));
                Assert.That(references[i].Name, Is.Not.EqualTo(s_gameplay.GetName().Name));
            }
        }

        [Test]
        public void OnlyDomainCommandHandlersCanChangeTheBoard()
        {
            // Internal mutators and internal handlers keep Gameplay and Presentation code from changing
            // occupancy past the attempt's command executor and its log (DEC-122).
            string[] mutators = { "Move", "Release", "Reset", "RemoveCell" };
            for (int i = 0; i < mutators.Length; i++)
            {
                MethodInfo method = typeof(BoardState).GetMethod(mutators[i], k_declared);
                Assert.That(method, Is.Not.Null, mutators[i]);
                Assert.That(method.IsAssembly, Is.True, mutators[i] + " must be internal to the Domain.");
            }

            List<string> publicHandlers = new List<string>();
            Type[] types = GetTypes(s_domain);
            for (int t = 0; t < types.Length; t++)
            {
                Type type = types[t];
                if (type != null && !type.IsInterface && typeof(ICommandHandler).IsAssignableFrom(type)
                    && (type.IsPublic || type.IsNestedPublic))
                {
                    publicHandlers.Add(type.FullName);
                }
            }

            Assert.That(publicHandlers, Is.Empty);
        }

        // Unity calls only the parameterless messages, so DragResolver.Update(pointer, ...) is not a frame loop.
        private static bool IsFrameLoop(MethodInfo method)
        {
            return (method.Name == "Update" || method.Name == "FixedUpdate" || method.Name == "LateUpdate")
                && method.GetParameters().Length == 0;
        }

        private static bool IsAsyncVoid(MethodInfo method)
        {
            return method.ReturnType == typeof(void) && method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;
        }

        private static void AddDeclaredMethods(Assembly assembly, List<string> output, Func<MethodInfo, bool> match)
        {
            Type[] types = GetTypes(assembly);
            for (int t = 0; t < types.Length; t++)
            {
                if (types[t] == null)
                {
                    continue;
                }

                MethodInfo[] methods = types[t].GetMethods(k_declared);
                for (int m = 0; m < methods.Length; m++)
                {
                    if (match(methods[m]))
                    {
                        output.Add(types[t].FullName + "." + methods[m].Name);
                    }
                }
            }
        }

        // Editor assemblies can reference types that fail to load; the loadable ones are still checked.
        private static Type[] GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }
    }
}
