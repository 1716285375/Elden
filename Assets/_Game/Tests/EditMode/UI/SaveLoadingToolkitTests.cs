using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public class SaveLoadingToolkitTests
    {
        [Test]
        public void SaveRejectsClosedOrBlockedMenuWithoutCallingWorldWriter()
        {
            Type type = Type.GetType("ZZ.PlayerUISaveGameManager, Assembly-CSharp", true);
            GameObject owner = new GameObject("Save Guard Test");
            try
            {
                Component manager = owner.AddComponent(type);
                MethodInfo save = type.GetMethod("SaveCurrentGame");
                Assert.That(type.GetProperty("CanSaveCurrentGame").GetValue(manager), Is.False);
                Assert.DoesNotThrow(() => save.Invoke(manager, null));
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo open = type.GetField("m_isSaveGameMenuOpen", flags);
                FieldInfo lastFrame = type.GetField("m_lastSaveFrame", flags);
                open.SetValue(manager, true);
                type.GetField("m_hasSavedThisOpen", flags).SetValue(manager, true);
                save.Invoke(manager, null);
                Assert.That(lastFrame.GetValue(manager), Is.EqualTo(-1), "Repeated success never dispatches another save.");
                type.GetField("m_hasSavedThisOpen", flags).SetValue(manager, false);
                type.GetMethod("SetDeathInputBlocked").Invoke(manager, new object[] { true });
                Assert.That(type.GetProperty("IsSaveGameMenuOpen").GetValue(manager), Is.False);
                save.Invoke(manager, null);
                Assert.That(lastFrame.GetValue(manager), Is.EqualTo(-1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void LayoutsSeparateExplicitSaveFromAnIndeterminateLoadingCover()
        {
            VisualElement save = Resources.Load<VisualTreeAsset>("UI/SaveGame").CloneTree();
            Assert.That(save.Q<Button>("save"), Is.Not.Null);
            Assert.That(save.Q<Button>("back"), Is.Not.Null);
            Assert.That(save.Q<Label>("save-feedback"), Is.Not.Null);
            VisualElement loading = Resources.Load<VisualTreeAsset>("UI/LoadingScreen").CloneTree();
            Assert.That(loading.Q("loading-overlay").focusable, Is.True);
            Assert.That(loading.Q("loading-pulse"), Is.Not.Null);
            Assert.That(loading.Query<Button>().ToList(), Is.Empty, "Loading offers no action before the world is ready.");
        }
    }
}
