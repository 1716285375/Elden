using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Restores a persistent Site of Grace and coordinates server-owned world resets.
    /// </summary>
    public class SiteOfGraceInteractable : Interactable
    {
        private const string k_RestorePrompt = "Restore Site of Grace";
        private const string k_RestPrompt = "Rest";
        private const string k_RestingMessage = "Resting at Site of Grace";

        [Header("SITE OF GRACE")]
        [SerializeField, Min(1)] private int m_siteOfGraceID = 1;
        [SerializeField] private ParticleSystem[] m_graceParticles =
            System.Array.Empty<ParticleSystem>();
        [SerializeField] private Light m_graceLight;
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private AudioClip m_activationSound;
        [SerializeField] private AudioClip m_restSound;
        [SerializeField, Min(0.1f)] private float m_restDuration = 3f;
        [SerializeField, Min(0.1f)] private float m_maxInteractionDistance = 5f;
        [SerializeField] private Transform m_teleportTransform;

        private readonly NetworkVariable<bool> m_isActivated = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Coroutine m_restRoutine;

        /// <summary>Gets the stable identifier stored in the character save file.</summary>
        public int SiteOfGraceID => m_siteOfGraceID;

        /// <summary>Gets the synchronized activation state.</summary>
        public bool IsActivated => m_isActivated.Value;

        /// <summary>Gets the authored destination used for local fast travel.</summary>
        public Transform TeleportTransform => m_teleportTransform;

        protected override void Awake()
        {
            base.Awake();
            m_audioSource ??= GetComponent<AudioSource>();
            ApplyActivationPresentation(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_isActivated.OnValueChanged += OnIsActivatedChanged;
            if (IsServer)
            {
                m_isActivated.Value =
                    WorldSaveGameManager.Instance?.IsSiteOfGraceActivated(
                        m_siteOfGraceID) == true;
            }

            OnIsActivatedChanged(false, m_isActivated.Value);
            WorldObjectManager.Instance?.RegisterSiteOfGrace(this);
        }

        public override void OnNetworkDespawn()
        {
            WorldObjectManager.Instance?.UnregisterSiteOfGrace(this);
            m_isActivated.OnValueChanged -= OnIsActivatedChanged;
            if (m_restRoutine != null)
            {
                StopCoroutine(m_restRoutine);
                m_restRoutine = null;
            }

            SetInteractionColliderEnabled(false);
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player) || !IsSpawned)
            {
                return;
            }

            RequestSiteOfGraceInteractionServerRpc(player.NetworkObjectId);
        }

        /// <summary>Moves the locally owned player to this Site's separate destination.</summary>
        public bool TeleportLocalPlayer()
        {
            NetworkObject playerNetworkObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            PlayerManager player =
                playerNetworkObject?.GetComponent<PlayerManager>();
            return TeleportPlayer(player, m_teleportTransform);
        }

        /// <summary>Applies one owner-authoritative teleport to an authored destination.</summary>
        public static bool TeleportPlayer(
            PlayerManager player,
            Transform teleportTransform)
        {
            if (player == null || !player.IsOwner || teleportTransform == null)
            {
                return false;
            }

            CharacterController characterController =
                player.GetComponent<CharacterController>();
            bool wasControllerEnabled = characterController?.enabled == true;
            if (wasControllerEnabled)
            {
                characterController.enabled = false;
            }

            player.transform.SetPositionAndRotation(
                teleportTransform.position,
                teleportTransform.rotation);
            Physics.SyncTransforms();
            if (wasControllerEnabled)
            {
                characterController.enabled = true;
            }

            player.ResetActionFlags();
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSiteOfGraceInteractionServerRpc(
            ulong playerNetworkObjectId,
            ServerRpcParams serverRpcParams = default)
        {
            if (!TryResolvePlayer(playerNetworkObjectId, out PlayerManager player) ||
                player.OwnerClientId != serverRpcParams.Receive.SenderClientId ||
                player.IsDead ||
                !IsPlayerWithinInteractionDistance(player))
            {
                return;
            }

            if (!m_isActivated.Value)
            {
                WorldSaveGameManager.Instance?.RecordSiteOfGraceActivation(
                    m_siteOfGraceID,
                    true,
                    true);
                m_isActivated.Value = true;
                PlayActivationFeedback();
                PlayActivationFeedbackClientRpc(playerNetworkObjectId);
                return;
            }

            BeginRest(player);
            BeginRestClientRpc(playerNetworkObjectId);
            WorldAIManager.Instance?.ResetAllCharacters();
        }

        [ClientRpc]
        private void PlayActivationFeedbackClientRpc(ulong playerNetworkObjectId)
        {
            if (IsServer)
            {
                return;
            }

            PlayActivationFeedback();
            if (TryResolvePlayer(playerNetworkObjectId, out PlayerManager player) &&
                player.IsOwner)
            {
                WorldSaveGameManager.Instance?.RecordSiteOfGraceActivation(
                    m_siteOfGraceID,
                    true,
                    true);
            }
        }

        [ClientRpc]
        private void BeginRestClientRpc(ulong playerNetworkObjectId)
        {
            if (IsServer ||
                !TryResolvePlayer(playerNetworkObjectId, out PlayerManager player))
            {
                return;
            }

            BeginRest(player);
        }

        private void OnIsActivatedChanged(bool wasActivated, bool isActivated)
        {
            ApplyActivationPresentation(isActivated);
            SetInteractableText(isActivated ? k_RestPrompt : k_RestorePrompt);
        }

        private void ApplyActivationPresentation(bool isActivated)
        {
            if (m_graceLight != null)
            {
                m_graceLight.enabled = isActivated;
            }

            foreach (ParticleSystem graceParticle in m_graceParticles)
            {
                if (graceParticle == null)
                {
                    continue;
                }

                if (isActivated)
                {
                    graceParticle.Play(true);
                }
                else
                {
                    graceParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void PlayActivationFeedback()
        {
            if (m_audioSource != null && m_activationSound != null)
            {
                m_audioSource.PlayOneShot(m_activationSound);
            }
        }

        private void BeginRest(PlayerManager player)
        {
            if (m_restRoutine != null)
            {
                StopCoroutine(m_restRoutine);
            }

            SetInteractionColliderEnabled(false);
            FacePlayerTowardsGrace(player);
            player.PlayerAnimatorManager?.PlayTargetActionAnimation(
                CharacterActionAnimation.RestAtSiteOfGrace,
                true,
                false,
                false,
                false);
            if (m_audioSource != null && m_restSound != null)
            {
                m_audioSource.PlayOneShot(m_restSound);
            }

            if (player.IsOwner)
            {
                RestorePlayerResources(player);
                PlayerUIManager.Instance?.PlayerUIPopUpManager
                    ?.SendPlayerMessagePopup(k_RestingMessage);
                WorldSaveGameManager saveGameManager =
                    WorldSaveGameManager.Instance;
                saveGameManager?.RecordLastSiteOfGraceRestedAt(
                    m_siteOfGraceID,
                    false);
                if (saveGameManager?.CanSaveGame == true)
                {
                    saveGameManager.SaveGame();
                }
            }

            m_restRoutine = StartCoroutine(
                WaitForAnimationAndPopupThenRestoreCollider(player));
        }

        private IEnumerator WaitForAnimationAndPopupThenRestoreCollider(
            PlayerManager player)
        {
            yield return new WaitForSeconds(m_restDuration);

            player?.ResetActionFlags();
            if (player != null && player.IsOwner)
            {
                PlayerUIManager.Instance?.PlayerUIPopUpManager
                    ?.CloseAllPopUpWindows();
                PlayerUIManager.Instance?.PlayerUISiteOfGraceManager
                    ?.OpenSiteOfGraceMenu();
            }

            SetInteractionColliderEnabled(true);
            m_restRoutine = null;
        }

        private static void RestorePlayerResources(PlayerManager player)
        {
            CharacterNetworkManager networkManager = player.CharacterNetworkManager;
            if (networkManager == null || !networkManager.IsOwner)
            {
                return;
            }

            networkManager.CurrentHealth.Value =
                Mathf.Max(0f, networkManager.MaxHealth.Value);
            networkManager.CurrentStamina.Value =
                Mathf.Max(0f, networkManager.MaxStamina.Value);
        }

        private bool IsPlayerWithinInteractionDistance(PlayerManager player)
        {
            Vector3 closestPoint = InteractableCollider.ClosestPoint(
                player.transform.position);
            return Vector3.Distance(closestPoint, player.transform.position) <=
                m_maxInteractionDistance;
        }

        private void FacePlayerTowardsGrace(PlayerManager player)
        {
            Vector3 direction = transform.position - player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                player.transform.rotation = Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
            }
        }

        private static bool TryResolvePlayer(
            ulong playerNetworkObjectId,
            out PlayerManager player)
        {
            player = null;
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    playerNetworkObjectId,
                    out NetworkObject playerNetworkObject))
            {
                return false;
            }

            player = playerNetworkObject.GetComponent<PlayerManager>();
            return player != null;
        }
    }
}
