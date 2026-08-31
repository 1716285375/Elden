using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public class ClientPredictionSystemTests
    {
        private const string k_DamageColliderSourcePath =
            "Assets/_Game/Scripts/Combat/Damage/DamageCollider.cs";
        private const string k_NetworkSourcePath =
            "Assets/_Game/Scripts/Characters/Common/CharacterNetworkManager.cs";

        [Test]
        public void DamageEffectsExposeThreeExplicitProcessingPhases()
        {
            Type baseType = GetRuntimeType("ZZ.DamageEffect");
            Type takeDamageType = GetRuntimeType("ZZ.TakeDamageEffect");
            Type blockedType = GetRuntimeType("ZZ.TakeBlockedDamageEffect");
            Type modeType = GetRuntimeType("ZZ.DamageProcessingMode");

            Assert.That(takeDamageType.BaseType, Is.EqualTo(baseType));
            Assert.That(blockedType.IsSubclassOf(baseType), Is.True);
            Assert.That(Enum.GetNames(modeType), Is.EqualTo(new[]
            {
                "PredictedPresentation",
                "Authoritative",
                "ReplicatedPresentation"
            }));
        }

        [TestCase(100f, 25, 75f)]
        [TestCase(20f, 50, 0f)]
        [TestCase(-5f, 10, 0f)]
        public void ProjectedHealthNeverMutatesBelowZero(
            float currentHealth,
            int damage,
            float expectedHealth)
        {
            MethodInfo method = GetRuntimeType("ZZ.DamageEffect").GetMethod(
                "CalculateProjectedHealth",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            Assert.That(
                method.Invoke(null, new object[] { currentHealth, damage }),
                Is.EqualTo(expectedHealth));
        }

        [Test]
        public void PredictionInputsAreReplicatedForLateJoinAndCorrection()
        {
            Type networkType = GetRuntimeType("ZZ.CharacterNetworkManager");
            string[] requiredFields =
            {
                "ArmorPhysicalAbsorption",
                "BasePoiseDefense",
                "OffensivePoiseBonus",
                "TotalPoiseDamage",
                "CurrentStance",
                "CanBeKnockedOffLadder"
            };

            foreach (string fieldName in requiredFields)
            {
                Assert.That(
                    networkType.GetField(fieldName),
                    Is.Not.Null,
                    $"Missing prediction field {fieldName}.");
            }
        }

        [Test]
        public void DamageRequestSkipsInitiatorAndPreservesEnvironmentHits()
        {
            string colliderSource = File.ReadAllText(k_DamageColliderSourcePath);
            string networkSource = File.ReadAllText(k_NetworkSourcePath);

            Assert.That(colliderSource,
                Does.Contain("DamageProcessingMode.PredictedPresentation"));
            Assert.That(colliderSource,
                Does.Contain("m_characterCausingDamage != null"));
            Assert.That(networkSource,
                Does.Contain("NetworkManager.LocalClientId == initiatingClientId"));
            Assert.That(networkSource,
                Does.Contain("if (!attackerIsPresent)"));
            Assert.That(networkSource,
                Does.Contain("senderClientId == target.OwnerClientId"));
        }

        [Test]
        public void LocalDeathUsesOneShotPredictionAndReconciliationApis()
        {
            Type characterType = GetRuntimeType("ZZ.CharacterManager");
            Type animatorType = GetRuntimeType("ZZ.CharacterAnimatorManager");

            Assert.That(characterType.GetProperty("IsDeadLocal"), Is.Not.Null);
            Assert.That(characterType.GetMethod("SetPredictedDead"), Is.Not.Null);
            Assert.That(characterType.GetMethod("ReconcilePredictedDeath"),
                Is.Not.Null);
            Assert.That(characterType.GetMethod("CheckForDeathAnimation"),
                Is.Not.Null);
            Assert.That(animatorType.GetMethod("PlayLocalAnimation"), Is.Not.Null);
            Assert.That(animatorType.GetMethod("PlayLocalAnimationInstantly"),
                Is.Not.Null);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }
    }
}
