using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        [MenuItem("Tools/ZZ/Elden Ring/Verify Equipped Original Motion Poses")]
        public static void VerifyEquippedOriginalMotionPoses()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            var report = new StringBuilder();
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                animator.Rebind();
                player.GetComponent<PlayerSkinnedEquipment>().Refresh(true, 4, 5, 6, 7);
                foreach (var pair in new[]
                {
                    (WeaponModelSlot.RightHandSlot, "Straight Sword"),
                    (WeaponModelSlot.LeftHandShieldSlot, "Medium Shield")
                })
                {
                    Transform slot = player.GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                        .Single(item => item.WeaponModelSlot == pair.Item1).transform;
                    GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/" + pair.Item2 + ".prefab");
                    GameObject weapon = UnityEngine.Object.Instantiate(source, slot, false);
                    weapon.transform.localPosition = Vector3.zero;
                    weapon.transform.localRotation = Quaternion.identity;
                    weapon.transform.localScale = Vector3.one;
                    weapon.GetComponent<OriginalWeaponAttachment>()?.Apply(slot.GetComponent<WeaponModelInstantiationSlot>(),
                        player.GetComponent<PlayerManager>());
                    if ((!slot.parent.name.EndsWith("_Weapon") && !slot.parent.name.EndsWith("_Shield")) ||
                        Quaternion.Angle(slot.localRotation, Quaternion.identity) > 0.01f)
                    {
                        throw new InvalidOperationException("Legacy weapon grip survived: " + pair.Item2);
                    }
                    report.AppendLine(pair.Item2 + ": original weapon bone, identity attachment.");
                }
                AnimationMode.StartAnimationMode();
                foreach (string name in new[] { "a000_000000", "a030_030000", "a030_030010", "a692_040040", "a000_000100" })
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        k_ActionOutput + "/Humanoid/Clips/" + name + ".anim");
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * 0.45f);
                    AnimationMode.EndSampling();
                    string filename = name + "-equipped.png";
                    Capture(player, filename, k_ModularRoot, includeAttachments: true);
                    File.Copy(Path.GetDirectoryName(k_Report) + "/" + filename,
                        "Docs/PresentationRepair/" + filename, true);
                    report.AppendLine(name + ": equipped pose captured at 45%. Timing and hit behavior require runtime checks.");
                }
                File.WriteAllText("Docs/PresentationRepair/equipped-poses.txt", report.ToString());
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                PrefabUtility.UnloadPrefabContents(player);
            }
        }
    }
}
