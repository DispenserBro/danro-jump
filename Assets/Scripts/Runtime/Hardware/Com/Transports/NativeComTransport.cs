using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Получает список COM-портов через native-плагин ComNative.
    /// </summary>
    public sealed class NativeComPortProvider : IComPortProvider
    {
        public string[] GetPortNames()
        {
            try
            {
                string[] ports = ComNativeMethods.ListPorts();
                Debug.Log($"[ComSystem] ComNative.ListPorts returned: {(ports.Length > 0 ? string.Join(", ", ports) : "<none>")}");
                return ports;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ComSystem] ComNative.ListPorts failed: {exception.GetType().Name}: {exception.Message}");
                return Array.Empty<string>();
            }
        }
    }

    /// <summary>
    /// Создает COM-транспорт через native-плагин ComNative.
    /// </summary>
    public sealed class NativeComTransportFactory : IComTransportFactory
    {
        public IComTransport Create(SerialSettings settings)
        {
            return new NativeComTransport(settings);
        }
    }

    /// <summary>
    /// Native serial-транспорт для IL2CPP-билдов, где System.IO.Ports.SerialPort может быть недоступен.
    /// </summary>
    internal sealed class NativeComTransport : IComTransport
    {
        private const int ReadBufferSize = 4096;
        private const int MaxFlushLines = 100;

        private readonly SerialSettings settings;
        private int handleId;

        public NativeComTransport(SerialSettings settings)
        {
            this.settings = settings;
        }

        public bool IsOpen => handleId > 0;

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            handleId = ComNativeMethods.Serial_Open(
                settings.PortName,
                settings.BaudRate,
                settings.DataBits,
                ConvertParity(settings.Parity),
                ConvertStopBits(settings.StopBits),
                settings.ReadTimeoutMs,
                settings.WriteTimeoutMs);

            if (handleId <= 0)
            {
                string nativeError = ComNativeMethods.GetLastError();
                handleId = 0;
                throw new IOException($"ComNative failed to open {settings.PortName}: {nativeError}");
            }

            Debug.Log($"[ComSystem] ComNative opened {settings.PortName} with handle {handleId}.");
        }

        public void Close()
        {
            if (handleId <= 0)
            {
                return;
            }

            int closingHandle = handleId;
            handleId = 0;
            int result = ComNativeMethods.Serial_Close(closingHandle);
            if (result < 0)
            {
                Debug.LogWarning($"[ComSystem] ComNative close failed for handle {closingHandle}: {ComNativeMethods.GetLastError()}");
            }
        }

        public void DiscardInBuffer()
        {
            EnsureOpen();

            byte[] buffer = new byte[ReadBufferSize];
            for (int i = 0; i < MaxFlushLines; i++)
            {
                int read = ComNativeMethods.Serial_ReadLineUTF8_bytes(handleId, buffer, buffer.Length);
                if (read <= 0)
                {
                    return;
                }
            }
        }

        public void DiscardOutBuffer()
        {
            // Native plugin has no explicit outgoing-buffer flush API.
        }

        public string ReadLine()
        {
            EnsureOpen();

            byte[] buffer = new byte[ReadBufferSize];
            int read = ComNativeMethods.Serial_ReadLineUTF8_bytes(handleId, buffer, buffer.Length);
            if (read == 0)
            {
                throw new TimeoutException();
            }

            if (read < 0)
            {
                throw new IOException($"ComNative read failed on {settings.PortName}: {ComNativeMethods.GetLastError()}");
            }

            int count = Math.Min(read, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, count).TrimEnd('\r', '\n');
        }

        public void WriteLine(string value)
        {
            EnsureOpen();

            int result = ComNativeMethods.Serial_WriteStringUTF8(handleId, value ?? "", appendNewline: 1);
            if (result < 0)
            {
                throw new IOException($"ComNative write failed on {settings.PortName}: {ComNativeMethods.GetLastError()}");
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"ComNative port {settings.PortName} is not open.");
            }
        }

        private static int ConvertParity(SerialParity parity)
        {
            return parity switch
            {
                SerialParity.Odd => 1,
                SerialParity.Even => 2,
                SerialParity.Mark => 3,
                SerialParity.Space => 4,
                _ => 0
            };
        }

        private static int ConvertStopBits(SerialStopBits stopBits)
        {
            return stopBits switch
            {
                SerialStopBits.Two => 2,
                SerialStopBits.OnePointFive => 3,
                _ => 1
            };
        }
    }

    internal static class ComNativeMethods
    {
        private const string Dll = "ComNative";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Serial_ListPortsLength();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Serial_ListPorts(StringBuilder buffer, int bufferChars);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Serial_Open(
            string portName,
            int baud,
            int dataBits,
            int parity,
            int stopBits,
            int readTimeoutMs,
            int writeTimeoutMs);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Serial_Close(int handleId);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Serial_CloseAll();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Serial_WriteStringUTF8(int handleId, string utf8, int appendNewline);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Serial_ReadLineUTF8")]
        public static extern int Serial_ReadLineUTF8_bytes(int handleId, byte[] buffer, int bufferSize);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Serial_GetLastErrorW(StringBuilder buffer, int bufferChars);

        public static string GetLastError()
        {
            int requiredLength = Serial_GetLastErrorW(null, 0);
            if (requiredLength < 0)
            {
                return $"Serial_GetLastErrorW failed with code {requiredLength}.";
            }

            var buffer = new StringBuilder(Math.Max(requiredLength, 1));
            int result = Serial_GetLastErrorW(buffer, buffer.Capacity);
            return result < 0 ? $"Serial_GetLastErrorW failed with code {result}." : buffer.ToString();
        }

        public static string[] ListPorts()
        {
            int requiredLength = Serial_ListPortsLength();
            if (requiredLength <= 1)
            {
                return Array.Empty<string>();
            }

            var buffer = new StringBuilder(requiredLength);
            int result = Serial_ListPorts(buffer, buffer.Capacity);
            if (result < 0)
            {
                throw new IOException($"ComNative port listing failed: {GetLastError()}");
            }

            string joined = buffer.ToString();
            if (string.IsNullOrWhiteSpace(joined))
            {
                return Array.Empty<string>();
            }

            string[] rawPorts = joined.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < rawPorts.Length; i++)
            {
                rawPorts[i] = rawPorts[i].Trim();
            }

            return rawPorts;
        }
    }
}
