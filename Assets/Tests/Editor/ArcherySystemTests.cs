using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public class ArcherySystemTests
    {
        private const string k_BowPath =
            "Assets/Data/Items/Weapons/Ranged Weapons/Longbow.asset";
        private const string k_StandardArrowPath =
            "Assets/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_FireArrowPath =
            "Assets/Data/Items/Projectiles/Fire Arrow.asset";
        private const string k_ReleaseArrowPrefabPath =
            "Assets/Data/Prefabs/Projectiles/Released Arrow.prefab";
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";
        private const string k_UIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_BowFireClipPath =
            "Assets/Data/Animations/Archery/Bow_Fire.anim";

        [Test]
        public void BowAndArrowAssetsRemainSeparateAndCompatible()
        {
            UnityEngine.Object bow = AssetDatabase.LoadMainAssetAtPath(k_BowPath);
            UnityEngine.Object standardArrow = AssetDatabase.LoadMainAssetAtPath(
                k_StandardArrowPath);
            UnityEngine.Object fireArrow = AssetDatabase.LoadMainAssetAtPath(
                k_FireArrowPath);
            MethodInfo compatibility = bow.GetType().GetMethod(
                "CanFireProjectile");

            Assert.That(bow.GetType().Name, Is.EqualTo("RangedWeaponItem"));
            Assert.That(
                standardArrow.GetType().Name,
                Is.EqualTo("RangedProjectileItem"));
            Assert.That(GetItemID(bow), Is.EqualTo(11));
            Assert.That(GetItemID(standardArrow), Is.EqualTo(12));
            Assert.That(GetItemID(fireArrow), Is.EqualTo(13));
            Assert.That(
                compatibility.Invoke(bow, new[] { standardArrow }),
                Is.True);
            Assert.That(
                standardArrow.GetType().GetProperty("MaxAmmoAmount")
                    ?.GetValue(standardArrow),
                Is.EqualTo(30));
            Assert.That(
                (float)fireArrow.GetType().GetProperty("FireDamage")
                    ?.GetValue(fireArrow),
                Is.GreaterThan(0f));
        }

        [Test]
        public void ProjectilePrefabUsesContinuousPhysicsAndSeparatedDamageTrigger()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_ReleaseArrowPrefabPath);
            Rigidbody rigidbody = prefab.GetComponent<Rigidbody>();
            Component projectileManager = prefab.GetComponents<Component>()
                .Single(component =>
                    component.GetType().Name == "RangedProjectileManager");
            Component damageCollider = prefab.GetComponentsInChildren<Component>(true)
                .Single(component =>
                    component.GetType().Name == "RangeProjectileDamageCollider");
            Collider damageTrigger = damageCollider.GetComponent<Collider>();

            Assert.That(projectileManager, Is.Not.Null);
            Assert.That(rigidbody.useGravity, Is.True);
            Assert.That(
                rigidbody.collisionDetectionMode,
                Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("Projectile")));
            Assert.That(damageTrigger.isTrigger, Is.True);
            Assert.That(
                damageCollider.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("Damage Collider")));
            Assert.That(
                Physics.GetIgnoreLayerCollision(
                    LayerMask.NameToLayer("Projectile"),
                    LayerMask.NameToLayer("Player")),
                Is.True);
            Assert.That(
                Physics.GetIgnoreLayerCollision(
                    LayerMask.NameToLayer("Projectile"),
                    LayerMask.NameToLayer("Damageable Character")),
                Is.True);
        }

        [TestCase(true, false, 1f, true, 1, true)]
        [TestCase(true, true, 1f, true, 1, false)]
        [TestCase(true, false, 0f, true, 1, false)]
        [TestCase(true, false, 1f, false, 1, false)]
        [TestCase(true, false, 1f, true, 0, false)]
        public void NotchingRequiresOwnershipIdleStaminaCompatibilityAndAmmo(
            bool isOwner,
            bool isPerformingAction,
            float stamina,
            bool isCompatible,
            int ammo,
            bool expected)
        {
            Type combatType = Type.GetType(
                "ZZ.PlayerCombatManager, Assembly-CSharp");
            MethodInfo method = combatType.GetMethod(
                "CanNotchProjectile",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(
                method.Invoke(
                    null,
                    new object[]
                    {
                        isOwner,
                        isPerformingAction,
                        stamina,
                        isCompatible,
                        ammo
                    }),
                Is.EqualTo(expected));
        }

        [Test]
        public void PlayerReplicatesDualAmmoNotchHoldAndAimState()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Component network = root.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "PlayerNetworkManager");
                string[] ownerWrittenVariables =
                {
                    "MainProjectileID",
                    "SecondaryProjectileID",
                    "CurrentProjectileID",
                    "CurrentProjectileSlot",
                    "HasArrowNotched",
                    "IsHoldingArrow",
                    "IsAiming"
                };
                foreach (string propertyName in ownerWrittenVariables)
                {
                    AssertOwnerWrittenNetworkVariable(network, propertyName);
                }

                Component inventory = root.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "PlayerInventoryManager");
                SerializedObject serializedInventory = new SerializedObject(inventory);
                SerializedProperty rightWeaponSlots = serializedInventory.FindProperty(
                    "m_weaponsInRightHandSlots");
                Assert.That(
                    serializedInventory.FindProperty("m_startingMainProjectile")
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    serializedInventory.FindProperty("m_startingSecondaryProjectile")
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    Enumerable.Range(0, rightWeaponSlots.arraySize)
                        .Select(index => rightWeaponSlots
                            .GetArrayElementAtIndex(index)
                            .objectReferenceValue)
                        .Any(item => item != null && item.GetType().Name ==
                            "RangedWeaponItem"),
                    Is.True);
                Assert.That(
                    root.GetComponentsInChildren<Transform>(true)
                        .Any(transform => transform.name == "Projectile Pivot"),
                    Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ReleaseRpcCarriesDirectionAndFireFrameYaw()
        {
            Type networkType = Type.GetType(
                "ZZ.PlayerNetworkManager, Assembly-CSharp");
            MethodInfo releaseRpc = networkType.GetMethod(
                "NotifyServerOfReleaseProjectileServerRpc");
            ParameterInfo[] parameters = releaseRpc.GetParameters();

            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(Vector3)));
            Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(float)));
            Assert.That(
                releaseRpc.GetCustomAttributes(false)
                    .Any(attribute =>
                        attribute.GetType().Name == "ServerRpcAttribute"),
                Is.True);
        }

        [Test]
        public void ProjectileDirectionUsesAimSnapshotOrYawFallback()
        {
            Type combatType = Type.GetType(
                "ZZ.PlayerCombatManager, Assembly-CSharp");
            MethodInfo resolve = combatType.GetMethod(
                "ResolveReplicatedProjectileDirection",
                BindingFlags.Public | BindingFlags.Static);

            Vector3 aimed = (Vector3)resolve.Invoke(
                null,
                new object[] { new Vector3(1f, 1f, 0f), 90f });
            Vector3 fallback = (Vector3)resolve.Invoke(
                null,
                new object[] { Vector3.zero, 90f });
            Assert.That(aimed, Is.EqualTo(new Vector3(1f, 1f, 0f).normalized));
            Assert.That(Vector3.Angle(fallback, Vector3.right), Is.LessThan(0.01f));
        }

        [Test]
        public void ProjectileBlockingUsesArrowForwardAndOneHundredFortyFiveDegrees()
        {
            Type colliderType = Type.GetType(
                "ZZ.RangeProjectileDamageCollider, Assembly-CSharp");
            MethodInfo blockMethod = colliderType.GetMethod(
                "IsWithinBlockingAngle",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                blockMethod.Invoke(
                    null,
                    new object[] { Vector3.forward, Vector3.back }),
                Is.True);
            Assert.That(
                blockMethod.Invoke(
                    null,
                    new object[] { Vector3.forward, Vector3.forward }),
                Is.False);
        }

        [Test]
        public void BowAnimatorReleasesOnlyFromTheFireAnimationEvent()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                k_ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .Single(layer => layer.name == "Action Override")
                .stateMachine;
            string[] stateNames = stateMachine.states
                .Select(childState => childState.state.name)
                .ToArray();
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(k_BowFireClipPath));

            Assert.That(stateNames, Does.Contain("Bow_Draw"));
            Assert.That(stateNames, Does.Contain("Bow_Aim"));
            Assert.That(stateNames, Does.Contain("Bow_Fire"));
            Assert.That(stateNames, Does.Contain("Bow_Out_Of_Ammo"));
            Assert.That(
                events.Count(animationEvent =>
                    animationEvent.functionName == "ReleaseArrow"),
                Is.EqualTo(1));
        }

        [Test]
        public void AimCrosshairStartsHiddenAtExactScreenCenter()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_UIManagerPrefabPath);
            try
            {
                Component hud = root.GetComponentsInChildren<Component>(true)
                    .Single(component =>
                        component.GetType().Name == "PlayerUIHUDManager");
                GameObject crosshair = new SerializedObject(hud)
                    .FindProperty("m_crosshair")
                    .objectReferenceValue as GameObject;
                RectTransform rect = crosshair.transform as RectTransform;

                Assert.That(crosshair.activeSelf, Is.False);
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void RangedInputsPreserveHoldAndReleaseSemantics()
        {
            InputActionsDocument inputAsset = JsonUtility.FromJson<InputActionsDocument>(
                File.ReadAllText("Assets/PlayerControls.inputactions"));
            InputBindingData[] bindings = inputAsset.maps
                .Single(map => map.name == "Player Movement")
                .bindings;
            InputBindingData[] rbBindings = bindings
                .Where(binding => binding.action == "RB")
                .ToArray();
            InputBindingData[] rtBindings = bindings
                .Where(binding => binding.action == "RT")
                .ToArray();

            Assert.That(
                rbBindings.Where(binding => !binding.isComposite)
                    .All(binding => binding.interactions.Contains("Hold")),
                Is.True);
            Assert.That(rtBindings.Length, Is.GreaterThanOrEqualTo(2));
            Type inputManagerType = Type.GetType(
                "ZZ.PlayerInputManager, Assembly-CSharp");
            Assert.That(
                inputManagerType.GetMethod(
                    "OnRTCanceled",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
            Assert.That(
                inputManagerType.GetMethod(
                    "OnRBCanceled",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
        }

        [Test]
        public void ProjectileAmmoSurvivesSaveSerialization()
        {
            CharacterSaveData saveData = new CharacterSaveData
            {
                MainProjectileID = 12,
                SecondaryProjectileID = 13,
                MainProjectileAmount = 17,
                SecondaryProjectileAmount = 9
            };

            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(saveData));
            Assert.That(restored.MainProjectileID, Is.EqualTo(12));
            Assert.That(restored.SecondaryProjectileID, Is.EqualTo(13));
            Assert.That(restored.MainProjectileAmount, Is.EqualTo(17));
            Assert.That(restored.SecondaryProjectileAmount, Is.EqualTo(9));
        }

        private static int GetItemID(UnityEngine.Object item)
        {
            return (int)item.GetType().GetProperty("ItemID").GetValue(item);
        }

        private static void AssertOwnerWrittenNetworkVariable(
            Component network,
            string propertyName)
        {
            object networkVariable = network.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(network);
            object writePermission = networkVariable?.GetType()
                .GetProperty("WritePerm")
                ?.GetValue(networkVariable);
            Assert.That(writePermission?.ToString(), Is.EqualTo("Owner"));
        }

        [Serializable]
        private sealed class InputActionsDocument
        {
            public InputActionMapData[] maps;
        }

        [Serializable]
        private sealed class InputActionMapData
        {
            public string name;
            public InputBindingData[] bindings;
        }

        [Serializable]
        private sealed class InputBindingData
        {
            public string action;
            public string interactions;
            public bool isComposite;
        }
    }
}
