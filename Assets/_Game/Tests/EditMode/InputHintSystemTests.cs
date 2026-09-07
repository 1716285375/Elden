using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public sealed class InputHintSystemTests
    {
        private Type m_deviceType;
        private Gamepad m_gamepad;
        private Keyboard m_keyboard;

        [SetUp]
        public void SetUp()
        {
            m_deviceType = Type.GetType("ZZ.InputHintDevice, Assembly-CSharp", true);
            m_deviceType.GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            m_gamepad = InputSystem.AddDevice<Gamepad>();
            m_keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(m_gamepad);
            InputSystem.RemoveDevice(m_keyboard);
        }

        [Test]
        public void LastActualInputSwitchesIconsAndDisconnectRestoresKeyboard()
        {
            InputSystem.QueueStateEvent(m_gamepad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.Update();
            Assert.That(IsGamepad(), Is.True);
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(Key.R));
            InputSystem.Update();
            Assert.That(IsGamepad(), Is.False);
            InputSystem.QueueStateEvent(m_gamepad, new GamepadState { leftStick = new Vector2(0.8f, 0) });
            InputSystem.Update();
            Assert.That(IsGamepad(), Is.True);
            InputSystem.DisableDevice(m_gamepad);
            Assert.That(IsGamepad(), Is.False);
        }

        [Test]
        public void StickDriftDoesNotReplaceKeyboardHints()
        {
            InputSystem.QueueStateEvent(m_gamepad, new GamepadState { leftStick = new Vector2(0.1f, -0.1f) });
            InputSystem.Update();
            Assert.That(IsGamepad(), Is.False);
        }

        [Test]
        public void InteractionHintRendersActualBindingAndChangesWhileVisible()
        {
            var root = new GameObject("Hint Test", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                Type type = Type.GetType("ZZ.InputHintText, Assembly-CSharp", true);
                var text = root.GetComponent<TMP_Text>();
                type.GetMethod("SetTemplate").Invoke(null, new object[] { text, "{Interact} Use" });
                Component hint = root.GetComponent(type);
                type.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hint, null);
                Assert.That(text.text, Does.Contain("keyboard_r"));
                InputSystem.QueueStateEvent(m_gamepad, new GamepadState().WithButton(GamepadButton.North));
                InputSystem.Update();
                Assert.That(text.text, Does.Contain("xbox_button_y"));
                Assert.That(text.spriteAsset.GetSpriteIndexFromName("xbox_button_y"), Is.GreaterThanOrEqualTo(0));
                type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hint, null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CharacterCommandsHaveSeparateTargetsAndUpgradeBelongsToCanvas()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab");
            try
            {
                Transform column = root.transform.Find("Player UI/Character Menu/Menu Panel/Command Column");
                Assert.That(column.childCount, Is.EqualTo(6));
                Button back = column.Find("Return To Main Menu Button").GetComponent<Button>();
                Button quit = column.Find("Quit Game Button").GetComponent<Button>();
                Assert.That(back.onClick.GetPersistentMethodName(0), Is.EqualTo("ReturnToMainMenu"));
                Assert.That(quit.onClick.GetPersistentMethodName(0), Is.EqualTo("QuitGame"));
                Transform upgrade = root.transform.Find("Player UI/Weapon Upgrade Menu");
                Assert.That(upgrade, Is.Not.Null);
                Assert.That(upgrade.GetComponentInParent<Canvas>(true), Is.Not.Null);
                Assert.That(upgrade.GetComponentInParent<GraphicRaycaster>(true), Is.Not.Null);
                foreach (Button button in column.GetComponentsInChildren<Button>(true))
                {
                    Assert.That(button.navigation.selectOnDown, Is.Not.Null);
                    Assert.That(button.navigation.selectOnUp, Is.Not.Null);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private bool IsGamepad()
        {
            return (bool)m_deviceType.GetProperty("IsGamepad").GetValue(null);
        }
    }
}
