using UnityEngine;
using UnityEngine.VFX;

namespace DanroJump.Runtime.Gameplay.Equipment
{
    [DisallowMultipleComponent]
    public sealed class JetpackFlameVfxSettings : MonoBehaviour
    {
        private VisualEffect visualEffect;
        private VFXRenderer vfxRenderer;

        private void Awake()
        {
            CacheComponents();
        }

        public void Apply()
        {
            CacheComponents();
        }

        public void Play()
        {
            CacheComponents();
            if (visualEffect == null)
            {
                return;
            }

            if (vfxRenderer != null)
            {
                vfxRenderer.enabled = true;
            }

            visualEffect.enabled = true;
            visualEffect.Reinit();
            visualEffect.Play();
        }

        public void Play(string sortingLayerName, int sortingOrder)
        {
            Play();
            SetSorting(sortingLayerName, sortingOrder);
        }

        public void SetSorting(string sortingLayerName, int sortingOrder)
        {
            CacheComponents();
            if (vfxRenderer == null)
            {
                return;
            }

            vfxRenderer.sortingLayerName = sortingLayerName;
            vfxRenderer.sortingOrder = sortingOrder;
        }

        public void Stop()
        {
            if (visualEffect != null)
            {
                visualEffect.Stop();
                visualEffect.enabled = false;
            }

            if (vfxRenderer != null)
            {
                vfxRenderer.enabled = false;
            }
        }

        private void CacheComponents()
        {
            visualEffect ??= GetComponent<VisualEffect>();
            vfxRenderer ??= GetComponent<VFXRenderer>();
        }
    }
}
