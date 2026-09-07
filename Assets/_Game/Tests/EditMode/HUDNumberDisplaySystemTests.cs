using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public class HUDNumberDisplaySystemTests
    {
        private GameObject m_root;
        private Component m_display;

        [SetUp]
        public void SetUp()
        {
            m_root = new GameObject("HUD Number Test", typeof(RectTransform));
            m_display = m_root.AddComponent(Type.GetType("ZZ.HUDNumberDisplay, Assembly-CSharp", true));
            Sprite[] sprites = Enumerable.Range(0, 10).Select(value => AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/_Game/Art/UI/HUD/Numbers/hud_{value}.png")).ToArray();
            Assert.That(sprites.All(sprite => sprite != null), Is.True);
            SetField("m_digitSprites", sprites);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_root);
        }

        [Test]
        public void RepeatedValueReusesItsExistingImages()
        {
            SetNumber(125);
            Image[] firstImages = m_root.GetComponentsInChildren<Image>(true);

            SetNumber(125);

            Assert.That(m_root.GetComponentsInChildren<Image>(true), Is.EqualTo(firstImages));
        }

        [Test]
        public void ShrinkingAndGrowingAValueReusesImagesAndImmediatelyHidesSurplusDigits()
        {
            SetNumber(125);
            Image[] firstImages = m_root.GetComponentsInChildren<Image>(true);

            SetNumber(7);

            Assert.That(m_root.GetComponentsInChildren<Image>().Length, Is.EqualTo(1));
            SetNumber(234);
            Assert.That(m_root.GetComponentsInChildren<Image>(true), Is.EqualTo(firstImages));
        }

        [Test]
        public void ClearingThenShowingZeroReusesTheExistingPool()
        {
            SetNumber(123);
            SetNumber(-1);
            Assert.That(m_root.GetComponentsInChildren<Image>(), Is.Empty);

            SetNumber(0);

            Assert.That(m_root.GetComponentsInChildren<Image>().Single().sprite.name, Is.EqualTo("hud_0"));
            Assert.That(m_root.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(3));
        }

        [Test]
        public void LeftAlignmentPlacesAllDigitsInsideTheLeftEdgeInReadingOrder()
        {
            SetField("m_rightAligned", false);

            SetNumber(123);

            Image[] images = m_root.GetComponentsInChildren<Image>()
                .OrderBy(image => image.rectTransform.anchoredPosition.x).ToArray();
            Assert.That(images.Select(image => image.sprite.name), Is.EqualTo(new[] { "hud_1", "hud_2", "hud_3" }));
            Assert.That(images.All(image => image.rectTransform.pivot.x == 0f), Is.True);
        }

        private void SetNumber(int value)
        {
            m_display.GetType().GetMethod("SetNumber").Invoke(m_display, new object[] { value });
        }

        private void SetField(string name, object value)
        {
            m_display.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(m_display, value);
        }
    }
}
