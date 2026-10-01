using DanroJump.Gameplay;
using UnityEditor;
using UnityEngine;

namespace DanroJump.Editor.Gameplay
{
    /// <summary>
    /// Editor-окно для быстрой проверки текущего этапа сложности и прогресса генерации.
    /// </summary>
    public sealed class GameplayProgressionOverlayWindow : EditorWindow
    {
        /// <summary>
        /// Открывает overlay-окно прогрессии из меню Unity.
        /// </summary>
        [MenuItem("Window/Danro Jump/Gameplay Progression Overlay")]
        public static void Open()
        {
            var window = GetWindow<GameplayProgressionOverlayWindow>("Progression");
            window.minSize = new Vector2(320f, 180f);
            window.Show();
        }

        /// <summary>
        /// Подписывается на editor update, чтобы overlay обновлялся без ручного Repaint.
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        /// <summary>
        /// Снимает подписку на editor update при закрытии окна.
        /// </summary>
        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        /// <summary>
        /// Рисует актуальные данные сцены без ручной привязки к конкретному объекту.
        /// </summary>
        private void OnGUI()
        {
            var progression = FindAnyObjectByType<GameplayDifficultyProgression>();
            var spawner = FindAnyObjectByType<PlatformSpawner>();
            GameplayProgressionOverlayGui.Draw(progression, spawner, true);
        }
    }

    /// <summary>
    /// Добавляет компактный preview прогрессии прямо в инспектор GameplayDifficultyProgression.
    /// </summary>
    [CustomEditor(typeof(GameplayDifficultyProgression))]
    public sealed class GameplayDifficultyProgressionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10f);
            var progression = (GameplayDifficultyProgression)target;
            var spawner = FindAnyObjectByType<PlatformSpawner>();
            GameplayProgressionOverlayGui.Draw(progression, spawner, false);
        }
    }

    /// <summary>
    /// Общий IMGUI-рендерер для отдельного окна и встроенного блока в инспекторе.
    /// </summary>
    internal static class GameplayProgressionOverlayGui
    {
        /// <summary>
        /// Рисует состояние этапа игрока и этапа генерации платформ.
        /// </summary>
        public static void Draw(GameplayDifficultyProgression progression, PlatformSpawner spawner, bool showHeader)
        {
            if (showHeader)
            {
                EditorGUILayout.LabelField("Gameplay Progression", EditorStyles.boldLabel);
                EditorGUILayout.Space(4f);
            }

            if (progression == null)
            {
                EditorGUILayout.HelpBox("GameplayDifficultyProgression не найден в открытой сцене.", MessageType.Warning);
                return;
            }

            DrawStageBlock(
                "Этап игрока",
                progression.CurrentStageName,
                progression.CurrentProgress,
                GetPlayModeHint());

            EditorGUILayout.Space(8f);

            if (spawner == null)
            {
                EditorGUILayout.HelpBox("PlatformSpawner не найден в открытой сцене.", MessageType.Info);
                return;
            }

            DrawStageBlock(
                "Этап генерации платформ",
                spawner.CurrentGenerationStage,
                spawner.CurrentGenerationStageProgress,
                $"Generation Y: {spawner.CurrentGenerationY:0.##} | Distribution: {spawner.CurrentPlatformDistribution:0.##}");
        }

        /// <summary>
        /// Рисует одну карточку этапа с числовым прогрессом и progress bar.
        /// </summary>
        private static void DrawStageBlock(string title, string stageName, float progress, string subtitle)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Текущий этап", string.IsNullOrWhiteSpace(stageName) ? "<нет этапа>" : stageName);
            EditorGUILayout.LabelField("Прогресс этапа", $"{Mathf.Clamp01(progress) * 100f:0}%");
            var rect = GUILayoutUtility.GetRect(18f, 18f, "TextField");
            EditorGUI.ProgressBar(rect, Mathf.Clamp01(progress), $"{Mathf.Clamp01(progress):0.00}");

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Возвращает подсказку о том, из какого режима редактора сейчас читаются данные.
        /// </summary>
        private static string GetPlayModeHint()
        {
            return EditorApplication.isPlaying ? "Play Mode: данные обновляются runtime-логикой" : "Edit Mode: данные рассчитаны по текущим ссылкам сцены";
        }
    }
}
