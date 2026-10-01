using UnityEngine;
using Zenject;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Камера, которая следует за игроком только вверх и не опускается при падении.
    /// </summary>
    public sealed class VerticalCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float verticalOffset = 1.25f;
        [SerializeField] private float followSmoothing = 7f;

        private float highestCameraY;

        /// <summary>
        /// Получает игрока через SceneContext.
        /// </summary>
        [Inject]
        public void Construct(IPlayerController playerController)
        {
            target = playerController.Transform;
        }

        /// <summary>
        /// Запоминает стартовую высоту камеры.
        /// </summary>
        private void Awake()
        {
            highestCameraY = transform.position.y;
        }

        /// <summary>
        /// Плавно поднимает камеру за игроком, не позволяя ей двигаться вниз.
        /// </summary>
        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desiredY = target.position.y + verticalOffset;
            if (desiredY <= highestCameraY)
            {
                return;
            }

            highestCameraY = Mathf.Lerp(highestCameraY, desiredY, Time.deltaTime * followSmoothing);
            transform.position = new Vector3(transform.position.x, highestCameraY, transform.position.z);
        }
    }
}
