using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ZZ.Demo
{
    /// <summary>
    /// Turns <see cref="DemoInputState"/> into synthetic Input System device events, so a scripted
    /// sequence reaches the game through the real input path instead of poking character code.
    /// </summary>
    /// <remarks>
    /// A replay module can use this as-is to re-drive the game from recorded intents. A recording
    /// module would replace it with the inverse: sample real devices into a
    /// <see cref="DemoInputState"/> per frame.
    /// </remarks>
    public sealed class DemoInputInjector : IDisposable
    {
        private Gamepad m_gamepad;
        private Keyboard m_keyboard;

        public DemoInputInjector()
        {
            m_gamepad = InputSystem.AddDevice<Gamepad>();
            m_keyboard = InputSystem.AddDevice<Keyboard>();
        }

        /// <summary>Queues one frame of intent. Call once per frame before the game reads input.</summary>
        public void Push(DemoInputState state)
        {
            InputSystem.QueueStateEvent(m_gamepad, new GamepadState
            {
                leftStick = state.Move,
                rightStick = state.Look,
                leftTrigger = state.AshOfWar ? 1f : 0f,
                rightTrigger = state.HeavyAttack ? 1f : 0f,
                buttons = BuildGamepadButtons(state)
            });

            // Dodge and Sprint share <Gamepad>/buttonEast in PlayerControls, so those two intents go
            // over the keyboard where space and left shift are unambiguous.
            int keyCount = (state.Sprint ? 1 : 0) + (state.Dodge ? 1 : 0);
            var keys = new Key[keyCount];
            int index = 0;
            if (state.Sprint)
            {
                keys[index++] = Key.LeftShift;
            }

            if (state.Dodge)
            {
                keys[index] = Key.Space;
            }

            InputSystem.QueueStateEvent(m_keyboard, new KeyboardState(keys));
        }

        public void Dispose()
        {
            if (m_gamepad != null)
            {
                InputSystem.QueueStateEvent(m_gamepad, new GamepadState());
                InputSystem.RemoveDevice(m_gamepad);
                m_gamepad = null;
            }

            if (m_keyboard != null)
            {
                InputSystem.QueueStateEvent(m_keyboard, new KeyboardState());
                InputSystem.RemoveDevice(m_keyboard);
                m_keyboard = null;
            }
        }

        /// <summary>
        /// Packs the face, shoulder and stick buttons into the gamepad bitmask. East is deliberately
        /// unused because PlayerControls binds both Dodge and Sprint to it.
        /// </summary>
        private static uint BuildGamepadButtons(DemoInputState state)
        {
            uint buttons = 0;
            if (state.Jump)
            {
                buttons |= 1u << (int)GamepadButton.South;
            }

            if (state.Interact)
            {
                buttons |= 1u << (int)GamepadButton.North;
            }

            if (state.LightAttack)
            {
                buttons |= 1u << (int)GamepadButton.RightShoulder;
            }

            if (state.Block)
            {
                buttons |= 1u << (int)GamepadButton.LeftShoulder;
            }

            if (state.Sneak)
            {
                buttons |= 1u << (int)GamepadButton.LeftStick;
            }

            if (state.LockOn)
            {
                buttons |= 1u << (int)GamepadButton.RightStick;
            }

            return buttons;
        }
    }
}
