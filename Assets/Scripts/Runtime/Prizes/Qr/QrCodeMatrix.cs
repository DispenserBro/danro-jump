namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Чистая матрица QR-модулей.
    /// </summary>
    public sealed class QrCodeMatrix
    {
        private readonly bool[,] modules;

        public QrCodeMatrix(int size)
        {
            Size = size > 0 ? size : 1;
            modules = new bool[Size, Size];
        }

        public int Size { get; }

        public bool GetModule(int x, int y)
        {
            return x >= 0 && x < Size && y >= 0 && y < Size && modules[y, x];
        }

        public void SetModule(int x, int y, bool value)
        {
            if (x >= 0 && x < Size && y >= 0 && y < Size)
            {
                modules[y, x] = value;
            }
        }
    }
}
