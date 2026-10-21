using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class ReleasePolishTests
    {
        [TestCase("Original", "", false)]
        [TestCase("Licensed", "   ", false)]
        [TestCase("Unverified", "some-source", false)]
        [TestCase("Original", "project-source-history", true)]
        [TestCase("Licensed", "license-document-reference", true)]
        public void CommercialArtRequiresClassificationAndEvidence(string status, string evidence, bool expected)
        {
            Type type = Type.GetType("ZZ.Editor.ReleaseReadinessAudit, Assembly-CSharp-Editor");
            Assert.That(type.GetMethod("HasRightsEvidence").Invoke(null, new object[] { status, evidence }),
                Is.EqualTo(expected));
        }

        [Test]
        public void RepeatedHitsCannotExtendAnAnimationPauseIndefinitely()
        {
            GameObject root = new("Impact pause test", typeof(Animator));
            PropertyInfo hitStop = Type.GetType("ZZ.GameSettings, Assembly-CSharp").GetProperty("HitStopEnabled");
            bool previousHitStop = (bool)hitStop.GetValue(null);
            float previousTimeScale = Time.timeScale;
            try
            {
                hitStop.SetValue(null, true);
                Time.timeScale = 1f;
                Animator animator = root.GetComponent<Animator>();
                animator.speed = 1.35f;
                Type type = Type.GetType("ZZ.CombatHitFeedback, Assembly-CSharp");
                Component feedback = root.AddComponent(type);
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                type.GetField("m_animator", flags).SetValue(feedback, animator);
                MethodInfo play = type.GetMethod("Play", flags);
                play.Invoke(feedback, new object[] { 1f, false });
                Assert.That(animator.speed, Is.Zero);
                float beganAt = Time.unscaledTime - 0.09f;
                type.GetField("m_pauseStartedAt", flags).SetValue(feedback, beganAt);
                play.Invoke(feedback, new object[] { 1f, false });
                float resumeAt = (float)type.GetField("m_resumeAt", flags).GetValue(feedback);
                Assert.That(resumeAt, Is.LessThanOrEqualTo(beganAt + 0.101f));
                type.GetMethod("Stop").Invoke(feedback, null);
                Assert.That(animator.speed, Is.EqualTo(1.35f));
                type.GetMethod("Stop").Invoke(feedback, null);
                Assert.That(animator.speed, Is.EqualTo(1.35f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                hitStop.SetValue(null, previousHitStop);
                Time.timeScale = previousTimeScale;
            }
        }

        [Test]
        public void VegetationShaderImportsWithoutDuplicateShadowKeywords()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/_Game/Art/Environment/Nature/Shaders/IdyllicFantasyNature/Vegetation.shadergraph");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [Test]
        public void AnimationShaderContainsForwardDepthNormalsAndShadowPasses()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Shaders/AnimeCharacter.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Material material = new(shader);
            try
            {
                foreach (string pass in new[] { "AnimationLit", "DepthOnly", "DepthNormals", "ShadowCaster" })
                {
                    Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0), pass);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
