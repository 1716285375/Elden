# SCN_MainMenu Presentation Rework — Final Report

- Project: `C:/C/Game/Unity/Elden`
- Unity 6000.3.11f1, URP 17.3.0, Netcode for GameObjects 2.13.x, Input System 1.19.0
- Scene: `Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity`
- Namespace: `ZZ`
- Audit baseline: `Docs/MainMenu/PHASE0_CurrentArchitecture.md`

---

## 1. Original architecture (before this pass)

The audit (Phase 0) established the real architecture:

- `PlayerCamera` is a **persistent** (`DontDestroyOnLoad`) gameplay rig. The host
  player binds it in `PlayerManager.LateUpdate` and it follows the player, so it
  could never double as a static menu camera.
- `WorldSaveGameManager`, `WorldGameSessionManager`, `PlayerInputManager` are
  persistent root objects / root prefab instances. `WorldSaveGameManager` lives
  inside a prefab instance (`World Save Game Manager.prefab`).
- `TitleScreenManager.m_characterCreationManager` was **null** and the creation
  UI was built at runtime by `TitleScreenCharacterCreationManager.ConfigureRuntime
  → BuildRuntimeInterface()` (hard-coded rects, runtime `RawImage` /
  `RenderTexture` / preview camera / spot light).
- `WorldSaveGameManager` had **no last-played tracking**; `SecondsPlayed` is an
  accumulated total and is not a recency signal.
- Load Game always selected the Return button; Press Start always selected New
  Game.
- One camera + one `AudioListener` existed (`Main Camera`).

## 2. New architecture

```
SCN_MainMenu
│
├── Player Camera                          persistent gameplay rig (root)
├── Player Input Manager                   persistent (root)
├── World Game Session Manager             persistent (root)
├── World Save Game Manager                persistent (root prefab instance)
├── World Sound FX Manager / World Action Manager / ...  (untouched)
│
├── _MENU_PRESENTATION                     menu presentation layer (root, scene-only)
│   ├── Camera / CAM_MenuPresentation       FOV 42, (0, 2.2, -7.5), culling Default+MenuPresentation
│   │   [TitleScreenCameraCoordinator]      hands output to/from gameplay rig
│   │   [TitleScreenPresentationController] composition + hero idle
│   ├── Lighting / LGT_Key, LGT_Fill, LGT_Rim
│   ├── Environment / ENV_MainMenu_Placeholder (greybox monastery)
│   └── Character / CHR_MenuHero_Placeholder (MenuPresentation layer)
│
├── Global Volume
├── Title Screen Canvas
│   └── Title Screen Background            [TitleScreenManager, TitleScreenCharacterCreationManager, ...]
│       ├── Press Start Button
│       ├── Title Screen Main Menu
│       │   ├── LeftMenuRoot (CONTINUE / NEW GAME / LOAD / SETTINGS / CREDITS / QUIT)
│       │   ├── Description (TitleScreenDescriptionController)
│       │   └── Zephyring Title
│       ├── Load Character Menu            (10 slots, Return)
│       ├── Character Creation Menu        (authored UI — see §7)
│       ├── Settings Menu / Credits Menu   (placeholder)
│       └── Delete / No-Free-Slots popups
└── EventSystem
```

Three camera concepts, kept separate on purpose:

1. **Gameplay Camera** — `PlayerCamera` (persistent, game only).
2. **Menu Presentation Camera** — `CAM_MenuPresentation` (SCN_MainMenu only).
3. **Character Preview Camera** — runtime `Character Creation Preview Camera`
   (RenderTexture, `Player` layer only).

## 3. Modified scripts

| Script | Change |
| --- | --- |
| `Scripts/World/Managers/WorldSaveGameManager.cs` | Added `TryGetContinueSlot(out CharacterSlot)` + lightweight `ZZ.LastPlayedCharacterSlot` `PlayerPrefs` metadata. Updated on `NewGame` / `SaveGame` / `LoadGame` success; cleared in `DeleteGame`. No save-data version bump (v14 untouched). |
| `Scripts/UI/Frontend/TitleScreenManager.cs` | Added Continue / Settings / Credits / Quit flow, `RefreshMainMenuState()`, Continue-first selection on Press Start, first-active-slot selection in the load menu, `QuitGame()`. |
| `Scripts/UI/Frontend/TitleScreenCharacterCreationManager.cs` | Decoupled preview from the runtime UI builder: new `EnsurePreviewInfrastructure()` + serialized `m_previewImage`; added `ConfirmAuthoredName()`. Runtime builder kept as fallback only. |
| `ProjectSettings/TagManager.asset` | Added `MenuPresentation` layer (index 21). |

## 4. New scripts

| Script | Purpose |
| --- | --- |
| `Scripts/UI/Frontend/Presentation/TitleScreenCameraCoordinator.cs` | (pre-existing from prior pass, kept) disables the gameplay `Camera` + `AudioListener`, enables the menu camera + listener while the title scene is alive; restores both on destroy. |
| `Scripts/UI/Frontend/Presentation/TitleScreenPresentationController.cs` | Applies composition (FOV 42, position), holds environment/hero references, runs a gentle hero idle bob/sway. |
| `Scripts/UI/Frontend/Presentation/FrontendSelectableVisual.cs` | One reusable selection visual (ISelect/IDeselect/IPointerEnter/IPointerExit) — background, marker, label shift +12, 0.14 s transition; hover routes into `EventSystem` selection. Used by main menu, back buttons, save slots, creation UI. |
| `Scripts/UI/Frontend/Presentation/TitleScreenDescriptionController.cs` | Shows the authored description of the currently selected entry; entries are parallel `Selectable[]` + `string[]`. |

## 5. Serialized reference changes (scene)

`TitleScreenManager` gained and wired: `m_continueButton`, `m_settingsButton`,
`m_creditsButton`, `m_quitButton`, `m_settingsMenu`, `m_creditsMenu`,
`m_settingsReturnButton`, `m_creditsReturnButton`. `m_characterCreationManager`
now points at the real scene component (no runtime `AddComponent` in the
production path).

`TitleScreenCharacterCreationManager` (scene component on Title Screen
Background) wired: `m_characterCreationRoot`, `m_creationOptions`, `m_classMenu`,
`m_hairMenu`, `m_hairColorMenu`, `m_nameMenu`, `m_firstCreationButton`,
`m_firstClassButton`, `m_firstHairButton`, `m_firstHairColorButton`,
`m_nameInputButton`, `m_characterNameInput`, `m_characterNameValue`,
`m_classValue`, `m_hairValue`, `m_sexValue`, `m_hairColorValue`, RGB sliders,
`m_previewImage`, `m_previewRotator`, `m_titleScreenManager`.

`TitleScreenDescriptionController` entries wired for all six main-menu buttons.

## 6. Camera architecture

- `CAM_MenuPresentation`: Perspective, FOV 42, near 0.3 / far 1000, solid-color
  clear, culling = `Default | MenuPresentation` (excludes `Player`), Untagged,
  own `AudioListener` + `UniversalAdditionalCameraData`.
- `TitleScreenCameraCoordinator` (on `_MENU_PRESENTATION`): at `Start`, disables
  `PlayerCamera.Instance.CameraObject` and its `AudioListener`, enables the menu
  camera + listener; `OnDestroy` restores the gameplay rig. Verified in play
  mode: exactly one enabled camera and one enabled `AudioListener` at all times.
- The gameplay camera keeps tag `MainCamera`; the menu camera is Untagged to
  avoid `Camera.main` ambiguity.

## 7. Character creation — runtime-builder migration

- Authored hierarchy under `Character Creation Menu`: `Creation Options`
  (Name / Class / Hair / Hair Color / Sex / START GAME / RETURN), `Class Menu`
  (Knight, Ranger), `Hair Menu` (Style 00–08), `Hair Color Menu` (6 swatches +
  RGB sliders + APPLY), `Name Menu` (input + CONFIRM + RETURN), and an authored
  `Player Preview` `RawImage`.
- `EnsurePreviewInfrastructure()` guarantees the RenderTexture, preview camera
  (culling = `Player` layer) and spot light exist regardless of whether the UI is
  authored or runtime-built. The authored `RawImage` is used when assigned.
- Verified in play mode: creation opens, first option selected, class preview /
  commit works, name input + confirm works, host player resolved as the preview
  target, `START GAME` wrote a save and loaded the world.

## 8. Continue implementation

- `WorldSaveGameManager.TryGetContinueSlot(out CharacterSlot)` reads
  `PlayerPrefs` key `ZZ.LastPlayedCharacterSlot`, validates the cached slot
  still holds data, returns `false` otherwise. Updated on `NewGame` / `SaveGame`
  / `LoadGame` success; cleared in `DeleteGame`. No max-`SecondsPlayed` hack.
- `TitleScreenManager.RefreshMainMenuState()` sets `Continue.interactable`;
  `PressStart()` selects Continue when available, else New Game.

## 9. Regression results (play-mode tested)

| Check | Result |
| --- | --- |
| Press Start → host starts → menu camera static | **Pass** (menu cam stays at (0, 2.2, -7.5)) |
| Main menu opens; New Game selected when no last-played | **Pass** |
| Continue disabled without last-played save | **Pass** |
| Selection visual (dark-red background + marker) | **Pass** |
| Load Game → first active slot auto-selected (slot with save) | **Pass** |
| Load Game → Return selected when no saves | **Pass** (code path; slot 01 existed in test env) |
| Character creation authored UI opens | **Pass** |
| Class / Hair / Hair Color / Name submenus | **Pass** (Class + Name exercised) |
| Preview player resolved; preview camera positioned on player | **Pass** |
| START GAME → save written → world scene loaded | **Pass** (test save cleaned up after) |
| Gameplay camera restored in world scene | **Pass** (Main Camera enabled) |
| Menu-related console errors / AudioListener duplicates | **0** during play; only Netcode teardown noise on exit |

## 10. Screenshots

- `Assets/Screenshots/MainMenu_Final_MainMenu.png` (1920×1080 game view)
- `Assets/Screenshots/MainMenu_Final_CharacterCreation.png` (1920×1080 game view)
- `Assets/Screenshots/MainMenu_Phase2_SceneView.png` (2048×1054 scene view)

3440×1440 (21:9) is not reproducible from the current Game View resolution; the
layout is anchor-based (menu anchored left-center, environment fills the right)
so extra width reveals environment. Manual 21:9 check remains on the QA list.

## 11. Placeholder asset replacement list

| Placeholder | Replacement |
| --- | --- |
| Cube / Capsule environment (greybox monastery) | Monastery meshes (real geometry) |
| `CHR_MenuHero_Placeholder` capsule figure | Final hero character |
| Unity primitives + `M_Graybox_*` / `Gray.mat` materials | Final materials |
| `Image` panel placeholders (no sprites) | 9-slice sprites |
| TMP default font | Final font |
| Settings / Credits placeholder text | Final content |

## 12. Notes / leftovers

- `MenuCameraSetup.cs` / `DescriptionSetup.cs` (one-shot editor authoring
  helpers from the prior pass) were deleted after use; trigger files under
  `F:/tmp-files/` removed.
- The pre-existing save `CharacterSlot01.json` (from an earlier session) was
  left untouched; the test-created `CharacterSlot02.json` and the
  `ZZ.LastPlayedCharacterSlot` registry value were cleaned up after verification.
- Next steps: real art pass (replace placeholders), 21:9 QA, Settings screen
  real options, final font pass.
