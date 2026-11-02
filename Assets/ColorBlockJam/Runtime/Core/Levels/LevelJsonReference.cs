using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// An Addressables reference to the manifest of one level database: it keeps the file's GUID without loading or
    /// bundling the file, and the Inspector offers and accepts only database manifests under
    /// <see cref="LevelJsonFiles.Folder"/> (DEC-194, DEC-196).
    /// </summary>
    [Serializable]
    public sealed class LevelJsonReference : AssetReferenceT<TextAsset>
    {
        public LevelJsonReference(string guid) : base(guid)
        {
        }

        // The Addressables picker, a drop and SetEditorAsset all ask this, so no pack or other TextAsset can be assigned,
        // and a drop can never make one Addressable in the default group.
        public override bool ValidateAsset(string mainAssetPath)
        {
            return LevelJsonFiles.IsManifest(mainAssetPath) && base.ValidateAsset(mainAssetPath);
        }
    }
}
