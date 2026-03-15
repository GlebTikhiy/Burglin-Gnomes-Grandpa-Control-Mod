# Burglin' Gnomes Grandpa Control Mod

Player-controlled Grandpa mod for **Burglin' Gnomes Demo** (BepInEx plugin).

## What It Does
- Assigns Grandpa control to the **host player** at round start.
- Keeps the original player entity hidden as a network anchor.
- Replaces Grandpa AI movement with manual input.
- Supports manual Grandpa movement, camera, grab/release/throw/shoot actions.
- Auto-opens doors while moving into them.
- Adds stronger wall and camera collision handling to reduce clipping.
- Adds fallback wake-up logic when Grandpa spawns in bed or lying state.
- Stabilizes Grandpa movement animation and removes the incorrect start animation that could play before walk.

## Current Version
- `v1.7.0`

## Controls (Default)
- `W/A/S/D` - Move
- `Mouse X/Y` - Turn / look
- `LMB (hold)` - Grab attempt loop
- `E` - Release held gnome
- `Q` - Throw held gnome
- `RMB` - Shoot (if Grandpa has gun)
- `R` - Reset Grandpa to spawn position

## Notes About Current Behavior
- Grandpa has a single fixed movement speed for fairness.
- Door opening is automatic; manual interact, crouch, and sprint are intentionally disabled.
- Multiplayer is currently tuned for **host-controlled Grandpa**.

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



