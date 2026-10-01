using System.Collections.Generic;
using System.Reflection;
using DanroJump.Prizes;
using DanroJump.Prizes.Qr;
using DanroJump.UI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class PrizeResultOverlayWindowTests
    {
        [Test]
        public void SetResult_ShowsBigPrizeNamePhoneAndRunStats()
        {
            var root = CreateRoot();
            var window = new PrizeResultOverlayWindow(root);

            window.SetResult(
                new PrizeFlowResult(PrizeRewardKind.Big, score: 125, coins: 8, height: 42.4f),
                "Наушники",
                "+79990000000");

            Assert.That(root.Q<Label>("PrizeResultTitle").text, Is.EqualTo("Большой приз: Наушники"));
            Assert.That(root.Q<Label>("PrizeResultSubtitle").text, Is.EqualTo("Обратитесь к оператору: +79990000000"));
            Assert.That(root.Q<Label>("PrizeResultDetails").text, Is.EqualTo("Очки: 125. Монеты: 8. Высота: 42 м."));
            Assert.That(root.Q<VisualElement>("PrizeQrContainer").style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ReturnButton_RaisesReturnRequested()
        {
            var root = CreateRoot();
            var window = new PrizeResultOverlayWindow(root);
            var raised = false;
            window.ReturnRequested += () => raised = true;

            typeof(PrizeResultOverlayWindow)
                .GetMethod("HandleReturnClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(window, null);

            Assert.That(raised, Is.True);
        }

        [TestCase(PrizeRewardKind.QRCode, "QR-приз!")]
        [TestCase(PrizeRewardKind.Hopper, "Приз из хоппера!")]
        [TestCase(PrizeRewardKind.PrizeStand, "Приз на витрине!")]
        public void SetResult_ShowsLegacyPrizeTypeTitles(PrizeRewardKind rewardKind, string expectedTitle)
        {
            var root = CreateRoot();
            var window = new PrizeResultOverlayWindow(root);

            window.SetResult(new PrizeFlowResult(rewardKind, score: 10, coins: 0, height: 1f));

            Assert.That(root.Q<Label>("PrizeResultTitle").text, Is.EqualTo(expectedTitle));
        }

        [Test]
        public void SetResult_QrPrizeShowsQrBlock()
        {
            var root = CreateRoot();
            var window = new PrizeResultOverlayWindow(root);
            var texture = new Texture2D(4, 4);
            var ticket = new PrizeQrTicket(
                "https://t.me/+79999999999?text=QR",
                "+7 (999) 999-99-99",
                "79999999999",
                "QR message",
                texture,
                QrPrizeTier.Bronze);

            try
            {
                window.SetResult(
                    new PrizeFlowResult(PrizeRewardKind.QRCode, score: 10, coins: 1, height: 5f),
                    qrTicket: ticket);

                Assert.That(root.Q<VisualElement>("PrizeQrContainer").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<Image>("PrizeQrImage").image, Is.SameAs(texture));
                Assert.That(root.Q<Label>("PrizeQrPhone").text, Is.EqualTo("+7 (999) 999-99-99"));
                Assert.That(root.Q<Label>("PrizeQrMessage").text, Is.EqualTo("QR message"));
                Assert.That(root.Q<Label>("PrizeResultSubtitle").text, Is.EqualTo("Отсканируйте QR-код для связи в Telegram."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Open_IgnoresImmediateReturnSubmitFromHeldArcadeButton()
        {
            var root = CreateRoot();
            var window = new PrizeResultOverlayWindow(root);
            var raised = false;
            window.ReturnRequested += () => raised = true;

            window.Open();
            typeof(PrizeResultOverlayWindow)
                .GetMethod("HandleReturnClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(window, null);

            Assert.That(raised, Is.False);

            typeof(PrizeResultOverlayWindow)
                .GetField("inputEnabledAtTime", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(window, float.NegativeInfinity);
            typeof(PrizeResultOverlayWindow)
                .GetMethod("HandleReturnClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(window, null);

            Assert.That(raised, Is.True);
        }

        [Test]
        public void SetPrizeStandStatus_RendersCellsWithCorrectStatuses()
        {
            var root = CreateRoot();
            var window = new PrizeResultOverlayWindow(root);

            var statuses = new Dictionary<int, DanroJump.Hardware.Com.PrizeBoxStatus>
            {
                { 1, DanroJump.Hardware.Com.PrizeBoxStatus.Ok },
                { 2, DanroJump.Hardware.Com.PrizeBoxStatus.Empty },
                { 3, DanroJump.Hardware.Com.PrizeBoxStatus.Error }
            };

            window.SetPrizeStandStatus(statuses, openingBox: 1);

            var standContainer = root.Q<VisualElement>("PrizeStandContainer");
            Assert.That(standContainer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            
            var cells = standContainer.Query<VisualElement>(className: "prize-stand-cell").ToList();
            Assert.That(cells.Count, Is.EqualTo(3));

            Assert.That(cells[0].Q<Label>().text, Is.EqualTo("1"));
            Assert.That(cells[0].ClassListContains("prize-stand-cell--opening"), Is.True);

            Assert.That(cells[1].Q<Label>().text, Is.EqualTo("2"));
            Assert.That(cells[1].ClassListContains("prize-stand-cell--empty"), Is.True);

            Assert.That(cells[2].Q<Label>().text, Is.EqualTo("3"));
            Assert.That(cells[2].ClassListContains("prize-stand-cell--error"), Is.True);
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement { name = "PrizeResultOverlay" };
            root.Add(new Label { name = "PrizeResultTitle" });
            root.Add(new Label { name = "PrizeResultSubtitle" });
            root.Add(new Label { name = "PrizeResultDetails" });
            var qrContainer = new VisualElement { name = "PrizeQrContainer" };
            qrContainer.style.display = DisplayStyle.None;
            qrContainer.Add(new Image { name = "PrizeQrImage" });
            qrContainer.Add(new Label { name = "PrizeQrPhone" });
            qrContainer.Add(new Label { name = "PrizeQrMessage" });
            root.Add(qrContainer);
            var standContainer = new VisualElement { name = "PrizeStandContainer" };
            standContainer.style.display = DisplayStyle.None;
            root.Add(standContainer);
            root.Add(new Button { name = "PrizeResultReturnButton" });
            return root;
        }
    }
}
