using System.Collections;
using UnityEngine;

namespace ZZ
{
    /// <summary>Runs server-authoritative draw, aim, and fire behavior for a ranger.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AIRangerManager))]
    [RequireComponent(typeof(AIRangerEquipmentManager))]
    public sealed class AIRangerCombatManager : AICharacterCombatManager
    {
        [SerializeField, Min(0f)] private float m_minimumAimTime = 1f;
        [SerializeField, Min(0f)] private float m_maximumAimTime = 4f;
        [SerializeField, Min(0f)] private float m_minimumShootingDistance = 1f;
        [SerializeField, Range(0f, 180f)] private float m_viewableAngle = 35f;

        private AIRangerManager m_ranger;
        private AIRangerEquipmentManager m_rangerEquipment;
        private Coroutine m_aimCoroutine;

        protected override void Awake()
        {
            base.Awake();
            m_ranger = GetComponent<AIRangerManager>();
            m_rangerEquipment = GetComponent<AIRangerEquipmentManager>();
        }

        private void OnDisable()
        {
            StopAimCoroutine();
            DestroyDrawnProjectile();
        }

        /// <inheritdoc />
        public override bool PerformAttack(AICharacterAttackAction attackAction)
        {
            if (m_ranger == null ||
                !m_ranger.IsServer ||
                m_rangerEquipment?.Projectile == null)
            {
                return false;
            }

            m_ranger.CharacterNetworkManager?.SetNotchedProjectileState(
                true,
                true);
            bool startedAttack = base.PerformAttack(attackAction);
            if (!startedAttack)
            {
                CancelRangedAttack(false);
                return false;
            }

            StopAimCoroutine();
            m_aimCoroutine = StartCoroutine(HoldArrowForTime());
            return true;
        }

        /// <summary>Recreates a non-authoritative projectile from the server fire snapshot.</summary>
        public void PerformReleaseProjectileFromRpc(
            int projectileID,
            Vector3 releaseDirection)
        {
            RangedProjectileItem projectile = WorldItemDatabase.Instance
                ?.GetProjectileByID(projectileID) ?? m_rangerEquipment?.Projectile;
            if (projectile == null)
            {
                return;
            }

            DestroyDrawnProjectile();
            SpawnRangedProjectile(projectile, releaseDirection, false);
            m_rangerEquipment?.SetRangedWeaponState(false, false);
            m_ranger?.CharacterSoundFXManager?.PlayRangedWeaponSound(
                m_rangerEquipment?.Bow,
                true);
        }

        /// <inheritdoc />
        public override void ResetActionState()
        {
            StopAimCoroutine();
            m_rangerEquipment?.SetRangedWeaponState(false, false);
            base.ResetActionState();
        }

        /// <summary>Returns a sanitized randomized aim duration.</summary>
        public static float SelectAimDuration(
            float minimumAimTime,
            float maximumAimTime,
            float randomValue)
        {
            float minimum = Mathf.Max(0f, Mathf.Min(
                minimumAimTime,
                maximumAimTime));
            float maximum = Mathf.Max(minimum, Mathf.Max(
                minimumAimTime,
                maximumAimTime));
            return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(randomValue));
        }

        /// <summary>Checks the minimum distance and horizontal facing cone.</summary>
        public static bool CanFireAtTarget(
            Vector3 forward,
            Vector3 directionToTarget,
            float targetDistance,
            float minimumShootingDistance,
            float viewableAngle)
        {
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                forward,
                Vector3.up);
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(
                directionToTarget,
                Vector3.up);
            if (targetDistance <= Mathf.Max(0f, minimumShootingDistance) ||
                horizontalForward.sqrMagnitude <= Mathf.Epsilon ||
                horizontalDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            return Vector3.Angle(horizontalForward, horizontalDirection) <=
                Mathf.Clamp(viewableAngle, 0f, 180f);
        }

        protected override RangedProjectileItem ResolveCurrentRangedProjectile()
        {
            return m_rangerEquipment?.Projectile;
        }

        protected override Transform ResolveProjectileDrawHand()
        {
            return m_rangerEquipment?.DrawHand ?? base.ResolveProjectileDrawHand();
        }

        protected override Vector3 ResolveProjectileReleaseDirection()
        {
            Transform releaseOrigin = m_ranger?.LockOnTransform;
            PlayerManager target = m_ranger?.CurrentTarget;
            if (releaseOrigin == null || target == null)
            {
                return m_ranger != null
                    ? m_ranger.transform.forward
                    : Vector3.forward;
            }

            Vector3 direction = target.LockOnTransform.position -
                releaseOrigin.position;
            return direction.sqrMagnitude > Mathf.Epsilon
                ? direction.normalized
                : m_ranger.transform.forward;
        }

        protected override bool PrepareProjectileRelease(
            RangedProjectileItem projectile)
        {
            return m_ranger?.IsServer == true &&
                projectile == m_rangerEquipment?.Projectile;
        }

        protected override bool ShouldReleasedProjectileApplyDamage =>
            m_ranger?.IsServer == true;

        protected override void OnProjectileDrawn(
            RangedProjectileItem projectile)
        {
            m_rangerEquipment?.SetRangedWeaponState(true, true);
            m_ranger?.CharacterSoundFXManager?.PlayRangedWeaponSound(
                m_rangerEquipment?.Bow,
                false);
        }

        protected override void OnProjectileReleased(
            RangedProjectileItem projectile,
            Vector3 releaseDirection)
        {
            StopAimCoroutine();
            m_rangerEquipment?.SetRangedWeaponState(false, false);
            m_ranger?.CharacterSoundFXManager?.PlayRangedWeaponSound(
                m_rangerEquipment?.Bow,
                true);
            m_ranger?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            m_ranger?.CharacterNetworkManager?.SetNotchedProjectileState(
                false,
                false);
            m_ranger?.GetComponent<AICharacterNetworkManager>()
                ?.ReplicateRangedProjectile(
                    projectile.ItemID,
                    releaseDirection);
        }

        protected override void OnProjectileReleaseFailed()
        {
            CancelRangedAttack(true);
        }

        private IEnumerator HoldArrowForTime()
        {
            float aimDuration = SelectAimDuration(
                m_minimumAimTime,
                m_maximumAimTime,
                Random.value);
            yield return new WaitForSeconds(aimDuration);

            while (m_ranger != null && m_ranger.HasValidTarget)
            {
                PlayerManager target = m_ranger.CurrentTarget;
                Vector3 directionToTarget = target.LockOnTransform.position -
                    m_ranger.LockOnTransform.position;
                m_ranger.SetCanRotate(true);
                m_ranger.FaceTarget();
                if (CanFireAtTarget(
                        m_ranger.transform.forward,
                        directionToTarget,
                        directionToTarget.magnitude,
                        m_minimumShootingDistance,
                        m_viewableAngle))
                {
                    m_ranger.SetCanRotate(false);
                    m_ranger.CharacterNetworkManager?.SetHoldingArrowState(false);
                    m_aimCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            m_aimCoroutine = null;
            CancelRangedAttack(true);
        }

        private void CancelRangedAttack(bool resetActionFlags)
        {
            StopAimCoroutine();
            DestroyDrawnProjectile();
            m_rangerEquipment?.SetRangedWeaponState(false, false);
            m_ranger?.CharacterNetworkManager?.SetNotchedProjectileState(
                false,
                false);
            if (resetActionFlags)
            {
                m_ranger?.ResetActionFlags();
            }
        }

        private void StopAimCoroutine()
        {
            if (m_aimCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_aimCoroutine);
            m_aimCoroutine = null;
        }
    }
}
