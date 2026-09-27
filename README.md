# NoHammerBuildMenu

Press a configurable key (default `B`) to open Valheim's build menu without a hammer
taking up an inventory slot, and without needing to re-craft one after dying.

## How it works

A private, in-memory clone of the real hammer's item data is kept by the plugin. It's
never added to `player.GetInventory()`, so it never occupies a slot. On keypress, the
plugin calls the same public `Player.EquipItem()` / `UnequipItem()` methods the game
uses when you pick a hammer off your hotbar - since the clone shares the real hammer's
model, icon and build-piece table, the result looks and behaves exactly like equipping
a real one.
