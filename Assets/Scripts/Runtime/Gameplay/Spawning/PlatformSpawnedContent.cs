using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Маркер объекта, который был создан спавнером контента платформы.
    /// </summary>
    public sealed class PlatformSpawnedContent : MonoBehaviour
    {
        /// <summary>
        /// True, если объект забран из владения платформы и не должен удаляться при ее очистке.
        /// </summary>
        public bool IsDetachedFromPlatform { get; private set; }

        /// <summary>
        /// Ссылка на оригинальный префаб, из которого был инстанцирован данный объект.
        /// Используется для возврата объекта в нужную ветку пула.
        /// </summary>
        public GameObject SourcePrefab { get; set; }

        /// <summary>
        /// Помечает объект как больше не принадлежащий платформе.
        /// </summary>
        public void DetachFromPlatform()
        {
            IsDetachedFromPlatform = true;
        }

        /// <summary>
        /// Возвращает marker в исходное состояние при новом спавне контента платформы.
        /// </summary>
        public void ResetForPlatformSpawn()
        {
            IsDetachedFromPlatform = false;
        }
    }
}
