using UnityEngine;

namespace DanroJump.UI
{
    /// <summary>
    /// Shows a persistent non-interactive marker in development builds.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class DevelopmentBuildOverlay : MonoBehaviour
    {
        private const string LabelText = "DEV BUILD";
        private static DevelopmentBuildOverlay instance;
        private static readonly GUIContent LabelContent = new(LabelText);

        [SerializeField] private Vector2 margin = new(12f, 10f);
        [SerializeField] private int fontSize = 18;
        [SerializeField] private Color backgroundColor = new(0f, 0f, 0f, 0.58f);
        [SerializeField] private Color accentColor = new(1f, 0.18f, 0.12f, 0.95f);
        [SerializeField] private Color textColor = new(1f, 1f, 1f, 0.96f);
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.65f);

        private GUIStyle labelStyle;
        private GUIStyle shadowStyle;

        private void Awake()
        {
            if (!ShouldShowOverlay)
            {
                Destroy(gameObject);
                return;
            }

            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnGUI()
        {
            if (!ShouldShowOverlay || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyles();

            int previousDepth = GUI.depth;
            GUI.depth = int.MinValue;

            Vector2 size = labelStyle.CalcSize(LabelContent);
            Rect rect = new(margin.x, Screen.height - margin.y - size.y, size.x, size.y);
            Rect backgroundRect = new(rect.x - 8f, rect.y - 4f, rect.width + 16f, rect.height + 8f);
            Rect accentRect = new(backgroundRect.x, backgroundRect.y, 4f, backgroundRect.height);
            Rect shadowRect = new(rect.x + 1f, rect.y + 1f, rect.width, rect.height);

            Color previousColor = GUI.color;
            GUI.color = backgroundColor;
            GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture);
            GUI.color = accentColor;
            GUI.DrawTexture(accentRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            shadowStyle.Draw(shadowRect, LabelContent, false, false, false, false);
            labelStyle.Draw(rect, LabelContent, false, false, false, false);

            GUI.depth = previousDepth;
        }

        private static bool ShouldShowOverlay => Debug.isDebugBuild || Application.isEditor;

        private void EnsureStyles()
        {
            if (labelStyle != null && labelStyle.fontSize == fontSize)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerLeft,
                clipping = TextClipping.Overflow,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = textColor }
            };

            shadowStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = shadowColor }
            };
        }
    }
}
