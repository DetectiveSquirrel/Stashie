﻿using System;
using System.Collections.Generic;
using System.IO;
using PoeHUD.Hud.Settings;
using PoeHUD.Plugins;
using SharpDX;
using Newtonsoft.Json;

namespace Stashie
{
    /*
    public class RefillParser
    {
        private const string REFILL_CONFIG = "RefillCurrency.txt";
        private const string SYMBOL_NAMEDIVIDER = ":";
        private const char SYMBOL_PARAMETERSDIVIDER = ',';
        private const string SYMBOL_IGNORE = "#";
        private const string SYMBOL_IGNORE2 = "//";

        public static List<RefillProcessor> Parse(string pluginDir)
        {
            var refills = new List<RefillProcessor>();

            var refillConfigPath = Path.Combine(pluginDir, REFILL_CONFIG);
            if (!File.Exists(refillConfigPath))
            {
                return refills;
            }

            var configLines = File.ReadAllLines(refillConfigPath);

            for (var i = 0; i < configLines.Length; i++)
            {
                var configLine = configLines[i];
                configLine = configLine.Replace("\t", "");
                if (configLine.Replace(" ", "").Length == 0)
                {
                    continue;
                }
                if (configLine.StartsWith(SYMBOL_IGNORE) || configLine.StartsWith(SYMBOL_IGNORE2))
                {
                    continue;
                }

                var newRefill = new RefillProcessor();

                var nameIndex = configLine.IndexOf(SYMBOL_NAMEDIVIDER, StringComparison.Ordinal);

                if (nameIndex == -1)
                {
                    BasePlugin.LogMessage(
                        $"Refill parser: Can't find refill name in line: {configLine}. Name should have \":\" divider.",
                        10);
                    continue;
                }

                newRefill.MenuName = configLine.Substring(0, nameIndex);
                TrimName(ref newRefill.MenuName);

                configLine = configLine.Substring(nameIndex + SYMBOL_NAMEDIVIDER.Length);

                var configLineParams = configLine.Split(SYMBOL_PARAMETERSDIVIDER);

                if (configLineParams.Length != 4)
                {
                    BasePlugin.LogMessage(
                        $"Refill parser: Config line should have 4 parameters (ClassName,StackSize,InventoryX,InventoryY): {configLine}, Ignoring refill..",
                        10);
                    continue;
                }

                newRefill.CurrencyClass = configLineParams[0];
                TrimName(ref newRefill.CurrencyClass);

                if (!int.TryParse(configLineParams[1], out newRefill.RefillAmount))
                {
                    BasePlugin.LogMessage(
                        $"Refill parser: Can't parse StackSize from 2nd parameter in line: {configLine} (line num: {i + 1}), Ignoring refill..",
                        10);
                    continue;
                }

                if (!int.TryParse(configLineParams[2], out newRefill.InventPos.X))
                {
                    BasePlugin.LogMessage(
                        $"Refill parser: Can't parse InventoryX from 3rd parameter in line: {configLine} (line num: {i + 1}), Ignoring refill..",
                        10);
                    continue;
                }
                if (newRefill.InventPos.X < 1 || newRefill.InventPos.X > 12)
                {
                    BasePlugin.LogMessage(
                        $"Refill parser: InventoryX should be in range 1-12, current value: {newRefill.InventPos.X}  (line num: {i + 1}), Ignoring refill..",
                        10);
                    continue;
                }

                if (!int.TryParse(configLineParams[3], out newRefill.InventPos.Y))
                {
                    BasePlugin.LogMessage(
                        $"Refill parser: Can't parse InventoryY from 4th parameter in line: {configLine} (line num: {i + 1}), Ignoring refill..",
                        10);
                    continue;
                }
                if (newRefill.InventPos.Y < 1 || newRefill.InventPos.Y > 5)
                {
                    BasePlugin.LogMessage(
                        $"Refill parser: InventPosY should be in range 1-5, current value: {newRefill.InventPos.Y} (line num: {i + 1}), Ignoring refill..",
                        10);
                    continue;
                }

                // Convert to zero based index.
                newRefill.InventPos.X--;
                newRefill.InventPos.Y--;

                refills.Add(newRefill);
            }
            return refills;
        }

        private static void TrimName(ref string name)
        {
            name = name.TrimEnd(' ');
            name = name.TrimStart(' ');
        }
    }
    */
    public class RefillProcessor
    {
        public ListNode CurrencyClass = new ListNode { Value = "Scroll of Wisdom" };
        public RangeNode<int> InventPosX { get; set; } = new RangeNode<int>(0, 0, 11);
        public RangeNode<int> InventPosY { get; set; } = new RangeNode<int>(0, 0, 4);
        public RangeNode<int> Amount { get; set; } = new RangeNode<int>(0, 0, 40);

        //Temp values:
        [JsonIgnore]
        public Vector2 ClickPos;
        [JsonIgnore]
        public int OwnedCount;

    

        public void Clear()
        {
            OwnedCount = 0;
        }
    }


    public class RefillSettings
    {
        public int Amount;
        public string RefillName;
    }
}