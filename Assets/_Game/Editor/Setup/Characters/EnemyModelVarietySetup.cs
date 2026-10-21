using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Builds network-ready visual variants from the existing combat prefab and authored humanoid rigs.</summary>
    public static class EnemyModelVarietySetup
    {
        private const string k_Output = "Assets/_Game/Prefabs/Characters/AI/Variants";
        private const string k_Models = "Assets/_Game/Art/Shared/Models/Rigged/Characters";

        [MenuItem("Tools/ZZ/Clean Replaced Legacy Enemy Models")]
        public static void CleanReplacedModels()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before deleting retired models.");
            }
            var names = new HashSet<string> { "Imp_01", "Grave_Tender_01", "Golem_01",
                "Corpse_Ghost_01", "Bell_Keeper_01", "Durk_01" };
            string[] candidates = Directory.GetFiles(k_Models, "*.prefab", SearchOption.AllDirectories)
                .Where(path => names.Contains(Path.GetFileNameWithoutExtension(path)))
                .Select(path => path.Replace('\\', '/')).ToArray();
            if (candidates.Length == 0)
            {
                return;
            }
            var extensions = new HashSet<string> { ".prefab", ".unity", ".asset", ".mat", ".controller",
                ".overrideController" };
            string[] users = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") &&
                extensions.Contains(Path.GetExtension(path)) && !candidates.Contains(path)).ToArray();
            var used = new HashSet<string>(AssetDatabase.GetDependencies(users, true));
            var report = new StringBuilder();
            foreach (string candidate in candidates)
            {
                if (used.Contains(candidate))
                {
                    report.AppendLine("KEPT " + candidate + ": still referenced");
                    continue;
                }
                string folder = Path.GetDirectoryName(candidate).Replace('\\', '/');
                if (!AssetDatabase.DeleteAsset(folder))
                {
                    throw new IOException("Cannot delete unreferenced retired model: " + folder);
                }
                report.AppendLine("DELETED " + folder + ": native external references=0");
            }
            File.WriteAllText("Docs/ArtRefresh/legacy-character-deletion.txt", report.ToString());
        }

        [MenuItem("Tools/ZZ/Build Enemy Model Variety")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before building character prefabs.");
            }
            if (!AssetDatabase.IsValidFolder(k_Output))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs/Characters/AI", "Variants");
            }
            string[] modelNames = { "Skeleton_00_Unarmed", "Imp_01", "Grave_Tender_01", "Golem_01",
                "Corpse_Ghost_01", "Bell_Keeper_01", "Durk_01" };
            float[] heights = { 1.85f, 1.25f, 2.5f, 3.2f, 2.25f, 3f, 3.5f };
            GameObject[] variants = new GameObject[modelNames.Length];
            string[] modelPaths = Directory.GetFiles(k_Models, "*.prefab", SearchOption.AllDirectories);
            var report = new StringBuilder();
            for (int index = 0; index < modelNames.Length; index++)
            {
                string outputPath = k_Output + "/" + modelNames[index] + (index >= 2 ? " Boss" : " Enemy") + ".prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                bool hasRealisticVisual = existing != null &&
                    existing.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.enabled &&
                        AssetDatabase.GetAssetPath(renderer.sharedMesh).StartsWith("Assets/_Game/Art/Characters/EldenRing/"));
                string modelPath = hasRealisticVisual ? AssetDatabase.GetAssetPath(existing) :
                    modelPaths.Single(path => Path.GetFileNameWithoutExtension(path) == modelNames[index]);
                variants[index] = hasRealisticVisual ? existing :
                    BuildVariant(modelPath.Replace('\\', '/'), modelNames[index], index >= 2, heights[index]);
                report.AppendLine($"{variants[index].name}: {modelPath}, height={heights[index]}");
            }
            var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset");
            foreach (GameObject variant in variants)
            {
                if (!networkPrefabs.Contains(variant))
                {
                    networkPrefabs.Add(new NetworkPrefab { Override = NetworkPrefabOverride.None, Prefab = variant });
                }
            }
            EditorUtility.SetDirty(networkPrefabs);
            AssignSpawners(variants, report);
            AssetDatabase.SaveAssets();
            File.WriteAllText(".utmp/enemy-model-variety.txt", report.ToString());
        }

        private static GameObject BuildVariant(string modelPath, string name, bool boss, float height)
        {
            string sourcePath = "Assets/_Game/Prefabs/Characters/AI/" + (boss ? "Fallen Watcher Boss" : "Undead AI") + ".prefab";
            string outputPath = k_Output + "/" + name + (boss ? " Boss" : " Enemy") + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.SetActive(false);
                root.name = name + (boss ? " Boss" : " Enemy");
                Animator oldAnimator = root.GetComponentInChildren<Animator>(true);
                GameObject oldVisual = oldAnimator.gameObject;
                GameObject visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                visual.name = name + " Visuals";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                if (name == "Golem_01")
                {
                    ConfigureGolemMaterial(visual);
                }
                foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = oldVisual.layer;
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }
                foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    Object.DestroyImmediate(behaviour);
                }
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(collider);
                }
                foreach (Joint joint in visual.GetComponentsInChildren<Joint>(true))
                {
                    Object.DestroyImmediate(joint);
                }
                foreach (Rigidbody importedBody in visual.GetComponentsInChildren<Rigidbody>(true))
                {
                    Object.DestroyImmediate(importedBody);
                }
                Animator animator = visual.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    throw new InvalidOperationException(name + " requires a valid humanoid rig.");
                }
                foreach (Animator secondary in visual.GetComponentsInChildren<Animator>(true))
                {
                    if (secondary != animator)
                    {
                        Object.DestroyImmediate(secondary);
                    }
                }
                animator.avatar = BuildRigAvatar(animator, name);
                animator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
                animator.applyRootMotion = oldAnimator.applyRootMotion;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var remap = new Dictionary<Object, Object> { { oldAnimator, animator }, { oldVisual, animator.gameObject },
                    { oldVisual.transform, animator.transform } };
                foreach (Component component in oldVisual.GetComponents<Component>())
                {
                    if (component is not MonoBehaviour && component is not AudioSource)
                    {
                        continue;
                    }
                    Component copy = animator.gameObject.GetComponent(component.GetType());
                    if (copy == null)
                    {
                        copy = animator.gameObject.AddComponent(component.GetType());
                    }
                    EditorUtility.CopySerialized(component, copy);
                    remap[component] = copy;
                }
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component is not MonoBehaviour || component.transform.IsChildOf(oldVisual.transform))
                    {
                        continue;
                    }
                    var data = new SerializedObject(component);
                    SerializedProperty property = data.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue != null && remap.TryGetValue(property.objectReferenceValue, out Object value))
                        {
                            property.objectReferenceValue = value;
                        }
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                // Match the existing attack volumes to the new rig instead of retaining the old hand positions.
                foreach (AIDamageCollider damage in root.GetComponentsInChildren<AIDamageCollider>(true))
                {
                    HumanBodyBones bone = damage.name.StartsWith("Left") ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                    Transform hand = animator.GetBoneTransform(bone);
                    if (hand != null)
                    {
                        damage.transform.SetParent(hand, false);
                        damage.transform.localPosition = Vector3.zero;
                        damage.transform.localRotation = Quaternion.identity;
                    }
                }
                Object.DestroyImmediate(oldVisual);
                root.SetActive(true);
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException(name + " has no visible mesh.");
                }
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1))
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                float factor = height / Mathf.Max(bounds.size.y, 0.1f);
                visual.transform.localScale *= factor;
                visual.transform.localPosition -= Vector3.up * (bounds.min.y - root.transform.position.y) * factor;
                CharacterController capsule = root.GetComponent<CharacterController>();
                capsule.height = height;
                capsule.center = Vector3.up * height * 0.5f;
                capsule.radius = Mathf.Min(height * 0.22f, 0.65f);
                CapsuleCollider body = root.GetComponent<CapsuleCollider>();
                body.height = capsule.height;
                body.center = capsule.center;
                body.radius = capsule.radius;
                NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
                agent.height = height;
                agent.radius = capsule.radius;
                var target = new GameObject("Lock On Target");
                target.transform.SetParent(root.transform, false);
                target.transform.localPosition = Vector3.up * height * 0.72f;
                var character = new SerializedObject(root.GetComponent<AICharacterManager>());
                character.FindProperty("m_lockOnTransform").objectReferenceValue = target.transform;
                character.ApplyModifiedPropertiesWithoutUndo();
                CharacterUIManager characterUI = root.GetComponentInChildren<CharacterUIManager>(true);
                if (characterUI != null)
                {
                    Vector3 uiPosition = characterUI.transform.localPosition;
                    uiPosition.y = height + 0.3f;
                    characterUI.transform.localPosition = uiPosition;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Avatar BuildRigAvatar(Animator animator, string name)
        {
            HumanDescription description = animator.avatar.humanDescription;
            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            var bones = transforms.GroupBy(bone => bone.name).ToDictionary(group => group.Key, group => group.First());
            description.human = description.human.Where(bone => bones.ContainsKey(bone.boneName)).ToArray();
            description.skeleton = transforms.GroupBy(bone => bone.name).Select(group => group.First())
                .Select(bone => new SkeletonBone
                {
                    name = bone.name,
                    position = bone.localPosition,
                    rotation = bone.localRotation,
                    scale = bone.localScale
                }).ToArray();
            Avatar built = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);
            if (!built.isValid || !built.isHuman)
            {
                Object.DestroyImmediate(built);
                throw new InvalidOperationException("Cannot build a valid humanoid Avatar for " + name);
            }
            built.name = name + " Runtime Avatar";
            string path = k_Output + "/" + built.name + ".asset";
            Avatar saved = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            EditorUtility.CopySerialized(built, saved);
            Object.DestroyImmediate(built);
            return saved;
        }

        private static void ConfigureGolemMaterial(GameObject visual)
        {
            Renderer renderer = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            string path = k_Output + "/Golem Stone.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(renderer.sharedMaterial) { name = "Golem Stone" };
                AssetDatabase.CreateAsset(material, path);
            }
            // The recovered dungeon material emits white without a mask, obscuring the stone texture.
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
            renderer.sharedMaterial = material;
        }

        private static void AssignSpawners(GameObject[] variants, StringBuilder report)
        {
            Scene previous = SceneManager.GetActiveScene();
            int ordinary = 0;
            foreach (string file in Directory.GetFiles("Assets/_Game/Scenes/Levels", "*_Spawners.unity", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                if (scene.isDirty)
                {
                    throw new InvalidOperationException("Save scene edits before assigning models: " + path);
                }
                foreach (AICharacterSpawner spawner in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AICharacterSpawner>(true)))
                {
                    var data = new SerializedObject(spawner);
                    SerializedProperty prefab = data.FindProperty("m_characterGameObject");
                    if (spawner.IsBoss)
                    {
                        int index = spawner.BossID == 1001 ? 6 : spawner.BossID - 1101 + 2;
                        if (index >= 2 && index < variants.Length)
                        {
                            prefab.objectReferenceValue = variants[index];
                            report.AppendLine($"Boss {spawner.BossID} -> {variants[index].name}");
                        }
                    }
                    else if (prefab.objectReferenceValue != null &&
                        (prefab.objectReferenceValue.name == "Undead AI" ||
                            prefab.objectReferenceValue == variants[0] || prefab.objectReferenceValue == variants[1]))
                    {
                        int choice = ordinary++ % 3;
                        if (choice < 2)
                        {
                            prefab.objectReferenceValue = variants[choice];
                        }
                        else
                        {
                            prefab.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
                        }
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            SceneManager.SetActiveScene(previous);
            report.AppendLine($"Distributed {ordinary} ordinary enemy spawners across three models.");
        }
    }
}
