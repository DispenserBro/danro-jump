using DanroJump.Prizes.Qr;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class QrCodeByteModeEncoderTests
    {
        [Test]
        public void TryEncode_CreatesNonEmptyDeterministicMatrix()
        {
            const string payload = "https://t.me/+79999999999?text=QR%20prize%20Danro%20Jump";

            var first = QrCodeByteModeEncoder.TryEncode(payload, out var firstMatrix, out var firstError);
            var second = QrCodeByteModeEncoder.TryEncode(payload, out var secondMatrix, out var secondError);

            Assert.That(first, Is.True, firstError);
            Assert.That(second, Is.True, secondError);
            Assert.That(firstMatrix.Size, Is.EqualTo(secondMatrix.Size));
            Assert.That(CountDarkModules(firstMatrix), Is.GreaterThan(0));
            Assert.That(CountDarkModules(firstMatrix), Is.EqualTo(CountDarkModules(secondMatrix)));
        }

        [Test]
        public void TryEncode_TooLongPayloadFailsSafely()
        {
            var payload = "https://t.me/+79999999999?text=" + new string('a', 140);

            var success = QrCodeByteModeEncoder.TryEncode(payload, out var matrix, out var error);

            Assert.That(success, Is.False);
            Assert.That(matrix, Is.Null);
            Assert.That(error, Does.Contain("слишком длинная"));
        }

        [Test]
        public void Render_AddsQuietZoneAndUsesPointFilter()
        {
            QrCodeByteModeEncoder.TryEncode("https://t.me/+79999999999?text=QR", out var matrix, out _);

            var texture = new QrCodeTextureRenderer().Render(matrix, pixelsPerModule: 2);

            try
            {
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(texture.width, Is.EqualTo((matrix.Size + 8) * 2));
                Assert.That(texture.GetPixel(0, 0), Is.EqualTo(Color.white));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static int CountDarkModules(QrCodeMatrix matrix)
        {
            var count = 0;
            for (var y = 0; y < matrix.Size; y++)
            {
                for (var x = 0; x < matrix.Size; x++)
                {
                    if (matrix.GetModule(x, y))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
