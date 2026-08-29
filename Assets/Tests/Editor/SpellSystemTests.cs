using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class SpellSystemTests
    {
        private const string k_DatabasePrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_FireballPath =
            "Assets/_Game/Data/Items/Spells/Fireball.asset";
        private const string k_CatalystPath =
            "Assets/_Game/Data/Items/Weapons/Catalysts/Incantation Catalyst.asset";
        private const string k_FireballPrefabPath =
            "Assets/_Game/Prefabs/Abilities/Fireball.prefab";
        private const string k_InputAssetPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_AnimatorPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";

        [Test]
        public void FireballUsesRegisteredIncantationDataAndChargeValues()
        {
            UnityEngine.Object fireball = AssetDatabase.LoadMainAssetAtPath(
                k_FireballPath);
            UnityEngine.Object catalyst = AssetDatabase.LoadMainAssetAtPath(
                k_CatalystPath);
            GameObject root = PrefabUtility.LoadPrefabContents(k_DatabasePrefabPath);
            try
            {
                Component database = GetComponentByName(root, "WorldItemDatabase");
                SerializedObject serializedFireball = new SerializedObject(fireball);
                SerializedObject serializedCatalyst = new SerializedObject(catalyst);
                SerializedObject serializedDatabase = new SerializedObject(database);
                Assert.That(fireball, Is.Not.Null);
                Assert.That(catalyst, Is.Not.Null);
                Assert.That(
                    serializedFireball.FindProperty("m_spellClass").enumValueIndex,
                    Is.EqualTo(0));
                Assert.That(
                    serializedFireball.FindProperty("m_fireDamage").floatValue,
                    Is.EqualTo(150f));
                Assert.That(
                    serializedFireball.FindProperty("m_fullChargeModifier").floatValue,
                    Is.EqualTo(1.4f));
                Assert.That(
                    serializedCatalyst.FindProperty("m_spellClass").enumValueIndex,
                    Is.EqualTo(0));
                Assert.That(
                    serializedCatalyst.FindProperty("m_rightHandAction")
                        .objectReferenceValue.GetType().Name,
                    Is.EqualTo("CastIncantationAction"));
                Assert.That(
                    serializedCatalyst.FindProperty("m_leftHandAction")
                        .objectReferenceValue.GetType().Name,
                    Is.EqualTo("CastIncantationAction"));
                SerializedProperty spells = serializedDatabase.FindProperty("m_spells");
                Assert.That(
                    Enumerable.Range(0, spells.arraySize).Count(index =>
                        spells.GetArrayElementAtIndex(index).objectReferenceValue ==
                            fireball),
                    Is.EqualTo(1));
                int fireballID = serializedFireball.FindProperty("m_itemID").intValue;
                Assert.That(
                    database.GetType().GetMethod("GetSpellByID")
                        ?.Invoke(database, new object[] { fireballID }),
                    Is.SameAs(fireball));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void PlayerReplicatesOneSpellAndIndependentHandChargeStates()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Component network = root.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "PlayerNetworkManager");
                Component inventory = GetComponentByName(
                    root,
                    "PlayerInventoryManager");
                UnityEngine.Object fireball = AssetDatabase.LoadMainAssetAtPath(
                    k_FireballPath);
                UnityEngine.Object catalyst = AssetDatabase.LoadMainAssetAtPath(
                    k_CatalystPath);
                SerializedObject serializedInventory = new SerializedObject(inventory);
                AssertOwnerWrittenNetworkVariable(network, "CurrentSpellID");
                AssertOwnerWrittenNetworkVariable(
                    network,
                    "IsChargingRightSpell");
                AssertOwnerWrittenNetworkVariable(
                    network,
                    "IsChargingLeftSpell");
                Assert.That(
                    serializedInventory.FindProperty("m_startingSpell")
                        .objectReferenceValue,
                    Is.SameAs(fireball));
                Assert.That(
                    serializedInventory.FindProperty("m_weaponsInRightHandSlots")
                        .GetArrayElementAtIndex(2).objectReferenceValue,
                    Is.SameAs(catalyst));
                Assert.That(
                    serializedInventory.FindProperty("m_weaponsInLeftHandSlots")
                        .GetArrayElementAtIndex(2).objectReferenceValue,
                    Is.SameAs(catalyst));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void FireballSeparatesTravelAndDamageColliderResponsibilities()
        {
            GameObject fireball = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_FireballPrefabPath);
            Rigidbody travelRigidbody = fireball.GetComponent<Rigidbody>();
            SphereCollider travelCollider = fireball.GetComponent<SphereCollider>();
            Component damageCollider = fireball.GetComponentsInChildren<Component>(true)
                .Single(component =>
                    component.GetType().Name == "SpellProjectileDamageCollider");
            Rigidbody damageRigidbody = damageCollider.GetComponent<Rigidbody>();
            Assert.That(GetComponentByName(fireball, "FireballManager"), Is.Not.Null);
            Assert.That(fireball.layer, Is.EqualTo(LayerMask.NameToLayer("Projectile")));
            Assert.That(travelRigidbody.useGravity, Is.False);
            Assert.That(travelRigidbody.isKinematic, Is.False);
            Assert.That(travelCollider.isTrigger, Is.False);
            Assert.That(
                damageCollider.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("Damage Collider")));
            Assert.That(damageCollider.GetComponent<Collider>().isTrigger, Is.True);
            Assert.That(damageRigidbody.isKinematic, Is.True);
        }

        [Test]
        public void SpellAnimatorContainsBothHandChargeAndReleaseBranches()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_AnimatorPath);
            string[] parameters = controller.parameters
                .Select(parameter => parameter.name)
                .ToArray();
            AnimatorState[] states = controller.layers
                .Single(layer => layer.name == "Action Override")
                .stateMachine.states
                .Select(child => child.state)
                .ToArray();
            Assert.That(parameters, Does.Contain("isChargingRightSpell"));
            Assert.That(parameters, Does.Contain("isChargingLeftSpell"));
            Assert.That(parameters, Does.Contain("isSpellFullyCharged"));
            Assert.That(states.Select(state => state.name), Does.Contain(
                "Cast_Spell_Right_Charge"));
            Assert.That(states.Select(state => state.name), Does.Contain(
                "Cast_Spell_Left_Charge"));
            Assert.That(states.Select(state => state.name), Does.Contain(
                "Cast_Spell_Right_Release_Full"));
            Assert.That(states.Select(state => state.name), Does.Contain(
                "Cast_Spell_Left_Release_Full"));
            Assert.That(
                states.Where(state => state.name.Contains("_Release"))
                    .All(state => AnimationUtility.GetAnimationEvents(
                            state.motion as AnimationClip)
                        .Any(animationEvent =>
                            animationEvent.functionName == "CompleteSpellCast")),
                Is.True);
        }

        [Test]
        public void CatalystInputsUseHoldForGamepadAndKeyboardMouse()
        {
            string inputJson = File.ReadAllText(k_InputAssetPath);
            const string k_HoldBindingPattern =
                "\\\"interactions\\\": \\\"Hold\\(duration=0\\.05\\)\\\"" +
                "[\\s\\S]*?\\\"action\\\": \\\"(?:RB|LB)\\\"";
            Assert.That(
                Regex.Matches(inputJson, k_HoldBindingPattern).Count,
                Is.EqualTo(4));
        }

        private static void AssertOwnerWrittenNetworkVariable(
            Component network,
            string propertyName)
        {
            object networkVariable = network.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(network);
            object writePermission = networkVariable?.GetType()
                .GetProperty("WritePerm")
                ?.GetValue(networkVariable);
            Assert.That(writePermission?.ToString(), Is.EqualTo("Owner"));
        }

        private static Component GetComponentByName(
            GameObject root,
            string componentName)
        {
            return root.GetComponents<Component>()
                .SingleOrDefault(component =>
                    component.GetType().Name == componentName);
        }
    }
}
