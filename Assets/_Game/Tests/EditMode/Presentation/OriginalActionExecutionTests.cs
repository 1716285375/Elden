using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ZZ.Tests
{
    public sealed class OriginalActionExecutionTests : InputTestFixture
    {
        [Test]
        public void OriginalAttackRetainsAnimatedWeaponOffsets()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Transform anchor = prefab.GetComponentsInChildren<Transform>(true).Single(item => item.name == "ER_R_Weapon");
            string path = AnimationUtility.CalculateTransformPath(anchor, animator.transform);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/_Game/Art/Characters/EldenRing/PlayerActions/Humanoid/Clips/a030_030000.anim");
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(binding =>
                binding.path == path && binding.propertyName == "m_LocalRotation.x"), Is.True,
                "The original weapon moves relative to the hand; Humanoid muscles cannot retain that track.");
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/_Game/Art/Characters/EldenRing/PlayerActions/Humanoid/Clips/a000_000000.anim");
            var values = new float[4];
            for (int axis = 0; axis < 4; axis++)
            {
                values[axis] = AnimationUtility.GetEditorCurve(idle, EditorCurveBinding.FloatCurve(
                    path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis])).Evaluate(0f);
            }
            Assert.That(Quaternion.Angle(anchor.localRotation,
                new Quaternion(values[0], values[1], values[2], values[3]).normalized), Is.LessThan(0.1f),
                "Clips without weapon tracks must fall back to the original idle grip, not the FLVER bind pose.");
        }

        [Test]
        public void ReleasingTwoHandChordBeforeGameTickRetainsOnlyTheStanceCommand()
        {
            Type type = Type.GetType("ZZ.PlayerInputManager, Assembly-CSharp");
            FieldInfo singleton = type.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = singleton.GetValue(null);
            var root = new GameObject("Real input fixture");
            root.SetActive(false);
            Component input = root.AddComponent(type);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            InputActionAsset actions = null;
            try
            {
                singleton.SetValue(null, input);
                type.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
                object controls = type.GetField("m_playerControls", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(input);
                actions = (InputActionAsset)controls.GetType().GetProperty("asset").GetValue(controls);
                actions.devices = new InputDevice[] { keyboard, mouse };
                actions.FindActionMap("Player Movement").Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                InputSystem.Update();
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.That(type.GetField("m_hasRBInput", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(input), Is.False, "Changing stance must not leave a light attack queued.");
                Assert.That(type.GetField("m_hasTwoHandRightWeaponInput", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(input), Is.True, "Releasing the modifier must not erase an already submitted chord.");
                Assert.That(type.GetField("m_isTwoHandInputHeld", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(input), Is.False, "PassThrough reports release through performed; E must not stay held.");
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(type.GetField("m_hasRBInput", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(input), Is.True, "An ordinary click after releasing E must queue a light attack.");
            }
            finally
            {
                type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
                singleton.SetValue(null, previous);
                if (actions != null) { UnityEngine.Object.DestroyImmediate(actions); }
                UnityEngine.Object.DestroyImmediate(root);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }
    }
}
