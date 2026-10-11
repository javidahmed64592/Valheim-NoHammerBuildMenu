# NoHammerBuildMenu

This mod adds several improvements to the vanilla base hammer:

- Press `B` to open the build menu without a hammer in your inventory.
- Toggle repair mode with `Shift+Mouse2` (right-click) while building.
- Hold `Alt` and scroll to zoom the camera in and out while building, without unequipping the hammer.
- Toggle the build grid with `Ctrl+Mouse2` (right-click) while building.

## Build grid

While placing a piece, a grid is drawn just above the ground (or on top of a player-built floor) and pieces snap to its vertices, in the same way they snap to the snap points of other pieces. Snapping to existing pieces takes priority over the grid. Grid snapping is horizontal only; height comes from the surface you aim at.

- `Ctrl+Mouse2` (right-click) toggles the grid.
- `Ctrl+Scroll` doubles or halves the distance between vertices: 0.25m, 0.5m, 1m, 2m or 4m (default 0.5m).
- Enabled state and distance are in the `Grid` section of the config.

![NoHammerBuildMenu Example](https://github.com/javidahmed64592/Valheim-NoHammerBuildMenu/raw/main/game_screenshot.png)
