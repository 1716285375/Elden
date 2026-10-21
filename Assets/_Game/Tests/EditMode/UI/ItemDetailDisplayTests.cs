using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public sealed class ItemDetailDisplayTests
    {
        [TestCase("WeaponMenu")]
        [TestCase("EquipmentMenu")]
        [TestCase("InventoryMenu")]
        public void DisplayCommandsToggleIndependentlyAndReleaseCallbacks(string layoutName)
        {
            VisualElement root = Resources.Load<VisualTreeAsset>("UI/" + layoutName).CloneTree();
            Type type = Type.GetType("ZZ.ItemDetailDisplay, Assembly-CSharp", true);
            var display = (IDisposable)Activator.CreateInstance(type, new object[] { root });
            VisualElement surface = root.Q(className: "menu-overlay");
            try
            {
                Assert.That(surface.ClassListContains("menu--description"), Is.False);
                type.GetMethod("ToggleDescription").Invoke(display, null);
                Assert.That(surface.ClassListContains("menu--description"), Is.True);
                Assert.That(surface.ClassListContains("menu--simple"), Is.False);
                type.GetMethod("ToggleStatus").Invoke(display, null);
                Assert.That(surface.ClassListContains("menu--simple"), Is.True);
                type.GetMethod("ToggleDescription").Invoke(display, null);
                Assert.That(surface.ClassListContains("menu--description"), Is.False);
                Assert.That(root.Q<Button>("switch-display").Q<Image>().image, Is.Not.Null);
            }
            finally
            {
                display.Dispose();
            }
            Assert.DoesNotThrow(() =>
            {
                using var reopened = (IDisposable)Activator.CreateInstance(type, new object[] { root });
                Assert.That(surface.ClassListContains("menu--simple"), Is.False);
                Assert.That(root.Q<Button>("switch-display").Query<Image>().ToList().Count, Is.EqualTo(1));
            });
        }
    }
}
