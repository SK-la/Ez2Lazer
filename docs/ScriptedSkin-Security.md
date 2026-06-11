# Scripted skin security model

Ez2Lazer scripted skins (`.csx` under `EzResources/ScriptedSkin/`) are **trusted local code** running in the **same process** as the game.

## What is enforced today

- Source-level substring checks block obvious uses of `System.IO`, `System.Net`, reflection, `unsafe`, P/Invoke, etc. (see `SandboxedScriptRunner.validateScriptSafety`).
- `SafeMetadataResolver` exists for optional `#r` directive restrictions but is not yet wired into compilation (doing so requires composing with the default resolver).
- Script resource access is limited to files in the skin's directory (via `NativeStorage` fallback store).

## What is **not** enforced

- Scripts are compiled with Roslyn and loaded with `Assembly.Load` in the game AppDomain. This is **not** a sandbox.
- Blacklist checks are trivially bypassed (`global::System.IO.File`, type aliases, indirect calls).
- Referenced game assemblies expose full game APIs (graphics, audio, configuration, Mania HUD, etc.).
- `LoadScriptInfoAsync` may invoke static `CreateInfo()` during catalog scan; avoid side effects in constructors when possible.

## User expectations

- Only install scripted skins from sources you trust, same as running arbitrary executables.
- Scripted skins do **not** use Realm file storage; layout JSON and assets live next to the `.csx` file.
- Do not use Skin Editor **Edit externally** (Realm import) on scripted skins — use **Open script folder** instead.
