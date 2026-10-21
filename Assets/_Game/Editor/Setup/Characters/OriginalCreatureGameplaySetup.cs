using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Connects the verified original rigs to the existing authoritative AI and combat systems.</summary>
    public static class OriginalCreatureGameplaySetup
    {
        public const string Output = "Assets/_Game/Data/AI/OriginalCreatures";
        private const string k_Source = "F:/MyProject/RE/EldenRing/Converted/SingleMapGameplay";
        private const string k_Rigs = "Assets/_Game/Art/Characters/EldenRing/OriginalCreatures";
        private const string k_Template = "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";

        [MenuItem("Tools/ZZ/Elden Ring/Install Original Creature Gameplay")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before installing original AI.");
            }
            JObject progress = JObject.Parse(File.ReadAllText("Docs/OriginalSingleMap/animation-import.json"));
            if (progress.Value<string>("Status") != "completed" || progress.Value<int>("VerifiedNativeClips") != 664)
            {
                throw new InvalidOperationException("Complete and verify all 664 native creature clips first.");
            }
            JObject gameplay = JObject.Parse(File.ReadAllText(k_Source + "/source.json"));
            JObject timelines = JObject.Parse(File.ReadAllText(k_Source + "/combat-timelines.json"));
            if (((JArray)timelines["Diagnostics"]).Count != 0)
            {
                throw new InvalidDataException("Original attack timelines still contain unresolved references.");
            }
            Directory.CreateDirectory(Output);
            File.Copy(k_Source + "/combat-timelines.json", Output + "/CombatTimelines.json", true);
            AssetDatabase.ImportAsset(Output + "/CombatTimelines.json", ImportAssetOptions.ForceSynchronousImport);
            foreach (var entry in new[] { ("c3970", 39701910), ("c4070", 40700010), ("c4071", 40710010) })
            {
                InstallActor(entry.Item1, entry.Item2, gameplay, timelines);
            }
            File.WriteAllText("Docs/OriginalSingleMap/gameplay-prefabs.txt",
                "PASS: c3970, c4070, c4071 original generic rigs, locomotion, source melee actions, " +
                "TAE hit volumes and replicated definition IDs installed.\n" +
                "Runtime encounter, complete original goal logic, source event scripting and rendering require validation.\n");
        }

        private static void InstallActor(string actor, int npcId, JObject gameplay, JObject timelines)
        {
            string folder = Output + "/" + actor;
            Directory.CreateDirectory(folder);
            JObject npc = (JObject)gameplay["Parameters"]["NpcParam"]
                .Single(row => row.Value<int>("ID") == npcId)["Fields"];
            JToken spawn = gameplay["Enemies"].First(row => row.Value<string>("ModelName") == actor);
            JObject think = (JObject)gameplay["Parameters"]["NpcThinkParam"]
                .Single(row => row.Value<int>("ID") == spawn.Value<int>("ThinkParamID"))["Fields"];
            JToken[] actions = timelines["Actions"].Where(row => row.Value<string>("Actor") == actor).ToArray();
            GameObject root = PrefabUtility.LoadPrefabContents(k_Template);
            try
            {
                root.name = actor;
                Transform visual = root.transform.Find("Undead Visuals");
                Animator animator = visual.GetComponent<Animator>();
                RuntimeAnimatorController sourceController = animator.runtimeAnimatorController;
                while (sourceController is AnimatorOverrideController overrides)
                {
                    sourceController = overrides.runtimeAnimatorController;
                }
                AnimatorControllerParameter[] parameters = ((AnimatorController)sourceController).parameters;
                foreach (Transform child in visual.Cast<Transform>().ToArray())
                {
                    Object.DestroyImmediate(child.gameObject);
                }
                Transform oldColliders = root.transform.Find("AI Damage Colliders");
                if (oldColliders != null)
                {
                    Object.DestroyImmediate(oldColliders.gameObject);
                }
                GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(k_Rigs + "/" + actor + "/" + actor + ".prefab"), root.scene);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    animator.avatar = rig.GetComponent<Animator>().avatar;
                    foreach (Transform child in rig.transform.Cast<Transform>().ToArray())
                    {
                        child.SetParent(visual, false);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(rig);
                }
                visual.name = actor + " Visuals";
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
                visual.localScale = Vector3.one;
                AnimatorController controller = CreateController(actor, folder, parameters, actions);
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = true;
                AICharacterAttackAction[] attackActions = CreateAttacks(actor, folder, actions);
                var sourceCombat = visual.gameObject.AddComponent<OriginalCreatureCombat>();
                var sourceData = new SerializedObject(sourceCombat);
                sourceData.FindProperty("m_source").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(Output + "/CombatTimelines.json");
                sourceData.FindProperty("m_actor").stringValue = actor;
                sourceData.FindProperty("m_motionRoot").objectReferenceValue = visual.Find("Master");
                sourceData.FindProperty("m_hurtBoxLayers").intValue = 1 << root.layer;
                sourceData.ApplyModifiedPropertiesWithoutUndo();
                AICharacterAnimatorManager animationManager = visual.GetComponent<AICharacterAnimatorManager>();
                var animation = new SerializedObject(animationManager);
                animation.FindProperty("m_animator").objectReferenceValue = animator;
                foreach (string direction in new[] { "Forward", "Backward", "Left", "Right" })
                {
                    SetArray(animation.FindProperty("m_hit" + direction + "Animations"), new[] { Clip(actor, "a000_005000") });
                    animation.FindProperty("m_ping" + direction + "Animations").ClearArray();
                }
                animation.ApplyModifiedPropertiesWithoutUndo();
                AICharacterManager ai = root.GetComponent<AICharacterManager>();
                var manager = new SerializedObject(ai);
                SetArray(manager.FindProperty("m_combatStanceAttacks"), attackActions);
                manager.FindProperty("m_defaultAttackAction").objectReferenceValue = attackActions[0];
                manager.FindProperty("m_aiAnimatorManager").objectReferenceValue = animationManager;
                manager.FindProperty("m_animator").objectReferenceValue = animator;
                manager.FindProperty("m_characterAnimatorManager").objectReferenceValue = animationManager;
                manager.FindProperty("m_lockOnTransform").objectReferenceValue = visual.GetComponentsInChildren<Transform>(true)
                    .Single(bone => bone.name == "Head");
                manager.FindProperty("m_detectionRadius").floatValue = think.Value<float>("eye_dist");
                manager.FindProperty("m_fieldOfView").floatValue = think.Value<float>("eye_angY") * 2f;
                manager.FindProperty("m_eyeHeight").floatValue = npc.Value<float>("hitHeight") * 0.8f;
                manager.FindProperty("m_walkSpeed").floatValue = Speed(actions, 2000);
                manager.FindProperty("m_runSpeed").floatValue = Speed(actions, 2100);
                manager.FindProperty("m_sleepingAnimation").stringValue = actor == "c3970" ? "a000_030003" : "a000_030000";
                manager.FindProperty("m_wakingAnimation").stringValue = actor == "c3970" ? "a000_020003" : "a000_020000";
                manager.ApplyModifiedPropertiesWithoutUndo();
                var combat = new SerializedObject(root.GetComponent<AICharacterCombatManager>());
                combat.FindProperty("m_leftHandDamageCollider").objectReferenceValue = null;
                combat.FindProperty("m_rightHandDamageCollider").objectReferenceValue = null;
                combat.ApplyModifiedPropertiesWithoutUndo();
                float radius = npc.Value<float>("chrHitRadius");
                float height = Mathf.Max(npc.Value<float>("chrHitHeight"), radius * 2f);
                foreach (CapsuleCollider capsule in root.GetComponents<CapsuleCollider>())
                {
                    capsule.radius = radius;
                    capsule.height = height;
                    capsule.center = Vector3.up * height * 0.5f;
                }
                CapsuleCollider hurtBox = root.transform.Find("Combat HurtBox").GetComponent<CapsuleCollider>();
                hurtBox.radius = radius;
                hurtBox.height = height;
                hurtBox.center = Vector3.up * height * 0.5f;
                NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
                agent.radius = radius;
                agent.height = height;
                if (actor == "c3970")
                {
                    ConfigureBoss(root, folder, attackActions.Cast<BossAttackData>().ToArray(), npc, gameplay, actions);
                }
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + actor + " Gameplay.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimatorController CreateController(string actor, string folder,
            AnimatorControllerParameter[] parameters, JToken[] actions)
        {
            string path = folder + "/" + actor + " Gameplay.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ??
                AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorControllerLayer[] oldLayers = controller.layers;
            controller.layers = Array.Empty<AnimatorControllerLayer>();
            foreach (AnimatorControllerLayer old in oldLayers)
            {
                Object.DestroyImmediate(old.stateMachine, true);
            }
            var locomotion = new AnimatorStateMachine { name = "Base Layer" };
            var actionLayer = new AnimatorStateMachine { name = "Action Override" };
            AssetDatabase.AddObjectToAsset(locomotion, controller);
            AssetDatabase.AddObjectToAsset(actionLayer, controller);
            controller.layers = new[]
            {
                new AnimatorControllerLayer { name = "Base Layer", stateMachine = locomotion, defaultWeight = 1f },
                new AnimatorControllerLayer { name = "Action Override", stateMachine = actionLayer, defaultWeight = 1f }
            };
            controller.parameters = parameters;
            var tree = new BlendTree { name = "Original movement", blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "Horizontal", blendParameterY = "Vertical", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach (var entry in new[] { (0, Vector2.zero), (2000, new Vector2(0f, 0.5f)),
                (2001, new Vector2(0f, -0.5f)), (2002, new Vector2(-0.5f, 0f)),
                (2003, new Vector2(0.5f, 0f)), (2100, Vector2.up) })
            {
                AnimationClip clip = Clip(actor, "a000_" + entry.Item1.ToString("000000"));
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.SaveAssetIfDirty(clip);
                tree.AddChild(clip, entry.Item2);
            }
            AnimatorState movement = locomotion.AddState("Locomotion");
            movement.motion = tree;
            movement.writeDefaultValues = false;
            locomotion.defaultState = movement;
            AnimatorState empty = actionLayer.AddState("Empty");
            empty.writeDefaultValues = false;
            empty.AddStateMachineBehaviour<ResetActionFlags>();
            actionLayer.defaultState = empty;
            foreach (JToken action in actions.Where(row => row.Value<int>("AnimationID") is >= 3000 and < 4000 or
                >= 5000 and < 7020 or >= 10000 and < 10062 or >= 20000 and < 21000 or >= 30000 and < 31000))
            {
                int id = action.Value<int>("AnimationID");
                AddState(actionLayer, empty, Name(id), Clip(actor, action.Value<string>("PoseSource")),
                    id < 10000 || id is >= 20000 and < 21000);
            }
            AddState(actionLayer, empty, "Dead_01", Clip(actor, "a000_010000"), false);
            AddState(actionLayer, empty, "Guard_Break_01", Clip(actor, "a000_007000"), true);
            // shortcut: wolves use their supported recoil until the original stagger selector is ported.
            JToken stagger = actions.FirstOrDefault(row => row.Value<int>("AnimationID") == 7010) ??
                actions.Single(row => row.Value<int>("AnimationID") == 7000);
            AddState(actionLayer, empty, "Stance_Break_01", Clip(actor, stagger.Value<string>("PoseSource")), true);
            foreach (string id in new[] { "a000_005000", "a000_005001", "a000_005002", "a000_005003" })
            {
                AnimationClip clip = Clip(actor, id);
                AddState(actionLayer, empty, clip.name, clip, true);
            }
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static void AddState(AnimatorStateMachine machine, AnimatorState empty, string name,
            AnimationClip clip, bool returnsToIdle)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = false;
            if (returnsToIdle)
            {
                AnimatorStateTransition transition = state.AddTransition(empty);
                transition.hasExitTime = true;
                transition.exitTime = 1f;
                transition.duration = 0.08f;
                transition.hasFixedDuration = true;
            }
        }

        private static AICharacterAttackAction[] CreateAttacks(string actor, string folder, JToken[] actions)
        {
            var result = new List<AICharacterAttackAction>();
            foreach (JToken action in actions.Where(row => row.Value<int>("AnimationID") is >= 3000 and < 3040 &&
                row["Hits"].Any()).OrderBy(row => row.Value<int>("AnimationID")))
            {
                int id = action.Value<int>("AnimationID");
                string path = folder + "/" + Name(id);
                AttackDefinition definition = LoadOrCreate<AttackDefinition>(path + " Definition.asset");
                var data = new SerializedObject(definition);
                data.FindProperty("m_attackId").longValue = (long)int.Parse(actor.Substring(1)) * 1000000 + id;
                data.FindProperty("m_attackType").intValue = (int)AttackType.LightAttack01;
                data.FindProperty("m_animationState").stringValue = "Action Override." + Name(id);
                float duration = action.Value<float>("Duration");
                float first = action["Hits"].Min(hit => hit.Value<float>("StartSeconds"));
                float end = action["Hits"].Max(hit => hit.Value<float>("EndSeconds"));
                data.FindProperty("m_activePhase").FindPropertyRelative("m_start").floatValue = first / duration;
                data.FindProperty("m_activePhase").FindPropertyRelative("m_end").floatValue = end / duration;
                data.FindProperty("m_recoveryPhase").FindPropertyRelative("m_start").floatValue = end / duration;
                data.FindProperty("m_recoveryPhase").FindPropertyRelative("m_end").floatValue = 1f;
                JToken hit = action["Hits"].First;
                foreach (var field in new[] { ("m_physicalDamage", "PhysicalDamage"), ("m_magicDamage", "MagicDamage"),
                    ("m_fireDamage", "FireDamage"), ("m_lightningDamage", "LightningDamage"),
                    ("m_holyDamage", "HolyDamage"), ("m_poiseDamage", "PoiseDamage") })
                {
                    data.FindProperty(field.Item1).floatValue = hit.Value<float>(field.Item2);
                }
                data.FindProperty("m_parryLevel").intValue = hit.Value<bool>("Parryable") ? 1 : 0;
                data.FindProperty("m_isBlockable").boolValue = hit.Value<bool>("Blockable");
                data.ApplyModifiedPropertiesWithoutUndo();
                AICharacterAttackAction attack = actor == "c3970"
                    ? LoadOrCreate<BossAttackData>(path + " AI.asset")
                    : LoadOrCreate<AICharacterAttackAction>(path + " AI.asset");
                var selection = new SerializedObject(attack);
                selection.FindProperty("m_attackDefinition").objectReferenceValue = definition;
                selection.FindProperty("m_minimumRange").floatValue = 0f;
                selection.FindProperty("m_maximumRange").floatValue = actor == "c3970" ? 3.5f : 2.5f;
                selection.FindProperty("m_recoveryTime").floatValue = duration;
                selection.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(definition);
                AssetDatabase.SaveAssetIfDirty(attack);
                result.Add(attack);
            }
            if (result.Count == 0)
            {
                throw new InvalidDataException("No source melee actions: " + actor);
            }
            return result.ToArray();
        }

        private static void ConfigureBoss(GameObject root, string folder, BossAttackData[] attacks,
            JObject npc, JObject gameplay, JToken[] actions)
        {
            BossPhaseData phase = LoadOrCreate<BossPhaseData>(folder + "/Original Phase.asset");
            var phaseData = new SerializedObject(phase);
            SetArray(phaseData.FindProperty("m_attacks"), attacks);
            phaseData.FindProperty("m_movementSpeed").floatValue = Speed(actions, 2100);
            phaseData.ApplyModifiedPropertiesWithoutUndo();
            BossCharacterManager boss = root.AddComponent<BossCharacterManager>();
            var data = new SerializedObject(boss);
            data.FindProperty("m_bossID").intValue = gameplay["Enemies"].Single(row =>
                row.Value<string>("ModelName") == "c3970").Value<int>("EntityID");
            data.FindProperty("m_bossName").stringValue = "法姆·亚兹拉的兽人";
            data.FindProperty("m_maximumHealth").floatValue = CalculatePassiveHealth(npc, gameplay);
            SetArray(data.FindProperty("m_phases"), new[] { phase });
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(phase);
        }

        /// <summary>Applies the NPC's direct passive HP rates for a new game, excluding GameClear effects.</summary>
        public static float CalculatePassiveHealth(JObject npc, JObject gameplay)
        {
            float health = npc.Value<float>("hp");
            foreach (JProperty property in npc.Properties().Where(property => property.Name.StartsWith("spEffectID") &&
                property.Value.Value<int>() > 0))
            {
                JToken effect = gameplay["Parameters"]["SpEffectParam"].Single(row =>
                    row.Value<int>("ID") == property.Value.Value<int>());
                health *= effect["Fields"].Value<float>("maxHpRate");
            }
            if (!float.IsFinite(health) || health < 1f)
            {
                throw new InvalidDataException("Original NPC has invalid passive health.");
            }
            return Mathf.Round(health);
        }

        private static float Speed(JToken[] actions, int id)
        {
            JToken action = actions.Single(row => row.Value<int>("AnimationID") == id);
            JToken first = action["RootMotion"].First;
            JToken last = action["RootMotion"].Last;
            return Vector2.Distance(new Vector2(first.Value<float>("x"), first.Value<float>("z")),
                new Vector2(last.Value<float>("x"), last.Value<float>("z"))) / action.Value<float>("Duration");
        }

        private static string Name(int id) => "a" + (id / 1000000).ToString("000") + "_" + (id % 1000000).ToString("000000");

        private static AnimationClip Clip(string actor, string name)
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(k_Rigs + "/" + actor + "/Animations/" + name + ".anim") ??
                throw new InvalidDataException("Missing verified original clip: " + actor + "/" + name);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void SetArray<T>(SerializedProperty property, IReadOnlyList<T> items) where T : Object
        {
            property.arraySize = items.Count;
            for (int index = 0; index < items.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = items[index];
            }
        }
    }
}
