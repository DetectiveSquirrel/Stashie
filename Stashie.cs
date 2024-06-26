﻿using ExileCore;
using ExileCore.Shared;
using ImGuiNET;
using Stashie.Classes;
using Stashie.Compartments;
using Stashie.Filter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Vector2N = System.Numerics.Vector2;

namespace Stashie;

public class StashieCore : BaseSettingsPlugin<StashieSettings>
{
    public const string CoroutineName = "Drop To Stash";
    public const string StashTabsNameChecker = "Stash Tabs Name Checker";
    public static StashieCore Main;

    public static List<string> RenamedAllStashNames;
    public readonly Stopwatch DebugTimer = new Stopwatch();
    public Vector2N ClickWindowOffset;
    public long CoroutineIteration;
    public Coroutine CoroutineWorker;

    public List<CustomFilter> currentFilter;
    public List<FilterResult> DropItems;
    public Action FilterTabs;
    public bool IsFilterEditorTab;
    public List<ListIndexNode> SettingsListNodes;
    public string[] StashTabNamesByIndex;
    public Coroutine StashTabNamesCoroutine;
    public int VisibleStashIndex = -1;

    public StashieCore()
    {
        Name = "Stashie With Linq";
    }

    public override bool Initialise()
    {
        Main = this;
        Settings.Enable.OnValueChanged += (sender, b) =>
        {
            if (b)
            {
                if (Core.ParallelRunner.FindByName(StashTabsNameChecker) == null) StashTabNameCoRoutine.InitStashTabNameCoRoutine();
                StashTabNamesCoroutine?.Resume();
            }
            else
            {
                StashTabNamesCoroutine?.Pause();
            }

            Utility.SetupOrClose();
        };

        StashieEditorHandler.FileSaveName = Settings.ConfigLastSaved;
        StashieEditorHandler.SelectedFileName = Settings.ConfigLastSaved;

        StashTabNameCoRoutine.InitStashTabNameCoRoutine();
        Utility.SetupOrClose();

        Input.RegisterKey(Settings.DropHotkey);

        Settings.DropHotkey.OnValueChanged += () => { Input.RegisterKey(Settings.DropHotkey); };
        Settings.FilterFile.OnValueSelected = _ => FilterManager.LoadCustomFilters();

        return true;
    }

    public override void DrawSettings()
    {
        ImGui.BeginTabBar("TabBar");
        if (ImGui.TabItemButton("Main Settings"))
        {
            IsFilterEditorTab = false;
        }

        if (ImGui.TabItemButton("Filter Editor"))
        {
            IsFilterEditorTab = true;
        }

        ImGui.EndTabBar();

        if (IsFilterEditorTab)
        {
            StashieEditorHandler.ConverterMenu();
            StashieEditorHandler.SaveLoadMenu();
            StashieEditorHandler.DrawEditorMenu();
        }
        else
        {
            StashieSettingsHandler.FilePicker();
            base.DrawSettings();
            FilterTabs?.Invoke();
        }

        Settings.ConfigLastSaved = StashieEditorHandler.FileSaveName;
        Settings.ConfigLastSelected = StashieEditorHandler.SelectedFileName;
    }

    public override void ReceiveEvent(string eventId, object args)
    {
        if (!Settings.Enable.Value)
        {
            return;
        }

        switch (eventId)
        {
            case "switch_to_tab":
                ActionsHandler.HandleSwitchToTabEvent(args);
                break;

            case "start_stashie":
                if (Core.ParallelRunner.FindByName(CoroutineName) == null)
                {
                    ActionCoRoutine.StartDropItemsToStashCoroutine();
                }

                break;
        }
    }

    public override void AreaChange(AreaInstance area)
    {
        if (StashTabNamesCoroutine == null) return;
        if (StashTabNamesCoroutine.Running)
        {
            if (!area.IsHideout && !area.IsTown && !area.DisplayName.Contains("Azurite Mine")) StashTabNamesCoroutine?.Pause();
        }
        else
        {
            if (area.IsHideout || area.IsTown || area.DisplayName.Contains("Azurite Mine")) StashTabNamesCoroutine?.Resume();
        }
    }

    public override Job Tick()
    {
        if (!StashingRequirementsMet() && Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") != null)
        {
            ActionCoRoutine.StopCoroutine("Stashie_DropItemsToStash");
            return null;
        }

        if (!Settings.DropHotkey.PressedOnce()) return null;
        if (Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") == null)
        {
            ActionCoRoutine.StartDropItemsToStashCoroutine();
        }
        else
        {
            ActionCoRoutine.StopCoroutine("Stashie_DropItemsToStash");
        }

        return null;
    }

    public bool StashingRequirementsMet() => GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible && GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;
}