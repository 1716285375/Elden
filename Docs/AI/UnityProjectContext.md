# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:/C/Game/Unity/Elden`
- Last analyzed: 2026-08-18
- Last analyzed commit: `ff55bf7` (working tree contained pre-existing untracked animation assets)

## Confirmed Environment

- Unity version: 6000.3.11f1 (revision `3000ef702840`)
- Render pipeline: Universal Render Pipeline 17.3.0
- Input system: Unity Input System 1.19.0; `activeInputHandler: 1`
- Target platforms: not established by this inspection

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP 17.3.0 | Confirmed | `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset` |
| Input | Input System 1.19.0 | Confirmed | `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset` |
| Multiplayer | Netcode for GameObjects 2.13.0 and Unity Transport 2.6.0 | Confirmed | `Packages/manifest.json`, `Packages/packages-lock.json`, scripts under `Assets/Script` |
| Navigation | AI Navigation 2.0.11 | Confirmed | `Packages/manifest.json` |
| Testing | Unity Test Framework 1.6.0 installed | Confirmed | `Packages/manifest.json` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Art` | Models, textures, materials, fonts, UI sprites, and animations | Confirmed | Directory contents |
| `Assets/Script/Character` | Character, player, locomotion, camera, and networking behaviours | Confirmed | C# scripts |
| `Assets/Script/World Managers` | Persistent input/save/scene-flow managers | Confirmed | C# scripts |
| `Assets/Scenes` | Main menu and world scenes | Confirmed | Scene files and Build Settings |
| `Docs/ArtRecovery/Nephilite` | Recovered-art provenance and dependency manifests | Confirmed | Recovery workflow output |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Assembly-CSharp` | First-party runtime gameplay | UnityEngine, Input System, Netcode for GameObjects | No first-party `.asmdef` files found |

## Scenes And Startup Flow

- Build scenes: `Assets/Scenes/Scene_Main_Menu_01.unity`, then `Assets/Scenes/Scene_World_01.unity`
- Likely startup scene: `Scene_Main_Menu_01`
- Scene loading flow: `TitleScreenManager` starts host/new game; `WorldSaveGameManager` loads `Scene_World_01`, using Netcode scene management when networking is active.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Unity component architecture | Gameplay uses `MonoBehaviour` and `NetworkBehaviour` components | Confirmed | `Assets/Script/**/*.cs` |
| Persistent managers | Input and save/scene flow use singleton-style managers with `DontDestroyOnLoad` | Confirmed | `PlayerInputManager.cs`, `WorldSaveGameManager.cs` |
| Networked character base | Character state is split between manager, locomotion, and network manager components | Confirmed | `Assets/Script/Character` |

## Coding Conventions

- Namespace style: first-party code uses `ZZ`.
- Serialized fields: private fields generally use `[SerializeField]`; public runtime references are also present.
- Async: scene loading uses coroutines and Unity async operations.
- Comments/docs: concise implementation code; no project-wide XML documentation requirement observed.

## Testing And Validation

- EditMode tests: none found under `Assets`.
- PlayMode tests: none found under `Assets`.
- CI/build validation: not established.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity Skills package | available in project | Local `com.besty.unity-skills` dependency in `Packages/manifest.json` |
| Connected Unity Editor | unavailable during analysis | No Unity Editor process was running |
| AssetDatabase refresh/import | deferred | Recovered files are copied externally and will import on the next Editor launch |
| Tests/profiler/play mode | unverified | Not invoked for this asset-recovery task |

## Important Constraints

- Recovered source game used Unity 6000.2.12f1 IL2CPP; this project uses Unity 6000.3.11f1.
- AssetStudio OBJ exports do not restore original FBX rigs, skin weights, prefab hierarchy, or importer settings.
- Recovered `.anim` files and material JSON are evidence-rich exports, but material JSON is not a native Unity `.mat` file.
- IL2CPP decompilation restores types and fields but generally not managed method bodies.
- Use recovered content only within the user's applicable ownership/licensing rights.

## Unknowns And Confidence

- Original AssetDatabase GUIDs and original folder hierarchy are unavailable; PathID and serialized source file are the strongest recovered identities.
- Custom MonoBehaviour payloads are often reduced to base fields, so exact prefab/scene wiring is incomplete.
- Import-time disk expansion is unknown and may be substantial for thousands of textures, meshes, and animations.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- Representative scripts under `Assets/Script`
- `F:/MyProject/Game/RE-Assets/Nephilite-Demo/README.md`
- `F:/MyProject/Game/RE-Code/Nephilite-Demo/README.md`

<!-- unity-onboarding:generated:end -->

## Coding Standards

The authoritative coding standards for this repository live in [`CLAUDE.md`](../../CLAUDE.md), and [`AGENTS.md`](../../AGENTS.md) points every agent (Claude Code, Codex, pi, etc.) to it as the single source of truth. The auto-generated `Coding Conventions` section above is informational only and is superseded by `CLAUDE.md`.

Current conventions in effect (as of the last cleanup pass):

- Private instance fields use the `m_` prefix, private static fields `s_`, and constants `k_PascalCase`.
- Inspector-visible data uses `[SerializeField] private`; cross-component references are exposed through read-only properties rather than `public` fields (for example `CharacterManager.CharacterNetworkManager`).
- Renamed serialized fields carry `[FormerlySerializedAs]` so existing Prefab/Scene data survives.
- Line endings are LF (enforced by `.editorconfig`), and brace style is Allman with braces on all control-flow bodies.

