# NoHammerBuildMenu

Press a configurable key (default `B`) to open Valheim's build menu without a hammer taking up an inventory slot.

## How it works

A private, in-memory clone of the real hammer's item data is kept by the plugin.
It's never added to the player's inventory, so it never occupies a slot.
On keypress, the plugin calls the same public methods the game uses when you pick a hammer off your hotbar.

## Example

The below screenshot demonstrates the plugin in action, showing the build menu open without a hammer occupying an inventory slot.

![NoHammerBuildMenu Example](https://github.com/javidahmed64592/Valheim-NoHammerBuildMenu/raw/main/game_screenshot.png)
