using System;
using System.Reflection;
using System.Text;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Создает COM-транспорт через reflection-обертку над System.IO.Ports.SerialPort.
    /// </summary>
    public sealed class ReflectionSerialComTransportFactory : IComTransportFactory
    {
        /// <summary>
        /// Создает транспорт для указанного serial-порта.
        /// </summary>
        public IComTransport Create(SerialSettings settings)
        {
            return ReflectionSerialComTransport.Create(settings);
        }
    }

    /// <summary>
    /// Reflection-адаптер serial-порта, который изолирует проект от прямой ссылки на System.IO.Ports.
    /// </summary>
    internal sealed class ReflectionSerialComTransport : IComTransport
    {
        private const BindingFlags InstancePublicFlags = BindingFlags.Public | BindingFlags.Instance;

        private static readonly Type SerialPortType = FindSystemIoPortsType("SerialPort");
        private static readonly Type ParityType = FindSystemIoPortsType("Parity");
        private static readonly Type StopBitsType = FindSystemIoPortsType("StopBits");
        private static readonly PropertyInfo IsOpenProperty = SerialPortType?.GetProperty("IsOpen", InstancePublicFlags);
        private static readonly PropertyInfo DataBitsProperty = SerialPortType?.GetProperty("DataBits", InstancePublicFlags);
        private static readonly PropertyInfo ParityProperty = SerialPortType?.GetProperty("Parity", InstancePublicFlags);
        private static readonly PropertyInfo StopBitsProperty = SerialPortType?.GetProperty("StopBits", InstancePublicFlags);
        private static readonly PropertyInfo ReadTimeoutProperty = SerialPortType?.GetProperty("ReadTimeout", InstancePublicFlags);
        private static readonly PropertyInfo WriteTimeoutProperty = SerialPortType?.GetProperty("WriteTimeout", InstancePublicFlags);
        private static readonly PropertyInfo NewLineProperty = SerialPortType?.GetProperty("NewLine", InstancePublicFlags);
        private static readonly PropertyInfo EncodingProperty = SerialPortType?.GetProperty("Encoding", InstancePublicFlags);
        private static readonly PropertyInfo DtrEnableProperty = SerialPortType?.GetProperty("DtrEnable", InstancePublicFlags);
        private static readonly PropertyInfo RtsEnableProperty = SerialPortType?.GetProperty("RtsEnable", InstancePublicFlags);
        private static readonly MethodInfo OpenMethod = SerialPortType?.GetMethod("Open", InstancePublicFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo CloseMethod = SerialPortType?.GetMethod("Close", InstancePublicFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo DiscardInBufferMethod = SerialPortType?.GetMethod("DiscardInBuffer", InstancePublicFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo DiscardOutBufferMethod = SerialPortType?.GetMethod("DiscardOutBuffer", InstancePublicFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo ReadLineMethod = SerialPortType?.GetMethod("ReadLine", InstancePublicFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo WriteLineMethod = SerialPortType?.GetMethod("WriteLine", InstancePublicFlags, null, new[] { typeof(string) }, null);

        private readonly object instance;

        private ReflectionSerialComTransport(object instance)
        {
            this.instance = instance;
        }

        /// <summary>
        /// Показывает, открыт ли underlying serial-порт.
        /// </summary>
        public bool IsOpen => GetBoolProperty(IsOpenProperty);

        /// <summary>
        /// Создает и настраивает экземпляр SerialPort через reflection.
        /// </summary>
        public static ReflectionSerialComTransport Create(SerialSettings settings)
        {
            if (SerialPortType == null)
            {
                throw new InvalidOperationException(
                    "System.IO.Ports.SerialPort is not available. " +
                    "Check Unity API compatibility/scripting backend and whether System.IO.Ports is present in the player.");
            }

            EnsureRequiredMetadata();

            object serialPort = Activator.CreateInstance(SerialPortType, settings.PortName, settings.BaudRate);
            var transport = new ReflectionSerialComTransport(serialPort);
            transport.SetProperty(DataBitsProperty, settings.DataBits);
            transport.SetProperty(ParityProperty, ParseEnum(ParityType, settings.Parity.ToString()));
            transport.SetProperty(StopBitsProperty, ParseEnum(StopBitsType, settings.StopBits.ToString()));
            transport.SetProperty(ReadTimeoutProperty, settings.ReadTimeoutMs);
            transport.SetProperty(WriteTimeoutProperty, settings.WriteTimeoutMs);
            transport.SetProperty(NewLineProperty, "\n");
            transport.SetProperty(EncodingProperty, Encoding.UTF8);
            transport.SetProperty(DtrEnableProperty, true);
            transport.SetProperty(RtsEnableProperty, true);
            return transport;
        }

        /// <summary>
        /// Открывает serial-порт.
        /// </summary>
        public void Open()
        {
            Invoke(OpenMethod);
        }

        /// <summary>
        /// Закрывает serial-порт.
        /// </summary>
        public void Close()
        {
            Invoke(CloseMethod);
        }

        /// <summary>
        /// Очищает входной буфер serial-порта.
        /// </summary>
        public void DiscardInBuffer()
        {
            Invoke(DiscardInBufferMethod);
        }

        /// <summary>
        /// Очищает выходной буфер serial-порта.
        /// </summary>
        public void DiscardOutBuffer()
        {
            Invoke(DiscardOutBufferMethod);
        }

        /// <summary>
        /// Читает одну строку из serial-порта.
        /// </summary>
        public string ReadLine()
        {
            return (string)Invoke(ReadLineMethod);
        }

        /// <summary>
        /// Записывает одну строку в serial-порт.
        /// </summary>
        public void WriteLine(string value)
        {
            Invoke(WriteLineMethod, value);
        }

        /// <summary>
        /// Освобождает underlying SerialPort, если он поддерживает IDisposable.
        /// </summary>
        public void Dispose()
        {
            if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Проверяет наличие всех reflection-членов заранее, чтобы ошибки были явными.
        /// </summary>
        private static void EnsureRequiredMetadata()
        {
            Require(DataBitsProperty, "DataBits");
            Require(ParityProperty, "Parity");
            Require(StopBitsProperty, "StopBits");
            Require(ReadTimeoutProperty, "ReadTimeout");
            Require(WriteTimeoutProperty, "WriteTimeout");
            Require(NewLineProperty, "NewLine");
            Require(EncodingProperty, "Encoding");
            Require(DtrEnableProperty, "DtrEnable");
            Require(RtsEnableProperty, "RtsEnable");
            Require(IsOpenProperty, "IsOpen");
            Require(OpenMethod, "Open()");
            Require(CloseMethod, "Close()");
            Require(DiscardInBufferMethod, "DiscardInBuffer()");
            Require(DiscardOutBufferMethod, "DiscardOutBuffer()");
            Require(ReadLineMethod, "ReadLine()");
            Require(WriteLineMethod, "WriteLine(string)");
        }

        private static void Require(MemberInfo member, string memberName)
        {
            if (member == null)
            {
                throw new MissingMemberException(SerialPortType?.FullName ?? "System.IO.Ports.SerialPort", memberName);
            }
        }

        /// <summary>
        /// Читает bool-свойство SerialPort через заранее найденный PropertyInfo.
        /// </summary>
        private bool GetBoolProperty(PropertyInfo property)
        {
            return property.GetValue(instance) is bool value && value;
        }

        /// <summary>
        /// Записывает свойство SerialPort через заранее найденный PropertyInfo.
        /// </summary>
        private void SetProperty(PropertyInfo property, object value)
        {
            property.SetValue(instance, value);
        }

        /// <summary>
        /// Вызывает метод SerialPort через reflection и пробрасывает реальную inner-ошибку.
        /// </summary>
        private object Invoke(MethodInfo method, params object[] args)
        {
            try
            {
                return method.Invoke(instance, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                // Наружу пробрасываем реальную ошибку serial-порта, а не reflection-обертку.
                throw exception.InnerException;
            }
        }

        private static object ParseEnum(Type enumType, string value)
        {
            if (enumType == null)
            {
                throw new InvalidOperationException("System.IO.Ports enum metadata is not available.");
            }

            return Enum.Parse(enumType, value);
        }

        private static Type FindSystemIoPortsType(string typeName)
        {
            string fullName = "System.IO.Ports." + typeName;
            var type = Type.GetType(fullName + ", System", false) ??
                Type.GetType(fullName + ", System.IO.Ports", false);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
