using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public sealed class MenuAudioFeedbackTests
    {
        [Test]
        public void FeedbackResolvesButtonChildrenAndRejectsDisabledButtons()
        {
            Type type = Type.GetType("ZZ.MenuAudioFeedback, Assembly-CSharp");
            MethodInfo method = type.GetMethod("FindButton", BindingFlags.Static | BindingFlags.NonPublic);
            var button = new Button();
            var label = new Label("Confirm");
            button.Add(label);
            Assert.That(method.Invoke(null, new object[] { label }), Is.SameAs(button));
            button.SetEnabled(false);
            Assert.That(method.Invoke(null, new object[] { label }), Is.Null);
            Assert.That(method.Invoke(null, new object[] { new VisualElement() }), Is.Null);
        }

        [TestCase("TitleIntro", 645210)]
        [TestCase("TitleLoop", 6852550)]
        public void OriginalMusicSegmentsRetainAuthoredSampleBoundary(string name, int frames)
        {
            string path = "Assets/_Game/Art/Audio/EldenRing/Title/" + name + ".wav";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.frequency, Is.EqualTo(48000));
            Assert.That(clip.samples, Is.EqualTo(frames));
            Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
        }
    }
}
