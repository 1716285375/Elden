using System;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class ModelAndWeaponMotionTests
    {
        private const string k_Variants = "Assets/_Game/Prefabs/Characters/AI/Variants";
        private const string k_Controller = "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Runtime/Humanoid Runtime.controller";

        [TestCase(190)]
        [TestCase(191)]
        [TestCase(249)]
        public void SkeletonRightWristVerticesFollowTheLowerArm(int vertex)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_Variants + "/Skeleton_00_Unarmed Enemy.prefab");
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            SkinnedMeshRenderer renderer = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(item => item.enabled && item.sharedMesh.name == "c3510_0");
            BoneWeight weight = renderer.sharedMesh.boneWeights[vertex];
            Transform expected = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Assert.That(renderer.bones[weight.boneIndex0], Is.EqualTo(expected),
                "Original R_Forearm2 wrist vertices must bend with the forearm, not the upper arm.");
            Assert.That(weight.weight0, Is.GreaterThan(0.95f));
        }

        [Test]
        public void ModelVariantsHaveTheirOwnValidRigAndNetworkRegistration()
        {
            var prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset");
            string[] assets = AssetDatabase.FindAssets("t:Prefab", new[] { k_Variants });
            Assert.That(assets.Length, Is.EqualTo(7));
            foreach (string guid in assets)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Assert.That(animators.Length, Is.EqualTo(1), prefab.name);
                Animator animator = animators[0];
                Assert.That(animator.avatar.isHuman && animator.avatar.isValid, Is.True, prefab.name);
                const string avatarFolder = "Assets/_Game/Art/Characters/EldenRing/";
                Assert.That(AssetDatabase.GetAssetPath(animator.avatar), Does.StartWith(avatarFolder),
                    prefab.name + " must use a rebuilt Avatar for its actual hierarchy.");
                Assert.That(prefabs.Contains(prefab), Is.True, prefab.name);
                Assert.That(animator.GetComponent("AICharacterAnimatorManager"), Is.Not.Null, prefab.name);
                Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer =>
                    renderer.sharedMesh != null && renderer.enabled), Is.True, prefab.name);
                Component ai = prefab.GetComponent("AICharacterManager");
                Assert.That(new SerializedObject(ai).FindProperty("m_animator").objectReferenceValue, Is.EqualTo(animator));
            }
        }

        [TestCase("Skeleton_00_Unarmed Enemy", "c3510", 2)]
        [TestCase("Imp_01 Enemy", "c3080", 2)]
        [TestCase("Grave_Tender_01 Boss", "c3400", 10)]
        [TestCase("Golem_01 Boss", "c4660", 4)]
        [TestCase("Corpse_Ghost_01 Boss", "c3661", 2)]
        [TestCase("Bell_Keeper_01 Boss", "c3100", 21)]
        [TestCase("Durk_01 Boss", "c2500", 13)]
        public void VariantsUseTexturedOriginalMeshesAndKeepAttackVolumes(string name, string id, int count)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_Variants + "/" + name + ".prefab");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.enabled).ToArray();
            Assert.That(renderers.Length, Is.EqualTo(count));
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMesh),
                    Does.StartWith("Assets/_Game/Art/Characters/EldenRing/" + id));
                Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null);
                Assert.That(renderer.updateWhenOffscreen, Is.False, "Offscreen mesh updates should be culled.");
                Assert.That(renderer.localBounds.size.magnitude, Is.GreaterThan(0f));
                Assert.That(renderer.bones.All(bone => bone != null), Is.True);
                Assert.That(renderer.bones.Length, Is.EqualTo(renderer.sharedMesh.bindposes.Length));
            }
            var attacks = prefab.GetComponentsInChildren<Collider>(true)
                .Where(collider => collider.name.Contains("Hand Damage")).ToArray();
            Assert.That(attacks.Length, Is.EqualTo(2));
            Assert.That(attacks.All(collider => !collider.enabled), Is.True);
            Assert.That(attacks.All(collider => collider.transform.localScale.x >= 0f &&
                collider.transform.localScale.y >= 0f && collider.transform.localScale.z >= 0f), Is.True,
                "Attack boxes must not rely on Unity correcting negative scales.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            var baked = new Mesh();
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                animator.fireEvents = false;
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                    "Gameplay bones must continue updating for offscreen attacks.");
                animator.Rebind();
                var vertices = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.enabled).SelectMany(renderer =>
                    {
                        Mesh mesh = renderer.sharedMesh;
                        Matrix4x4[] matrices = renderer.bones.Select((bone, index) =>
                            bone.localToWorldMatrix * mesh.bindposes[index]).ToArray();
                        BoneWeight[] weights = mesh.boneWeights;
                        return mesh.vertices.Select((vertex, index) =>
                            matrices[weights[index].boneIndex0].MultiplyPoint3x4(vertex) * weights[index].weight0 +
                            matrices[weights[index].boneIndex1].MultiplyPoint3x4(vertex) * weights[index].weight1 +
                            matrices[weights[index].boneIndex2].MultiplyPoint3x4(vertex) * weights[index].weight2 +
                            matrices[weights[index].boneIndex3].MultiplyPoint3x4(vertex) * weights[index].weight3).ToArray();
                    }).ToArray();
                float height = vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y);
                float capsuleHeight = instance.GetComponent<CharacterController>().height * instance.transform.lossyScale.y;
                Assert.That(height, Is.EqualTo(capsuleHeight).Within(0.01f), name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [TestCase("OffHand_Attack_01")]
        [TestCase("OffHand_Attack_02")]
        [TestCase("OffHand_JumpLight")]
        public void LeftHandStatesContainMotionAndBalancedHitWindows(string stateName)
        {
            AnimatorStateMachine machine = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller).layers
                .Single(layer => layer.name == "Action Override").stateMachine;
            AnimatorState state = machine.states.Single(child => child.state.name == stateName).state;
            var clip = state.motion as AnimationClip;
            Assert.That(clip != null && clip.humanMotion, Is.True);
            string[] events = AnimationUtility.GetAnimationEvents(clip).Select(item => item.functionName).ToArray();
            Assert.That(events.Count(name => name == "OpenDamageCollider"), Is.EqualTo(1));
            Assert.That(events.Count(name => name == "CloseDamageCollider"), Is.EqualTo(1));
            Assert.That(events, Does.Not.Contain("ActivateMainHandWeaponTrail"));
            Assert.That(state.transitions.Length, Is.GreaterThan(0), "Attacks must return to locomotion.");
        }

        [TestCase("Straight Sword Animator.overrideController", "straight_sword")]
        [TestCase("Broadsword.overrideController", "ultra")]
        public void EquippedWeaponsUseTheirOwnAerialMotions(string file, string prefix)
        {
            string path = file.StartsWith("Broadsword")
                ? "Assets/_Game/Data/Animations/Broadsword/" + file
                : "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/" + file;
            var weapon = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            AnimatorStateMachine action = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller).layers
                .Single(layer => layer.name == "Action Override").stateMachine;
            string[] states = { "MainJumpLightAttack", "MainJumpHeavyStart", "MainJumpHeavyIdle", "MainJumpHeavyEnd",
                "TwoHandJumpLightAttack", "TwoHandJumpHeavyStart", "TwoHandJumpHeavyIdle", "TwoHandJumpHeavyEnd" };
            foreach (string name in states)
            {
                var original = (AnimationClip)action.states.Single(child => child.state.name == name).state.motion;
                AnimationClip motion = weapon[original];
                Assert.That(motion, Is.Not.Null, name);
                Assert.That(motion.humanMotion, Is.True, name);
                Assert.That(motion.name, Does.StartWith(prefix), name);
            }
        }

        [Test]
        public void LeftTwoHandGripMirrorsBothAttacksAndLocomotion()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "isTwoHandingLeftWeapon"), Is.True);
            AnimatorState[] states = controller.layers.SelectMany(layer => layer.stateMachine.states)
                .Select(child => child.state).Where(state => state.name.StartsWith("TwoHand") ||
                    state.name.Contains("Two Handed")).ToArray();
            Assert.That(states.Length, Is.GreaterThan(10));
            Assert.That(states.All(state => state.mirrorParameterActive &&
                state.mirrorParameter == "isTwoHandingLeftWeapon"), Is.True);
        }
    }
}
