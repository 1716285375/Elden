using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SwordSlashCoreConfigurator
{
    private const string k_MaterialFolder = "Assets/_Game/Art/VFX/Combat/SwordSlash/Materials";

    [MenuItem("Tools/VFX/SwordSlash/Configure Core")]
    public static void ConfigureCore()
    {
        var root = GameObject.Find("VFX_SwordSlash_Swing_Blue");
        if (root == null)
        {
            Debug.LogError("VFX_SwordSlash_Swing_Blue not found in scene");
            return;
        }

        Configure(root, "Core/PS_SwordSlash_Ink", "M_SwordSlash_Ink", 0.015f, 0.18f, 2.08f,
            AlphaKeys((0f, 0f), (0.03f, 1f), (0.75f, 0.9f), (1f, 0f)),
            SizeKeys((0f, 0.82f), (0.10f, 1.00f), (1f, 1.08f)));

        Configure(root, "Core/PS_SwordSlash_MainBlade", "M_SwordSlash_MainBlade", 0.020f, 0.16f, 2.00f,
            AlphaKeys((0f, 0f), (0.04f, 1f), (0.70f, 1f), (1f, 0f)),
            SizeKeys((0f, 0.80f), (0.12f, 1.00f), (0.70f, 1.04f), (1f, 1.10f)));

        Configure(root, "Core/PS_SwordSlash_OuterGlow", "M_SwordSlash_OuterGlow", 0.012f, 0.23f, 2.15f,
            AlphaKeys((0f, 0f), (0.05f, 0.35f), (0.55f, 0.25f), (1f, 0f)),
            SizeKeys((0f, 0.90f), (0.25f, 1.00f), (0.60f, 1.12f), (1f, 1.20f)));

        Configure(root, "Core/PS_SwordSlash_InnerDetail", "M_SwordSlash_InnerDetail", 0.028f, 0.13f, 1.95f,
            AlphaKeys((0f, 0f), (0.15f, 0.8f), (0.60f, 0.7f), (1f, 0f)),
            SizeKeys((0f, 0.90f), (0.50f, 1.00f), (1f, 1.05f)));

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("SwordSlash core configured");
    }

    [MenuItem("Tools/VFX/SwordSlash/Configure Motion Peak")]
    public static void ConfigureMotionPeak()
    {
        var root = FindSwingRoot();
        if (root == null)
        {
            Debug.LogError("VFX_SwordSlash_Swing_Blue not found");
            return;
        }

        var accent = EnsureChild(root.transform, "Accent");
        var motion = EnsureChild(root.transform, "Motion");

        EnsureParticleSystem(EnsureChild(accent, "PS_SwordSlash_HighlightGlint"));
        EnsureParticleSystem(EnsureChild(motion, "PS_SwordSlash_SpeedLines"));

        Configure(root, "Accent/PS_SwordSlash_HighlightGlint", "M_SwordSlash_HighlightGlint", 0.035f, 0.065f, 0.55f,
            AlphaKeys((0f, 0f), (0.40f, 1f), (1f, 0f)),
            SizeKeys((0f, 0.2f), (0.40f, 1.0f), (1f, 0.4f)));

        Configure(root, "Motion/PS_SwordSlash_SpeedLines", "M_SwordSlash_SpeedLines", 0.000f, 0.08f, 0.03f,
            AlphaKeys((0f, 0f), (0.20f, 1f), (0.80f, 0.6f), (1f, 0f)),
            SizeKeys((0f, 0.8f), (1f, 1.2f)),
            speed: 5f, burstCount: 6,
            renderMode: ParticleSystemRenderMode.Stretch, lengthScale: 3f);

        MarkSceneDirty(root);
        Debug.Log("SwordSlash motion peak configured");
    }

    [MenuItem("Tools/VFX/SwordSlash/Configure Secondary Details")]
    public static void ConfigureSecondaryDetails()
    {
        var root = FindSwingRoot();
        if (root == null)
        {
            Debug.LogError("VFX_SwordSlash_Swing_Blue not found");
            return;
        }

        var accent = EnsureChild(root.transform, "Accent");
        var motion = EnsureChild(root.transform, "Motion");

        EnsureParticleSystem(EnsureChild(accent, "PS_SwordSlash_BrokenSlash"));
        EnsureParticleSystem(EnsureChild(accent, "PS_SwordSlash_LightningEnergy"));
        EnsureParticleSystem(EnsureChild(motion, "PS_SwordSlash_MicroParticles"));
        EnsureParticleSystem(EnsureChild(motion, "PS_SwordSlash_StarSparkles"));

        // BrokenSlash: single big slash shard, after MainBlade
        ConfigureDetail(root, "Accent/PS_SwordSlash_BrokenSlash", "M_SwordSlash_BrokenSlash",
            0.050f, 0.14f, 2.10f, 0f, 1, 1,
            alphaKeys: AlphaKeys((0f, 0f), (0.20f, 0.65f), (0.70f, 0.5f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 0.90f), (1f, 1.10f)));

        // LightningEnergy: random 1-2 bolts, 60% chance, subtle accent
        ConfigureDetail(root, "Accent/PS_SwordSlash_LightningEnergy", "M_SwordSlash_LightningEnergy",
            0.045f, 0.12f, 1.9f, 0f, 1, 2, 0.6f,
            lifetimeMin: 0.08f, sizeMin: 1.8f, sizeMax: 2.0f,
            alphaKeys: AlphaKeys((0f, 0f), (0.25f, 0.7f), (0.75f, 0.3f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 0.9f), (1f, 1.05f)));

        // MicroParticles: box burst, white->cyan->blue
        ConfigureDetail(root, "Motion/PS_SwordSlash_MicroParticles", "M_SwordSlash_MicroParticles",
            0.050f, 0.30f, 0.04f, 1.6f, 8, 14, 1f,
            lifetimeMin: 0.12f, speedMin: 0.8f, speedMax: 2.5f,
            sizeMin: 0.015f, sizeMax: 0.06f, maxParticles: 32,
            shapeType: ParticleSystemShapeType.Box, shapeScale: new Vector3(1.5f, 0.15f, 0.05f),
            colorKeys: new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.29f, 0.93f, 1f), 0.5f),
                new GradientColorKey(new Color(0.09f, 0.48f, 1f), 1f),
            },
            alphaKeys: AlphaKeys((0f, 0f), (0.15f, 1f), (0.85f, 0.4f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 1f), (0.5f, 0.7f), (1f, 0f)));

        // StarSparkles: few small sparks
        ConfigureDetail(root, "Motion/PS_SwordSlash_StarSparkles", "M_SwordSlash_StarSparkles",
            0.065f, 0.20f, 0.06f, 0.5f, 2, 4,
            lifetimeMin: 0.10f, speedMin: 0.2f, speedMax: 0.8f,
            sizeMin: 0.03f, sizeMax: 0.10f,
            alphaKeys: AlphaKeys((0f, 0f), (0.2f, 1f), (0.8f, 0.5f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 0.8f), (0.5f, 1f), (1f, 0.5f)));

        MarkSceneDirty(root);
        Debug.Log("SwordSlash secondary details configured");
    }

    [MenuItem("Tools/VFX/SwordSlash/Configure Residual")]
    public static void ConfigureResidual()
    {
        var root = FindSwingRoot();
        if (root == null)
        {
            Debug.LogError("VFX_SwordSlash_Swing_Blue not found");
            return;
        }

        var residual = EnsureChild(root.transform, "Residual");
        EnsureParticleSystem(EnsureChild(residual, "PS_SwordSlash_SmokeTrail"));

        ConfigureDetail(root, "Residual/PS_SwordSlash_SmokeTrail", "M_SwordSlash_SmokeTrail",
            0.080f, 0.40f, 0.8f, 0.4f, 2, 4,
            lifetimeMin: 0.25f, speedMin: 0.1f, speedMax: 0.4f,
            sizeMin: 0.3f, sizeMax: 0.8f,
            alphaKeys: AlphaKeys((0f, 0f), (0.20f, 0.35f), (0.70f, 0.20f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 0.8f), (1f, 1.3f)));

        var child = root.transform.Find("Residual/PS_SwordSlash_SmokeTrail");
        var noise = child.GetComponent<ParticleSystem>().noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.5f;

        EditorUtility.SetDirty(child.GetComponent<ParticleSystem>());
        MarkSceneDirty(root);
        Debug.Log("SwordSlash residual configured");
    }

    [MenuItem("Tools/VFX/SwordSlash/Fix Weapon Mounts")]
    public static void FixWeaponMounts()
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null)
        {
            Debug.LogError("Open the Player prefab first");
            return;
        }

        var root = stage.prefabContentsRoot.transform;

        // blade forward (+Z world), blade flat horizontal: computed against hand rest poses
        SetSlotLocalRotation(root, "Right Hand Weapon Slot",
            new Quaternion(-0.0668f, 0.8429f, -0.5221f, -0.1118f));
        SetSlotLocalRotation(root, "Left Hand Weapon Slot",
            new Quaternion(-0.4711f, -0.1419f, -0.3403f, 0.8013f));
        SetSlotLocalRotation(root, "Left Hand Shield Slot",
            new Quaternion(-0.1403f, -0.8997f, -0.2335f, -0.3410f));

        // shift weapon pivots so the grip sits at the prefab origin
        ShiftWeaponPivot("Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Straight Sword.prefab", 0.10f);
        ShiftWeaponPivot("Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Broadsword.prefab", 0.11f);

        EditorUtility.SetDirty(stage.prefabContentsRoot);
        Debug.Log("Weapon mounts fixed");
    }

    private static void SetSlotLocalRotation(Transform root, string slotName, Quaternion localRotation)
    {
        var slot = FindDeep(root, slotName);
        if (slot == null)
        {
            Debug.LogError("Slot not found: " + slotName);
            return;
        }

        slot.localRotation = localRotation;
        EditorUtility.SetDirty(slot.gameObject);
        Debug.Log(slotName + " rotation set");
    }

    private static void ShiftWeaponPivot(string prefabPath, float gripOffsetY)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("Weapon prefab not found: " + prefabPath);
            return;
        }

        var pivot = prefab.transform.Find("Weapon Pivot");
        if (pivot == null)
        {
            Debug.LogError("Weapon Pivot not found in " + prefabPath);
            return;
        }

        var localPosition = pivot.localPosition;
        localPosition.y = gripOffsetY;
        pivot.localPosition = localPosition;
        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log(prefabPath + " pivot y = " + gripOffsetY);
    }

    [MenuItem("Tools/VFX/SwordSlash/Enter Play Mode")]
    public static void EnterPlayMode()
    {
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/VFX/SwordSlash/Exit Play Mode")]
    public static void ExitPlayMode()
    {
        EditorApplication.ExitPlaymode();
    }

    [MenuItem("Tools/VFX/SwordSlash/Toggle Pause")]
    public static void TogglePause()
    {
        EditorApplication.isPaused = !EditorApplication.isPaused;
        Debug.Log("Paused = " + EditorApplication.isPaused);
    }

    [MenuItem("Tools/VFX/SwordSlash/Build Attack Test Rig")]
    public static void BuildAttackTestRig()
    {
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("Close the prefab stage first");
            return;
        }

        // locate or create the test root
        var testRoot = GameObject.Find("SwordAttackTest");
        if (testRoot == null)
        {
            testRoot = new GameObject("SwordAttackTest");
        }

        testRoot.transform.SetPositionAndRotation(new Vector3(0f, -0.4f, 0f), Quaternion.identity);
        testRoot.layer = 0;

        // driver + hit spawner components
        if (testRoot.GetComponent<SwordSlashTestDriver>() == null)
        {
            testRoot.AddComponent<SwordSlashTestDriver>();
        }

        var spawner = testRoot.GetComponent<SwordSlashHitVFXSpawner>();
        if (spawner == null)
        {
            spawner = testRoot.AddComponent<SwordSlashHitVFXSpawner>();
        }

        // sword rig
        var rig = testRoot.transform.Find("SwordRig");
        if (rig == null)
        {
            rig = new GameObject("SwordRig").transform;
            rig.SetParent(testRoot.transform, false);
        }

        // sword model (blade +Y in prefab -> rotate X -90 so blade points +Z forward)
        var sword = rig.Find("Sword");
        if (sword == null)
        {
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Straight Sword.prefab");
            if (swordPrefab == null)
            {
                Debug.LogError("Straight Sword prefab not found");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, rig);
            instance.name = "Sword";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            sword = instance.transform;
        }

        // blade mount points + slash anchor
        EnsureAnchor(sword, "BladeBase", new Vector3(0f, 0f, -0.1f));
        EnsureAnchor(sword, "BladeTip", new Vector3(0f, 0f, 0.9f));
        var slashAnchor = EnsureAnchor(sword, "VFX_SlashAnchor", new Vector3(0f, 0f, 0.4f));

        // move the scene swing instance under the anchor
        var swing = GameObject.Find("VFX/VFX_SwordSlash_Swing_Blue");
        if (swing != null && swing.transform.parent != slashAnchor)
        {
            swing.transform.SetParent(slashAnchor, false);
            swing.transform.localPosition = Vector3.zero;
            swing.transform.localRotation = Quaternion.identity;
        }

        // ensure the swing root has the runtime player component
        if (swing != null && swing.GetComponent<SwordSlashVFXPlayer>() == null)
        {
            swing.AddComponent<SwordSlashVFXPlayer>();
        }

        // wire the hit prefab on the spawner
        var hitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/VFX/Combat/SwordSlash/VFX_SwordSlash_Hit_Blue.prefab");
        if (hitPrefab != null)
        {
            var so = new SerializedObject(spawner);
            so.FindProperty("hitVfxPrefab").objectReferenceValue = hitPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(testRoot.scene);
        Debug.Log("Attack test rig built");
    }

    private static Transform EnsureAnchor(Transform parent, string childName, Vector3 localPosition)
    {
        var existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    [MenuItem("Tools/VFX/SwordSlash/Mount Test Weapons")]
    public static void MountTestWeapons()
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null)
        {
            Debug.LogError("Not in prefab stage");
            return;
        }

        var root = stage.prefabContentsRoot.transform;
        MountTestWeapon(root, "Right Hand Weapon Slot",
            "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Straight Sword.prefab");
        MountTestWeapon(root, "Left Hand Weapon Slot",
            "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Broadsword.prefab");
        EditorUtility.SetDirty(stage.prefabContentsRoot);
        Debug.Log("Test weapons mounted");
    }

    [MenuItem("Tools/VFX/SwordSlash/Remove Test Mounts")]
    public static void RemoveTestMounts()
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null)
        {
            return;
        }

        foreach (var t in stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("TEST_MOUNT_"))
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        EditorUtility.SetDirty(stage.prefabContentsRoot);
        Debug.Log("Test mounts removed");
    }

    private static void MountTestWeapon(Transform root, string slotName, string weaponPath)
    {
        var slot = FindDeep(root, slotName);
        if (slot == null)
        {
            Debug.LogError("Slot not found: " + slotName);
            return;
        }

        var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
        if (weaponPrefab == null)
        {
            Debug.LogError("Weapon prefab not found: " + weaponPath);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, slot);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.name = "TEST_MOUNT_" + weaponPrefab.name;
    }

    private static Transform FindDeep(Transform parent, string targetName)
    {
        if (parent.name == targetName)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            var result = FindDeep(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    [MenuItem("Tools/VFX/SwordSlash/Configure Hit VFX")]
    public static void ConfigureHitVfx()
    {
        var hitRoot = FindOrCreateHitRoot();
        if (hitRoot == null)
        {
            Debug.LogError("Hit root not found and could not be created");
            return;
        }

        EnsureParticleSystem(EnsureChild(hitRoot, "PS_SwordSlash_ImpactBurst"));
        EnsureParticleSystem(EnsureChild(hitRoot, "PS_SwordSlash_RingImpact"));
        EnsureParticleSystem(EnsureChild(hitRoot, "PS_SwordSlash_DebrisShards"));

        var hitRootGo = hitRoot.gameObject;

        // ImpactBurst: quick radial flash
        ConfigureDetail(hitRootGo, "PS_SwordSlash_ImpactBurst", "M_SwordSlash_ImpactBurst",
            0f, 0.10f, 0.8f, 0f, 1, 1,
            lifetimeMin: 0.07f, sizeMin: 0.4f, sizeMax: 0.8f,
            alphaKeys: AlphaKeys((0f, 0f), (0.30f, 1f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 0.5f), (0.50f, 1.1f), (1f, 0.9f)));

        // RingImpact: expanding ring (size curve is a multiplier on start size)
        ConfigureDetail(hitRootGo, "PS_SwordSlash_RingImpact", "M_SwordSlash_RingImpact",
            0f, 0.18f, 0.2f, 0f, 1, 1,
            lifetimeMin: 0.12f,
            alphaKeys: AlphaKeys((0f, 1f), (0.60f, 0.6f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 1f), (0.50f, 4f), (1f, 7f)));

        // DebrisShards: cone burst along hit normal, gravity falloff
        ConfigureDetail(hitRootGo, "PS_SwordSlash_DebrisShards", "M_SwordSlash_DebrisShards",
            0f, 0.50f, 0.10f, 4f, 5, 10,
            lifetimeMin: 0.25f, speedMin: 1.5f, speedMax: 4f,
            sizeMin: 0.025f, sizeMax: 0.10f, maxParticles: 16,
            shapeType: ParticleSystemShapeType.Cone, shapeScale: new Vector3(0.05f, 0.05f, 0.05f),
            alphaKeys: AlphaKeys((0f, 0f), (0.10f, 1f), (0.80f, 0.6f), (1f, 0f)),
            sizeKeys: SizeKeys((0f, 1f), (1f, 0.6f)));

        var debris = hitRoot.Find("PS_SwordSlash_DebrisShards").GetComponent<ParticleSystem>();
        var debrisMain = debris.main;
        debrisMain.gravityModifier = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        var coneShape = debris.shape;
        coneShape.angle = 25f;

        EditorUtility.SetDirty(debris);
        MarkSceneDirty(hitRootGo);
        Debug.Log("SwordSlash hit VFX configured");
    }

    private static Transform FindOrCreateHitRoot()
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            return stage.prefabContentsRoot.transform;
        }

        var vfxNode = GameObject.Find("VFX");
        if (vfxNode == null)
        {
            return null;
        }

        var hitRoot = vfxNode.transform.Find("VFX_SwordSlash_Hit_Blue");
        if (hitRoot != null)
        {
            return hitRoot;
        }

        var go = new GameObject("VFX_SwordSlash_Hit_Blue");
        go.transform.SetParent(vfxNode.transform, false);
        return go.transform;
    }

    [MenuItem("Tools/VFX/SwordSlash/Create PC Pipeline Assets")]
    public static void CreatePCPipelineAssets()
    {
        const string folder = "Assets/_Dev/VFXAuthoring/SwordSlash/Settings/Rendering/Pipeline";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/_Dev/VFXAuthoring/SwordSlash/Settings/Rendering", "Pipeline");
        }

        var rendererPath = folder + "/PC_Renderer.asset";
        var assetPath = folder + "/PC_RPAsset.asset";

        var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(rendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);
        }

        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(assetPath);
        if (pipelineAsset == null)
        {
            pipelineAsset = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, assetPath);
        }

        pipelineAsset.supportsHDR = true;
        pipelineAsset.msaaSampleCount = 1;
        pipelineAsset.renderScale = 1f;
        pipelineAsset.supportsCameraDepthTexture = true;
        pipelineAsset.supportsCameraOpaqueTexture = true;
        pipelineAsset.shadowDistance = 50f;

        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(pipelineAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("PC pipeline assets created at " + folder);
    }

    [MenuItem("Tools/VFX/SwordSlash/Preview Frame")]
    public static void PreviewFrame()
    {
        PreviewAt(0.10f);
    }

    [MenuItem("Tools/VFX/SwordSlash/Preview Frame Early")]
    public static void PreviewFrameEarly()
    {
        PreviewAt(0.05f);
    }

    [MenuItem("Tools/VFX/SwordSlash/Preview Frame Late")]
    public static void PreviewFrameLate()
    {
        PreviewAt(0.155f);
    }

    [MenuItem("Tools/VFX/SwordSlash/Preview Frame Residual")]
    public static void PreviewFrameResidual()
    {
        PreviewAt(0.30f);
    }

    [MenuItem("Tools/VFX/SwordSlash/Preview Hit VFX")]
    public static void PreviewHitVfx()
    {
        var hitRoot = GameObject.Find("VFX_SwordSlash_Hit_Blue");
        if (hitRoot == null)
        {
            Debug.LogError("VFX_SwordSlash_Hit_Blue not found in scene");
            return;
        }

        foreach (var ps in hitRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Simulate(0.05f, true, true, false);
        }

        Debug.Log("SwordSlash hit VFX previewed at t=0.05");
    }

    [MenuItem("Tools/VFX/SwordSlash/Clear")]
    public static void Clear()
    {
        var root = FindSwingRoot();
        if (root == null)
        {
            return;
        }

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void PreviewAt(float time)
    {
        var root = FindSwingRoot();
        if (root == null)
        {
            return;
        }

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Simulate(time, true, true, false);
        }

        Debug.Log("SwordSlash previewed at t=" + time);
    }

    private static GameObject FindSwingRoot()
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            return stage.prefabContentsRoot;
        }

        return GameObject.Find("VFX_SwordSlash_Swing_Blue");
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        var existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void EnsureParticleSystem(Transform target)
    {
        if (target.GetComponent<ParticleSystem>() == null)
        {
            target.gameObject.AddComponent<ParticleSystem>();
        }
    }

    private static void MarkSceneDirty(GameObject root)
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            EditorUtility.SetDirty(stage.prefabContentsRoot);
            return;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    private static void Configure(GameObject root, string childPath, string materialName,
        float delay, float lifetime, float size,
        (float time, float alpha)[] alphaKeys,
        (float time, float size)[] sizeKeys,
        float speed = 0f, int burstCount = 1,
        ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard,
        float lengthScale = 0f)
    {
        var child = root.transform.Find(childPath);
        if (child == null)
        {
            Debug.LogError("Child not found: " + childPath);
            return;
        }

        var ps = child.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("No ParticleSystem on " + childPath);
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = lifetime + delay;
        main.loop = false;
        main.playOnAwake = false;
        main.startDelay = delay;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.enabled = false;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f),
        };
        var gradientAlphaKeys = new GradientAlphaKey[alphaKeys.Length];
        for (var i = 0; i < alphaKeys.Length; i++)
        {
            gradientAlphaKeys[i] = new GradientAlphaKey(alphaKeys[i].alpha, alphaKeys[i].time);
        }

        gradient.alphaKeys = gradientAlphaKeys;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, BuildCurve(sizeKeys));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        renderer.lengthScale = lengthScale;
        renderer.material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialFolder + "/" + materialName + ".mat");

        EditorUtility.SetDirty(ps);
    }

    private static void ConfigureDetail(GameObject root, string childPath, string materialName,
        float delay, float lifetimeMax, float size, float speed, int burstMin, int burstMax,
        float probability = 1f,
        float lifetimeMin = -1f, float speedMin = -1f, float speedMax = -1f,
        float sizeMin = -1f, float sizeMax = -1f, int maxParticles = 20,
        ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone, Vector3 shapeScale = default,
        GradientColorKey[] colorKeys = null,
        (float time, float alpha)[] alphaKeys = null,
        (float time, float size)[] sizeKeys = null)
    {
        var child = root.transform.Find(childPath);
        if (child == null)
        {
            Debug.LogError("Child not found: " + childPath);
            return;
        }

        var ps = child.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("No ParticleSystem on " + childPath);
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = lifetimeMax + delay;
        main.loop = false;
        main.playOnAwake = false;
        main.startDelay = delay;
        main.startLifetime = lifetimeMin > 0f
            ? new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax)
            : new ParticleSystem.MinMaxCurve(lifetimeMax);
        main.startSpeed = speedMin >= 0f
            ? new ParticleSystem.MinMaxCurve(speedMin, speedMax)
            : new ParticleSystem.MinMaxCurve(speed);
        main.startSize = sizeMin > 0f
            ? new ParticleSystem.MinMaxCurve(sizeMin, sizeMax)
            : new ParticleSystem.MinMaxCurve(size);
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var burst = new ParticleSystem.Burst(0f, (short)burstMin, (short)burstMax);
        burst.probability = probability;
        emission.SetBursts(new[] { burst });

        var shape = ps.shape;
        shape.enabled = shapeType != ParticleSystemShapeType.Cone;
        if (shape.enabled)
        {
            shape.shapeType = shapeType;
            shape.scale = shapeScale;
        }

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = alphaKeys != null || colorKeys != null;
        if (colorOverLifetime.enabled)
        {
            var gradient = new Gradient();
            gradient.colorKeys = colorKeys ?? new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            };
            var effectiveAlpha = alphaKeys ?? new[] { (0f, 1f), (1f, 1f) };
            var gradientAlphaKeys = new GradientAlphaKey[effectiveAlpha.Length];
            for (var i = 0; i < effectiveAlpha.Length; i++)
            {
                gradientAlphaKeys[i] = new GradientAlphaKey(effectiveAlpha[i].alpha, effectiveAlpha[i].time);
            }

            gradient.alphaKeys = gradientAlphaKeys;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = sizeKeys != null;
        if (sizeOverLifetime.enabled)
        {
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, BuildCurve(sizeKeys));
        }

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialFolder + "/" + materialName + ".mat");

        EditorUtility.SetDirty(ps);
    }

    private static (float time, float alpha)[] AlphaKeys(params (float time, float alpha)[] keys)
    {
        return keys;
    }

    private static (float time, float size)[] SizeKeys(params (float time, float size)[] keys)
    {
        return keys;
    }

    private static AnimationCurve BuildCurve((float time, float size)[] keys)
    {
        var curve = new AnimationCurve();
        foreach (var key in keys)
        {
            curve.AddKey(key.time, key.size);
        }

        return curve;
    }
}
