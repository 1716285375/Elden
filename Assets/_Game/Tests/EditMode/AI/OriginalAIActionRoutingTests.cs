using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZZ.Tests
{
    public sealed class OriginalAIActionRoutingTests
    {
        [Test]
        public void BossIdentityIsConfiguredWithoutWritingUnspawnedNetworkVariables()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Data/AI/OriginalCreatures/c3970/c3970 Gameplay.prefab");
            try
            {
                Type type = RuntimeType("ZZ.BossCharacterManager");
                Component boss = root.GetComponent(type);
                type.GetMethod("ConfigureEncounterIdentity").Invoke(boss, new object[] { 31030800, "法姆·亚兹拉的兽人" });
                Assert.That(type.GetProperty("BossID").GetValue(boss), Is.EqualTo(31030800));
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(42u, "LightAttack01", true)]
        [TestCase(43u, "LightAttack01", false)]
        [TestCase(42u, "HeavyAttack01", false)]
        public void ReplicatedSourceAttackMustBelongToThisActorAndMatchItsCommand(uint id, string command, bool expected)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            ScriptableObject definition = ScriptableObject.CreateInstance(RuntimeType("ZZ.AttackDefinition"));
            ScriptableObject action = ScriptableObject.CreateInstance(RuntimeType("ZZ.AICharacterAttackAction"));
            try
            {
                var definitionData = new SerializedObject(definition);
                definitionData.FindProperty("m_attackId").longValue = 42;
                definitionData.FindProperty("m_attackType").intValue = 0;
                definitionData.ApplyModifiedPropertiesWithoutUndo();
                var actionData = new SerializedObject(action);
                actionData.FindProperty("m_attackDefinition").objectReferenceValue = definition;
                actionData.ApplyModifiedPropertiesWithoutUndo();
                Component ai = root.GetComponent(RuntimeType("ZZ.AICharacterManager"));
                var aiData = new SerializedObject(ai);
                SerializedProperty actions = aiData.FindProperty("m_combatStanceAttacks");
                actions.arraySize = 1;
                actions.GetArrayElementAtIndex(0).objectReferenceValue = action;
                aiData.FindProperty("m_defaultAttackAction").objectReferenceValue = null;
                aiData.ApplyModifiedPropertiesWithoutUndo();
                Type combatType = RuntimeType("ZZ.AICharacterCombatManager");
                Component combat = root.GetComponent(combatType);
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                combatType.GetField("m_aiCharacter", flags).SetValue(combat, ai);
                combatType.GetField("m_currentAttackAction", flags).SetValue(combat, action);
                object type = Enum.Parse(RuntimeType("ZZ.AttackType"), command);
                Assert.That(combatType.GetMethod("TrySelectReplicatedAttack", flags)
                    .Invoke(combat, new[] { (object)id, type }), Is.EqualTo(expected));
                Assert.That(combatType.GetField("m_currentAttackAction", flags).GetValue(combat), Is.SameAs(action));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                Object.DestroyImmediate(action);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(-1f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(2f, 1f)]
        public void OriginalRootTrackUsesSourceEndpointsAndInterpolation(float progress, float fraction)
        {
            MethodInfo sample = RuntimeType("ZZ.OriginalCreatureCombat").GetMethod("SampleRoot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Vector4[] frames = { new(1f, 2f, 3f, 0.2f), new(3f, 6f, 7f, 0.6f) };
            var actual = (Vector4)sample.Invoke(null, new object[] { frames, progress });
            Assert.That((actual - Vector4.Lerp(frames[0], frames[1], fraction)).sqrMagnitude, Is.LessThan(0.0000001f));
            Assert.That((Vector4)sample.Invoke(null, new object[] { Array.Empty<Vector4>(), progress }),
                Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void OriginalCreaturesSharingSourceDataKeepIndependentBoneBindings()
        {
            var first = new GameObject("First original creature");
            var second = new GameObject("Second original creature");
            first.SetActive(false);
            second.SetActive(false);
            try
            {
                Type type = RuntimeType("ZZ.OriginalCreatureCombat");
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                Type anchorType = type.GetNestedType("SourceAnchor", BindingFlags.NonPublic);
                object anchor = Activator.CreateInstance(anchorType, true);
                anchorType.GetField("Bone").SetValue(anchor, "Head");
                anchorType.GetField("Position").SetValue(anchor, new[] { 0.1f, 0.2f, 0.3f });
                Component firstCombat = first.AddComponent(type);
                Component secondCombat = second.AddComponent(type);
                first.transform.position = Vector3.right * 5f;
                second.transform.position = Vector3.left * 7f;
                Transform firstBone = new GameObject("Head").transform;
                Transform secondBone = new GameObject("Head").transform;
                firstBone.SetParent(first.transform, false);
                secondBone.SetParent(second.transform, false);
                ((System.Collections.IDictionary)type.GetField("m_bones", flags).GetValue(firstCombat)).Add("Head", firstBone);
                ((System.Collections.IDictionary)type.GetField("m_bones", flags).GetValue(secondCombat)).Add("Head", secondBone);
                type.GetMethod("Bind", flags).Invoke(firstCombat, new[] { anchor });
                type.GetMethod("Bind", flags).Invoke(secondCombat, new[] { anchor });
                var firstPoint = (Vector3)type.GetMethod("Point", flags).Invoke(firstCombat, new[] { anchor });
                var secondPoint = (Vector3)type.GetMethod("Point", flags).Invoke(secondCombat, new[] { anchor });
                Assert.That(Vector3.Distance(firstPoint, new Vector3(5.1f, 0.2f, 0.3f)), Is.LessThan(0.00001f));
                Assert.That(Vector3.Distance(secondPoint, new Vector3(-6.9f, 0.2f, 0.3f)), Is.LessThan(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void AIHitRegistryDeduplicatesEachStrikeButAllowsTheNextStrikeInTheSameAction()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            try
            {
                Type combatType = RuntimeType("ZZ.AICharacterCombatManager");
                object registry = combatType.GetField("m_charactersDamaged", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(root.GetComponent(combatType));
                MethodInfo register = registry.GetType().GetMethod("TryRegister");
                object target = root.GetComponent(RuntimeType("ZZ.CharacterManager"));
                Assert.That(register.Invoke(registry, new[] { target, 0, 0 }), Is.True);
                Assert.That(register.Invoke(registry, new[] { target, 0, 0 }), Is.False);
                Assert.That(register.Invoke(registry, new[] { target, 1, 0 }), Is.True);
                registry.GetType().GetMethod("Clear").Invoke(registry, null);
                Assert.That(register.Invoke(registry, new[] { target, 0, 0 }), Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SelectedAIActionUsesItsSourceStateWithoutReusingAStaleDifferentAttack(bool matchesAttack)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            ScriptableObject definition = ScriptableObject.CreateInstance(RuntimeType("ZZ.AttackDefinition"));
            ScriptableObject action = ScriptableObject.CreateInstance(RuntimeType("ZZ.AICharacterAttackAction"));
            try
            {
                Type animatorType = RuntimeType("ZZ.CharacterAnimatorManager");
                Type combatType = RuntimeType("ZZ.AICharacterCombatManager");
                Type characterType = RuntimeType("ZZ.CharacterManager");
                Component character = root.GetComponent(characterType);
                Component combat = root.GetComponent(combatType);
                Component animator = root.GetComponentInChildren(animatorType, true);
                Type attackType = RuntimeType("ZZ.AttackType");
                object requestedAttack = Enum.Parse(attackType, "LightAttack01");
                var serializedDefinition = new SerializedObject(definition);
                serializedDefinition.FindProperty("m_animationState").stringValue = "Action Override.a000_003000";
                serializedDefinition.FindProperty("m_attackType").intValue = Convert.ToInt32(
                    Enum.Parse(attackType, matchesAttack ? "LightAttack01" : "LightAttack02"));
                serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
                var serializedAction = new SerializedObject(action);
                serializedAction.FindProperty("m_attackDefinition").objectReferenceValue = definition;
                serializedAction.ApplyModifiedPropertiesWithoutUndo();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                combatType.GetField("m_currentAttackAction", flags).SetValue(combat, action);
                characterType.GetField("m_characterCombatManager", flags).SetValue(character, combat);
                animatorType.GetField("m_characterManager", flags).SetValue(animator, character);
                int actual = (int)animatorType.GetMethod("ResolveAttackStateHash", flags)
                    .Invoke(animator, new[] { requestedAttack, null });
                Assert.That(actual, Is.EqualTo(Animator.StringToHash(matchesAttack
                    ? "Action Override.a000_003000" : "Action Override.Attack_01")));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                Object.DestroyImmediate(action);
                Object.DestroyImmediate(definition);
            }
        }

        private static Type RuntimeType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
                .First(type => type != null);
        }
    }
}
