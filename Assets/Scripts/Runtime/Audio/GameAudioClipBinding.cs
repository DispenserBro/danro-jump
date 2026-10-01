using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DanroJump.Audio
{
    /// <summary>
    /// Описывает набор клипов и параметры воспроизведения для одного игрового события.
    /// </summary>
    [Serializable]
    public sealed class GameAudioClipBinding
    {
        [SerializeField] private GameAudioEvent eventId;
        [SerializeField] private GameAudioChannel channel = GameAudioChannel.Sfx;
        [SerializeField] private AudioMixerGroup outputGroupOverride;
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private Vector2 pitchRange = Vector2.one;
        [SerializeField] private bool spatial;

        public GameAudioEvent EventId => eventId;

        public GameAudioChannel Channel => channel;

        public AudioMixerGroup OutputGroupOverride => outputGroupOverride;

        public IReadOnlyList<AudioClip> Clips => clips;

        public float Volume => Mathf.Clamp01(volume);

        public bool Spatial => spatial;

        public bool HasClip => clips != null && clips.Length > 0 && clips[0] != null;

        public float RandomPitch => Mathf.Clamp(
            UnityEngine.Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y)),
            0.05f,
            3f);

        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            var firstValidClip = default(AudioClip);
            for (var index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null)
                {
                    continue;
                }

                firstValidClip ??= clips[index];
            }

            if (firstValidClip == null)
            {
                return null;
            }

            for (var attempt = 0; attempt < clips.Length; attempt++)
            {
                var clip = clips[UnityEngine.Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    return clip;
                }
            }

            return firstValidClip;
        }
    }
}
