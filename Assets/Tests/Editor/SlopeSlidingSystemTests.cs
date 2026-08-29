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
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
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
