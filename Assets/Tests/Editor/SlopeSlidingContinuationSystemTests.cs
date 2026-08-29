using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public class SlopeSlidingContinuationSystemTests
    {
        [Test]
        public static void WorldUtilityPersistsAndClassifiesCharacterLayers()
        {
            string utilitySource = ReadRuntimeSource(
                "World Managers/WorldUtilityManager.cs");
            Type utilityType = GetRuntimeType("ZZ.WorldUtilityManager");
            GameObject utilityObject = new GameObject("EP126 Utility Test");
            utilityObject.SetActive(false);
            try
            {
                Component utility = utilityObject.AddComponent(utilityType);
                MethodInfo maskMethod = utilityType.GetMethod(
                    "GetCharacterLayers");
                LayerMask characterLayers = (LayerMask)maskMethod.Invoke(
                    utility,
                    null);

                Assert.That(utilitySource, Does.Contain("DontDestroyOnLoad"));
                Assert.That(characterLayers.value & (1 << 8), Is.Not.Zero);
                Assert.That(characterLayers.value & (1 << 10), Is.Not.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(utilityObject);
            }
        }

        [Test]
        public static void GroundedEdgesAreProtectedVirtualAndStateDriven()
        {
            Type locomotionType = GetRuntimeType(
                "ZZ.CharacterLocomotionManager");
            const BindingFlags k_NonPublicInstance =
                BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo groundedMethod = locomotionType.GetMethod(
                "OnIsGrounded",
                k_NonPublicInstance);
            MethodInfo airborneMethod = locomotionType.GetMethod(
                "OnIsNotGrounded",
                k_NonPublicInstance);
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(groundedMethod?.IsVirtual, Is.True);
            Assert.That(groundedMethod?.IsFamily, Is.True);
            Assert.That(airborneMethod?.IsVirtual, Is.True);
            Assert.That(airborneMethod?.IsFamily, Is.True);
            Assert.That(source, Does.Contain("UpdateGroundedState(isGrounded)"));
            Assert.That(source, Does.Contain("wasGrounded == isGrounded"));
        }

        [Test]
        public static void JumpAscentForcesAirborneBeforeGroundProbe()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");
            int jumpGuardIndex = source.IndexOf(
                "m_characterManager.IsJumping && m_verticalVelocity.y > 0f",
                StringComparison.Ordinal);
            int groundProbeIndex = source.IndexOf(
                "Physics.CheckSphere(",
                StringComparison.Ordinal);

            Assert.That(jumpGuardIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(groundProbeIndex, Is.GreaterThan(jumpGuardIndex));
            Assert.That(source, Does.Contain("UpdateGroundedState(false)"));
        }

        [Test]
        public static void AirCollisionSlidesUntilGroundedThenResets()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(source, Does.Contain("OnControllerColliderHit"));
            Assert.That(
                source,
                Does.Contain("m_slideUntilGrounded = true"));
            Assert.That(
                source,
                Does.Contain("protected virtual void OnIsGrounded()"));
            Assert.That(
                source,
                Does.Contain("m_slideUntilGrounded = false"));
            Assert.That(source, Does.Contain("GetSlipperyEnviroLayers()"));
            Assert.That(source, Does.Contain("GetEnvironmentLayers()"));
        }

        [Test]
        public static void CharacterProbeExcludesSelfAndRequiresBelowCollision()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(source, Does.Contain("Physics.OverlapSphere("));
            Assert.That(
                source,
                Does.Contain("characterCollider.transform.root == transform.root"));
            Assert.That(
                source,
                Does.Contain("GetComponent<CharacterController>()"));
            Assert.That(source, Does.Contain("CollisionFlags.Below"));
            Assert.That(source, Does.Contain("SlideOffCharacter();"));
        }

        [Test]
        public static void CharacterSlideCoroutineIsSingleAndSurfaceProjected()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(source, Does.Contain("m_slideOffCharacterCoroutine"));
            Assert.That(source, Does.Contain("StopCoroutine("));
            Assert.That(source, Does.Contain("Physics.SphereCastAll("));
            Assert.That(source, Does.Contain("Vector3.ProjectOnPlane("));
            Assert.That(
                source,
                Does.Contain("m_verticalVelocity.y += m_slopeSlideForce"));
            Assert.That(
                source,
                Does.Contain("m_characterSlideVelocity * Time.deltaTime"));
        }

        [Test]
        public static void CharacterSlideVelocityFollowsSurfaceNormal()
        {
            Type locomotionType = GetRuntimeType(
                "ZZ.CharacterLocomotionManager");
            MethodInfo calculationMethod = locomotionType.GetMethod(
                "CalculateCharacterSlideVelocity",
                BindingFlags.Public | BindingFlags.Static);
            Vector3 normal = Quaternion.AngleAxis(
                35f,
                Vector3.forward) * Vector3.up;
            Vector3 velocity = (Vector3)calculationMethod.Invoke(
                null,
                new object[] { -8f, normal });

            Assert.That(
                Mathf.Abs(Vector3.Dot(velocity, normal)),
                Is.LessThan(0.0001f));
            Assert.That(velocity.y, Is.LessThan(0f));
        }

        [Test]
        public static void CharacterCollisionTuningMatchesEP126Defaults()
        {
            string source = ReadRuntimeSource(
                "Character/CharacterLocomotionManager.cs");

            Assert.That(
                source,
                Does.Contain(
                    "m_characterCollisionCheckSphereMultiplier = 1.5f"));
            Assert.That(
                source,
                Does.Contain(
                    "m_characterSlideOffHeadCollisionMaxDistance = 5f"));
            Assert.That(
                source,
                Does.Contain("m_groundCheckRadius *"));
            Assert.That(
                source,
                Does.Contain("m_characterCollisionCheckSphereMultiplier;"));
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
