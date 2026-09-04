using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns one server-authoritative two-stop elevator and its local passenger motion.
    /// </summary>
    public class ElevatorInteractable : Interactable
    {
        [Header("ELEVATOR")]
        [SerializeField] private Transform m_elevatorPlatform;
        [SerializeField] private Vector3 m_destinationHigh =
            new(0f, 8f, 0f);
        [SerializeField] private Vector3 m_destinationLow = Vector3.zero;
        [SerializeField, Min(0.01f)] private float m_movementSpeed = 2f;
        [SerializeField, Min(0f)] private float m_arrivalTolerance = 0.01f;
        [SerializeField] private float m_movementOffset = 0.1f;
        [SerializeField, Min(0.1f)] private float m_maxInteractionDistance = 5f;

        [Header("AUDIO")]
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private AudioClip m_movementSound;
        [SerializeField] private AudioClip m_stopSound;

        public readonly NetworkVariable<Vector3> NetworkPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> ElevatorIsRising = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> ElevatorIsDescending = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly HashSet<CharacterManager> m_charactersOnElevator = new();
        private readonly List<CallElevatorInteractable> m_callStations = new();
        private Coroutine m_moveElevatorCoroutine;
        private bool m_localMovementActive;

        /// <summary>Raised locally when movement starts or finishes.</summary>
        public event Action<bool> MovementStateChanged;

        /// <summary>Gets whether either synchronized travel direction is active.</summary>
        public bool IsMoving =>
            ElevatorIsRising.Value || ElevatorIsDescending.Value;

        /// <summary>Gets the moving platform rather than the stationary network root.</summary>
        public Transform ElevatorPlatform => m_elevatorPlatform;

        /// <summary>Gets every valid character currently inside the occupancy trigger.</summary>
        public IReadOnlyCollection<CharacterManager> CharactersOnElevator =>
            m_charactersOnElevator;

        protected override void Awake()
        {
            base.Awake();
            m_elevatorPlatform ??= transform;
            m_audioSource ??= GetComponent<AudioSource>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            NetworkPosition.OnValueChanged += OnNetworkPositionChanged;
            ElevatorIsRising.OnValueChanged += OnMovementDirectionChanged;
            ElevatorIsDescending.OnValueChanged += OnMovementDirectionChanged;
            if (IsServer)
            {
                NetworkPosition.Value = GetPlatformLocalPosition();
            }

            SetPlatformLocalPosition(NetworkPosition.Value);
            if (IsMoving && !IsServer)
            {
                BeginLocalMovement(ElevatorIsRising.Value);
            }

            RefreshCallStations();
        }

        public override void OnNetworkDespawn()
        {
            NetworkPosition.OnValueChanged -= OnNetworkPositionChanged;
            ElevatorIsRising.OnValueChanged -= OnMovementDirectionChanged;
            ElevatorIsDescending.OnValueChanged -= OnMovementDirectionChanged;
            StopMovementCoroutine();
            ClearCharactersOnElevator();
            SetInteractionColliderEnabled(false);
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return player != null &&
                player.IsOwner &&
                enabled &&
                gameObject.activeInHierarchy &&
                InteractableCollider != null &&
                InteractableCollider.enabled &&
                IsSpawned &&
                !IsMoving;
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            ActivateElevatorServerRpc(player.NetworkObjectId);
        }

        /// <summary>The elevator manages its reusable Collider from replicated state.</summary>
        public override void CompleteInteraction()
        {
        }

        /// <summary>Requests a direction toggle from one validated overlapping player.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ActivateElevatorServerRpc(
            ulong playerNetworkObjectId,
            RpcParams rpcParams = default)
        {
            if (!TryResolvePlayer(
                    playerNetworkObjectId,
                    out PlayerManager player) ||
                player.OwnerClientId != rpcParams.Receive.SenderClientId ||
                player.IsDead ||
                !IsPlayerWithinInteractionDistance(player))
            {
                return;
            }

            ActivateElevatorFromServer();
        }

        /// <summary>Starts the next direction from a server-side pressure button.</summary>
        public bool ActivateElevatorFromServer()
        {
            if (!IsServer || IsMoving)
            {
                return false;
            }

            float distanceToLow = Vector3.SqrMagnitude(
                GetPlatformLocalPosition() - m_destinationLow);
            float distanceToHigh = Vector3.SqrMagnitude(
                GetPlatformLocalPosition() - m_destinationHigh);
            return BeginMovementServer(distanceToLow <= distanceToHigh);
        }

        /// <summary>Starts travel to one explicit call-station destination.</summary>
        public bool ActivateElevatorForDestinationFromServer(
            bool destinationIsHigh)
        {
            if (!IsServer || IsMoving || IsAtDestination(destinationIsHigh))
            {
                return false;
            }

            return BeginMovementServer(destinationIsHigh);
        }

        /// <summary>Returns whether the platform is idle at the selected station.</summary>
        public bool IsAtDestination(bool destinationIsHigh)
        {
            Vector3 destination = destinationIsHigh
                ? m_destinationHigh
                : m_destinationLow;
            float tolerance = Mathf.Max(0.0001f, m_arrivalTolerance);
            return !IsMoving &&
                Vector3.SqrMagnitude(
                    GetPlatformLocalPosition() - destination) <=
                tolerance * tolerance;
        }

        /// <summary>Adds one rider once and transfers its vertical placement authority.</summary>
        public bool AddCharacter(CharacterManager character)
        {
            if (character == null || !m_charactersOnElevator.Add(character))
            {
                return false;
            }

            character.CharacterLocomotionManager?.SetRidingLift(true);
            return true;
        }

        /// <summary>Removes one rider and restores full network position smoothing.</summary>
        public bool RemoveCharacter(CharacterManager character)
        {
            if (character == null || !m_charactersOnElevator.Remove(character))
            {
                return false;
            }

            character.CharacterLocomotionManager?.SetRidingLift(false);
            return true;
        }

        /// <summary>Registers one local station for availability refreshes.</summary>
        public void RegisterCallStation(CallElevatorInteractable callStation)
        {
            if (callStation != null && !m_callStations.Contains(callStation))
            {
                m_callStations.Add(callStation);
                callStation.RefreshElevatorAvailability();
            }
        }

        /// <summary>Removes a station that despawned before this elevator.</summary>
        public void UnregisterCallStation(CallElevatorInteractable callStation)
        {
            m_callStations.Remove(callStation);
        }

        private bool BeginMovementServer(bool destinationIsHigh)
        {
            Vector3 destination = destinationIsHigh
                ? m_destinationHigh
                : m_destinationLow;
            if (Vector3.SqrMagnitude(
                    GetPlatformLocalPosition() - destination) <=
                Mathf.Max(0.0001f, m_arrivalTolerance) *
                Mathf.Max(0.0001f, m_arrivalTolerance))
            {
                return false;
            }

            NetworkPosition.Value = GetPlatformLocalPosition();
            ElevatorIsRising.Value = destinationIsHigh;
            ElevatorIsDescending.Value = !destinationIsHigh;
            BeginLocalMovement(destinationIsHigh);
            ActivateElevatorClientRpc(destinationIsHigh);
            return true;
        }

        [ClientRpc]
        private void ActivateElevatorClientRpc(bool destinationIsHigh)
        {
            if (!IsServer)
            {
                BeginLocalMovement(destinationIsHigh);
            }
        }

        [ClientRpc]
        private void FinishElevatorClientRpc(Vector3 finalLocalPosition)
        {
            if (!IsServer)
            {
                CompleteLocalMovement(finalLocalPosition);
            }
        }

        private void BeginLocalMovement(bool destinationIsHigh)
        {
            if (m_localMovementActive)
            {
                return;
            }

            m_localMovementActive = true;
            SetInteractionColliderEnabled(false);
            RefreshCallStations();
            PlayMovementAudio();
            MovementStateChanged?.Invoke(true);
            m_moveElevatorCoroutine = StartCoroutine(
                MoveElevatorCoroutine(destinationIsHigh));
        }

        private IEnumerator MoveElevatorCoroutine(bool destinationIsHigh)
        {
            Vector3 destination = destinationIsHigh
                ? m_destinationHigh
                : m_destinationLow;
            float tolerance = Mathf.Max(0.0001f, m_arrivalTolerance);
            while (Vector3.SqrMagnitude(
                    GetPlatformLocalPosition() - destination) >
                tolerance * tolerance)
            {
                Vector3 nextPosition = Vector3.MoveTowards(
                    GetPlatformLocalPosition(),
                    destination,
                    Mathf.Max(0.01f, m_movementSpeed) * Time.deltaTime);
                SetPlatformLocalPosition(nextPosition);
                MoveCharactersWithElevator();
                if (IsServer)
                {
                    NetworkPosition.Value = nextPosition;
                }

                yield return null;
            }

            SetPlatformLocalPosition(destination);
            MoveCharactersWithElevator();
            if (!IsServer)
            {
                while (IsMoving)
                {
                    MoveCharactersWithElevator();
                    yield return null;
                }

                yield break;
            }

            NetworkPosition.Value = destination;
            ElevatorIsRising.Value = false;
            ElevatorIsDescending.Value = false;
            CompleteLocalMovement(destination);
            FinishElevatorClientRpc(destination);
        }

        private void CompleteLocalMovement(Vector3 finalLocalPosition)
        {
            bool wasMoving = m_localMovementActive;
            StopMovementCoroutine();
            SetPlatformLocalPosition(finalLocalPosition);
            MoveCharactersWithElevator();
            SetInteractionColliderEnabled(true);
            RefreshCallStations();
            if (!wasMoving)
            {
                return;
            }

            StopMovementAudio();
            MovementStateChanged?.Invoke(false);
        }

        private void MoveCharactersWithElevator()
        {
            m_charactersOnElevator.RemoveWhere(character => character == null);
            float targetHeight = m_elevatorPlatform.position.y +
                m_movementOffset;
            foreach (CharacterManager character in m_charactersOnElevator)
            {
                if (character == null || character.IsJumping)
                {
                    continue;
                }

                character.CharacterLocomotionManager?.MoveWithLiftToHeight(
                    targetHeight);
            }
        }

        private void ClearCharactersOnElevator()
        {
            foreach (CharacterManager character in m_charactersOnElevator)
            {
                character?.CharacterLocomotionManager?.SetRidingLift(false);
            }

            m_charactersOnElevator.Clear();
        }

        private void RefreshCallStations()
        {
            for (int stationIndex = m_callStations.Count - 1;
                stationIndex >= 0;
                stationIndex--)
            {
                CallElevatorInteractable station = m_callStations[stationIndex];
                if (station == null)
                {
                    m_callStations.RemoveAt(stationIndex);
                    continue;
                }

                station.RefreshElevatorAvailability();
            }
        }

        private void OnNetworkPositionChanged(
            Vector3 previousPosition,
            Vector3 currentPosition)
        {
            if (!IsMoving && !m_localMovementActive)
            {
                SetPlatformLocalPosition(currentPosition);
            }
        }

        private void OnMovementDirectionChanged(bool wasMoving, bool isMoving)
        {
            if (IsServer)
            {
                return;
            }

            if (IsMoving)
            {
                BeginLocalMovement(ElevatorIsRising.Value);
                return;
            }

            CompleteLocalMovement(NetworkPosition.Value);
        }

        private void StopMovementCoroutine()
        {
            if (m_moveElevatorCoroutine != null)
            {
                StopCoroutine(m_moveElevatorCoroutine);
                m_moveElevatorCoroutine = null;
            }

            m_localMovementActive = false;
        }

        private void PlayMovementAudio()
        {
            if (m_audioSource == null || m_movementSound == null)
            {
                return;
            }

            m_audioSource.clip = m_movementSound;
            m_audioSource.loop = true;
            m_audioSource.Play();
        }

        private void StopMovementAudio()
        {
            if (m_audioSource == null)
            {
                return;
            }

            m_audioSource.Stop();
            m_audioSource.loop = false;
            if (m_stopSound != null)
            {
                m_audioSource.PlayOneShot(m_stopSound);
            }
        }

        private Vector3 GetPlatformLocalPosition()
        {
            return m_elevatorPlatform != null
                ? m_elevatorPlatform.localPosition
                : transform.localPosition;
        }

        private void SetPlatformLocalPosition(Vector3 localPosition)
        {
            Transform platform = m_elevatorPlatform != null
                ? m_elevatorPlatform
                : transform;
            platform.localPosition = localPosition;
        }

        private bool IsPlayerWithinInteractionDistance(PlayerManager player)
        {
            if (player == null || InteractableCollider == null)
            {
                return false;
            }

            Vector3 closestPoint = InteractableCollider.ClosestPoint(
                player.transform.position);
            return Vector3.Distance(closestPoint, player.transform.position) <=
                m_maxInteractionDistance;
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
