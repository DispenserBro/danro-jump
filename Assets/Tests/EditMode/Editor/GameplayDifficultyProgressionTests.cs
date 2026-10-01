using System.Collections.Generic;
using DanroJump.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class GameplayDifficultyProgressionTests
    {
        private readonly List<Object> createdObjects = new();

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
        public void InitialSpawnY_UsesLowestConfiguredStage()
        {
            var progression = CreateProgression(
                TestObjectFactory.CreateDifficultyStage("Late", 100f, 50f, 0f),
                TestObjectFactory.CreateDifficultyStage("Early", 15f, 50f, 0f));

            Assert.That(progression.InitialSpawnY, Is.EqualTo(15f));
        }

        [Test]
        public void GetStageName_UsesHighestReachedStageStart()
        {
            var progression = CreateProgression(
                TestObjectFactory.CreateDifficultyStage("Early", 0f, 100f, 0f),
                TestObjectFactory.CreateDifficultyStage("Late", 75f, 100f, 0f));

            Assert.That(progression.GetStageName(50f), Is.EqualTo("Early"));
            Assert.That(progression.GetStageName(75f), Is.EqualTo("Late"));
            Assert.That(progression.GetStageName(1000f), Is.EqualTo("Late"));
        }

        [Test]
        public void StageProgress_IsClampedInsideActiveStage()
        {
            var progression = CreateProgression(
                TestObjectFactory.CreateDifficultyStage("Stage", 10f, 100f, 0f));

            Assert.That(progression.GetStageProgress(10f), Is.EqualTo(0f));
            Assert.That(progression.GetStageProgress(60f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(progression.GetStageProgress(500f), Is.EqualTo(1f));
        }

        private GameplayDifficultyProgression CreateProgression(params GameplayDifficultyStage[] stages)
        {
            foreach (var stage in stages)
            {
                createdObjects.Add(stage);
            }

            var gameObject = new GameObject("Difficulty Progression Test");
            createdObjects.Add(gameObject);

            var progression = gameObject.AddComponent<GameplayDifficultyProgression>();
            TestObjectFactory.SetPrivateField(progression, "progressionEnabled", true);
            TestObjectFactory.SetPrivateField(progression, "stages", new List<GameplayDifficultyStage>(stages));
            return progression;
        }
    }
}
