using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Описывает посадку игрока на платформу независимо от источника события: collision или probe.
    /// </summary>
    public readonly struct PlatformLandingContext
    {
        public PlatformLandingContext(
            Collider2D platformCollider,
            Collision2D collision,
            Vector2 point,
            Vector2 normal,
            bool isSweptProbe)
        {
            PlatformCollider = platformCollider;
            Collision = collision;
            Point = point;
            Normal = normal;
            IsSweptProbe = isSweptProbe;
        }

        public Collider2D PlatformCollider { get; }

        public Collision2D Collision { get; }

        public Vector2 Point { get; }

        public Vector2 Normal { get; }

        public bool IsSweptProbe { get; }
    }
}
