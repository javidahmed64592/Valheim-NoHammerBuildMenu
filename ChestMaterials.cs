using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace NoHammerBuildMenu
{
    // Lets building draw materials from nearby chests after the player's own inventory runs out.
    internal static class ChestMaterials
    {
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<float> _radius;
        private static ConfigEntry<float> _groundRange;

        private static readonly List<Container> Containers = new List<Container>();
        private static readonly List<Container> NearbyCache = new List<Container>();
        private static readonly AccessTools.FieldRef<Container, ZNetView> NviewRef =
            AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

        private static readonly List<ItemDrop> Drops = new List<ItemDrop>();
        private static readonly AccessTools.FieldRef<ItemDrop, ZNetView> DropNviewRef =
            AccessTools.FieldRefAccess<ItemDrop, ZNetView>("m_nview");

        private static int _nearbyFrame = -1;
        private static Vector3 _nearbyPos;

        // >0 while the player's inventory counts should include chests (requirement checks / display).
        private static int _countDepth;
        // True while the resources of the placed piece are being consumed.
        private static bool _consuming;

        public static void BindConfig(ConfigFile config)
        {
            // Chests
            _enabled = config.Bind("Chests", "Use Nearby Chests", true,
                "When building, take any materials you are missing from your inventory out of nearby chests.");
            _radius = config.Bind("Chests", "Radius", 30f,
                new ConfigDescription("Distance in metres from the player within which chests are used for building materials.",
                    new AcceptableValueRange<float>(1f, 128f)));

            // Ground items
            _groundRange = config.Bind("Ground Items", "Radius", 10f,
                new ConfigDescription("Distance in metres from the player within which items lying on the ground are used for building materials. Set to 0 to disable.",
                    new AcceptableValueRange<float>(0f, 64f)));
        }

        private static bool Active(Player player) =>
            _enabled != null && player != null && player == Player.m_localPlayer
            && (_enabled.Value || _groundRange.Value > 0f);

        // Dropped items within range that match the requested item.
        private static List<ItemDrop> GetGroundMatches(Player player, string name, int quality, bool matchWorldLevel)
        {
            List<ItemDrop> result = new List<ItemDrop>();
            if (_groundRange.Value <= 0f)
                return result;

            Vector3 pos = player.transform.position;
            float maxSqr = _groundRange.Value * _groundRange.Value;
            Drops.RemoveAll(d => d == null);
            foreach (ItemDrop drop in Drops)
            {
                ItemDrop.ItemData data = drop.m_itemData;
                ZNetView nview = DropNviewRef(drop);
                if (data == null || nview == null || !nview.IsValid()
                    || drop.IsPiece()
                    || data.m_shared.m_name != name
                    || (quality >= 0 && quality != data.m_quality)
                    || (matchWorldLevel && data.m_worldLevel < Game.m_worldLevel)
                    || (drop.transform.position - pos).sqrMagnitude > maxSqr)
                    continue;
                result.Add(drop);
            }

            result.Sort((a, b) =>
                (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));
            return result;
        }

        private static int TakeFromGround(Player player, string name, int amount, int quality, bool matchWorldLevel)
        {
            foreach (ItemDrop drop in GetGroundMatches(player, name, quality, matchWorldLevel))
            {
                if (amount <= 0)
                    break;

                ZNetView nview = DropNviewRef(drop);
                if (!nview.IsOwner())
                    nview.ClaimOwnership();

                int take = Mathf.Min(drop.m_itemData.m_stack, amount);
                amount -= take;
                if (take >= drop.m_itemData.m_stack)
                    nview.Destroy();
                else
                    drop.SetStack(drop.m_itemData.m_stack - take);
            }
            return amount;
        }

        // Chests within range that this player may take from, nearest first.
        private static List<Container> GetNearby(Player player)
        {
            Vector3 pos = player.transform.position;
            if (_nearbyFrame == Time.frameCount && _nearbyPos == pos)
                return NearbyCache;

            _nearbyFrame = Time.frameCount;
            _nearbyPos = pos;
            NearbyCache.Clear();
            if (!_enabled.Value)
                return NearbyCache;

            float maxSqr = _radius.Value * _radius.Value;
            Containers.RemoveAll(c => c == null);
            foreach (Container container in Containers)
            {
                if ((container.transform.position - pos).sqrMagnitude <= maxSqr && CanTakeFrom(container))
                    NearbyCache.Add(container);
            }

            NearbyCache.Sort((a, b) =>
                (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));
            return NearbyCache;
        }

        // Only player-built chests that the player could open normally and that nobody is using.
        private static bool CanTakeFrom(Container container)
        {
            if (container.GetInventory() == null
                || container.IsInUse()
                || container.m_wagon != null
                || container.m_autoDestroyEmpty
                || container.GetComponent<TombStone>() != null)
                return false;

            ZNetView nview = NviewRef(container);
            if (nview == null || !nview.IsValid())
                return false;

            Piece piece = container.GetComponent<Piece>();
            if (piece == null)
                return false;

            switch (container.m_privacy)
            {
                case Container.PrivacySetting.Public:
                    break;
                case Container.PrivacySetting.Private:
                    if (piece.GetCreator() != Game.instance.GetPlayerProfile().GetPlayerID())
                        return false;
                    break;
                default:
                    return false;
            }

            return !container.m_checkGuardStone
                || PrivateArea.CheckAccess(container.transform.position, 0f, false);
        }

        private static void TakeFromChests(Player player, string name, int amount, int quality, bool matchWorldLevel)
        {
            amount = TakeFromGround(player, name, amount, quality, matchWorldLevel);
            foreach (Container container in GetNearby(player))
            {
                if (amount <= 0)
                    return;

                Inventory inventory = container.GetInventory();
                int take = Mathf.Min(inventory.CountItems(name, quality, matchWorldLevel), amount);
                if (take <= 0)
                    continue;

                // Only the owner's changes are saved, so take ownership of chests held by others.
                if (!container.IsOwner())
                    NviewRef(container).ClaimOwnership();

                inventory.RemoveItem(name, take, quality, matchWorldLevel);
                amount -= take;
            }
        }

        [HarmonyPatch(typeof(ItemDrop), "Awake")]
        private static class ItemDrop_Awake_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemDrop __instance)
            {
                if (!Drops.Contains(__instance))
                    Drops.Add(__instance);
            }
        }

        [HarmonyPatch(typeof(Container), "Awake")]
        private static class Container_Awake_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Container __instance)
            {
                if (__instance.GetInventory() != null && !Containers.Contains(__instance))
                    Containers.Add(__instance);
            }
        }

        // Build checks (can the piece be built / is its button greyed out) count chest contents too.
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
        private static class Player_HaveRequirements_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, Player.RequirementMode mode, out bool __state)
            {
                __state = mode == Player.RequirementMode.CanBuild && Active(__instance);
                if (__state)
                    _countDepth++;
            }

            [HarmonyFinalizer]
            private static void Finalizer(bool __state)
            {
                if (__state)
                    _countDepth--;
            }
        }

        // The requirement list in the build panel shows the combined amount.
        [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
        private static class Hud_SetupPieceInfo_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(out bool __state)
            {
                __state = Active(Player.m_localPlayer);
                if (__state)
                    _countDepth++;
            }

            [HarmonyFinalizer]
            private static void Finalizer(bool __state)
            {
                if (__state)
                    _countDepth--;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
        private static class Inventory_CountItems_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
            {
                Player player = Player.m_localPlayer;
                if (_countDepth <= 0 || name == null || !Active(player) || __instance != player.GetInventory())
                    return;

                foreach (Container container in GetNearby(player))
                    __result += container.GetInventory().CountItems(name, quality, matchWorldLevel);

                foreach (ItemDrop drop in GetGroundMatches(player, name, quality, matchWorldLevel))
                    __result += drop.m_itemData.m_stack;
            }
        }

        // Only the resources of a piece being placed are drawn from chests, not crafting costs.
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        private static class Player_ConsumeResources_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, Piece.Requirement[] requirements, out bool __state)
            {
                __state = Active(__instance)
                    && __instance.InPlaceMode()
                    && __instance.GetSelectedPiece() != null
                    && __instance.GetSelectedPiece().m_resources == requirements;
                if (__state)
                    _consuming = true;
            }

            [HarmonyFinalizer]
            private static void Finalizer(bool __state)
            {
                if (__state)
                    _consuming = false;
            }
        }

        // Uses the player's own stock first and takes only the shortfall from chests.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(string), typeof(int), typeof(int), typeof(bool))]
        private static class Inventory_RemoveItem_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Inventory __instance, string name, ref int amount, int itemQuality, bool worldLevelBased)
            {
                Player player = Player.m_localPlayer;
                if (!_consuming || !Active(player) || __instance != player.GetInventory())
                    return true;

                int have = __instance.CountItems(name, itemQuality, worldLevelBased);
                if (have >= amount)
                    return true;

                TakeFromChests(player, name, amount - have, itemQuality, worldLevelBased);
                if (have <= 0)
                    return false;

                amount = have;
                return true;
            }
        }
    }
}
