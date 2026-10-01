using DanroJump.UI.Modals;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class ModalStackControllerTests
    {
        [Test]
        public void Open_StretchesTemplateHostLayerAndModalRoot()
        {
            var host = new TemplateContainer();
            var layer = new VisualElement { name = "ModalLayer" };
            var backdrop = new VisualElement { name = "ModalBackdrop" };
            var modalRoot = new VisualElement { name = "ModalRoot" };
            host.Add(layer);
            layer.Add(backdrop);
            layer.Add(modalRoot);

            var stack = new ModalStackController(layer, backdrop);
            var modal = new TestModalWindow(modalRoot);

            stack.Open(modal);

            Assert.That(host.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(host.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(layer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(layer.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(modalRoot.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(modalRoot.style.left.value.value, Is.EqualTo(0f));
            Assert.That(modalRoot.style.right.value.value, Is.EqualTo(0f));
            Assert.That(modalRoot.style.top.value.value, Is.EqualTo(0f));
            Assert.That(modalRoot.style.bottom.value.value, Is.EqualTo(0f));
        }

        [Test]
        public void Close_ClearsModalButtonFocusHighlight()
        {
            var modalRoot = new VisualElement { name = "ModalRoot" };
            var button = new Button { name = "FocusedButton" };
            button.AddToClassList("modal-button");
            button.AddToClassList("modal-button--focused");
            modalRoot.Add(button);
            var modal = new TestModalWindow(modalRoot);

            modal.Close();

            Assert.That(button.ClassListContains("modal-button--focused"), Is.False);
        }

        private sealed class TestModalWindow : ModalWindowBase
        {
            public TestModalWindow(VisualElement root)
                : base(root)
            {
            }
        }
    }
}
