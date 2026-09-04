using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class SlopeSlidingSystemTests
    {
        private const int k_SlipperyLayerIndex = 15;

        [Test]
        public static void SlipperyDefaultUsesDedicatedPhysicsLayer()
        {
            Assert.That(
                LayerMask.NameToLayer("Slippery Default"),
                Is.EqualTo(k_SlipperyLayerIndex));
        }

        [Test]
        public static void SlipperyLayerBelongsToAllRequiredMasks()
        {
            GameObject utilityObject = new GameObject("Utility Mask Test");
            utilityObject.SetActive(false);
            try
            {
                Component utility = utilityObject.AddComponent(
                    GetRuntimeType("ZZ.WorldUtilityManager"));
                int slipperyLayerBit = 1 << k_SlipperyLayerIndex;

                AssertMaskContainsLayer(
                    utility,
                    "GetEnvironmentLayers",
                    slipperyLayerBit);
                AssertMaskContainsLayer(
                    utility,
                    "GetGroundLayers",
                    slipperyLayerBit);
                AssertMaskContainsLayer(
                    utility,
                    "GetSlipperyEnviroLayers",
                    slipperyLayerBit);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(utilityObject);
            }
        }

        [Test]
        public static void SlideVelocityIsProjectedAlongSurface()
        {
            Type locomotionType = GetRuntimeType(
                "ZZ.CharacterLocomotionManager");
            MethodInfo calculateMethod = locomotionType.GetMethod(
                "CalculateSlopeSlideVelocity",
                BindingFlags.Public | BindingFlags.Static);
            Vector3 normal = Quaternion.AngleAxis(30f, Vector3.forward) *
                Vector3.up;
            Vector3 velocity = (Vector3)calculateMethod.Invoke(
                null,
                new object[] { Vector3.down, normal, 11f });

            Assert.That(
                Mathf.Abs(Vector3.Dot(velocity, normal)),
                Is.LessThan(0.0001f));
            Assert.That(velocity.magnitude, Is.EqualTo(11f).Within(0.0001f));
            Assert.That(velocity.y, Is.LessThan(0f));
        }

        [Test]
        public static void ProbeUsesGroundRadiusThresholdAndStateSpecificMasks()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(source, Does.Contain("Physics.SphereCast("));
            Assert.That(source, Does.Contain("m_groundCheckRadius"));
            Assert.That(source, Does.Contain("Vector3.Angle(hitInfo.normal"));
            Assert.That(source, Does.Contain("m_slipperySurfaceMaxAngle"));
            Assert.That(source, Does.Contain("GetEnvironmentLayers()"));
            Assert.That(source, Does.Contain("GetSlipperyEnviroLayers()"));
        }

        [Test]
        public static void OwnerAppliesPreviousSlideBeforeNextProbe()
        {
            string source = ReadRuntimeSource(
                "Character/Player/PlayerLocomotionManager.cs");
            int setVelocityIndex = source.IndexOf(
                "SetGroundedVelocity();",
                StringComparison.Ordinal);
            int slopeCheckIndex = source.IndexOf(
                "HandleSlopeSlideCheck();",
                StringComparison.Ordinal);

            Assert.That(setVelocityIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(slopeCheckIndex, Is.GreaterThan(setVelocityIndex));
        }

        [Test]
        public static void SafetyGuardsCoverJumpGravityControllerAndRemoteDrift()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(
                source,
                Does.Contain("m_characterManager.IsJumping && " +
                    "m_verticalVelocity.y > 0f"));
            Assert.That(source, Does.Contain("m_ignoreGravity"));
            Assert.That(source, Does.Contain("!m_characterController.enabled"));
            Assert.That(source, Does.Contain("sqrMagnitude <= 6.25f"));
            Assert.That(source, Does.Contain("NetworkPosition.Value"));
            Assert.That(source, Does.Contain("ClearSlopeSlideState()"));
        }

        [Test]
        public static void DefaultTuningMatchesEP124Values()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(
                source,
                Does.Contain("m_slopeSlideStartPositionYOffset = 1f"));
            Assert.That(
                source,
                Does.Contain("m_slopeSlideSphereCastMaxDistance = 2f"));
            Assert.That(
                source,
                Does.Contain("m_slipperySurfaceMaxAngle = 15f"));
            Assert.That(source, Does.Contain("m_slopeSlideSpeed = 11f"));
            Assert.That(
                source,
                Does.Contain("m_slopeSlideSpeedMultiplier = 3f"));
            Assert.That(source, Does.Contain("m_slopeSlideForce = -5f"));
            Assert.That(source, Does.Contain("m_slideUntilGrounded"));
        }

        private static void AssertMaskContainsLayer(
            Component utility,
            string methodName,
            int layerBit)
        {
            MethodInfo method = utility.GetType().GetMethod(methodName);
            LayerMask mask = (LayerMask)method.Invoke(utility, null);
            Assert.That(mask.value & layerBit, Is.Not.Zero, methodName);
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            relativePath = RemapRuntimeSourcePath(relativePath);
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }
        /// <summary>Maps a pre-refactor Script-relative path to the new layout.</summary>
        private static string RemapRuntimeSourcePath(string relativePath)
        {
            if (relativePath.StartsWith("Character/Player/Player UI/"))
                return "UI/Gameplay/Player/" + relativePath.Substring("Character/Player/Player UI/".Length);
            if (relativePath.StartsWith("Character/Player/"))
                return "Characters/Player/" + relativePath.Substring("Character/Player/".Length);
            if (relativePath.StartsWith("Character/AI/"))
                return "Characters/AI/" + relativePath.Substring("Character/AI/".Length);
            if (relativePath.StartsWith("Character/Effects/"))
                return "Characters/Common/Effects/" + relativePath.Substring("Character/Effects/".Length);
            if (relativePath.StartsWith("Character/Equipment/"))
                return "Characters/Common/Equipment/" + relativePath.Substring("Character/Equipment/".Length);
            if (relativePath.StartsWith("Character/Inventory/"))
                return "Characters/Common/Inventory/" + relativePath.Substring("Character/Inventory/".Length);
            if (relativePath.StartsWith("Character/Character UI/"))
                return "UI/Gameplay/Character/" + relativePath.Substring("Character/Character UI/".Length);
            if (relativePath.StartsWith("Character/Animation State Behaviors/"))
                return "Characters/Common/Animation State Behaviors/" + relativePath.Substring("Character/Animation State Behaviors/".Length);
            if (relativePath.StartsWith("Character/"))
                return "Characters/Common/" + relativePath.Substring("Character/".Length);
            if (relativePath.StartsWith("World Managers/AI/"))
                return "World/AI/" + relativePath.Substring("World Managers/AI/".Length);
            if (relativePath.StartsWith("World Managers/"))
                return "World/Managers/" + relativePath.Substring("World Managers/".Length);
            if (relativePath.StartsWith("World Objects/"))
                return "World/Objects/" + relativePath.Substring("World Objects/".Length);
            if (relativePath.StartsWith("Save System/"))
                return "Save/" + relativePath.Substring("Save System/".Length);
            if (relativePath.StartsWith("Menu Scene/"))
                return "UI/Frontend/" + relativePath.Substring("Menu Scene/".Length);
            if (relativePath.StartsWith("Effects/"))
                return "Combat/Effects/" + relativePath.Substring("Effects/".Length);
            if (relativePath.StartsWith("Damage/"))
                return "Combat/Damage/" + relativePath.Substring("Damage/".Length);
            if (relativePath.StartsWith("Actions/"))
                return "Combat/Actions/" + relativePath.Substring("Actions/".Length);
            if (relativePath.StartsWith("Projectiles/"))
                return "Combat/Projectiles/" + relativePath.Substring("Projectiles/".Length);
            if (relativePath.StartsWith("Spells/"))
                return "Abilities/Spells/" + relativePath.Substring("Spells/".Length);
            if (relativePath.StartsWith("Utility/"))
                return "Utilities/" + relativePath.Substring("Utility/".Length);
            return relativePath;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
