using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Маркер обычной платформы, которую игрок может использовать для автопрыжка.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class JumpPlatform : MonoBehaviour
    {
    }
}
