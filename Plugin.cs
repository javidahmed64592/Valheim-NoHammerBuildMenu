using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;

namespace NoHammerBuildMenu
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class NoHammerBuildMenuPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "javidahmed64592.nohammerbuildmenu";
        public const string PluginName = "No Hammer Build Menu";
        public const string PluginVersion = "0.1.0";

        private const string HammerPrefabName = "Hammer";
        private const string ButtonName = "NoHammerBuildMenu_Toggle";
        private const string ButtonQueryName = ButtonName + "!" + PluginGUID;

        private ConfigEntry<KeyCode> _keybind;
        private static ItemDrop.ItemData _phantomHammer;
        private Harmony _harmony;

        private void Awake()
        {
            _keybind = Config.Bind(
                "Keybinds",
                "Open Build Menu",
                KeyCode.B,
                "Equips a virtual hammer and opens the build menu, without needing a real " +
                "hammer in your inventory. Rebindable from the in-game controls menu.");

            InputManager.Instance.AddButton(PluginGUID, new ButtonConfig
            {
                Name = ButtonName,
                Config = _keybind
            });

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                _phantomHammer = null;
                return;
            }

            // Same guards vanilla input uses - don't fire while a menu/inventory/chat is open.
            if (Menu.IsVisible() || InventoryGui.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()))
            {
                return;
            }

            if (ZInput.GetButtonDown(ButtonQueryName))
            {
                ToggleBuildMenu(player);
            }
        }

        private void ToggleBuildMenu(Player player)
        {
            EnsurePhantomHammer();
            if (_phantomHammer == null)
            {
                return; // ObjectDB isn't loaded yet (e.g. still on the main menu)
            }

            // Checking the player's actual right-hand item (rather than tracking our own
            // bool) keeps this correct across death/respawn or if something else unequips us.
            bool isEquipped = player.GetRightItem() == _phantomHammer;

            if (!isEquipped)
            {
                // Same call path as picking the hammer from a hotbar slot: shows it
                // in-hand and enables the build piece grid.
                player.EquipItem(_phantomHammer);
            }
            else
            {
                player.UnequipItem(_phantomHammer);
            }
        }

        private void EnsurePhantomHammer()
        {
            if (_phantomHammer != null || ObjectDB.instance == null)
            {
                return;
            }

            GameObject hammerPrefab = ObjectDB.instance.GetItemPrefab(HammerPrefabName);
            if (hammerPrefab == null)
            {
                return;
            }

            ItemDrop realHammer = hammerPrefab.GetComponent<ItemDrop>();

            // Clone() reuses the same SharedData (model, icon, and crucially the
            // build-piece table) as the real hammer, so it looks and behaves identically.
            // It's just never added to player.GetInventory(), so it never takes a slot
            // and never needs re-crafting after death.
            _phantomHammer = realHammer.m_itemData.Clone();
        }

        // Makes EquipItem accept the phantom hammer despite it not being in the player's inventory.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ContainsItem))]
        private static class Inventory_ContainsItem_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ItemDrop.ItemData item, ref bool __result)
            {
                if (_phantomHammer != null && item == _phantomHammer)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
    }
}
