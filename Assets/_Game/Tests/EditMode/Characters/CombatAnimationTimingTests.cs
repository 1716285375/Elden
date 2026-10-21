using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class CombatAnimationTimingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CombatActionsUseFixedTimeBlendingAndStartAtTheirFirstFrame(bool dodge)
        {
            GameObject instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab"));
            try
            {
                Component manager = instance.GetComponentsInChildren<Component>(true)
                    .First(c => c != null && c.GetType().Name == "PlayerAnimatorManager");
                Component player = instance.GetComponent("PlayerManager");
                player.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, null);
                Component combat = instance.GetComponent("PlayerCombatManager");
                combat.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(combat, null);
                Animator animator = manager.GetComponent<Animator>();
                Type baseType = manager.GetType().BaseType;
                baseType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                animator.Rebind();
                int layer = animator.GetLayerIndex("Action Override");
                animator.Play("Action Override.Empty", layer, 0f);
                animator.Update(0f);
                if (dodge)
                {
                    Type actionType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType("ZZ.CharacterActionAnimation")).First(t => t != null);
                    baseType.GetMethod("PlayTargetActionAnimation").Invoke(manager,
                        new[] { Enum.Parse(actionType, "RollForward"), (object)true, true, false, false });
                }
                else
                {
                    Type attackType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType("ZZ.AttackType")).First(t => t != null);
                    baseType.GetMethod("PlayTargetAttackActionAnimation").Invoke(manager,
                        new[] { Enum.Parse(attackType, "LightAttack01"), null, (object)true });
                }
                animator.Update(0f);
                Assert.That(animator.IsInTransition(layer), Is.True);
                AnimatorTransitionInfo transition = animator.GetAnimatorTransitionInfo(layer);
                Assert.That(transition.durationUnit, Is.EqualTo(DurationUnit.Fixed));
                Assert.That(transition.duration, Is.InRange(0.03f, 0.12f));
                Assert.That(animator.GetNextAnimatorStateInfo(layer).normalizedTime, Is.LessThan(0.03f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
