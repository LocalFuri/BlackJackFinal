using System;
using UnityEngine;

namespace Blackjack
{
    /// <summary>
    /// Pairs an AudioClip with an individual volume slider.
    /// Use this for every sound effect so new sounds automatically
    /// get Inspector-exposed volume control.
    /// </summary>
    [Serializable]
    public struct SoundEntry
    {
        [Tooltip("The audio clip to play.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        [Tooltip("Playback volume for this clip (0 = silent, 1 = full).")]
        public float volume;

        /// <summary>Plays the clip on the given AudioSource at the configured volume.</summary>
        public void Play(AudioSource source)
        {
            if (clip != null && source != null)
                source.PlayOneShot(clip, volume);
        }

        /// <summary>Returns the clip length in seconds, or 0 if the clip is null.</summary>
        public float Length => clip != null ? clip.length : 0f;

        /// <summary>Returns true when a clip has been assigned.</summary>
        public bool HasClip => clip != null;
    }
}
