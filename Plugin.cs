using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using System.Collections.Generic;
using UnityEngine;

namespace NoHammerBuildMenu
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class NoHammerBuildMenuPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "javidahmed64592.nohammerbuildmenu";
        public const string PluginName = "No Hammer Build Menu";
        public const string PluginVersion = "0.1.1";

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

            ItemDrop itemDrop = hammerPrefab.GetComponent<ItemDrop>();

            // Build a fresh ItemData rather than Clone()-ing the prefab's data: raw prefab
            // item data has uninitialised fields (e.g. m_customData = null) that
            // SetupVisEquipment dereferences in Valheim l-1.0.x, causing a crash.
            _phantomHammer = new ItemDrop.ItemData
            {
                m_shared = itemDrop.m_itemData.m_shared,
                m_dropPrefab = hammerPrefab,
                m_stack = 1,
                m_quality = 1,
                m_variant = 0,
                m_durability = itemDrop.m_itemData.m_shared.m_maxDurability,
                m_customData = new Dictionary<string, string>()
            };
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

        // SetupVisEquipment in Valheim l-1.0.x crashes with a NullReferenceException when
        // equipping items that didn't come through the normal inventory flow. Work around it
        // by hiding the phantom from the method and setting the visual directly afterward.
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
        private static class Humanoid_SetupVisEquipment_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Humanoid __instance, out ItemDrop.ItemData __state)
            {
                __state = null;
                if (_phantomHammer != null && __instance.GetRightItem() == _phantomHammer)
                {
                    __state = _phantomHammer;
                    __instance.m_rightItem = null;
                }
            }

            [HarmonyPostfix]
            private static void Postfix(Humanoid __instance, ItemDrop.ItemData __state)
            {
                if (__state == null) return;
                __instance.m_rightItem = __state;
                if (__instance is Player player)
                    player.m_visEquipment?.SetRightHandEquipped(__state.m_variant, __state.m_quality);
            }
        }
    }
}
