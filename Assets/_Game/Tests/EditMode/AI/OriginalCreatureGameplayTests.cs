using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class OriginalCreatureGameplayTests
    {
        [TestCase("c3970", 446, 2f, 5.5f, "a000_030003", "a000_020003")]
        [TestCase("c4070", 371, 1.3f, 6.55f, "a000_030000", "a000_020000")]
        [TestCase("c4071", 372, 1.3f, 6.55f, "a000_030000", "a000_020000")]
        public void OriginalGameplayPrefabUsesItsOwnRigMotionsAndSourceLocomotion(string actor, int boneCount,
            float walkSpeed, float runSpeed, string sleepingState, string wakingState)
        {
            string folder = "Assets/_Game/Data/AI/OriginalCreatures/" + actor;
            GameObject root = PrefabUtility.LoadPrefabContents(folder + "/" + actor + " Gameplay.prefab");
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                Assert.That(animator.avatar.isValid, Is.True);
                Assert.That(animator.avatar.isHuman, Is.False);
                Assert.That(root.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length, Is.EqualTo(boneCount));
                Assert.That(Quaternion.Angle(animator.transform.localRotation, Quaternion.Euler(0f, 180f, 0f)),
                    Is.LessThan(0.001f));
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips.Distinct())
                {
                    Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith(
                        "Assets/_Game/Art/Characters/EldenRing/OriginalCreatures/" + actor + "/Animations/"));
                }
                Type aiType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("ZZ.AICharacterManager"))
                    .First(type => type != null);
                var ai = new SerializedObject(root.GetComponent(aiType));
                Assert.That(ai.FindProperty("m_walkSpeed").floatValue, Is.EqualTo(walkSpeed).Within(0.01f));
                Assert.That(ai.FindProperty("m_runSpeed").floatValue, Is.EqualTo(runSpeed).Within(0.01f));
                AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;
                AnimatorStateMachine machine = controller.layers.Single(layer => layer.name == "Action Override").stateMachine;
                string[] states = machine.states.Select(state => state.state.name).ToArray();
                Assert.That(states, Does.Contain(sleepingState));
                Assert.That(states, Does.Contain(wakingState));
                SerializedProperty attacks = ai.FindProperty("m_combatStanceAttacks");
                Assert.That(attacks.arraySize, Is.GreaterThan(2));
                for (int index = 0; index < attacks.arraySize; index++)
                {
                    var action = new SerializedObject(attacks.GetArrayElementAtIndex(index).objectReferenceValue);
                    var definition = new SerializedObject(action.FindProperty("m_attackDefinition").objectReferenceValue);
                    string state = definition.FindProperty("m_animationState").stringValue;
                    Assert.That(states, Does.Contain(state.Substring("Action Override.".Length)));
                    Assert.That(definition.FindProperty("m_attackId").longValue, Is.GreaterThan(0));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
