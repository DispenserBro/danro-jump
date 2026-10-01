using System.Collections.Generic;
using DanroJump.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class WeightedPrefabOptionTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void TrySelect_IgnoresNullAndZeroWeightOptions()
        {
            var selectablePrefab = CreateGameObject("Selectable");
            var options = new[]
            {
                TestObjectFactory.CreateWeightedPrefabOption(null, 100f),
                TestObjectFactory.CreateWeightedPrefabOption(CreateGameObject("Zero"), 0f),
                TestObjectFactory.CreateWeightedPrefabOption(selectablePrefab, 1f),
            };

            var selected = WeightedPrefabOption.TrySelect(options, new System.Random(123), out var prefab);

            Assert.That(selected, Is.True);
            Assert.That(prefab, Is.SameAs(selectablePrefab));
        }

        [Test]
        public void TrySelect_RespectsPrefabFilter()
        {
            var blocked = CreateGameObject("Blocked");
            var allowed = CreateGameObject("Allowed");
            var options = new[]
            {
                TestObjectFactory.CreateWeightedPrefabOption(blocked, 100f),
                TestObjectFactory.CreateWeightedPrefabOption(allowed, 1f),
            };

            var selected = WeightedPrefabOption.TrySelect(
                options,
                new System.Random(123),
                prefab => prefab == allowed,
                out var prefab);

            Assert.That(selected, Is.True);
            Assert.That(prefab, Is.SameAs(allowed));
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }
    }
}
