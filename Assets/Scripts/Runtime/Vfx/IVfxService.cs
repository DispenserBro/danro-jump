using UnityEngine;

namespace DanroJump.Vfx
{
    /// <summary>
    /// Интерфейс централизованной службы визуальных эффектов (VFX).
    /// </summary>
    public interface IVfxService
    {
        /// <summary>
        /// Создает визуальный эффект обычного автопрыжка игрока.
        /// </summary>
        /// <param name="position">Мировая позиция эффекта.</param>
        void SpawnJumpEffect(Vector3 position);

        /// <summary>
        /// Создает визуальный эффект сбора монеты.
        /// </summary>
        /// <param name="position">Мировая позиция эффекта.</param>
        void SpawnCoinCollectedEffect(Vector3 position);

        /// <summary>
        /// Создает визуальный эффект смерти игрока.
        /// </summary>
        /// <param name="position">Мировая позиция эффекта.</param>
        void SpawnPlayerDeathEffect(Vector3 position);

        /// <summary>
        /// Создает праздничный визуальный эффект при выигрыше приза.
        /// </summary>
        /// <param name="position">Мировая позиция эффекта.</param>
        void SpawnPrizeEffect(Vector3 position);

        /// <summary>
        /// Создает визуальный эффект ошибки связи с COM-устройством.
        /// </summary>
        /// <param name="position">Мировая позиция эффекта.</param>
        void SpawnComErrorEffect(Vector3 position);
    }
}
