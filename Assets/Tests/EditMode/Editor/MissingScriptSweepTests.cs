using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class MissingScriptSweepTests
    {
        [Test]
        public void PrefabsAndScenes_DoNotContainExplicitMissingScriptReferences()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, $"{path} could not be loaded as prefab.");
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab),
                    Is.EqualTo(0),
                    $"{path} contains missing MonoBehaviour references.");
            }

            foreach (var scenePath in Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories))
            {
                Assert.That(
                    File.ReadAllText(scenePath),
                    Does.Not.Contain("m_Script: {fileID: 0}"),
                    $"{scenePath} contains explicit missing script references.");
            }
        }
    }
}
