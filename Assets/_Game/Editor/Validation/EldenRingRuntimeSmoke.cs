using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Records imported presentation on the spawned player in an unsaved host session.</summary>
    public static class EldenRingRuntimeSmoke
    {
        private const string k_Report = "F:/MyProject/Game/RE-Assets/EldenRing/Logs/runtime-presentation.txt";
        private const string k_EnemyReport = "F:/MyProject/Game/RE-Assets/EldenRing/Logs/runtime-enemy.txt";
        private const string k_ContentReport = "F:/MyProject/Game/RE-Assets/EldenRing/Logs/runtime-content.txt";

        /// <summary>Checks streamed map collision, the new soldier and original feedback in an unsaved session.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Verify Runtime Content")]
        public static void VerifyContent()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            save.StartCoroutine(ObserveContent());
        }

        private static IEnumerator ObserveContent()
        {
            File.WriteAllText(k_ContentReport, "RUNNING\n");
            GameObject map = GameObject.Find("Stormveil Rampart");
            CheckContent(map != null, "Original map chunk loaded with the starting props slice");
            MeshCollider[] colliders = map.GetComponentsInChildren<MeshCollider>();
            CheckContent(colliders.Any(collider => collider.Raycast(new Ray(
                collider.bounds.center + Vector3.up * 40f, Vector3.down), out _, 80f)),
                "Original map geometry responds to physical raycasts");
            Camera camera = Camera.main;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            try
            {
                var bounds = new Bounds(colliders[0].bounds.center, Vector3.zero);
                foreach (MeshCollider collider in colliders)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                camera.transform.position = bounds.center + new Vector3(48f, 26f, -55f);
                camera.transform.LookAt(bounds.center);
                CaptureCamera(camera, Path.GetDirectoryName(k_Report) + "/runtime-stormveil.png");
            }
            finally
            {
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }
            PlayerManager player = PlayerUIManager.Instance.LocalPlayer;
            CheckContent(UnityEngine.AI.NavMesh.SamplePosition(player.transform.position, out var hit,
                5f, UnityEngine.AI.NavMesh.AllAreas), "Existing starting navigation remains available");
            GameObject ranger = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Ranger/Ranger AI.prefab"), hit.position + Vector3.right * 2f,
                Quaternion.identity);
            var rangerNetwork = ranger.GetComponent<Unity.Netcode.NetworkObject>();
            try
            {
                rangerNetwork.Spawn();
                var enemy = ranger.GetComponent<AICharacterManager>();
                enemy.enabled = false;
                Animator animator = ranger.GetComponentInChildren<Animator>();
                CheckContent(animator.avatar.isHuman && animator.avatar.isValid &&
                    animator.avatar.name.Contains("c3010"), "Ranged enemy uses the new valid c3010 Avatar");
                yield return null;
                animator.Play("Locomotion", 0, 0f);
                animator.Update(0.2f);
                CaptureEnemy(ranger, "runtime-c3010.png");
            }
            finally
            {
                if (rangerNetwork.IsSpawned)
                {
                    rangerNetwork.Despawn();
                }
                else
                {
                    UnityEngine.Object.Destroy(ranger);
                }
            }
            GameObject audioFixture = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab"), hit.position, Quaternion.identity);
            try
            {
                audioFixture.GetComponent<AICharacterManager>().enabled = false;
                audioFixture.GetComponentInChildren<CharacterSoundFXManager>().PlayDamageGrunt();
                yield return null;
                CheckContent(audioFixture.GetComponentInChildren<AudioSource>().isPlaying,
                    "Existing damage feedback starts an original decoded audio clip");
            }
            finally
            {
                UnityEngine.Object.Destroy(audioFixture);
            }
            Vector3 point = player.transform.position + Vector3.up + player.transform.forward * 2f;
            CombatVfx.PlayGuard(point);
            CombatVfx.PlayParry(point + Vector3.right);
            yield return null;
            GameObject guard = GameObject.Find("Guard Spark(Clone)");
            GameObject parry = GameObject.Find("Parry Flash(Clone)");
            CheckContent(guard != null && parry != null, "Guard and parry still use their existing gameplay triggers");
            CaptureCamera(Camera.main, Path.GetDirectoryName(k_Report) + "/runtime-original-flares.png");
            yield return new WaitForSecondsRealtime(3.2f);
            CheckContent(guard == null && parry == null, "Retextured combat effects clean themselves up");
            File.AppendAllText(k_ContentReport, "ALL PASSED\n");
        }

        private static void CheckContent(bool condition, string message)
        {
            File.AppendAllText(k_ContentReport, (condition ? "PASS " : "FAIL ") + message + "\n");
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>Exercises the imported enemy's attack, damage and death in a temporary host fixture.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Verify Runtime Enemy")]
        public static void VerifyEnemy()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            save.StartCoroutine(ObserveEnemy());
        }

        private static IEnumerator ObserveEnemy()
        {
            File.WriteAllText(k_EnemyReport, "RUNNING\n");
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .Single(candidate => candidate.IsOwner);
            Vector3 position = player.transform.position + player.transform.forward * 4f;
            if (!UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit,
                    5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                throw new InvalidOperationException("No navigation surface near the player for the enemy fixture.");
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab, hit.position,
                Quaternion.LookRotation(player.transform.position - hit.position));
            var network = instance.GetComponent<Unity.Netcode.NetworkObject>();
            try
            {
                network.Spawn();
                AICharacterManager enemy = instance.GetComponent<AICharacterManager>();
                AICharacterCombatManager combat = instance.GetComponent<AICharacterCombatManager>();
                combat.AddPlayerToPlayersWithinRange(player);
                // Keep the fixture stationary; its Animator, network and combat components remain live.
                enemy.enabled = false;
                yield return null;
                CheckEnemy(enemy.IsSpawned && instance.activeInHierarchy, "Enemy fixture is active and network spawned");
                Animator animator = instance.GetComponentInChildren<Animator>();
                CheckEnemy(animator.avatar.isValid && animator.avatar.isHuman, "Imported enemy Avatar is valid");
                enemy.ResetActionFlags();
                CheckEnemy(combat.PerformAttack(), "Existing AI attack starts on imported skeleton");
                yield return new WaitForSecondsRealtime(0.3f);
                CaptureEnemy(instance, "runtime-enemy-attack.png");
                yield return new WaitForSecondsRealtime(2f);
                enemy.ResetActionFlags();
                combat.CloseDamageColliders();
                float health = enemy.CharacterNetworkManager.CurrentHealth.Value;
                var template = ScriptableObject.CreateInstance<TakeDamageEffect>();
                try
                {
                    TakeDamageEffect effect = template.CreateRuntimeDamageEffect(player,
                        25f, 0f, 0f, 0f, 0f, hit.position + Vector3.up, 50f);
                    enemy.CharacterEffectsManager.ProcessRuntimeDamageEffect(effect, DamageProcessingMode.Authoritative);
                    CheckEnemy(enemy.CharacterNetworkManager.CurrentHealth.Value < health && !enemy.IsDead,
                        "Damage pipeline reduces enemy health without killing the fixture");
                }
                finally
                {
                    UnityEngine.Object.Destroy(template);
                }
                yield return new WaitForSecondsRealtime(0.3f);
                CaptureEnemy(instance, "runtime-enemy-hit.png");
                enemy.CharacterNetworkManager.CurrentHealth.Value = 0f;
                yield return new WaitForSecondsRealtime(1f);
                CheckEnemy(enemy.IsDead, "Zero health enters the existing death flow");
                CheckEnemy(!instance.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled,
                    "Death disables navigation");
                CaptureEnemy(instance, "runtime-enemy-death.png");
                File.AppendAllText(k_EnemyReport, "ALL PASSED\n");
            }
            finally
            {
                if (network.IsSpawned)
                {
                    network.Despawn();
                }
                else
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }
        }

        private static void CheckEnemy(bool condition, string message)
        {
            File.AppendAllText(k_EnemyReport, (condition ? "PASS " : "FAIL ") + message + "\n");
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void CaptureEnemy(GameObject enemy, string filename)
        {
            Camera camera = Camera.main;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            try
            {
                Vector3 center = enemy.transform.position + Vector3.up;
                camera.transform.position = center + enemy.transform.forward * 3f + enemy.transform.right;
                camera.transform.LookAt(center);
                CaptureCamera(camera, Path.Combine(Path.GetDirectoryName(k_Report), filename));
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>Captures live renderer, skeleton and weapon placement evidence without changing assets.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Record Runtime Presentation")]
        public static void Record()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .Single(candidate => candidate.IsOwner);
            var report = new StringBuilder();
            report.AppendLine($"PLAYER position={player.transform.position} scale={player.transform.lossyScale}");
            Animator animator = player.GetComponentInChildren<Animator>();
            report.AppendLine($"ANIMATOR {animator.name} avatar={animator.avatar.name} scale={animator.transform.lossyScale}");
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Head,
                         HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot })
            {
                Transform target = animator.GetBoneTransform(bone);
                report.AppendLine($"BONE {bone} {target.name} position={target.position} scale={target.lossyScale}");
            }
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true)
                         .Where(candidate => candidate.enabled && candidate.gameObject.activeInHierarchy))
            {
                report.AppendLine($"RENDERER {renderer.name} type={renderer.GetType().Name} " +
                    $"position={renderer.transform.position} scale={renderer.transform.lossyScale} bounds={renderer.bounds}");
                if (renderer is SkinnedMeshRenderer skin)
                {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    report.AppendLine($"  CPU bounds={mesh.bounds} bones={skin.bones.Length} " +
                        $"root={skin.rootBone?.name} quality={skin.quality} offscreen={skin.updateWhenOffscreen}");
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            foreach (WeaponModelInstantiationSlot slot in player.GetComponentsInChildren<WeaponModelInstantiationSlot>(true))
            {
                report.AppendLine($"SLOT {slot.WeaponModelSlot} parent={slot.transform.parent.name} " +
                    $"position={slot.transform.position} scale={slot.transform.lossyScale} model={slot.CurrentWeaponModel?.name}");
            }
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                report.AppendLine($"CAMERA {camera.name} position={camera.transform.position} " +
                    $"rotation={camera.transform.eulerAngles} enabled={camera.enabled}");
            }
            File.WriteAllText(k_Report, report.ToString());
            CaptureCamera(Camera.main);
        }

        /// <summary>Equips the imported sword in the temporary session using the normal inventory API.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Equip Runtime Broadsword")]
        public static void EquipBroadsword()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .Single(candidate => candidate.IsOwner);
            WeaponItem source = AssetDatabase.LoadAssetAtPath<WeaponItem>(
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset");
            var weapon = WorldItemDatabase.Instance.GetRuntimeItemByID(source.ItemID) as WeaponItem;
            if (weapon == null || !player.InventoryManager.AddItemToInventory(weapon) ||
                !player.InventoryManager.EquipItemInSlot(EquipmentSlotType.RightWeapon01, weapon))
            {
                throw new InvalidOperationException("Could not equip the Broadsword fixture.");
            }
        }

        private static void CaptureCamera(Camera camera, string filename = null)
        {
            // ScreenCapture waits for Game View repaint, which may never occur in a background editor.
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(filename ?? Path.ChangeExtension(k_Report, ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
