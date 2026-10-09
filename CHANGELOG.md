# Changelog

## 0.2.0

- Added a world build grid: a visible grid overlay near your aim point (following the terrain, or flat on top of player-built floors) with snap points at every vertex. Pieces snap to grid vertices like they do to other pieces' snap points, and snapping to other pieces still takes priority.
- Hold Shift and scroll to change the grid distance by 0.05m; hold Ctrl and scroll to double or halve it (0.1m minimum, 8m maximum, 0.5m default).
- Added a keybind (default `G`) to toggle the grid, plus `Grid` config options for enabled state, snap distance and line colour.

## 0.1.5

- Added repair mode toggle keybind (Shift + Right Mouse Button) to switch between repair mode and the last selected build piece.

## 0.1.4

- Ensure hammer recipes are known without needing to hold a physical hammer

## 0.1.3

- Fix website link in manifest file
- Include CHANGELOG.md in the packaged mod files

## 0.1.2

- Made hammer invincible so it does not break
- The hammer is no longer invisible when equipped in the player's hand

## 0.1.1

- Fixed README with example screenshot

## 0.1.0

- Initial release
- Configurable keybind (default `B`) opens the build menu without a hammer in your inventory, and shows the hammer in-hand
- Keybind is rebindable from your mod manager
