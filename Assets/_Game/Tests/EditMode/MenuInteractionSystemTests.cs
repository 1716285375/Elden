using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public class MenuInteractionSystemTests
    {
        private readonly List<GameObject> m_createdObjects = new();
        private Component m_uiManager;
        private FieldInfo m_uiInstanceField;
        private FieldInfo m_inputInstanceField;
        private object m_previousUIManager;
        private object m_previousInputManager;
        private EventSystem m_previousEventSystem;
        private EventSystem m_testEventSystem;
        private bool m_previousCursorVisible;
        private CursorLockMode m_previousCursorLockState;

        [SetUp]
        public void SetUp()
        {
            m_previousEventSystem = EventSystem.current;
            m_previousCursorVisible = Cursor.visible;
            m_previousCursorLockState = Cursor.lockState;
            m_uiInstanceField = GetRuntimeType("ZZ.PlayerUIManager")
                .GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            m_inputInstanceField = GetRuntimeType("ZZ.PlayerInputManager")
                .GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            m_previousUIManager = m_uiInstanceField.GetValue(null);
            m_previousInputManager = m_inputInstanceField.GetValue(null);
            m_inputInstanceField.SetValue(null, null);

            GameObject managerRoot = CreateObject("Menu Interaction Manager");
            managerRoot.SetActive(false);
            m_uiManager = managerRoot.AddComponent(GetRuntimeType("ZZ.PlayerUIManager"));
            m_uiInstanceField.SetValue(null, m_uiManager);
            m_testEventSystem = CreateObject("Menu Interaction EventSystem").AddComponent<EventSystem>();
            // EditMode tests do not automatically invoke EventSystem's play-mode lifecycle.
            Invoke(m_testEventSystem, "OnEnable");
            EventSystem.current = m_testEventSystem;
        }

        [TearDown]
        public void TearDown()
        {
            m_uiInstanceField.SetValue(null, null);
            if (m_testEventSystem != null)
            {
                Invoke(m_testEventSystem, "OnDisable");
            }

            for (int objectIndex = m_createdObjects.Count - 1; objectIndex >= 0; objectIndex--)
            {
                if (m_createdObjects[objectIndex] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_createdObjects[objectIndex]);
                }
            }

            m_createdObjects.Clear();
            m_uiInstanceField.SetValue(null, m_previousUIManager);
            m_inputInstanceField.SetValue(null, m_previousInputManager);
            if (m_previousEventSystem != null)
            {
                EventSystem.current = m_previousEventSystem;
            }
            Cursor.lockState = m_previousCursorLockState;
            Cursor.visible = m_previousCursorVisible;
        }

        [Test]
        public void MenuSoundsUseTheConfiguredInputSystemWithoutLegacyInputExceptions()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Assert.DoesNotThrow(() => Invoke(m_uiManager, "UpdateMenuSounds"));
        }

        [Test]
        public void AuthoredUIPrefabLoadsWithoutInvalidComponents()
        {
            var importWarnings = new List<string>();
            GameObject prefabRoot = null;
            Application.logMessageReceived += CaptureImportWarning;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(
                    "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab");
                Assert.That(prefabRoot, Is.Not.Null);
                Assert.That(importWarnings, Is.Empty, string.Join("\n", importWarnings));
            }
            finally
            {
                Application.logMessageReceived -= CaptureImportWarning;
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            void CaptureImportWarning(string message, string stackTrace, LogType logType)
            {
                if (logType == LogType.Warning)
                {
                    importWarnings.Add(message);
                }
            }
        }

        [TestCase(false, 16)]
        [TestCase(false, 8)]
        [TestCase(true, 8)]
        public void CursorCopyRetainsReadablePixelsAtItsConfiguredSize(bool isSourceReadable, int targetWidth)
        {
            var sourceTexture = new Texture2D(16, 8, TextureFormat.RGBA32, false);
            Texture2D cursorTexture = null;
            RenderTexture previousRenderTexture = RenderTexture.active;
            try
            {
                var pixels = new Color32[sourceTexture.width * sourceTexture.height];
                for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
                {
                    pixels[pixelIndex] = new Color32(0, 255, 0, 128);
                }

                sourceTexture.SetPixels32(pixels);
                sourceTexture.Apply(false, !isSourceReadable);
                SetField(m_uiManager, "m_uiCursorTexture", sourceTexture);
                SetField(m_uiManager, "m_uiCursorPixelWidth", targetWidth);

                Invoke(m_uiManager, "BuildScaledUiCursor");

                cursorTexture = (Texture2D)m_uiManager.GetType()
                    .GetField("m_scaledUiCursor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(m_uiManager);
                Assert.That(cursorTexture, Is.Not.SameAs(sourceTexture));
                Assert.That(cursorTexture.isReadable, Is.True);
                Assert.That(cursorTexture.width, Is.EqualTo(targetWidth));
                Assert.That(cursorTexture.height, Is.EqualTo(targetWidth / 2));
                Assert.That(cursorTexture.format, Is.EqualTo(TextureFormat.RGBA32));
                Assert.That(cursorTexture.GetPixel(0, 0).g, Is.EqualTo(1f).Within(0.01f));
                Assert.That(cursorTexture.GetPixel(0, 0).a, Is.EqualTo(128f / 255f).Within(0.01f));
                Assert.That(RenderTexture.active, Is.SameAs(previousRenderTexture));
            }
            finally
            {
                SetField(m_uiManager, "m_scaledUiCursor", null);
                SetField(m_uiManager, "m_uiCursorTexture", null);
                if (cursorTexture != null && cursorTexture != sourceTexture)
                {
                    UnityEngine.Object.DestroyImmediate(cursorTexture);
                }

                UnityEngine.Object.DestroyImmediate(sourceTexture);
            }
        }

        [Test]
        public void HiddenMenuHierarchyDoesNotKeepGameplayInputBlocked()
        {
            Component equipmentManager = m_uiManager.gameObject.AddComponent(
                GetRuntimeType("ZZ.PlayerUIEquipmentManager"));
            GameObject hiddenParent = CreateObject("Hidden Menu Parent");
            GameObject menuWindow = CreateObject("Hidden Equipment Menu");
            menuWindow.transform.SetParent(hiddenParent.transform);
            SetField(equipmentManager, "m_menuWindow", menuWindow);
            SetField(m_uiManager, "m_playerUIEquipmentManager", equipmentManager);
            SetField(m_uiManager, "m_isMenuWindowOpen", true);
            hiddenParent.SetActive(false);

            Invoke(m_uiManager, "RefreshMenuWindowState");

            Assert.That(GetProperty<bool>(equipmentManager, "IsMenuOpen"), Is.False);
            Assert.That(GetProperty<bool>(m_uiManager, "IsMenuWindowOpen"), Is.False);
        }

        [Test]
        public void EquipmentBackClosesCandidatesBeforeClosingTheEquipmentMenu()
        {
            Component characterMenu = m_uiManager.gameObject.AddComponent(
                GetRuntimeType("ZZ.PlayerUICharacterMenuManager"));
            Component equipmentManager = m_uiManager.gameObject.AddComponent(
                GetRuntimeType("ZZ.PlayerUIEquipmentManager"));
            GameObject menuWindow = CreateObject("Equipment Menu");
            GameObject candidateWindow = CreateObject("Equipment Candidates");
            candidateWindow.transform.SetParent(menuWindow.transform);
            Button equipmentButton = CreateObject("Equipment Slot", typeof(RectTransform))
                .AddComponent<Button>();
            equipmentButton.transform.SetParent(menuWindow.transform);
            SetField(equipmentManager, "m_menuWindow", menuWindow);
            SetField(equipmentManager, "m_equipmentInventoryWindow", candidateWindow);
            SetField(equipmentManager, "m_equipmentSlotButtons", new[] { equipmentButton });
            SetField(m_uiManager, "m_playerUICharacterMenuManager", characterMenu);
            SetField(m_uiManager, "m_playerUIEquipmentManager", equipmentManager);
            SetField(m_uiManager, "m_isMenuWindowOpen", true);

            Invoke(characterMenu, "OnCloseMenuPerformed", default(InputAction.CallbackContext));

            Assert.That(candidateWindow.activeSelf, Is.False);
            Assert.That(menuWindow.activeSelf, Is.True);
            Assert.That(GetProperty<bool>(m_uiManager, "IsMenuWindowOpen"), Is.True);
            Assert.That(m_testEventSystem.currentSelectedGameObject, Is.EqualTo(equipmentButton.gameObject));

            Invoke(characterMenu, "OnCloseMenuPerformed", default(InputAction.CallbackContext));

            Assert.That(menuWindow.activeSelf, Is.False);
            Assert.That(GetProperty<bool>(m_uiManager, "IsMenuWindowOpen"), Is.False);
        }

        private GameObject CreateObject(string objectName, params Type[] componentTypes)
        {
            var gameObject = new GameObject(objectName, componentTypes);
            m_createdObjects.Add(gameObject);
            return gameObject;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail($"Could not resolve field {fieldName}.");
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not resolve method {methodName}.");
            method.Invoke(target, arguments);
        }
    }
}
