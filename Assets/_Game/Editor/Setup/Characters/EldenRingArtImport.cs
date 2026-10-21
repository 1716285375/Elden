using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Imports local FLVER equipment and retargets the player's animations onto its original skeleton.</summary>
    public static partial class EldenRingArtImport
    {
        private const string k_Source = "F:/MyProject/RE/EldenRing/Converted/Set1100/models.json";
        private const string k_Output = "Assets/_Game/Art/Characters/EldenRing/Set1100";
        private const string k_Player = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_Candidate = k_Output + "/PlayerCandidate.prefab";
        private const string k_Report = "F:/MyProject/RE/EldenRing/Logs/unity-import.txt";
        private const string k_VisualName = "EldenRing Set1100";
        // EquipParamProtector rows 60000/60100/60200/60300 hide these numbered meshes for the complete outfit.
        private static readonly HashSet<int> s_HiddenMasks = new() { 24, 26, 30, 32, 41, 42, 43, 52, 54 };

        /// <summary>Imports and validates the outfit into a candidate while preserving the live player prefab.</summary>
        [MenuItem("Tools/ZZ/Import Elden Ring Outfit 1100")]
        public static void Apply()
        {
            BuildOutfit(k_Candidate);
        }

        /// <summary>Builds and samples the c3000 body on a candidate copy of the existing Undead AI.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Build Enemy Candidate")]
        public static void BuildEnemyCandidate()
        {
            ImportSettings settings = EnemySettings();
            BuildCharacter(settings.Output + "/EnemyCandidate.prefab", settings);
            VerifyEnemy(settings.Output + "/EnemyCandidate.prefab");
        }

        /// <summary>Installs the validated enemy appearance while keeping the existing gameplay prefab identity.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Install Enemy Appearance")]
        public static void InstallEnemy()
        {
            ImportSettings settings = EnemySettings();
            VerifyEnemy(settings.Output + "/EnemyCandidate.prefab");
            BuildCharacter(settings.Prefab, settings);
            VerifyEnemy(settings.Prefab);
        }

        [MenuItem("Tools/ZZ/Elden Ring/Install Realistic Ordinary Variants")]
        public static void InstallOrdinaryVariants()
        {
            string[] ids = { "c3510", "c3080" };
            string[] names = { "Skeleton_00_Unarmed Enemy", "Imp_01 Enemy" };
            for (int index = 0; index < ids.Length; index++)
            {
                string id = ids[index];
                var settings = new ImportSettings
                {
                    Source = "F:/MyProject/RE/EldenRing/Converted/" + id + "/unity-body.json",
                    Output = "Assets/_Game/Art/Characters/EldenRing/" + id,
                    Prefab = "Assets/_Game/Prefabs/Characters/AI/Variants/" + names[index] + ".prefab",
                    VisualName = "EldenRing " + id,
                    AvatarFile = id + "Avatar.asset",
                    Report = "F:/MyProject/RE/EldenRing/Logs/" + id + "-import.txt",
                    MeshCount = 2,
                    FitCharacterHeight = true
                };
                string backup = "Docs/ArtRefresh/" + id + "-before.prefab.txt";
                if (!File.Exists(backup))
                {
                    File.Copy(settings.Prefab, backup);
                }
                string candidate = settings.Output + "/EnemyCandidate.prefab";
                BuildCharacter(candidate, settings);
                VerifyEnemy(candidate, settings);
                BuildCharacter(settings.Prefab, settings);
                VerifyEnemy(settings.Prefab, settings);
            }
            CleanReplacedEnemyRenderers();
        }

        [MenuItem("Tools/ZZ/Elden Ring/Repair Skeleton Wrist Weights")]
        public static void RepairSkeletonWristWeights()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before repairing skin weights.");
            }
            var document = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/c3510/unity-body.json"));
            var sourceBones = BuildSourceBones(document);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Variants/Skeleton_00_Unarmed Enemy.prefab");
            var report = new StringBuilder();
            foreach (ModelRecord model in document.Models)
            {
                for (int index = 0; index < model.Meshes.Length; index++)
                {
                    string meshName = model.Name + "_" + index;
                    SkinnedMeshRenderer renderer = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Single(item => item.enabled && item.sharedMesh.name == meshName);
                    Mesh mesh = renderer.sharedMesh;
                    string backup = ".utmp/" + meshName + ".before-wrist.asset";
                    if (!File.Exists(backup))
                    {
                        File.Copy(AssetDatabase.GetAssetPath(mesh), backup);
                    }
                    var targets = renderer.bones.ToDictionary(bone => bone.name.Replace("ER_c3510Avatar_", ""));
                    BoneWeight[] previous = mesh.boneWeights;
                    BoneWeight[] repaired = MapSkinWeights(model, model.Meshes[index], sourceBones, targets);
                    if (repaired.Length != mesh.vertexCount)
                    {
                        throw new InvalidOperationException("Original vertex order does not match the imported mesh.");
                    }
                    mesh.boneWeights = repaired;
                    EditorUtility.SetDirty(mesh);
                    report.AppendLine(meshName + ": changed vertices=" +
                        previous.Zip(repaired, (before, after) => before.Equals(after) ? 0 : 1).Sum());
                }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/ArtRefresh/skeleton-wrist-repair.txt", report.ToString());
        }

        [MenuItem("Tools/ZZ/Elden Ring/Install Realistic Boss Variants")]
        public static void InstallBossVariants()
        {
            string[] ids = { "c3400", "c4660", "c3661", "c3100", "c2500" };
            string[] names = { "Grave_Tender_01 Boss", "Golem_01 Boss", "Corpse_Ghost_01 Boss",
                "Bell_Keeper_01 Boss", "Durk_01 Boss" };
            int[] counts = { 10, 4, 2, 21, 13 };
            var report = new StringBuilder();
            for (int index = 0; index < ids.Length; index++)
            {
                string id = ids[index];
                var settings = new ImportSettings
                {
                    Source = "F:/MyProject/RE/EldenRing/Converted/" + id + "/unity-body.json",
                    Output = "Assets/_Game/Art/Characters/EldenRing/" + id,
                    Prefab = "Assets/_Game/Prefabs/Characters/AI/Variants/" + names[index] + ".prefab",
                    VisualName = "EldenRing " + id,
                    AvatarFile = id + "Avatar.asset",
                    Report = "F:/MyProject/RE/EldenRing/Logs/" + id + "-import.txt",
                    MeshCount = counts[index],
                    FitCharacterHeight = true
                };
                GameObject installed = AssetDatabase.LoadAssetAtPath<GameObject>(settings.Prefab);
                if (installed.GetComponentsInChildren<Transform>(true).Any(child => child.name == settings.VisualName))
                {
                    report.AppendLine(id + " -> " + names[index] + ": already installed");
                    continue;
                }
                string backup = "Docs/ArtRefresh/" + id + "-before.prefab.txt";
                if (!File.Exists(backup))
                {
                    File.Copy(settings.Prefab, backup);
                }
                string candidate = settings.Output + "/EnemyCandidate.prefab";
                BuildCharacter(candidate, settings);
                VerifyEnemy(candidate, settings);
                BuildCharacter(settings.Prefab, settings);
                VerifyEnemy(settings.Prefab, settings);
                report.AppendLine(id + " -> " + names[index] + ": PASS");
                File.WriteAllText("Docs/ArtRefresh/boss-integration.txt", report.ToString());
                AssetDatabase.DeleteAsset(candidate);
            }
            CleanReplacedEnemyRenderers();
        }

        private static ImportSettings EnemySettings()
        {
            return new ImportSettings
            {
                Source = "F:/MyProject/RE/EldenRing/Converted/c3000/body.json",
                Output = "Assets/_Game/Art/Characters/EldenRing/c3000",
                Prefab = "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab",
                VisualName = "EldenRing c3000",
                AvatarFile = "c3000Avatar.asset",
                Report = "F:/MyProject/RE/EldenRing/Logs/enemy-import.txt",
                MeshCount = 21
            };
        }

        [MenuItem("Tools/ZZ/Elden Ring/Clean Replaced Enemy Renderers")]
        public static void CleanReplacedEnemyRenderers()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before cleaning character prefabs.");
            }
            var report = new StringBuilder();
            foreach (string name in new[] { "Skeleton_00_Unarmed Enemy", "Imp_01 Enemy",
                "Grave_Tender_01 Boss", "Golem_01 Boss", "Corpse_Ghost_01 Boss", "Bell_Keeper_01 Boss", "Durk_01 Boss" })
            {
                string path = "Assets/_Game/Prefabs/Characters/AI/Variants/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Animator animator = root.GetComponentInChildren<Animator>(true);
                    Transform visual = animator.transform.Cast<Transform>()
                        .SingleOrDefault(child => child.name.StartsWith("EldenRing "));
                    if (visual == null)
                    {
                        continue;
                    }
                    foreach (MeshRenderer legacy in animator.GetComponentsInChildren<MeshRenderer>(true)
                        .Where(renderer => !renderer.transform.IsChildOf(visual)))
                    {
                        MeshFilter filter = legacy.GetComponent<MeshFilter>();
                        Object.DestroyImmediate(legacy);
                        if (filter != null)
                        {
                            Object.DestroyImmediate(filter);
                        }
                    }
                    SkinnedMeshRenderer[] retired = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(renderer => !renderer.enabled && !renderer.transform.IsChildOf(visual)).ToArray();
                    foreach (SkinnedMeshRenderer renderer in retired)
                    {
                        Object.DestroyImmediate(renderer);
                    }
                    foreach (BoxCollider box in visual.GetComponentsInChildren<BoxCollider>(true))
                    {
                        Vector3 scale = box.transform.localScale;
                        if (scale.x >= 0f && scale.y >= 0f && scale.z >= 0f)
                        {
                            continue;
                        }
                        Vector3 center = box.transform.TransformPoint(box.center);
                        box.center = Vector3.Scale(box.center,
                            new Vector3(Mathf.Sign(scale.x), Mathf.Sign(scale.y), Mathf.Sign(scale.z)));
                        box.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                        if (Vector3.Distance(center, box.transform.TransformPoint(box.center)) > 0.0001f)
                        {
                            throw new InvalidOperationException("Normalizing damage scale moved its center.");
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine(name + ": removed disabled legacy renderers=" + retired.Length);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            File.WriteAllText("Docs/ArtRefresh/enemy-renderer-cleanup.txt", report.ToString());
        }

        /// <summary>Installs the c3010 soldier body on the authored ranged enemy.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Install Ranger Appearance")]
        public static void InstallRanger()
        {
            var settings = new ImportSettings
            {
                Source = "F:/MyProject/RE/EldenRing/Converted/c3010/body.json",
                Output = "Assets/_Game/Art/Characters/EldenRing/c3010",
                Prefab = "Assets/_Game/Prefabs/Characters/AI/Ranger/Ranger AI.prefab",
                VisualName = "EldenRing c3010",
                AvatarFile = "c3010Avatar.asset",
                Report = "F:/MyProject/RE/EldenRing/Logs/ranger-import.txt",
                MeshCount = 18
            };
            string candidate = settings.Output + "/RangerCandidate.prefab";
            BuildCharacter(candidate, settings);
            VerifyEnemy(candidate, settings);
            BuildCharacter(settings.Prefab, settings);
            VerifyEnemy(settings.Prefab, settings);
        }

        private static void VerifyEnemy(string path, ImportSettings settings = null)
        {
            settings ??= EnemySettings();
            string prefix = Path.GetFileNameWithoutExtension(settings.Report).Replace("-import", string.Empty);
            GameObject enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var report = new StringBuilder();
            try
            {
                Animator animator = enemy.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                animator.Rebind();
                ValidateSkin(enemy, report, settings.VisualName, settings.MeshCount);
                Capture(enemy, prefix + "-rest.png", settings.VisualName);
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips
                    .Where(clip => clip.isHumanMotion && clip.length > 0.1f).Distinct().ToArray();
                if (clips.Length == 0)
                {
                    throw new InvalidOperationException("Enemy controller has no humanoid clips.");
                }
                AnimationMode.StartAnimationMode();
                foreach (AnimationClip clip in clips)
                {
                    AnimationMode.StopAnimationMode();
                    animator.Rebind();
                    AnimationMode.StartAnimationMode();
                    foreach (float phase in new[] { 0.2f, 0.7f })
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * phase);
                        AnimationMode.EndSampling();
                        report.AppendLine("SAMPLE " + clip.name + " " + phase);
                        File.WriteAllText(settings.Report.Replace("import", "verification"), report.ToString());
                        ValidateSkin(enemy, report, settings.VisualName, settings.MeshCount);
                    }
                    report.AppendLine("PASS " + clip.name);
                }
                foreach (Collider collider in enemy.GetComponentsInChildren<Collider>(true)
                    .Where(collider => collider.name.Contains("Hand Damage")))
                {
                    Transform visual = animator.transform.Find(settings.VisualName);
                    if (collider.transform.IsChildOf(animator.transform) && !collider.transform.IsChildOf(visual))
                    {
                        throw new InvalidOperationException("Enemy hand collider still follows the old skeleton.");
                    }
                    report.AppendLine("ATTACHMENT " + collider.name + " -> " + collider.transform.parent.name);
                }
                Capture(enemy, prefix + "-animated.png", settings.VisualName);
                File.WriteAllText(settings.Report.Replace("import", "verification"), report.ToString());
                Debug.Log("ELDEN_RING_ENEMY_VERIFIED clips=" + clips.Length);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(enemy);
            }
        }

        private static void BuildOutfit(string destination)
        {
            BuildCharacter(destination, new ImportSettings());
        }

        private static void BuildCharacter(string destination, ImportSettings settings)
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before replacing player art.");
            }

            var document = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(settings.Source));
            Directory.CreateDirectory(settings.Output + "/Meshes");
            Directory.CreateDirectory(settings.Output + "/Materials");
            AssetDatabase.Refresh();
            ConfigureTextures(settings.Output);
            GameObject player = PrefabUtility.LoadPrefabContents(settings.Prefab);
            var report = new StringBuilder();
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
                {
                    throw new InvalidOperationException("Player requires its existing valid humanoid Avatar.");
                }

                Transform previous = animator.transform.Find(settings.VisualName);
                if (previous != null)
                {
                    foreach (ModelRecord model in document.Models)
                    {
                        BuildMaterials(model, settings.Output);
                    }
                    AssetDatabase.SaveAssets();
                    PrefabUtility.SaveAsPrefabAsset(player, destination);
                    Debug.Log("Elden Ring outfit is already installed. Run Verify to check the saved prefab.");
                    return;
                }

                var originalTargets = GetHumanTargets(animator);
                var sourceBones = BuildSourceBones(document);
                var visual = new GameObject(settings.VisualName);
                visual.transform.SetParent(animator.transform, false);
                visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                visual.layer = animator.gameObject.layer;
                foreach (Renderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.enabled = false;
                }

                var importedBones = new Dictionary<string, Transform>();
                foreach (string name in sourceBones.Keys)
                {
                    var bone = new GameObject("ER_" + Path.GetFileNameWithoutExtension(settings.AvatarFile) +
                        "_" + name).transform;
                    bone.SetParent(visual.transform, false);
                    Matrix4x4 world = sourceBones[name].World;
                    bone.localPosition = world.GetColumn(3);
                    bone.localRotation = world.rotation;
                    bone.localScale = world.lossyScale;
                    importedBones.Add(name, bone);
                }
                foreach (KeyValuePair<string, SourceBone> pair in sourceBones)
                {
                    if (pair.Value.Parent != null)
                    {
                        importedBones[pair.Key].SetParent(importedBones[pair.Value.Parent], true);
                    }
                }
                importedBones["Spine"].SetParent(importedBones["Pelvis"], true);
                foreach (string side in new[] { "L_", "R_" })
                {
                    Transform hand = importedBones[side + "Hand"];
                    Transform forearm = importedBones[side + "Forearm"];
                    // Some original rigs drive hands beside the forearm; Humanoid requires an ancestor chain.
                    if (!hand.IsChildOf(forearm))
                    {
                        hand.SetParent(forearm, true);
                    }
                }
                var skinTargets = originalTargets.Keys.Where(importedBones.ContainsKey)
                    .ToDictionary(name => name, name => importedBones[name]);

                int meshCount = 0;
                foreach (ModelRecord model in document.Models)
                {
                    Material[] materials = BuildMaterials(model, settings.Output);
                    for (int index = 0; index < model.Meshes.Length; index++)
                    {
                        MeshRecord sourceMesh = model.Meshes[index];
                        string materialName = model.Materials[sourceMesh.Material].Name;
                        if (materialName.StartsWith("#") &&
                            int.TryParse(materialName.Split('#')[1], out int mask) && s_HiddenMasks.Contains(mask))
                        {
                            continue;
                        }
                        var meshObject = new GameObject(model.Name + "_" + index);
                        meshObject.transform.SetParent(visual.transform, false);
                        meshObject.layer = visual.layer;
                        var renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
                        ImportMesh(model, sourceMesh, sourceBones, skinTargets, renderer, index, settings.Output);
                        renderer.sharedMaterial = materials[sourceMesh.Material];
                        renderer.rootBone = importedBones["Pelvis"];
                        renderer.quality = SkinQuality.Bone4;
                        renderer.updateWhenOffscreen = true;
                        report.AppendLine($"{meshObject.name}: {renderer.sharedMesh.vertexCount} vertices");
                        meshCount++;
                    }
                }

                foreach (string side in new[] { "L_", "R_" })
                {
                    Vector3 direction = side == "L_" ? -animator.transform.right : animator.transform.right;
                    AlignBone(importedBones[side + "UpperArm"], importedBones[side + "Forearm"], direction);
                    AlignBone(importedBones[side + "Forearm"], importedBones[side + "Hand"], direction);
                }
                Avatar originalAvatar = animator.avatar;
                var human = originalAvatar.humanDescription.human;
                var mapped = new List<HumanBone>();
                foreach (HumanBone source in human)
                {
                    HumanBodyBones id = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),
                        source.humanName.Replace(" ", ""));
                    // Some source models contain an unweighted root-level Neck placeholder.
                    if (id == HumanBodyBones.Neck && !importedBones["Head"].IsChildOf(importedBones["Neck"]))
                    {
                        continue;
                    }
                    Transform oldBone = animator.GetBoneTransform(id);
                    string name = originalTargets.FirstOrDefault(pair => pair.Value == oldBone).Key;
                    if (name != null && importedBones.TryGetValue(name, out Transform replacement))
                    {
                        mapped.Add(new HumanBone { humanName = source.humanName, boneName = replacement.name,
                            limit = new HumanLimit { useDefaultValues = true } });
                    }
                }
                var description = new HumanDescription
                {
                    human = mapped.ToArray(),
                    skeleton = new[] { animator.transform, visual.transform }.Concat(importedBones.Values)
                        .Select(bone => new SkeletonBone
                    {
                        name = bone.name, position = bone.localPosition,
                        rotation = bone.localRotation, scale = bone.localScale
                    }).ToArray(),
                    upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
                    upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                    armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f,
                    hasTranslationDoF = false
                };
                Avatar avatar = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);
                if (!avatar.isValid || !avatar.isHuman)
                {
                    Object.DestroyImmediate(avatar);
                    throw new InvalidOperationException("Imported skeleton did not produce a valid humanoid avatar.");
                }
                avatar.name = settings.VisualName + " Avatar";
                string avatarPath = settings.Output + "/" + settings.AvatarFile;
                Avatar savedAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                if (savedAvatar == null)
                {
                    AssetDatabase.CreateAsset(avatar, avatarPath);
                    savedAvatar = avatar;
                }
                else
                {
                    EditorUtility.CopySerialized(avatar, savedAvatar);
                    Object.DestroyImmediate(avatar);
                }
                Transform[] attachments = player.GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                    .Select(slot => slot.transform).Concat(player.GetComponentsInChildren<Collider>(true)
                        .Select(collider => collider.transform)).Distinct().ToArray();
                foreach (Transform attachment in attachments)
                {
                    string sourceName = originalTargets.FirstOrDefault(pair => pair.Value == attachment.parent).Key;
                    if (sourceName != null && importedBones.TryGetValue(sourceName, out Transform replacement))
                    {
                        Vector3 offset = attachment.position - attachment.parent.position;
                        attachment.SetParent(replacement, true);
                        attachment.position = replacement.position + offset;
                    }
                }
                animator.avatar = savedAvatar;
                if (settings.FitCharacterHeight)
                {
                    EnemySkinBoundsSetup.FitCapsuleHeight(player);
                }
                ValidateSkin(player, report, settings.VisualName, settings.MeshCount);
                report.AppendLine($"Meshes={meshCount}; Avatar={AssetDatabase.GetAssetPath(animator.avatar)}");
                PrefabUtility.SaveAsPrefabAsset(player, destination);
                AssetDatabase.SaveAssets();
                File.WriteAllText(settings.Report, report.ToString());
                Debug.Log("ELDEN_RING_IMPORT_COMPLETE " + meshCount);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static void AlignBone(Transform bone, Transform child, Vector3 direction)
        {
            bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
        }

        /// <summary>Rebuilds the candidate and verifies its saved mesh buffers and animation.</summary>
        public static void RebuildAndVerify()
        {
            EldenRingIconImport.Apply();
            Apply();
            Verify();
        }

        /// <summary>Installs the verified outfit into the existing player while preserving its asset identity.</summary>
        [MenuItem("Tools/ZZ/Install Verified Elden Ring Outfit")]
        public static void Install()
        {
            Verify();
            BuildOutfit(k_Player);
            VerifyPrefab(k_Player);
        }

        /// <summary>Checks the saved outfit and captures rest and animated poses.</summary>
        public static void Verify()
        {
            VerifyPrefab(k_Candidate);
        }

        private static void VerifyPrefab(string path)
        {
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var report = new StringBuilder();
            try
            {
                ValidateSkin(player, report);
                Capture(player, "player-rest.png");
                Animator animator = player.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                animator.Rebind();
                report.AppendLine(string.Join("; ", animator.avatar.humanDescription.human.Select(
                    bone => bone.humanName + "=" + bone.boneName)));
                Quaternion armBefore = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation;
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips
                    .Where(clip => clip.isHumanMotion && clip.length > 0.2f).Distinct().ToArray();
                AnimationClip clip = clips.FirstOrDefault(candidate =>
                    candidate.name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0) ?? clips.First();
                var renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => IsVisibleSkin(renderer, k_VisualName)).ToArray();
                Vector3[] before = BakeVertices(renderers[0]);
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * 0.35f);
                AnimationMode.EndSampling();
                Vector3[] after = BakeVertices(renderers[0]);
                float displacement = before.Zip(after, Vector3.Distance).Max();
                report.AppendLine("Upper arm rotation delta=" + Quaternion.Angle(armBefore,
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation));
                if (displacement < 0.001f)
                {
                    throw new InvalidOperationException("Humanoid clip did not deform the imported skin.");
                }

                ValidateSkin(player, report);
                Capture(player, "player-animated.png");
                report.AppendLine($"Animation={clip.name}; maximum sampled displacement={displacement:F4}m");
                foreach (string name in new[] { "core_main_idle_01", "straight_sword_main_light_attack_01",
                    "core_main_run_FR_01", "core_main_roll_med_to_idle_F_01" })
                {
                    string clipPath = Directory.GetFiles("Assets/_Game/Art/Characters/Shared/Humanoid/Animations",
                        name + ".anim", SearchOption.AllDirectories).Single().Replace('\\', '/');
                    AnimationClip sample = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    foreach (float phase in new[] { 0.1f, 0.45f, 0.8f })
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(animator.gameObject, sample, sample.length * phase);
                        AnimationMode.EndSampling();
                        ValidateSkin(player, report);
                        report.AppendLine($"PASS {name} phase={phase}");
                    }
                }
                File.WriteAllText(k_Report.Replace("import", "verification"), report.ToString());
                Debug.Log("ELDEN_RING_VERIFY_COMPLETE " + report);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(player);
            }
        }

        private static Dictionary<string, Transform> GetHumanTargets(Animator animator)
        {
            var names = new Dictionary<string, HumanBodyBones>
            {
                { "Pelvis", HumanBodyBones.Hips }, { "Spine", HumanBodyBones.Spine },
                { "Spine1", HumanBodyBones.Chest }, { "Spine2", HumanBodyBones.UpperChest },
                { "Neck", HumanBodyBones.Neck }, { "Head", HumanBodyBones.Head }
            };
            string[] sourceNames = { "Clavicle", "UpperArm", "Forearm", "Hand", "Thigh", "Calf", "Foot", "Toe0" };
            string[] humanNames = { "Shoulder", "UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes" };
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
            string[] segments = { "Proximal", "Intermediate", "Distal" };
            foreach (string side in new[] { "Left", "Right" })
            {
                string prefix = side == "Left" ? "L_" : "R_";
                for (int index = 0; index < sourceNames.Length; index++)
                {
                    names[prefix + sourceNames[index]] = (HumanBodyBones)Enum.Parse(
                        typeof(HumanBodyBones), side + humanNames[index]);
                }

                for (int finger = 0; finger < fingers.Length; finger++)
                {
                    for (int segment = 0; segment < segments.Length; segment++)
                    {
                        string suffix = segment == 0 ? "" : segment.ToString();
                        names[prefix + "Finger" + finger + suffix] = (HumanBodyBones)Enum.Parse(
                            typeof(HumanBodyBones), side + fingers[finger] + segments[segment]);
                    }
                }
            }

            var result = names.ToDictionary(pair => pair.Key, pair => animator.GetBoneTransform(pair.Value));
            result["Spine2"] ??= result["Spine1"];
            return result.Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private static Dictionary<string, SourceBone> BuildSourceBones(ExportDocument document)
        {
            var result = new Dictionary<string, SourceBone>();
            foreach (ModelRecord model in document.Models)
            {
                for (int index = 0; index < model.Bones.Length; index++)
                {
                    BoneRecord bone = model.Bones[index];
                    if (result.TryGetValue(bone.Name, out SourceBone existing))
                    {
                        if (existing.Parent == null && bone.Parent >= 0)
                        {
                            existing.Parent = model.Bones[bone.Parent].Name;
                            existing.World = GetSourceWorld(model.Bones, index);
                        }
                        continue;
                    }

                    result.Add(bone.Name, new SourceBone
                    {
                        Parent = bone.Parent < 0 ? null : model.Bones[bone.Parent].Name,
                        World = GetSourceWorld(model.Bones, index)
                    });
                }
            }

            return result;
        }

        private static string ResolveDeformBone(string name)
        {
            // shortcut: chest armor helper bones follow the torso until secondary cloth rigs are imported.
            if (name == "SpineArmor1" || name == "SpineArmor2")
            {
                return name == "SpineArmor1" ? "Spine1" : "Spine2";
            }
            foreach (string side in new[] { "L_", "R_" })
            {
                if (name == side + "Shoulder" || name == side + "ShoulderArmor")
                {
                    return side + "UpperArm";
                }
                if (name == side + "Elbow" || name == side + "Forearm2")
                {
                    return side + "Forearm";
                }
                if (name == side + "Hip")
                {
                    return side + "Thigh";
                }
                if (name == side + "Knee" || name == side + "Knee_Skirt")
                {
                    return side + "Calf";
                }
            }
            return name;
        }

        private static Matrix4x4 GetSourceWorld(BoneRecord[] bones, int index)
        {
            BoneRecord bone = bones[index];
            Matrix4x4 local = Matrix4x4.TRS(Vector(bone.Position, 0),
                new Quaternion(bone.Rotation[0], bone.Rotation[1], bone.Rotation[2], bone.Rotation[3]),
                Vector(bone.Scale, 0));
            return bone.Parent < 0 ? local : GetSourceWorld(bones, bone.Parent) * local;
        }

        private static void ImportMesh(ModelRecord model, MeshRecord source,
            Dictionary<string, SourceBone> sourceBones, Dictionary<string, Transform> targets,
            SkinnedMeshRenderer renderer, int meshIndex, string output)
        {
            Transform[] bones = targets.Values.ToArray();
            int count = source.Positions.Length / 3;
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            BoneWeight[] weights = MapSkinWeights(model, source, sourceBones, targets);
            for (int vertex = 0; vertex < count; vertex++)
            {
                positions[vertex] = Vector(source.Positions, vertex * 3);
                normals[vertex] = Vector(source.Normals, vertex * 3).normalized;
                uv[vertex] = new Vector2(source.UV[vertex * 2], source.UV[vertex * 2 + 1]);
            }

            string meshName = model.Name + "_" + meshIndex;
            string path = output + "/Meshes/" + meshName + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh ??= new Mesh();
            // CopySerialized does not reliably replace the native vertex buffer of an existing Mesh.
            mesh.Clear();
            mesh.name = meshName;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uv;
            int[] triangles = (int[])source.Indices.Clone();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                (triangles[index + 1], triangles[index + 2]) = (triangles[index + 2], triangles[index + 1]);
            }
            mesh.triangles = triangles;
            mesh.boneWeights = weights;
            // Bind in the complete source skeleton's A-pose before preparing its humanoid T-pose.
            mesh.bindposes = bones.Select(bone => bone.worldToLocalMatrix * renderer.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (isNew)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.localBounds = mesh.bounds;
        }

        private static BoneWeight[] MapSkinWeights(ModelRecord model, MeshRecord source,
            Dictionary<string, SourceBone> sourceBones, Dictionary<string, Transform> targets)
        {
            Transform[] bones = targets.Values.ToArray();
            var boneMap = new int[model.Bones.Length];
            for (int index = 0; index < boneMap.Length; index++)
            {
                string name = ResolveDeformBone(model.Bones[index].Name);
                while (name != null && !targets.ContainsKey(name))
                {
                    name = sourceBones[name].Parent;
                    if (name != null)
                    {
                        name = ResolveDeformBone(name);
                    }
                }
                boneMap[index] = name == null ? -1 : Array.IndexOf(bones, targets[name]);
            }

            var weights = new BoneWeight[source.Positions.Length / 3];
            for (int vertex = 0; vertex < weights.Length; vertex++)
            {
                var influences = new Dictionary<int, float>();
                for (int slot = 0; slot < 4; slot++)
                {
                    int offset = vertex * 4 + slot;
                    float weight = source.Weights[offset];
                    if (weight <= 0)
                    {
                        continue;
                    }

                    int bone = boneMap[source.BoneIndices[offset]];
                    if (bone < 0)
                    {
                        throw new InvalidOperationException("Unmapped skin bone: " +
                            model.Bones[source.BoneIndices[offset]].Name);
                    }
                    influences.TryGetValue(bone, out float existing);
                    influences[bone] = existing + weight;
                }

                KeyValuePair<int, float>[] ordered = influences.OrderByDescending(pair => pair.Value).ToArray();
                weights[vertex] = MakeWeight(ordered);
            }
            return weights;
        }

        private static BoneWeight MakeWeight(KeyValuePair<int, float>[] values)
        {
            var result = new BoneWeight { boneIndex0 = values[0].Key, weight0 = values[0].Value };
            if (values.Length > 1)
            {
                result.boneIndex1 = values[1].Key;
                result.weight1 = values[1].Value;
            }
            if (values.Length > 2)
            {
                result.boneIndex2 = values[2].Key;
                result.weight2 = values[2].Value;
            }
            if (values.Length > 3)
            {
                result.boneIndex3 = values[3].Key;
                result.weight3 = values[3].Value;
            }
            return result;
        }

        private static Material[] BuildMaterials(ModelRecord model, string output)
        {
            var result = new Material[model.Materials.Length];
            for (int index = 0; index < result.Length; index++)
            {
                MaterialRecord source = model.Materials[index];
                string path = output + "/Materials/" + model.Name + "_" + index + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = material == null;
                material ??= new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = model.Name + "_" + source.Name;
                Texture2D albedo = FindTexture(source, "AlbedoMap", output);
                // C_Fur's strand alpha lives in sampler 1; FurColor is an opaque tint texture.
                TextureRecord fur = source.Textures.FirstOrDefault(texture =>
                    texture.Type == "C_Fur__snp_Texture2D_1_AlbedoMap") ??
                    source.Textures.FirstOrDefault(texture => texture.Type.Contains("AlbedoMap") &&
                    (texture.Name.IndexOf("fur", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     texture.Name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0));
                if (fur != null)
                {
                    albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(output + "/Textures/" + fur.Name + ".png") ?? albedo;
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.5f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                if (albedo == null)
                {
                    throw new InvalidOperationException("Missing base color texture: " + material.name);
                }

                material.SetTexture("_BaseMap", albedo);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.3f);
                material.SetFloat("_Cull", 0f);
                Texture2D metallic = FindTexture(source, "MetallicMap", output);
                if (metallic != null)
                {
                    material.SetTexture("_MetallicGlossMap", metallic);
                    material.SetFloat("_Metallic", 1f);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                Texture2D normal = FindTexture(source, "NormalMap", output);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                }
                // Original packed maps require shader-specific channel conversion before URP can use them.
                material.DisableKeyword("_NORMALMAP");
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetTexture("_BumpMap", null);
                material.SetTexture("_MetallicGlossMap", null);
                material.SetFloat("_Metallic", 0.25f);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    EditorUtility.SetDirty(material);
                }
                result[index] = material;
            }
            return result;
        }

        private static Texture2D FindTexture(MaterialRecord material, string kind, string output)
        {
            foreach (TextureRecord texture in material.Textures.Where(texture => texture.Type.Contains(kind)))
            {
                if (string.IsNullOrEmpty(texture.Name))
                {
                    continue;
                }
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(output + "/Textures/" + texture.Name + ".png");
                if (asset != null)
                {
                    return asset;
                }
            }
            return null;
        }

        private static void ConfigureTextures(string output)
        {
            foreach (string path in Directory.GetFiles(output + "/Textures", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                bool normal = Path.GetFileNameWithoutExtension(path).EndsWith("_n", StringComparison.OrdinalIgnoreCase);
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal && !Path.GetFileNameWithoutExtension(path).EndsWith("_m");
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static void ValidateSkin(GameObject player, StringBuilder report,
            string visualName = k_VisualName, int expectedCount = 33)
        {
            SkinnedMeshRenderer[] meshes = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => IsVisibleSkin(renderer, visualName)).ToArray();
            bool isModularPlayer = visualName == k_VisualName && player.GetComponent<PlayerSkinnedEquipment>() != null;
            if (meshes.Length == 0 || (!isModularPlayer && meshes.Length != expectedCount))
            {
                throw new InvalidOperationException("Unexpected visible mesh count " + meshes.Length);
            }
            var bounds = new Bounds(EnemySkinBoundsSetup.GetWorldVertices(meshes[0])[0], Vector3.zero);
            foreach (SkinnedMeshRenderer renderer in meshes)
            {
                if (renderer.bones.Any(bone => bone == null) || renderer.sharedMaterial == null)
                {
                    throw new InvalidOperationException("Missing bone or material: " + renderer.name);
                }
                foreach (Vector3 world in EnemySkinBoundsSetup.GetWorldVertices(renderer))
                {
                    if (!float.IsFinite(world.x) || !float.IsFinite(world.y) || !float.IsFinite(world.z))
                    {
                        throw new InvalidOperationException("Invalid skinned vertex: " + renderer.name +
                            " position=" + world + " root=" + player.transform.position);
                    }
                    bounds.Encapsulate(world);
                }
            }
            // Root-motion clips can translate the whole character; validate its extent, not distance from origin.
            float maximumHeight = Mathf.Max(3.5f, player.GetComponent<CharacterController>().height * 1.6f);
            if (bounds.size.y < 0.1f || bounds.size.y > maximumHeight || bounds.size.magnitude > maximumHeight * 3f)
            {
                throw new InvalidOperationException("Unexpected skin bounds " + bounds);
            }
            report.AppendLine("Validated skin bounds: " + bounds);
        }

        private static Vector3[] BakeVertices(SkinnedMeshRenderer renderer)
        {
            var baked = new Mesh();
            renderer.BakeMesh(baked);
            Vector3[] result = baked.vertices;
            Object.DestroyImmediate(baked);
            return result;
        }

        private static bool IsVisibleSkin(SkinnedMeshRenderer renderer, string visualName)
        {
            return renderer.enabled && (renderer.transform.parent.name == visualName ||
                (visualName == k_VisualName && renderer.transform.parent.name == k_ModularRoot));
        }

        private static void Capture(GameObject player, string filename, string visualName = k_VisualName,
            int width = 960, int height = 960, bool includeAttachments = false)
        {
            var preview = new PreviewRenderUtility();
            var displayRoot = new GameObject("Drake Knight Preview");
            var meshes = new List<Mesh>();
            try
            {
                preview.AddSingleGO(displayRoot);
                var bounds = new Bounds();
                bool hasBounds = false;
                foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => IsVisibleSkin(renderer, visualName)))
                {
                    var baked = new Mesh();
                    renderer.BakeMesh(baked);
                    meshes.Add(baked);
                    var display = new GameObject(renderer.name);
                    display.transform.SetParent(displayRoot.transform, false);
                    display.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                    display.transform.localScale = renderer.transform.lossyScale;
                    display.AddComponent<MeshFilter>().sharedMesh = baked;
                    display.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        Vector3 position = display.transform.TransformPoint(vertex);
                        if (!hasBounds)
                        {
                            bounds = new Bounds(position, Vector3.zero);
                            hasBounds = true;
                        }
                        bounds.Encapsulate(position);
                    }
                }
                if (includeAttachments)
                {
                    foreach (MeshRenderer renderer in player.GetComponentsInChildren<MeshRenderer>(true)
                        .Where(item => item.enabled && item.gameObject.activeInHierarchy))
                    {
                        Mesh source = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                        if (source == null) { continue; }
                        var display = new GameObject(renderer.name);
                        display.transform.SetParent(displayRoot.transform, false);
                        display.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                        display.transform.localScale = renderer.transform.lossyScale;
                        display.AddComponent<MeshFilter>().sharedMesh = source;
                        display.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
                preview.camera.fieldOfView = 32f;
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 100f;
                preview.camera.transform.position = bounds.center + (width < height ?
                    new Vector3(0.20f, 0.05f, 2.0f) : new Vector3(2.5f, 0.5f, 4f));
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = width < height ? Color.clear : new Color(0.13f, 0.15f, 0.18f);
                preview.lights[0].intensity = 1.5f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
                preview.lights[1].intensity = 0.8f;
                preview.BeginStaticPreview(new Rect(0, 0, width, height));
                preview.Render(true);
                Texture2D alpha = null;
                if (width < height)
                {
                    // Preserve the static preview's color conversion while retaining the render target alpha.
                    RenderTexture target = preview.camera.targetTexture;
                    RenderTexture previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = target;
                        alpha = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                        alpha.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        alpha.Apply();
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                    }
                }
                Texture2D image = preview.EndStaticPreview();
                if (alpha != null)
                {
                    Color[] colors = image.GetPixels();
                    for (int y = 0; y < image.height; y++)
                    {
                        for (int x = 0; x < image.width; x++)
                        {
                            colors[y * image.width + x].a = alpha.GetPixelBilinear(
                                (x + 0.5f) / image.width, (y + 0.5f) / image.height).a;
                        }
                    }
                    var rgba = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
                    rgba.SetPixels(colors);
                    rgba.Apply();
                    Object.DestroyImmediate(image);
                    Object.DestroyImmediate(alpha);
                    image = rgba;
                }
                File.WriteAllBytes(Path.GetDirectoryName(k_Report) + "/" + filename, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                preview.Cleanup();
                foreach (Mesh mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }
        private static Vector3 Vector(float[] values, int offset)
        {
            return new Vector3(values[offset], values[offset + 1], values[offset + 2]);
        }

        private sealed class ImportSettings
        {
            public string Source = k_Source;
            public string Output = k_Output;
            public string Prefab = k_Player;
            public string VisualName = k_VisualName;
            public string AvatarFile = "Set1100Avatar.asset";
            public string Report = k_Report;
            public int MeshCount = 33;
            public bool FitCharacterHeight;
        }

        [Serializable] private sealed class ExportDocument { public ModelRecord[] Models; }
        [Serializable] private sealed class ModelRecord
        {
            public string Name;
            public BoneRecord[] Bones;
            public MeshRecord[] Meshes;
            public MaterialRecord[] Materials;
        }
        [Serializable] private sealed class BoneRecord
        {
            public string Name;
            public int Parent;
            public float[] Position;
            public float[] Rotation;
            public float[] Scale;
        }
        [Serializable] private sealed class MeshRecord
        {
            public int Material;
            public float[] Positions;
            public float[] Normals;
            public float[] UV;
            public int[] Indices;
            public float[] Weights;
            public int[] BoneIndices;
        }
        [Serializable] private sealed class MaterialRecord { public string Name; public TextureRecord[] Textures; }
        [Serializable] private sealed class TextureRecord { public string Type; public string Name; }
        private sealed class SourceBone { public string Parent; public Matrix4x4 World; }
    }
}
