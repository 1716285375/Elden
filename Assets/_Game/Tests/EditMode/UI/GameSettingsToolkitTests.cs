using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public class GameSettingsToolkitTests
    {
        [UnityTest]
        public IEnumerator NativeControlsWritePreferencesAndRefreshVisibleValues()
        {
            yield return new EnterPlayMode();
            Type preferences = Type.GetType("ZZ.GameSettings, Assembly-CSharp", true);
            Type panelType = Type.GetType("ZZ.GameSettingsPanel, Assembly-CSharp", true);
            string[] floatKeys = { "Volume", "Look", "Shake" };
            string[] intKeys = { "Invert", "Quality", "Fullscreen", "FrameRate", "Haptics", "HitStop" };
            var originalKeys = new HashSet<string>();
            var floatValues = new Dictionary<string, float>();
            var intValues = new Dictionary<string, int>();
            foreach (string key in floatKeys)
            {
                string fullKey = "ZZ.Settings." + key;
                if (PlayerPrefs.HasKey(fullKey))
                {
                    originalKeys.Add(fullKey);
                }
                floatValues[fullKey] = PlayerPrefs.GetFloat(fullKey);
            }
            foreach (string key in intKeys)
            {
                string fullKey = "ZZ.Settings." + key;
                if (PlayerPrefs.HasKey(fullKey))
                {
                    originalKeys.Add(fullKey);
                }
                intValues[fullKey] = PlayerPrefs.GetInt(fullKey);
            }
            object originalVolume = preferences.GetProperty("Volume").GetValue(null);
            object originalLook = preferences.GetProperty("LookSensitivity").GetValue(null);
            object originalInvert = preferences.GetProperty("InvertLook").GetValue(null);
            object originalShake = preferences.GetProperty("ShakeIntensity").GetValue(null);
            object originalHaptics = preferences.GetProperty("HapticsEnabled").GetValue(null);
            object originalHitStop = preferences.GetProperty("HitStopEnabled").GetValue(null);
            GameObject parent = new GameObject("Settings Value Test");
            try
            {
                Component panel = (Component)panelType.GetMethod("Create").Invoke(null, new object[] { parent.transform });
                panelType.GetMethod("Open").Invoke(panel, new object[] { (Action)(() => { }) });
                yield return null;
                VisualElement root = panel.GetComponent<UIDocument>().rootVisualElement;
                yield return null;
                root.Q<Slider>("volume").Focus();
                yield return null;
                Assert.That(root.Q<Label>("settings-help").text, Is.EqualTo("调整游戏整体音量。"));
                Assert.That(root.Q<Slider>("volume").GetFirstAncestorOfType<VisualElement>()
                    .parent.ClassListContains("settings-row--focused"), Is.True);
                root.Q<Slider>("volume").value = 37f;
                Assert.That((float)preferences.GetProperty("Volume").GetValue(null), Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(root.Q<Label>("volume-value").text, Is.EqualTo("37%"));
                Submit(root.Q<Button>("tab-controls"));
                Assert.That(root.Q<Label>("category-heading").text, Is.EqualTo("镜头操作"));
                root.Q<Slider>("look").value = 1.75f;
                root.Q<Toggle>("invert").value = !(bool)originalInvert;
                Assert.That(preferences.GetProperty("LookSensitivity").GetValue(null), Is.EqualTo(1.75f));
                Assert.That(preferences.GetProperty("InvertLook").GetValue(null), Is.EqualTo(!(bool)originalInvert));
                Submit(root.Q<Button>("tab-impact"));
                root.Q<Slider>("shake").value = 0f;
                root.Q<Toggle>("haptics").value = !(bool)originalHaptics;
                root.Q<Toggle>("hit-stop").value = !(bool)originalHitStop;
                Assert.That(preferences.GetProperty("ShakeIntensity").GetValue(null), Is.EqualTo(0f));
                Assert.That(preferences.GetProperty("HapticsEnabled").GetValue(null), Is.EqualTo(!(bool)originalHaptics));
                Assert.That(preferences.GetProperty("HitStopEnabled").GetValue(null), Is.EqualTo(!(bool)originalHitStop));
                Assert.That(root.Q<Toggle>("haptics").text, Is.EqualTo((bool)originalHaptics ? "关闭" : "开启"));
                Assert.That(PlayerPrefs.GetFloat("ZZ.Settings.Volume"), Is.EqualTo(0.37f).Within(0.001f));
                root.Q<Slider>("volume").value = 49f;
                Assert.That(PlayerPrefs.GetFloat("ZZ.Settings.Volume"), Is.EqualTo(0.37f).Within(0.001f),
                    "A slider previews immediately without flushing every intermediate value");
                panelType.GetMethod("Close").Invoke(panel, null);
                Assert.That(PlayerPrefs.GetFloat("ZZ.Settings.Volume"), Is.EqualTo(0.49f).Within(0.001f),
                    "Closing commits the last slider value before destroying its scheduled callback");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                preferences.GetMethod("SetVolume").Invoke(null, new[] { originalVolume });
                preferences.GetMethod("SetLook").Invoke(null, new[] { originalLook, originalInvert });
                preferences.GetMethod("SetImpact").Invoke(null, new[] { originalShake, originalHaptics, originalHitStop });
                foreach (KeyValuePair<string, float> pair in floatValues)
                {
                    if (originalKeys.Contains(pair.Key))
                    {
                        PlayerPrefs.SetFloat(pair.Key, pair.Value);
                    }
                    else
                    {
                        PlayerPrefs.DeleteKey(pair.Key);
                    }
                }
                foreach (KeyValuePair<string, int> pair in intValues)
                {
                    if (originalKeys.Contains(pair.Key))
                    {
                        PlayerPrefs.SetInt(pair.Key, pair.Value);
                    }
                    else
                    {
                        PlayerPrefs.DeleteKey(pair.Key);
                    }
                }
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator ReopenRebuildsControlsAndCancelInvokesOnlyTheCurrentOwner()
        {
            yield return new EnterPlayMode();
            GameObject parent = new GameObject("Settings Lifecycle Test");
            try
            {
                Type type = Type.GetType("ZZ.GameSettingsPanel, Assembly-CSharp", true);
                Component panel = (Component)type.GetMethod("Create").Invoke(null, new object[] { parent.transform });
                int firstCloseCount = 0;
                int secondCloseCount = 0;
                type.GetMethod("Open").Invoke(panel, new object[] { (Action)(() => firstCloseCount++) });
                yield return null;
                UIDocument document = panel.GetComponent<UIDocument>();
                VisualElement firstRoot = document.rootVisualElement;
                Submit(firstRoot.Q<Button>("tab-impact"));
                Assert.That(firstRoot.Q("page-impact").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(firstRoot.Q("page-audio").style.display.value, Is.EqualTo(DisplayStyle.None));
                Submit(firstRoot.Q<Button>("back"));
                Assert.That(panel.gameObject.activeSelf, Is.False);
                Assert.That(firstCloseCount, Is.EqualTo(1));
                type.GetMethod("Close").Invoke(panel, null);
                Assert.That(firstCloseCount, Is.EqualTo(1), "Close must be idempotent");
                type.GetMethod("Open").Invoke(panel, new object[] { (Action)(() => secondCloseCount++) });
                yield return null;
                VisualElement reopenedRoot = document.rootVisualElement;
                Assert.That(reopenedRoot.Q("page-impact").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                using (NavigationCancelEvent cancel = NavigationCancelEvent.GetPooled())
                {
                    cancel.target = reopenedRoot.Q<Button>("tab-impact");
                    reopenedRoot.SendEvent(cancel);
                }
                Assert.That(secondCloseCount, Is.EqualTo(1));
                Assert.That(firstCloseCount, Is.EqualTo(1), "Old callbacks must not survive reopen");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreEditorMode()
        {
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        private static void Submit(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
