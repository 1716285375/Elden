using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class EnemyMovesetTests
    {
        [TestCase("Skeleton_00_Unarmed Enemy")]
        [TestCase("Imp_01 Enemy")]
        [TestCase("Grave_Tender_01 Boss")]
        [TestCase("Golem_01 Boss")]
        [TestCase("Corpse_Ghost_01 Boss")]
        [TestCase("Bell_Keeper_01 Boss")]
        [TestCase("Durk_01 Boss")]
        public void CreatureAttacksResolvePlayableClipsWithPairedHitWindows(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Variants/" + name + ".prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator.runtimeAnimatorController, Is.TypeOf<AnimatorOverrideController>());
                Component ai = instance.GetComponents<Component>().First(component =>
                    component != null && component.GetType().Name == "AICharacterManager");
                var data = new SerializedObject(ai);
                SerializedProperty attacks = data.FindProperty("m_combatStanceAttacks");
                Assert.That(attacks.arraySize, Is.EqualTo(3));
                animator.Rebind();
                int layer = animator.GetLayerIndex("Action Override");
                var ids = new HashSet<long>();
                var usedClips = new HashSet<AnimationClip>();
                for (int index = 0; index < attacks.arraySize; index++)
                {
                    var action = new SerializedObject(attacks.GetArrayElementAtIndex(index).objectReferenceValue);
                    var definition = new SerializedObject(action.FindProperty("m_attackDefinition").objectReferenceValue);
                    Assert.That(ids.Add(definition.FindProperty("m_attackId").longValue), Is.True);
                    string type = definition.FindProperty("m_attackType").enumNames[
                        definition.FindProperty("m_attackType").enumValueIndex];
                    string state = "Action Override." + (type == "LightAttack01" ? "Attack_01" :
                        type == "LightAttack02" ? "Attack_Light_02" : "Attack_02");
                    int hash = Animator.StringToHash(state);
                    Assert.That(animator.HasState(layer, hash), Is.True, state);
                    animator.Play(hash, layer, 0f);
                    animator.Update(0f);
                    AnimationClip clip = animator.GetCurrentAnimatorClipInfo(layer).Single().clip;
                    Assert.That(usedClips.Add(clip), Is.True, "Attacks must use distinct motions.");
                    Assert.That(clip.humanMotion, Is.True);
                    Assert.That(clip.isLooping, Is.False);
                    AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
                    foreach (string hand in new[] { "Left", "Right" })
                    {
                        AnimationEvent[] opens = events.Where(item => item.functionName ==
                            "Open" + hand + "HandDamageCollider").ToArray();
                        AnimationEvent[] closes = events.Where(item => item.functionName ==
                            "Close" + hand + "HandDamageCollider").ToArray();
                        Assert.That(closes.Length, Is.EqualTo(opens.Length), clip.name + ": " + hand);
                        for (int window = 0; window < opens.Length; window++)
                        {
                            Assert.That(closes[window].time, Is.GreaterThan(opens[window].time));
                            Assert.That(closes[window].time, Is.LessThan(clip.length));
                        }
                    }
                    Assert.That(events.Any(item => item.functionName.StartsWith("Open")), Is.True);
                    Assert.That(events.Any(item => item.functionName == "DrainStaminaBasedOnAttack"), Is.False);
                    Assert.That(action.FindProperty("m_recoveryTime").floatValue, Is.GreaterThan(clip.length));
                }
                Component boss = instance.GetComponents<Component>().FirstOrDefault(component =>
                    component != null && component.GetType().Name == "BossCharacterManager");
                if (boss != null)
                {
                    SerializedProperty phases = new SerializedObject(boss).FindProperty("m_phases");
                    Assert.That(phases.arraySize, Is.EqualTo(2));
                    for (int index = 0; index < 2; index++)
                    {
                        var phase = new SerializedObject(phases.GetArrayElementAtIndex(index).objectReferenceValue);
                        Assert.That(phase.FindProperty("m_attacks").arraySize, Is.EqualTo(index == 0 ? 2 : 3));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
