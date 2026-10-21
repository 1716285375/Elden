using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        [MenuItem("Tools/ZZ/Elden Ring/Audit Original Weapon Coordinate Frames")]
        public static void AuditOriginalWeaponCoordinateFrames()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before auditing source poses.");
            }
            var records = new List<PoseAuditRecord>();
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                animator.Rebind();
                Transform visual = animator.transform.Find(k_VisualName);
                var equipment = new Dictionary<string, MeshFilter>();
                foreach (var pair in new[] { ("R_", "Straight Sword", WeaponModelSlot.RightHandSlot),
                    ("L_", "Medium Shield", WeaponModelSlot.LeftHandShieldSlot) })
                {
                    Transform slot = player.GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                        .Single(item => item.WeaponModelSlot == pair.Item3).transform;
                    GameObject model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/" + pair.Item2 + ".prefab"), slot, false);
                    model.GetComponent<OriginalWeaponAttachment>()?.Apply(slot.GetComponent<WeaponModelInstantiationSlot>(),
                        player.GetComponent<PlayerManager>());
                    equipment.Add(pair.Item1, model.GetComponentsInChildren<MeshFilter>(true)
                        .Single(item => item.GetComponent<MeshRenderer>()?.enabled == true));
                }
                AnimationMode.StartAnimationMode();
                foreach (string name in new[] { "a000_000000", "a030_030000", "a030_030010", "a692_040040", "a000_000100" })
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        k_ActionOutput + "/Humanoid/Clips/" + name + ".anim");
                    int lastFrame = Mathf.RoundToInt(clip.length * 30f);
                    for (int frame = 0; frame <= lastFrame; frame++)
                    {
                        float time = frame / 30f;
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
                        AnimationMode.EndSampling();
                        foreach (string side in new[] { "L_", "R_" })
                        {
                            Transform hand = animator.GetBoneTransform(side == "L_" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                            Transform weapon = player.GetComponentsInChildren<Transform>(true).Single(item => item.name == "ER_" + side + "Weapon");
                            MeshFilter mesh = equipment[side];
                            Vector3[] vertices = mesh.sharedMesh.vertices;
                            int[] indices = Enumerable.Range(0, 12).Select(index => index * (vertices.Length - 1) / 11).ToArray();
                            records.Add(new PoseAuditRecord
                            {
                                Name = name, Phase = time / clip.length, Side = side,
                                Hand = Flatten(visual.worldToLocalMatrix * hand.localToWorldMatrix),
                                Weapon = Flatten(visual.worldToLocalMatrix * weapon.localToWorldMatrix),
                                Visual = Flatten(animator.transform.worldToLocalMatrix * visual.localToWorldMatrix),
                                Equipment = Flatten(visual.worldToLocalMatrix * mesh.transform.localToWorldMatrix),
                                Indices = indices,
                                Vertices = indices.SelectMany(index => new[] { vertices[index].x, vertices[index].y, vertices[index].z }).ToArray()
                            });
                        }
                    }
                }
                Directory.CreateDirectory("Docs/WeaponCoordinateRepair");
                File.WriteAllText("Docs/WeaponCoordinateRepair/actual-poses.json",
                    JsonUtility.ToJson(new PoseAuditDocument
                    {
                        Records = records.ToArray(),
                        Meshes = equipment.Select(pair => new PoseAuditMesh
                        {
                            Side = pair.Key,
                            Vertices = pair.Value.sharedMesh.vertices.SelectMany(vertex =>
                                new[] { vertex.x, vertex.y, vertex.z }).ToArray()
                        }).ToArray()
                    }, true));
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static float[] Flatten(Matrix4x4 matrix)
        {
            return Enumerable.Range(0, 16).Select(index => matrix[index / 4, index % 4]).ToArray();
        }

        [Serializable]
        private sealed class PoseAuditDocument
        {
            public PoseAuditRecord[] Records;
            public PoseAuditMesh[] Meshes;
        }

        [Serializable]
        private sealed class PoseAuditMesh
        {
            public string Side;
            public float[] Vertices;
        }

        [Serializable]
        private sealed class PoseAuditRecord
        {
            public string Name;
            public string Side;
            public float Phase;
            public float[] Hand;
            public float[] Weapon;
            public float[] Visual;
            public float[] Equipment;
            public int[] Indices;
            public float[] Vertices;
        }
    }
}
