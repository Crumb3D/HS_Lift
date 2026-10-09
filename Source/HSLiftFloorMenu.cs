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
        // A quick tap of E runs the first enabled entry, so with 3+ floors the floor the car is at goes
        // first and only opens the doors; the player has to hold E and pick where to go.
        int hereIndex = d.Floors.FindIndex(f => f.Y == d.CurrentY);
        bool hereFirst = d.Floors.Count > 2 && !moving && hereIndex >= 0;
        var order = new List<int>();
        if (hereFirst) order.Add(hereIndex);
        for (int i = d.Floors.Count - 1; i >= 0; i--)
        {
            if (hereFirst && i == hereIndex) continue;
            order.Add(i);
        }
        const int Wheel = 30;
        if (order.Count <= Wheel)
        {
            foreach (var i in order)
                list.Add(Command(d.Floors[i], i, i == hereIndex, i != hereIndex && !moving));
            return list;
        }
        int pageSize = Wheel - 2;
        int pages = (order.Count + pageSize - 1) / pageSize;
        int page;
        pageByLift.TryGetValue(d.ElevatorId ?? "", out page);
        if (page < 0) page = 0;
        if (page >= pages) page = pages - 1;
        pageByLift[d.ElevatorId ?? ""] = page;
        if (page > 0) list.Add(PageCommand(false));
        int start = page * pageSize;
        int end = Math.Min(start + pageSize, order.Count);
        for (int k = start; k < end; k++)
        {
            int i = order[k];
            list.Add(Command(d.Floors[i], i, i == hereIndex, i != hereIndex && !moving));
        }
        if (page < pages - 1) list.Add(PageCommand(true));
        return list;
    }

    static readonly Dictionary<string, int> pageByLift = new Dictionary<string, int>();

    public static bool TurnPage(string command, HSLiftConfigData d, EntityPlayerLocal player)
    {
        int dir = command == "hsliftFloorPageNext" ? 1 : command == "hsliftFloorPagePrev" ? -1 : 0;
        if (dir == 0) return false;
        string id = d != null && d.ElevatorId != null ? d.ElevatorId : "";
        int page;
        pageByLift.TryGetValue(id, out page);
        pageByLift[id] = Math.Max(0, page + dir);
        if (player != null) GameManager.ShowTooltip(player, "Hold E again for the other floors");
        return true;
    }

    static BlockActivationCommand PageCommand(bool next)
    {
        var text = next ? "hsliftFloorPageNext" : "hsliftFloorPagePrev";
        SetLabel("blockcommand_" + text, next ? "Lower floors" : "Higher floors");
        return new BlockActivationCommand(text, next ? "arrow_down" : "arrow_up", true, false, null);
    }

    static BlockActivationCommand Command(HSLiftFloor f, int index, bool here, bool enabled)
    {
        var text = Prefix + index;
        SetLabel("blockcommand_" + text, HSLiftConfiguration.FloorDisplayName(f.Name) + (here ? " (here)" : ""));
        return new BlockActivationCommand(text, Icon(f.Name), enabled, here, null);
    }

    // Sprites shipped in UIAtlases/UIAtlas: ui_game_symbol_hslift_<G|A-Z|B1-B99|0-99>.png
    static string Icon(string floorName)
    {
        var t = HSLiftConfiguration.FloorSignText(floorName);
        if (HasIcon(t)) return "hslift_" + t;
        return "electric_switch";
    }

    static bool HasIcon(string t)
    {
        if (string.IsNullOrEmpty(t)) return false;
        if (t.Length == 1) return char.IsLetterOrDigit(t[0]);
        int n;
        if (t[0] == 'B' && int.TryParse(t.Substring(1), out n)) return n >= 1 && n <= 99;
        return t.Length == 2 && int.TryParse(t, out n) && n >= 0 && n <= 99;
    }

    // Picking the floor the car is at just opens the doors; that is not worth a "not ready" tooltip.
    public static bool IsAlreadyHere(string problem)
    {
        return problem != null && problem.StartsWith("already at ", StringComparison.Ordinal);
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
