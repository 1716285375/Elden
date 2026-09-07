using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Reduces loudness differences within short sound-effect pools without amplifying their peaks.</summary>
    public static class SoundEffectVolume
    {
        private const float k_WindowDurationSeconds = 0.05f;
        private const float k_MinimumAudibleLevel = 0.000001f;
        private const float k_MinimumVolumeScale = 0.3548134f;

        private static readonly Dictionary<AudioClip, ClipLevel> s_clipLevels = new();
        private static readonly HashSet<AudioClip> s_requestedLoads = new();

        /// <summary>
        /// Requests sample data once during character or equipment setup, then caches available measurements.
        /// Streaming and compressed-in-memory clips retain their authored volume.
        /// </summary>
        public static void WarmUp(AudioClip[] soundEffects)
        {
            if (soundEffects == null)
            {
                return;
            }

            foreach (AudioClip clip in soundEffects)
            {
                if (clip == null || clip.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    continue;
                }

                if (clip.loadState == AudioDataLoadState.Unloaded && s_requestedLoads.Add(clip))
                {
                    clip.LoadAudioData();
                }

                GetClipLevel(clip);
            }
        }

        /// <summary>
        /// Matches a variant to the quietest audible variant in its pool, reducing it by at most 9 dB.
        /// Missing, silent, or unavailable sample data keeps its authored volume. Pass the result to PlayOneShot.
        /// </summary>
        public static float GetVolumeScale(AudioClip[] soundEffects, AudioClip selectedClip)
        {
            if (soundEffects == null || soundEffects.Length < 2 || selectedClip == null)
            {
                return 1f;
            }

            ClipLevel selectedLevel = GetClipLevel(selectedClip);
            if (!selectedLevel.IsAudible)
            {
                return 1f;
            }

            float targetLevel = selectedLevel.WindowRms;
            bool containsSelectedClip = false;
            foreach (AudioClip clip in soundEffects)
            {
                containsSelectedClip |= clip == selectedClip;
                ClipLevel level = GetClipLevel(clip);
                if (level.IsAudible)
                {
                    targetLevel = Mathf.Min(targetLevel, level.WindowRms);
                }
            }

            return containsSelectedClip
                ? Mathf.Clamp(targetLevel / selectedLevel.WindowRms, k_MinimumVolumeScale, 1f)
                : 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            s_clipLevels.Clear();
            s_requestedLoads.Clear();
        }

        private static ClipLevel GetClipLevel(AudioClip clip)
        {
            if (clip == null || clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                return default;
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                // Do not cache unloaded samples as silence; a later playback or warm-up can finish loading them.
                s_clipLevels.Remove(clip);
                return default;
            }

            if (!s_clipLevels.TryGetValue(clip, out ClipLevel level))
            {
                level = MeasureClip(clip);
                // Failed reads are cached too, avoiding repeated allocations for an unreadable loaded clip.
                s_clipLevels.Add(clip, level);
            }

            return level;
        }

        private static ClipLevel MeasureClip(AudioClip clip)
        {
            int sampleCount = clip.samples * clip.channels;
            if (sampleCount <= 0 || clip.frequency <= 0)
            {
                return default;
            }

            var samples = new float[sampleCount];
            if (!clip.GetData(samples, 0))
            {
                return default;
            }

            int windowSampleCount = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * k_WindowDurationSeconds)) *
                clip.channels;
            double windowEnergy = 0d;
            double maximumWindowEnergy = 0d;
            float peak = 0f;
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                float sample = samples[sampleIndex];
                if (float.IsNaN(sample) || float.IsInfinity(sample))
                {
                    return default;
                }

                peak = Mathf.Max(peak, Mathf.Abs(sample));
                windowEnergy += (double)sample * sample;
                if (sampleIndex >= windowSampleCount)
                {
                    float previousSample = samples[sampleIndex - windowSampleCount];
                    windowEnergy -= (double)previousSample * previousSample;
                }

                maximumWindowEnergy = System.Math.Max(maximumWindowEnergy, windowEnergy);
            }

            // A fixed window also treats very short clips as zero-padded, so authored silence cannot inflate gain.
            float windowRms = (float)System.Math.Sqrt(maximumWindowEnergy / windowSampleCount);
            return new ClipLevel(windowRms, peak);
        }

        private readonly struct ClipLevel
        {
            public ClipLevel(float windowRms, float peak)
            {
                WindowRms = windowRms;
                Peak = peak;
            }

            public float WindowRms { get; }
            public float Peak { get; }
            public bool IsAudible => WindowRms > k_MinimumAudibleLevel && Peak > k_MinimumAudibleLevel;
        }
    }
}
