﻿using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ItemFilterLibrary;
using SharpDX;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using static Stashie.StashieCore;
using Vector2N = System.Numerics.Vector2;

namespace Stashie.Compartments;

internal class FilterManager
{
    public static void LoadCustomFilters()
    {
        var pickitConfigFileDirectory = Path.Combine(Main.ConfigDirectory);

        if (!Directory.Exists(pickitConfigFileDirectory))
        {
            Directory.CreateDirectory(pickitConfigFileDirectory);
            return;
        }

        var dirInfo = new DirectoryInfo(pickitConfigFileDirectory);
        Main.Settings.FilterFile.Values = dirInfo.GetFiles("*.json").Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList();
        if (Main.Settings.FilterFile.Values.Count != 0 && !Main.Settings.FilterFile.Values.Contains(Main.Settings.FilterFile.Value))
        {
            Main.Settings.FilterFile.Value = Main.Settings.FilterFile.Values.First();
        }

        if (!string.IsNullOrWhiteSpace(Main.Settings.FilterFile.Value))
        {
            var filterFilePath = Path.Combine(pickitConfigFileDirectory, $"{Main.Settings.FilterFile.Value}.json");
            if (File.Exists(filterFilePath))
            {
                Main.currentFilter = FilterParser.Load($"{Main.Settings.FilterFile.Value}.json", filterFilePath);

                foreach (var customFilter in Main.currentFilter)
                {
                    foreach (var filter in customFilter.Filters)
                    {
                        if (!Main.Settings.CustomFilterOptions.TryGetValue(customFilter.ParentMenuName + filter.FilterName, out var indexNodeS))
                        {
                            indexNodeS = new ListIndexNode {Value = "Ignore", Index = -1};
                            Main.Settings.CustomFilterOptions.Add(customFilter.ParentMenuName + filter.FilterName, indexNodeS);
                        }

                        filter.StashIndexNode = indexNodeS;
                        Main.SettingsListNodes.Add(indexNodeS);
                    }
                }
            }
            else
            {
                Main.currentFilter = null;
                Main.LogError("Item filter file not found, plugin will not work");
            }
        }
    }

    public static FilterResult CheckFilters(ItemData itemData, Vector2N clickPos)
    {
        foreach (var filter in Main.currentFilter)
        {
            foreach (var subFilter in filter.Filters)
            {
                try
                {
                    if (!subFilter.AllowProcess) continue;

                    if (filter.CompareItem(itemData, subFilter.CompiledQuery)) return new FilterResult(subFilter, itemData, clickPos);
                }
                catch (Exception ex)
                {
                    DebugWindow.LogError($"Check filters error: {ex}");
                }
            }
        }

        return null;
    }

    public static IEnumerator ParseItems()
    {
        var _serverData = Main.GameController.Game.IngameState.Data.ServerData;
        var invItems = _serverData.PlayerInventories[0].Inventory.InventorySlotItems;

        yield return new WaitFunctionTimed(() => invItems != null, true, 500, "ServerInventory->InventSlotItems is null!");
        Main.DropItems = [];
        Main.ClickWindowOffset = Main.GameController.Window.GetWindowRectangle().TopLeft.ToVector2Num();

        foreach (var invItem in invItems)
        {
            if (invItem.Item == null || invItem.Address == 0) continue;
            if (ActionsHandler.CheckIgnoreCells(invItem, (12, 5), Main.Settings.IgnoredCells)) continue;

            var testItem = new ItemData(invItem.Item, Main.GameController);
            var result = CheckFilters(testItem, invItem.GetClientRect().Center.ToVector2Num());
            if (result != null) Main.DropItems.Add(result);
        }

        if (Main.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerExpandedInventory].IsVisible)
        {
            var inventoryInventorySlotItems = _serverData.PlayerInventories[(int)InventorySlotE.ExpandedMainInventory1].Inventory.InventorySlotItems;

            foreach (var expandedInvItem in inventoryInventorySlotItems)
            {
                if (expandedInvItem.Item == null || expandedInvItem.Address == 0) continue;
                if (ActionsHandler.CheckIgnoreCells(expandedInvItem, (4, 5), Main.Settings.IgnoredExpandedCells)) continue;

                var testItem = new ItemData(expandedInvItem.Item, Main.GameController);
                var result = CheckFilters(testItem, GetExpandedClientRect(expandedInvItem).Center.ToVector2Num());
                if (result != null) Main.DropItems.Add(result);
            }
        }
    }

    public static RectangleF GetExpandedClientRect(InventSlotItem item)
    {
        var playerInventElement = Main.GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerExpandedInventory];
        var inventClientRect = playerInventElement.GetClientRect();
        var cellSize = inventClientRect.Width / 4;
        return item.Location.GetItemRect(inventClientRect.X, inventClientRect.Y, cellSize);
    }
}