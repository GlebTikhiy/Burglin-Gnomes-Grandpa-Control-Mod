# Changelog

## v1.6.0 - 2026-03-08
- Added robust wake-up/stand routine when Grandpa spawns lying in bed.
- Improved Grandpa wall collision:
  - Increased `CollisionRadius`
  - Increased `CollisionSkin` (earlier stop before walls)
- Improved camera near walls:
  - Increased camera collision probe radius
  - Fixed clamp logic so camera is pulled safely before obstacles
- Kept host-first multiplayer behavior for stability.
- Updated plugin version metadata to `1.6.0`.

## v1.5.9
- Prior iteration with host Grandpa control, basic movement/camera/grab framework.
