[![CI](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-nohammerbuildmenu/ci.yml?branch=main&style=flat-square&label=CI&logo=github)](https://github.com/javidahmed64592/valheim-nohammerbuildmenu/actions/workflows/ci.yml)
[![Package](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-nohammerbuildmenu/package.yml?branch=main&style=flat-square&label=Package&logo=github)](https://github.com/javidahmed64592/valheim-nohammerbuildmenu/actions/workflows/package.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# NoHammerBuildMenu

This mod adds several improvements to the vanilla base hammer:

| Key                          | Action                                                            |
| ---------------------------- | ----------------------------------------------------------------- |
| `B`                          | Open the build menu without a hammer in your inventory.           |
| `Ctrl+Mouse2` (right-click)  | Toggle the build grid.                                            |
| `Ctrl+Scroll`                | Adjust the grid distance.                                         |
| `Shift+Mouse2` (right-click) | Toggle repair mode.                                               |
| `Alt+Scroll`                 | Zoom the camera in and out without needing to unequip the hammer. |

Additionally, the hammer also uses materials from the ground and nearby chests when your inventory runs short.
The total quantity of each required material is displayed, including items in your inventory, nearby chests, and on the ground within range.

## Build Grid

While placing a piece, a grid is drawn on the surface you are aiming at and pieces snap to its vertices, in the same way they snap to the snap points of other pieces.

- On terrain, the grid follows the ground and pieces snap horizontally, with height coming from the ground.
- On horizontal and vertical surfaces, the grid aligns with the surface and pieces snap accordingly.
- The grid does not appear on diagonal surfaces and pieces only snap within the surface you are aiming at.
- The chunk borders are drawn as red lines, and the centrelines of the chunk are green.

![Chunk Lines](https://github.com/javidahmed64592/Valheim-NoHammerBuildMenu/raw/main/images/game_screenshot_1.png)
![Chunk Edges](https://github.com/javidahmed64592/Valheim-NoHammerBuildMenu/raw/main/images/game_screenshot_2.png)

## Configuration

Settings are in the BepInEx config file `javidahmed64592.nohammerbuildmenu.cfg`.
You can change the key that opens the build menu, how the build grid looks and snaps, and how far away chests and ground items are used for building materials.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/javidahmed64592/Valheim-NoHammerBuildMenu/blob/main/LICENSE) file for details.
