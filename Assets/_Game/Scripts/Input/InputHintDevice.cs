using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace ZZ
{
    /// <summary>Tracks meaningful input independently of enabled gameplay action maps.</summary>
    public static class InputHintDevice
    {
        private static InputDevice s_activeDevice;

        public static bool IsGamepad => s_activeDevice is Gamepad;
        public static event Action DeviceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            InputSystem.onEvent -= HandleEvent;
            InputSystem.onDeviceChange -= HandleDeviceChange;
            s_activeDevice = null;
            DeviceChanged = null;
            InputSystem.onEvent += HandleEvent;
            InputSystem.onDeviceChange += HandleDeviceChange;
        }

        private static void HandleEvent(InputEventPtr inputEvent, InputDevice device)
        {
            if (!inputEvent.IsA<StateEvent>() && !inputEvent.IsA<DeltaStateEvent>())
            {
                return;
            }
            if (device is not Keyboard && device is not Mouse && device is not Gamepad)
            {
                return;
            }
            foreach (InputControl control in inputEvent.EnumerateChangedControls(device))
            {
                bool isButton = control is ButtonControl button && button.ReadValueFromEvent(inputEvent) > 0.5f;
                bool isStick = device is Gamepad && control is AxisControl axis &&
                    control.path.Contains("Stick") && Mathf.Abs(axis.ReadValueFromEvent(inputEvent)) > 0.35f;
                bool isMouseMotion = device is Mouse && control is AxisControl mouseAxis &&
                    (control.path.Contains("delta") || control.path.Contains("scroll")) &&
                    Mathf.Abs(mouseAxis.ReadValueFromEvent(inputEvent)) > 2f;
                if (isButton || isStick || isMouseMotion)
                {
                    SetDevice(device);
                    return;
                }
            }
        }

        private static void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == s_activeDevice && (change == InputDeviceChange.Disconnected ||
                change == InputDeviceChange.Removed || change == InputDeviceChange.Disabled))
            {
                SetDevice(null);
            }
        }

        private static void SetDevice(InputDevice device)
        {
            bool wasGamepad = IsGamepad;
            s_activeDevice = device;
            if (wasGamepad != IsGamepad)
            {
                DeviceChanged?.Invoke();
            }
        }
    }
}
