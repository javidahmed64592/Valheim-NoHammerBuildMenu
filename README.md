# NoHammerBuildMenu

This mod adds several improvements to the vanilla base hammer:

- Press `B` to open the build menu without a hammer in your inventory.
- Toggle repair mode with `Shift+Mouse2` (right-click) while building.
- Hold `Alt` and scroll to zoom the camera in and out while building, without unequipping the hammer.
- Toggle the build grid with `Ctrl+Mouse2` (right-click) while building.
- The mod's controls are listed in the build key hints at the bottom of the screen while a hammer is equipped.
- Building uses materials from nearby chests when your inventory runs short.

## Build grid

While placing a piece, a grid is drawn on the surface you are aiming at and pieces snap to its vertices, in the same way they snap to the snap points of other pieces.
Snapping to existing pieces takes priority over the grid.

- On terrain, the grid follows the ground and pieces snap horizontally, with height coming from the ground.
- On top of a player-built piece (e.g. a floor), the grid is flat on its top surface.
- Aiming up at the underside of a piece (e.g. a ceiling) draws the grid on the underside.
- Aiming at a vertical face of a piece draws the grid on that wall, so you can align pieces vertically as well as horizontally.
- Pieces only snap within the surface you are aiming at, never to empty air. Diagonal walls snap height only and show no grid.

- `Ctrl+Mouse2` (right-click) toggles the grid.
- `Ctrl+Scroll` doubles or halves the distance between vertices: 0.25m, 0.5m, 1m, 2m or 4m (default 0.5m).
- The world's chunk borders are highlighted in a different colour.
- The grid's radius, fade, line widths, line colours and more can be changed in the `Grid Appearance` section of the config file.

![NoHammerBuildMenu Example](https://github.com/javidahmed64592/Valheim-NoHammerBuildMenu/raw/main/game_screenshot.png)

## Nearby chests

When you place a piece, materials come from your inventory first and any shortfall is taken from chests within range, nearest first.
You can only build if your inventory and the nearby chests together have enough.
Only chests you could normally open are used.
Enable/disable this and set the range (default 30m) in the `Chests` section of the config file.
