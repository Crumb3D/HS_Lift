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
            var parts = new System.Collections.Generic.List<string>();
            foreach (var p in d.Panels) parts.Add(p.Stop + " " + WireState(world, p.Pos));
            problem = "no power: wire a generator or battery bank to any outside panel on this lift (" + string.Join("; ", parts.ToArray()) + ")";
            return false;
        }
        problem = null;
        return true;
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

    // Lights in this cabin: any registered outside panel on this lift is wired.
    public static bool CabinHasPower(HSLiftConfigData d)
    {
        if (d == null || d.Panels == null) return false;
        for (int i = 0; i < d.Panels.Count; i++)
        {
            if (d.Panels[i] == null) continue;
            if (IsPanelPowered(d.Panels[i].Pos)) return true;
        }
        return false;
    }
}
