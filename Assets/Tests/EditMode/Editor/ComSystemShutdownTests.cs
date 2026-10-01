using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using DanroJump.Hardware.Com;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    /// <summary>
    /// Тесты для проверки быстрого и безопасного закрытия (graceful shutdown) COM-системы.
    /// </summary>
    public sealed class ComSystemShutdownTests
    {
        private GameObject systemGo;
        private ComSystem comSystem;
        private MockComTransport mockTransport;

        [SetUp]
        public void SetUp()
        {
            systemGo = new GameObject("Test_ComSystem");
            comSystem = systemGo.AddComponent<ComSystem>();

            // Отключаем автоматический запуск и сохранение между сценами
            SetPrivateField(comSystem, "startOnEnable", false);
            SetPrivateField(comSystem, "dontDestroyOnLoad", false);
            SetPrivateField(comSystem, "useEncryptedHandshake", false);
            SetPrivateField(comSystem, "keepAlive", false);
        }

        [TearDown]
        public void TearDown()
        {
            if (comSystem != null)
            {
                comSystem.StopSystem(false);
            }

            if (systemGo != null)
            {
                UnityEngine.Object.DestroyImmediate(systemGo);
            }

            mockTransport?.Dispose();
        }

        [Test]
        public void ComSystem_StopSystem_CompletesImmediately()
        {
            // Настраиваем mock-окружение
            var mockProvider = new MockComPortProvider();
            mockTransport = new MockComTransport();
            var mockFactory = new MockComTransportFactory(mockTransport);

            comSystem.ConfigureInfrastructure(mockProvider, mockFactory);

            // Запускаем систему
            comSystem.StartSystem();
            Assert.That(comSystem.IsRunning, Is.True);

            // Проверяем время остановки
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            comSystem.StopSystem(sendShutdownCommands: false);
            stopwatch.Stop();

            Assert.That(comSystem.IsRunning, Is.False);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500), "StopSystem took too long to complete!");
        }

        [Test]
        public void ComSystem_StopSystem_WithShutdownCommands_CompletesImmediately()
        {
            var mockProvider = new MockComPortProvider();
            mockTransport = new MockComTransport();
            var mockFactory = new MockComTransportFactory(mockTransport);

            comSystem.ConfigureInfrastructure(mockProvider, mockFactory);

            comSystem.StartSystem();
            Assert.That(comSystem.IsRunning, Is.True);

            // Проверяем время остановки с отправкой команд выключения
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            comSystem.StopSystem(sendShutdownCommands: true);
            stopwatch.Stop();

            Assert.That(comSystem.IsRunning, Is.False);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500), "StopSystem with shutdown commands took too long to complete!");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new ArgumentException($"Field '{fieldName}' not found in type '{target.GetType().FullName}'");
            }
            field.SetValue(target, value);
        }

        private sealed class MockComPortProvider : IComPortProvider
        {
            public string[] GetPortNames() => new[] { "COM_TEST" };
        }

        private sealed class MockComTransportFactory : IComTransportFactory
        {
            private readonly IComTransport transport;

            public MockComTransportFactory(IComTransport transport)
            {
                this.transport = transport;
            }

            public IComTransport Create(SerialSettings settings) => transport;
        }

        private sealed class MockComTransport : IComTransport
        {
            private readonly AutoResetEvent readEvent = new(false);
            private readonly ConcurrentQueue<string> readQueue = new();
            private bool isOpen;

            public bool IsOpen => isOpen;

            public void Open()
            {
                isOpen = true;
            }

            public void Close()
            {
                isOpen = false;
                readEvent.Set();
            }

            public void DiscardInBuffer() { }
            public void DiscardOutBuffer() { }

            public string ReadLine()
            {
                while (isOpen)
                {
                    if (readQueue.TryDequeue(out var line))
                    {
                        return line;
                    }
                    readEvent.WaitOne(10);
                }
                throw new System.IO.IOException("Port is closed");
            }

            public void WriteLine(string value)
            {
                // Для простоты теста просто игнорируем команды записи
            }

            public void Dispose()
            {
                Close();
            }
        }
    }
}
