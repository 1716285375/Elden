# Project Architecture

## Runtime Script Organization

Runtime code lives under `Assets/_Game/Scripts`. Directories are organized by stable functional domain; class namespaces remain under `ZZ` and do not mirror every physical subfolder.

```text
Scripts/
|-- Abilities/          Character abilities and spells
|-- Characters/         Shared, player, and AI character behaviour
|-- Combat/             Actions, damage, effects, and projectiles
|-- Core/               Cross-domain bootstrap and infrastructure
|-- Dialogue/           Dialogue data and runtime flow
|-- Generated/          Tool-generated source; do not edit manually
|-- Input/              Input routing and input actions
|-- Items/              Item definitions grouped by item family
|   |-- Core/
|   |-- Equipment/
|   |-- Projectiles/
|   |-- QuickSlots/
|   |-- Shops/
|   |-- Upgrades/
|   `-- Weapons/
|-- Networking/         Cross-domain network spawning and coordination
|-- Rendering/          Render-pipeline extensions
|-- Save/               Save models, serialization, and persistence
|-- UI/
|   |-- Frontend/       Title screen and character creation
|   `-- Gameplay/       Character and player in-game UI
|-- Utilities/          Small, domain-independent helpers
|-- VFX/                Runtime visual-effects orchestration
`-- World/
    |-- AI/              World-level AI spawning and coordination
    |-- Debug/           Development-only world diagnostics
    |-- Environment/     Environment-specific behaviour
    |-- Interactions/    Interactables, doors, elevators, and traversal triggers
    |-- LevelDesign/     Runtime level-design helpers
    |-- Managers/        True world-scoped coordinators only
    |-- Objects/         Stateful world objects
    |-- Rendering/       World-specific rendering control
    `-- Streaming/       Scene, location, and streaming lifecycle
```

## Placement Rules

- Place a script with the feature that owns its behaviour, not with a caller that happens to use it.
- Keep `Managers` folders for long-lived coordinators; place interactables, data models, and leaf components in their domain folders.
- Put all runtime UI under `UI/Frontend` or `UI/Gameplay`; character scripts may reference UI but do not own its presentation code.
- Use PascalCase directory names without spaces so paths remain consistent across editor tooling and tests.
- Move Unity assets through `AssetDatabase` and preserve `.meta` GUIDs to keep scene and Prefab references intact.

## Assembly Boundaries

Most runtime scripts currently compile into `Assembly-CSharp`. `Save/ZZ.SaveSystem.asmdef` is the only first-party runtime assembly boundary. Add another assembly definition only when a measured compile-time or dependency-boundary need justifies it.
