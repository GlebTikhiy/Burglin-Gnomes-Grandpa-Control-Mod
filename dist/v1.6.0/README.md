# Burglin' Gnomes Grandpa Control Mod

Player-controlled Grandpa mod for **Burglin' Gnomes Demo** (BepInEx plugin).

## What It Does
- Assigns Grandpa control to the **host player** at round start.
- Keeps the original player entity hidden as a network anchor.
- Replaces Grandpa AI movement with manual input.
- Supports movement, run, crouch, camera, grab/release/throw/shoot actions.
- Adds stronger wall/camera collision handling to reduce clipping.
- Adds fallback wake-up logic when Grandpa spawns in bed/lying state.

## Current Version
- `v1.6.0`

## Controls (Default)
- `W/A/S/D` - Move
- `Mouse X/Y` - Turn / look
- `Left Shift` - Run
- `Left Ctrl` or `C` - Crouch
- `LMB (hold)` - Grab attempt loop
- `E` - Release held gnome
- `Q` - Throw held gnome
- `RMB` - Shoot (if Grandpa has gun)
- `F` - Interact forward (doors/handles/openables)

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
- Some game cutscenes/mechanics can still force vanilla behavior depending on map state.

## Changelog
See [CHANGELOG.md](./CHANGELOG.md).

## License
MIT - see [LICENSE](./LICENSE).



