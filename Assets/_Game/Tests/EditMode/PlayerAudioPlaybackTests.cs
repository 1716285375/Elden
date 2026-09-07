using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class PlayerAudioPlaybackTests
    {
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";

        private GameObject m_root;
        private Component m_player;
        private Component m_network;
        private Component m_sound;

        [SetUp]
        public void SetUp()
        {
            m_root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            m_player = m_root.GetComponent("PlayerManager");
            m_network = m_root.GetComponent("PlayerNetworkManager");
            m_sound = m_root.GetComponentInChildren(
                Type.GetType("ZZ.PlayerSoundFXManager, Assembly-CSharp"), true);
            SetField(m_sound, "m_player", m_player);
            SetField(m_player, "m_characterNetworkManager", m_network);
            m_player.GetType().GetProperty("PlayerNetworkManager").SetValue(m_player, m_network);
            SetNetworkRole("IsSpawned", true);
            SetNetworkRole("IsClient", true);
            SetNetworkRole("IsServer", false);
            SetNetworkRole("IsOwner", true);
            SetField(m_player, "m_isGrounded", true);
            SetField(m_player, "m_canMove", true);
            SetField(m_player, "m_isPerformingAction", false);
            SetField(m_player, "m_shouldApplyRootMotion", false);
            GetNetworkVariable<float>("MoveAmount").Reset(1f);
            GetNetworkVariable<bool>("IsSneaking").Reset(false);
            GetNetworkVariable<bool>("IsChargingAttack").Reset(false);
            GetNetworkVariable<bool>("IsRolling").Reset(false);
            GetNetworkVariable<bool>("IsJumping").Reset(false);
            GetNetworkVariable<bool>("IsClimbingLadder").Reset(false);
            GetNetworkVariable<bool>("IsDead").Reset(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_root != null)
            {
                SetNetworkRole("IsSpawned", false);
                PrefabUtility.UnloadPrefabContents(m_root);
            }
        }

        [Test]
        public void HeavyChargeSuppressesFootstepsWhileMovementInputRemainsHeld()
        {
            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.True);
            GetNetworkVariable<bool>("IsChargingAttack").Reset(true);

            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.False);
            Assert.That(GetNetworkVariable<float>("MoveAmount").Value, Is.EqualTo(1f));

            GetNetworkVariable<bool>("IsChargingAttack").Reset(false);
            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.True);
        }

        [Test]
        public void MovementLockSuppressesFootstepsWithoutClearingHeldInput()
        {
            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.True);
            SetField(m_player, "m_canMove", false);

            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.False);

            SetField(m_player, "m_canMove", true);
            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.True);
        }

        [TestCase("IsRolling")]
        [TestCase("IsJumping")]
        [TestCase("IsClimbingLadder")]
        [TestCase("IsSneaking")]
        [TestCase("IsDead")]
        public void NonWalkingStatesSuppressFootsteps(string stateName)
        {
            GetNetworkVariable<bool>(stateName).Reset(true);

            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.False);
        }

        [Test]
        public void RootMotionDoesNotTurnAnAttackLungeIntoWalkingAudio()
        {
            SetField(m_player, "m_shouldApplyRootMotion", true);

            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.False);
        }

        [Test]
        public void LocalAudioIgnoresCameraDistanceAndRemoteAudioKeepsSpatialBlend()
        {
            AudioSource source = m_sound.GetComponent<AudioSource>();
            SetField(m_sound, "m_audioSource", source);
            SetField(m_sound, "m_remoteSpatialBlend", 0.85f);
            Invoke<object>("UpdateAudioPerspective");
            Assert.That(source.spatialBlend, Is.Zero);

            SetNetworkRole("IsOwner", false);
            Invoke<object>("UpdateAudioPerspective");
            Assert.That(source.spatialBlend, Is.EqualTo(0.85f).Within(0.001f));
        }

        [Test]
        public void FootstepCallbackCannotBypassChargeGateOrDuplicateCadence()
        {
            GetNetworkVariable<bool>("IsChargingAttack").Reset(true);
            Invoke<object>("PlayFootstepSoundEffect");
            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.False);
            GetNetworkVariable<bool>("IsChargingAttack").Reset(false);
            SetNetworkRole("IsClient", false);
            SetNetworkRole("IsServer", true);
            Invoke<object>("PlayFootstepSoundEffect");
            Assert.That(Invoke<bool>("CanPlayFootstep"), Is.False,
                "The timer and an animation event must share one cadence gate.");
        }

        [Test]
        public void DetectorRecoversWhenServerRoleArrivesAfterInitialization()
        {
            Component detector = m_root.GetComponentInChildren(Type.GetType("ZZ.BeaconDetector, Assembly-CSharp"), true);
            SetField(detector, "m_player", m_player);
            SphereCollider collider = detector.GetComponent<SphereCollider>();
            SetField(detector, "m_detectorCollider", collider);
            MethodInfo update = detector.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            update.Invoke(detector, null);
            Assert.That(collider.enabled, Is.False);
            SetNetworkRole("IsServer", true);
            update.Invoke(detector, null);
            Assert.That(collider.enabled, Is.True);
        }

        private NetworkVariable<T> GetNetworkVariable<T>(string name)
        {
            FieldInfo field = m_network.GetType().GetField(name);
            return (NetworkVariable<T>)(field != null
                ? field.GetValue(m_network)
                : m_network.GetType().GetProperty(name).GetValue(m_network));
        }

        private void SetNetworkRole(string name, bool value)
        {
            typeof(NetworkBehaviour).GetProperty(name).SetValue(m_player, value);
        }

        private T Invoke<T>(string methodName, params object[] arguments)
        {
            return (T)m_sound.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(m_sound, arguments);
        }

        private static void SetField(object target, string name, object value)
        {
            Type targetType = target.GetType();
            while (targetType != null)
            {
                FieldInfo field = targetType.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                targetType = targetType.BaseType;
            }

            throw new MissingFieldException(name);
        }
    }
}
