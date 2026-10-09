using System;

// Vanilla electrical only: a panel counts when it still exists and its tile entity reports power.
public static class HSLiftPower
{
    public static bool IsPanelPowered(Vector3i pos)
    {
        try
        {
            var world = GameManager.Instance.World;
            if (world == null) return false;
            if (!(world.GetBlock(pos).Block is BlockHSLiftOutsidePanel)) return false;
            var te = world.GetTileEntity(pos) as TileEntityPowered;
            if (te != null && te.IsPowered) return true;
            if (PowerManager.HasInstance)
            {
                var item = PowerManager.Instance.GetPowerItemByWorldPos(pos);
                if (item != null && item.IsPowered) return true;
            }
            return false;
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Power check failed at " + pos, e);
            return false;
        }
    }

    public static bool HasWorkingPower(out string problem)
    {
        return HasWorkingPower(HSLiftConfiguration.Data, out problem);
    }

    // Only this lift's registered panels. Another shaft's generator never counts.
    public static bool HasWorkingPower(HSLiftConfigData d, out string problem)
    {
        if (d == null || d.Panels == null || d.Panels.Count == 0)
        {
            problem = "needs outside button panels registered on at least two floors (have 0)";
            return false;
        }
        var world = GameManager.Instance.World;
        var floors = new System.Collections.Generic.HashSet<string>();
        foreach (var p in d.Panels)
        {
            if (world != null && world.GetChunkFromWorldPos(p.Pos) == null)
            {
                problem = "floor " + p.Stop + " panel at " + p.Pos + " is too far away to check (not loaded)";
                return false;
            }
            if (world == null || !(world.GetBlock(p.Pos).Block is BlockHSLiftOutsidePanel))
            {
                problem = "floor " + p.Stop + " panel at " + p.Pos + " is missing (re-register or: hslift panel clear)";
                return false;
            }
            floors.Add(p.Stop);
        }
        floors.Remove("?");
        if (floors.Count < 2)
        {
            problem = "needs outside button panels registered on at least two floors (have " + floors.Count + ")";
            return false;
        }
        return HasBatteryPower(d, out problem);
    }

    public const int WattsPerFloor = 5;

    public static int WattsFor(HSLiftConfigData d)
    {
        int n = d != null && d.Floors != null ? d.Floors.Count : 0;
        if (n < 1) n = 1;
        return n * WattsPerFloor;
    }

    // One panel on the battery bank draws 5W for every floor. Generators and solar do not count.
    // The bank then drains its batteries by that many watts, the same way a light does.
    public static void ApplyDraw(HSLiftConfigData d)
    {
        if (d == null || d.Panels == null || HSLiftNet.IsRemoteClient || !PowerManager.HasInstance) return;
        int watts = WattsFor(d);
        var items = new System.Collections.Generic.List<PowerItem>();
        PowerBatteryBank bank = null;
        foreach (var p in d.Panels)
        {
            if (p == null) continue;
            var item = PowerManager.Instance.GetPowerItemByWorldPos(p.Pos);
            if (item == null) continue;
            items.Add(item);
            if (bank == null) bank = SourceOf(item) as PowerBatteryBank;
        }
        var meter = bank != null ? Closest(bank, items) : null;
        foreach (var item in items)
        {
            ushort want = item == meter ? (ushort)watts : (ushort)0;
            if (item.RequiredPower != want) item.RequiredPower = want;
        }
    }

    public static bool HasBatteryPower(HSLiftConfigData d, out string problem)
    {
        problem = null;
        if (d == null) { problem = "no lift"; return false; }
        ApplyDraw(d);
        int watts = WattsFor(d);
        int floors = d.Floors != null ? d.Floors.Count : 0;
        PowerBatteryBank bank = null;
        PowerSource other = null;
        PowerItem meter = null;
        if (d.Panels != null && PowerManager.HasInstance)
        {
            var items = new System.Collections.Generic.List<PowerItem>();
            foreach (var p in d.Panels)
            {
                if (p == null) continue;
                var item = PowerManager.Instance.GetPowerItemByWorldPos(p.Pos);
                if (item == null) continue;
                items.Add(item);
                var src = SourceOf(item);
                var asBank = src as PowerBatteryBank;
                if (asBank != null && bank == null) bank = asBank;
                else if (src != null && other == null && asBank == null) other = src;
            }
            meter = bank != null ? Closest(bank, items) : null;
        }
        if (bank == null)
        {
            if (other != null)
                problem = "lifts only run from a battery bank, not " + SourceName(other) + " (" + watts + "W, " + floors + " floors x 5W)";
            else
                problem = "wire a battery bank to an outside panel (" + watts + "W, " + floors + " floors x 5W)";
            return false;
        }
        if (!bank.IsOn)
        {
            problem = "battery bank is switched off (" + watts + "W needed)";
            return false;
        }
        if (bank.MaxOutput < watts)
        {
            problem = "needs " + watts + "W (" + floors + " floors x 5W), battery bank supplies " + bank.MaxOutput + "W";
            return false;
        }
        if (meter == null || !IsPanelPowered(meter.Position))
        {
            problem = "battery bank has no charge left for " + watts + "W (" + floors + " floors x 5W)";
            return false;
        }
        return true;
    }

    static PowerSource SourceOf(PowerItem item)
    {
        var up = item;
        for (int i = 0; up != null && i < 32; i++)
        {
            var src = up as PowerSource;
            if (src != null) return src;
            up = up.Parent;
        }
        return null;
    }

    static PowerItem Closest(PowerBatteryBank bank, System.Collections.Generic.List<PowerItem> items)
    {
        PowerItem best = null;
        int bestHops = int.MaxValue;
        foreach (var item in items)
        {
            int hops = 0;
            var up = item != null ? item.Parent : null;
            bool found = false;
            for (int g = 0; up != null && g < 32; g++)
            {
                if (up == bank) { found = true; break; }
                up = up.Parent;
                hops++;
            }
            if (found && hops < bestHops) { best = item; bestHops = hops; }
        }
        return best;
    }

    static string SourceName(PowerSource src)
    {
        try
        {
            var world = GameManager.Instance.World;
            if (world != null)
            {
                var n = world.GetBlock(src.Position).Block.GetLocalizedBlockName();
                if (!string.IsNullOrEmpty(n)) return n;
            }
        }
        catch { }
        return "that power source";
    }

    // What the panel is wired to, walking up to the generator / battery bank / solar bank.
    static string WireState(World world, Vector3i pos)
    {
        try
        {
            var te = world.GetTileEntity(pos) as TileEntityPowered;
            if (te == null) return "panel has no power data";
            if (!te.HasParent()) return "panel not wired";
            PowerItem item = te.GetPowerItem();
            if (item == null && PowerManager.HasInstance) item = PowerManager.Instance.GetPowerItemByWorldPos(pos);
            var up = item != null ? item.Parent : null;
            for (int guard = 0; up != null && !(up is PowerSource) && up.Parent != null && guard < 32; guard++)
                up = up.Parent;
            var src = up as PowerSource;
            if (src == null) return "wired to " + te.GetParent() + ", no power source behind it";
            var name = world.GetBlock(src.Position).Block.GetLocalizedBlockName();
            if (!src.IsOn) return "wired to " + name + " at " + src.Position + ", which is switched OFF";
            return "wired to " + name + " at " + src.Position + ", on, giving " + src.CurrentPower + "W";
        }
        catch (Exception e)
        {
            return "power check failed: " + e.Message;
        }
    }

    // Lights follow the lift's battery draw, not a panel that is only wired and drawing nothing.
    public static bool CabinHasPower(HSLiftConfigData d)
    {
        string problem;
        return HasBatteryPower(d, out problem);
    }
}
