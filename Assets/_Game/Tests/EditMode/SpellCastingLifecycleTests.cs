using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class SpellCastingLifecycleTests
    {
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_FireballPath =
            "Assets/_Game/Data/Items/Spells/Fireball.asset";
        private const string k_CatalystPath =
            "Assets/_Game/Data/Items/Weapons/Catalysts/Incantation Catalyst.asset";

        private GameObject m_root;
        private Component m_player;
        private Component m_combat;
        private Component m_network;
        private object m_spells;
        private UnityEngine.Object m_spell;
        private UnityEngine.Object m_catalyst;

        [SetUp]
        public void SetUp()
        {
            m_root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            m_player = m_root.GetComponent("PlayerManager");
            m_combat = m_root.GetComponent("PlayerCombatManager");
            m_network = m_root.GetComponent("PlayerNetworkManager");
            Component inventory = m_root.GetComponent("PlayerInventoryManager");
            m_spell = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadMainAssetAtPath(k_FireballPath));
            m_catalyst = AssetDatabase.LoadMainAssetAtPath(k_CatalystPath);

            Assert.That(m_player, Is.Not.Null);
            Assert.That(m_combat, Is.Not.Null);
            Assert.That(m_network, Is.Not.Null);
            typeof(NetworkBehaviour).GetProperty("IsOwner")
                .SetValue(m_player, true);
            SetField(m_player, "m_characterNetworkManager", m_network);
            SetField(m_player, "m_isGrounded", true);
            SetField(inventory, "m_currentSpell", m_spell);
            m_player.GetType().GetProperty("InventoryManager")
                .SetValue(m_player, inventory);

            // Exercise local decisions without starting network or animation presentation.
            m_player.GetType().GetProperty("PlayerNetworkManager")
                .SetValue(m_player, null);
            m_player.GetType().GetProperty("PlayerCombatManager")
                .SetValue(m_player, null);
            m_player.GetType().GetProperty("PlayerStatsManager")
                .SetValue(m_player, null);
            SetField(m_combat, "m_player", m_player);
            m_spells = Activator.CreateInstance(Type.GetType("ZZ.SpellCombatController, Assembly-CSharp", true), new object[] { m_player });
            SetField(m_combat, "m_spells", m_spells);
            GetResource("CurrentStamina").Reset(100f);
            GetResource("CurrentFocusPoints").Reset(100f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_spell);
            if (m_root != null)
            {
                PrefabUtility.UnloadPrefabContents(m_root);
            }
        }

        [TestCase(24f, 25, false)]
        [TestCase(25f, 25, true)]
        [TestCase(26f, 25, true)]
        [TestCase(0f, 0, true)]
        public void SpellEligibilityAcceptsExactlyTheAuthoredFocusPointCost(
            float currentFocusPoints,
            int focusPointsCost,
            bool expectedCanCast)
        {
            GetResource("CurrentFocusPoints").Reset(currentFocusPoints);
            SetField(m_spell, "m_focusPointsCost", focusPointsCost);

            object canCast = m_spell.GetType().GetMethod("CanICastThisSpell")
                .Invoke(m_spell, new object[] { m_player, m_catalyst });

            Assert.That(canCast, Is.EqualTo(expectedCanCast));
        }

        [Test]
        public void ReleasedSpellCannotBecomeFullyChargedDuringItsReleaseAnimation()
        {
            PrepareCharge();
            InvokeCombat("ReleaseChargingSpell", true);
            SetField(m_spells, "m_spellChargeStartTime", Time.time - 10f);

            m_spells.GetType().GetMethod("Tick").Invoke(m_spells, new object[] { 1.5f });

            Assert.That(GetField(m_spells, "m_hasReachedFullSpellCharge"), Is.False);
            Assert.That(m_combat.GetType().GetProperty("IsChargingSpell")
                .GetValue(m_combat), Is.False);
            Assert.That(GetField(m_spells, "m_currentCastingSpell"), Is.SameAs(m_spell),
                "The release animation still needs its committed spell snapshot.");
        }

        [Test]
        public void ReleaseConsumesOnlyItsOwnHandAndCompletionAllowsAnotherCharge()
        {
            PrepareCharge();
            InvokeCombat("ReleaseChargingSpell", false);
            Assert.That(m_combat.GetType().GetProperty("IsChargingSpell")
                .GetValue(m_combat), Is.True);

            InvokeCombat("ReleaseChargingSpell", true);
            InvokeCombat("ReleaseChargingSpell", true);
            Assert.That(m_combat.GetType().GetProperty("IsChargingSpell")
                .GetValue(m_combat), Is.False);

            InvokeCombat("CompleteSpellCast");
            PrepareCharge();
            Assert.That(m_combat.GetType().GetProperty("IsChargingSpell")
                .GetValue(m_combat), Is.True);
        }

        [TestCase(24f, 100f, false, false)]
        [TestCase(25f, 25f, false, true)]
        [TestCase(100f, 24f, false, false)]
        [TestCase(34f, 100f, true, false)]
        [TestCase(35f, 35f, true, true)]
        public void ReleaseRequiresBothCompleteResourceCosts(
            float stamina, float focusPoints, bool fullyCharged, bool expected)
        {
            SetField(m_spell, "m_staminaCost", 25f);
            SetField(m_spell, "m_focusPointsCost", 25);
            SetField(m_spell, "m_fullChargeModifier", 1.4f);
            GetResource("CurrentStamina").Reset(stamina);
            GetResource("CurrentFocusPoints").Reset(focusPoints);

            object canAfford = m_spell.GetType().GetMethod("CanAffordCast")
                .Invoke(m_spell, new object[] { m_player, fullyCharged });

            Assert.That(canAfford, Is.EqualTo(expected));
            Assert.That(GetResource("CurrentStamina").Value, Is.EqualTo(stamina));
            Assert.That(GetResource("CurrentFocusPoints").Value, Is.EqualTo(focusPoints));
        }

        [Test]
        public void InsufficientChargeResourcesCancelWithoutPartialPayment()
        {
            PrepareCharge();
            SetField(m_spells, "m_hasReachedFullSpellCharge", true);
            GetResource("CurrentStamina").Reset(1f);

            InvokeCombat("ReleaseChargingSpell", true);

            Assert.That(GetField(m_spells, "m_currentCastingSpell"), Is.Null);
            Assert.That(GetResource("CurrentStamina").Value, Is.EqualTo(1f));
            Assert.That(GetResource("CurrentFocusPoints").Value, Is.EqualTo(100f));
        }

        [Test]
        public void ReleaseKeepsCommittedSpellAndCatalystAfterInventoryChanges()
        {
            PrepareCharge();
            Component inventory = m_root.GetComponent("PlayerInventoryManager");
            SetField(inventory, "m_currentSpell", null);

            object resolvedSpell = m_spells.GetType().GetMethod("ResolveCurrentSpell",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(m_spells, null);
            object resolvedCatalyst = m_spells.GetType().GetMethod("ResolveCasterWeapon",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(m_spells, new object[] { true });

            Assert.That(resolvedSpell, Is.SameAs(m_spell));
            Assert.That(resolvedCatalyst, Is.SameAs(m_catalyst));
        }

        private void PrepareCharge()
        {
            SetField(m_spells, "m_currentCastingSpell", m_spell);
            SetField(m_spells, "m_currentCasterWeapon", m_catalyst);
            SetField(m_spells, "m_isCastingRightHandSpell", true);
            SetField(m_spells, "m_spellChargeStartTime", Time.time);
        }

        private NetworkVariable<float> GetResource(string name)
        {
            return (NetworkVariable<float>)m_network.GetType().GetField(name)
                .GetValue(m_network);
        }

        private void InvokeCombat(string name, params object[] arguments)
        {
            m_combat.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(m_combat, arguments);
        }

        private static object GetField(object target, string name)
        {
            return FindField(target.GetType(), name).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FindField(target.GetType(), name).SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(name);
        }
    }
}
