using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns the runtime Image and generated Sprite for one save slot.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterProfileIconSlotPresenter : MonoBehaviour
    {
        private const string k_ProfileIconObjectName = "Profile Icon";

        [SerializeField] private Image m_profileIconImage;
        private Sprite m_ownedSprite;

        /// <summary>Creates the slot Image lazily so existing menu scenes need no migration.</summary>
        public void EnsureImage()
        {
            if (m_profileIconImage != null)
            {
                return;
            }

            Transform existing = transform.Find(k_ProfileIconObjectName);
            GameObject imageObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    k_ProfileIconObjectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
            if (existing == null)
            {
                imageObject.transform.SetParent(transform, false);
                imageObject.transform.SetAsFirstSibling();
            }

            m_profileIconImage = imageObject.GetComponent<Image>();
            RectTransform imageTransform =
                m_profileIconImage.rectTransform;
            imageTransform.anchorMin = new Vector2(0f, 0f);
            imageTransform.anchorMax = new Vector2(0f, 1f);
            imageTransform.pivot = new Vector2(0f, 0.5f);
            imageTransform.offsetMin = new Vector2(12f, 8f);
            imageTransform.offsetMax = new Vector2(108f, -8f);
            m_profileIconImage.preserveAspect = true;
            m_profileIconImage.raycastTarget = false;
            m_profileIconImage.color = Color.white;
        }

        /// <summary>Replaces and disposes the previous generated portrait.</summary>
        public void SetProfileIcon(Sprite profileIcon)
        {
            EnsureImage();
            DisposeOwnedSprite();
            m_ownedSprite = profileIcon;
            m_profileIconImage.sprite = profileIcon;
            m_profileIconImage.enabled = profileIcon != null;
        }

        private void OnDestroy()
        {
            DisposeOwnedSprite();
        }

        private void DisposeOwnedSprite()
        {
            if (m_ownedSprite == null)
            {
                return;
            }

            Texture2D ownedTexture = m_ownedSprite.texture;
            Destroy(m_ownedSprite);
            if (ownedTexture != null)
            {
                Destroy(ownedTexture);
            }

            m_ownedSprite = null;
        }
    }
}
