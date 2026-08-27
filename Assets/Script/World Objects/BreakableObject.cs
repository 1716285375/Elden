using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Predicts break effects locally while the server owns the shared broken state.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class BreakableObject : NetworkBehaviour
    {
        private const float k_DefaultExplosionForce = 350f;
        private const float k_DefaultExplosionRadius = 5f;
        private const float k_DefaultMinimumTorque = 250f;
        private const float k_DefaultMaximumTorque = 500f;

        [Header("Whole Object")]
        [SerializeField] private Renderer[] m_wholeObjectRenderers;
        [SerializeField] private Collider[] m_wholeObjectColliders;

        [Header("Broken Object")]
        [SerializeField] private GameObject m_brokenObjectPrefab;
        [SerializeField] private bool m_addForceOnBreak = true;
        [SerializeField, Min(0f)] private float m_explosionForce =
            k_DefaultExplosionForce;
        [SerializeField, Min(0f)] private float m_explosionRadius =
            k_DefaultExplosionRadius;
        [SerializeField, Min(0f)] private float m_minimumTorque =
            k_DefaultMinimumTorque;
        [SerializeField, Min(0f)] private float m_maximumTorque =
            k_DefaultMaximumTorque;

        [Header("Sound")]
        [SerializeField] private AudioClip[] m_brokenSoundEffects;

        public NetworkVariable<bool> IsBroken = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public NetworkVariable<Vector3> NetworkPosition =
            new NetworkVariable<Vector3>(
                Vector3.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        public NetworkVariable<Quaternion> NetworkRotation =
            new NetworkVariable<Quaternion>(
                Quaternion.identity,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private AudioSource m_audioSource;
        private GameObject m_instantiatedBrokenObject;
        private bool m_isBrokenLocal;

        /// <summary>Gets whether this peer has already applied the broken presentation.</summary>
        public bool IsBrokenLocal => m_isBrokenLocal;

        private void Awake()
        {
            m_audioSource = GetComponent<AudioSource>();
            ResolveWholeObjectComponents();
        }

        /// <inheritdoc />
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            IsBroken.OnValueChanged += OnIsBrokenChanged;
            NetworkPosition.OnValueChanged += OnNetworkPositionChanged;
            NetworkRotation.OnValueChanged += OnNetworkRotationChanged;

            if (IsServer)
            {
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
            }
            else
            {
                ApplyNetworkTransform();
            }

            OnIsBrokenChanged(IsBroken.Value, IsBroken.Value);
        }

        /// <inheritdoc />
        public override void OnNetworkDespawn()
        {
            IsBroken.OnValueChanged -= OnIsBrokenChanged;
            NetworkPosition.OnValueChanged -= OnNetworkPositionChanged;
            NetworkRotation.OnValueChanged -= OnNetworkRotationChanged;
            DestroyBrokenObject();
            m_isBrokenLocal = false;
            ToggleMeshRenderers(true);
            ToggleMeshColliders(true);
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Applies the break immediately on this peer and asks the server to confirm it.
        /// </summary>
        public void BreakObject()
        {
            if (IsBroken.Value || m_isBrokenLocal)
            {
                return;
            }

            PlayBreakEffects();
            if (IsSpawned && NetworkManager != null && NetworkManager.IsListening)
            {
                BreakObjectServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void BreakObjectServerRpc()
        {
            if (!IsBroken.Value)
            {
                IsBroken.Value = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || IsBroken.Value || m_isBrokenLocal)
            {
                return;
            }

            if (other.GetComponentInParent<DamageCollider>() != null ||
                other.GetComponentInParent<AICharacterManager>() != null)
            {
                BreakObject();
                return;
            }

            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            CharacterNetworkManager characterNetworkManager =
                player?.CharacterNetworkManager;
            if (player != null &&
                (player.IsJumping ||
                    characterNetworkManager?.IsRolling.Value == true))
            {
                BreakObject();
            }
        }

        private void OnIsBrokenChanged(bool previousValue, bool newValue)
        {
            if (newValue)
            {
                if (!m_isBrokenLocal)
                {
                    PlayBreakEffects();
                }

                return;
            }

            RestoreWholeObject();
        }

        private void OnNetworkPositionChanged(
            Vector3 previousValue,
            Vector3 newValue)
        {
            transform.position = newValue;
        }

        private void OnNetworkRotationChanged(
            Quaternion previousValue,
            Quaternion newValue)
        {
            transform.rotation = newValue;
        }

        private void ApplyNetworkTransform()
        {
            transform.SetPositionAndRotation(
                NetworkPosition.Value,
                NetworkRotation.Value);
        }

        private void PlayBreakEffects()
        {
            if (m_isBrokenLocal)
            {
                return;
            }

            m_isBrokenLocal = true;
            ToggleMeshRenderers(false);
            ToggleMeshColliders(false);
            if (m_brokenObjectPrefab != null)
            {
                m_instantiatedBrokenObject = Instantiate(
                    m_brokenObjectPrefab,
                    transform.position,
                    transform.rotation);
                if (m_addForceOnBreak)
                {
                    AddForceToBrokenObject(m_instantiatedBrokenObject);
                }
            }

            WorldSoundFXManager.Instance?.PlaySoundEffect(
                m_brokenSoundEffects,
                m_audioSource);
        }

        private void RestoreWholeObject()
        {
            DestroyBrokenObject();
            m_isBrokenLocal = false;
            ToggleMeshRenderers(true);
            ToggleMeshColliders(true);
        }

        private void DestroyBrokenObject()
        {
            if (m_instantiatedBrokenObject == null)
            {
                return;
            }

            Destroy(m_instantiatedBrokenObject);
            m_instantiatedBrokenObject = null;
        }

        private void ToggleMeshRenderers(bool isEnabled)
        {
            foreach (Renderer wholeRenderer in m_wholeObjectRenderers)
            {
                if (wholeRenderer != null)
                {
                    wholeRenderer.enabled = isEnabled;
                }
            }
        }

        private void ToggleMeshColliders(bool isEnabled)
        {
            foreach (Collider wholeCollider in m_wholeObjectColliders)
            {
                if (wholeCollider != null)
                {
                    wholeCollider.enabled = isEnabled;
                }
            }
        }

        private void AddForceToBrokenObject(GameObject brokenObject)
        {
            Rigidbody[] fragmentRigidbodies =
                brokenObject.GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody fragmentRigidbody in fragmentRigidbodies)
            {
                fragmentRigidbody.AddExplosionForce(
                    m_explosionForce,
                    transform.position,
                    m_explosionRadius);
                float torque = Random.Range(
                    Mathf.Min(m_minimumTorque, m_maximumTorque),
                    Mathf.Max(m_minimumTorque, m_maximumTorque));
                fragmentRigidbody.AddTorque(
                    Random.onUnitSphere * torque,
                    ForceMode.Impulse);
            }
        }

        private void ResolveWholeObjectComponents()
        {
            if (m_wholeObjectRenderers == null ||
                m_wholeObjectRenderers.Length == 0)
            {
                m_wholeObjectRenderers =
                    GetComponentsInChildren<Renderer>(true);
            }

            if (m_wholeObjectColliders == null ||
                m_wholeObjectColliders.Length == 0)
            {
                m_wholeObjectColliders =
                    GetComponentsInChildren<Collider>(true);
            }
        }
    }
}
