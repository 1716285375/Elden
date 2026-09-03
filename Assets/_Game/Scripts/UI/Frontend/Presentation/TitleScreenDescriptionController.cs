using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Displays the authored description of the currently selected frontend entry
    /// (main menu, save slots, settings, credits) in a shared description label.
    /// Entries provide their own text through the serialized maps; the controller
    /// only reads EventSystem selection and never touches button visuals.
    /// </summary>
    public class TitleScreenDescriptionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text m_descriptionText;

        [Header("Entries")]
        [SerializeField] private Selectable[] m_selectables;
        [SerializeField] private string[] m_descriptions;

        private readonly Dictionary<GameObject, string> m_descriptionsByObject = new();
        private GameObject m_lastSelectedObject;

        private void Awake()
        {
            if (m_selectables == null || m_descriptions == null)
            {
                return;
            }

            int entryCount = Mathf.Min(m_selectables.Length, m_descriptions.Length);
            for (int index = 0; index < entryCount; index++)
            {
                Selectable selectable = m_selectables[index];
                string description = m_descriptions[index];
                if (selectable != null && !string.IsNullOrEmpty(description))
                {
                    m_descriptionsByObject[selectable.gameObject] = description;
                }
            }
        }

        private void OnEnable()
        {
            m_lastSelectedObject = EventSystem.current?.currentSelectedGameObject;
            RefreshDescription(m_lastSelectedObject);
        }

        private void Update()
        {
            GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
            if (selectedObject == m_lastSelectedObject)
            {
                return;
            }

            m_lastSelectedObject = selectedObject;
            RefreshDescription(selectedObject);
        }

        private void RefreshDescription(GameObject selectedObject)
        {
            if (m_descriptionText == null)
            {
                return;
            }

            m_descriptionText.text = selectedObject != null &&
                m_descriptionsByObject.TryGetValue(
                    selectedObject,
                    out string description)
                    ? description
                    : string.Empty;
        }
    }
}
