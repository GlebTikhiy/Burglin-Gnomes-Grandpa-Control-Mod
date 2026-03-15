# Changelog

## v1.7.0 - 2026-03-15
- Fixed Grandpa wake-up handling when Grandpa starts the round lying in bed.
- Fixed the incorrect movement-start animation that could play before normal walking.
- Improved post-wake movement recovery so Grandpa can move correctly after standing up.
- Simplified Grandpa controls for stability and fairness:
  - Removed sprint
  - Removed crouch
  - Removed manual `F` interact
- Reworked door interaction into automatic door opening while moving into doors.
- Tuned automatic door opening to be more reliable and less jittery.
- Reduced Grandpa movement speed slightly for better balance.
- Updated documentation and plugin version metadata to `1.7.0`.

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
