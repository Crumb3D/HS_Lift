using System;
using System.Collections.Generic;

// Hold-E floor list. The radial labels each entry with Localization.Get("blockcommand_" + text), so the
// label for each numbered slot is written into the live localization table when the menu is built.
public static class HSLiftFloorMenu
{
    const string Prefix = "hsliftFloor";

    public static List<BlockActivationCommand> Commands()
    {
        return Commands(HSLiftConfiguration.Data);
    }

    public static List<BlockActivationCommand> Commands(HSLiftConfigData d)
    {
        var list = new List<BlockActivationCommand>();
        if (d == null || d.Floors == null) return list;
        var ctrl = HSLiftController.Of(d);
        var moving = ctrl != null && ctrl.IsThisMoving;
        for (int i = d.Floors.Count - 1; i >= 0; i--)
        {
            var f = d.Floors[i];
            bool here = f.Y == d.CurrentY;
            var text = Prefix + i;
            SetLabel("blockcommand_" + text, HSLiftConfiguration.FloorDisplayName(f.Name) + (here ? " (here)" : ""));
            list.Add(new BlockActivationCommand(text, "electric_switch", !here && !moving, false, null));
        }
        return list;
    }

    public static int OtherFloorIndex()
    {
        var d = HSLiftConfiguration.Data;
        if (d == null || d.Floors == null || d.Floors.Count != 2) return -1;
        if (d.Floors[0].Y == d.CurrentY) return 1;
        if (d.Floors[1].Y == d.CurrentY) return 0;
        return d.CurrentY < d.Floors[1].Y ? 1 : 0;
    }

    public static string Prompt()
    {
        return Localization.Get("hsliftChooseFloor");
    }

    // Floor index for a menu command, or -1 when it is not one of ours.
    public static int Parse(string commandName)
    {
        return Parse(commandName, HSLiftConfiguration.Data);
    }

    public static int Parse(string commandName, HSLiftConfigData d)
    {
        if (d == null || d.Floors == null) return -1;
        if (commandName == null || !commandName.StartsWith(Prefix, StringComparison.Ordinal)) return -1;
        int i;
        if (!int.TryParse(commandName.Substring(Prefix.Length), out i) || i < 0 || i >= d.Floors.Count) return -1;
        return i;
    }

    static void SetLabel(string key, string label)
    {
        try
        {
            var dict = Localization.Dictionary;
            string[] row;
            if (dict.TryGetValue(key, out row) && row != null && row.Length > 0 && row[row.Length - 1] == label) return;
            int columns = 0;
            string[] sample;
            if (dict.TryGetValue("blockcommand_open", out sample) && sample != null) columns = sample.Length;
            if (columns == 0) foreach (var v in dict.Values) { if (v != null) { columns = v.Length; break; } }
            if (columns == 0) return;
            row = new string[columns];
            for (int c = 0; c < columns; c++) row[c] = label;
            dict[key] = row;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Floor label failed for " + key + ": " + e.Message);
        }
    }
}
