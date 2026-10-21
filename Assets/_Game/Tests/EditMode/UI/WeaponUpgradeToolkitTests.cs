using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public class WeaponUpgradeToolkitTests
    {
        [Test]
        public void NativeUpgradeLayoutSeparatesSelectionFromConfirmation()
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/WeaponUpgrade");
            Assert.That(layout, Is.Not.Null);
            VisualElement root = layout.CloneTree();
            VisualElement content = root.Q("upgrade-content");
            VisualElement confirmation = root.Q("upgrade-confirmation");
            Assert.That(content, Is.Not.Null);
            Assert.That(confirmation, Is.Not.Null);
            Assert.That(content.Contains(confirmation), Is.False);
            foreach (string command in new[] { "right", "left", "upgrade", "back" })
            {
                Assert.That(content.Q<Button>(command), Is.Not.Null, command);
            }
            Assert.That(confirmation.Q<Button>("confirm"), Is.Not.Null);
            Assert.That(confirmation.Q<Button>("cancel"), Is.Not.Null);
            Assert.That(content.Q<Label>("material-count"), Is.Not.Null);
        }
    }
}
