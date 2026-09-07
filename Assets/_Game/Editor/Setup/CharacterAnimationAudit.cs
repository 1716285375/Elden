using System.IO;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    public static class CharacterAnimationAudit
    {
        [MenuItem("Tools/ZZ/Audit Character Animations")]
        public static void Audit()
        {
            var report = new StringBuilder();
            string root = "Assets/_Game/Art/Characters/Shared/Humanoid/";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(root + "AnimationControllers/Runtime/Humanoid Runtime.controller");
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                InspectStates(layer.stateMachine, layer.name, report);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { root + "Animations/Combat/Bow" }))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                report.AppendLine($"BOW {clip.name} human={clip.humanMotion} length={clip.length} curves={AnimationUtility.GetCurveBindings(clip).Length} events={string.Join(",", AnimationUtility.GetAnimationEvents(clip).Select(e => e.functionName))} path={AssetDatabase.GetAssetPath(clip)}");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/character-animation-audit.txt", report.ToString());
        }

        private static void InspectStates(AnimatorStateMachine machine, string parent, StringBuilder report)
        {
            foreach (ChildAnimatorState state in machine.states)
            {
                if (state.state.motion is AnimationClip clip)
                {
                    report.AppendLine($"STATE {parent}.{state.state.name} human={clip.humanMotion} curves={AnimationUtility.GetCurveBindings(clip).Length} clip={AssetDatabase.GetAssetPath(clip)} events={string.Join(",", AnimationUtility.GetAnimationEvents(clip).Select(e => e.functionName))}");
                }
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                InspectStates(child.stateMachine, parent + "." + child.stateMachine.name, report);
            }
        }

        [MenuItem("Tools/ZZ/Record AI Perception")]
        public static void RecordAI()
        {
            var report = new StringBuilder();
            foreach (PlayerManager player in Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None))
            {
                report.AppendLine($"PLAYER scene={player.gameObject.scene.name} spawned={player.IsSpawned} dead={player.IsDead} position={player.transform.position}");
            }
            foreach (AICharacterManager ai in Object.FindObjectsByType<AICharacterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Animator animator = ai.GetComponentInChildren<Animator>(true);
                string clips = animator != null && animator.isActiveAndEnabled ? string.Join(",", animator.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name)) : "inactive";
                report.AppendLine($"AI {ai.name} scene={ai.gameObject.scene.name} spawned={ai.IsSpawned} dead={ai.IsDead} awake={ai.IsAwake} state={ai.CurrentState} target={ai.CurrentTarget?.name} action={ai.IsPerformingAction} position={ai.transform.position} clips={clips}");
            }
            foreach (BossArenaController arena in Object.FindObjectsByType<BossArenaController>(FindObjectsSortMode.None))
            {
                report.AppendLine($"ARENA {arena.name} bounds={arena.GetComponent<BoxCollider>().bounds}");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/ai-perception.txt", report.ToString());
        }

        [MenuItem("Tools/ZZ/Verify AI Combat")]
        public static void VerifyCombat()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new System.InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            save.StartCoroutine(ObserveCombat());
        }

        private static IEnumerator ObserveCombat()
        {
            PlayerManager player = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).First(p => p.IsOwner);
            Vector3 previous = player.transform.position;
            bool invulnerable = player.IsInvulnerable;
            var report = new StringBuilder();
            try
            {
                player.SetInvulnerable(true);
                AICharacterManager[] targets = Object.FindObjectsByType<AICharacterManager>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Where(ai => ai.name.StartsWith("Undead AI") ||
                        ai.GetComponent<BossCharacterManager>() != null).OrderBy(ai =>
                        Vector3.Distance(ai.transform.position, player.transform.position)).ToArray();
                AICharacterManager[] samples = { targets.First(ai => ai.GetComponent<BossCharacterManager>() == null),
                    targets.First(ai => ai.GetComponent<BossCharacterManager>() != null) };
                foreach (AICharacterManager ai in samples)
                {
                    Vector3 destination = ai.transform.position + ai.transform.forward * 3f + Vector3.up * 0.2f;
                    CharacterController capsule = player.GetComponent<CharacterController>();
                    capsule.enabled = false;
                    player.transform.position = destination;
                    capsule.enabled = true;
                    Physics.SyncTransforms();
                    for (int frame = 0; frame < 12; frame++)
                    {
                        yield return new WaitForSecondsRealtime(1f);
                        if (ai == null)
                        {
                            report.AppendLine("Sample unloaded during streaming.");
                            break;
                        }
                        Animator animator = ai.GetComponentInChildren<Animator>(true);
                        string clips = string.Join(",", Enumerable.Range(0, animator.layerCount)
                            .SelectMany(layer => animator.GetCurrentAnimatorClipInfo(layer)).Select(c => c.clip.name));
                        report.AppendLine($"{ai.name} t={frame} dead={ai.IsDead} awake={ai.IsAwake} state={ai.CurrentState} " +
                            $"target={ai.CurrentTarget?.name} action={ai.IsPerformingAction} position={ai.transform.position} " +
                            $"encounter={ai.GetComponent<BossCharacterManager>()?.IsEncounterActive} " +
                            $"player={player.transform.position} " +
                            $"rotation={animator.transform.eulerAngles} scale={animator.transform.lossyScale} clips={clips}");
                        File.WriteAllText(".utmp/ai-combat-verification.txt", report.ToString());
                    }
                }
            }
            finally
            {
                player.SetInvulnerable(invulnerable);
                CharacterController capsule = player.GetComponent<CharacterController>();
                capsule.enabled = false;
                player.transform.position = previous;
                capsule.enabled = true;
                File.WriteAllText(".utmp/ai-combat-verification.txt", report.ToString());
            }
        }
    }
}
