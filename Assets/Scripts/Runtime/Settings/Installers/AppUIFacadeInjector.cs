using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace DanroJump.Settings
{
    /// <summary>
    /// Инжектит сценовые AppUIFacade из ProjectContext без прямого глобального поиска объектов.
    /// </summary>
    public sealed class AppUIFacadeInjector : IInitializable, IDisposable
    {
        private readonly DiContainer container;
        private readonly bool injectActionsInScene;
        private readonly List<GameObject> rootObjects = new();
        private readonly List<AppUIFacade> actions = new();
        private readonly HashSet<EntityId> injectedEntityIds = new();

        public AppUIFacadeInjector(DiContainer container, bool injectActionsInScene)
        {
            this.container = container;
            this.injectActionsInScene = injectActionsInScene;
        }

        public void Initialize()
        {
            if (!injectActionsInScene)
            {
                return;
            }

            InjectLoadedScenes();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            rootObjects.Clear();
            actions.Clear();
            injectedEntityIds.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InjectScene(scene);
        }

        private void InjectLoadedScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                InjectScene(SceneManager.GetSceneAt(index));
            }
        }

        private void InjectScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            rootObjects.Clear();
            scene.GetRootGameObjects(rootObjects);

            for (var rootIndex = 0; rootIndex < rootObjects.Count; rootIndex++)
            {
                actions.Clear();
                rootObjects[rootIndex].GetComponentsInChildren(true, actions);

                for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    var sceneActions = actions[actionIndex];
                    if (sceneActions == null)
                    {
                        continue;
                    }

                    if (!injectedEntityIds.Add(sceneActions.GetEntityId()))
                    {
                        continue;
                    }

                    container.Inject(sceneActions);
                }
            }

            actions.Clear();
            rootObjects.Clear();
        }
    }
}
