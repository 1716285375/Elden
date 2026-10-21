using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Authors creature movesets without changing their rigs or shared animation controller.</summary>
    public static class EnemyCombatVarietySetup
    {
        private const string k_Output = "Assets/_Game/Data/Combat/EnemyMovesets";
        private const string k_Variants = "Assets/_Game/Prefabs/Characters/AI/Variants/";
        private const string k_Controller =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";

        [MenuItem("Tools/ZZ/Combat/Build Creature Movesets")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before authoring movesets.");
            }
            Directory.CreateDirectory(k_Output);
            AssetDatabase.Refresh();
            var clips = AssetDatabase.FindAssets("t:AnimationClip", new[]
                { "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/General" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".anim", StringComparison.Ordinal))
                .ToDictionary(Path.GetFileNameWithoutExtension, path =>
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(path));
            uint nextId = AssetDatabase.FindAssets("t:AttackDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<AttackDefinition>(path).AttackId)
                .DefaultIfEmpty(0u).Max() + 1u;
            var report = new StringBuilder();
            Build("Skeleton_00_Unarmed Enemy", "Skeleton Boxer", "main", 18f, 1.35f, 1.65f, 75f, clips, ref nextId, report);
            Build("Imp_01 Enemy", "Imp Ambusher", "off", 14f, 0.85f, 1.25f, 90f, clips, ref nextId, report);
            Build("Grave_Tender_01 Boss", "Grave Brawler", "th", 30f, 1.8f, 2.15f, 65f, clips, ref nextId, report);
            Build("Golem_01 Boss", "Golem Crusher", "th", 42f, 3.2f, 2.6f, 20f, clips, ref nextId, report);
            Build("Corpse_Ghost_01 Boss", "Ghost Duelist", "off", 24f, 1.25f, 2.05f, 85f, clips, ref nextId, report);
            Build("Bell_Keeper_01 Boss", "Bell Enforcer", "main", 34f, 2.4f, 2.35f, 45f, clips, ref nextId, report);
            Build("Durk_01 Boss", "Durk Berserker", "dw", 28f, 1.5f, 2.5f, 95f, clips, ref nextId, report);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/CombatRefresh");
            File.WriteAllText("Docs/CombatRefresh/enemy-movesets.txt", report.ToString());
        }

        private static void Build(string prefabName, string role, string stance, float damage,
            float recovery, float reach, float comboChance, Dictionary<string, AnimationClip> sources,
            ref uint nextId, StringBuilder report)
        {
            string folder = k_Output + "/" + role;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            AnimationClip[] originals = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_Controller)
                .animationClips.Distinct().Where(clip => clip.name.StartsWith("zombie_") &&
                    (clip.name.Contains("attack") || clip.name.Contains("swipe")))
                .OrderBy(clip => clip.name).ToArray();
            if (originals.Length != 3)
            {
                throw new InvalidOperationException("Expected three legacy attack slots.");
            }
            var controller = LoadOrCreate<AnimatorOverrideController>(folder + "/Controller.overrideController");
            string basePath = folder + "/Creature.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(basePath) == null &&
                !AssetDatabase.CopyAsset(k_Controller, basePath))
            {
                throw new IOException("Cannot create creature controller: " + basePath);
            }
            var creatureController = AssetDatabase.LoadAssetAtPath<AnimatorController>(basePath);
            float speed = role == "Golem Crusher" ? 0.65f : role == "Bell Enforcer" ? 0.8f :
                role == "Grave Brawler" ? 0.9f : role == "Imp Ambusher" ? 1.1f : 1f;
            foreach (AnimatorState state in creatureController.layers.SelectMany(layer => States(layer.stateMachine)))
            {
                if (state.name == "Attack_01" || state.name == "Attack_02" || state.name == "Attack_Light_02")
                {
                    state.speed = speed;
                    EditorUtility.SetDirty(state);
                }
            }
            controller.runtimeAnimatorController = creatureController;
            var actions = new AICharacterAttackAction[3];
            bool boss = prefabName.EndsWith("Boss", StringComparison.Ordinal);
            string[] sourceNames = { "unarmed_" + stance + "_light_attack_01",
                "unarmed_" + stance + "_light_attack_02",
                "unarmed_" + (stance == "th" ? "th" : "main") + "_charged_attack_01_release" };
            for (int index = 0; index < 3; index++)
            {
                AnimationClip source = sources[sourceNames[index]];
                if (!source.humanMotion)
                {
                    throw new InvalidOperationException(source.name + " is not humanoid motion.");
                }
                AnimationClip clip = Adapt(source, folder + "/Attack " + (index + 1) + ".anim", stance);
                controller[originals[index]] = clip;
                AttackType type = index == 0 ? AttackType.LightAttack01 :
                    index == 1 ? AttackType.HeavyAttack01 : AttackType.LightAttack02;
                var definition = LoadOrCreate<AttackDefinition>(folder + "/Definition " + (index + 1) + ".asset");
                var data = new SerializedObject(definition);
                if (definition.AttackId == 0u)
                {
                    data.FindProperty("m_attackId").longValue = nextId++;
                }
                data.FindProperty("m_attackType").intValue = (int)type;
                data.FindProperty("m_physicalDamage").floatValue = damage * (index == 2 ? 1.5f : 1f);
                data.FindProperty("m_poiseDamage").floatValue = damage * (index == 2 ? 1.2f : 0.5f);
                data.FindProperty("m_stanceDamage").floatValue = damage * (index == 2 ? 1.5f : 0.7f);
                data.FindProperty("m_parryLevel").intValue = index == 2 ? 0 : 1;
                SetWindow(data.FindProperty("m_activePhase"), clip, "Open", "Close");
                SerializedProperty active = data.FindProperty("m_activePhase");
                SerializedProperty recoveryWindow = data.FindProperty("m_recoveryPhase");
                recoveryWindow.FindPropertyRelative("m_start").floatValue = active.FindPropertyRelative("m_end").floatValue;
                recoveryWindow.FindPropertyRelative("m_end").floatValue = 1f;
                data.ApplyModifiedPropertiesWithoutUndo();
                string actionPath = folder + "/Action " + (index + 1) + ".asset";
                AICharacterAttackAction action = AssetDatabase.LoadAssetAtPath<AICharacterAttackAction>(actionPath);
                if (action == null)
                {
                    action = boss ? ScriptableObject.CreateInstance<BossAttackData>() :
                        ScriptableObject.CreateInstance<AICharacterAttackAction>();
                    AssetDatabase.CreateAsset(action, actionPath);
                }
                data = new SerializedObject(action);
                data.FindProperty("m_attackDefinition").objectReferenceValue = definition;
                data.FindProperty("m_attackType").intValue = (int)type;
                data.FindProperty("m_maximumRange").floatValue = reach;
                data.FindProperty("m_selectionWeight").floatValue = index == 2 ? 0.35f : 1f;
                data.FindProperty("m_recoveryTime").floatValue = Mathf.Max(recovery, clip.length / speed + 0.2f);
                data.ApplyModifiedPropertiesWithoutUndo();
                actions[index] = action;
                report.AppendLine($"{role}: {type}; {source.name}; duration={clip.length / speed:F3}s; speed={speed:F2}; damage={definition.PhysicalDamage}; recovery={action.RecoveryTime:F2}s");
            }
            for (int index = 0; index < 2; index++)
            {
                var data = new SerializedObject(actions[index]);
                data.FindProperty("m_comboAction").objectReferenceValue = actions[index + 1];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(controller);
            string prefabPath = k_Variants + prefabName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.GetComponentInChildren<Animator>(true).runtimeAnimatorController = controller;
                var ai = new SerializedObject(root.GetComponent<AICharacterManager>());
                ai.FindProperty("m_defaultAttackAction").objectReferenceValue = actions[0];
                Assign(ai.FindProperty("m_combatStanceAttacks"), actions);
                ai.FindProperty("m_attackDistance").floatValue = reach;
                ai.FindProperty("m_canPerformCombo").boolValue = true;
                ai.FindProperty("m_chanceToPerformCombo").floatValue = comboChance;
                ai.FindProperty("m_onlyPerformComboIfInitialAttackHits").boolValue = true;
                ai.ApplyModifiedPropertiesWithoutUndo();
                if (boss)
                {
                    var bossData = new SerializedObject(root.GetComponent<BossCharacterManager>());
                    var phases = new BossPhaseData[2];
                    for (int phaseIndex = 0; phaseIndex < 2; phaseIndex++)
                    {
                        phases[phaseIndex] = LoadOrCreate<BossPhaseData>(folder + "/Phase " + phaseIndex + ".asset");
                        var phase = new SerializedObject(phases[phaseIndex]);
                        phase.FindProperty("m_healthThreshold").floatValue = phaseIndex == 0 ? 1f : 0.5f;
                        phase.FindProperty("m_movementSpeed").floatValue = role == "Golem Crusher" ? 1.3f : 2.4f + phaseIndex * 0.4f;
                        Assign(phase.FindProperty("m_attacks"), phaseIndex == 0 ? actions.Take(2).ToArray() : actions);
                        phase.ApplyModifiedPropertiesWithoutUndo();
                    }
                    Assign(bossData.FindProperty("m_phases"), phases);
                    bossData.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimationClip Adapt(AnimationClip source, string path, string stance)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, clip);
            }
            string hand = source.name.Contains("_off_") ? "Left" : "Right";
            var events = new List<AnimationEvent> { new AnimationEvent { functionName = "SetSwipeAttackDamage", time = 0f } };
            foreach (AnimationEvent original in AnimationUtility.GetAnimationEvents(source))
            {
                string function = original.functionName;
                if (function == "OpenOffHandDamageCollider")
                {
                    function = "OpenLeftHandDamageCollider";
                }
                else if (function == "CloseOffHandDamageCollider")
                {
                    function = "CloseLeftHandDamageCollider";
                }
                else if (function == "OpenMainHandDamageCollider")
                {
                    function = "OpenRightHandDamageCollider";
                }
                else if (function == "CloseMainHandDamageCollider")
                {
                    function = "CloseRightHandDamageCollider";
                }
                else if (function == "OpenDamageCollider")
                {
                    function = "Open" + hand + "HandDamageCollider";
                }
                else if (function == "CloseDamageCollider")
                {
                    function = "Close" + hand + "HandDamageCollider";
                }
                else if (function != "EnableCanRotate" && function != "DisableCanRotate" &&
                    function != "EnableCanDoCombo" && function != "DisableCanDoCombo")
                {
                    continue;
                }
                events.Add(new AnimationEvent { time = original.time, functionName = function });
            }
            if (!events.Any(item => item.functionName.StartsWith("Open", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(source.name + " has no authored hit window.");
            }
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(item => item.time).ToArray());
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SetWindow(SerializedProperty window, AnimationClip clip, string open, string close)
        {
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            window.FindPropertyRelative("m_start").floatValue = events.First(item => item.functionName.StartsWith(open)).time / clip.length;
            window.FindPropertyRelative("m_end").floatValue = events.Last(item => item.functionName.StartsWith(close)).time / clip.length;
        }

        private static void Assign(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                yield return child.state;
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                foreach (AnimatorState state in States(child.stateMachine))
                {
                    yield return state;
                }
            }
        }

        private static T LoadOrCreate<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = typeof(T) == typeof(AnimatorOverrideController)
                    ? new AnimatorOverrideController() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
    }
}
