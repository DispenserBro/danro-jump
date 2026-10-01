using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Инжектит debug-зависимости в явно помеченные сценовые компоненты.
    /// </summary>
    public sealed class RuntimeDebugSceneInjector : IInitializable, IDisposable
    {
        private readonly DiContainer container;
        private readonly HashSet<EntityId> injectedEntityIds = new();

        public RuntimeDebugSceneInjector(DiContainer container)
        {
            this.container = container;
        }

        public void Initialize()
        {
            InjectLoadedScenes();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            injectedEntityIds.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InjectLoadedScenes();
        }

        private void InjectLoadedScenes()
        {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

            for (int index = 0; index < behaviours.Length; index++)
            {
                var behaviour = behaviours[index];
                if (behaviour is not IDebugLogConsumer)
                {
                    continue;
                }

                if (!injectedEntityIds.Add(behaviour.GetEntityId()))
                {
                    continue;
                }

                container.Inject(behaviour);
            }
        }
    }
}
