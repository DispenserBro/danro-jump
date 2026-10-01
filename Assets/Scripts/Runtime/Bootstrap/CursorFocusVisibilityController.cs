using UnityEngine;

namespace DanroJump.Bootstrap
{
    /// <summary>
    /// Прячет системный курсор, пока окно игры находится в фокусе.
    /// </summary>
    [DefaultExecutionOrder(-11000)]
    public sealed class CursorFocusVisibilityController : MonoBehaviour
    {
        private static CursorFocusVisibilityController instance;

        [SerializeField] private bool hideCursorWhenFocused = true;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            MoveToPersistentScene(gameObject);
            ApplyCursorFocusState(Application.isFocused);
        }

        private void Update()
        {
            ApplyCursorFocusState(Application.isFocused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            ApplyCursorFocusState(hasFocus);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            ApplyCursorFocusState(!pauseStatus && Application.isFocused);
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            Cursor.visible = true;
            instance = null;
        }

        private static void MoveToPersistentScene(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void ApplyCursorFocusState(bool hasFocus)
        {
            Cursor.visible = !hideCursorWhenFocused || !hasFocus;
        }
    }
}
