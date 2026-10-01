using System;
using System.Collections.Generic;
using DanroJump.SceneFlow;
using DanroJump.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;

namespace DanroJump.Tests.EditMode
{
    public sealed class AppUIFacadeInjectorTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Initialize_InjectsActionsAlreadyPresentInLoadedScene()
        {
            var gameObject = CreateGameObject("Service Settings Actions");
            var actions = gameObject.AddComponent<AppUIFacade>();
            var sceneFlow = new RecordingSceneFlowService();
            var container = new DiContainer();
            container.Bind<IServiceSettingsService>().FromInstance(new NoopServiceSettingsService()).AsSingle();
            container.Bind<ISceneFlowService>().FromInstance(sceneFlow).AsSingle();
            var injector = new AppUIFacadeInjector(container, true);

            injector.Initialize();
            actions.LoadScene("Game");
            actions.LoadMainMenu();
            injector.Dispose();

            Assert.That(sceneFlow.LoadedScene, Is.EqualTo("Game"));
            Assert.That(sceneFlow.OpenMainMenuCount, Is.EqualTo(1));
        }

        [Test]
        public void Initialize_DoesNothingWhenSceneActionInjectionIsDisabled()
        {
            var gameObject = CreateGameObject("Disabled Service Settings Actions");
            var actions = gameObject.AddComponent<AppUIFacade>();
            var sceneFlow = new RecordingSceneFlowService();
            var container = new DiContainer();
            container.Bind<IServiceSettingsService>().FromInstance(new NoopServiceSettingsService()).AsSingle();
            container.Bind<ISceneFlowService>().FromInstance(sceneFlow).AsSingle();
            var injector = new AppUIFacadeInjector(container, false);

            injector.Initialize();
            LogAssert.Expect(LogType.Warning, "AppUIFacade requires ISceneFlowService from DI.");
            actions.LoadMainMenu();
            injector.Dispose();

            Assert.That(sceneFlow.OpenMainMenuCount, Is.EqualTo(0));
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private sealed class RecordingSceneFlowService : ISceneFlowService
        {
            public string LoadedScene { get; private set; }
            public int OpenMainMenuCount { get; private set; }

            public void LoadScene(string sceneName)
            {
                LoadedScene = sceneName;
            }

            public void OpenMainMenu()
            {
                OpenMainMenuCount++;
            }

            public void StartGameplay(string sceneName)
            {
                LoadedScene = sceneName;
            }

            public void OpenSettingsScene(string sceneName)
            {
                LoadedScene = sceneName;
            }

            public void ReturnToMainMenuAfterGameOver(TimeSpan delay)
            {
                OpenMainMenu();
            }

            public void CancelGameOverReturn()
            {
            }

            public bool SceneExists(string sceneName)
            {
                return !string.IsNullOrWhiteSpace(sceneName);
            }
        }

        private sealed class NoopServiceSettingsService : IServiceSettingsService
        {
            public ServiceSettingsDatabase Database => null;
            public event Action SettingsLoaded;
            public event Action SettingsSaved;
            public event Action<string, ServiceSettingValue> SettingChanged;
            public void Load() => SettingsLoaded?.Invoke();
            public void Save() => SettingsSaved?.Invoke();
            public void ReloadExternalChanges() { }
            public void ApplyAll() { }
            public void ResetAllToDefaults() { }
            public void ResetGroupToDefaults(string groupName) { }
            public bool Set(string key, ServiceSettingValue value) { SettingChanged?.Invoke(key, value); return true; }
            public bool SetExternal(string key, ServiceSettingValue value) => Set(key, value);
            public bool SetBool(string key, bool value) => Set(key, ServiceSettingValue.Bool(value));
            public bool SetInt(string key, int value) => Set(key, ServiceSettingValue.Int(value));
            public bool SetFloat(string key, float value) => Set(key, ServiceSettingValue.Float(value));
            public bool SetString(string key, string value) => Set(key, ServiceSettingValue.String(value));
            public bool SetOptionIndex(string key, int optionIndex) => Set(key, ServiceSettingValue.Option(optionIndex));
            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition) { definition = null; return false; }
            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot() => new Dictionary<string, ServiceSettingValue>();
            public ServiceSettingValue Get(string key) => default;
            public bool GetBool(string key) => false;
            public int GetInt(string key) => 0;
            public float GetFloat(string key) => 0f;
            public string GetString(string key) => string.Empty;
            public int GetOptionIndex(string key) => 0;
        }
    }
}
