using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ZZ.Editor
{
    /// <summary>Exercises the actual player loop with temporary Input System devices in an unsaved session.</summary>
    [InitializeOnLoad]
    public static class OriginalInputRuntimeSmoke
    {
        private const string k_Report = "Docs/OriginalActionRepair/runtime-input.txt";
        private const string k_StartupPhase = "ZZ.OriginalInputVerification.Startup";

        static OriginalInputRuntimeSmoke() { EditorApplication.update += WaitForVillage; }

        [MenuItem("Tools/ZZ/Combat/Start Original Input Verification")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            Directory.CreateDirectory("Docs/OriginalActionRepair");
            EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath);
            File.WriteAllText(k_Report, "STARTING UNSAVED SESSION\n");
            SessionState.SetInt(k_StartupPhase, 1);
            SessionState.SetFloat(k_StartupPhase + ".Start", (float)EditorApplication.timeSinceStartup);
            AllMenuPresentationSmoke.StartReviewFromTitle();
        }

        private static void WaitForVillage()
        {
            int phase = SessionState.GetInt(k_StartupPhase, 0);
            if (phase == 0 || !EditorApplication.isPlaying) { return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(k_StartupPhase + ".Start", 0f) > 180d)
                {
                    throw new TimeoutException("Original input verification startup timed out.");
                }
                if (phase == 1 && WorldSaveGameManager.Instance != null &&
                    UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>() != null)
                {
                    GameplayRuntimeSmoke.Start();
                    SessionState.SetInt(k_StartupPhase, 2);
                }
                else if (phase == 2 && SceneManager.GetActiveScene().path == WorldScenePathLayout.GameplayScenePath &&
                    WorldSaveGameManager.Instance?.IsSceneLoadInProgress == false &&
                    WorldAIManager.Instance?.IsPerformingLoadingOperation == false &&
                    PlayerUIManager.Instance?.PlayerUILoadingScreenManager?.IsLoadingScreenActive == false)
                {
                    SessionState.SetInt(k_StartupPhase, 0);
                    Run();
                }
            }
            catch (Exception exception)
            {
                SessionState.SetInt(k_StartupPhase, 0);
                File.AppendAllText(k_Report, "FAIL: " + exception);
            }
        }

        [MenuItem("Tools/ZZ/Combat/Verify Original Input Execution")]
        public static void Run()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved Village smoke session.");
            }
            File.WriteAllText(k_Report, "RUNNING: actual player loop, temporary keyboard/mouse/gamepad.\n");
            save.StartCoroutine(Observe());
        }

        [MenuItem("Tools/ZZ/Combat/Verify Current Original Equipment Placements")]
        public static void VerifyCurrentOriginalEquipmentPlacements()
        {
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            if (!Application.isPlaying || player == null)
            {
                throw new InvalidOperationException("Load a character into Village before checking its equipment.");
            }
            const string report = "Docs/WeaponCoordinateRepair/saved-equipment.txt";
            File.WriteAllText(report, "RUNNING: checking the loaded character's source placements.\n");
            CheckSourceGripPlacements(player);
            File.AppendAllText(report, "PASS: loaded character " + player.PlayerNetworkManager.CharacterName.Value +
                "; slot=" + WorldSaveGameManager.Instance.CurrentCharacterSlot + "; original equipment placements match.\n");
        }

        private static IEnumerator Observe()
        {
            PlayerManager player = PlayerUIManager.Instance.LocalPlayer;
            PlayerNetworkManager network = player.PlayerNetworkManager;
            foreach (AICharacterManager enemy in UnityEngine.Object.FindObjectsByType<AICharacterManager>(FindObjectsSortMode.None))
            {
                enemy.enabled = false;
                enemy.GetComponent<AICharacterCombatManager>()?.CloseDamageColliders();
            }
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Original input verification");
            Mouse mouse = InputSystem.AddDevice<Mouse>("Original input verification");
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>("Original input verification");
            try
            {
                // Synthetic-device verification can run while the Editor is unfocused; modal blocking still applies.
                PlayerInputManager.Instance.EnablePlayerControls();
                Check(PlayerInputManager.Instance.IsMovementInputEnabled, "Gameplay action map is enabled");
                Check(!PlayerUIManager.Instance.IsMenuWindowOpen, "No menu intercepts gameplay input");
                CheckSourceGripPlacements(player);
                Check(player.GetComponentInChildren<Animator>().cullingMode == AnimatorCullingMode.AlwaysAnimate,
                    "Player combat animation keeps advancing outside the camera frustum");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftAlt));
                yield return null;
                yield return null;
                Check(Mathf.Abs(PlayerInputManager.Instance.MoveAmount - 0.5f) < 0.01f, "W + Alt walks");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null;
                yield return null;
                Check(PlayerInputManager.Instance.MoveAmount > 0.9f, "Releasing Alt restores running movement");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.4f);

                bool initialStance = network.IsTwoHandingWeapon.Value;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Check(network.IsTwoHandingWeapon.Value != initialStance && !network.IsAttacking.Value,
                    "E + left click changes stance without attacking");
                CheckSourceGripPlacements(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.4f);
                Check(network.IsTwoHandingWeapon.Value == initialStance, "Second chord restores the original stance");
                CheckSourceGripPlacements(player);

                Transform anchor = player.GetComponentsInChildren<Transform>(true).Single(item => item.name == "ER_R_Weapon");
                Quaternion before = anchor.localRotation;
                int verifiedSamples = 0;
                float maximumTrackError = 0f;
                var inputType = typeof(PlayerInputManager);
                var controls = inputType.GetField("m_playerControls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(PlayerInputManager.Instance);
                var inputAsset = (InputActionAsset)controls.GetType().GetProperty("asset").GetValue(controls);
                int presses = 0;
                inputAsset.FindAction("Player Movement/RB").performed += _ => presses++;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                Check(network.IsAttacking.Value && player.PlayerCombatManager.CurrentAttackType == AttackType.LightAttack01,
                    "Left click starts the right-hand light attack; attacking=" + network.IsAttacking.Value +
                    "; type=" + player.PlayerCombatManager.CurrentAttackType + "; busy=" + player.IsPerformingAction +
                    "; grounded=" + player.IsGrounded + "; stamina=" + network.CurrentStamina.Value +
                    "; jumping=" + network.IsJumping.Value + "; menu=" + PlayerUIManager.Instance.IsMenuWindowOpen +
                    "; enabled=" + PlayerInputManager.Instance.IsMovementInputEnabled +
                    "; presses=" + presses + "; modifier=" + inputType.GetField("m_isTwoHandInputHeld", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(PlayerInputManager.Instance) +
                    "; weapon=" + player.PlayerCombatManager.CurrentWeaponBeingUsed?.name +
                    "; requirements=" + player.PlayerCombatManager.CurrentWeaponBeingUsed?.MeetsRequirements(player) +
                    "; playable=" + player.PlayerAnimatorManager.CanPlayAttack(AttackType.LightAttack01,
                        player.PlayerCombatManager.CurrentWeaponBeingUsed));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                float angle = 0f;
                float end = Time.time + 2.5f;
                while (Time.time < end)
                {
                    angle = Mathf.Max(angle, Quaternion.Angle(before, anchor.localRotation));
                    Animator animator = player.GetComponentInChildren<Animator>();
                    for (int layer = 0; layer < animator.layerCount; layer++)
                    {
                        if (animator.IsInTransition(layer)) { continue; }
                        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                        if (state.normalizedTime < 0.15f || state.normalizedTime > 0.8f) { continue; }
                        foreach (AnimatorClipInfo info in animator.GetCurrentAnimatorClipInfo(layer))
                        {
                            if (!info.clip.name.EndsWith("straight_sword_main_light_attack_01")) { continue; }
                            string path = AnimationUtility.CalculateTransformPath(anchor, animator.transform);
                            var values = new float[4];
                            for (int axis = 0; axis < 4; axis++)
                            {
                                AnimationCurve curve = AnimationUtility.GetEditorCurve(info.clip,
                                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]));
                                if (curve == null) { throw new InvalidOperationException("Live motion lost the weapon track."); }
                                values[axis] = curve.Evaluate(state.normalizedTime * info.clip.length);
                            }
                            Quaternion expected = new Quaternion(values[0], values[1], values[2], values[3]).normalized;
                            maximumTrackError = Mathf.Max(maximumTrackError, Quaternion.Angle(expected, anchor.localRotation));
                            verifiedSamples++;
                        }
                    }
                    yield return null;
                }
                Check(angle > 2f, "Live Animator preserves the weapon's independent rotation; degrees=" + angle.ToString("F3"));
                Check(verifiedSamples >= 3 && maximumTrackError < 2f,
                    "Live weapon follows original curve; samples=" + verifiedSamples + "; max angle error=" + maximumTrackError);
                Check(!player.IsPerformingAction, "Light attack returns to controllable movement");

                foreach (Key shift in new[] { Key.LeftShift, Key.RightShift })
                {
                    network.CurrentStamina.Value = network.MaxStamina.Value;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(shift));
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                    yield return null;
                    yield return null;
                    Check(player.PlayerCombatManager.IsChargingAttack, shift + " + left click begins heavy charging");
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    yield return null;
                    AttackType attack = player.PlayerCombatManager.CurrentAttackType;
                    Check(network.IsAttacking.Value && (attack == AttackType.HeavyAttack01 || attack == AttackType.ChargedAttack01),
                        "Releasing " + shift + " + left click executes the heavy attack");
                    yield return new WaitForSeconds(3f);
                }
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                yield return null;
                Check(network.IsBlocking.Value, "Right click holds guard");
                yield return new WaitForSeconds(0.4f);
                Check(player.GetComponentInChildren<Animator>().GetCurrentAnimatorClipInfo(0)
                    .Any(info => info.clip.name.Contains("a000_000100")), "Guard idle uses the extracted shield posture");
                CheckSourceGripPlacements(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(0.5f);
                Check(player.GetComponentInChildren<Animator>().GetCurrentAnimatorClipInfo(0)
                    .Any(info => info.clip.name.Contains("a000_000100")), "Walking guard retains the original shield posture");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                yield return null;
                Check(!network.IsBlocking.Value, "Releasing right click drops guard");

                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                yield return null;
                yield return null;
                Check(network.IsAttacking.Value && player.PlayerCombatManager.CurrentAttackType == AttackType.LightAttack01,
                    "Gamepad RB starts the same light attack");
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForSeconds(2.5f);
                network.CurrentStamina.Value = network.MaxStamina.Value;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return VerifyOriginalParry(player, "Shift + right click");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.5f);
                network.CurrentStamina.Value = network.MaxStamina.Value;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1f });
                yield return VerifyOriginalParry(player, "Gamepad LT");
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForSeconds(0.5f);
                network.CurrentStamina.Value = network.MaxStamina.Value;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                yield return null;
                Check(network.IsJumping.Value, "F starts a jump");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X));
                yield return null;
                yield return null;
                Check(network.IsSneaking.Value, "X toggles crouching");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                File.AppendAllText(k_Report, "PASS: ORIGINAL INPUT EXECUTION COMPLETED\n");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/OriginalActionRepair/runtime-input.png"));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(gamepad);
            }
        }

        private static IEnumerator VerifyOriginalParry(PlayerManager player, string input)
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            int layer = animator.GetLayerIndex("Action Override");
            bool playedOriginal = false;
            bool openedWindow = false;
            float until = Time.time + 2f;
            while (Time.time < until)
            {
                yield return null;
                playedOriginal |= animator.GetCurrentAnimatorClipInfo(layer).Any(info =>
                    info.clip.name.Contains("a692_040040"));
                openedWindow |= player.CharacterNetworkManager.IsParrying.Value;
            }
            Check(playedOriginal, input + " plays the extracted medium shield parry");
            Check(openedWindow && !player.CharacterNetworkManager.IsParrying.Value,
                input + " opens and closes the parry window");
            CheckSourceGripPlacements(player);
        }

        [MenuItem("Tools/ZZ/Combat/Verify Original Shield Input Execution")]
        public static void VerifyOriginalShieldInputExecution()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved Village smoke session.");
            }
            File.WriteAllText(k_Report, "RUNNING: focused shield input verification.\n");
            save.StartCoroutine(ObserveShieldInputs());
        }

        private static IEnumerator ObserveShieldInputs()
        {
            PlayerManager player = PlayerUIManager.Instance.LocalPlayer;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Shield verification");
            Mouse mouse = InputSystem.AddDevice<Mouse>("Shield verification");
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>("Shield verification");
            try
            {
                PlayerInputManager.Instance.EnablePlayerControls();
                player.PlayerNetworkManager.CurrentStamina.Value = player.PlayerNetworkManager.MaxStamina.Value;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return new WaitForSeconds(0.5f);
                Check(player.CharacterNetworkManager.IsBlocking.Value, "Right click holds guard");
                Animator animator = player.GetComponentInChildren<Animator>();
                Check(animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip.name.Contains("a000_000100")),
                    "Guard idle uses the original shield posture");
                CheckSourceGripPlacements(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(0.5f);
                Check(animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip.name.Contains("a000_000100")),
                    "Walking guard retains the original shield posture");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(0.5f);
                Check(!player.CharacterNetworkManager.IsBlocking.Value, "Releasing right click drops guard");
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                yield return new WaitForSeconds(0.5f);
                Check(player.CharacterNetworkManager.IsBlocking.Value, "Gamepad LB holds guard");
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForSeconds(0.5f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return VerifyOriginalParry(player, "Shift + right click");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(0.5f);
                player.PlayerNetworkManager.CurrentStamina.Value = player.PlayerNetworkManager.MaxStamina.Value;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1f });
                yield return VerifyOriginalParry(player, "Gamepad LT");
                File.AppendAllText(k_Report, "PASS: ORIGINAL SHIELD INPUT EXECUTION COMPLETED\n");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(gamepad);
            }
        }

        private static void CheckSourceGripPlacements(PlayerManager player)
        {
            JObject source = JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Game/Art/Equipment/EldenRing/OriginalAttachments.json").text);
            int checkedModels = 0;
            foreach (WeaponModelInstantiationSlot slot in player.GetComponentsInChildren<WeaponModelInstantiationSlot>(true))
            {
                GameObject model = slot.CurrentWeaponModel;
                if (model == null)
                {
                    continue;
                }
                int parameter = model.name.StartsWith("Medium Shield") ? 49 : 23;
                bool isStored = slot.WeaponModelSlot == WeaponModelSlot.BackSlot || slot.WeaponModelSlot == WeaponModelSlot.HipSlot;
                string field = isStored
                    ? player.PlayerNetworkManager.IsTwoHandingLeftWeapon.Value ? "rightHang_0" : "leftHang_0"
                    : player.PlayerNetworkManager.IsTwoHandingWeapon.Value
                    ? player.PlayerNetworkManager.IsTwoHandingLeftWeapon.Value ? "leftBoth_0" : "both_0"
                    : slot.WeaponModelSlot == WeaponModelSlot.RightHandSlot ? "right_0" : "left_0";
                int id = (int)source["Parameters"].Single(row => (int)row["Id"] == parameter)["Fields"][field] % 1000;
                JToken placement = source["Attachments"].Single(row => (int)row["Id"] == id);
                float[] position = placement["Position"].ToObject<float[]>();
                float[] rotation = placement["Rotation"].ToObject<float[]>();
                Check(slot.transform.parent.name == "ER_" + (string)placement["Bone"] &&
                    Vector3.Distance(model.transform.localPosition, new Vector3(position[0], position[1], position[2])) < 0.00001f &&
                    Quaternion.Angle(model.transform.localRotation,
                        new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]).normalized) < 0.05f,
                    model.name + " uses original " + field + " dummy " + id + " in the running player loop");
                checkedModels++;
            }
            Check(checkedModels == 2, "Both equipped source models were checked, including storage during two-handing");
        }

        private static void Check(bool condition, string message)
        {
            File.AppendAllText(k_Report, (condition ? "PASS: " : "FAIL: ") + message + "\n");
            if (!condition) { throw new InvalidOperationException(message); }
        }
    }
}
