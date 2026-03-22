# Burglin' Gnomes Grandpa Control Mod

Player-controlled Grandpa mod for **Burglin' Gnomes Demo** (BepInEx plugin).

## What It Does
- Assigns Grandpa control to the **host player** at round start.
- Keeps the original player entity hidden as a network anchor.
- Replaces Grandpa AI movement with manual input.
- Supports manual Grandpa movement, camera, grab/release/throw actions.
- Auto-opens doors while moving into them.
- Adds stronger wall and camera collision handling to reduce clipping.
- Adds fallback wake-up logic when Grandpa spawns in bed or lying state.
- Stabilizes Grandpa movement animation and removes the incorrect start animation that could play before walk.

## Current Version
- `v1.7.1`

## Controls (Default)
- `W/A/S/D` - Move
- `Mouse X/Y` - Turn / look
- `LMB (hold)` - Grab attempt loop
- `E` - Release held gnome
- `Q` - Throw held gnome

## Notes About Current Behavior
- Grandpa has a single fixed movement speed for fairness.
- Door opening is automatic; manual interact, crouch, sprint, and shooting are intentionally disabled.
- Multiplayer is currently tuned for **host-controlled Grandpa**.

## Known Issues
- Wake-up can still bug in some rounds. Grandpa may stand up but fail to move correctly.
- Round flow is not fully stable after a round ends. For now, restarting the lobby/round is recommended between rounds.
- Reset behavior via `R` is not considered stable and should be treated as disabled for public use.
- Because this is a host-focused gameplay mod, non-host multiplayer behavior may still be inconsistent.

## Versioning
- Format: `major.minor.patch`
- `major`:
  global release line for the mod. We keep this at `1` while the mod is still in its first public release line.
- `minor`:
  meaningful gameplay / feature updates to the mod.
- `patch`:
  small fixes, balancing, documentation, and stability updates.
- Tags are best used only for real release points.
- For non-release local work, a normal commit is enough and a tag is not required.

## Install
1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for the game.
2. Copy `BurglinGnomesGrandpaMod.dll` into:
   - `Burglin' Gnomes Demo\BepInEx\plugins\`
3. Start the game.

## Update
1. Remove old `BurglinGnomesGrandpaMod.dll` from `BepInEx\plugins`.
2. Copy new `BurglinGnomesGrandpaMod.dll` from release.

## Build (Local)
Requirements:
- Windows
- .NET Framework 4.7.2 build tools
- Game DLL references available at paths in `BurglinGnomesGrandpaMod/BurglinGnomesGrandpaMod.csproj`

Build command:
```powershell
dotnet build "C:\Users\gleb\source\repos\Mod1\BurglinGnomesGrandpaMod\BurglinGnomesGrandpaMod.csproj" -v minimal
```

Output:
- `BurglinGnomesGrandpaMod\bin\Debug\BurglinGnomesGrandpaMod.dll`

## Known Notes
- Multiplayer support is currently optimized for **host-as-Grandpa**.
- Some game cutscenes and vanilla round transitions can still override parts of normal behavior depending on map state.

## Changelog
See [CHANGELOG.md](./CHANGELOG.md).

## License
MIT - see [LICENSE](./LICENSE).



