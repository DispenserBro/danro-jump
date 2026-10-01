using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Маркер точки спавна контента на платформе.
    /// </summary>
    public sealed class PlatformSpawnPoint : MonoBehaviour
    {
        [SerializeField] private PlatformContentKind kind;
        [SerializeField] private bool optional;
        [SerializeField] [Min(0f)] private float weight = 1f;

        /// <summary>
        /// Категория контента, который может появиться в этой точке.
        /// </summary>
        public PlatformContentKind Kind => kind;

        /// <summary>
        /// Можно ли пропустить эту точку, если контент этапа не выбран.
        /// </summary>
        public bool Optional => optional;

        /// <summary>
        /// Вес точки при выборе между несколькими подходящими позициями.
        /// </summary>
        public float Weight => weight;

        /// <summary>
        /// Transform, в позиции которого создается контент.
        /// </summary>
        public Transform Anchor => transform;

        /// <summary>
        /// Рисует цветную gizmo-точку в редакторе для удобной настройки prefab платформ.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = KindColor(kind);
            Gizmos.DrawWireSphere(transform.position, 0.12f);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.25f);
        }

        /// <summary>
        /// Возвращает цвет gizmo по типу контента.
        /// </summary>
        private static Color KindColor(PlatformContentKind contentKind)
        {
            switch (contentKind)
            {
                case PlatformContentKind.Collectible:
                    return Color.yellow;
                case PlatformContentKind.Equipment:
                    return Color.cyan;
                case PlatformContentKind.Enemy:
                    return Color.red;
                default:
                    return Color.white;
            }
        }
    }
}
