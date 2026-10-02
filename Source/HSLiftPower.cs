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
        bool anyPowered = false;
        foreach (var p in d.Panels)
        {
            if (world == null || !(world.GetBlock(p.Pos).Block is BlockHSLiftOutsidePanel))
            {
                problem = "floor " + p.Stop + " panel at " + p.Pos + " is missing (re-register or: hslift panel clear)";
                return false;
            }
            floors.Add(p.Stop);
            if (!anyPowered && IsPanelPowered(p.Pos)) anyPowered = true;
        }
        floors.Remove("?");
        if (floors.Count < 2)
        {
            problem = "needs outside button panels registered on at least two floors (have " + floors.Count + ")";
            return false;
        }
        if (!anyPowered)
        {
            problem = "no power: wire a generator or battery bank to any outside panel on this lift";
            return false;
        }
        problem = null;
        return true;
    }
}
