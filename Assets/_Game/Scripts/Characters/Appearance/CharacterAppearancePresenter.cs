using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Updates cached original meshes only when an appearance snapshot changes.</summary>
    public sealed class CharacterAppearancePresenter : MonoBehaviour
    {
        [SerializeField] private FaceMorphDefinition m_definition;
        [SerializeField] private SkinnedMeshRenderer[] m_faces = Array.Empty<SkinnedMeshRenderer>();
        [SerializeField] private SkinnedMeshRenderer[] m_skin = Array.Empty<SkinnedMeshRenderer>();
        [SerializeField] private GameObject[] m_hairStyles = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] m_features = Array.Empty<GameObject>();
        [SerializeField] private SkinnedMeshRenderer[] m_eyes = Array.Empty<SkinnedMeshRenderer>();
        private MaterialPropertyBlock m_block;
        private CharacterAppearanceData m_current = new();
        private bool m_isHairHidden;
        private bool m_isFaceHidden;

        public void BindFeatures(GameObject[] features, SkinnedMeshRenderer[] eyes)
        {
            m_features = features;
            m_eyes = eyes;
        }

        public FaceMorphDefinition Definition => m_definition;
        public CharacterAppearanceData Capture() => m_current.Clone();

        public void Bind(FaceMorphDefinition definition, SkinnedMeshRenderer[] faces,
            SkinnedMeshRenderer[] skin, GameObject[] hairStyles)
        {
            m_definition = definition;
            m_faces = faces;
            m_skin = skin;
            m_hairStyles = hairStyles;
        }

        public void Apply(CharacterAppearanceData appearance)
        {
            m_current = appearance?.Clone() ?? new CharacterAppearanceData();
            foreach (SkinnedMeshRenderer renderer in m_faces)
            {
                if (renderer == null)
                {
                    continue;
                }
                for (int index = 0; index < renderer.sharedMesh.blendShapeCount; index++)
                {
                    renderer.SetBlendShapeWeight(index, m_current.GetShape(index) * 100f);
                }
            }
            foreach (SkinnedMeshRenderer renderer in m_skin)
            {
                ApplyColor(renderer, m_current.SkinColor);
            }
            for (int index = 0; index < m_hairStyles.Length; index++)
            {
                GameObject style = m_hairStyles[index];
                if (style == null)
                {
                    continue;
                }
                style.SetActive(index + 1 == m_current.Hairstyle && !m_isHairHidden);
                if (index + 1 == m_current.Hairstyle)
                {
                    foreach (Renderer renderer in style.GetComponentsInChildren<Renderer>(true))
                    {
                        ApplyColor(renderer, m_current.HairColor);
                    }
                }
            }
            RefreshFeatures();
            foreach (SkinnedMeshRenderer eye in m_eyes)
            {
                m_block ??= new MaterialPropertyBlock();
                eye.GetPropertyBlock(m_block);
                m_block.SetColor("_EyeColor", m_current.EyeColor);
                m_block.SetFloat("_IrisSize", m_current.IrisSize);
                eye.SetPropertyBlock(m_block);
            }
        }

        public void SetHeadEquipmentVisible(bool hidesHair)
        {
            m_isHairHidden = hidesHair;
            for (int index = 0; index < m_hairStyles.Length; index++)
            {
                m_hairStyles[index]?.SetActive(index + 1 == m_current.Hairstyle && !hidesHair);
            }
        }

        public void SetFaceEquipmentVisible(bool hidesFace)
        {
            m_isFaceHidden = hidesFace;
            foreach (SkinnedMeshRenderer face in m_faces)
            {
                if (face != null)
                {
                    face.enabled = !hidesFace;
                }
            }
            RefreshFeatures();
        }

        private void RefreshFeatures()
        {
            foreach (GameObject feature in m_features)
            {
                int id = int.Parse(feature.name.Substring(feature.name.Length - 4));
                bool selected = id == 1500 || id == 7001 || id == 2000 + m_current.Eyebrow ||
                    id == 3000 + m_current.Beard;
                feature.SetActive(selected && !m_isFaceHidden);
                if (selected && id != 1500)
                {
                    foreach (Renderer renderer in feature.GetComponentsInChildren<Renderer>(true))
                    {
                        ApplyColor(renderer, m_current.HairColor);
                    }
                }
            }
        }

        private void ApplyColor(Renderer renderer, Color32 color)
        {
            if (renderer == null)
            {
                return;
            }
            m_block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(m_block);
            m_block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(m_block);
        }
    }
}
