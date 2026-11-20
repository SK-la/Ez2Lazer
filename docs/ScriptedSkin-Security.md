# Scripted skin security model

Ez2Lazer scripted skins (`.csx` under `EzResources/ScriptedSkin/`) are **trusted local code** running in the **same process** as the game, with the game user's privileges. There is no sandbox.

## What is enforced today

- Scripted skins are opt-in. `EnableScriptedSkins` defaults to off. While off, the `ScriptedSkin` directory is neither scanned nor watched, and no script is compiled. Turning it on requires a restart.
- The skin settings note warns that an enabled script is full-trust C#: it can read and write files, start processes, and use the network.
- Catalog listing only reads directory names. A script is compiled (and its static `CreateInfo()` invoked) when the skin is first selected, not at startup. Still, avoid side effects in constructors/`CreateInfo()`.

## What is **not** enforced

- Scripts are compiled with Roslyn and loaded with `Assembly.Load` in the game process. Source text is not checked for forbidden APIs.
- Referenced assemblies include the game and the `System.*` / `Microsoft.*` closure needed to compile normal scripts, so scripts can call ordinary .NET APIs, including file, process, and network APIs.
- A directory resource store is passed in for skin assets. It does not stop a script from reaching the rest of the filesystem through other APIs.
- Referenced game assemblies expose game APIs (graphics, audio, configuration, Mania HUD, and so on).

## User expectations

- Only install scripted skins from sources you trust, same as running arbitrary executables.
- Scripted skins do **not** use Realm file storage; layout JSON and assets live next to the `.csx` file.
- Do not use Skin Editor **Edit externally** (Realm import) on scripted skins — use **Open script folder** instead.
