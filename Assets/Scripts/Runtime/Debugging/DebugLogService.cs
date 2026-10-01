using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Глобальный singleton отладочного вывода. Создается до загрузки сцен и не уничтожается при переходах.
    /// </summary>
    [DefaultExecutionOrder(-12500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Debugging/Debug Log Service")]
    public sealed class DebugLogService : MonoBehaviour, IDebugLogService
    {
        private static DebugLogService instance;

        [SerializeField] private bool isEnabled = true;
        [SerializeField] private DebugLogChannel enabledChannels = DebugLogChannel.All;
        [SerializeField] private DebugLogOutput enabledOutputs = DebugLogOutput.UnityConsole | DebugLogOutput.CustomHandlers;
        [SerializeField] [Min(16)] private int maxQueuedEntries = 2048;

        private readonly ConcurrentQueue<DebugLogEntry> pendingEntries = new();
        private readonly List<IDebugLogHandler> handlers = new();
        private readonly object handlersLock = new();
        private int pendingCount;

        public static DebugLogService Instance => instance;

        public bool IsEnabled
        {
            get => isEnabled;
            set => isEnabled = value;
        }

        public DebugLogChannel EnabledChannels
        {
            get => enabledChannels;
            set => enabledChannels = value;
        }

        public DebugLogOutput EnabledOutputs
        {
            get => enabledOutputs;
            set => enabledOutputs = value;
        }

        public static DebugLogService GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            var existing = FindAnyObjectByType<DebugLogService>();
            if (existing != null)
            {
                instance = existing;
                MoveToPersistentScene(existing.gameObject);
                return existing;
            }

            var gameObject = new GameObject("Debug Log Service");
            MoveToPersistentScene(gameObject);
            return gameObject.AddComponent<DebugLogService>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            MoveToPersistentScene(gameObject);
        }

        private void OnDestroy()
        {
            FlushPendingEntries();

            if (instance == this)
            {
                instance = null;
            }

            lock (handlersLock)
            {
                handlers.Clear();
            }

            while (pendingEntries.TryDequeue(out _)) { }
            pendingCount = 0;
        }

        private static void MoveToPersistentScene(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            FlushPendingEntries();
        }

        public bool IsChannelEnabled(DebugLogChannel channel)
        {
            return isEnabled &&
                channel != DebugLogChannel.None &&
                (enabledChannels & channel) != 0 &&
                enabledOutputs != DebugLogOutput.None;
        }

        public void Log(DebugLogChannel channel, string message, Object context = null)
        {
            Write(new DebugLogEntry(channel, DebugLogLevel.Log, message, context));
        }

        public void LogWarning(DebugLogChannel channel, string message, Object context = null)
        {
            Write(new DebugLogEntry(channel, DebugLogLevel.Warning, message, context));
        }

        public void LogError(DebugLogChannel channel, string message, Object context = null)
        {
            Write(new DebugLogEntry(channel, DebugLogLevel.Error, message, context));
        }

        public void AddHandler(IDebugLogHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (handlersLock)
            {
                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }

        public void RemoveHandler(IDebugLogHandler handler)
        {
            lock (handlersLock)
            {
                handlers.Remove(handler);
            }
        }

        private void Write(DebugLogEntry entry)
        {
            if (!IsChannelEnabled(entry.Channel))
            {
                return;
            }

            pendingEntries.Enqueue(entry);
            int queued = Interlocked.Increment(ref pendingCount);
            while (queued > maxQueuedEntries && pendingEntries.TryDequeue(out _))
            {
                queued = Interlocked.Decrement(ref pendingCount);
            }
        }

        private void FlushPendingEntries()
        {
            while (pendingEntries.TryDequeue(out DebugLogEntry entry))
            {
                Interlocked.Decrement(ref pendingCount);
                Dispatch(entry);
            }
        }

        private void Dispatch(DebugLogEntry entry)
        {
            if ((enabledOutputs & DebugLogOutput.UnityConsole) != 0)
            {
                WriteToUnityConsole(entry);
            }

            if ((enabledOutputs & DebugLogOutput.CustomHandlers) != 0)
            {
                WriteToHandlers(entry);
            }
        }

        private static void WriteToUnityConsole(DebugLogEntry entry)
        {
            string message = $"[{entry.Channel}] {entry.Message}";
            switch (entry.Level)
            {
                case DebugLogLevel.Warning:
                    Debug.LogWarning(message, entry.Context);
                    break;
                case DebugLogLevel.Error:
                    Debug.LogError(message, entry.Context);
                    break;
                default:
                    Debug.Log(message, entry.Context);
                    break;
            }
        }

        private void WriteToHandlers(DebugLogEntry entry)
        {
            IDebugLogHandler[] snapshot;
            lock (handlersLock)
            {
                handlers.RemoveAll(handler => handler == null);
                snapshot = handlers.ToArray();
            }

            for (int index = 0; index < snapshot.Length; index++)
            {
                var handler = snapshot[index];
                if (handler == null)
                {
                    continue;
                }

                try
                {
                    handler.Handle(entry);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
