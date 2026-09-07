using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class SoundEffectVolumeTests
    {
        private const int k_SampleRate = 1000;
        private readonly List<AudioClip> m_createdClips = new();
        private Type m_volumeType;

        [SetUp]
        public void SetUp()
        {
            m_volumeType = Type.GetType("ZZ.SoundEffectVolume, Assembly-CSharp", true);
            ResetCache();
        }

        [TearDown]
        public void TearDown()
        {
            ResetCache();
            foreach (AudioClip clip in m_createdClips)
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }

            m_createdClips.Clear();
        }

        [Test]
        public void LouderVariantIsAttenuatedWithoutAmplifyingTheQuietVariant()
        {
            AudioClip quiet = CreateClip(0.2f);
            AudioClip loud = CreateClip(0.5f);
            AudioClip[] sounds = { quiet, loud };

            Assert.That(GetVolumeScale(sounds, loud), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(GetVolumeScale(sounds, quiet), Is.EqualTo(1f));
        }

        [Test]
        public void PaddingWithSilenceDoesNotChangeTheSameImpactLoudness()
        {
            AudioClip shortSound = CreateClip(0.4f, activeFrames: 20, totalFrames: 20);
            AudioClip paddedSound = CreateClip(0.4f, activeFrames: 20, totalFrames: 300);
            AudioClip[] sounds = { shortSound, paddedSound };

            Assert.That(GetVolumeScale(sounds, shortSound), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(GetVolumeScale(sounds, paddedSound), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void LoudnessMeasurementDoesNotDependOnWindowAlignment()
        {
            AudioClip initialImpact = CreateClip(0.4f, activeFrames: 40, totalFrames: 150);
            AudioClip shiftedImpact = CreateClip(0.4f, activeFrames: 40, totalFrames: 150, leadingFrames: 25);
            AudioClip[] sounds = { initialImpact, shiftedImpact };

            Assert.That(GetVolumeScale(sounds, shiftedImpact), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(GetVolumeScale(sounds, initialImpact), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void OutlierCanReduceAnotherVariantByAtMostNineDecibels()
        {
            AudioClip quiet = CreateClip(0.001f);
            AudioClip loud = CreateClip(0.9f);

            float gain = GetVolumeScale(new[] { quiet, loud }, loud);

            Assert.That(gain, Is.EqualTo(Mathf.Pow(10f, -9f / 20f)).Within(0.0001f));
        }

        [Test]
        public void NullAndSilentVariantsDoNotMuteAudibleSounds()
        {
            AudioClip silence = CreateClip(0f);
            AudioClip audible = CreateClip(0.4f);
            AudioClip[] sounds = { null, silence, audible };

            Assert.That(GetVolumeScale(sounds, audible), Is.EqualTo(1f));
            Assert.That(GetVolumeScale(sounds, silence), Is.EqualTo(1f));
            Assert.That(GetVolumeScale(sounds, null), Is.EqualTo(1f));
            Assert.That(GetVolumeScale(null, audible), Is.EqualTo(1f));
            Assert.That(GetVolumeScale(Array.Empty<AudioClip>(), audible), Is.EqualTo(1f));
        }

        [Test]
        public void StereoChannelsAreMeasuredAsEnergyWithoutPhaseCancellation()
        {
            AudioClip mono = CreateClip(0.4f);
            AudioClip stereo = CreateClip(0.4f, channels: 2);
            AudioClip[] sounds = { mono, stereo };

            Assert.That(GetVolumeScale(sounds, mono), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(GetVolumeScale(sounds, stereo), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SingleClipAndClipOutsidePoolKeepTheirAuthoredVolume()
        {
            AudioClip quiet = CreateClip(0.1f);
            AudioClip loud = CreateClip(0.5f);

            Assert.That(GetVolumeScale(new[] { quiet }, quiet), Is.EqualTo(1f));
            Assert.That(GetVolumeScale(new[] { quiet }, loud), Is.EqualTo(1f));
        }

        [Test]
        public void StreamingClipsKeepTheirVolumeWithoutAttemptingToReadSamples()
        {
            string path = $"Assets/_Game/Tests/AudioImportTest_{Guid.NewGuid():N}.wav";
            try
            {
                // AudioClip.Create(stream: true) reports DecompressOnLoad in Unity 6.
                // An imported streaming clip exercises the actual authored-asset contract.
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(236);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(k_SampleRate);
                    writer.Write(k_SampleRate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    writer.Write(200);
                    writer.Write(new byte[200]);
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                AudioClip streaming = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                AudioClip audible = CreateClip(0.3f);
                Assert.That(streaming.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
                Assert.That(GetVolumeScale(new[] { streaming, audible }, streaming), Is.EqualTo(1f));
                Assert.That(GetVolumeScale(new[] { streaming, audible }, audible), Is.EqualTo(1f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void CacheResetRefreshesMeasurementsWhenDomainReloadIsDisabled()
        {
            AudioClip quiet = CreateClip(0.2f);
            AudioClip loud = CreateClip(0.5f);
            AudioClip[] sounds = { quiet, loud };
            Assert.That(GetVolumeScale(sounds, loud), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(quiet.SetData(CreateSamples(0.5f, 100, 100, 1, 0), 0), Is.True);

            ResetCache();

            Assert.That(GetVolumeScale(sounds, loud), Is.EqualTo(1f).Within(0.0001f));
        }

        private AudioClip CreateClip(
            float amplitude,
            int activeFrames = 100,
            int totalFrames = 100,
            int channels = 1,
            int leadingFrames = 0)
        {
            AudioClip clip = AudioClip.Create("Volume Test", totalFrames, channels, k_SampleRate, false);
            m_createdClips.Add(clip);
            Assert.That(clip.SetData(CreateSamples(amplitude, activeFrames, totalFrames, channels, leadingFrames), 0),
                Is.True);
            return clip;
        }

        private static float[] CreateSamples(
            float amplitude,
            int activeFrames,
            int totalFrames,
            int channels,
            int leadingFrames)
        {
            var samples = new float[totalFrames * channels];
            for (int frame = leadingFrames; frame < leadingFrames + activeFrames; frame++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    samples[frame * channels + channel] = channel % 2 == 0 ? amplitude : -amplitude;
                }
            }

            return samples;
        }

        private float GetVolumeScale(AudioClip[] sounds, AudioClip selectedClip)
        {
            return (float)m_volumeType.GetMethod("GetVolumeScale").Invoke(null, new object[] { sounds, selectedClip });
        }

        private void ResetCache()
        {
            m_volumeType?.GetMethod("ResetCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
    }
}
