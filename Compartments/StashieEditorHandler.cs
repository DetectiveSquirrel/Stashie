using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ExileCore;
using ImGuiNET;
using Stashie.Classes;
using static Stashie.StashieCore;
using Vector2N = System.Numerics.Vector2;

namespace Stashie.Compartments;

public class StashieEditorHandler
{
    public const string OverwritePopup = "Overwrite?";
    public static string EditorGroupFilter = "";
    public static string EditorQueryFilter = "";
    public static string EditorQueryContentFilter = "";
    public static string FileSaveName = "";
    public static string SelectedFileName = "";

    public static List<string> Files = [];
    public static FilterEditor.Filter CondEditValue = new();
    public static FilterEditor.Filter TempCondValue = new();

    private static readonly List<Action> QueuedOperations = [];

    private struct FilterDragPayload
    {
        public int ParentIndex;
        public int FilterIndex;
    }

    private struct GroupDragPayload
    {
        public int GroupIndex;
    }

    #region Filter Editor Seciton

    public static void ConverterMenu()
    {
        ImGui.TextUnformatted("This does not alter the main settings, this is only a filter file editor");

        ImGui.Spacing();

        if (!ImGui.Button("\nConvert Old .ifl To New .json\nOld files will not be altered.\n "))
            return;

        foreach (var file in FileManager.GetFilesWithExtension(Main.ConfigDirectory, ".ifl"))
        {
            if (!FileManager.TryLoadFile<FilterEditorOld.FilterParent>(
                    file, ".ifl", obj =>
                    {
                        var newData = new FilterEditor.FilterParent
                        {
                            ParentMenu = obj.ParentMenu.Select(
                                    pm => new FilterEditor.ParentMenu
                                    {
                                        MenuName = pm.MenuName,
                                        Filters = pm.Filters.Select(
                                                f => new FilterEditor.Filter
                                                {
                                                    FilterName = f.FilterName,
                                                    RawQuery = string.Join("\n", f.RawQuery),
                                                    Shifting = f.Shifting,
                                                    Affinity = f.Affinity
                                                })
                                            .ToList()
                                    })
                                .ToList()
                        };

                        FileManager.SaveToFile(newData, file);
                    }))
                Main.LogError($"Failed to load file, is it possible its not an older style?\n\t{file}", 15);
        }
    }

    public static void DrawEditorMenu()
    {
        if (Main.Settings.CurrentFilterOptions.ParentMenu == null) return;
        var tempFilters = new List<FilterEditor.ParentMenu>(Main.Settings.CurrentFilterOptions.ParentMenu);
        if (!ImGui.CollapsingHeader("Filters", ImGuiTreeNodeFlags.DefaultOpen)) return;

        ImGui.Indent();
        ImGui.InputTextWithHint("Filter Groups", "Group...", ref EditorGroupFilter, 100);
        ImGui.InputTextWithHint("Filter Queries", "Query...", ref EditorQueryFilter, 100);
        ImGui.InputTextWithHint("Filter Query Content", "Query Content...", ref EditorQueryContentFilter, 100);

        for (var parentIndex = 0; parentIndex < tempFilters.Count; parentIndex++)
        {
            var localParentIndex = parentIndex;
            var currentParent = tempFilters[localParentIndex];
            ImGui.PushID(localParentIndex);

            if (!currentParent.MenuName.Contains(EditorGroupFilter, StringComparison.InvariantCultureIgnoreCase) ||
                currentParent.Filters.All(x => !x.FilterName.Contains(EditorQueryFilter, StringComparison.InvariantCultureIgnoreCase)) ||
                currentParent.Filters.All(x => !x.RawQuery.Contains(EditorQueryContentFilter, StringComparison.InvariantCultureIgnoreCase)))
            {
                ImGui.PopID();
                continue;
            }

            ImGui.BeginChild($"parentFilterGroup_{localParentIndex}", Vector2N.Zero, ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY);
            HandleGroupDragDrop(tempFilters, localParentIndex);
            ImGui.Indent();
            ImGui.InputTextWithHint("Group Name", "\"Heist Items\" etc..", ref currentParent.MenuName, 200);
            ImGui.BeginChild($"innerParentFilterGroup_{localParentIndex}", Vector2N.Zero, ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY);

            for (var filterIndex = 0; filterIndex < currentParent.Filters.Count; filterIndex++)
            {
                var localFilterIndex = filterIndex;
                var currentFilter = currentParent.Filters[localFilterIndex];
                ImGui.PushID(localFilterIndex);

                if (!currentFilter.FilterName.Contains(EditorQueryFilter, StringComparison.InvariantCultureIgnoreCase) ||
                    !currentFilter.RawQuery.Contains(EditorQueryContentFilter, StringComparison.InvariantCultureIgnoreCase))
                {
                    ImGui.PopID();
                    continue;
                }

                HandleFilterDragDrop(tempFilters, localParentIndex, localFilterIndex);
                ImGui.SameLine();
                ImGui.InputTextWithHint("", "\"Heist Items\" etc..", ref currentFilter.FilterName, 200);

                ImGui.SameLine();
                CheckboxWithTooltip("Shifting", ref currentFilter.Shifting, "Holds Shift to bypass Tab Affinity.");
                ImGui.SameLine();
                CheckboxWithTooltip(
                    "Affinity", ref currentFilter.Affinity, "Assumes Affinity is set and won't change to selected stash tab\nwhen stashing items.");

                ImGui.SameLine();
                var isEditing = IsCurrentEditorContext(localParentIndex, localFilterIndex);
                if (isEditing)
                    BeginFilterEditWindow(localParentIndex, localFilterIndex, tempFilters);
                var editString = isEditing ? "Editing" : "Edit";
                if (ImGui.Button($"{editString}"))
                {
                    if (isEditing)
                    {
                        ResetEditingIdentifiers();
                    }
                    else
                    {
                        CondEditValue = new FilterEditor.Filter
                        {
                            FilterName = currentFilter.FilterName, Affinity = currentFilter.Affinity, RawQuery = currentFilter.RawQuery,
                            Shifting = currentFilter.Shifting
                        };
                        TempCondValue = new FilterEditor.Filter
                        {
                            FilterName = currentFilter.FilterName, Affinity = currentFilter.Affinity, RawQuery = currentFilter.RawQuery,
                            Shifting = currentFilter.Shifting
                        };
                        _editor = new EditorRecord(localParentIndex, localFilterIndex);
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Delete"))
                    QueuedOperations.Add(
                        () =>
                        {
                            ResetEditingIdentifiers();
                            tempFilters[localParentIndex].Filters.RemoveAt(localFilterIndex);
                        });

                ImGui.PopID();
            }

            if (ImGui.Button("[=] Add New Filter"))
                QueuedOperations.Add(
                    () =>
                    {
                        ResetEditingIdentifiers();
                        tempFilters[localParentIndex]
                            .Filters.Add(new FilterEditor.Filter { FilterName = "", RawQuery = "", Affinity = false, Shifting = false });
                    });

            ImGui.EndChild();
            ImGui.Unindent();

            if (ImGui.Button("[X] Delete Group"))
                QueuedOperations.Add(
                    () =>
                    {
                        tempFilters.RemoveAt(localParentIndex);
                        ResetEditingIdentifiers();
                    });

            ImGui.EndChild();
            ImGui.Spacing();
            ImGui.PopID();
        }

        ImGui.Unindent();
        if (ImGui.Button("[=] Add New Group"))
            QueuedOperations.Add(
                () =>
                {
                    ResetEditingIdentifiers();
                    tempFilters.Add(
                        new FilterEditor.ParentMenu
                            { MenuName = "", Filters = [new FilterEditor.Filter { FilterName = "", RawQuery = "", Affinity = false, Shifting = false }] });
                });

        foreach (var operation in QueuedOperations)
        {
            try
            {
                operation();
            }
            catch (Exception e)
            {
                Main.LogError($"Error during queued UI operation: {e}", 15);
            }
        }

        QueuedOperations.Clear();

        Main.Settings.CurrentFilterOptions.ParentMenu = tempFilters;
    }

    private static void HandleGroupDragDrop(List<FilterEditor.ParentMenu> tempFilters, int localParentIndex)
    {
        ImGui.PushID($"groupDrag_{localParentIndex}");
        var groupDropTargetStart = ImGui.GetCursorScreenPos();

        ImGui.Button("=", new Vector2N(30, 20));

        if (ImGui.BeginDragDropSource())
        {
            ImGuiHelpers.SetDragDropPayload("GroupIndex", new GroupDragPayload { GroupIndex = localParentIndex });
            ImGui.Text($"Group: {tempFilters[localParentIndex].MenuName}");
            ImGui.EndDragDropSource();
        }
        else if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Drag me");
        }

        ImGui.SetCursorScreenPos(groupDropTargetStart);
        ImGui.InvisibleButton($"groupDropTarget_{localParentIndex}", new Vector2N(30, 20));

        if (ImGui.BeginDragDropTarget())
        {
            var groupPayload = ImGuiHelpers.AcceptDragDropPayload<GroupDragPayload>("GroupIndex");
            if (groupPayload.HasValue && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                var payload = groupPayload.Value;
                if (payload.GroupIndex != localParentIndex)
                    QueuedOperations.Add(
                        () =>
                        {
                            (tempFilters[payload.GroupIndex], tempFilters[localParentIndex]) = (tempFilters[localParentIndex], tempFilters[payload.GroupIndex]);
                            ResetEditingIdentifiers();
                        });
            }

            ImGui.EndDragDropTarget();
        }

        ImGui.PopID();
    }

    private static void HandleFilterDragDrop(List<FilterEditor.ParentMenu> tempFilters, int localParentIndex, int localFilterIndex)
    {
        var currentParent = tempFilters[localParentIndex];
        var currentFilter = currentParent.Filters[localFilterIndex];

        ImGui.PushID($"drag_{currentFilter.FilterName}");

        var dropTargetStart = ImGui.GetCursorScreenPos();

        ImGui.Button("=", new Vector2N(30, 20));

        if (ImGui.BeginDragDropSource())
        {
            ImGuiHelpers.SetDragDropPayload("FilterIndex", new FilterDragPayload { ParentIndex = localParentIndex, FilterIndex = localFilterIndex });
            ImGui.Text($"{currentParent.MenuName} -> {currentFilter.FilterName}");
            ImGui.EndDragDropSource();
        }
        else if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Drag me");
        }

        ImGui.SetCursorScreenPos(dropTargetStart);
        ImGui.InvisibleButton($"dropTarget_{currentFilter.FilterName}", new Vector2N(30, 20));

        if (ImGui.BeginDragDropTarget())
        {
            var filterPayload = ImGuiHelpers.AcceptDragDropPayload<FilterDragPayload>("FilterIndex");
            if (filterPayload.HasValue && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                var payload = filterPayload.Value;
                QueuedOperations.Add(
                    () =>
                    {
                        var sourceParent = tempFilters[payload.ParentIndex];
                        var targetParent = tempFilters[localParentIndex];
                        var movedRule = sourceParent.Filters[payload.FilterIndex];

                        if (payload.ParentIndex == localParentIndex)
                        {
                            var targetRule = targetParent.Filters[localFilterIndex];
                            (sourceParent.Filters[payload.FilterIndex], targetParent.Filters[localFilterIndex]) = (targetRule, movedRule);
                        }
                        else
                        {
                            targetParent.Filters.Insert(localFilterIndex, movedRule);
                            sourceParent.Filters.Remove(movedRule);
                        }

                        ResetEditingIdentifiers();
                    });
            }

            ImGui.EndDragDropTarget();
        }

        ImGui.PopID();
    }

    private static void BeginFilterEditWindow(int parentIndex, int filterIndex, List<FilterEditor.ParentMenu> parentMenu)
    {
        if (_editor.GroupIndex != parentIndex || _editor.FilterIndex != filterIndex)
            return;

        if (!ImGui.Begin("Edit Stashie Filter", ImGuiWindowFlags.None))
        {
            ImGui.End();
            return;
        }

        var groupName = parentMenu[parentIndex].MenuName;
        var filterName = parentMenu[parentIndex].Filters[filterIndex].FilterName;

        ImGui.BulletText(
            $"Editing: Group[{(!string.IsNullOrEmpty(groupName) ? groupName : _editor.GroupIndex + 1)}] => Filter[{(!string.IsNullOrEmpty(filterName) ? filterName : _editor.FilterIndex + 1)}]");

        if (ImGui.Button("Save"))
        {
            parentMenu[parentIndex].Filters[filterIndex] = TempCondValue;
            ResetEditingIdentifiers();
        }

        ImGui.SameLine();

        if (ImGui.Button("Revert"))
            TempCondValue = new FilterEditor.Filter
            {
                FilterName = CondEditValue.FilterName, Affinity = CondEditValue.Affinity, RawQuery = CondEditValue.RawQuery, Shifting = CondEditValue.Shifting
            };

        ImGui.SameLine();

        if (ImGui.Button("Close"))
            ResetEditingIdentifiers();

        CheckboxWithTooltip("Shifting", ref TempCondValue.Shifting, "Holds Shift to bypass Tab Affinity.");
        CheckboxWithTooltip("Affinity", ref TempCondValue.Affinity, "Assumes Affinity is set and won't change to selected stash tab\nwhen stashing items.");

        ImGui.InputTextMultiline("##text_edit", ref TempCondValue.RawQuery, 15000, ImGui.GetContentRegionAvail(), ImGuiInputTextFlags.AllowTabInput);

        ImGui.End();
    }

    public static void CheckboxWithTooltip(string label, ref bool value, string tooltip)
    {
        ImGui.Checkbox(label, ref value);
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
            ImGui.SetTooltip(tooltip);
    }

    private static void ResetEditingIdentifiers()
    {
        _editor = new EditorRecord(-1, -1);
    }

    private static bool IsCurrentEditorContext(int groupIndex, int stepIndex)
    {
        return _editor.FilterIndex == stepIndex && _editor.GroupIndex == groupIndex;
    }

    private static EditorRecord _editor = new(-1, -1);

    private record EditorRecord(int GroupIndex, int FilterIndex);

    public static void SaveLoadMenu()
    {
        if (!ImGui.CollapsingHeader($"Load / Save##{Main.Name}Load / Save", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.Indent();
        ImGui.InputTextWithHint("##SaveAs", "File Path...", ref FileSaveName, 100);
        ImGui.SameLine();

        if (ImGui.Button("Save To File"))
        {
            Files = FileManager.GetFilesWithExtension(Main.ConfigDirectory, ".json");

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                FileSaveName = FileSaveName.Replace(c, '_');
            }

            if (!string.IsNullOrEmpty(FileSaveName))
            {
                if (Files.Contains(FileSaveName))
                    ImGui.OpenPopup(OverwritePopup);
                else
                    FileManager.SaveToFile(Main.Settings.CurrentFilterOptions, FileSaveName);
            }
        }

        ImGui.Separator();

        if (ImGui.BeginCombo("Load File##LoadNewFile", SelectedFileName))
        {
            Files = FileManager.GetFilesWithExtension(Main.ConfigDirectory, ".json");

            foreach (var fileName in Files)
            {
                var isSelected = SelectedFileName == fileName;

                if (ImGui.Selectable(fileName, isSelected))
                {
                    SelectedFileName = fileName;
                    FileSaveName = fileName;
                    FileManager.TryLoadFile<FilterEditor.FilterParent>(
                        fileName, ".json", loadedFilter =>
                        {
                            Main.Settings.CurrentFilterOptions = loadedFilter;
                            ResetEditingIdentifiers();
                        });
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.Separator();

        if (ImGui.Button("Open Filter Folder"))
        {
            var configDir = Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory), "Stashie");

            if (!Directory.Exists(configDir))
                Main.LogError($"Path Doesn't Exist\n{configDir}");
            else
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = configDir
                    });
        }

        ImGui.Unindent();

        if (!ShowButtonPopup(OverwritePopup, ["Are you sure?", "STOP"], out var saveSelectedIndex))
            return;

        if (saveSelectedIndex == 0)
            FileManager.SaveToFile(Main.Settings.CurrentFilterOptions, FileSaveName);
    }

    public static bool ShowButtonPopup(string popupId, List<string> items, out int selectedIndex)
    {
        selectedIndex = -1;
        var isItemClicked = false;
        var showPopup = true;

        if (!ImGui.BeginPopupModal(popupId, ref showPopup, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
            return false;

        for (var i = 0; i < items.Count; i++)
        {
            if (ImGui.Button(items[i]))
            {
                selectedIndex = i;
                isItemClicked = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
        }

        ImGui.EndPopup();
        return isItemClicked;
    }

    #endregion
}