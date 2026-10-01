using System;
using DanroJump.Hardware.Com;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class ComTransportFallbackTests
    {
        private static readonly SerialSettings TestSettings = new(
            "COM_TEST",
            115200,
            8,
            SerialParity.None,
            SerialStopBits.One,
            50,
            100);

        [Test]
        public void FallbackComTransportFactory_Open_UsesPrimaryWhenPrimarySucceeds()
        {
            var primaryTransport = new FakeComTransport();
            var fallbackTransport = new FakeComTransport();
            var factory = new FallbackComTransportFactory(
                new FakeComTransportFactory(primaryTransport),
                new FakeComTransportFactory(fallbackTransport));

            using IComTransport transport = factory.Create(TestSettings);
            transport.Open();

            Assert.That(primaryTransport.OpenCount, Is.EqualTo(1));
            Assert.That(fallbackTransport.OpenCount, Is.EqualTo(0));
            Assert.That(transport.IsOpen, Is.True);
        }

        [Test]
        public void FallbackComTransportFactory_Open_UsesFallbackWhenPrimaryOpenFails()
        {
            var primaryTransport = new FakeComTransport(openException: new InvalidOperationException("SerialPort failed"));
            var fallbackTransport = new FakeComTransport();
            var factory = new FallbackComTransportFactory(
                new FakeComTransportFactory(primaryTransport),
                new FakeComTransportFactory(fallbackTransport));

            using IComTransport transport = factory.Create(TestSettings);
            transport.Open();

            Assert.That(primaryTransport.OpenCount, Is.EqualTo(1));
            Assert.That(primaryTransport.DisposeCount, Is.EqualTo(1));
            Assert.That(fallbackTransport.OpenCount, Is.EqualTo(1));
            Assert.That(transport.IsOpen, Is.True);
        }

        [Test]
        public void FallbackComTransportFactory_DiscardInBuffer_UsesFallbackWhenPrimaryOperationFails()
        {
            var primaryTransport = new FakeComTransport(discardInException: new NotSupportedException("Discard failed"));
            var fallbackTransport = new FakeComTransport();
            var factory = new FallbackComTransportFactory(
                new FakeComTransportFactory(primaryTransport),
                new FakeComTransportFactory(fallbackTransport));

            using IComTransport transport = factory.Create(TestSettings);
            transport.Open();
            transport.DiscardInBuffer();

            Assert.That(primaryTransport.OpenCount, Is.EqualTo(1));
            Assert.That(primaryTransport.DisposeCount, Is.EqualTo(1));
            Assert.That(fallbackTransport.OpenCount, Is.EqualTo(1));
            Assert.That(fallbackTransport.DiscardInBufferCount, Is.EqualTo(1));
            Assert.That(transport.IsOpen, Is.True);
        }

        [Test]
        public void FallbackComPortProvider_GetPortNames_UsesFallbackWhenPrimaryReturnsEmpty()
        {
            var provider = new FallbackComPortProvider(
                new FakeComPortProvider(Array.Empty<string>()),
                new FakeComPortProvider(new[] { "COM3" }));

            string[] ports = provider.GetPortNames();

            Assert.That(ports, Is.EqualTo(new[] { "COM3" }));
        }

        private sealed class FakeComPortProvider : IComPortProvider
        {
            private readonly string[] ports;

            public FakeComPortProvider(string[] ports)
            {
                this.ports = ports;
            }

            public string[] GetPortNames()
            {
                return ports;
            }
        }

        private sealed class FakeComTransportFactory : IComTransportFactory
        {
            private readonly IComTransport transport;

            public FakeComTransportFactory(IComTransport transport)
            {
                this.transport = transport;
            }

            public IComTransport Create(SerialSettings settings)
            {
                return transport;
            }
        }

        private sealed class FakeComTransport : IComTransport
        {
            private readonly Exception openException;
            private readonly Exception discardInException;
            private bool isOpen;

            public FakeComTransport(Exception openException = null, Exception discardInException = null)
            {
                this.openException = openException;
                this.discardInException = discardInException;
            }

            public int OpenCount { get; private set; }
            public int DiscardInBufferCount { get; private set; }
            public int DisposeCount { get; private set; }
            public bool IsOpen => isOpen;

            public void Open()
            {
                OpenCount++;
                if (openException != null)
                {
                    throw openException;
                }

                isOpen = true;
            }

            public void Close()
            {
                isOpen = false;
            }

            public void DiscardInBuffer()
            {
                DiscardInBufferCount++;
                if (discardInException != null)
                {
                    throw discardInException;
                }
            }

            public void DiscardOutBuffer() { }
            public string ReadLine() => "";
            public void WriteLine(string value) { }

            public void Dispose()
            {
                DisposeCount++;
                Close();
            }
        }
    }
}
