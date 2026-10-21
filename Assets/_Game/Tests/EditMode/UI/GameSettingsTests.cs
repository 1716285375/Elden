using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public class GameSettingsTests
    {
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(-3f, 0.25f)]
        [TestCase(9f, 3f)]
        [TestCase(1.75f, 1.75f)]
        public void CameraPreferenceRejectsInvalidNumbers(float input, float expected)
        {
            Type type = Type.GetType("ZZ.GameSettings, Assembly-CSharp");
            MethodInfo clamp = type.GetMethod("ClampFinite", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(clamp.Invoke(null, new object[] { input, 0.25f, 3f }), Is.EqualTo(expected));
        }

        [Test]
        public void SettingsPanelUsesNativeControlsWithoutALegacyCanvas()
        {
            GameObject parent = new("Settings Test", typeof(RectTransform));
            try
            {
                Type type = Type.GetType("ZZ.GameSettingsPanel, Assembly-CSharp");
                Component panel = (Component)type.GetMethod("Create").Invoke(null, new object[] { parent.transform });
                Assert.That(panel.GetComponentsInChildren<Canvas>(true), Is.Empty);
                Assert.That(panel.GetComponentsInChildren<UnityEngine.UI.Selectable>(true), Is.Empty);
                UIDocument document = panel.GetComponent<UIDocument>();
                Assert.That(document, Is.Not.Null);
                VisualElement root = document.visualTreeAsset.CloneTree();
                foreach (string name in new[] { "volume", "look", "shake" })
                {
                    Assert.That(root.Q<UnityEngine.UIElements.Slider>(name), Is.Not.Null);
                }
                foreach (string name in new[] { "invert", "haptics", "hit-stop" })
                {
                    Assert.That(root.Q<UnityEngine.UIElements.Toggle>(name), Is.Not.Null);
                }
                foreach (string name in new[] { "quality", "display", "frame-rate" })
                {
                    Assert.That(root.Q<DropdownField>(name), Is.Not.Null);
                }
                Assert.That(panel.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
