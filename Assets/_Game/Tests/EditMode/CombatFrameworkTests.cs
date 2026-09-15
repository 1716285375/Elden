using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests.EditMode
{
    public class CombatFrameworkTests
    {
        [TestCase(2f, 30f, false, 0)]
        [TestCase(30f, 30f, true, 1)]
        [TestCase(40f, 30f, true, 1)]
        [TestCase(0f, 0f, true, 0)]
        [TestCase(30f, -1f, false, 0)]
        [TestCase(float.NaN, 10f, false, 0)]
        [TestCase(30f, float.PositiveInfinity, false, 0)]
        public void CommitPaysOnlyAffordableFiniteCosts(float available, float cost, bool expected, int calls)
        {
            int payments = 0;
            Func<float, bool> consume = value =>
            {
                payments++;
                available -= value;
                return true;
            };
            object result = RuntimeType("CombatActionRunner").GetMethod("TryPay")
                .Invoke(null, new object[] { available, cost, consume });
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(payments, Is.EqualTo(calls));
        }

        [Test]
        public void FailedPaymentDoesNotCommit()
        {
            Func<float, bool> consume = _ => false;
            Assert.That(RuntimeType("CombatActionRunner").GetMethod("TryPay")
                .Invoke(null, new object[] { 100f, 30f, consume }), Is.False);
        }

        [Test]
        public void AttackReplacementAndInterruptionClearEveryWindow()
        {
            Type runnerType = RuntimeType("CombatActionRunner");
            object runner = Activator.CreateInstance(runnerType);
            object hit = Enum.Parse(RuntimeType("CombatWindowType"), "Hit");
            object combo = Enum.Parse(RuntimeType("CombatWindowType"), "Combo");
            runnerType.GetMethod("Begin").Invoke(runner, new object[] { null, null, null, Type.Missing });
            runnerType.GetMethod("SetWindow").Invoke(runner, new[] { hit, (object)true });
            Assert.That(runnerType.GetProperty("Phase").GetValue(runner).ToString(), Is.EqualTo("Startup"));
            runnerType.GetMethod("SetWindow").Invoke(runner, new[] { hit, (object)false });
            Assert.That(runnerType.GetProperty("Phase").GetValue(runner).ToString(), Is.EqualTo("Startup"));
            runnerType.GetMethod("SetWindow").Invoke(runner, new[] { combo, (object)true });
            runnerType.GetMethod("Begin").Invoke(runner, new object[] { null, null, null, Type.Missing });
            object windows = runnerType.GetProperty("Windows").GetValue(runner);
            Assert.That(windows.GetType().GetMethod("IsOpen").Invoke(windows, new[] { combo }), Is.False);
            runnerType.GetMethod("SetWindow").Invoke(runner, new[] { hit, (object)true });
            runnerType.GetMethod("End").Invoke(runner, new object[] { Type.Missing });
            Assert.That(windows.GetType().GetProperty("OpenWindows").GetValue(windows).ToString(), Is.EqualTo("None"));
            Assert.That(runnerType.GetProperty("Phase").GetValue(runner).ToString(), Is.EqualTo("None"));
        }

        [Test]
        public void MigratedWeaponsHaveAllAttacksAndPreserveLegacyStaminaCosts()
        {
            string[] weapons = AssetDatabase.FindAssets("t:MeleeWeaponItem", new[] { "Assets/_Game" });
            Assert.That(weapons.Length, Is.GreaterThan(0));
            foreach (string guid in weapons)
            {
                UnityEngine.Object weapon = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                Type type = weapon.GetType();
                object moveset = type.GetProperty("Moveset").GetValue(weapon);
                Assert.That(moveset, Is.Not.Null, weapon.name);
                foreach (object attackType in Enum.GetValues(RuntimeType("AttackType")))
                {
                    object attack = type.GetMethod("GetAttackDefinition").Invoke(weapon, new[] { attackType });
                    Assert.That(attack, Is.Not.Null, $"{weapon.name}/{attackType}");
                    float baseCost = (float)type.GetProperty("BaseStaminaCost").GetValue(weapon);
                    float multiplier = (float)type.GetMethod("GetStaminaCostMultiplier").Invoke(weapon, new[] { attackType });
                    Assert.That(attack.GetType().GetProperty("StaminaCost").GetValue(attack),
                        Is.EqualTo(baseCost * multiplier).Within(0.001f));
                }
            }
        }

        [TestCase(true, 0.079f, false)]
        [TestCase(true, 0.08f, true)]
        [TestCase(true, 0.549f, true)]
        [TestCase(true, 0.55f, false)]
        [TestCase(false, 0.079f, false)]
        [TestCase(false, 0.08f, true)]
        [TestCase(false, 0.349f, true)]
        [TestCase(false, 0.35f, false)]
        [TestCase(true, 1.2f, false)]
        [TestCase(true, float.NaN, false)]
        public void DodgeImmunityHasStartupRecoveryAndDoesNotLoop(bool isRoll, float time, bool expected)
        {
            // Dodge windows are authored data now; these are the fallback values every character keeps.
            // The type is resolved by reflection because this test assembly does not reference runtime code.
            GameObject host = new GameObject();
            try
            {
                Type animatorType = RuntimeType("CharacterAnimatorManager");
                object animatorManager = host.AddComponent(animatorType);
                object window = animatorType.GetMethod("GetDodgeWindow")
                    .Invoke(animatorManager, new object[] { isRoll });
                Assert.That(window.GetType().GetMethod("Contains").Invoke(window, new object[] { time }),
                    Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase(100f, 0f, 0f, 100f)]
        [TestCase(100f, 100f, 0f, 50f)]
        [TestCase(100f, 100f, 50f, 25f)]
        [TestCase(0.5f, 0f, 0f, 0.5f)]
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(100f, 100f, 100f, 0f)]
        public void DamageChannelsApplyDefenseBeforeAbsorption(float power, float defense, float absorption, float expected)
        {
            Assert.That(RuntimeType("DamageResolver").GetMethod("ResolveChannel")
                .Invoke(null, new object[] { power, defense, absorption }), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void TwoHandStrengthMeetsRequirementWithoutIncreasingOtherAttributes()
        {
            MethodInfo check = RuntimeType("WeaponAttackRating").GetMethod("MeetsRequirements");
            Assert.That(check.Invoke(null, new object[] { 10, 10, 10, 10, false, 15, 10, 10, 10 }), Is.False);
            Assert.That(check.Invoke(null, new object[] { 10, 10, 10, 10, true, 15, 10, 10, 10 }), Is.True);
            Assert.That(check.Invoke(null, new object[] { 10, 10, 10, 10, true, 15, 11, 10, 10 }), Is.False);
        }

        [Test]
        public void HyperArmorConsumesItsOwnBudgetWithinItsAuthoredWindow()
        {
            ScriptableObject definition = ScriptableObject.CreateInstance(RuntimeType("AttackDefinition"));
            try
            {
                var data = new SerializedObject(definition);
                data.FindProperty("m_hyperArmorPoise").floatValue = 30f;
                data.FindProperty("m_hyperArmorWindow").FindPropertyRelative("m_start").floatValue = 0.2f;
                data.FindProperty("m_hyperArmorWindow").FindPropertyRelative("m_end").floatValue = 0.6f;
                data.ApplyModifiedPropertiesWithoutUndo();
                Type runnerType = RuntimeType("CombatActionRunner");
                object runner = Activator.CreateInstance(runnerType);
                object hit = Enum.Parse(RuntimeType("CombatWindowType"), "Hit");
                runnerType.GetMethod("Begin").Invoke(runner,
                    new object[] { definition, null, null, Type.Missing });
                MethodInfo consume = runnerType.GetMethod("ConsumeHyperArmor");
                Assert.That(consume.Invoke(runner, new object[] { 10f }), Is.EqualTo(10f));
                runnerType.GetMethod("SetWindow").Invoke(runner, new[] { hit, (object)true });
                MethodInfo tick = runnerType.GetMethod("Tick", new[] { typeof(float), typeof(float) });
                tick.Invoke(runner, new object[] { 0.25f, 0.25f });
                Assert.That(consume.Invoke(runner, new object[] { 20f }), Is.EqualTo(0f));
                Assert.That(consume.Invoke(runner, new object[] { 20f }), Is.EqualTo(10f));
                runnerType.GetMethod("SetWindow").Invoke(runner, new[] { hit, (object)false });
                tick.Invoke(runner, new object[] { 0.7f, 0.7f });
                Assert.That(consume.Invoke(runner, new object[] { 5f }), Is.EqualTo(5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [TestCase("Straight Sword")]
        [TestCase("Broadsword")]
        public void SwordsOfferMixedChainsAndHigherStancePressureForCommittedAttacks(string weaponName)
        {
            UnityEngine.Object weapon = AssetDatabase.LoadMainAssetAtPath(
                $"Assets/_Game/Data/Items/Weapons/Melee Weapons/{weaponName}.asset");
            MethodInfo lookup = weapon.GetType().GetMethod("GetAttackDefinition");
            object light = lookup.Invoke(weapon, new[] { Enum.Parse(RuntimeType("AttackType"), "LightAttack01") });
            object heavy = lookup.Invoke(weapon, new[] { Enum.Parse(RuntimeType("AttackType"), "HeavyAttack01") });
            object charged = lookup.Invoke(weapon, new[] { Enum.Parse(RuntimeType("AttackType"), "ChargedAttack01") });
            MethodInfo followUp = light.GetType().GetMethod("GetFollowUp", new[] { RuntimeType("CombatCommand") });
            Assert.That(followUp.Invoke(light, new[] { Enum.Parse(RuntimeType("CombatCommand"), "HeavyAttack") }),
                Is.SameAs(heavy));
            float lightStance = (float)light.GetType().GetProperty("StanceDamage").GetValue(light);
            float heavyStance = (float)heavy.GetType().GetProperty("StanceDamage").GetValue(heavy);
            float chargedStance = (float)charged.GetType().GetProperty("StanceDamage").GetValue(charged);
            Assert.That(heavyStance, Is.GreaterThan(lightStance));
            Assert.That(chargedStance, Is.GreaterThan(heavyStance));
        }

        [TestCase("MeleeWeaponDamageCollider", false, false)]
        [TestCase("MeleeWeaponDamageCollider", true, true)]
        [TestCase("RangeProjectileDamageCollider", true, false)]
        [TestCase("SpellProjectileDamageCollider", true, false)]
        public void OnlyRunningMeleeActionsOwnTheContactRegistry(string detectorName, bool running, bool usesAction)
        {
            GameObject player = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            var detectorRoot = new GameObject("Contact ownership test", typeof(BoxCollider));
            ScriptableObject definition = ScriptableObject.CreateInstance(RuntimeType("AttackDefinition"));
            try
            {
                Component character = player.GetComponent("PlayerManager");
                Component combat = player.GetComponent("PlayerCombatManager");
                RuntimeType("CharacterManager").GetField("m_characterCombatManager",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(character, combat);
                object runner = combat.GetType().GetProperty("ActionRunner").GetValue(combat);
                if (running)
                {
                    runner.GetType().GetMethod("Begin").Invoke(runner,
                        new object[] { definition, character, null, Type.Missing });
                }

                Component detector = detectorRoot.AddComponent(RuntimeType(detectorName));
                object candidate = Activator.CreateInstance(RuntimeType("HitCandidate"),
                    detector, character, player.GetComponent<Collider>());
                object action = candidate.GetType().GetField("Action").GetValue(candidate);
                Assert.That(action != null, Is.EqualTo(usesAction));
                if (usesAction)
                {
                    runner.GetType().GetMethod("Begin").Invoke(runner,
                        new object[] { definition, character, null, Type.Missing });
                    Assert.That(candidate.GetType().GetProperty("IsValid").GetValue(candidate), Is.False,
                        "An old contact must not migrate into the next swing of the same definition.");
                    object[] registration = { 0 };
                    Assert.That(candidate.GetType().GetMethod("TryRegisterHit").Invoke(candidate, registration),
                        Is.False, "A stale contact must not poison the new swing's hit registry.");
                    candidate = Activator.CreateInstance(RuntimeType("HitCandidate"),
                        detector, character, player.GetComponent<Collider>());
                    runner.GetType().GetMethod("End").Invoke(runner, new object[] { Type.Missing });
                    Assert.That(candidate.GetType().GetProperty("IsValid").GetValue(candidate), Is.False,
                        "An interrupted contact cannot settle after its action ends.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(detectorRoot);
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        [Test]
        public void StanceOnlyAttackRetainsItsCommittedSnapshot()
        {
            ScriptableObject definition = ScriptableObject.CreateInstance(RuntimeType("AttackDefinition"));
            try
            {
                var data = new SerializedObject(definition);
                data.FindProperty("m_stanceDamage").floatValue = 25f;
                data.ApplyModifiedPropertiesWithoutUndo();
                object snapshot = RuntimeType("AttackSnapshot").GetMethod("Capture").Invoke(null,
                    new object[] { definition, null, null, Enum.ToObject(RuntimeType("AttackType"), 0) });
                Assert.That(snapshot.GetType().GetProperty("IsValid").GetValue(snapshot), Is.True);
                data.FindProperty("m_stanceDamage").floatValue = 100f;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(snapshot.GetType().GetField("StanceDamage").GetValue(snapshot), Is.EqualTo(25f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static Type RuntimeType(string name)
        {
            foreach (string assembly in new[] { "Assembly-CSharp", "ZZ.Combat.Rules", "ZZ.Foundation" })
            {
                Type type = Type.GetType($"ZZ.{name}, {assembly}", false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not resolve ZZ.{name}.");
            return null;
        }
    }
}
