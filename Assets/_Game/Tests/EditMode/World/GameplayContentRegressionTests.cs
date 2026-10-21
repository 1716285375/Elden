using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class GameplayContentRegressionTests
    {
        [Test]
        public void ButtonVisualRefreshesWhenCommandBecomesUnavailable()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab");
            try
            {
                GameObject buttonObject = root.transform.Find(
                    "Player UI/Character Menu/Menu Panel/Command Column/Save Game Button").gameObject;
                Component button = buttonObject.GetComponent("Button");
                Component visual = buttonObject.GetComponent("FrontendSelectableVisual");
                BindingFlags methods = BindingFlags.Instance | BindingFlags.NonPublic;
                visual.GetType().GetMethod("Awake", methods).Invoke(visual, null);
                button.GetType().GetProperty("interactable").SetValue(button, true);
                visual.GetType().GetMethod("OnEnable", methods).Invoke(visual, null);
                button.GetType().GetProperty("interactable").SetValue(button, false);
                visual.GetType().GetMethod("LateUpdate", methods).Invoke(visual, null);
                var data = new SerializedObject(visual);
                var background = (Component)data.FindProperty("m_selectionBackground").objectReferenceValue;
                Color actual = (Color)background.GetType().GetProperty("color").GetValue(background);
                Assert.That(actual, Is.EqualTo(data.FindProperty("m_disabledBackgroundColor").colorValue));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void CharacterMenuCommandsHaveSeparateVisibleBounds()
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(
                "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab");
            try
            {
                Transform panel = prefab.transform.Find("Player UI/Character Menu/Menu Panel/Command Column");
                panel.parent.parent.gameObject.SetActive(true);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel);
                string[] names = { "Equipment Button", "Upgrade Weapon Button", "Save Game Button", "Return Button",
                    "Return To Main Menu Button", "Quit Game Button" };
                for (int firstIndex = 0; firstIndex < names.Length; firstIndex++)
                {
                    var first = (RectTransform)panel.Find(names[firstIndex]);
                    Assert.That(first.gameObject.activeSelf, Is.True, names[firstIndex]);
                    for (int secondIndex = firstIndex + 1; secondIndex < names.Length; secondIndex++)
                    {
                        var second = (RectTransform)panel.Find(names[secondIndex]);
                        Rect firstBounds = new((Vector2)first.localPosition + first.rect.position, first.rect.size);
                        Rect secondBounds = new((Vector2)second.localPosition + second.rect.position, second.rect.size);
                        Assert.That(firstBounds.height, Is.GreaterThan(0));
                        Assert.That(firstBounds.Overlaps(secondBounds), Is.False, $"{first.name} overlaps {second.name}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        [Test]
        public void CompoundPlayerOverlapRemainsUntilLastColliderLeavesAndStayRestoresRegistration()
        {
            GameObject player = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            var root = new GameObject("Interaction regression", typeof(BoxCollider));
            try
            {
                Type type = Type.GetType("ZZ.Interactable, Assembly-CSharp", true);
                Component interaction = root.AddComponent(type);
                Component manager = player.GetComponent("PlayerInteractionManager");
                Collider first = player.GetComponent<Collider>();
                var secondObject = new GameObject("Second collider");
                secondObject.transform.SetParent(player.transform);
                Collider second = secondObject.AddComponent<BoxCollider>();
                MethodInfo enter = type.GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo exit = type.GetMethod("OnTriggerExit", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo stay = type.GetMethod("OnTriggerStay", BindingFlags.Instance | BindingFlags.NonPublic);
                var interactions = (ICollection)manager.GetType().GetProperty("CurrentInteractableActions").GetValue(manager);
                enter.Invoke(interaction, new object[] { first });
                enter.Invoke(interaction, new object[] { second });
                Assert.That(interactions.Count, Is.EqualTo(1));
                exit.Invoke(interaction, new object[] { first });
                Assert.That(interactions.Count, Is.EqualTo(1));
                exit.Invoke(interaction, new object[] { second });
                Assert.That(interactions.Count, Is.Zero);
                stay.Invoke(interaction, new object[] { second });
                Assert.That(interactions.Count, Is.EqualTo(1));
                root.SetActive(false);
                // Non-ExecuteAlways behaviours do not receive lifecycle callbacks in Edit Mode.
                type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interaction, null);
                Assert.That(interactions.Count, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        [Test]
        public void AreaVolumesUsePlayerPositionAndDeterministicSharedEdges()
        {
            var root = new GameObject("Area regression", typeof(BoxCollider));
            try
            {
                BoxCollider box = root.GetComponent<BoxCollider>();
                box.size = new Vector3(20f, 10f, 20f);
                root.transform.position = new Vector3(100f, 5f, 100f);
                Component trigger = root.AddComponent(Type.GetType("ZZ.EventTriggerLoadScene, Assembly-CSharp", true));
                MethodInfo contains = trigger.GetType().GetMethod("ContainsPosition", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(contains.Invoke(trigger, new object[] { new Vector3(100f, 5f, 100f) }), Is.True);
                Assert.That(contains.Invoke(trigger, new object[] { new Vector3(110f, 5f, 100f) }), Is.False);
                Assert.That(contains.Invoke(trigger, new object[] { new Vector3(90f, 5f, 100f) }), Is.True);
                box.enabled = false;
                Assert.That(contains.Invoke(trigger, new object[] { root.transform.position }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BroadswordOverridesPreserveRuntimeControllerAndActionEventOrder()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                "Assets/_Game/Data/Animations/Broadsword/Broadsword.overrideController");
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.runtimeAnimatorController.name, Is.EqualTo("Humanoid Runtime"));
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(pairs);
            Assert.That(pairs.Count(pair => pair.Value != null), Is.GreaterThan(50));
            foreach (var pair in pairs.Where(pair => pair.Value != null))
            {
                AnimationEvent[] expected = AnimationUtility.GetAnimationEvents(pair.Key);
                AnimationEvent[] actual = AnimationUtility.GetAnimationEvents(pair.Value);
                Assert.That(actual.Select(entry => entry.functionName), Is.EqualTo(expected.Select(entry => entry.functionName)), pair.Key.name);
                for (int index = 0; index < expected.Length; index++)
                {
                    Assert.That(actual[index].time / pair.Value.length,
                        Is.EqualTo(expected[index].time / pair.Key.length).Within(0.001f), pair.Key.name);
                }
            }
        }
    }
}
