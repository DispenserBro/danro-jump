using System.Collections.Generic;
using System.Text;

namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Минимальный QR encoder: byte mode, ECC Low, версии 1-5.
    /// </summary>
    public static class QrCodeByteModeEncoder
    {
        private static readonly VersionInfo[] Versions =
        {
            new(1, 19, 7),
            new(2, 34, 10),
            new(3, 55, 15),
            new(4, 80, 20),
            new(5, 108, 26),
        };

        private static readonly int[] ExpTable = new int[512];
        private static readonly int[] LogTable = new int[256];

        static QrCodeByteModeEncoder()
        {
            var value = 1;
            for (var index = 0; index < 255; index++)
            {
                ExpTable[index] = value;
                LogTable[value] = index;
                value <<= 1;
                if ((value & 0x100) != 0)
                {
                    value ^= 0x11D;
                }
            }

            for (var index = 255; index < ExpTable.Length; index++)
            {
                ExpTable[index] = ExpTable[index - 255];
            }
        }

        public static bool TryEncode(string text, out QrCodeMatrix matrix, out string error)
        {
            matrix = null;
            var data = Encoding.UTF8.GetBytes(text ?? string.Empty);
            if (!TrySelectVersion(data.Length, out var version))
            {
                error = "QR-ссылка слишком длинная для базового QR-генератора.";
                return false;
            }

            var dataCodewords = BuildDataCodewords(data, version.DataCodewords);
            var eccCodewords = CalculateErrorCorrection(dataCodewords, version.ErrorCorrectionCodewords);
            var allCodewords = new List<int>(dataCodewords.Count + eccCodewords.Length);
            allCodewords.AddRange(dataCodewords);
            allCodewords.AddRange(eccCodewords);

            matrix = DrawMatrix(version.Version, allCodewords);
            error = string.Empty;
            return true;
        }

        private static bool TrySelectVersion(int byteLength, out VersionInfo version)
        {
            foreach (var candidate in Versions)
            {
                if (4 + 8 + byteLength * 8 <= candidate.DataCodewords * 8)
                {
                    version = candidate;
                    return true;
                }
            }

            version = default;
            return false;
        }

        private static List<int> BuildDataCodewords(byte[] data, int capacityCodewords)
        {
            var bits = new List<int>(capacityCodewords * 8);
            AppendBits(bits, 0b0100, 4);
            AppendBits(bits, data.Length, 8);
            foreach (var value in data)
            {
                AppendBits(bits, value, 8);
            }

            var capacityBits = capacityCodewords * 8;
            AppendBits(bits, 0, System.Math.Min(4, capacityBits - bits.Count));
            while (bits.Count % 8 != 0)
            {
                bits.Add(0);
            }

            var codewords = new List<int>(capacityCodewords);
            for (var index = 0; index < bits.Count; index += 8)
            {
                var value = 0;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value << 1) | bits[index + bit];
                }

                codewords.Add(value);
            }

            var pad = 0;
            while (codewords.Count < capacityCodewords)
            {
                codewords.Add((pad++ & 1) == 0 ? 0xEC : 0x11);
            }

            return codewords;
        }

        private static void AppendBits(List<int> bits, int value, int count)
        {
            for (var index = count - 1; index >= 0; index--)
            {
                bits.Add((value >> index) & 1);
            }
        }

        private static int[] CalculateErrorCorrection(IReadOnlyList<int> data, int degree)
        {
            var generator = BuildGeneratorPolynomial(degree);
            var result = new int[degree];
            foreach (var value in data)
            {
                var factor = value ^ result[0];
                for (var index = 0; index < degree - 1; index++)
                {
                    result[index] = result[index + 1];
                }

                result[degree - 1] = 0;
                for (var index = 0; index < degree; index++)
                {
                    result[index] ^= Multiply(generator[index + 1], factor);
                }
            }

            return result;
        }

        private static int[] BuildGeneratorPolynomial(int degree)
        {
            var result = new[] { 1 };
            for (var index = 0; index < degree; index++)
            {
                var next = new int[result.Length + 1];
                for (var coefficient = 0; coefficient < result.Length; coefficient++)
                {
                    next[coefficient] ^= result[coefficient];
                    next[coefficient + 1] ^= Multiply(result[coefficient], ExpTable[index]);
                }

                result = next;
            }

            return result;
        }

        private static int Multiply(int left, int right)
        {
            return left == 0 || right == 0 ? 0 : ExpTable[LogTable[left] + LogTable[right]];
        }

        private static QrCodeMatrix DrawMatrix(int version, IReadOnlyList<int> codewords)
        {
            var size = version * 4 + 17;
            var matrix = new QrCodeMatrix(size);
            var function = new bool[size, size];

            DrawFinder(matrix, function, 0, 0);
            DrawFinder(matrix, function, size - 7, 0);
            DrawFinder(matrix, function, 0, size - 7);
            DrawTiming(matrix, function);
            DrawAlignment(matrix, function, version);
            matrix.SetModule(8, size - 8, true);
            function[size - 8, 8] = true;
            ReserveFormat(function, size);
            DrawData(matrix, function, codewords);
            DrawFormat(matrix);
            return matrix;
        }

        private static void DrawFinder(QrCodeMatrix matrix, bool[,] function, int x, int y)
        {
            for (var dy = -1; dy <= 7; dy++)
            {
                for (var dx = -1; dx <= 7; dx++)
                {
                    var xx = x + dx;
                    var yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= matrix.Size || yy >= matrix.Size)
                    {
                        continue;
                    }

                    var inside = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6;
                    var dark = inside &&
                        (dx == 0 || dx == 6 || dy == 0 || dy == 6 ||
                            dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4);
                    matrix.SetModule(xx, yy, dark);
                    function[yy, xx] = true;
                }
            }
        }

        private static void DrawTiming(QrCodeMatrix matrix, bool[,] function)
        {
            for (var index = 8; index < matrix.Size - 8; index++)
            {
                var dark = (index & 1) == 0;
                matrix.SetModule(index, 6, dark);
                matrix.SetModule(6, index, dark);
                function[6, index] = true;
                function[index, 6] = true;
            }
        }

        private static void DrawAlignment(QrCodeMatrix matrix, bool[,] function, int version)
        {
            if (version <= 1)
            {
                return;
            }

            var positions = new[] { 6, matrix.Size - 7 };
            foreach (var y in positions)
            {
                foreach (var x in positions)
                {
                    if (function[y, x])
                    {
                        continue;
                    }

                    for (var dy = -2; dy <= 2; dy++)
                    {
                        for (var dx = -2; dx <= 2; dx++)
                        {
                            var distance = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
                            matrix.SetModule(x + dx, y + dy, distance != 1);
                            function[y + dy, x + dx] = true;
                        }
                    }
                }
            }
        }

        private static void ReserveFormat(bool[,] function, int size)
        {
            for (var index = 0; index < 9; index++)
            {
                if (index != 6)
                {
                    function[8, index] = true;
                    function[index, 8] = true;
                }
            }

            for (var index = 0; index < 8; index++)
            {
                function[8, size - 1 - index] = true;
                function[size - 1 - index, 8] = true;
            }
        }

        private static void DrawData(QrCodeMatrix matrix, bool[,] function, IReadOnlyList<int> codewords)
        {
            var bits = new List<int>(codewords.Count * 8);
            foreach (var value in codewords)
            {
                AppendBits(bits, value, 8);
            }

            var bitIndex = 0;
            var upward = true;
            for (var right = matrix.Size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                {
                    right--;
                }

                for (var vertical = 0; vertical < matrix.Size; vertical++)
                {
                    var y = upward ? matrix.Size - 1 - vertical : vertical;
                    for (var column = 0; column < 2; column++)
                    {
                        var x = right - column;
                        if (function[y, x])
                        {
                            continue;
                        }

                        var dark = bitIndex < bits.Count && bits[bitIndex++] != 0;
                        if (((x + y) & 1) == 0)
                        {
                            dark = !dark;
                        }

                        matrix.SetModule(x, y, dark);
                    }
                }

                upward = !upward;
            }
        }

        private static void DrawFormat(QrCodeMatrix matrix)
        {
            var bits = CalculateFormatBits();
            for (var index = 0; index <= 5; index++)
            {
                matrix.SetModule(8, index, GetBit(bits, index));
            }

            matrix.SetModule(8, 7, GetBit(bits, 6));
            matrix.SetModule(8, 8, GetBit(bits, 7));
            matrix.SetModule(7, 8, GetBit(bits, 8));
            for (var index = 9; index < 15; index++)
            {
                matrix.SetModule(14 - index, 8, GetBit(bits, index));
            }

            for (var index = 0; index < 8; index++)
            {
                matrix.SetModule(matrix.Size - 1 - index, 8, GetBit(bits, index));
            }

            for (var index = 8; index < 15; index++)
            {
                matrix.SetModule(8, matrix.Size - 15 + index, GetBit(bits, index));
            }

            matrix.SetModule(8, matrix.Size - 8, true);
        }

        private static int CalculateFormatBits()
        {
            const int errorCorrectionLow = 1;
            const int mask = 0;
            const int generator = 0x537;
            var data = (errorCorrectionLow << 3) | mask;
            var value = data << 10;
            for (var bit = 14; bit >= 10; bit--)
            {
                if (((value >> bit) & 1) != 0)
                {
                    value ^= generator << (bit - 10);
                }
            }

            return ((data << 10) | value) ^ 0x5412;
        }

        private static bool GetBit(int value, int index)
        {
            return ((value >> index) & 1) != 0;
        }

        private readonly struct VersionInfo
        {
            public VersionInfo(int version, int dataCodewords, int errorCorrectionCodewords)
            {
                Version = version;
                DataCodewords = dataCodewords;
                ErrorCorrectionCodewords = errorCorrectionCodewords;
            }

            public int Version { get; }
            public int DataCodewords { get; }
            public int ErrorCorrectionCodewords { get; }
        }
    }
}
