using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        [MenuItem("Tools/ZZ/Elden Ring/Verify Runtime Lock On")]
        public static void VerifyRuntimeLockOn()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            bool isFreshTest = save?.CurrentCharacterData?.CharacterName == "褪色者测试";
            if (!Application.isPlaying || save == null ||
                (save.CurrentCharacterSlot != CharacterSlot.NoSlot && !isFreshTest))
            {
                throw new InvalidOperationException("Use an unsaved host or the freshly created test character.");
            }
            File.WriteAllText("Docs/RigCombatRepair/runtime-lock-on.txt", "RUNNING\n");
            save.StartCoroutine(ObserveRuntimeLockOn());
        }

        private static System.Collections.IEnumerator ObserveRuntimeLockOn()
        {
            PlayerManager player = PlayerUIManager.Instance.LocalPlayer;
            PlayerLockOnManager lockOn = player.GetComponent<PlayerLockOnManager>();
            Camera camera = PlayerCamera.Instance.CameraObject;
            PlayerUIHUDManager hud = PlayerUIManager.Instance.PlayerUIHUDManager;
            VisualElement hudRoot = hud.Toolkit.GetComponent<UIDocument>().rootVisualElement;
            bool wasHudHidden = hudRoot.style.display.value == DisplayStyle.None;
            hud.ShowHUD();
            Vector3 position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 5f)) - Vector3.up * 1.5f;
            GameObject fixture = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab"), position, Quaternion.identity);
            var network = fixture.GetComponent<Unity.Netcode.NetworkObject>();
            try
            {
                foreach (UnityEngine.AI.NavMeshAgent agent in fixture.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())
                {
                    agent.enabled = false;
                }
                var enemy = fixture.GetComponent<AICharacterManager>();
                enemy.enabled = false;
                network.Spawn();
                yield return null;
                lockOn.ClearLockOn();
                Physics.SyncTransforms();
                lockOn.HandleLockOn();
                if (lockOn.CurrentTarget != enemy)
                {
                    string detail = $"owner={player.IsOwner}; dead={player.IsDead}; enemyDead={enemy.IsDead}; " +
                        $"spawned={enemy.IsSpawned}; target={lockOn.CurrentTarget?.name}; " +
                        $"player={player.transform.position}; enemy={enemy.transform.position}; " +
                        $"viewport={camera.WorldToViewportPoint(enemy.LockOnTransform.position)}; " +
                        $"overlaps={Physics.OverlapSphere(player.transform.position, 20f, 1 << 10).Length}";
                    File.WriteAllText("Docs/RigCombatRepair/runtime-lock-on.txt", "FAIL: " + detail);
                    throw new InvalidOperationException("Runtime lock-on did not acquire the visible enemy: " + detail);
                }
                yield return null;
                VisualElement reticle = hudRoot.Q("lock-on-reticle");
                if (reticle == null || reticle.resolvedStyle.display == DisplayStyle.None ||
                    reticle.worldBound.width <= 0f)
                {
                    throw new InvalidOperationException("Lock-on target marker is not visible on the active HUD.");
                }
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/RigCombatRepair/runtime-lock-on.png"));
                yield return null;
                lockOn.HandleLockOn();
                if (lockOn.IsLockedOn)
                {
                    throw new InvalidOperationException("Lock-on toggle did not release the enemy.");
                }
                File.WriteAllText("Docs/RigCombatRepair/runtime-lock-on.txt",
                    "PASS: spawned host enemy acquired; HUD marker visible and laid out; second press releases.\n");
            }
            finally
            {
                lockOn.ClearLockOn();
                if (wasHudHidden)
                {
                    hud.HideHUD();
                }
                if (network.IsSpawned)
                {
                    network.Despawn();
                }
                else
                {
                    Object.Destroy(fixture);
                }
            }
        }

        private static void CompletePlayerFingerMapping(Animator animator, Transform visual, ExportDocument outfit,
            Dictionary<string, Transform> targets, ref Matrix4x4[] bindposes)
        {
            HumanDescription description = animator.avatar.humanDescription;
            var human = description.human.ToList();
            var poses = bindposes.ToList();
            var sources = BuildSourceBones(outfit);
            Transform[] bones = visual.GetComponentsInChildren<Transform>(true);
            foreach (string side in new[] { "Left", "Right" })
            {
                string prefix = side == "Left" ? "L_" : "R_";
                foreach (var finger in new[] { (Number: 2, Name: "Middle"), (Number: 3, Name: "Ring") })
                {
                    string[] segments = { "Proximal", "Intermediate", "Distal" };
                    for (int index = 0; index < segments.Length; index++)
                    {
                        string name = prefix + "Finger" + finger.Number + (index == 0 ? "" : index.ToString());
                        Transform bone = bones.Single(item => item.name == "ER_" + name ||
                            item.name == "ER_Set1100Avatar_" + name);
                        if (!targets.ContainsKey(name))
                        {
                            targets.Add(name, bone);
                            poses.Add(sources[name].World.inverse);
                        }
                        string humanName = side + " " + finger.Name + " " + segments[index];
                        if (!human.Any(item => item.humanName == humanName))
                        {
                            human.Add(new HumanBone { humanName = humanName, boneName = bone.name,
                                limit = new HumanLimit { useDefaultValues = true } });
                        }
                    }
                }
            }
            description.human = human.ToArray();
            Avatar repaired = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);
            if (!repaired.isHuman || !repaired.isValid)
            {
                Object.DestroyImmediate(repaired);
                throw new InvalidOperationException("Complete finger mapping did not produce a valid player Avatar.");
            }
            repaired.name = animator.avatar.name;
            EditorUtility.CopySerialized(repaired, animator.avatar);
            EditorUtility.SetDirty(animator.avatar);
            Object.DestroyImmediate(repaired);
            bindposes = poses.ToArray();
        }

        [MenuItem("Tools/ZZ/Elden Ring/Audit Player Rig Binding")]
        public static void AuditPlayerRigBinding()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            var report = new StringBuilder();
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                foreach (HumanBone human in animator.avatar.humanDescription.human)
                {
                    report.AppendLine(human.humanName + "=" + human.boneName);
                }
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                report.AppendLine("HEAD " + head.position);
                foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(item => item.name.StartsWith("HD_M_1100") || item.name.StartsWith("AM_M_1100") ||
                        item.name.StartsWith("FG_A_0100")))
                {
                    var mesh = new Mesh();
                    renderer.BakeMesh(mesh);
                    report.AppendLine(renderer.name + " Enabled=" + renderer.enabled + " Bounds=" + mesh.bounds +
                        " HeadLocal=" + renderer.transform.InverseTransformPoint(head.position));
                    Object.DestroyImmediate(mesh);
                    report.AppendLine(string.Join(";", renderer.bones.Select((bone, index) =>
                        index + ":" + bone.name)));
                }
                Directory.CreateDirectory("Docs/RigCombatRepair");
                File.WriteAllText("Docs/RigCombatRepair/rig-before.txt", report.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }
    }
}
