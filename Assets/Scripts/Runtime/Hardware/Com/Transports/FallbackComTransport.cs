using System;
using UnityEngine;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Поставщик портов, который переходит на запасной источник, если основной не нашел порты или упал.
    /// </summary>
    public sealed class FallbackComPortProvider : IComPortProvider
    {
        private readonly IComPortProvider primaryProvider;
        private readonly IComPortProvider fallbackProvider;

        public FallbackComPortProvider(IComPortProvider primaryProvider, IComPortProvider fallbackProvider)
        {
            this.primaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
            this.fallbackProvider = fallbackProvider ?? throw new ArgumentNullException(nameof(fallbackProvider));
        }

        public string[] GetPortNames()
        {
            try
            {
                string[] primaryPorts = primaryProvider.GetPortNames() ?? Array.Empty<string>();
                if (primaryPorts.Length > 0)
                {
                    return primaryPorts;
                }

                Debug.LogWarning("[ComSystem] Primary COM port provider returned no ports. Trying native ComNative provider.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ComSystem] Primary COM port provider failed: {exception.GetType().Name}: {exception.Message}. Trying native ComNative provider.");
            }

            return fallbackProvider.GetPortNames() ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Фабрика транспорта, которая открывает порт через основной backend и переключается на запасной при ошибке.
    /// </summary>
    public sealed class FallbackComTransportFactory : IComTransportFactory
    {
        private readonly IComTransportFactory primaryFactory;
        private readonly IComTransportFactory fallbackFactory;

        public FallbackComTransportFactory(IComTransportFactory primaryFactory, IComTransportFactory fallbackFactory)
        {
            this.primaryFactory = primaryFactory ?? throw new ArgumentNullException(nameof(primaryFactory));
            this.fallbackFactory = fallbackFactory ?? throw new ArgumentNullException(nameof(fallbackFactory));
        }

        public IComTransport Create(SerialSettings settings)
        {
            return new FallbackComTransport(settings, primaryFactory, fallbackFactory);
        }
    }

    internal sealed class FallbackComTransport : IComTransport
    {
        private const string PrimaryBackendName = "System.IO.Ports";
        private const string FallbackBackendName = "ComNative";

        private readonly SerialSettings settings;
        private readonly IComTransportFactory primaryFactory;
        private readonly IComTransportFactory fallbackFactory;
        private IComTransport activeTransport;
        private string activeBackendName = "";

        public FallbackComTransport(
            SerialSettings settings,
            IComTransportFactory primaryFactory,
            IComTransportFactory fallbackFactory)
        {
            this.settings = settings;
            this.primaryFactory = primaryFactory;
            this.fallbackFactory = fallbackFactory;
        }

        public bool IsOpen => activeTransport != null && activeTransport.IsOpen;

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            Exception primaryException = null;
            try
            {
                OpenWith(primaryFactory, PrimaryBackendName);
                return;
            }
            catch (Exception exception)
            {
                primaryException = exception;
                DisposeActiveTransport();
                Debug.LogWarning($"[ComSystem] System.IO.Ports failed for {settings.PortName}: {exception.GetType().Name}: {exception.Message}. Trying ComNative.");
            }

            try
            {
                OpenWith(fallbackFactory, FallbackBackendName);
            }
            catch (Exception fallbackException)
            {
                DisposeActiveTransport();
                throw new InvalidOperationException(
                    $"Both COM backends failed for {settings.PortName}. " +
                    $"System.IO.Ports: {primaryException.GetType().Name}: {primaryException.Message}. " +
                    $"ComNative: {fallbackException.GetType().Name}: {fallbackException.Message}",
                    fallbackException);
            }
        }

        public void Close()
        {
            activeTransport?.Close();
        }

        public void DiscardInBuffer()
        {
            try
            {
                RequireActiveTransport().DiscardInBuffer();
            }
            catch (Exception exception) when (TrySwitchToFallback(exception, "discard input buffer"))
            {
                RequireActiveTransport().DiscardInBuffer();
            }
        }

        public void DiscardOutBuffer()
        {
            try
            {
                RequireActiveTransport().DiscardOutBuffer();
            }
            catch (Exception exception) when (TrySwitchToFallback(exception, "discard output buffer"))
            {
                RequireActiveTransport().DiscardOutBuffer();
            }
        }

        public string ReadLine()
        {
            try
            {
                return RequireActiveTransport().ReadLine();
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception exception) when (TrySwitchToFallback(exception, "read"))
            {
                return RequireActiveTransport().ReadLine();
            }
        }

        public void WriteLine(string value)
        {
            try
            {
                RequireActiveTransport().WriteLine(value);
            }
            catch (Exception exception) when (TrySwitchToFallback(exception, "write"))
            {
                RequireActiveTransport().WriteLine(value);
            }
        }

        public void Dispose()
        {
            DisposeActiveTransport();
        }

        private void OpenWith(IComTransportFactory factory, string backendName)
        {
            activeTransport = factory.Create(settings);
            activeTransport.Open();
            activeBackendName = backendName;
            Debug.Log($"[ComSystem] {settings.PortName} serial backend selected: {activeBackendName}.");
        }

        private IComTransport RequireActiveTransport()
        {
            if (activeTransport == null)
            {
                throw new InvalidOperationException($"COM port {settings.PortName} has no active transport backend.");
            }

            return activeTransport;
        }

        private bool TrySwitchToFallback(Exception exception, string operationName)
        {
            if (!string.Equals(activeBackendName, PrimaryBackendName, StringComparison.Ordinal))
            {
                return false;
            }

            Debug.LogWarning(
                $"[ComSystem] {PrimaryBackendName} {operationName} failed for {settings.PortName}: " +
                $"{exception.GetType().Name}: {exception.Message}. Switching to {FallbackBackendName}.");

            DisposeActiveTransport();
            OpenWith(fallbackFactory, FallbackBackendName);
            return true;
        }

        private void DisposeActiveTransport()
        {
            try
            {
                activeTransport?.Dispose();
            }
            finally
            {
                activeTransport = null;
                activeBackendName = "";
            }
        }
    }
}
