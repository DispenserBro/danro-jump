using UnityEngine;

namespace DanroJump.Vfx
{
    /// <summary>
    /// Служба визуальных эффектов, загружающая и инстанцирующая префабы систем частиц из Resources/Vfx/.
    /// </summary>
    public sealed class GameVfxService : IVfxService
    {
        public void SpawnJumpEffect(Vector3 position)
        {
            SpawnVfx("VFX_Jump", position);
        }

        public void SpawnCoinCollectedEffect(Vector3 position)
        {
            SpawnVfx("VFX_CoinCollected", position);
        }

        public void SpawnPlayerDeathEffect(Vector3 position)
        {
            SpawnVfx("VFX_PlayerDeath", position);
        }

        public void SpawnPrizeEffect(Vector3 position)
        {
            SpawnVfx("VFX_PrizeWin", position);
        }

        public void SpawnComErrorEffect(Vector3 position)
        {
            SpawnVfx("VFX_ComError", position);
        }

        private void SpawnVfx(string prefabName, Vector3 position)
        {
            var prefab = Resources.Load<ParticleSystem>($"Vfx/{prefabName}");
            if (prefab == null)
            {
                Debug.LogWarning($"[GameVfxService] Prefab Resources/Vfx/{prefabName} not found. Attempting to generate default assets.");
                return;
            }

            var ps = Object.Instantiate(prefab, position, Quaternion.identity);
            ps.Play();

            if (Application.isPlaying)
            {
                var main = ps.main;
                Object.Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax);
            }
        }
    }
}
