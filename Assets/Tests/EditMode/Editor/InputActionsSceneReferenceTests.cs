using System.IO;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class InputActionsSceneReferenceTests
    {
        private const string ProjectInputActionsGuid = "2bcd2660ca9b64942af0de543d8d7100";
        private const string PackageDefaultInputActionsGuid = "ca9f5fa95ffab41fb9a615ab714db018";

        [TestCase("Assets/Scenes/MainMenu.unity")]
        [TestCase("Assets/Scenes/Game.unity")]
        public void SceneInputSystemUiModule_UsesProjectInputActions(string scenePath)
        {
            var sceneText = File.ReadAllText(scenePath);

            Assert.That(sceneText, Does.Contain(ProjectInputActionsGuid));
            Assert.That(sceneText, Does.Not.Contain(PackageDefaultInputActionsGuid));
        }
    }
}
