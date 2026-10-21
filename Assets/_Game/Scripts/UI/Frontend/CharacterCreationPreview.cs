using System;
using System.Linq;
using UnityEngine;

namespace ZZ
{
    /// <summary>Owns an isolated visual-only preview; never mutates the network player or its inventory.</summary>
    public sealed class CharacterCreationPreview : MonoBehaviour
    {
        private GameObject m_visual;
        private Camera m_camera;
        private CharacterAppearancePresenter m_appearance;
        private PlayerSkinnedEquipment m_equipment;
        private Animator m_animator;
        private RenderTexture m_texture;
        private bool m_closeUp = true;
        private CharacterClass m_class;
        private bool m_isMale = true;
        private bool m_showHelmet;
        private bool m_hasEquipmentPreview;
        private GameObject m_rightWeapon;
        private GameObject m_leftWeapon;
        private static int s_instanceCount;
        private Vector3 m_origin;

        public RenderTexture Texture => m_texture;
        public Camera Camera => m_camera;
        public FaceMorphDefinition Definition => m_appearance.Definition;

        public void Initialize()
        {
            if (m_visual != null)
            {
                return;
            }
            GameObject prefab = Resources.Load<GameObject>("UI/CharacterPreview");
            if (prefab == null)
            {
                throw new InvalidOperationException("The extracted character preview prefab is missing.");
            }
            m_origin = new Vector3(++s_instanceCount * 20f, -1000f, 0f);
            m_visual = Instantiate(prefab, m_origin, Quaternion.identity);
            m_visual.name = "Character Creation Visual";
            m_appearance = m_visual.GetComponent<CharacterAppearancePresenter>();
            m_equipment = m_visual.GetComponent<PlayerSkinnedEquipment>();
            m_animator = m_visual.GetComponent<Animator>();
            foreach (Transform item in m_visual.GetComponentsInChildren<Transform>(true))
            {
                item.gameObject.layer = 31;
            }
            foreach (Collider collider in m_visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            m_animator.fireEvents = false;
            m_animator.applyRootMotion = false;
            m_animator.Rebind();
            m_animator.Play("Locomotion One Handed", 0, 0f);
            m_animator.Update(0.2f);
            m_texture = new RenderTexture(768, 1024, 24, RenderTextureFormat.ARGB32)
                { name = "Character Creation Preview" };
            m_texture.Create();
            var cameraObject = new GameObject("Character Creation Camera");
            cameraObject.transform.SetParent(transform, false);
            m_camera = cameraObject.AddComponent<Camera>();
            m_camera.targetTexture = m_texture;
            m_camera.cullingMask = 1 << 31;
            m_camera.clearFlags = CameraClearFlags.SolidColor;
            m_camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            m_camera.fieldOfView = 32f;
            Light key = new GameObject("Character Preview Key").AddComponent<Light>();
            key.transform.SetParent(cameraObject.transform, false);
            key.transform.localEulerAngles = new Vector3(15f, -20f, 0f);
            key.type = LightType.Directional;
            key.intensity = 1.6f;
            key.cullingMask = 1 << 31;
            SetCloseUp(true);
        }

        public void Apply(CharacterAppearanceData data, bool isMale, CharacterClass characterClass, bool showHelmet)
        {
            m_isMale = isMale;
            if (m_class != characterClass)
            {
                Destroy(m_rightWeapon);
                Destroy(m_leftWeapon);
                WeaponItem right = characterClass?.RightHandWeapons?.FirstOrDefault();
                WeaponItem left = characterClass?.LeftHandWeapons?.FirstOrDefault();
                m_rightWeapon = CreateWeapon(right, WeaponModelSlot.RightHandSlot);
                m_leftWeapon = CreateWeapon(left, left?.WeaponClass == WeaponClass.Shield ?
                    WeaponModelSlot.LeftHandShieldSlot : WeaponModelSlot.LeftHandSlot);
                if (right?.WeaponAnimator != null)
                {
                    float previousSpeed = m_animator.speed;
                    m_animator.speed = 1f;
                    m_animator.runtimeAnimatorController = right.WeaponAnimator;
                    m_animator.Rebind();
                    m_animator.Play("Locomotion One Handed", 0, 0f);
                    m_animator.Update(0.2f);
                    m_animator.speed = previousSpeed;
                }
            }
            m_class = characterClass;
            m_showHelmet = showHelmet;
            m_appearance.Apply(data);
            m_equipment.Refresh(isMale, m_hasEquipmentPreview && showHelmet ? characterClass?.HeadEquipment?.ItemID ?? -1 : -1,
                m_hasEquipmentPreview ? characterClass?.BodyEquipment?.ItemID ?? -1 : -1,
                m_hasEquipmentPreview ? characterClass?.HandEquipment?.ItemID ?? -1 : -1,
                m_hasEquipmentPreview ? characterClass?.LegEquipment?.ItemID ?? -1 : -1);
            m_rightWeapon?.SetActive(m_hasEquipmentPreview);
            m_leftWeapon?.SetActive(m_hasEquipmentPreview);
            m_appearance.SetHeadEquipmentVisible(m_hasEquipmentPreview && showHelmet && characterClass?.HeadEquipment != null);
            m_appearance.SetFaceEquipmentVisible(m_hasEquipmentPreview && showHelmet &&
                characterClass?.HeadEquipment?.HeadEquipmentType == HeadEquipmentType.FullHelmet);
        }

        public void SetEquipmentPreview(bool visible)
        {
            if (m_hasEquipmentPreview == visible)
            {
                return;
            }
            m_hasEquipmentPreview = visible;
            Apply(m_appearance.Capture(), m_isMale, m_class, m_showHelmet);
        }

        public void SetCloseUp(bool closeUp)
        {
            m_closeUp = closeUp;
            m_visual.transform.rotation = Quaternion.identity;
            Vector3 target = m_origin + Vector3.up * (closeUp ? 1.60f : 1.05f);
            m_camera.transform.position = target + Vector3.forward * (closeUp ? 0.65f : 3.9f);
            m_camera.transform.LookAt(target);
            m_animator.speed = closeUp ? 0f : 1f;
        }

        public void Rotate(float degrees) => m_visual.transform.Rotate(Vector3.up, degrees, Space.World);

        public void RenderNow() => m_camera.Render();

        public void ApplySavedCharacter(CharacterSaveData data)
        {
            m_appearance.Apply(data.Appearance);
            m_equipment.Refresh(data.IsMale, -1, data.BodyEquipmentID, data.HandEquipmentID, data.LegEquipmentID);
            m_appearance.SetHeadEquipmentVisible(false);
            SetCloseUp(true);
        }

        private GameObject CreateWeapon(WeaponItem weapon, WeaponModelSlot slotType)
        {
            if (weapon == null || weapon.IsUnarmed || weapon.WeaponModel == null)
            {
                return null;
            }
            WeaponModelInstantiationSlot slot = m_visual.GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                .SingleOrDefault(item => item.WeaponModelSlot == slotType);
            if (slot == null)
            {
                return null;
            }
            GameObject model = Instantiate(weapon.WeaponModel, slot.transform);
            model.transform.localPosition = weapon.WeaponPivotPosition;
            model.transform.localRotation = Quaternion.Euler(weapon.WeaponPivotRotation);
            model.transform.localScale = weapon.WeaponPivotScale;
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 31;
            }
            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            return model;
        }

        private void OnDestroy()
        {
            if (m_visual != null)
            {
                Destroy(m_visual);
            }
            if (m_texture != null)
            {
                m_texture.Release();
                Destroy(m_texture);
            }
        }
    }
}
