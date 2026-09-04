using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>Builds save-slot portraits through one isolated reusable dummy.</summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class CharacterProfileIconMaker : MonoBehaviour
    {
        private const string k_MainMenuSceneName = "SCN_MainMenu";
        private const string k_ProfileIconResourcePath =
            "UI/Profile Icon Maker";
        private const string k_ProfileIconDirectoryName = "Icons";

        private static CharacterProfileIconMaker s_instance;

        [SerializeField] private ProfileIconMakerManager m_dummyManager;
        [SerializeField] private Camera m_profileIconCamera;
        [SerializeField] private RenderTexture m_iconRenderTextureTemplate;

        public static CharacterProfileIconMaker Instance => s_instance;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            m_dummyManager ??= GetComponentInChildren<ProfileIconMakerManager>(
                true);
            m_profileIconCamera ??= GetComponentInChildren<Camera>(true);
        }

        private IEnumerator Start()
        {
            const int k_MaxInitializationFrames = 120;
            for (int frame = 0;
                frame < k_MaxInitializationFrames &&
                    (WorldSaveGameManager.Instance == null ||
                        WorldItemDatabase.Instance == null);
                frame++)
            {
                yield return null;
            }

            CreateAllProfileIcons();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>Generates portraits for every populated save slot in the menu.</summary>
        public int CreateAllProfileIcons()
        {
            UICharacterSaveSlot[] saveSlots = FindObjectsByType<UICharacterSaveSlot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int createdPortraits = 0;
            foreach (UICharacterSaveSlot saveSlot in saveSlots)
            {
                if (saveSlot == null ||
                    saveSlot.CharacterSlot == CharacterSlot.NoSlot)
                {
                    continue;
                }

                CharacterProfileIconSlotPresenter presenter =
                    saveSlot.GetComponent<CharacterProfileIconSlotPresenter>() ??
                    saveSlot.gameObject.AddComponent<
                        CharacterProfileIconSlotPresenter>();
                CharacterSaveData characterData = WorldSaveGameManager.Instance
                    ?.GetCharacterDataForSlot(saveSlot.CharacterSlot);
                if (characterData == null)
                {
                    presenter.SetProfileIcon(null);
                    continue;
                }

                Sprite profileIcon = CreateCharacterProfileIcon(
                    characterData,
                    saveSlot.CharacterSlot);
                presenter.SetProfileIcon(profileIcon);
                if (profileIcon != null)
                {
                    createdPortraits++;
                }
            }

            return createdPortraits;
        }

        /// <summary>Rebuilds, renders, persists, and returns one save-slot portrait.</summary>
        public Sprite CreateCharacterProfileIcon(
            CharacterSaveData characterData,
            CharacterSlot characterSlot)
        {
            if (characterData == null ||
                characterSlot == CharacterSlot.NoSlot ||
                m_dummyManager == null ||
                m_profileIconCamera == null ||
                m_iconRenderTextureTemplate == null ||
                !m_dummyManager.EquipDummy(characterData))
            {
                return null;
            }

            Animator dummyAnimator =
                m_dummyManager.GetComponentInChildren<Animator>(true);
            dummyAnimator?.Rebind();
            dummyAnimator?.Update(0f);
            Texture2D portraitTexture = RenderPortraitTexture();
            if (portraitTexture == null)
            {
                return null;
            }

            portraitTexture.name = $"{characterSlot} Profile Icon";
            TryWriteProfileIcon(characterSlot, portraitTexture.EncodeToPNG());
            Sprite profileIcon = Sprite.Create(
                portraitTexture,
                new Rect(0f, 0f, portraitTexture.width, portraitTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            profileIcon.name = portraitTexture.name;
            return profileIcon;
        }

        /// <summary>Gets the stable PNG path used by one character slot.</summary>
        public static string GetProfileIconPath(CharacterSlot characterSlot)
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                k_ProfileIconDirectoryName,
                $"{characterSlot}.png");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapMainMenuProfileIconMaker()
        {
            if (s_instance != null ||
                SceneManager.GetActiveScene().name != k_MainMenuSceneName)
            {
                return;
            }

            GameObject makerPrefab = Resources.Load<GameObject>(
                k_ProfileIconResourcePath);
            if (makerPrefab == null)
            {
                Debug.LogWarning(
                    $"Missing Resources/{k_ProfileIconResourcePath}.prefab.");
                return;
            }

            Instantiate(makerPrefab);
        }

        private Texture2D RenderPortraitTexture()
        {
            RenderTextureDescriptor descriptor =
                m_iconRenderTextureTemplate.descriptor;
            descriptor.depthBufferBits = Mathf.Max(16, descriptor.depthBufferBits);
            RenderTexture portraitTarget = RenderTexture.GetTemporary(descriptor);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = m_profileIconCamera.targetTexture;
            try
            {
                m_profileIconCamera.targetTexture = portraitTarget;
                RenderTexture.active = portraitTarget;
                m_profileIconCamera.Render();
                Texture2D portraitTexture = new(
                    descriptor.width,
                    descriptor.height,
                    TextureFormat.RGBA32,
                    false,
                    false);
                portraitTexture.ReadPixels(
                    new Rect(0f, 0f, descriptor.width, descriptor.height),
                    0,
                    0,
                    false);
                portraitTexture.Apply(false, false);
                return portraitTexture;
            }
            finally
            {
                m_profileIconCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(portraitTarget);
            }
        }

        private static void TryWriteProfileIcon(
            CharacterSlot characterSlot,
            byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return;
            }

            string filePath = GetProfileIconPath(characterSlot);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllBytes(filePath, pngBytes);
            }
            catch (IOException exception)
            {
                Debug.LogWarning(
                    $"Could not persist profile icon '{filePath}': " +
                    exception.Message);
            }
            catch (System.UnauthorizedAccessException exception)
            {
                Debug.LogWarning(
                    $"Could not persist profile icon '{filePath}': " +
                    exception.Message);
            }
        }
    }
}
