# Unity C# Project Coding Standards

This file defines the shared development conventions for this repository. It applies whenever Claude Code analyzes code, implements features, fixes defects, refactors, or performs code reviews. Readability, correctness, maintainability, and the smallest necessary change take priority.

## Instruction Priority

- Explicit requirements in the user's current task take precedence over this file.
- Rules in a more specific directory's `CLAUDE.md` or `.claude/rules/` take precedence over this file.
- The repository's `.editorconfig`, compiler configuration, and established local conventions take precedence over the general guidance in this file.
- When rules conflict or a requirement is materially ambiguous, explain the conflict and ask for confirmation. Do not guess critical business behavior.

## Working Principles

- Understand the problem and its root cause before changing code. Do not hide real defects behind null checks, swallowed exceptions, or special-case branches.
- Follow KISS: choose the simplest solution that correctly solves the current problem.
- Follow YAGNI: do not add abstractions, extension points, or configuration for unconfirmed future requirements.
- Follow DRY: remove duplicated business logic; similar-looking text alone does not always justify an abstraction.
- Keep changes within the smallest scope required to complete the task. Do not rewrite unrelated code opportunistically.
- Prefer existing Unity, .NET, and project APIs over reimplementing mature functionality.
- Stop when the code meets project standards. Do not perform "perfection" refactors without a clear benefit.

## Before Making Changes

- Read the relevant implementation, callers, tests, interfaces, data models, and configuration to establish the change boundary.
- Check the current directory and its parents for `CLAUDE.md`, `.editorconfig`, and project documentation.
- Search the repository for similar implementations and follow the existing architecture, naming, error handling, and testing patterns.
- Treat `ProjectSettings/ProjectVersion.txt` as the source of truth for the Unity version; use `Packages/manifest.json` and lock files for package versions.
- Do not change public APIs, serialized field names, asset GUIDs, scenes, or Prefab data structures unless the task authorizes it.

## Unity File Safety

- Do not edit Unity-generated directories: `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, or `Builds/`.
- Do not manually edit Unity-generated `.csproj` or `.sln` files. Persist settings through Unity project configuration or generation rules.
- Do not modify `Packages/PackageCache/`. Change packages through `Packages/manifest.json` or the project's established workflow.
- When moving, renaming, or deleting assets under `Assets/`, preserve or move the corresponding `.meta` files and never change existing GUIDs.
- Modify scenes, Prefabs, materials, and other serialized assets only when the task requires it, and avoid unrelated YAML reordering.
- Generated code, third-party source, and vendor directories are read-only by default. Explain the reason and alternatives before modifying them.

## Naming Conventions

- Use `PascalCase` for classes, structs, delegates, methods, properties, events, and public fields.
- Prefix interfaces with `I` and use `PascalCase`, for example `IDamageable`.
- Use `camelCase` for local variables and method parameters.
- Use `m_camelCase` for private instance fields.
- Use `s_camelCase` for private static fields.
- Use `k_PascalCase` for constants.
- Use clear nouns for variables and fields; use verbs or verb phrases for methods.
- Prefix Boolean variables and methods returning `bool` with question-like terms such as `Is`, `Has`, `Can`, or `Should`.
- Use singular nouns for ordinary enums and plural nouns for bit-flag enums marked with `[Flags]`.
- Names must be readable, searchable, and pronounceable. Include units when needed, for example `CooldownSeconds`.
- Do not use single-letter names or unclear abbreviations except for loop indices and mathematical expressions.
- Do not use special symbols, unnecessary Unicode characters, jokes, or puns in identifiers.
- Do not repeat context already supplied by the class name: prefer `Player.Score` over `Player.PlayerScore`.

## Files and Types

- Each source file contains one primary type.
- A file containing a `MonoBehaviour` or `ScriptableObject` must exactly match the type name.
- Define only one `MonoBehaviour` per file by default, except for tightly related private nested types.
- Use `PascalCase` namespaces without underscores or special characters.
- Namespaces represent stable functional domains. Continue following any existing convention that maps namespaces to directories.

## Formatting

- Use Allman brace style: every opening brace appears on a new line.
- Indent with four spaces, never tabs, unless `.editorconfig` specifies otherwise.
- Always use braces for `if`, `else`, `for`, `foreach`, `while`, `switch`, and similar statements, including one-line bodies.
- Declare one variable per line.
- Use one space after commas and around operators.
- Do not put a space between a method name and its opening parenthesis, or inside parentheses and brackets.
- Target a maximum line width of 120 characters. Split long expressions by logical structure instead of relying on horizontal scrolling.
- Do not use fragile column alignment with repeated spaces unless it materially improves bit flags or tabular data.
- Keep related members and methods adjacent. Separate different responsibilities with blank lines without excessive vertical spacing.
- Do not use `#region` to hide bloated classes; split responsibilities instead. Preserve local consistency in files that already use regions extensively.
- Every `switch` includes an explicit `default` case, or uses a verifiable exhaustive construct when no default is possible.

## Class Member Order

Order class members as follows:

1. Constants and static fields.
2. Serialized fields and instance fields.
3. Properties.
4. Events and delegates.
5. Unity lifecycle methods.
6. Public methods.
7. Protected methods.
8. Private methods.
9. Nested types.

- Organize classes top-down using the newspaper structure: high-level entry points first, implementation details later.
- Each class has one clear responsibility. Split classes that simultaneously handle input, data, movement, audio, or other unrelated concerns.
- Prefer composition over unnecessary inheritance. A new abstraction requires at least two real use cases or an explicit architectural requirement.

## Method Design

- A method performs one action or answers one question. Its name accurately describes all observable behavior.
- Keep methods small. Extract meaningful private methods when logic contains too many levels of abstraction.
- Minimize parameter counts. Group parameters that always travel together into a clearly named data type when appropriate.
- Do not use Boolean flags to make one method perform two behaviors. Create two methods with explicit intent instead.
- Avoid unnecessary `ref`, `out`, global state mutation, and other hidden side effects.
- Keep only the overloads required by real call sites, and make each signature easy to distinguish.
- Use `var` when the type is obvious from context; use an explicit type when the returned type or intent is unclear.
- Do not create empty `Update`, `FixedUpdate`, `LateUpdate`, or other Unity lifecycle methods.
- Do not repeat expensive lookups, allocations, or string concatenation in per-frame hot paths. Cache references according to the existing architecture.

## Properties and Encapsulation

- Keep data private by default and expose only the smallest interface callers actually need.
- Use expression-bodied syntax for single-line read-only properties: `public int MaxHealth => m_maxHealth;`.
- Use properties for simple reads and writes. Use methods for complex computation, I/O, or visible side effects.
- Keep setters `private` by default unless external mutation is part of the type's contract.
- Do not mechanically wrap every field in meaningless getters and setters.

## Unity Serialization

- For Inspector-visible data, prefer `[SerializeField] private` instead of making fields `public` solely for display.
- Use `[Min]` or `[Range]` when numeric values have valid limits, while retaining necessary runtime boundary validation.
- Use `[Tooltip]` when an Inspector field needs explanation instead of relying only on source comments.
- Group related data into `[Serializable]` classes or structs when this makes the Inspector clearer.
- Do not casually rename released serialized fields. When a rename is necessary, use a Unity-supported migration path and validate existing assets.
- Do not rely on redundant default field initialization. Initialize explicitly only when the value has business meaning or differs from the type default.

## Events

- Prefer `System.Action` or the project's existing event mechanism for gameplay events.
- Name events with verb phrases describing a state change: use a present participle before an action, such as `OpeningDoor`, and a past participle after it, such as `DoorOpened`.
- Prefix event-raising methods with `On`, for example `OnDoorOpened()`.
- Use null-conditional invocation: `DoorOpened?.Invoke();`.
- Create custom event argument types only when several related values genuinely need to travel together.
- Subscriptions and unsubscriptions occur in matching lifecycle points to prevent duplicate subscriptions and stale references.

## Comments and Documentation

- Comments explain why, not what the code already says.
- Refactor hard-to-explain code before deciding whether a comment is still necessary.
- Use `// ` for ordinary comments, place them above the relevant code, and keep them concise.
- Use XML `<summary>` documentation for public APIs or non-obvious contracts, including responsibilities, constraints, and side effects.
- Do not commit commented-out code, decorative comment blocks, personal attribution, or development journals. Version control preserves history.
- Every `TODO` is specific, actionable, and linked to an owner or issue. Remove stale or unscheduled TODOs.

## UI Toolkit

- Use BEM and kebab-case for UXML and USS class names: `block__element--modifier`.
- Names describe semantics and state, not volatile colors, sizes, or control types.
- Use `block__element` when an element depends on its block. A standalone generic component may omit the block name.
- Add relevant USS classes with `AddToClassList()` when constructing visual elements.
- Use consistent semantic naming across UI code, sprites, textures, and related assets.

## Testing and Verification

- Establish a reliable reproduction before fixing a defect. Add a failing regression test first when automation is appropriate.
- New features cover the critical success path, boundary conditions, and explicit failure paths.
- Run the Edit Mode or Play Mode tests closest to the change before the project's full required checks.
- Use only test, build, and formatting commands already defined by the repository. Never claim tests passed when no reliable command exists.
- When Unity cannot run, a license is unavailable, or the environment restricts testing, report exactly what remains unverified and why.
- Refactoring must not change externally observable behavior. Rerun relevant tests after each verifiable step.
- Before completion, check compiler errors, warnings, null-reference risks, serialization compatibility, event lifecycles, and per-frame performance impact.

## Completing a Task

- Summarize the files and behavior actually changed without overstating incomplete work.
- List the tests or verification steps performed and their results.
- State any checks not run, remaining risks, and steps that require manual verification in the Unity Editor.
- Do not commit, push, publish, or rewrite version history unless the user explicitly requests it.
