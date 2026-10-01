using System;
using System.Reflection;
using DanroJump.SceneFlow;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class SceneFlowServiceTests
    {
        [TestCase("Bootstrap")]
        [TestCase("MainMenu")]
        [TestCase("Game")]
        public void SceneExists_ReturnsTrueForConfiguredBuildScenes(string sceneName)
        {
            using var sceneFlow = new SceneFlowService();

            Assert.That(sceneFlow.SceneExists(sceneName), Is.True);
        }

        [Test]
        public void SceneExists_ReturnsFalseForUnknownOrEmptyScenes()
        {
            using var sceneFlow = new SceneFlowService();

            Assert.That(sceneFlow.SceneExists("MissingScene"), Is.False);
            Assert.That(sceneFlow.SceneExists(string.Empty), Is.False);
        }

        [Test]
        public void GetDelayMilliseconds_ConvertsTimeSpanToWholeMilliseconds()
        {
            var method = typeof(SceneFlowService)
                .GetMethod("GetDelayMilliseconds", BindingFlags.Static | BindingFlags.NonPublic);

            var milliseconds = (int)method.Invoke(null, new object[] { TimeSpan.FromSeconds(8) });

            Assert.That(milliseconds, Is.EqualTo(8000));
        }

        [Test]
        public void GetDelayMilliseconds_ClampsNegativeDelayToZero()
        {
            var method = typeof(SceneFlowService)
                .GetMethod("GetDelayMilliseconds", BindingFlags.Static | BindingFlags.NonPublic);

            var milliseconds = (int)method.Invoke(null, new object[] { TimeSpan.FromSeconds(-1) });

            Assert.That(milliseconds, Is.EqualTo(0));
        }
    }
}
