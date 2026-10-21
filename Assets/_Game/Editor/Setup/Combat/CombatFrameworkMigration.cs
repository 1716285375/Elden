using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Creates editable movesets from existing weapon balance without replacing authored migrations.</summary>
    public static class CombatFrameworkMigration
    {
        private const string k_Root = "Assets/_Game/Data/Combat/Movesets";

        [MenuItem("Tools/ZZ/Combat/Migrate Weapon Movesets")]
        public static void Migrate()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play before migrating weapon assets.");
            }

            EnsureFolder(k_Root);
            int migrated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:MeleeWeaponItem", new[] { "Assets/_Game" }))
            {
                WeaponItem weapon = AssetDatabase.LoadAssetAtPath<WeaponItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon == null || weapon.Moveset != null)
                {
                    continue;
                }

                string folder = k_Root + "/" + guid;
                EnsureFolder(folder);
                var attacks = new Dictionary<AttackType, AttackDefinition>();
                foreach (AttackType type in Enum.GetValues(typeof(AttackType)))
                {
                    string path = folder + "/" + type + ".asset";
                    AttackDefinition attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
                    if (attack == null)
                    {
                        attack = ScriptableObject.CreateInstance<AttackDefinition>();
                        var serialized = new SerializedObject(attack);
                        serialized.FindProperty("m_attackType").intValue = (int)type;
                        serialized.FindProperty("m_staminaCost").floatValue = weapon.GetAttackStaminaCost(type);
                        serialized.FindProperty("m_damageMultiplier").floatValue = weapon.GetAttackDamageModifier(type);
                        serialized.FindProperty("m_poiseDamage").floatValue = weapon.BasePoiseDamage;
                        serialized.FindProperty("m_stanceDamage").floatValue = weapon.BasePoiseDamage;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        AssetDatabase.CreateAsset(attack, path);
                    }

                    attacks.Add(type, attack);
                }

                Link(attacks, AttackType.LightAttack01, AttackType.LightAttack02, "m_nextLightAttack");
                Link(attacks, AttackType.LightAttack02, AttackType.LightAttack03, "m_nextLightAttack");
                Link(attacks, AttackType.HeavyAttack01, AttackType.HeavyAttack02, "m_nextHeavyAttack");
                Link(attacks, AttackType.ChargedAttack01, AttackType.HeavyAttack02, "m_nextHeavyAttack");
                WeaponMoveset moveset = AssetDatabase.LoadAssetAtPath<WeaponMoveset>(folder + "/Moveset.asset");
                if (moveset == null)
                {
                    moveset = ScriptableObject.CreateInstance<WeaponMoveset>();
                    AssetDatabase.CreateAsset(moveset, folder + "/Moveset.asset");
                }

                var movesetData = new SerializedObject(moveset);
                SerializedProperty entries = movesetData.FindProperty("m_attacks");
                entries.arraySize = attacks.Count;
                int index = 0;
                foreach (AttackDefinition attack in attacks.Values)
                {
                    entries.GetArrayElementAtIndex(index++).objectReferenceValue = attack;
                    AssetDatabase.SaveAssetIfDirty(attack);
                }

                movesetData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(moveset);
                var weaponData = new SerializedObject(weapon);
                weaponData.FindProperty("m_moveset").objectReferenceValue = moveset;
                weaponData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(weapon);
                migrated++;
            }

            Debug.Log($"Combat moveset migration completed: {migrated} weapons.");
        }

        [MenuItem("Tools/ZZ/Combat/Migrate Shared Combat Assets")]
        public static void MigrateSharedCombatAssets()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play before migrating combat assets.");
            }

            EnsureFolder(k_Root + "/AI");
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AICharacterAttackAction", new[] { "Assets/_Game" }))
            {
                var action = AssetDatabase.LoadAssetAtPath<AICharacterAttackAction>(AssetDatabase.GUIDToAssetPath(guid));
                if (action == null || action.AttackDefinition != null)
                {
                    continue;
                }

                var definition = ScriptableObject.CreateInstance<AttackDefinition>();
                var data = new SerializedObject(definition);
                data.FindProperty("m_attackType").intValue = (int)action.AttackType;
                data.FindProperty("m_physicalDamage").floatValue = action.PhysicalDamage;
                data.FindProperty("m_magicDamage").floatValue = action.MagicDamage;
                data.FindProperty("m_fireDamage").floatValue = action.FireDamage;
                data.FindProperty("m_lightningDamage").floatValue = action.LightningDamage;
                data.FindProperty("m_holyDamage").floatValue = action.HolyDamage;
                data.FindProperty("m_poiseDamage").floatValue = action.PoiseDamage;
                data.FindProperty("m_stanceDamage").floatValue = action.PoiseDamage;
                data.FindProperty("m_parryLevel").intValue = action.IsParryable ? 1 : 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, k_Root + "/AI/" + guid + ".asset");
                var actionData = new SerializedObject(action);
                actionData.FindProperty("m_attackDefinition").objectReferenceValue = definition;
                actionData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(action);
                count++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AttackDefinition", new[] { k_Root }))
            {
                var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var data = new SerializedObject(attack);
                SerializedProperty version = data.FindProperty("m_defenseRulesVersion");
                if (version.intValue > 0)
                {
                    continue;
                }

                bool heavy = attack.AttackType == AttackType.ChargedAttack01 ||
                    attack.AttackType == AttackType.HeavyAttack01 || attack.AttackType == AttackType.HeavyAttack02;
                data.FindProperty("m_hyperArmorPoise").floatValue = heavy ? 20f : 0f;
                version.intValue = 1;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(attack);
            }

            string bleedPath = "Assets/_Game/Data/Combat/Bleed.asset";
            var bleed = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(bleedPath);
            if (bleed == null)
            {
                bleed = ScriptableObject.CreateInstance<StatusEffectDefinition>();
                var data = new SerializedObject(bleed);
                data.FindProperty("m_status").intValue = (int)Buildup.Bleed;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(bleed, bleedPath);
            }

            MigrateTimedStatus<PoisonedEffect>(Buildup.Poison, "Poison", "m_poisonDamage");
            MigrateTimedStatus<FrostbiteEffect>(Buildup.Frost, "Frost", "m_hpPercentageDamage");

            int bodies = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Characters" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Backup"))
                {
                    continue;
                }

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponent<CharacterManager>() == null)
                {
                    continue;
                }

                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CharacterManager owner = prefab.GetComponent<CharacterManager>();
                    var effects = prefab.GetComponent<CharacterEffectsManager>();
                    if (effects != null)
                    {
                        var data = new SerializedObject(effects);
                        data.FindProperty("m_bleedDefinition").objectReferenceValue = bleed;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }

                    if (prefab.GetComponentInChildren<HurtBox>(true) == null)
                    {
                        var body = new GameObject("Combat HurtBox");
                        body.layer = prefab.layer;
                        body.transform.SetParent(prefab.transform, false);
                        var capsule = body.AddComponent<CapsuleCollider>();
                        CharacterController movement = prefab.GetComponent<CharacterController>();
                        capsule.center = movement != null ? movement.center : Vector3.up;
                        capsule.height = movement != null ? movement.height : 2f;
                        capsule.radius = movement != null ? movement.radius : 0.35f;
                        capsule.isTrigger = true;
                        var hurtBox = body.AddComponent<HurtBox>();
                        var data = new SerializedObject(hurtBox);
                        data.FindProperty("m_owner").objectReferenceValue = owner;
                        data.ApplyModifiedPropertiesWithoutUndo();
                        bodies++;
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }

            Debug.Log($"Shared combat migration: {count} AI definitions; {bodies} character HurtBoxes.");
        }

        private static void MigrateTimedStatus<T>(Buildup status, string name, string sourceProperty)
            where T : TimedCharacterEffect
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Game" }))
            {
                T effect = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                var effectData = new SerializedObject(effect);
                if (effectData.FindProperty("m_statusDefinition").objectReferenceValue != null)
                {
                    continue;
                }

                string path = "Assets/_Game/Data/Combat/" + name + "_" + guid + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<StatusEffectDefinition>();
                    var data = new SerializedObject(definition);
                    data.FindProperty("m_status").intValue = (int)status;
                    data.FindProperty("m_flatDamage").floatValue = status == Buildup.Poison
                        ? effectData.FindProperty(sourceProperty).floatValue : 0f;
                    data.FindProperty("m_maxHealthPercent").floatValue = status == Buildup.Frost
                        ? effectData.FindProperty(sourceProperty).floatValue : 0f;
                    data.FindProperty("m_timedEffect").objectReferenceValue = effect;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(definition, path);
                }

                effectData.FindProperty("m_statusDefinition").objectReferenceValue = definition;
                effectData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(effect);
            }
        }

        private static void Link(Dictionary<AttackType, AttackDefinition> attacks,
            AttackType from, AttackType to, string property)
        {
            var serialized = new SerializedObject(attacks[from]);
            serialized.FindProperty(property).objectReferenceValue = attacks[to];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
