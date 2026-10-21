using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class MixamoAnimationImportTests
    {
        [Test]
        public void DownloadedSwordLibraryContains49ValidHumanoidMotions()
        {
            string[] paths = Directory.GetFiles(
                "Assets/_Game/Art/Characters/Shared/Humanoid/Mixamo/SwordAndShield", "*.fbx");
            Assert.That(paths.Length, Is.EqualTo(49));
            foreach (string path in paths)
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/'));
                Avatar avatar = assets.OfType<Avatar>().Single();
                Assert.That(avatar.isValid && avatar.isHuman, Is.True, path);
                AnimationClip clip = assets.OfType<AnimationClip>().Single(item => !item.name.StartsWith("__preview__"));
                Assert.That(clip.humanMotion && clip.length > 0f, Is.True, path);
            }
        }

        [Test]
        public void BroadswordMixamoIdleCanRetargetToTheExistingPlayerAvatar()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                "Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController");
            AnimationClip clip = weapon["unarmed_main_idle_01"];
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Is.EqualTo("Mixamo Broadsword Idle"));
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            var controller = new AnimatorController();
            try
            {
                Animator animator = root.GetComponentsInChildren<Animator>(true).First(item => item.avatar != null);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
                controller.AddLayer("Validation");
                AnimatorState state = controller.AddMotion(clip);
                animator.runtimeAnimatorController = controller;
                animator.enabled = true;
                animator.Rebind();
                animator.Play(state.name, 0, 0f);
                for (int sample = 0; sample < 15; sample++)
                {
                    animator.Update(1f / 30f);
                }
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Assert.That(head, Is.Not.Null);
                Assert.That(float.IsNaN(head.position.sqrMagnitude) || float.IsInfinity(head.position.sqrMagnitude), Is.False);
                Assert.That(Vector3.Distance(head.position, animator.GetBoneTransform(HumanBodyBones.Hips).position),
                    Is.GreaterThan(0.1f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                Object.DestroyImmediate(controller);
            }
        }
    }
}
