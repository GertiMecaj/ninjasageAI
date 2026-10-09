# NinjaSage AI test launcher

Windows x86 launcher and injected DLL for the exact Adobe AIR build supplied in NinjaSage.rar. **Skip-only mode**: the main battle system's enemy-side actors advance the turn without selecting an attack. This covers the shared dispatcher for enemies, NPCs, pets and AI characters; actual coverage of each game mode requires in-game validation. Live PvP uses a separate system and is not patched. Charge is not implemented, because a uniform charge action is not established for all actor types.

## Run

1. Download the `NinjaSageAI-Windows-x86` artifact from this repository's successful Actions run and extract both ZIP layers if GitHub wraps the artifact.
2. Keep `NinjaSageAI.exe` and `NinjaSageHook.dll` together. Use a path representable in the Windows system code page (for example `C:\NinjaSageAI`); Detours' DLL path parameter is ANSI.
3. Extract the original game archive and close existing game instances.
4. Open `NinjaSageAI.exe`, browse to `Ninja Sage.exe`, and click **Launch with enemy skip**.
5. Wait for **DLL hook installed** and **Patched SWF opened by the game**. These confirm injection and redirection, not the outcome of a battle. Test a normal enemy, boss, NPC and pet turn in your test environment.
6. Close the game and click **Launch normal** to return to the original behavior.

No Python, Visual Studio, .NET installation, administrator privileges or manual injector is required for the packaged self-contained app. The app launches a new process with the DLL before the AIR runtime loads the SWF. Attaching to an already-running game is intentionally unsupported: AVM2 may have already compiled the original bytecode.

## How it works

- SHA-256 allowlist: `d9c550cfdd3de9ce63e2d9b5a81bf3535c8440189c9a7c1abbb4b9923952c53d`.
- A private patched SWF is created under `%LOCALAPPDATA%\NinjaSageAI\<session>`; the original is never overwritten by this tool.
- Microsoft Detours injects the native x86 DLL at process startup. The DLL redirects read-only `CreateFileW/A` opens of the exact original SWF path to the private copy. Other files, write operations and other processes are unaffected.
- AVM2 guards are prepended to `Combat.Battle.setActionsAvailable`, `handleNonControllableAttacker` and `handleSkipTurns`. If `attacker_model.getPlayerTeam() == "enemy"`, the guard calls `agility_bar_manager.startRun()` and returns. Original code executes for other teams. Existing relative branches remain valid; methods have no exception tables. SWF/DoABC/method lengths are rebuilt.
- Damage-over-time, passive effects and counters outside these dispatchers are not removed. This is a turn-selection patch, not invulnerability.
- New game versions are rejected. No assets, credentials, proprietary game binaries or account data are uploaded to this repository.

## Validation and limits

The profile generator parses all 860 classes / 12,745 methods in the supplied main SWF and reparses the patched ABC. Unit tests exercise the injected guards and preservation of original method bytes. CI builds on Windows and tests real native injection, both ANSI/Unicode read redirection, hook confirmations, and preservation of the original file using a synthetic host.

The actual game requires interactive Windows testing. This project does **not** claim every enemy or battle mode has been run. A confirmed SWF read does not establish that AIR accepts the patched SWF or that every battle path behaves correctly. If the launcher reports no confirmation, or a battle stalls, use a normal launch and retain the session log for diagnosis.

## Build

GitHub Actions builds the native DLL with MSVC, Microsoft Detours v4.0.1 (MIT), and the self-contained .NET 8 Windows Forms application. See `.github/workflows/build.yml`. Build output includes the Detours license. `tools/make_profile.py <original.swf> app/profile.json` reproduces the exact profile from the supplied build; it is not a generic updater for unknown builds.
