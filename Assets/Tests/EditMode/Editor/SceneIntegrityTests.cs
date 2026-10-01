using DanroJump.Gameplay;
using DanroJump.UI.Gameplay;
using DanroJump.UI.MainMenu;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace DanroJump.Tests.EditMode
{
    public sealed class SceneIntegrityTests
    {
        private string originalScenePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            originalScenePath = SceneManager.GetActiveScene().path;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (!string.IsNullOrEmpty(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void MainMenuScene_HasRequiredUiAndProjectBindingsEntryPoint()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);

            Assert.That(Object.FindAnyObjectByType<MainMenuController>(), Is.Not.Null);
            Assert.That(Resources.Load<ProjectContext>("ProjectContext"), Is.Not.Null);
        }

        [Test]
        public void GameScene_HasRequiredGameplayRoots()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            Assert.That(Object.FindAnyObjectByType<SceneContext>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<GameplayInstaller>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<JumpPlayerController>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<PlatformSpawner>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<GameplayHudController>(), Is.Not.Null);
        }
    }
}
