using System;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    public static class BossFightSystemSetup
    {
        private const int k_BossID = 1001;
        private const string k_BossName = "FALLEN WATCHER";
        private const string k_AIAnimatorControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_SourceAIPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_NetworkPrefabsPath = "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";
        private const string k_WorldScenePath = WorldScenePathLayout.MasterScenePath;
        private const string k_BossDataFolder = "Assets/Data/AI/Boss/Fallen Watcher";
        private const string k_FogMaterialPath =
            "Assets/Data/Materials/Fallen Watcher Fog Wall.mat";
        private const string k_BaseAttackClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Combat/General/" +
            "zombie_light_attack_01.anim";
        private const string k_SweepAttackClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Combat/General/" +
            "zombie_swipe_attack_01.anim";
        private const string k_FrenzyAttackClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Combat/General/" +
            "zombie_swipe_attack_02.anim";
        private const string k_PhaseTransitionClipPath =
            "Assets/Art/Animations/Characters/Creatures/Undead/Actions/" +
            "zombie_alert_to_aggro_02.anim";
        private const string k_LightAttackPath =
            k_BossDataFolder + "/Watcher Claw.asset";
        private const string k_SweepAttackPath =
            k_BossDataFolder + "/Watcher Sweep.asset";
        private const string k_FrenzyAttackPath =
            k_BossDataFolder + "/Watcher Frenzy.asset";
        private const string k_PhaseOnePath =
            k_BossDataFolder + "/Watcher Phase 01.asset";
        private const string k_PhaseTwoPath =
            k_BossDataFolder + "/Watcher Phase 02.asset";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_SweepStateName = "Attack_02";
        private const string k_FrenzyStateName = "Attack_Light_02";
        private const string k_PhaseTransitionStateName = "Boss_Phase_Transition";
        private const string k_BossArenaName = "Fallen Watcher Boss Arena";
        private const string k_BossSpawnerName = "Fallen Watcher Boss Spawner";
        private const string k_FogWallName = "Fallen Watcher Fog Wall";
        private const string k_BossUIName = "Boss Health Bar";

        private static readonly Color s_fogColor =
            new Color(0.36f, 0.12f, 0.55f, 0.72f);
        private static readonly Color s_panelColor =
            new Color(0.015f, 0.012f, 0.01f, 0.82f);
        private static readonly Color s_healthColor =
            new Color(0.56f, 0.025f, 0.025f, 1f);
        private static readonly Color s_goldColor =
            new Color(0.78f, 0.68f, 0.48f, 1f);

        [MenuItem("Tools/Elden/Configure Boss Fight System")]
        public static void ConfigureBossFightSystem()
        {
            EnsureFolder(k_BossDataFolder);
            EnsureFolder("Assets/Data/Materials");

            BossAttackData lightAttack = ConfigureAttack(
                k_LightAttackPath,
                AttackType.LightAttack01,
                0.6f,
                2.35f,
                3f,
                2.15f,
                36f,
                20f);
            BossAttackData sweepAttack = ConfigureAttack(
                k_SweepAttackPath,
                AttackType.HeavyAttack01,
                1.15f,
                3.25f,
                2f,
                2.8f,
                52f,
                34f);
            BossAttackData frenzyAttack = ConfigureAttack(
                k_FrenzyAttackPath,
                AttackType.LightAttack02,
                0.45f,
                2.75f,
                3.5f,
                1.55f,
                44f,
                27f);
            BossPhaseData phaseOne = ConfigurePhase(
                k_PhaseOnePath,
                1f,
                2.65f,
                lightAttack,
                sweepAttack);
            BossPhaseData phaseTwo = ConfigurePhase(
                k_PhaseTwoPath,
                0.5f,
                3.4f,
                lightAttack,
                sweepAttack,
                frenzyAttack);

            ConfigureAnimatorController();
            GameObject bossPrefab = ConfigureBossPrefab(phaseOne, phaseTwo);
            RegisterNetworkPrefab(bossPrefab);
            ConfigureWorldAIManagerPrefab(bossPrefab);
            ConfigurePlayerUIManagerPrefab();
            AssetDatabase.SaveAssets();
            ValidateBossFightSystem();
            Debug.Log(
                "[BossFightSystemSetup] Configured network activation, fog wall, " +
                "data-driven attacks, phases, HUD, death cleanup, and persistence.");
        }

        [MenuItem("Tools/Elden/Validate Boss Fight System")]
        public static void ValidateBossFightSystem()
        {
            ValidateAttackAndPhaseData();
            ValidateAnimatorController();
            ValidateBossPrefab();
            ValidateWorldAIManagerPrefab();
            ValidatePlayerUIManagerPrefab();
            ValidateNetworkPrefabRegistration();
            ValidateWorldSceneConnection();
            Debug.Log(
                "[BossFightSystemValidation] Boss arena, phase attacks, network state, " +
                "HUD, fog lifecycle, and save identity are valid.");
        }

        private static BossAttackData ConfigureAttack(
            string assetPath,
            AttackType attackType,
            float minimumRange,
            float maximumRange,
            float selectionWeight,
            float recoveryTime,
            float physicalDamage,
            float poiseDamage)
        {
            BossAttackData attack = LoadOrCreateAsset<BossAttackData>(assetPath);
            SerializedObject serializedAttack = new SerializedObject(attack);
            SetEnum(serializedAttack, "m_attackType", (int)attackType);
            SetFloat(serializedAttack, "m_minimumRange", minimumRange);
            SetFloat(serializedAttack, "m_maximumRange", maximumRange);
            SetFloat(serializedAttack, "m_selectionWeight", selectionWeight);
            SetFloat(serializedAttack, "m_recoveryTime", recoveryTime);
            SetFloat(serializedAttack, "m_physicalDamage", physicalDamage);
            SetFloat(serializedAttack, "m_magicDamage", 0f);
            SetFloat(serializedAttack, "m_fireDamage", 0f);
            SetFloat(serializedAttack, "m_lightningDamage", 0f);
            SetFloat(serializedAttack, "m_holyDamage", 0f);
            SetFloat(serializedAttack, "m_poiseDamage", poiseDamage);
            serializedAttack.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            return attack;
        }

        private static BossPhaseData ConfigurePhase(
            string assetPath,
            float healthThreshold,
            float movementSpeed,
            params BossAttackData[] attacks)
        {
            BossPhaseData phase = LoadOrCreateAsset<BossPhaseData>(assetPath);
            SerializedObject serializedPhase = new SerializedObject(phase);
            SetFloat(serializedPhase, "m_healthThreshold", healthThreshold);
            SetFloat(serializedPhase, "m_movementSpeed", movementSpeed);
            SerializedProperty attackList = GetRequiredProperty(
                serializedPhase,
                "m_attacks");
            attackList.arraySize = attacks.Length;
            for (int attackIndex = 0; attackIndex < attacks.Length; attackIndex++)
            {
                attackList.GetArrayElementAtIndex(attackIndex).objectReferenceValue =
                    attacks[attackIndex];
            }

            serializedPhase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(phase);
            return phase;
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIAnimatorControllerPath);
            AnimatorControllerLayer actionLayer = controller.layers.FirstOrDefault(
                layer => layer.name == k_ActionLayerName) ??
                throw new InvalidOperationException(
                    $"Animator Controller is missing {k_ActionLayerName}.");
            AnimatorStateMachine stateMachine = actionLayer.stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);

            ConfigureActionState(
                stateMachine,
                emptyState,
                k_SweepStateName,
                k_SweepAttackClipPath,
                new Vector3(520f, 300f));
            ConfigureActionState(
                stateMachine,
                emptyState,
                k_FrenzyStateName,
                k_FrenzyAttackClipPath,
                new Vector3(520f, 390f));
            ConfigureActionState(
                stateMachine,
                emptyState,
                k_PhaseTransitionStateName,
                k_PhaseTransitionClipPath,
                new Vector3(520f, 480f));
            CopyAttackEvents(k_BaseAttackClipPath, k_SweepAttackClipPath);
            CopyAttackEvents(k_BaseAttackClipPath, k_FrenzyAttackClipPath);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureActionState(
            AnimatorStateMachine stateMachine,
            AnimatorState emptyState,
            string stateName,
            string clipPath,
            Vector3 position)
        {
            AnimatorState state = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(candidate => candidate.name == stateName) ??
                stateMachine.AddState(stateName, position);
            state.motion = LoadRequiredAsset<AnimationClip>(clipPath);
            AnimatorStateTransition transition = state.transitions
                .FirstOrDefault(candidate => candidate.destinationState == emptyState) ??
                state.AddTransition(emptyState);
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.hasFixedDuration = true;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(transition);
        }

        private static void CopyAttackEvents(string sourcePath, string targetPath)
        {
            AnimationClip source = LoadRequiredAsset<AnimationClip>(sourcePath);
            AnimationClip target = LoadRequiredAsset<AnimationClip>(targetPath);
            AnimationEvent[] sourceEvents = AnimationUtility.GetAnimationEvents(source);
            AnimationEvent[] targetEvents = new AnimationEvent[sourceEvents.Length];
            for (int eventIndex = 0; eventIndex < sourceEvents.Length; eventIndex++)
            {
                AnimationEvent sourceEvent = sourceEvents[eventIndex];
                float normalizedTime = source.length > 0f
                    ? sourceEvent.time / source.length
                    : 0f;
                targetEvents[eventIndex] = new AnimationEvent
                {
                    functionName = sourceEvent.functionName,
                    floatParameter = sourceEvent.floatParameter,
                    intParameter = sourceEvent.intParameter,
                    stringParameter = sourceEvent.stringParameter,
                    objectReferenceParameter = sourceEvent.objectReferenceParameter,
                    messageOptions = sourceEvent.messageOptions,
                    time = Mathf.Clamp01(normalizedTime) * target.length
                };
            }

            AnimationUtility.SetAnimationEvents(target, targetEvents);
            EditorUtility.SetDirty(target);
        }

        private static GameObject ConfigureBossPrefab(
            BossPhaseData phaseOne,
            BossPhaseData phaseTwo)
        {
            string sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(k_BossPrefabPath) != null
                ? k_BossPrefabPath
                : k_SourceAIPrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = "Fallen Watcher Boss";
                BossCharacterManager bossManager =
                    GetOrAddComponent<BossCharacterManager>(root);
                SerializedObject serializedBoss = new SerializedObject(bossManager);
                SetInteger(serializedBoss, "m_bossID", k_BossID);
                SetString(serializedBoss, "m_bossName", k_BossName);
                SetFloat(serializedBoss, "m_maximumHealth", 600f);
                SerializedProperty phases = GetRequiredProperty(
                    serializedBoss,
                    "m_phases");
                phases.arraySize = 2;
                phases.GetArrayElementAtIndex(0).objectReferenceValue = phaseOne;
                phases.GetArrayElementAtIndex(1).objectReferenceValue = phaseTwo;
                serializedBoss.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedAI = new SerializedObject(
                    GetRequiredComponent<AICharacterManager>(root));
                SetFloat(serializedAI, "m_detectionRadius", 32f);
                SetFloat(serializedAI, "m_loseTargetRadius", 42f);
                SetFloat(serializedAI, "m_combatStanceDistance", 3.25f);
                SetFloat(serializedAI, "m_attackDistance", 3.25f);
                serializedAI.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(root, k_BossPrefabPath) == null)
                {
                    throw new InvalidOperationException("Could not save the Boss prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return LoadRequiredAsset<GameObject>(k_BossPrefabPath);
        }

        private static void RegisterNetworkPrefab(GameObject bossPrefab)
        {
            NetworkPrefabsList prefabsList =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedObject serializedList = new SerializedObject(prefabsList);
            SerializedProperty entries = GetRequiredProperty(serializedList, "List");
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                SerializedProperty prefab = entries.GetArrayElementAtIndex(entryIndex)
                    .FindPropertyRelative("Prefab");
                if (prefab != null && prefab.objectReferenceValue == bossPrefab)
                {
                    return;
                }
            }

            int newEntryIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newEntryIndex);
            SerializedProperty newEntry = entries.GetArrayElementAtIndex(newEntryIndex);
            SetRelativeInteger(newEntry, "Override", 0);
            SetRelativeObject(newEntry, "Prefab", bossPrefab);
            SetRelativeObject(newEntry, "SourcePrefabToOverride", null);
            SetRelativeInteger(newEntry, "SourceHashToOverride", 0);
            SetRelativeObject(newEntry, "OverridingTargetPrefab", null);
            serializedList.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabsList);
        }

        private static void ConfigureWorldAIManagerPrefab(GameObject bossPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldAIManagerPrefabPath);
            try
            {
                Transform arena = GetOrCreateChild(root.transform, k_BossArenaName);
                arena.localPosition = Vector3.zero;
                arena.localRotation = Quaternion.identity;
                arena.localScale = Vector3.one;

                BoxCollider arenaTrigger = GetOrAddComponent<BoxCollider>(
                    arena.gameObject);
                arenaTrigger.isTrigger = true;
                arenaTrigger.center = new Vector3(0f, 1.5f, 19f);
                arenaTrigger.size = new Vector3(18f, 5f, 12f);
                BossArenaController arenaController =
                    GetOrAddComponent<BossArenaController>(arena.gameObject);
                SerializedObject serializedArena = new SerializedObject(arenaController);
                SetInteger(serializedArena, "m_bossID", k_BossID);

                Transform fogWall = GetOrCreatePrimitiveChild(
                    arena,
                    k_FogWallName,
                    PrimitiveType.Cube);
                fogWall.localPosition = new Vector3(0f, 1.5f, 12.9f);
                fogWall.localRotation = Quaternion.identity;
                fogWall.localScale = new Vector3(18f, 3f, 0.35f);
                MeshRenderer fogRenderer =
                    GetRequiredComponent<MeshRenderer>(fogWall.gameObject);
                fogRenderer.sharedMaterial = ConfigureFogMaterial();
                fogWall.gameObject.SetActive(false);
                SetObjectReference(
                    serializedArena,
                    "m_fogWallRoot",
                    fogWall.gameObject);
                serializedArena.ApplyModifiedPropertiesWithoutUndo();

                Transform spawnerTransform = GetOrCreateChild(arena, k_BossSpawnerName);
                spawnerTransform.localPosition = new Vector3(0f, 0.1f, 20f);
                spawnerTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                spawnerTransform.localScale = Vector3.one;
                AICharacterSpawner spawner =
                    GetOrAddComponent<AICharacterSpawner>(spawnerTransform.gameObject);
                SerializedObject serializedSpawner = new SerializedObject(spawner);
                SetObjectReference(
                    serializedSpawner,
                    "m_characterGameObject",
                    bossPrefab);
                SetInteger(serializedSpawner, "m_bossID", k_BossID);
                serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_WorldAIManagerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the World AI Manager prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material ConfigureFogMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                k_FogMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
                material = new Material(shader)
                {
                    name = "Fallen Watcher Fog Wall"
                };
                AssetDatabase.CreateAsset(material, k_FogMaterialPath);
            }

            material.color = s_fogColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", s_fogColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", s_fogColor * 1.4f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigurePlayerUIManagerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);
            try
            {
                Transform playerUI = root.transform.Find("Player UI") ??
                    throw new InvalidOperationException("Player UI root is missing.");
                RectTransform panel = GetOrCreateRectTransform(playerUI, k_BossUIName);
                panel.anchorMin = new Vector2(0.5f, 0f);
                panel.anchorMax = new Vector2(0.5f, 0f);
                panel.pivot = new Vector2(0.5f, 0f);
                panel.anchoredPosition = new Vector2(0f, 105f);
                panel.sizeDelta = new Vector2(800f, 82f);
                Image panelImage = GetOrAddComponent<Image>(panel.gameObject);
                panelImage.color = s_panelColor;
                panelImage.raycastTarget = false;

                RectTransform nameRect = GetOrCreateRectTransform(panel, "Boss Name");
                nameRect.anchorMin = new Vector2(0.5f, 0f);
                nameRect.anchorMax = new Vector2(0.5f, 0f);
                nameRect.pivot = new Vector2(0.5f, 0f);
                nameRect.anchoredPosition = new Vector2(0f, 43f);
                nameRect.sizeDelta = new Vector2(760f, 32f);
                TextMeshProUGUI bossNameText =
                    GetOrAddComponent<TextMeshProUGUI>(nameRect.gameObject);
                ConfigureBossNameText(root, bossNameText);

                RectTransform healthRect = GetOrCreateRectTransform(panel, "Health");
                healthRect.anchorMin = new Vector2(0.5f, 0f);
                healthRect.anchorMax = new Vector2(0.5f, 0f);
                healthRect.pivot = new Vector2(0.5f, 0f);
                healthRect.anchoredPosition = new Vector2(0f, 14f);
                healthRect.sizeDelta = new Vector2(760f, 24f);
                UIStatBar statBar = ConfigureBossStatBar(healthRect);

                PlayerUIBossHealthBar bossUI =
                    GetOrAddComponent<PlayerUIBossHealthBar>(panel.gameObject);
                SerializedObject serializedBossUI = new SerializedObject(bossUI);
                SetObjectReference(
                    serializedBossUI,
                    "m_bossNameText",
                    bossNameText);
                SetObjectReference(serializedBossUI, "m_healthBar", statBar);
                serializedBossUI.ApplyModifiedPropertiesWithoutUndo();

                PlayerUIManager uiManager = GetRequiredComponent<PlayerUIManager>(root);
                SerializedObject serializedManager = new SerializedObject(uiManager);
                SetObjectReference(
                    serializedManager,
                    "m_playerUIBossHealthBar",
                    bossUI);
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                SetUILayerRecursively(panel.gameObject, playerUI.gameObject.layer);
                panel.gameObject.SetActive(false);

                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_PlayerUIManagerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Player UI Manager prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureBossNameText(
            GameObject root,
            TextMeshProUGUI bossNameText)
        {
            TextMeshProUGUI sourceText = root
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(candidate => candidate != bossNameText);
            if (sourceText != null)
            {
                bossNameText.font = sourceText.font;
                bossNameText.fontSharedMaterial = sourceText.fontSharedMaterial;
            }

            bossNameText.text = k_BossName;
            bossNameText.fontSize = 26f;
            bossNameText.fontStyle = FontStyles.SmallCaps;
            bossNameText.alignment = TextAlignmentOptions.Center;
            bossNameText.color = s_goldColor;
            bossNameText.raycastTarget = false;
        }

        private static UIStatBar ConfigureBossStatBar(RectTransform healthRect)
        {
            RectTransform background = GetOrCreateRectTransform(
                healthRect,
                "Background");
            StretchToParent(background);
            Image backgroundImage = GetOrAddComponent<Image>(background.gameObject);
            backgroundImage.color = new Color(0f, 0f, 0f, 0.95f);
            backgroundImage.raycastTarget = false;

            RectTransform fillArea = GetOrCreateRectTransform(
                healthRect,
                "Fill Area");
            StretchToParent(fillArea);
            fillArea.offsetMin = new Vector2(3f, 3f);
            fillArea.offsetMax = new Vector2(-3f, -3f);
            RectTransform fill = GetOrCreateRectTransform(fillArea, "Fill");
            StretchToParent(fill);
            Image fillImage = GetOrAddComponent<Image>(fill.gameObject);
            fillImage.color = s_healthColor;
            fillImage.raycastTarget = false;

            Slider slider = GetOrAddComponent<Slider>(healthRect.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = null;
            slider.targetGraphic = null;
            slider.minValue = 0f;
            slider.maxValue = 600f;
            slider.value = 600f;

            UIStatBar statBar = GetOrAddComponent<UIStatBar>(healthRect.gameObject);
            SerializedObject serializedBar = new SerializedObject(statBar);
            SetObjectReference(serializedBar, "m_slider", slider);
            SetObjectReference(serializedBar, "m_rectTransform", healthRect);
            SetBoolean(serializedBar, "m_shouldScaleBarLengthWithStats", false);
            serializedBar.ApplyModifiedPropertiesWithoutUndo();
            return statBar;
        }

        private static void ValidateAttackAndPhaseData()
        {
            BossAttackData[] attacks =
            {
                LoadRequiredAsset<BossAttackData>(k_LightAttackPath),
                LoadRequiredAsset<BossAttackData>(k_SweepAttackPath),
                LoadRequiredAsset<BossAttackData>(k_FrenzyAttackPath)
            };
            if (attacks.Any(attack =>
                    attack.MaximumRange <= 0f ||
                    attack.RecoveryTime <= 0f ||
                    attack.SelectionWeight <= 0f ||
                    attack.PhysicalDamage <= 0f))
            {
                throw new InvalidOperationException(
                    "Every Boss attack must define range, selection, recovery, and damage.");
            }

            BossPhaseData phaseOne = LoadRequiredAsset<BossPhaseData>(k_PhaseOnePath);
            BossPhaseData phaseTwo = LoadRequiredAsset<BossPhaseData>(k_PhaseTwoPath);
            if (phaseOne.HealthThreshold != 1f ||
                !Mathf.Approximately(phaseTwo.HealthThreshold, 0.5f) ||
                phaseOne.Attacks.Count != 2 ||
                phaseTwo.Attacks.Count != 3 ||
                phaseTwo.MovementSpeed <= phaseOne.MovementSpeed)
            {
                throw new InvalidOperationException(
                    "Boss phases must escalate at half Health with a larger attack set.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIAnimatorControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .First(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            ValidateStateMotion(
                stateMachine,
                k_SweepStateName,
                k_SweepAttackClipPath);
            ValidateStateMotion(
                stateMachine,
                k_FrenzyStateName,
                k_FrenzyAttackClipPath);
            ValidateStateMotion(
                stateMachine,
                k_PhaseTransitionStateName,
                k_PhaseTransitionClipPath);
            ValidateAttackEvents(k_SweepAttackClipPath);
            ValidateAttackEvents(k_FrenzyAttackClipPath);
        }

        private static void ValidateAttackEvents(string clipPath)
        {
            string[] eventNames = AnimationUtility.GetAnimationEvents(
                    LoadRequiredAsset<AnimationClip>(clipPath))
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();
            if (!eventNames.Contains("OpenLeftHandDamageCollider") ||
                !eventNames.Contains("CloseRightHandDamageCollider"))
            {
                throw new InvalidOperationException(
                    $"Boss attack {clipPath} is missing damage-window events.");
            }
        }

        private static void ValidateBossPrefab()
        {
            GameObject bossPrefab = LoadRequiredAsset<GameObject>(k_BossPrefabPath);
            BossCharacterManager boss =
                GetRequiredComponent<BossCharacterManager>(bossPrefab);
            GetRequiredComponent<NetworkObject>(bossPrefab);
            GetRequiredComponent<AICharacterManager>(bossPrefab);
            GetRequiredComponent<AICharacterCombatManager>(bossPrefab);
            SerializedProperty phases = GetRequiredProperty(
                new SerializedObject(boss),
                "m_phases");
            if (boss.BossID != k_BossID ||
                boss.BossName != k_BossName ||
                phases.arraySize != 2)
            {
                throw new InvalidOperationException(
                    "The Boss prefab has invalid identity or phase references.");
            }
        }

        private static void ValidateWorldAIManagerPrefab()
        {
            GameObject manager = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPrefabPath);
            BossArenaController arena =
                manager.GetComponentInChildren<BossArenaController>(true) ??
                throw new InvalidOperationException("The Boss arena is missing.");
            AICharacterSpawner bossSpawner = manager
                .GetComponentsInChildren<AICharacterSpawner>(true)
                .SingleOrDefault(spawner => spawner.BossID == k_BossID) ??
                throw new InvalidOperationException("The Boss spawner is missing.");
            SerializedObject serializedArena = new SerializedObject(arena);
            SerializedObject serializedSpawner = new SerializedObject(bossSpawner);
            if (GetRequiredProperty(serializedArena, "m_fogWallRoot")
                    .objectReferenceValue == null ||
                GetRequiredProperty(serializedSpawner, "m_characterGameObject")
                    .objectReferenceValue !=
                    LoadRequiredAsset<GameObject>(k_BossPrefabPath))
            {
                throw new InvalidOperationException(
                    "The Boss arena must reference its fog wall and Boss prefab.");
            }
        }

        private static void ValidatePlayerUIManagerPrefab()
        {
            GameObject manager = LoadRequiredAsset<GameObject>(
                k_PlayerUIManagerPrefabPath);
            PlayerUIBossHealthBar bossUI = manager
                .GetComponentInChildren<PlayerUIBossHealthBar>(true) ??
                throw new InvalidOperationException("The Boss Health HUD is missing.");
            SerializedObject serializedBossUI = new SerializedObject(bossUI);
            if (GetRequiredProperty(serializedBossUI, "m_bossNameText")
                    .objectReferenceValue == null ||
                GetRequiredProperty(serializedBossUI, "m_healthBar")
                    .objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    "The Boss Health HUD has missing presentation references.");
            }
        }

        private static void ValidateNetworkPrefabRegistration()
        {
            GameObject bossPrefab = LoadRequiredAsset<GameObject>(k_BossPrefabPath);
            NetworkPrefabsList prefabs =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedProperty entries = GetRequiredProperty(
                new SerializedObject(prefabs),
                "List");
            int count = 0;
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab")
                        ?.objectReferenceValue == bossPrefab)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "The Boss prefab must be registered exactly once for networking.");
            }
        }

        private static void ValidateWorldSceneConnection()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                BossArenaController arena = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<BossArenaController>(true))
                    .FirstOrDefault();
                if (arena == null)
                {
                    throw new InvalidOperationException(
                        "The World Scene does not receive the Boss arena prefab content.");
                }
            }
            finally
            {
                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateStateMotion(
            AnimatorStateMachine stateMachine,
            string stateName,
            string clipPath)
        {
            AnimatorState state = GetRequiredState(stateMachine, stateName);
            if (state.motion != LoadRequiredAsset<AnimationClip>(clipPath))
            {
                throw new InvalidOperationException(
                    $"Animator state {stateName} has the wrong clip.");
            }
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                throw new InvalidOperationException(
                    $"Animator state {stateName} is missing.");
        }

        private static Transform GetOrCreatePrimitiveChild(
            Transform parent,
            string childName,
            PrimitiveType primitiveType)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = childName;
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing as RectTransform ??
                    throw new InvalidOperationException(
                        $"{childName} must use a RectTransform.");
            }

            GameObject child = new GameObject(childName, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetUILayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetUILayerRecursively(child.gameObject, layer);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                string nextPath = $"{currentPath}/{segments[segmentIndex]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static T LoadOrCreateAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetEnum(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).enumValueIndex = value;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static void SetRelativeInteger(
            SerializedProperty property,
            string relativeName,
            int value)
        {
            SerializedProperty relative = property.FindPropertyRelative(relativeName);
            if (relative != null)
            {
                relative.intValue = value;
            }
        }

        private static void SetRelativeObject(
            SerializedProperty property,
            string relativeName,
            UnityEngine.Object value)
        {
            SerializedProperty relative = property.FindPropertyRelative(relativeName);
            if (relative != null)
            {
                relative.objectReferenceValue = value;
            }
        }
    }
}
