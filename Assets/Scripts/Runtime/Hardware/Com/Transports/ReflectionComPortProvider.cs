using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Получает список COM-портов через reflection, чтобы проект не падал в Unity-профилях без System.IO.Ports.
    /// </summary>
    public sealed class ReflectionComPortProvider : IComPortProvider
    {
        private static readonly Type SerialPortType = FindSerialPortType();
        private static bool diagnosticLogged;

        /// <summary>
        /// Возвращает имена доступных serial-портов или пустой список, если API недоступен.
        /// </summary>
        public string[] GetPortNames()
        {
            LogSerialPortDiagnosticOnce();

            string[] registryPorts = GetWindowsRegistryPortNames();
            if (SerialPortType == null)
            {
                Debug.LogWarning("[ComSystem] System.IO.Ports.SerialPort is not available in this Unity profile.");
                return registryPorts;
            }

            try
            {
                var getPortNamesMethod = SerialPortType.GetMethod("GetPortNames", BindingFlags.Public | BindingFlags.Static);
                if (getPortNamesMethod == null)
                {
                    Debug.LogWarning("[ComSystem] System.IO.Ports.SerialPort.GetPortNames() is not available.");
                    return registryPorts;
                }

                var ports = (string[])getPortNamesMethod.Invoke(null, null) ?? Array.Empty<string>();
                Debug.Log($"[ComSystem] SerialPort.GetPortNames returned: {(ports.Length > 0 ? string.Join(", ", ports) : "<none>")}");
                return ports.Length > 0 ? ports : registryPorts;
            }
            catch (Exception exception)
            {
                Exception unwrapped = Unwrap(exception);
                Debug.LogWarning($"[ComSystem] Failed to list COM ports via SerialPort.GetPortNames: {unwrapped.GetType().Name}: {unwrapped.Message}");
                return registryPorts;
            }
        }

        private static string[] GetWindowsRegistryPortNames()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                Type registryType = FindType("Microsoft.Win32.Registry");
                Type registryKeyType = FindType("Microsoft.Win32.RegistryKey");
                if (registryType == null || registryKeyType == null)
                {
                    Debug.Log("[ComSystem] Windows registry API is not available for COM fallback.");
                    return Array.Empty<string>();
                }

                object localMachine = registryType.GetProperty("LocalMachine", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);
                if (localMachine == null)
                {
                    localMachine = registryType.GetField("LocalMachine", BindingFlags.Public | BindingFlags.Static)
                        ?.GetValue(null);
                }

                if (localMachine == null)
                {
                    Debug.Log("[ComSystem] Windows registry fallback could not access LocalMachine.");
                    return Array.Empty<string>();
                }

                MethodInfo openSubKey = registryKeyType.GetMethod(
                    "OpenSubKey",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);
                MethodInfo getValueNames = registryKeyType.GetMethod(
                    "GetValueNames",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                MethodInfo getValue = registryKeyType.GetMethod(
                    "GetValue",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);

                if (openSubKey == null || getValueNames == null || getValue == null)
                {
                    Debug.Log("[ComSystem] Windows registry fallback methods are not available.");
                    return Array.Empty<string>();
                }

                object serialCommKey = openSubKey.Invoke(localMachine, new object[] { @"HARDWARE\DEVICEMAP\SERIALCOMM" });
                if (serialCommKey == null)
                {
                    Debug.Log("[ComSystem] Windows registry fallback did not find HARDWARE\\DEVICEMAP\\SERIALCOMM.");
                    return Array.Empty<string>();
                }

                using (serialCommKey as IDisposable)
                {
                    var valueNames = (string[])getValueNames.Invoke(serialCommKey, null) ?? Array.Empty<string>();
                    var ports = new List<string>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (string valueName in valueNames)
                    {
                        string portName = getValue.Invoke(serialCommKey, new object[] { valueName }) as string;
                        if (string.IsNullOrWhiteSpace(portName))
                        {
                            continue;
                        }

                        portName = portName.Trim();
                        if (!seen.Add(portName))
                        {
                            continue;
                        }

                        ports.Add(portName);
                    }

                    ports.Sort(StringComparer.OrdinalIgnoreCase);
                    Debug.Log($"[ComSystem] Windows registry COM fallback returned: {(ports.Count > 0 ? string.Join(", ", ports) : "<none>")}");
                    return ports.ToArray();
                }
            }
            catch (Exception exception)
            {
                Exception unwrapped = Unwrap(exception);
                Debug.LogWarning($"[ComSystem] Windows registry COM fallback failed: {unwrapped.GetType().Name}: {unwrapped.Message}");
                return Array.Empty<string>();
            }
#else
            return Array.Empty<string>();
#endif
        }

        private static Type FindSerialPortType()
        {
            var type = Type.GetType("System.IO.Ports.SerialPort, System", false) ??
                Type.GetType("System.IO.Ports.SerialPort, System.IO.Ports", false);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("System.IO.Ports.SerialPort", false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void LogSerialPortDiagnosticOnce()
        {
            if (diagnosticLogged)
            {
                return;
            }

            diagnosticLogged = true;
            string message = SerialPortType == null
                ? "SerialPort API resolution failed."
                : $"SerialPort API resolved from assembly: {SerialPortType.Assembly.FullName}";
            Debug.Log($"[ComSystem] {message}");
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Возвращает исходную ошибку reflection-вызова без TargetInvocationException-обертки.
        /// </summary>
        private static Exception Unwrap(Exception exception)
        {
            return exception is TargetInvocationException { InnerException: not null }
                ? exception.InnerException
                : exception;
        }
    }
}
