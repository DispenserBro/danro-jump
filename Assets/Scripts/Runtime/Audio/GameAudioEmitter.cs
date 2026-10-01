using System;
using UnityEngine;
using Zenject;

namespace DanroJump.Audio
{
    /// <summary>
    /// Универсальный мост от prefab/Animation Event к централизованной аудиосистеме.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Audio/Game Audio Emitter")]
    public sealed class GameAudioEmitter : MonoBehaviour
    {
        [SerializeField] private bool playAtTransform = true;

        private IGameAudioService audioService;

        [Inject]
        public void Construct([InjectOptional] IGameAudioService injectedAudioService = null)
        {
            audioService = injectedAudioService;
        }

        public void Play(GameAudioEvent eventId)
        {
            if (playAtTransform)
            {
                audioService?.Play(eventId, transform.position);
                return;
            }

            audioService?.Play(eventId);
        }

        public void PlayGlobal(GameAudioEvent eventId)
        {
            audioService?.Play(eventId);
        }

        public void PlayByName(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName) ||
                !Enum.TryParse(eventName, true, out GameAudioEvent eventId))
            {
                return;
            }

            Play(eventId);
        }

        public void PlayGlobalByName(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName) ||
                !Enum.TryParse(eventName, true, out GameAudioEvent eventId))
            {
                return;
            }

            PlayGlobal(eventId);
        }
    }
}
