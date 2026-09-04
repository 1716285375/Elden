# PHASE 0 — SCN_MainMenu Current Architecture Audit

Audit of the real Title Screen / Save / PlayerCamera / Netcode architecture before
any Presentation Layer work. **No code was changed.**

- Project: `C:/C/Game/Unity/Elden`
- Unity 6000.3.11f1, URP 17.3.0, Netcode for GameObjects 2.13.x, Input System 1.19.0
- Scene: `Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity` (YAML text, 9,327 lines, 278 KB)
- Namespace convention: `ZZ`

Method: scripts read directly; scene parsed from YAML (GameObjects, Transforms,
RectTransforms, Cameras, AudioListeners, MonoBehaviour script GUIDs resolved
against `*.cs.meta`, and PrefabInstance `m_SourcePrefab` GUIDs).

---

## 1. SCN_MainMenu hierarchy

Root GameObjects (Transform parent = 0):

```
SCN_MainMenu
├── Character Loading Ground
├── Directional Light                       [Light]
├── EventSystem
├── Global Volume
├── Player Camera                           [PlayerCamera]
│   └── Camera Pivot
│       └── Main Camera                     [Camera, AudioListener]
├── Player Input Manager                    [PlayerInputManager]
├── Title Screen Canvas                     [Canvas]
│   └── Title Screen Background             [TitleScreenManager]
│       ├── Delete Character Slot Popup
│       │   └── Panel
│       │       ├── Confirm Button → Label
│       │       ├── Message
│       │       └── No Button → Label
│       ├── Load Character Menu             [TitleScreenLoadMenuInputManager]
│       │   ├── Return Button → Label       [UITitleScreenSelectNoSlot]
│       │   ├── Scroll View                 [UIMatchScrollWheelToSelectedButton]
│       │   │   ├── Vertical Scrollbar → Sliding Area → Handle
│       │   │   └── Viewport → Content
│       │   │       └── Character Slot 01..10 [UICharacterSaveSlot]
│       │   │           └── Character Name, Time Played
│       │   └── Title
│       ├── No Free Character Slots Popup
│       │   └── Panel → Close Button → Label, Message
│       ├── Press Start Button → Text (TMP)
│       ├── Title Screen Banner
│       └── Title Screen Main Menu
│           ├── Load Game Button → Text (TMP)
│           └── New Game Start Button → Text (TMP)
├── World Action Manager
├── World Character Effects Manager
├── World Game Session Manager              [WorldGameSessionManager]
└── World Sound FX Manager
```

Root **PrefabInstances** (components are not expanded in the scene file):

| Prefab | Notes |
| --- | --- |
| `Assets/_Game/Prefabs/World/Managers/World Item Database.prefab` | item database singleton |
| `Assets/_Game/Prefabs/…/Player UI Manager.prefab` | UI singleton |
| `Assets/_Game/Prefabs/World/Managers/World Save Game Manager.prefab` | **holds `WorldSaveGameManager`** |
| `Assets/_Game/Prefabs/…/World Network Manager.prefab` | **holds `NetworkManager`** |

> These four were invisible to a naive scan because PrefabInstance components are
> stored as overrides only, not expanded YAML.

---

## 2. Script placement

| Script | GameObject | Root? |
| --- | --- | --- |
| `TitleScreenManager` | **Title Screen Background** (child of Title Screen Canvas) | no |
| `PlayerCamera` | Player Camera | **yes** |
| `PlayerInputManager` | Player Input Manager | **yes** |
| `WorldGameSessionManager` | World Game Session Manager | **yes** |
| `WorldSaveGameManager` | *(prefab instance)* World Save Game Manager | **yes** |
| `NetworkManager` | *(prefab instance)* World Network Manager | **yes** |
| `UICharacterSaveSlot` ×10 | Character Slot 01..10 | no |
| `TitleScreenLoadMenuInputManager` | Load Character Menu | no |
| `UIMatchScrollWheelToSelectedButton` | Scroll View | no |
| `UITitleScreenSelectNoSlot` | Return Button | no |
| `TitleScreenCharacterCreationManager` | **absent — created at runtime** | — |
| `TitleScreenPlayerPreviewRotator` | **absent — created at runtime** | — |

---

## 3. Camera audit — confirms all three of your warnings

### 3.1 `PlayerCamera` is a persistent gameplay camera

`Assets/_Game/Scripts/Characters/Player/PlayerCamera.cs`

| Line | Fact |
| --- | --- |
| 9 | `public static PlayerCamera Instance => s_instance;` |
| 59 | `public Camera CameraObject => m_cameraObject;` |
| 83 | `DontDestroyOnLoad(gameObject);` in `Awake()` |
| 110–120 | `BindPlayer()` → `transform.position = localPlayer.transform.position` |
| 161–182 | `HandleAllCameraActions()` → `FollowPlayer()`, rotations, collisions |

Binding chain, `PlayerManager.cs`:

```
LateUpdate()            :148   (returns early unless IsOwner)
  → BindLocalPlayerSystems()   :155  (defined :550)
      → PlayerCamera.Instance?.BindPlayer(this)      :557
  → PlayerCamera.Instance?.HandleAllCameraActions()  :156
```

**You press Start → host starts → player spawns → the existing Main Camera snaps
to the player and follows it.** Confirmed.

### 3.2 Current cameras and listeners

| GameObject | Camera | AudioListener |
| --- | --- | --- |
| Main Camera | yes | yes |

- **Exactly 1 Camera and 1 AudioListener** exist today.
- `Main Camera` settings: `ClearFlags = 1` (SolidColor), background
  `{0.192, 0.302, 0.475}`, `field of view = 60`, `near clip = 0.3`,
  `far clip = 1000`, `m_Depth = -1`, `m_TargetTexture = 0`.
- **`m_CullingMask.m_Bits = 4294967295`** → renders **Everything**, including the
  `Player` layer. So once the host player spawns it *will* be visible in the menu
  background. Confirmed; a `MenuPresentation` camera that excludes `Player` is
  genuinely required.

### 3.3 Character preview camera is runtime-only

`TitleScreenCharacterCreationManager.BuildPreview()` (line 777–811) creates at
runtime: a `RawImage`, a 512×512 `RenderTexture`, a preview `Camera`
(`fieldOfView = 50`, SolidColor, `cullingMask = 1 << LayerMask.NameToLayer("Player")`,
`targetTexture = previewTexture`) and a child Spot light. None of these are
serialized fields (`m_previewCamera`, `m_previewLight`, `m_previewTexture` are
plain private fields, lines 53–55).

---

## 4. `TitleScreenManager` serialized state

Component instance `!u!114 &689070890`, on **Title Screen Background**:

| Field | Value |
| --- | --- |
| `m_pressStartMenu` | **Press Start Button** (the button itself, not a container) |
| `m_mainMenu` | Title Screen Main Menu |
| `m_loadGameMenu` | Load Character Menu |
| `m_characterCreationManager` | **`{fileID: 0}` → null** ✔ |
| `m_startingClasses` | **`[]` → empty** (Knight + Ranger built at runtime) |
| `m_noFreeCharacterSlotsPopup` | No Free Character Slots Popup |
| `m_deleteCharacterSlotPopup` | Delete Character Slot Popup |
| `m_newGameButton` | `{fileID: 860510360}` |
| `m_loadGameButton` | `{fileID: 903986384}` |
| `m_loadGameReturnButton` | `{fileID: 1028317900}` |
| `m_noFreeSlotsCloseButton` | `{fileID: 268488086}` |
| `m_confirmDeleteButton` | `{fileID: 559406345}` |
| `m_characterSaveSlots` | 10 entries (Character Slot 01..10) |

**Not present** (to be added in later phases): `m_continueButton`,
`m_settingsButton`, `m_creditsButton`, `m_quitButton`, `m_settingsMenu`,
`m_creditsMenu`.

### Flow methods (`TitleScreenManager.cs`)

| Method | Line | Behaviour |
| --- | --- | --- |
| `Awake()` | 43 | resolves/adds `TitleScreenCharacterCreationManager`, then `ConfigureRuntime(this, m_newGameButton)` |
| `StartNetworkAsHost()` | 63 | host only, no menu state change |
| `PressStart()` | 71 | host → hide press start → show main menu → `m_newGameButton.Select()` |
| `StartNewGame()` | 86 | host check → `HasFreeCharacterSlot()` → open creation or popup |
| `ReturnFromCharacterCreation()` | 104 | closes creation, restores main menu focus |
| `OpenLoadGameMenu()` | 114 | shows slots, **always `m_loadGameReturnButton.Select()`** |
| `CloseLoadGameMenu()` | 131 | restores `m_loadGameButton` focus |
| `DeleteCharacterSlot()` | 192 | deletes, refreshes, selects return button |
| `CloseDeleteCharacterPopup()` | 211 | `RestoreLoadMenuSelection()` |
| `TryStartNetworkAsHost()` | 217 | `NetworkManager.Singleton.StartHost()` |
| `IsNetworkHostReady()` | 240 | requires `IsListening && IsServer` **and `WorldSaveGameManager.Instance`** |
| `EnsureDefaultStartingClasses()` | 309 | builds Knight + Ranger from `WorldItemDatabase` |

Confirmed: `PressStart()` currently selects **New Game** (line 80) — the "Continue
first" change belongs to Phase 8.

---

## 5. Character creation — runtime UI builder

`Assets/_Game/Scripts/UI/Frontend/TitleScreenCharacterCreationManager.cs` (998 lines)

### 5.1 All serialized fields already exist

`m_titleScreenManager`, `m_characterCreationRoot`, `m_creationOptions`,
`m_classMenu`, `m_hairMenu`, `m_hairColorMenu`, `m_nameMenu`,
`m_firstCreationButton`, `m_firstClassButton`, `m_firstHairButton`,
`m_firstHairColorButton`, `m_nameInputButton`, `m_characterNameInput`,
`m_characterNameValue`, `m_classValue`, `m_hairValue`, `m_sexValue`,
`m_hairColorValue`, `m_hairColorRedSlider/GreenSlider/BlueSlider`,
`m_previewRotator` (lines 17–44).

**Missing:** a serialized `RawImage` for the preview — matches your Phase 11 note.

### 5.2 `ConfigureRuntime` (line 116)

```csharp
m_titleScreenManager = titleScreenManager;
if (m_characterCreationRoot == null)
{
    BuildRuntimeInterface(styleSource);   // line 123
}
BindUIEvents();
```

`BuildRuntimeInterface` (599) creates the whole menu at runtime with hard-coded
rects — `new Vector2(-560f, -20f)`, `new Vector2(420f, 650f)`,
`new Vector2(-70f, 40f)`, `new Vector2(400f, 420f)` (lines 615–616 etc.) — then
`BuildClassMenu`, `BuildHairMenu`, `BuildHairColorMenu`, `BuildNameMenu`,
`BuildPreview`, and `AddComponent<TitleScreenPlayerPreviewRotator>()`.

### 5.3 The critical consequence

`BuildPreview()` (777) is called **only** from `BuildRuntimeInterface()` at line
663. There is no `EnsurePreviewInfrastructure()`.

Therefore, the moment `m_characterCreationRoot` is assigned (Phase 10 authored
UI), `BuildRuntimeInterface` is skipped, so `BuildPreview` never runs and the
preview camera / RenderTexture / spotlight are **never created** — silently.

This is stronger than "should be decoupled": **Phase 10 will break the preview
unless Phase 11's `EnsurePreviewInfrastructure()` is implemented first or with
it.** Recommended order: land `EnsurePreviewInfrastructure()` before touching the
authored UI, or ship them together.

### 5.4 Preview / network coupling

- `OpenCharacterCreation()` (130) → `PlayerInputManager.EnableMenuCameraInput()`
  → `StartCoroutine(ResolvePreviewPlayer())` (375).
- `ApplyPreviewLayer()` (831) sets the whole player hierarchy to the `Player`
  layer and caches originals; `RestorePreviewLayers()` (847) restores them.
- `CloseCharacterCreation()` (144) restores layers and calls
  `DisableMenuCameraInput()`.
- Host player is required as the preview target — consistent with your
  instruction not to delay `StartHost`.

---

## 6. Save system audit

`Assets/_Game/Scripts/World/Managers/WorldSaveGameManager.cs` (831 lines)

| Line | Fact |
| --- | --- |
| 321–330 | `Awake()` singleton guard; `Destroy(gameObject)` on duplicate |
| 332–336 | **`DontDestroyOnLoad(gameObject)` is in `Start()`, not `Awake()`** |
| 334 | `DontDestroyOnLoad` |
| 427–448 | `AttemptToCreateNewGame()`, `HasFreeCharacterSlot()`, `NewGame()` |
| 453 | `NewGame(CharacterSaveData)` — uses `TryFindFreeCharacterSlot` (first free) |
| 496 | `SaveGame()` |
| 527 | `LoadGame()` |
| 568 | `SelectCharacterSlot()` |
| 576 | `DeleteGame()` |
| 607 | `LoadAllCharacterSlots()` |
| 637 | `GetCharacterDataForSlot()` |

**Confirmed:** there is **no** `TryGetContinueSlot`, no `LastPlayedCharacterSlot`,
and no `PlayerPrefs` usage anywhere in the class. `SecondsPlayed` is accumulated
in `Update()` (line 349) and is a *total*, not a recency — so using its maximum as
"last played" would indeed be wrong.

Data versions in `CharacterSaveData.cs`: attribute v1, equipment v4, world loot
v5, focus points v6 (lines 19–22).

---

## 7. Input

`Assets/_Game/Scripts/Input/PlayerInputManager.cs` (930 lines)

| Line | Fact |
| --- | --- |
| 22–24 | `CameraInput`, `CameraVerticalInput`, `CameraHorizontalInput` |
| 57 | `m_isMenuCameraInputEnabled` |
| 68 | `DontDestroyOnLoad(gameObject)` |
| 296–300 | `EnableMenuCameraInput()` |
| 303–312 | `DisableMenuCameraInput()` |

`TitleScreenPlayerPreviewRotator` (32 lines) reads
`PlayerInputManager.Instance?.CameraInput.x` and rotates the preview target.
Behaviour is already correct and needs no change for the presentation work.

---

## 8. Layers

`ProjectSettings/TagManager.asset` — 18 layers defined (0–17):

```
0 Default        1 TransparentFX   2 Ignore Raycast  3 Water
4 UI             5 Player          6 Damage Collider 7 Damageable Character
8 Interactable   9 Projectile     10 Beacon         11 BeaconDetector
12 Slippery Default 13 Breakable Object 14 Broken Object 15 Event Trigger
16 StylizedOutlineHull 17 StylizedHighlight
```

- **`MenuPresentation` does not exist.** Layers **18–31 are free**.
- Note: the **`Player` layer is index 5** (not 8 — index 8 is `Interactable`).
  `BuildPreview` uses `1 << LayerMask.NameToLayer("Player")`, so this resolves
  correctly regardless.

---

## 9. Confirmations against your stated assumptions

| Your claim | Verdict |
| --- | --- |
| `PlayerCamera` is `DontDestroyOnLoad` and persistent | **Confirmed** (line 83) |
| Press Start → Player spawns → camera starts following | **Confirmed** (PlayerManager 148–156) |
| `m_characterCreationManager` is currently null | **Confirmed** (`{fileID: 0}`) |
| Character Creation UI is built by `ConfigureRuntime → BuildRuntimeInterface` | **Confirmed** (116 → 123 → 599) |
| Preview camera / RenderTexture / spotlight also come from the builder | **Confirmed**, and *only* from it |
| `WorldSaveGameManager` has no Last Played Slot | **Confirmed** (no PlayerPrefs, no such member) |
| Do not use max `SecondsPlayed` as Continue | **Confirmed** — it is an accumulated total (line 349) |
| Persistent managers are root GameObjects | **Confirmed** for PlayerCamera / PlayerInputManager / WorldGameSessionManager |
| `WorldSaveGameManager` is a root object | **Partly** — it is a root *PrefabInstance*, not a plain GameObject |
| Load Game always selects Return | **Confirmed** (line 125) |
| Press Start currently selects New Game | **Confirmed** (line 80) |

---

## 10. Deviations and risks found

1. **`WorldSaveGameManager` and `NetworkManager` live in prefab instances**, not
   plain scene GameObjects. Scene-specific references cannot be added to them
   without prefab overrides. Phase 7 (`TryGetContinueSlot`) should go into the
   script, not into scene wiring, so this is manageable — but any plan that
   assumes direct scene references on these two will not work as written.

2. **`EnsurePreviewInfrastructure()` is a prerequisite, not an optimisation.**
   See §5.3. Assigning authored UI without it silently removes the preview.

3. **`DontDestroyOnLoad` in `Start()`** for `WorldSaveGameManager` (line 334) is
   unusual. It works, but it means the object is destroyable for one frame after
   `Awake()`. Worth hardening if any phase touches scene-load ordering.

4. **`m_pressStartMenu` points at the `Press Start Button` itself**, not a
   container panel. `PressStart()` hides the button only. If Phase 5 wants a
   press-start *panel* (backdrop, logo, prompt styling), one must be authored and
   the reference repointed — the current structure cannot host it.

5. **No `Press Start Menu` container, no Settings, no Credits, no Quit** objects
   exist. Phases 8 and 12 are additive.

6. **`m_startingClasses` is empty**, so Knight/Ranger are constructed from
   `WorldItemDatabase` at runtime (lines 309–377). If Phase 10 authors the class
   menu in the scene, the class list must still resolve from this runtime path or
   the authored buttons will have no data.

7. **Single Camera + single AudioListener today.** Adding `CAM_MenuPresentation`
   introduces a second of each; the coordinator must disable both the gameplay
   `Camera` *and* its `AudioListener`, and must also account for URP Additional
   Camera Data and `Global Volume` (the volume already exists in the scene and
   will affect whichever camera renders it).

8. **`Global Volume` and `Directional Light` are scene objects** that currently
   light the title screen. Phase 2's `_MENU_PRESENTATION/Lighting` must not
   duplicate them into double-lighting; either reparent or disable.

---

## 11. Phase 0 sign-off

Audit complete. No files were modified.

Ready to proceed to **Phase 1 (Camera Isolation)** once you confirm. Phase 1 will
touch:

- new `Assets/_Game/Scripts/UI/Frontend/Presentation/TitleScreenCameraCoordinator.cs`
- new `CAM_MenuPresentation` object in `SCN_MainMenu`
- no changes to `PlayerCamera.cs`, `PlayerManager.cs` or any save script

Checkpoint A gate: after Press Start, with the host player spawned, the menu
camera must remain completely static.
