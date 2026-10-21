using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json.Linq;

namespace ZZ.Editor
{
    public static class OriginalControlsSetup
    {
        [MenuItem("Tools/ZZ/Input/Apply Elden Ring Defaults")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before changing input assets.");
            }
            const string path = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            InputActionMap movement = asset.FindActionMap("Player Movement", true);
            InputAction move = movement.FindAction("Movement", true);
            for (int index = move.bindings.Count - 1; index >= 0; index--)
            {
                if (move.bindings[index].name == "Arrows")
                {
                    move.ChangeBinding(index).Erase();
                }
            }
            Bind(movement, "Dodge", "Tap(duration=0.25)", "<Gamepad>/buttonEast", "<Keyboard>/space");
            Bind(movement, "Sprint", "Hold(duration=0.25)", "<Gamepad>/buttonEast", "<Keyboard>/space");
            Bind(movement, "Jump", "", "<Gamepad>/buttonSouth", "<Keyboard>/f");
            Bind(movement, "Sneak", "", "<Gamepad>/leftStickPress", "<Keyboard>/x");
            if (movement.FindAction("Walk") == null) { movement.AddAction("Walk", InputActionType.Button); }
            Bind(movement, "Walk", "", "<Keyboard>/leftAlt", "<Keyboard>/rightAlt");
            Bind(movement, "Interact", "Tap(duration=0.25)", "<Gamepad>/buttonNorth", "<Keyboard>/e");
            Bind(movement, "Use Quick Slot Item", "", "<Gamepad>/buttonWest", "<Keyboard>/r");
            Bind(movement, "Switch Right Weapon", "", "<Gamepad>/dpad/right", "<Keyboard>/rightArrow");
            Bind(movement, "Switch Left Weapon", "", "<Gamepad>/dpad/left", "<Keyboard>/leftArrow");
            Bind(movement, "Switch Quick Slot Item", "", "<Gamepad>/dpad/down", "<Keyboard>/downArrow");
            Bind(movement, "RB", "", "<Gamepad>/rightShoulder", "<Mouse>/leftButton");
            Bind(movement, "LB", "", "<Gamepad>/leftShoulder", "<Mouse>/rightButton");
            Bind(movement, "RT", "", "<Gamepad>/rightTrigger");
            Bind(movement, "LT", "", "<Gamepad>/leftTrigger");
            ModifiedMouse(movement.FindAction("RT"), "<Keyboard>/leftShift", "<Mouse>/leftButton");
            ModifiedMouse(movement.FindAction("LT"), "<Keyboard>/leftShift", "<Mouse>/rightButton");
            ModifiedMouse(movement.FindAction("RT"), "<Keyboard>/rightShift", "<Mouse>/leftButton");
            ModifiedMouse(movement.FindAction("LT"), "<Keyboard>/rightShift", "<Mouse>/rightButton");
            movement.FindAction("Switch Quick Slot Item").AddBinding("<Mouse>/scroll/down", groups: "Keyboard&Mouse");
            foreach (string modifier in new[] { "<Keyboard>/leftShift", "<Keyboard>/rightShift" })
            {
                ModifiedMouse(movement.FindAction("Switch Right Weapon"), modifier, "<Mouse>/scroll/up");
                ModifiedMouse(movement.FindAction("Switch Left Weapon"), modifier, "<Mouse>/scroll/down");
            }
            Bind(movement, "Two Hand Weapon", "", "<Gamepad>/buttonNorth", "<Keyboard>/e");
            Bind(movement, "Two Hand Right Weapon", "", "<Gamepad>/rightShoulder", "<Mouse>/leftButton");
            Bind(movement, "Two Hand Left Weapon", "", "<Gamepad>/leftShoulder", "<Mouse>/rightButton");
            Bind(asset.FindActionMap("Player Camera", true), "Lock On", "",
                "<Gamepad>/rightStickPress", "<Keyboard>/q", "<Mouse>/middleButton");
            Bind(asset.FindActionMap("UI", true), "Close Menu", "", "<Gamepad>/buttonEast", "<Keyboard>/q");
            Bind(asset.FindActionMap("UI", true), "Previous Shop Category", "", "<Gamepad>/leftShoulder", "<Keyboard>/z");
            Bind(asset.FindActionMap("UI", true), "Next Shop Category", "", "<Gamepad>/rightShoulder", "<Keyboard>/c");
            InputSystem.settings.shortcutKeysConsumeInput = true;
            EditorUtility.SetDirty(InputSystem.settings);
            JObject json = JObject.Parse(asset.ToJson());
            foreach (JToken map in json["maps"])
            {
                foreach (JToken action in map["actions"])
                {
                    action["interactions"] = "";
                }
            }
            File.WriteAllText(path, json.ToString());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            PlayerMovementSetup.GeneratePlayerControlsClass();
            const string uiPath = "Assets/_Game/Settings/Input/InputSystem_Actions.inputactions";
            InputActionAsset ui = AssetDatabase.LoadAssetAtPath<InputActionAsset>(uiPath);
            InputActionMap uiMap = ui.FindActionMap("UI", true);
            Bind(uiMap, "Submit", "", "<Gamepad>/buttonSouth", "<Keyboard>/e", "<Keyboard>/enter");
            Bind(uiMap, "Cancel", "", "<Gamepad>/buttonEast", "<Keyboard>/q", "<Keyboard>/escape");
            File.WriteAllText(uiPath, ui.ToJson());
            AssetDatabase.ImportAsset(uiPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("ORIGINAL_CONTROLS: gameplay defaults and generated wrapper updated.");
        }

        private static void Bind(InputActionMap map, string name, string interactions, params string[] paths)
        {
            InputAction action = map.FindAction(name, true);
            while (action.bindings.Count > 0)
            {
                action.ChangeBinding(0).Erase();
            }
            foreach (string path in paths)
            {
                action.AddBinding(path, interactions: interactions,
                    groups: path.StartsWith("<Gamepad>") ? "Gamepad" : "Keyboard&Mouse");
            }
        }

        private static void ModifiedMouse(InputAction action, string modifier, string button)
        {
            action.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", modifier, "Keyboard&Mouse").With("Button", button, "Keyboard&Mouse");
        }
    }
}
