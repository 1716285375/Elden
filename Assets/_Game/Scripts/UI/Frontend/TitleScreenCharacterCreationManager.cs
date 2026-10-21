using System;
using System.Collections;
using System.Globalization;
using System.Text;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Coordinates an isolated creation draft, preview, and one atomic save commit.</summary>
    public class TitleScreenCharacterCreationManager : MonoBehaviour
    {
        public const int MaximumCharacterNameLength = 15;
        [SerializeField] private TitleScreenManager m_titleScreenManager;
        [SerializeField] private GameObject m_characterCreationRoot;
        private CharacterCreationView m_view;
        private CharacterCreationPreview m_preview;
        private CharacterOriginView m_originView;
        private PlayerManager m_player;
        private Coroutine m_resolvePlayer;
        private CharacterAppearanceData m_draft = new();
        private int m_selectedClass;
        private bool m_isMale = true;
        private bool m_showHelmet;
        private bool m_isSubmitting;
        private bool m_hasSelectedOrigin;

        public bool IsOpen => m_view != null && m_view.gameObject.activeSelf;
        public bool IsReady => IsOpen && m_player != null && m_preview != null && !m_isSubmitting;
        public CharacterAppearanceData Draft => m_draft.Clone();
        public CharacterCreationPreview Preview => m_preview;

        public void ConfigureRuntime(TitleScreenManager titleScreenManager, Button styleSource)
        {
            m_titleScreenManager = titleScreenManager;
            m_characterCreationRoot?.SetActive(false);
            m_view ??= CharacterCreationView.Create(transform, this);
            m_originView ??= CharacterOriginView.Create(transform, this);
            m_view.gameObject.SetActive(false);
        }

        public void OpenCharacterCreation()
        {
            if (IsOpen)
            {
                return;
            }
            m_draft = new CharacterAppearanceData();
            m_selectedClass = 0;
            m_isMale = true;
            m_showHelmet = false;
            m_isSubmitting = false;
            m_hasSelectedOrigin = false;
            m_view.gameObject.SetActive(true);
            m_view.SetBusy(true, "正在准备角色…");
            m_resolvePlayer = StartCoroutine(ResolvePlayer());
        }

        public void CloseCharacterCreation()
        {
            if (m_resolvePlayer != null)
            {
                StopCoroutine(m_resolvePlayer);
                m_resolvePlayer = null;
            }
            if (m_preview != null)
            {
                Destroy(m_preview.gameObject);
                m_preview = null;
            }
            m_player = null;
            m_view?.gameObject.SetActive(false);
            m_originView?.Hide();
            PlayerInputManager.Instance?.DisableMenuCameraInput();
        }

        public void CancelCharacterCreation() => m_titleScreenManager.ReturnFromCharacterCreation();

        public void SelectClass(int index)
        {
            CharacterClass[] classes = m_titleScreenManager.StartingClasses;
            if (index < 0 || index >= classes.Length)
            {
                return;
            }
            m_selectedClass = index;
            RefreshPreview();
            m_view.SetClassDetails(classes[index]);
        }

        public void PreviewClass(int index) => SelectClass(index);
        public void ToggleSex() { m_isMale = !m_isMale; RefreshPreview(); }
        public void SelectBodyType(bool isMale) { m_isMale = isMale; RefreshPreview(); }

        public void OpenOriginSelection()
        {
            m_hasSelectedOrigin = false;
            m_view.SetVisible(false);
            m_originView.Show(m_titleScreenManager.StartingClasses, m_selectedClass);
        }

        public void ConfirmOrigin(int index, bool isMale)
        {
            SelectClass(index);
            SelectBodyType(isMale);
            m_hasSelectedOrigin = true;
            m_originView.Hide();
            m_view.SetBodyType(isMale);
            m_view.SetVisible(true);
            m_view.ShowSection("基本资料");
        }
        public void SetHelmetPreview(bool visible) { m_showHelmet = visible; RefreshPreview(); }
        public void SelectHairstyle(int index) { m_draft.Hairstyle = index; RefreshPreview(); }
        public void PreviewHairstyle(int index) => SelectHairstyle(index);
        public void SelectHairColor(Color32 color) { m_draft.HairColor = color; RefreshPreview(); }
        public void PreviewHairColor(Color32 color) => SelectHairColor(color);
        public void SetSkinColor(Color32 color) { m_draft.SkinColor = color; RefreshPreview(); }
        public void SetEyeColor(Color32 color) { m_draft.EyeColor = color; RefreshPreview(); }
        public void SetIrisSize(float size) { m_draft.IrisSize = size; RefreshPreview(); }
        public void SetEyebrow(int index) { m_draft.Eyebrow = index; RefreshPreview(); }
        public void SetBeard(int index) { m_draft.Beard = index; RefreshPreview(); }
        public void SetMorph(int index, float value) { m_draft.SetShape(index, value); RefreshPreview(); }

        public void ResetFace()
        {
            m_draft = new CharacterAppearanceData { Hairstyle = m_draft.Hairstyle,
                HairColor = m_draft.HairColor, SkinColor = m_draft.SkinColor, EyeColor = m_draft.EyeColor,
                Eyebrow = m_draft.Eyebrow, Beard = m_draft.Beard, IrisSize = m_draft.IrisSize };
            RefreshPreview();
            m_view.RefreshMorphValues(m_draft);
        }

        public void RandomizeFace()
        {
            for (int index = 0; index < m_preview.Definition.Count; index++)
            {
                m_draft.SetShape(index, UnityEngine.Random.Range(-0.35f, 0.35f));
            }
            RefreshPreview();
            m_view.RefreshMorphValues(m_draft);
        }

        public void StartGame()
        {
            if (!IsReady || !m_hasSelectedOrigin || WorldSaveGameManager.Instance == null)
            {
                return;
            }
            m_isSubmitting = true;
            m_view.SetBusy(true, "正在保存角色并进入世界…");
            m_player.ApplyCharacterClass(m_titleScreenManager.StartingClasses[m_selectedClass]);
            m_player.PlayerNetworkManager.IsMale.Value = m_isMale;
            m_player.PlayerNetworkManager.Appearance.Value = CharacterAppearanceState.FromData(m_draft);
            m_player.PlayerNetworkManager.CharacterName.Value = new FixedString64Bytes(
                SanitizeCharacterName(m_view.CharacterName));
            CharacterSaveData data = m_player.CreateCharacterSaveData();
            data.XPosition = 0f;
            data.YPosition = 0f;
            data.ZPosition = 0f;
            if (!WorldSaveGameManager.Instance.NewGame(data))
            {
                m_isSubmitting = false;
                m_view.SetBusy(false, "角色未保存，请检查存档空间后重试。");
                return;
            }
            CloseCharacterCreation();
        }

        public static string SanitizeCharacterName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }
            var result = new StringBuilder();
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(value.Trim());
            int count = 0;
            while (count < MaximumCharacterNameLength && elements.MoveNext())
            {
                string element = elements.GetTextElement();
                if (char.IsControl(element[0]))
                {
                    continue;
                }
                if (Encoding.UTF8.GetByteCount(result.ToString()) + Encoding.UTF8.GetByteCount(element) > 60)
                {
                    break;
                }
                result.Append(element);
                count++;
            }
            return result.Length > 0 ? result.ToString() : "Unnamed";
        }

        public void SubmitCharacterName(string name) => m_view.SetCharacterName(SanitizeCharacterName(name));
        public void ConfirmAuthoredName() => SubmitCharacterName(m_view.CharacterName);
        public void OpenClassMenu() => m_view.ShowSection("基本资料");
        public void OpenHairMenu() => m_view.ShowSection("发型与肤色");
        public void OpenHairColorMenu() => OpenHairMenu();
        public void OpenNameMenu() => OpenClassMenu();
        public void CloseSubMenu() => OpenClassMenu();
        public void CommitSliderHairColor() => RefreshPreview();

        private IEnumerator ResolvePlayer()
        {
            while (IsOpen && m_player == null)
            {
                var players = WorldGameSessionManager.Instance?.Players;
                if (players != null)
                {
                    foreach (PlayerManager player in players)
                    {
                        if (player != null && player.IsOwner && player.IsSpawned)
                        {
                            m_player = player;
                            break;
                        }
                    }
                }
                yield return null;
            }
            if (!IsOpen || m_player == null)
            {
                yield break;
            }
            var previewObject = new GameObject("Character Creation Preview");
            previewObject.transform.SetParent(transform, false);
            m_preview = previewObject.AddComponent<CharacterCreationPreview>();
            m_preview.Initialize();
            m_view.Bind(m_preview, m_titleScreenManager.StartingClasses, m_draft);
            SelectClass(0);
            m_view.SetBusy(false, "设定角色资料与外貌。选择「完成」后开始游戏。");
            OpenOriginSelection();
            m_resolvePlayer = null;
        }

        private void RefreshPreview()
        {
            if (m_preview != null)
            {
                m_preview.Apply(m_draft, m_isMale, m_titleScreenManager.StartingClasses[m_selectedClass], m_showHelmet);
            }
        }
    }
}
