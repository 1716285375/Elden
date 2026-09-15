using UnityEngine;

namespace ZZ
{
    /// <summary>Owns ConsumableController runtime state independently of the combat facade.</summary>
    public sealed class ConsumableController
    {
        private readonly PlayerManager m_player;
        private QuickSlotItem m_currentQuickSlotItem;
        private bool m_isUsingItem;

        public ConsumableController(PlayerManager player)
        {
            m_player = player;
        }

        public bool IsUsingItem => m_isUsingItem;

        public void AttemptToUseFlask(FlaskItem flask)
        {
            if (flask == null || m_player == null || !m_player.IsOwner)
            {
                return;
            }

            if (m_isUsingItem)
            {
                RequestAdditionalFlaskUse(flask);
                return;
            }

            BeginQuickSlotItemUse(flask, true);
        }

        public void PerformQuickSlotItemActionFromRpc(QuickSlotItem quickSlotItem)
        {
            if (quickSlotItem != null && m_player?.IsOwner != true)
            {
                BeginQuickSlotItemUse(quickSlotItem, false);
            }
        }

        public void SuccessfullyUseQuickSlotItem()
        {
            if (!m_isUsingItem || m_currentQuickSlotItem == null)
            {
                return;
            }

            m_currentQuickSlotItem.SuccessfullyUseItem(m_player);
        }

        public void HandleFlaskDrinkStateEntered()
        {
            if (!m_isUsingItem || m_currentQuickSlotItem is not FlaskItem flask)
            {
                return;
            }

            if (m_player?.IsOwner == true)
            {
                m_player.PlayerNetworkManager?.SetChuggingState(false);
            }

            if (GetRemainingFlaskCount(flask) <= 0)
            {
                PresentQuickSlotItemModel(flask.EmptyFlaskItemModel);
                m_player?.CharacterSoundFXManager?.PlayFlaskSound(true);
                m_player?.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(true);
            }
        }

        public void HandleRemainingFlasksChanged(
            bool healthFlask,
            int previousFlaskCount,
            int currentFlaskCount)
        {
            if (!m_isUsingItem ||
                m_currentQuickSlotItem is not FlaskItem flask ||
                flask.RestoresHealth != healthFlask)
            {
                return;
            }

            if (currentFlaskCount < previousFlaskCount &&
                m_player?.IsOwner != true)
            {
                flask.PlaySuccessfulUseFeedback(m_player);
            }

            if (currentFlaskCount <= 0)
            {
                PresentQuickSlotItemModel(flask.EmptyFlaskItemModel);
                m_player?.PlayerNetworkManager?.SetChuggingState(false);
                m_player?.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(true);
            }
        }

        public void ResetQuickSlotItemUse()
        {
            if (!m_isUsingItem)
            {
                return;
            }

            m_isUsingItem = false;
            m_currentQuickSlotItem = null;
            m_player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            m_player?.LocomotionManager?.SetCanRun(true);
            m_player?.LocomotionManager?.SetCanRoll(true);
            m_player?.EquipmentManager?.SetWeaponsHidden(false);
            m_player?.PlayerAnimatorManager?.SetFlaskChuggingState(false);
            if (m_player?.IsOwner == true)
            {
                m_player.PlayerNetworkManager?.SetChuggingState(false);
                if (m_player.PlayerNetworkManager?.IsSpawned == true)
                {
                    m_player.PlayerNetworkManager.SetWeaponsHiddenServerRpc(false);
                }
            }
        }

        public void CancelQuickSlotItemUse()
        {
            if (!m_isUsingItem)
            {
                return;
            }

            m_player?.PlayerAnimatorManager?.PlayEmptyUpperBodyAnimation();
            ResetQuickSlotItemUse();
        }

        private void BeginQuickSlotItemUse(
            QuickSlotItem quickSlotItem,
            bool notifyNetwork)
        {
            if (quickSlotItem == null || m_player == null || m_isUsingItem)
            {
                return;
            }

            m_isUsingItem = true;
            m_currentQuickSlotItem = quickSlotItem;
            m_player.LocomotionManager?.StopSprinting();
            m_player.LocomotionManager?.SetCanRun(false);
            m_player.LocomotionManager?.SetCanRoll(false);
            m_player.EquipmentManager?.SetWeaponsHidden(true);

            bool isEmpty = quickSlotItem is FlaskItem flask &&
                GetRemainingFlaskCount(flask) <= 0;
            GameObject model = isEmpty && quickSlotItem is FlaskItem emptyFlask
                ? emptyFlask.EmptyFlaskItemModel
                : quickSlotItem.ItemModel;
            PresentQuickSlotItemModel(model);
            m_player.PlayerAnimatorManager?.SetFlaskChuggingState(false);
            m_player.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(isEmpty);
            if (isEmpty)
            {
                m_player.CharacterSoundFXManager?.PlayFlaskSound(true);
            }

            if (!notifyNetwork || !m_player.IsOwner ||
                m_player.PlayerNetworkManager?.IsSpawned != true)
            {
                return;
            }

            m_player.PlayerNetworkManager.SetChuggingState(false);
            m_player.PlayerNetworkManager.SetWeaponsHiddenServerRpc(true);
            m_player.PlayerNetworkManager
                .NotifyServerOfQuickSlotItemActionServerRpc(quickSlotItem.ItemID);
        }

        private void RequestAdditionalFlaskUse(FlaskItem requestedFlask)
        {
            if (m_currentQuickSlotItem != requestedFlask ||
                m_player?.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsChugging.Value)
            {
                return;
            }

            if (GetRemainingFlaskCount(requestedFlask) <= 0)
            {
                PresentQuickSlotItemModel(requestedFlask.EmptyFlaskItemModel);
                m_player.CharacterSoundFXManager?.PlayFlaskSound(true);
                m_player.PlayerAnimatorManager?.PlayQuickSlotItemAnimation(true);
                return;
            }

            m_player.PlayerNetworkManager.SetChuggingState(true);
        }

        private int GetRemainingFlaskCount(FlaskItem flask)
        {
            return flask != null && m_player?.PlayerNetworkManager != null
                ? m_player.PlayerNetworkManager.GetRemainingFlaskCount(
                    flask.RestoresHealth)
                : 0;
        }

        private void PresentQuickSlotItemModel(GameObject model)
        {
            m_player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            Transform itemParent = m_player?.EquipmentManager?.QuickSlotItemParent;
            if (model == null || itemParent == null)
            {
                return;
            }

            GameObject itemModel = Object.Instantiate(model, itemParent);
            itemModel.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            m_player.CharacterEffectsManager?.RegisterCurrentActionEffect(itemModel);
        }
    }
}
