# Release Notes - v1.6.0

## Summary
This release focuses on reliability when Grandpa starts in non-standard states (bed/lying) and on reducing wall/camera clipping during manual control.

## Highlights
- Force-stand routine on control init to recover from bed spawn states.
- Stronger character collision envelope to prevent partial wall penetration.
- Camera collision fix near walls (safe clamp distance; no max-based overshoot).

## Files
- `BurglinGnomesGrandpaMod.dll`

## Installation
Copy `BurglinGnomesGrandpaMod.dll` to:
`Burglin' Gnomes Demo\BepInEx\plugins\`

## Compatibility
- Game: Burglin' Gnomes Demo
- Runtime: BepInEx 5.x
- Target framework: .NET Framework 4.7.2

