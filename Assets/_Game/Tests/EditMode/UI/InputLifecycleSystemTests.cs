using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Tests
{
    public class InputLifecycleSystemTests
    {
        private GameObject m_root;
        private Component m_input;
        private Type m_inputType;
        private object m_previousInstance;
        private InputActionAsset m_actions;

        [SetUp]
        public void SetUp()
        {
            m_inputType = Type.GetType("ZZ.PlayerInputManager, Assembly-CSharp", true);
            FieldInfo instanceField = GetField("s_instance");
            m_previousInstance = instanceField.GetValue(null);
            m_root = new GameObject("Input Lifecycle Test");
            m_input = m_root.AddComponent(m_inputType);
            instanceField.SetValue(null, m_input);
            Invoke("OnEnable");
            object controls = GetField("m_playerControls").GetValue(m_input);
            m_actions = (InputActionAsset)controls.GetType().GetProperty("asset").GetValue(controls);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_input != null)
            {
                Invoke("OnDisable");
                m_actions.Disable();
                UnityEngine.Object.DestroyImmediate(m_actions);
                GetField("m_playerControls").SetValue(m_input, null);
                Invoke("OnDestroy");
                UnityEngine.Object.DestroyImmediate(m_root);
            }

            GetField("s_instance").SetValue(null, m_previousInstance);
        }

        [Test]
        public void BoundInputUsesTheCombatBufferAndClearsItWhenControlsStop()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            try
            {
                Component manager = player.GetComponent("PlayerManager");
                Component combat = player.GetComponent("PlayerCombatManager");
                manager.GetType().GetProperty("PlayerCombatManager").SetValue(manager, combat);
                GetField("m_player").SetValue(m_input, manager);
                object buffer = combat.GetType().GetProperty("CommandBuffer").GetValue(combat);
                Assert.That(m_inputType.GetProperty("CommandBuffer").GetValue(m_input), Is.SameAs(buffer));
                Type commandType = buffer.GetType().GetMethod("Push").GetParameters()[0].ParameterType;
                buffer.GetType().GetMethod("Push").Invoke(buffer,
                    new[] { Enum.Parse(commandType, "LightAttack") });
                Invoke("ClearAttackInputQueue");
                Assert.That(buffer.GetType().GetProperty("Count").GetValue(buffer), Is.EqualTo(0));
            }
            finally
            {
                GetField("m_player").SetValue(m_input, null);
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        [Test]
        public void HeavyHeldStateEndsOnReleaseAndControlDisable()
        {
            Invoke("EnablePlayerControls");
            Invoke("OnRTStarted", default(InputAction.CallbackContext));
            PropertyInfo held = m_inputType.GetProperty("IsHeavyAttackHeld");
            Assert.That(held, Is.Not.Null);
            Assert.That(held.GetValue(m_input), Is.True);
            Invoke("OnRTCanceled", default(InputAction.CallbackContext));
            Assert.That(held.GetValue(m_input), Is.False);
            Invoke("OnRTStarted", default(InputAction.CallbackContext));
            Invoke("DisablePlayerControls");
            Assert.That(held.GetValue(m_input), Is.False);
        }

        [Test]
        public void HeavyAttackCancellationDoesNotQueueAReleaseAfterControlsAreDisabled()
        {
            Invoke("EnablePlayerControls");
            Invoke("OnRTStarted", default(InputAction.CallbackContext));
            Assert.That(GetField("m_hasRTStartedInput").GetValue(m_input), Is.True);

            Invoke("DisablePlayerControls");
            Invoke("OnRTCanceled", default(InputAction.CallbackContext));

            Assert.That(GetField("m_hasRTStartedInput").GetValue(m_input), Is.False);
            Assert.That(GetField("m_hasRTReleasedInput").GetValue(m_input), Is.False);
            Assert.That(m_actions.FindActionMap("Player Movement").enabled, Is.False);
        }

        [Test]
        public void HeavyAttackCancellationQueuesAReleaseWhileGameplayIsEnabled()
        {
            Invoke("EnablePlayerControls");
            Invoke("OnRTStarted", default(InputAction.CallbackContext));

            Invoke("OnRTCanceled", default(InputAction.CallbackContext));

            Assert.That(GetField("m_hasRTReleasedInput").GetValue(m_input), Is.True);
        }

        [Test]
        public void DisablingTheComponentAlsoDisablesTheMenuPreviewCamera()
        {
            Invoke("EnableMenuCameraInput");
            Assert.That(m_actions.FindActionMap("Player Camera").enabled, Is.True);

            Invoke("OnDisable");

            Assert.That(m_actions.FindActionMap("Player Camera").enabled, Is.False);
        }

        [Test]
        public void LosingFocusSuspendsTheMenuPreviewCamera()
        {
            Invoke("EnableMenuCameraInput");

            Invoke("OnApplicationFocus", false);

            Assert.That(m_actions.FindActionMap("Player Camera").enabled, Is.False);
        }

        [Test]
        public void GameplayCanRemainBlockedWhileTheMenuPreviewCameraIsEnabled()
        {
            Invoke("BlockGameplayInput");

            Invoke("EnableMenuCameraInput");

            Assert.That(m_actions.FindActionMap("Player Movement").enabled, Is.False);
            Assert.That(m_actions.FindActionMap("Player Camera").enabled, Is.True);
        }

        private FieldInfo GetField(string name)
        {
            return m_inputType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        private void Invoke(string name, params object[] arguments)
        {
            m_inputType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(m_input, arguments);
        }
    }
}
