using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Exercises commit and cancellation against the real player, Animator and network state.</summary>
    public static class CombatFrameworkSmoke
    {
        private const string k_Report = ".utmp/combat-framework-smoke.txt";

        [MenuItem("Tools/ZZ/Combat/Verify Attack Commit")]
        public static void Verify()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }

            File.WriteAllText(k_Report, "Running combat framework smoke\n");
            save.StartCoroutine(Observe());
        }

        private static IEnumerator Observe()
        {
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            PlayerNetworkManager network = player.PlayerNetworkManager;
            PlayerCombatManager combat = player.PlayerCombatManager;
            WeaponItem weapon = player.InventoryManager.CurrentRightHandWeapon;
            float cost = weapon.GetAttackStaminaCost(AttackType.LightAttack01);
            Check(cost > 0f && weapon.Moveset != null, "Equipped weapon uses a paid, migrated moveset");
            player.ResetActionFlags();
            player.PlayerAnimatorManager.PlayEmptyActionAnimation();
            yield return null;
            network.CurrentStamina.Value = Mathf.Max(0f, cost - 1f);
            float before = network.CurrentStamina.Value;
            Check(!combat.TryStartAttack(AttackType.LightAttack01, weapon), "Insufficient stamina rejects attack");
            Check(network.CurrentStamina.Value == before && !network.IsAttacking.Value,
                "Rejected attack changes neither stamina nor attacking state");
            network.CurrentStamina.Value = network.MaxStamina.Value;
            before = network.CurrentStamina.Value;
            Check(combat.TryStartAttack(AttackType.LightAttack01, weapon), "Affordable attack starts");
            Check(Mathf.Approximately(network.CurrentStamina.Value, before - cost), "Cost paid at commit");
            before = network.CurrentStamina.Value;
            combat.DrainStaminaBasedOnAttack();
            combat.DrainStaminaBasedOnAttack();
            Check(network.CurrentStamina.Value == before, "Legacy animation events never charge twice");
            Check(!combat.TryStartAttack(AttackType.LightAttack02, weapon), "Closed combo window rejects replacement");
            combat.EnableCanCombo();
            Check(combat.TryPerformMainHandCombo(AttackType.LightAttack01), "Authored combo edge starts second attack");
            Check(combat.CurrentAttackType == AttackType.LightAttack02 &&
                !combat.ActionRunner.Windows.IsOpen(CombatWindowType.Combo), "Replacement clears old combo permission");
            player.PlayerAnimatorManager.EnableCanRoll();
            player.LocomotionManager.AttemptToPerformDodge();
            Check(!network.IsAttacking.Value && combat.ActionRunner.Phase == CombatActionPhase.None,
                "Dodge cancels attack only after roll-cancel window opens");
            yield return new WaitForSecondsRealtime(2f);
            Check(!player.IsDead && !player.IsPerformingAction, "Dodge returns to controllable locomotion");
            File.AppendAllText(k_Report, "ALL PASSED\n");
        }

        [MenuItem("Tools/ZZ/Combat/Verify Buffered Input")]
        public static void VerifyBufferedInput()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }

            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            PlayerInputManager input = PlayerInputManager.Instance;
            PlayerCombatManager combat = player.PlayerCombatManager;
            PlayerNetworkManager network = player.PlayerNetworkManager;
            WeaponItem weapon = player.InventoryManager.CurrentRightHandWeapon;
            player.ResetActionFlags();
            input.EnablePlayerControls();
            input.ClearAttackInputQueue();
            network.CurrentStamina.Value = network.MaxStamina.Value;
            Check(ReferenceEquals(input.CommandBuffer, combat.CommandBuffer), "Input and combat share one buffer");
            Check(combat.TryStartAttack(AttackType.LightAttack01, weapon), "Buffer smoke starts opening attack");
            uint sequence = combat.ActionRunner.Sequence;
            Check(!input.TryDispatchCombatCommand(CombatCommand.LightAttack), "Early light waits for combo window");
            Check(combat.CommandBuffer.Count == 1, "Early light remains pending");
            combat.EnableCanCombo();
            Check(combat.ActionRunner.Sequence != sequence && combat.CurrentAttackType == AttackType.LightAttack02,
                "Opening combo window consumes early light into second swing");
            Check(combat.CommandBuffer.Count == 0, "Follow-up consumes exactly one press");
            combat.CommandBuffer.Push(CombatCommand.LightAttack);
            player.ResetActionFlags();
            Check(combat.CommandBuffer.Count == 1, "Normal completion preserves early input");
            input.DisablePlayerControls();
            Check(combat.CommandBuffer.Count == 0, "Suspending controls clears pending input");
            input.EnablePlayerControls();
            network.CurrentStamina.Value = network.MaxStamina.Value;
            combat.CommandBuffer.Push(CombatCommand.HeavyAttack);
            Check(combat.CommandBuffer.TryConsumeFirstExecutable(combat.TryExecuteCommand, out _),
                "Released buffered heavy starts an attack");
            Check(!combat.IsChargingAttack && network.IsAttacking.Value,
                "Released buffered heavy never leaves a held charge behind");
            input.ClearAttackInputQueue();
            player.ResetActionFlags();
            player.PlayerAnimatorManager.PlayEmptyActionAnimation();
        }

        [MenuItem("Tools/ZZ/Combat/Verify Hit Resolution")]
        public static void VerifyHitResolution()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }

            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            AICharacterManager enemy = UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsSortMode.None)
                .First(candidate => !candidate.IsDead && WorldUtilityManager.CanDamageCharacter(candidate, player));
            PlayerNetworkManager network = player.PlayerNetworkManager;
            float health = network.CurrentHealth.Value;
            var weapon = new GameObject("Combat sweep smoke");
            try
            {
                player.ResetActionFlags();
                player.SetInvulnerable(false);
                network.SetBlockingState(false);
                network.SetParryingState(false);
                player.PlayerAnimatorManager.PlayEmptyActionAnimation();
                HurtBox body = player.GetComponentInChildren<HurtBox>();
                Check(player.HasDedicatedHurtBoxes && body != null && body.Owner == player,
                    "Player owns a dedicated damageable body independent of movement");
                Collider target = body.GetComponent<Collider>();
                Vector3 center = target.bounds.center;
                weapon.transform.position = center + Vector3.left * 1.5f;
                BoxCollider box = weapon.AddComponent<BoxCollider>();
                box.size = Vector3.one * 0.15f;
                box.isTrigger = true;
                var detector = weapon.AddComponent<MeleeWeaponDamageCollider>();
                detector.SetDamageSource(enemy);
                detector.SetDamageValues(10f, 0f, 0f, 0f, 0f, 0f);
                detector.SetStanceDamage(0f);
                detector.OpenDamageCollider();
                var trace = new WeaponTrace(detector, box);
                Physics.SyncTransforms();
                trace.Sample();
                Check(network.CurrentHealth.Value == health, "Separated weapon starts without a hit");
                weapon.transform.position = center + Vector3.right * 1.5f;
                Physics.SyncTransforms();
                trace.Sample();
                Check(network.CurrentHealth.Value < health, "Swept blade hits across a frame without endpoint overlap");
                float afterHit = network.CurrentHealth.Value;
                trace.Sample();
                detector.TryProcessHit(target);
                Check(network.CurrentHealth.Value == afterHit, "Sweep and trigger share one hit per target");
                detector.CloseDamageCollider();
                player.SetInvulnerable(true);
                detector.OpenDamageCollider();
                Check(detector.TryProcessHit(target).Outcome == HitOutcome.Invulnerable,
                    "Invulnerability rejects damage without consuming the target registry");
                Check(network.CurrentHealth.Value == afterHit, "Invulnerability preserves health");
                File.AppendAllText(k_Report, "HIT RESOLUTION ALL PASSED\n");
            }
            finally
            {
                player.SetInvulnerable(false);
                player.GetComponent<CombatHitFeedback>()?.Stop();
                enemy.GetComponent<CombatHitFeedback>()?.Stop();
                network.CurrentHealth.Value = health;
                UnityEngine.Object.Destroy(weapon);
            }
        }

        private static void Check(bool passed, string message)
        {
            File.AppendAllText(k_Report, $"{(passed ? "PASS" : "FAIL")}: {message}\n");
            if (!passed)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
