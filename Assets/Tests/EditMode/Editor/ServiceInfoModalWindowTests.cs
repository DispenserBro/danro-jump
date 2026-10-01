using DanroJump.Settings;
using DanroJump.UI.MainMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class ServiceInfoModalWindowTests
    {
        [Test]
        public void SetResult_UpdatesVisibleLabels()
        {
            var root = CreateRoot();
            var modal = new ServiceInfoModalWindow(root);
            var result = ServiceSettingsActionResult.Success(
                "Настройки QR",
                "Раздел QR открыт.",
                "QR-выдача");

            modal.SetResult(result);

            Assert.That(root.Q<Label>("ServiceInfoTitle").text, Is.EqualTo("Настройки QR"));
            Assert.That(root.Q<Label>("ServiceInfoContent").text, Is.EqualTo("Раздел QR открыт."));
            Assert.That(root.Q<Label>("ServiceInfoFocus").text, Is.EqualTo("Раздел: QR-выдача"));

            modal.Dispose();
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement();
            root.Add(new Label { name = "ServiceInfoTitle" });
            root.Add(new Label { name = "ServiceInfoFocus" });
            root.Add(new Label { name = "ServiceInfoContent" });
            root.Add(new Button { name = "ServiceInfoCloseButton" });
            return root;
        }
    }
}
