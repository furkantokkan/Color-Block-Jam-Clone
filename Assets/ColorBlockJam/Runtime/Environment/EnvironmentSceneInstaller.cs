using System;
using Onity.DI;
using Onity.Unity.Installers;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Binds the Game scene's environment database.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentSceneInstaller : MonoInstaller
    {
        [SerializeField] private EnvironmentDatabase m_database;

        public override void InstallBindings(OnityContainer container)
        {
            if (m_database == null)
            {
                throw new InvalidOperationException("Environment database is missing.");
            }

            if (!m_database.Validate(out string error))
            {
                throw new InvalidOperationException($"Environment database is invalid: {error}");
            }

            container.BindScriptableObject(m_database);
        }
    }
}
