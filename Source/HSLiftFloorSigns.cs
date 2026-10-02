using System;
using System.Collections.Generic;

// Vanilla wall letters/numbers (woodShapes:signLetter_g, signNumber_1, …).
// Place a G (or 1, 2, …) near each outside door (ped elevator or vehicle garage); they switch to the car's floor.
public static class HSLiftFloorSigns
{
    const int Reach = 3;
    static HSLiftConfigData D { get { return HSLiftConfiguration.Data; } }

    static string lastLiftId;
    static string lastSignToken;

    public static void Invalidate()
    {
        lastLiftId = null;
        lastSignToken = null;
    }

    public static void UpdateToCurrentFloor(World world)
    {
        Apply(world, HSLiftConfiguration.FloorAt(D.CurrentY));
    }

    // Going down 2→G: show 2 until the car reaches 1, then 1, then G. Same going up.
    public static void UpdatePassing(World world, float carY, int fromY, int targetY)
    {
        Apply(world, FloorPassing(carY, fromY, targetY));
    }

    static HSLiftFloor FloorPassing(float carY, int fromY, int targetY)
    {
        if (D.Floors == null || D.Floors.Count == 0) return null;
        HSLiftFloor best = null;
        if (targetY < fromY)
        {
            foreach (var f in D.Floors)
                if (f.Y + 0.05f >= carY && (best == null || f.Y < best.Y)) best = f;
        }
        else
        {
            foreach (var f in D.Floors)
                if (f.Y - 0.05f <= carY && (best == null || f.Y > best.Y)) best = f;
        }
        return best;
    }

    static void Apply(World world, HSLiftFloor floor)
    {
        if (world == null || D == null || !D.HasCar || floor == null) return;
        var token = HSLiftConfiguration.FloorSignToken(floor.Name);
        if (D.ElevatorId == lastLiftId && string.Equals(lastSignToken, token, StringComparison.OrdinalIgnoreCase)) return;
        var shape = ShapeForFloor(floor.Name);
        if (shape == null) return;

        var changes = new List<BlockChangeInfo>();
        var seen = new HashSet<Vector3i>();
        foreach (var a in Anchors(world))
            ScanAround(world, a, shape, changes, seen);
        lastLiftId = D.ElevatorId;
        lastSignToken = token;
        if (changes.Count == 0) return;
        world.SetBlocksRPC(changes);
        HSLiftDebug.Verbose("Floor signs: " + changes.Count + " set to " + floor.Name + " (" + HSLiftConfiguration.FloorDisplayName(floor.Name) + ")");
    }

    static IEnumerable<Vector3i> Anchors(World world)
    {
        int top = Math.Max(D.LowerY, D.HasUpper ? D.UpperY : D.LowerY) + Math.Max(1, D.DoorHeight);
        foreach (var parent in HSLiftDoors.FindDoors(world, D.LowerY, top))
        {
            yield return parent;
            foreach (var c in HSLiftDoors.DoorCells(parent, world.GetBlock(parent)))
                yield return c;
        }
        if (D.Panels == null) yield break;
        foreach (var p in D.Panels)
            yield return p.Pos;
    }

    static void ScanAround(World world, Vector3i center, string shape, List<BlockChangeInfo> changes, HashSet<Vector3i> seen)
    {
        int yHi = Math.Max(2, D.DoorHeight);
        for (int dy = -1; dy <= yHi; dy++)
        for (int dx = -Reach; dx <= Reach; dx++)
        for (int dz = -Reach; dz <= Reach; dz++)
        {
            var pos = new Vector3i(center.x + dx, center.y + dy, center.z + dz);
            if (!seen.Add(pos)) continue;
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair || bv.ischild) continue;
            string name = bv.Block.GetBlockName();
            char glyph;
            if (!TryGlyph(name, out glyph) || !IsThisLiftFloorGlyph(glyph)) continue;
            var next = ReplaceShape(name, shape);
            if (next == null || string.Equals(next, name, StringComparison.OrdinalIgnoreCase)) continue;
            var nv = Block.GetBlockValue(next);
            if (nv.isair || nv.type == 0) continue;
            nv.rotation = bv.rotation;
            nv.damage = bv.damage;
            var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
            if (chunk == null) continue;
            int lx = World.toBlockXZ(pos.x), ly = World.toBlockY(pos.y), lz = World.toBlockXZ(pos.z);
            changes.Add(new BlockChangeInfo(pos, nv, chunk.GetDensity(lx, ly, lz), chunk.GetTextureFullArray(lx, ly, lz)));
        }
    }

    static bool IsThisLiftFloorGlyph(char glyph)
    {
        foreach (var f in D.Floors)
        {
            if (string.IsNullOrEmpty(f.Name)) continue;
            var stored = f.Name.Trim();
            if (char.ToUpperInvariant(stored[0]) == char.ToUpperInvariant(glyph)) return true;
            var shown = HSLiftConfiguration.FloorSignToken(stored);
            if (!string.IsNullOrEmpty(shown) && char.ToUpperInvariant(shown[0]) == char.ToUpperInvariant(glyph)) return true;
        }
        return false;
    }

    static string ShapeForFloor(string name)
    {
        var token = HSLiftConfiguration.FloorSignToken(name);
        if (string.IsNullOrEmpty(token)) return null;
        char ch = token[0];
        if (char.IsDigit(ch)) return "signNumber_" + ch;
        if (char.IsLetter(ch)) return "signLetter_" + char.ToLowerInvariant(ch);
        return null;
    }

    static bool TryGlyph(string blockName, out char glyph)
    {
        glyph = '\0';
        if (string.IsNullOrEmpty(blockName)) return false;
        var n = blockName;
        int c = n.LastIndexOf(':');
        if (c >= 0) n = n.Substring(c + 1);
        if (n.StartsWith("signLetter_", StringComparison.OrdinalIgnoreCase) && n.Length == 12)
        {
            glyph = n[11];
            return char.IsLetter(glyph);
        }
        if (n.StartsWith("signNumber_", StringComparison.OrdinalIgnoreCase) && n.Length == 12)
        {
            glyph = n[11];
            return char.IsDigit(glyph);
        }
        return false;
    }

    static string ReplaceShape(string blockName, string shape)
    {
        int c = blockName.LastIndexOf(':');
        if (c < 0) return shape;
        return blockName.Substring(0, c + 1) + shape;
    }
}
