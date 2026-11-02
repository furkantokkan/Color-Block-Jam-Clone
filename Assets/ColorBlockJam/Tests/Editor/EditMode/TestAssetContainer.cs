using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// A test-owned main asset: it holds transient Addressables settings, groups and schemas as sub-assets, which the
    /// vendor's import hooks never treat as the project's own, and stands in for an unrelated asset whose unsaved
    /// change must survive a test.
    /// </summary>
    public sealed class TestAssetContainer : ScriptableObject
    {
        [SerializeField] private string m_value;

        public string Value => m_value;
    }
}
