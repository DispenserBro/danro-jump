using UnityEngine;

namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Рендерит QR-матрицу в Texture2D для UI Toolkit Image.
    /// </summary>
    public sealed class QrCodeTextureRenderer
    {
        private const int DefaultPixelsPerModule = 8;
        private const int QuietZoneModules = 4;

        public Texture2D Render(
            QrCodeMatrix matrix,
            int pixelsPerModule = DefaultPixelsPerModule,
            Color? darkColor = null,
            Color? lightColor = null)
        {
            var modulePixels = Mathf.Max(1, pixelsPerModule);
            var moduleCount = matrix != null ? matrix.Size + QuietZoneModules * 2 : QuietZoneModules * 2 + 1;
            var textureSize = moduleCount * modulePixels;
            var darkModuleColor = darkColor ?? Color.black;
            var lightModuleColor = lightColor ?? Color.white;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var moduleX = x / modulePixels - QuietZoneModules;
                    var moduleY = y / modulePixels - QuietZoneModules;
                    var dark = matrix != null && matrix.GetModule(moduleX, moduleY);
                    texture.SetPixel(x, textureSize - 1 - y, dark ? darkModuleColor : lightModuleColor);
                }
            }

            texture.Apply(false, false);
            return texture;
        }
    }
}
