using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NoHammerBuildMenu
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class NoHammerBuildMenuPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "javidahmed64592.nohammerbuildmenu";
        public const string PluginName = "No Hammer Build Menu";
        public const string PluginVersion = "0.2.0";

        private const string HammerPrefabName = "Hammer";
        private const string ButtonName = "NoHammerBuildMenu_Toggle";
        private const string ButtonQueryName = ButtonName + "!" + PluginGUID;
        private const float GridMinSize = 0.25f;
        private const float GridMaxSize = 4f;
        private const float GridDefaultSize = 0.5f;
        private static readonly float[] GridSizes = { 0.25f, 0.5f, 1f, 2f, 4f };

        private ConfigEntry<KeyCode> _keybind;
        private static ConfigEntry<bool> _gridEnabled;
        private static ConfigEntry<float> _gridSize;
        private static ConfigEntry<Color> _gridColor;
        private static readonly GridOverlay _gridOverlay = new GridOverlay();

        // Latest aim point from Player.PieceRayTest, used to place the grid overlay.
        private static int _aimFrame = -10;
        private static Vector3 _aimPoint;
        private static bool _aimOnPiece;
        private static ItemDrop.ItemData _phantomHammer;
        private Harmony _harmony;
        private bool _knownRecipesRefreshed;
        private Piece _lastBuildPiece;

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

            _gridEnabled = Config.Bind(
                "Grid",
                "Enabled",
                true,
                "Show a world grid while building and snap pieces to its vertices.");

            _gridSize = Config.Bind(
                "Grid",
                "Snap Distance",
                GridDefaultSize,
                new ConfigDescription(
                    "Distance in metres between grid vertices (0.25, 0.5, 1, 2 or 4). While building, " +
                    "hold Ctrl and scroll to double or halve it.",
                    new AcceptableValueRange<float>(GridMinSize, GridMaxSize)));

            // Keep the distance on the 0.25 * 2^n steps so doubling/halving stays on vanilla distances.
            if (Array.IndexOf(GridSizes, _gridSize.Value) < 0)
                _gridSize.Value = GridDefaultSize;

            _gridColor = Config.Bind(
                "Grid",
                "Line Colour",
                new Color(1f, 1f, 1f, 0.5f),
                "Colour and opacity of the grid lines.");

            FindManualSnapField();

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _gridOverlay.Destroy();
        }

        private void Update()
        {
            // Keep phantom hammer at full durability so it never breaks or needs repair.
            if (_phantomHammer != null)
                _phantomHammer.m_durability = _phantomHammer.m_shared.m_maxDurability;

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                _phantomHammer = null;
                _knownRecipesRefreshed = false;
                _lastBuildPiece = null;
                _gridOverlay.Hide();
                return;
            }

            // Build the phantom as soon as ObjectDB is ready.
            EnsurePhantomHammer();

            // Force a recipe scan once per session with the phantom's piece table.
            if (!_knownRecipesRefreshed && _phantomHammer != null)
            {
                _knownRecipesRefreshed = true;
                player.UpdateKnownRecipesList();
            }

            // Same guards vanilla input uses - don't fire while a menu/inventory/chat is open.
            if (Menu.IsVisible() || InventoryGui.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()))
            {
                _gridOverlay.Hide();
                return;
            }

            if (ZInput.GetButtonDown(ButtonQueryName))
            {
                ToggleBuildMenu(player);
            }

            if (player.InPlaceMode()
                && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                && Input.GetMouseButtonDown(1))
            {
                ToggleRepairMode(player);
            }

            if (player.InPlaceMode() && CtrlHeld() && Input.GetMouseButtonDown(1))
            {
                _gridEnabled.Value = !_gridEnabled.Value;
                player.Message(MessageHud.MessageType.TopLeft, "Build grid " + (_gridEnabled.Value ? "on" : "off"));
            }

            if (GridActive(player))
            {
                HandleGridScroll(player);
                UpdateGridOverlay();
            }
            else
            {
                _gridOverlay.Hide();
            }
        }

        private static bool GridActive(Player player)
        {
            return _gridEnabled.Value
                && player.InPlaceMode()
                && !player.InRepairMode()
                && player.m_placementGhost != null
                && IsHammerEquipped(player);
        }

        // The grid is for hammers only (not the hoe, cultivator, etc.). Besides the phantom
        // hammer, any build tool with a piece table whose item, prefab or table name mentions
        // "hammer" counts, which covers vanilla and modded hammers.
        private static bool IsHammerEquipped(Player player)
        {
            ItemDrop.ItemData item = player.GetRightItem();
            if (item == null)
                return false;
            if (item == _phantomHammer)
                return true;

            PieceTable table = item.m_shared?.m_buildPieces;
            if (table == null)
                return false;

            return ContainsHammer(item.m_shared.m_name)
                || ContainsHammer(item.m_dropPrefab != null ? item.m_dropPrefab.name : null)
                || ContainsHammer(table.name);
        }

        private static bool ContainsHammer(string name) =>
            name != null && name.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0;

        // The game's manual snap point selection ("Snapping: Auto / Bottom 1 / ...") lives in a
        // private Player field. Find it by name so a rename just disables the feature instead of
        // breaking the plugin; candidates are logged to help diagnose that.
        private static FieldInfo _manualSnapField;

        private void FindManualSnapField()
        {
            var candidates = typeof(Player)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(int) && f.Name.IndexOf("snap", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            _manualSnapField = candidates.FirstOrDefault(f => f.Name.IndexOf("manual", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? candidates.FirstOrDefault();

            Logger.LogInfo("Manual snap field: " + (_manualSnapField?.Name ?? "not found")
                + " (candidates: " + string.Join(", ", candidates.Select(f => f.Name)) + ")");
        }

        // Index of the ghost snap point chosen by the player, or -1 for Auto / unknown.
        private static int ManualSnapIndex(Player player)
        {
            if (_manualSnapField == null)
                return -1;
            return (int)_manualSnapField.GetValue(player);
        }

        private static bool ShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool CtrlHeld() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        // Ctrl+scroll: double / halve the grid distance.
        private static void HandleGridScroll(Player player)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll == 0f)
                return;

            if (!CtrlHeld())
                return;

            float size = scroll > 0f ? _gridSize.Value * 2f : _gridSize.Value / 2f;
            size = Mathf.Clamp(size, GridMinSize, GridMaxSize);
            if (Mathf.Approximately(size, _gridSize.Value))
                return;

            _gridSize.Value = size;
            player.Message(MessageHud.MessageType.TopLeft, $"Grid distance: {size:0.###}m");
        }

        private static void UpdateGridOverlay()
        {
            // The aim point is refreshed every frame by the PieceRayTest patch while placing.
            if (Time.frameCount - _aimFrame > 2)
            {
                _gridOverlay.Hide();
                return;
            }

            _gridOverlay.Show(_aimPoint, _gridSize.Value, _aimOnPiece, _aimPoint.y, _gridColor.Value);
        }

        private void ToggleRepairMode(Player player)
        {
            if (player.InRepairMode())
            {
                if (_lastBuildPiece != null)
                    player.SetSelectedPiece(_lastBuildPiece);
            }
            else
            {
                _lastBuildPiece = player.GetSelectedPiece();
                Piece repairPiece = player.GetBuildPieces()?.FirstOrDefault(p => p.m_repairPiece);
                if (repairPiece != null)
                    player.SetSelectedPiece(repairPiece);
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

        // Includes the phantom hammer's piece table in every inventory scan so
        // UpdateKnownRecipesList discovers its pieces even without a real hammer.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetAllPieceTables))]
        private static class Inventory_GetAllPieceTables_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(List<PieceTable> tables)
            {
                if (_phantomHammer?.m_shared?.m_buildPieces != null
                    && !tables.Contains(_phantomHammer.m_shared.m_buildPieces))
                {
                    tables.Add(_phantomHammer.m_shared.m_buildPieces);
                }
            }
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

        // Moves the aimed placement point so one of the ghost's snap points (or its origin, if it
        // has none) lands on the nearest grid vertex. This runs before vanilla positions the ghost
        // and applies its own piece-to-piece snapping, so snap points of existing pieces within
        // vanilla's snap range still win over the grid, and validity checks see the final position.
        [HarmonyPatch(typeof(Player), "PieceRayTest")]
        private static class Player_PieceRayTest_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance, bool __result, ref Vector3 point, ref Vector3 normal, ref Piece piece)
            {
                // Only touch `point`: the other out params must keep the values vanilla produced.
                if (!__result
                    || __instance != Player.m_localPlayer
                    || !GridActive(__instance))
                    return;

                GameObject ghost = __instance.m_placementGhost;
                float size = _gridSize.Value;

                // Snap point offsets relative to the ghost's origin, at its current rotation.
                var snapPoints = new List<Transform>();
                ghost.GetComponent<Piece>()?.GetSnapPoints(snapPoints);

                // With a specific snap point selected, the game places that snap point at the aim
                // point (rather than the piece's origin), so the aim point itself goes on the vertex.
                var offsets = new List<Vector3>();
                int manual = ManualSnapIndex(__instance);
                if (manual >= 0 && manual < snapPoints.Count)
                {
                    offsets.Add(Vector3.zero);
                }
                else
                {
                    foreach (Transform t in snapPoints)
                        offsets.Add(t.position - ghost.transform.position);
                    if (offsets.Count == 0)
                        offsets.Add(Vector3.zero);
                }

                Vector3 bestDelta = Vector3.zero;
                float bestDist = float.MaxValue;
                foreach (Vector3 o in offsets)
                {
                    float x = point.x + o.x;
                    float z = point.z + o.z;
                    Vector3 delta = new Vector3(
                        Mathf.Round(x / size) * size - x,
                        0f,
                        Mathf.Round(z / size) * size - z);
                    float dist = delta.sqrMagnitude;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestDelta = delta;
                    }
                }

                point += bestDelta;

                _aimFrame = Time.frameCount;
                _aimPoint = point;
                _aimOnPiece = piece != null && normal.y > 0.5f;
            }
        }

        // Ctrl+scroll adjusts the grid, so stop vanilla from also using it to rotate the piece.
        [HarmonyPatch]
        private static class ZInput_GetMouseScrollWheel_Patch
        {
            private static System.Reflection.MethodBase TargetMethod() =>
                AccessTools.Method(typeof(ZInput), "GetMouseScrollWheel");

            private static bool Prepare() => TargetMethod() != null;

            [HarmonyPostfix]
            private static void Postfix(ref float __result)
            {
                Player player = Player.m_localPlayer;
                if (player != null && CtrlHeld() && GridActive(player))
                    __result = 0f;
            }
        }

        // Prevents Shift+RMB / Ctrl+RMB from also opening the build menu while we use them
        // for the repair and grid toggles.
        [HarmonyPatch(typeof(Player), "UpdateBuildGuiInput")]
        private static class Player_UpdateBuildGuiInput_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Player __instance)
            {
                if (__instance == Player.m_localPlayer
                    && _phantomHammer != null
                    && __instance.GetRightItem() == _phantomHammer
                    && (ShiftHeld() || CtrlHeld()))
                    return false;
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
                    player.m_visEquipment?.SetRightItem(
                        __state.m_dropPrefab.name.GetStableHashCode(),
                        __state.m_quality
                    );
            }
        }
    }
}
