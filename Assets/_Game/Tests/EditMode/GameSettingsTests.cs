using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
        public void SettingsPanelKeepsNavigationInsideItsOwnControls()
        {
            GameObject parent = new("Settings Test", typeof(RectTransform));
            try
            {
                Type type = Type.GetType("ZZ.GameSettingsPanel, Assembly-CSharp");
                Component panel = (Component)type.GetMethod("Create").Invoke(null, new object[] { parent.transform });
                Button[] buttons = panel.GetComponentsInChildren<Button>(true);
                Assert.That(buttons.Length, Is.EqualTo(10));
                foreach (Button button in buttons)
                {
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                    Assert.That(buttons, Contains.Item(button.navigation.selectOnUp));
                    Assert.That(buttons, Contains.Item(button.navigation.selectOnDown));
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
