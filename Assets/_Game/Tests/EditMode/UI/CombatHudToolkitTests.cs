using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace ZZ.Tests.EditMode
{
    public class CombatHudToolkitTests
    {
        [TestCase(50f, 100f, 0.5f)]
        [TestCase(-10f, 100f, 0f)]
        [TestCase(150f, 100f, 1f)]
        [TestCase(10f, 0f, 0f)]
        [TestCase(float.NaN, 100f, 0f)]
        [TestCase(10f, float.PositiveInfinity, 0f)]
        public void CorruptOrUninitializedStatsHaveFiniteDisplay(float current, float maximum, float expected)
        {
            Type type = Type.GetType("ZZ.HudResourceMeter, Assembly-CSharp", true);
            float actual = (float)type.GetMethod("Normalize").Invoke(null, new object[] { current, maximum });
            Assert.That(actual, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void AuthoredLayoutProvidesAllThreeResourceChannelsAndInteraction()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/_Game/Resources/UI/CombatHud.uxml");
            Assert.That(layout, Is.Not.Null);
            VisualElement root = layout.CloneTree();
            foreach (string channel in new[] { "health", "stamina", "focus" })
            {
                Assert.That(root.Q(channel + "-fill"), Is.Not.Null, channel);
                Assert.That(root.Q(channel + "-trail"), Is.Not.Null, channel);
                Assert.That(root.Q<Label>(channel + "-value"), Is.Not.Null, channel);
            }
            Assert.That(root.Q<Label>("interaction-key"), Is.Not.Null);
            Assert.That(root.Q<Label>("currency-value"), Is.Not.Null);
            Assert.That(Resources.Load<ThemeStyleSheet>("UI/CombatTheme"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator DisabledHudDocumentRestoresTreeAndNeverCapturesPointerInput()
        {
            yield return new EnterPlayMode();
            GameObject parent = new GameObject("Toolkit Lifecycle Test");
            try
            {
                Type type = Type.GetType("ZZ.CombatHudView, Assembly-CSharp", true);
                Component view = (Component)type.GetMethod("Create").Invoke(null, new object[] { parent.transform });
                Assert.That(view, Is.Not.Null);
                parent.SetActive(false);
                parent.SetActive(true);
                type.GetMethod("SetHealth").Invoke(view, new object[] { 25f, 100f });
                type.GetMethod("SetPrompt").Invoke(view, new object[] { "打开宝箱" });
                UIDocument document = view.GetComponent<UIDocument>();
                Assert.That(document.rootVisualElement.Q<Label>("health-value").text, Is.EqualTo("25 / 100"));
                document.rootVisualElement.Query<VisualElement>().ForEach(element =>
                    Assert.That(element.pickingMode, Is.EqualTo(PickingMode.Ignore)));
                type.GetMethod("SetVisible").Invoke(view, new object[] { false });
                Assert.That(document.rootVisualElement.style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [UnityTest]
        public IEnumerator FrontendHidesBoundHudAndGameplayRestoresIt()
        {
            yield return new EnterPlayMode();
            Scene previousScene = SceneManager.GetActiveScene();
            Scene frontend = SceneManager.CreateScene("SCN_MainMenu");
            Scene gameplay = SceneManager.CreateScene("HUD Presentation Test");
            SceneManager.SetActiveScene(frontend);
            GameObject parent = new GameObject("HUD Visibility Test");
            try
            {
                Type viewType = Type.GetType("ZZ.CombatHudView, Assembly-CSharp", true);
                Component view = (Component)viewType.GetMethod("Create").Invoke(null, new object[] { parent.transform });
                Type managerType = Type.GetType("ZZ.PlayerUIHUDManager, Assembly-CSharp", true);
                Component manager = parent.AddComponent(managerType);
                managerType.GetField("m_toolkit", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).SetValue(manager, view);
                managerType.GetMethod("ShowHUD").Invoke(manager, null);
                Assert.That(view.GetComponent<UIDocument>().rootVisualElement.style.display.value,
                    Is.EqualTo(DisplayStyle.None));
                SceneManager.SetActiveScene(gameplay);
                managerType.GetMethod("ShowHUD").Invoke(manager, null);
                Assert.That(view.GetComponent<UIDocument>().rootVisualElement.style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
            }
            finally
            {
                SceneManager.SetActiveScene(previousScene);
                UnityEngine.Object.DestroyImmediate(parent);
            }
            yield return SceneManager.UnloadSceneAsync(frontend);
            yield return SceneManager.UnloadSceneAsync(gameplay);
        }

        [UnityTearDown]
        public IEnumerator RestoreEditorMode()
        {
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }
    }
}
