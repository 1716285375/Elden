using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Observes actual animation events and recovery for all authored creature attacks.</summary>
    public static class EnemyAttackRuntimeSmoke
    {
        public const string ReportPath = "Docs/CombatRefresh/enemy-runtime-smoke.txt";

        [MenuItem("Tools/ZZ/Elden Ring/Verify Original Boss Entry")]
        public static void VerifyBossEntry()
        {
            if (!Application.isPlaying || Unity.Netcode.NetworkManager.Singleton?.IsHost != true)
            {
                throw new InvalidOperationException("Load a fresh cave Host session first.");
            }
            WorldSaveGameManager.Instance.StartCoroutine(RunBossEntry());
        }

        private static IEnumerator RunBossEntry()
        {
            const string report = "Docs/OriginalSingleMap/boss-entry-runtime.txt";
            File.WriteAllText(report, "RUNNING: original MSB entry region, source waking state and Boss HUD.\n");
            PlayerManager player = PlayerUIManager.Instance.LocalPlayer;
            BossCharacterManager boss = UnityEngine.Object.FindFirstObjectByType<BossCharacterManager>(FindObjectsInactive.Include);
            BossArenaController region = UnityEngine.Object.FindFirstObjectByType<BossArenaController>();
            if (boss == null || region == null || boss.IsEncounterActive)
            {
                throw new InvalidOperationException("Expected the original dormant encounter.");
            }
            AICharacterManager enemy = boss.GetComponent<AICharacterManager>();
            bool enabledState = enemy.enabled;
            bool locomotion = player.LocomotionManager.enabled;
            Vector3 position = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            enemy.enabled = false;
            player.LocomotionManager.enabled = false;
            player.SetInvulnerable(true);
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            CharacterSlot savedSlot = save.CurrentCharacterSlot;
            CharacterSaveData savedData = JsonUtility.FromJson<CharacterSaveData>(JsonUtility.ToJson(save.CurrentCharacterData));
            System.Reflection.FieldInfo slotField = typeof(WorldSaveGameManager).GetField("m_currentCharacterSlotBeingUsed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            slotField.SetValue(save, CharacterSlot.NoSlot);
            try
            {
                MeshCollider barrier = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                    .Single(collider => collider.name == "h900100");
                Vector3[] vertices = barrier.sharedMesh.vertices;
                int[] indices = barrier.sharedMesh.triangles;
                Vector3 normal = barrier.transform.TransformDirection(Vector3.Cross(vertices[indices[1]] - vertices[indices[0]],
                    vertices[indices[2]] - vertices[indices[0]]));
                normal.y = 0f;
                normal.Normalize();
                if (!Physics.Raycast(barrier.bounds.center + Vector3.up, Vector3.down, out RaycastHit ground, 8f,
                    ~0, QueryTriggerInteraction.Ignore))
                {
                    throw new InvalidOperationException("No original floor at the NPC-only fog boundary.");
                }
                Vector3 start = ground.point + normal + Vector3.up * 0.05f;
                player.LocomotionManager.WarpTo(start, rotation);
                player.GetComponent<CharacterController>().Move(-normal * 2f);
                bool crossed = Vector3.Dot(player.transform.position - start, -normal) > 1.5f;
                File.AppendAllText(report, (crossed ? "PASS" : "FAIL") + ": player crosses the original NPC-only collision\n");
                if (!crossed)
                {
                    throw new InvalidOperationException("The NPC-only collision still blocks player movement.");
                }
                BoxCollider box = region.GetComponent<BoxCollider>();
                Vector3 entrance = box.transform.TransformPoint(new Vector3(box.center.x, 0.5f, box.center.z));
                if (!UnityEngine.AI.NavMesh.SamplePosition(entrance, out var floor, 3f, UnityEngine.AI.NavMesh.AllAreas) ||
                    !box.bounds.Contains(floor.position))
                {
                    throw new InvalidOperationException("The original entry region must contain a navigation floor.");
                }
                player.LocomotionManager.WarpTo(floor.position, rotation);
                Animator animator = enemy.GetComponentInChildren<Animator>(true);
                int layer = animator.GetLayerIndex("Action Override");
                int waking = Animator.StringToHash("Action Override.a000_020003");
                bool woke = false;
                float deadline = Time.realtimeSinceStartup + 20f;
                do
                {
                    woke |= animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == waking ||
                        (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).fullPathHash == waking);
                    yield return null;
                }
                while ((!boss.IsEncounterActive || !woke) && Time.realtimeSinceStartup < deadline);
                PlayerUIBossHealthBar bar = PlayerUIManager.Instance.PlayerUIBossHealthBar;
                bool passed = boss.BossID == 31030800 && boss.IsEncounterActive && enemy.gameObject.activeInHierarchy &&
                    woke && bar != null && bar.gameObject.activeInHierarchy;
                File.AppendAllText(report, (passed ? "PASS" : "FAIL") + ": id=" + boss.BossID +
                    " encounter=" + boss.IsEncounterActive + " active=" + enemy.gameObject.activeInHierarchy +
                    " originalWake=" + woke + " hud=" + (bar != null && bar.gameObject.activeInHierarchy) +
                    " health=" + boss.CharacterNetworkManager.CurrentHealth.Value + "/" +
                    boss.CharacterNetworkManager.MaxHealth.Value + "\n");
                if (!passed)
                {
                    throw new InvalidOperationException("Original Boss entry, waking animation or HUD did not activate.");
                }
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/OriginalSingleMap/boss-entry-runtime.png"));
                yield return new WaitForEndOfFrame();
            }
            finally
            {
                player.SetInvulnerable(false);
                player.LocomotionManager.WarpTo(position, rotation);
                player.LocomotionManager.enabled = locomotion;
                enemy.enabled = enabledState;
                typeof(WorldSaveGameManager).GetField("m_currentCharacterData",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(save, savedData);
                save.LoadAllCharacterSlots();
                slotField.SetValue(save, savedSlot);
            }
        }

        [MenuItem("Tools/ZZ/Elden Ring/Verify Original Creature Attacks")]
        public static void VerifyOriginalAttacks()
        {
            if (!Application.isPlaying || Unity.Netcode.NetworkManager.Singleton?.IsHost != true ||
                WorldAIManager.Instance?.IsPerformingLoadingOperation != false)
            {
                throw new InvalidOperationException("Load the original cave as host before verifying attacks.");
            }
            WorldSaveGameManager.Instance.StartCoroutine(RunOriginal());
        }

        private static IEnumerator RunOriginal()
        {
            const string report = "Docs/OriginalSingleMap/attack-runtime.txt";
            File.WriteAllText(report, "RUNNING: real Animator states, source TAE windows and recovery.\n");
            PlayerManager player = PlayerUIManager.Instance.LocalPlayer;
            Vector3 playerPosition = player.transform.position;
            Quaternion playerRotation = player.transform.rotation;
            bool locomotionEnabled = player.LocomotionManager.enabled;
            player.LocomotionManager.enabled = false;
            AICharacterManager[] enemies = UnityEngine.Object.FindObjectsByType<AICharacterManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool[] enabledStates = enemies.Select(enemy => enemy.enabled).ToArray();
            foreach (AICharacterManager enemy in enemies)
            {
                enemy.enabled = false;
            }
            player.SetInvulnerable(true);
            int verified = 0;
            int actors = 0;
            try
            {
                foreach (OriginalCreatureCombat bridge in UnityEngine.Object.FindObjectsByType<OriginalCreatureCombat>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).GroupBy(component =>
                        new SerializedObject(component).FindProperty("m_actor").stringValue).Select(group => group.First()))
                {
                    AICharacterManager enemy = bridge.GetComponentInParent<AICharacterManager>(true);
                    enemy.ActivateCharacter(player);
                    enemy.GetComponent<AICharacterNetworkManager>().SetAwakeState(true, null, null, false);
                    Animator animator = bridge.GetComponent<Animator>();
                    animator.enabled = true;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var combat = enemy.GetComponent<AICharacterCombatManager>();
                    Vector3 origin = enemy.transform.position;
                    Quaternion rotation = enemy.transform.rotation;
                    UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    var data = new SerializedObject(enemy);
                    SerializedProperty list = data.FindProperty("m_combatStanceAttacks");
                    AICharacterAttackAction[] actions = Enumerable.Range(0, list.arraySize).Select(index =>
                        (AICharacterAttackAction)list.GetArrayElementAtIndex(index).objectReferenceValue).ToArray();
                    foreach (AICharacterAttackAction action in actions)
                    {
                        agent.Warp(origin);
                        enemy.transform.rotation = rotation;
                        Vector3 requested = origin - enemy.transform.forward * 3f;
                        if (!UnityEngine.AI.NavMesh.SamplePosition(requested, out var safePosition, 5f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            throw new InvalidOperationException("No safe attack verification position near " + enemy.name);
                        }
                        player.LocomotionManager.WarpTo(safePosition.position, rotation);
                        enemy.ResetActionFlags();
                        enemy.CharacterAnimatorManager.PlayEmptyActionAnimation();
                        yield return new WaitForSeconds(0.2f);
                        bool started = combat.PerformAttack(action);
                        if (!started)
                        {
                            File.AppendAllText(report, "FAIL: could not start " + action.name + "\n");
                            throw new InvalidOperationException("Original attack did not start: " + action.name);
                        }
                        bool window = false;
                        ulong sweepsBefore = bridge.CompletedSweeps;
                        int layer = animator.GetLayerIndex("Action Override");
                        int expectedState = Animator.StringToHash(action.AttackDefinition.AnimationState);
                        bool state = false;
                        float deadline = Time.time + Mathf.Max(10f, action.RecoveryTime + 3f);
                        float wallDeadline = Time.realtimeSinceStartup + 60f;
                        do
                        {
                            window |= combat.ActionRunner.Windows.IsOpen(CombatWindowType.Hit);
                            state |= animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == expectedState ||
                                (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).fullPathHash == expectedState);
                            yield return null;
                        }
                        while (enemy.IsPerformingAction && Time.time < deadline && Time.realtimeSinceStartup < wallDeadline);
                        bool swept = bridge.CompletedSweeps > sweepsBefore;
                        bool passed = window && swept && state && !enemy.IsPerformingAction;
                        File.AppendAllText(report, (passed ? "PASS: " : "FAIL: ") + action.name +
                            " state=" + state + " hitWindow=" + window + " sourceSweep=" + swept +
                            " recovered=" + !enemy.IsPerformingAction + " active=" + enemy.gameObject.activeInHierarchy +
                            " animator=" + animator.enabled + " normalized=" + animator.GetCurrentAnimatorStateInfo(layer).normalizedTime +
                            " speed=" + animator.speed + " actor=" + enemy.name +
                            " player=" + player.transform.position + " enemy=" + enemy.transform.position + "\n");
                        if (!passed)
                        {
                            throw new InvalidOperationException("Original attack animation/window/recovery failed: " + action.name);
                        }
                        verified++;
                    }
                    actors++;
                }
                if (actors != 3 || verified == 0)
                {
                    throw new InvalidOperationException("Expected all three original creature variants.");
                }
                File.AppendAllText(report, "PASS: actors=" + actors + " attacks=" + verified +
                    "; damage amounts, client replication and full AI decisions not covered.\n");
            }
            finally
            {
                player.SetInvulnerable(false);
                player.LocomotionManager.WarpTo(playerPosition, playerRotation);
                player.LocomotionManager.enabled = locomotionEnabled;
                for (int index = 0; index < enemies.Length; index++)
                {
                    if (enemies[index] != null)
                    {
                        enemies[index].enabled = enabledStates[index];
                    }
                }
            }
        }

        public static IEnumerator Run()
        {
            File.WriteAllText(ReportPath, "RUNNING\n");
            int verifiedAttacks = 0;
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .Single(candidate => candidate.IsOwner);
            foreach (AICharacterManager enemy in UnityEngine.Object.FindObjectsByType<AICharacterManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(enemy);
                SerializedProperty list = data.FindProperty("m_combatStanceAttacks");
                if (list.arraySize != 3 ||
                    !(enemy.GetComponentInChildren<Animator>(true).runtimeAnimatorController is AnimatorOverrideController))
                {
                    continue;
                }
                var actions = Enumerable.Range(0, list.arraySize).Select(index =>
                    list.GetArrayElementAtIndex(index).objectReferenceValue as AICharacterAttackAction).ToArray();
                if (actions.Any(action => action == null || !AssetDatabase.GetAssetPath(action)
                    .StartsWith("Assets/_Game/Data/Combat/EnemyMovesets/", StringComparison.Ordinal)))
                {
                    continue;
                }
                Animator animator = enemy.GetComponentInChildren<Animator>(true);
                enemy.ActivateCharacter(player);
                enemy.GetComponent<AICharacterNetworkManager>().SetAwakeState(true, null, null, false);
                enemy.enabled = false;
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var combat = enemy.GetComponent<AICharacterCombatManager>();
                Collider[] hitBoxes = enemy.GetComponentsInChildren<AIDamageCollider>(true)
                    .Select(collider => collider.GetComponent<Collider>()).ToArray();
                foreach (AICharacterAttackAction action in actions)
                {
                    enemy.ResetActionFlags();
                    enemy.CharacterAnimatorManager.PlayEmptyActionAnimation();
                    combat.CloseDamageColliders();
                    yield return null;
                    int layer = animator.GetLayerIndex("Action Override");
                    float neutralDeadline = Time.realtimeSinceStartup + 2f;
                    while (animator.IsInTransition(layer) && Time.realtimeSinceStartup < neutralDeadline)
                    {
                        yield return null;
                    }
                    Check(!animator.IsInTransition(layer), enemy.name + ": neutral transition settles");
                    Check(combat.PerformAttack(action), enemy.name + ": starts " + action.AttackType);
                    bool sawHitWindow = false;
                    float deadline = Time.realtimeSinceStartup + 5f;
                    do
                    {
                        sawHitWindow |= hitBoxes.Any(collider => collider != null && collider.enabled);
                        yield return null;
                    }
                    while (enemy.IsPerformingAction && Time.realtimeSinceStartup < deadline);
                    Check(sawHitWindow, enemy.name + ": animation opens hit window " + action.AttackType +
                        $"; active={enemy.gameObject.activeInHierarchy}; speed={animator.speed}; boxes={hitBoxes.Length}; " +
                        $"normalized={animator.GetCurrentAnimatorStateInfo(animator.GetLayerIndex("Action Override")).normalizedTime:F2}");
                    Check(!enemy.IsPerformingAction, enemy.name + ": returns to neutral " + action.AttackType);
                    Check(hitBoxes.All(collider => collider == null || !collider.enabled),
                        enemy.name + ": recovery closes every hit box " + action.AttackType);
                    verifiedAttacks++;
                }
            }
            Check(verifiedAttacks == 21, "Observed all 21 creature attacks, actual=" + verifiedAttacks);
            File.AppendAllText(ReportPath, "PASS\n");
        }

        private static void Check(bool passed, string message)
        {
            File.AppendAllText(ReportPath, (passed ? "PASS: " : "FAIL: ") + message + "\n");
            if (!passed)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
