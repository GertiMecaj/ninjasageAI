# NinjaSage AI — live enemy-skip switch

Windows launcher with a live **OFF / ON** switch for the exact Adobe AIR game supplied in NinjaSage.rar. Every session starts **OFF**, with original enemy decision code running. Switch **ON** to skip enemy-side turns in the main battle system; switch **OFF** to resume original decisions without restarting the game.

## Use

1. Extract the download. Keep `NinjaSageAI.exe` and `NinjaSageHook.dll` together.
2. Close any previously launched game instance. Open this launcher and browse to the extracted game's `Ninja Sage.exe`.
3. Click **Launch game (starts OFF)**. The launcher loads runtime control support, with enemy skipping disabled.
4. After the game opens its patched SWF, **Enemy skip: OFF** becomes clickable.
5. Click it to turn **ON**, then click it again for **OFF**. The game stays open. The change is checked at the next enemy decision; an attack or animation already underway is not cancelled.
6. Keep the launcher open while using the switch. Closing it resets OFF; the injected controller also treats a terminated launcher as OFF.

A game started directly, or through the old skip-only launcher, must be restarted through this version once. Attaching to an arbitrary already-running AVM2 instance is not implemented. The game files are not overwritten. No administrator privileges or .NET installation are required for the self-contained download. For the launcher's own folder, use a path representable in the Windows system code page, such as `C:\NinjaSageAI`, because Microsoft Detours takes an ANSI DLL path.

## Scope and status

This is **skip-only** mode, not charge. It targets `Combat.Battle` dispatch for enemy-side enemies, NPCs, pets and AI-controlled characters. Live PvP has a separate system and is not patched. Passive damage, counters and damage-over-time outside turn selection are not removed.

The UI distinguishes the requested switch state from a game-side switch read. “Game checked the switch” confirms a native control query; it does not independently prove a particular battle outcome. In-game verification of all enemy types and game modes is still required.

## Implementation

- Exact source SWF SHA-256: `d9c550cfdd3de9ce63e2d9b5a81bf3535c8440189c9a7c1abbb4b9923952c53d`. Unknown builds are rejected.
- A separate SWF under `%LOCALAPPDATA%\NinjaSageAI\<session>` contains a conditional guard at three dispatchers: `setActionsAvailable`, `handleNonControllableAttacker`, and `handleSkipTurns`.
- The guard checks the actor's enemy-team membership, then freshly resolves `File.applicationDirectory.resolvePath(".ninjasage-ai-control").exists`. OFF falls through to the original bytecode. ON calls the existing `agility_bar_manager.startRun()` skip path and returns.
- No physical control file is created. The injected DLL supplies that exact virtual path's existence via `GetFileAttributesW/A` and `GetFileAttributesExW/A`, backed by a session-specific named Windows event. Different sessions have independent switches. The controller also checks whether the launcher process remains alive.
- DLL injection happens once, before AIR loads the main SWF. Read-only opens of the exact main SWF path are redirected to the prepared copy. Toggling changes the shared event; it does not repeatedly patch files, rewrite JIT code, or reinject the DLL.
- The original game files and other processes are unaffected by this tool. Temporary test copies and logs can be deleted after closing the relevant game session.

## Validation

The profile generator reparses the complete patched ABC, including appended string and QName constants and updated SWF/DoABC/method lengths. Tests execute the shipped guard bytes for OFF → ON → OFF transitions across enemy and non-enemy teams, check stack bounds, and verify preservation of the original instruction stream.

Windows CI compiles the DLL and self-contained app; tests actual injection and ANSI/Unicode SWF redirection; and drives the packaged launcher's real native bridge through OFF → ON → OFF in the **same running process**, checking all four file-attribute APIs and preserving the source file. It also renders the interface. These tests use a synthetic host, not a logged-in game session.

## Build

See `.github/workflows/build.yml`: MSVC x86, Microsoft Detours v4.0.1 (MIT), and .NET 8 Windows Forms. The binary package includes the Detours license. `tools/make_profile.py <original.swf> app/profile.json` regenerates this exact-build profile; it is not a generic patcher for future game versions.
