using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ZZ.Tests
{
    public sealed class OriginalControlsTests : InputTestFixture
    {
        private InputActionAsset m_actions;
        private Keyboard m_keyboard;
        private Mouse m_mouse;
        private Gamepad m_gamepad;
        private bool m_shortcuts;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            m_actions = InputActionAsset.FromJson(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Game/Settings/Input/PlayerControls.inputactions").ToJson());
            m_keyboard = InputSystem.AddDevice<Keyboard>();
            m_mouse = InputSystem.AddDevice<Mouse>();
            m_gamepad = InputSystem.AddDevice<Gamepad>();
            m_actions.devices = new InputDevice[] { m_keyboard, m_mouse, m_gamepad };
            m_shortcuts = InputSystem.settings.shortcutKeysConsumeInput;
            InputSystem.settings.shortcutKeysConsumeInput = true;
        }

        [TearDown]
        public override void TearDown()
        {
            m_actions.Disable();
            Object.DestroyImmediate(m_actions);
            InputSystem.RemoveDevice(m_keyboard);
            InputSystem.RemoveDevice(m_mouse);
            InputSystem.RemoveDevice(m_gamepad);
            InputSystem.settings.shortcutKeysConsumeInput = m_shortcuts;
            base.TearDown();
        }

        [Test]
        public void LockToggleAcceptsQMiddleMouseAndRightStickPress()
        {
            InputAction action = m_actions.FindAction("Player Camera/Lock On", true);
            int presses = 0;
            action.performed += _ => presses++;
            action.Enable();
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(Key.Q));
            InputSystem.Update();
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(m_mouse, new MouseState().WithButton(MouseButton.Middle));
            InputSystem.Update();
            InputSystem.QueueStateEvent(m_mouse, new MouseState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(m_gamepad, new GamepadState().WithButton(GamepadButton.RightStick));
            InputSystem.Update();
            Assert.That(presses, Is.EqualTo(3));
        }

        [TestCase(Key.LeftShift)]
        [TestCase(Key.RightShift)]
        public void ShiftMouseAttackStartsHeavyWithoutLightAttack(Key shift)
        {
            InputAction light = m_actions.FindAction("Player Movement/RB", true);
            InputAction heavy = m_actions.FindAction("Player Movement/RT", true);
            int lightCount = 0;
            int heavyCount = 0;
            light.performed += _ => lightCount++;
            heavy.started += _ => heavyCount++;
            light.Enable();
            heavy.Enable();
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(shift));
            InputSystem.Update();
            InputSystem.QueueStateEvent(m_mouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.That(heavyCount, Is.EqualTo(1));
            Assert.That(lightCount, Is.Zero);
        }

        [Test]
        public void WeaponArrowsDoNotMovePlayerAndQDoesNotSwapWeapons()
        {
            InputAction move = m_actions.FindAction("Player Movement/Movement", true);
            InputAction left = m_actions.FindAction("Player Movement/Switch Left Weapon", true);
            int swaps = 0;
            left.performed += _ => swaps++;
            move.Enable();
            left.Enable();
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(Key.LeftArrow));
            InputSystem.Update();
            Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
            Assert.That(swaps, Is.EqualTo(1));
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(Key.Q));
            InputSystem.Update();
            Assert.That(swaps, Is.EqualTo(1));
        }

        [Test]
        public void WheelDownCyclesItemsAndShiftWheelUpOnlyCyclesRightWeapon()
        {
            InputAction item = m_actions.FindAction("Player Movement/Switch Quick Slot Item", true);
            InputAction weapon = m_actions.FindAction("Player Movement/Switch Right Weapon", true);
            int items = 0;
            int weapons = 0;
            item.performed += _ => items++;
            weapon.performed += _ => weapons++;
            item.Enable();
            weapon.Enable();
            InputSystem.QueueStateEvent(m_mouse, new MouseState { scroll = new Vector2(0f, -120f) });
            InputSystem.Update();
            Assert.That(items, Is.EqualTo(1));
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            InputSystem.QueueStateEvent(m_mouse, new MouseState { scroll = new Vector2(0f, 120f) });
            InputSystem.Update();
            Assert.That(weapons, Is.EqualTo(1));
            Assert.That(items, Is.EqualTo(1));
        }

        [TestCase(Key.LeftAlt)]
        [TestCase(Key.RightAlt)]
        public void AltActivatesWalkModifier(Key alt)
        {
            InputAction walk = m_actions.FindAction("Player Movement/Walk", true);
            walk.Enable();
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(alt));
            InputSystem.Update();
            Assert.That(walk.IsPressed(), Is.True);
            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(walk.IsPressed(), Is.False);
        }

        [Test]
        public void FullActionMapPreservesOrdinaryLeftClick()
        {
            int attacks = 0;
            m_actions.FindAction("Player Movement/RB", true).performed += _ => attacks++;
            m_actions.Enable();
            InputSystem.QueueStateEvent(m_mouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.That(attacks, Is.EqualTo(1));
        }
    }
}
