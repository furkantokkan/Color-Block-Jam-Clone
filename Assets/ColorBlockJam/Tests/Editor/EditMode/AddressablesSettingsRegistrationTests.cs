using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Guards the EditorBuildSettings entry through which Addressables finds its settings,
    /// which the Level Editor database dropdown depends on.
    /// </summary>
    public sealed class AddressablesSettingsRegistrationTests
    {
        private const string k_defaultObjectPath = "Assets/AddressableAssetsData/DefaultObject.asset";
        private const string k_settingsPath = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
        private const string k_editorBuildSettingsPath = "ProjectSettings/EditorBuildSettings.asset";

        [Test]
        public void ConfigObjectPointsToCommittedDefaultObject()
        {
            bool registered = EditorBuildSettings.TryGetConfigObject(
                AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName,
                out AddressableAssetSettingsDefaultObject defaultObject);

            Assert.That(registered, Is.True,
                "EditorBuildSettings has no com.unity.addressableassets config object; "
                + "the Level Editor cannot list level databases.");
            Assert.That(AssetDatabase.GetAssetPath(defaultObject), Is.EqualTo(k_defaultObjectPath));
        }

        [Test]
        public void SettingsResolveToCommittedAsset()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);

            Assert.That(settings, Is.Not.Null, "Addressables settings do not resolve.");
            Assert.That(AssetDatabase.GetAssetPath(settings), Is.EqualTo(k_settingsPath));
        }

        [Test]
        public void RegistrationIsPersistedInProjectSettings()
        {
            string defaultObjectGuid = AssetDatabase.AssetPathToGUID(k_defaultObjectPath);
            string text = File.ReadAllText(k_editorBuildSettingsPath);

            Assert.That(text, Does.Contain(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName),
                "The Addressables config object is missing from " + k_editorBuildSettingsPath + ".");
            Assert.That(text, Does.Contain(defaultObjectGuid));
        }
    }
}
