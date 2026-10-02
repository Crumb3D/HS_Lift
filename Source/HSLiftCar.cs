using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class HSLiftCell
{
    public int Dx, Dy, Dz;
    public BlockValue Bv;
    public sbyte Density;
    public TextureFullArray Tex;
    [JsonIgnore]
    public TileEntity Te;
}

public class HSLiftJournalCell
{
    public int Dx, Dy, Dz;
    public uint Raw;
    public int Damage;
    public sbyte Density;
    public long[] Tex;
}

public class HSLiftJournal
{
    public string ElevatorId;
    public int MinX, MinZ, BaseY;
    public List<HSLiftJournalCell> Cells = new List<HSLiftJournalCell>();
}

// Everything that touches the car's own blocks. Nothing else in the world is ever changed here.
public static class HSLiftCar
{
    static HSLiftConfigData D { get { return HSLiftConfiguration.Data; } }

    public static bool InBox(Vector3i p, int baseY)
    {
        return InRect(p.x, p.z) && p.y >= baseY && p.y < baseY + D.SizeY && !IsExcludedCell(p.x, p.y - baseY, p.z);
    }

    static bool InRect(int x, int z)
    {
        return x >= D.MinX && x < D.MinX + D.SizeX && z >= D.MinZ && z < D.MinZ + D.SizeZ;
    }

    // Where a rider counts as standing in the car: the footprint minus any column holding a stationary landing (P).
    public static bool InFootprint(int x, int z)
    {
        return InRect(x, z) && !IsExcluded(x, z);
    }

    // Column holds stationary landing blocks (P) at some rows. The car passes through such a column while travelling,
    // like a car passing each landing; only its stop position must be empty.
    public static bool IsExcluded(int x, int z)
    {
        if (D.ExcludedColumns == null) return false;
        foreach (var c in D.ExcludedColumns)
            if (c != null && c.Length >= 2 && c[0] == x && c[1] == z) return true;
        return false;
    }

    // Entries are [x, z] (every row) or [x, z, dyMin, dyMax] (rows relative to the car floor).
    public static bool IsExcludedCell(int x, int dy, int z)
    {
        if (D.ExcludedColumns == null) return false;
        foreach (var c in D.ExcludedColumns)
        {
            if (c == null || c.Length < 2 || c[0] != x || c[1] != z) continue;
            if (c.Length < 4 || (dy >= c[2] && dy <= c[3])) return true;
        }
        return false;
    }

    static bool IsCarPerimeter(int x, int z)
    {
        if (!D.HasCar) return false;
        return x == D.MinX || x == D.MinX + D.SizeX - 1
            || z == D.MinZ || z == D.MinZ + D.SizeZ - 1;
    }

    // Double-door volume reaches one cell into the cabin. That inner floor must ride.
    public static int DropInteriorFloorExcludes()
    {
        if (D == null || !D.HasCar || D.ExcludedColumns == null) return 0;
        return D.ExcludedColumns.RemoveAll(e =>
            e != null && e.Length >= 4
            && e[2] == 0 && e[3] == 0
            && InRect(e[0], e[1])
            && !IsCarPerimeter(e[0], e[1]));
    }

    // Doorway floor between the car and the exit stays put. Any block shape. Rows above (plate corner, etc.) still ride.
    public static bool EnsureExcludedCell(int x, int z, int dyMin, int dyMax)
    {
        if (D.ExcludedColumns == null) D.ExcludedColumns = new List<int[]>();
        foreach (var e in D.ExcludedColumns)
        {
            if (e == null || e.Length < 2 || e[0] != x || e[1] != z) continue;
            if (e.Length < 4) return false;
            int lo = Math.Min(e[2], dyMin), hi = Math.Max(e[3], dyMax);
            if (lo == e[2] && hi == e[3]) return false;
            e[2] = lo;
            e[3] = hi;
            return true;
        }
        D.ExcludedColumns.Add(new[] { x, z, dyMin, dyMax });
        return true;
    }

    public static int AutoExcludeDoorPlatforms(World world)
    {
        if (world == null || D == null || !D.HasCar) return 0;
        int added = 0;
        var seenY = new HashSet<int>();
        seenY.Add(D.CurrentY);
        if (D.Floors != null)
            foreach (var f in D.Floors) seenY.Add(f.Y);
        foreach (var y in seenY)
        {
            int top = y + Math.Max(1, D.DoorHeight) - 1;
            foreach (var parent in HSLiftDoors.FindDoors(world, y, top))
            {
                foreach (var cell in HSLiftDoors.DoorCells(parent, world.GetBlock(parent)))
                {
                    if (!InFootprint(cell.x, cell.z)) continue;
                    if (!IsCarPerimeter(cell.x, cell.z)) continue;
                    if (EnsureExcludedCell(cell.x, cell.z, 0, 0)) added++;
                }
            }
        }
        return added;
    }

    static bool IsInsidePanel(Block block)
    {
        return block is BlockHSLiftInsidePanel;
    }

    // Vehicle: the 2-tall inside panel sits on the pad and rides; walls and garage doors do not.
    static bool VehiclePanelFits(Vector3i parent, Vector3i cell, int baseY)
    {
        return D.IsVehicleType && InRect(parent.x, parent.z)
            && parent.y >= baseY && parent.y <= baseY + 1
            && InRect(cell.x, cell.z) && cell.y >= baseY && cell.y <= parent.y + 1;
    }

    static int ColumnHeight(List<HSLiftCell> cells, int x, int z)
    {
        int h = Math.Max(1, D.SizeY);
        if (cells == null) return h;
        foreach (var c in cells)
            if (D.MinX + c.Dx == x && D.MinZ + c.Dz == z) h = Math.Max(h, c.Dy + 1);
        return h;
    }

    static string AddCaptured(World world, List<HSLiftCell> cells, Vector3i pos, int baseY)
    {
        var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
        if (chunk == null) return "chunk not loaded at " + pos;
        var bv = world.GetBlock(pos);
        int lx = World.toBlockXZ(pos.x), ly = World.toBlockY(pos.y), lz = World.toBlockXZ(pos.z);
        TileEntity teCopy = null;
        if (bv.Block.HasTileEntity && !bv.ischild)
        {
            try
            {
                var te = world.GetTileEntity(pos);
                if (te != null) teCopy = te.Clone();
            }
            catch (Exception e)
            {
                HSLiftDebug.Warn("Could not copy tile entity at " + pos + ": " + e.Message);
            }
        }
        cells.Add(new HSLiftCell
        {
            Dx = pos.x - D.MinX,
            Dy = pos.y - baseY,
            Dz = pos.z - D.MinZ,
            Bv = bv,
            Density = chunk.GetDensity(lx, ly, lz),
            Tex = chunk.GetTextureFullArray(lx, ly, lz),
            Te = teCopy
        });
        return null;
    }

    public static bool HasInsidePanel(World world, int baseY)
    {
        if (world == null || !D.HasCar) return false;
        int h = D.IsVehicleType ? Math.Max(2, D.SizeY) : D.SizeY;
        for (int dy = 0; dy < h; dy++)
        for (int dx = 0; dx < D.SizeX; dx++)
        for (int dz = 0; dz < D.SizeZ; dz++)
            if (!IsExcludedCell(D.MinX + dx, dy, D.MinZ + dz) && world.GetBlock(new Vector3i(D.MinX + dx, baseY + dy, D.MinZ + dz)).Block is BlockHSLiftInsidePanel) return true;
        return false;
    }

    public static string ValidateCarAt(World world, int baseY)
    {
        List<HSLiftCell> cells;
        int found;
        return CaptureParked(world, out cells, out found);
    }

    // Capture at CurrentY, or at whichever registered floor actually has the car
    // (rebuild / leftover park after a broken move).
    public static string CaptureParked(World world, out List<HSLiftCell> cells, out int baseY)
    {
        baseY = D.CurrentY;
        var err = Capture(world, baseY, out cells);
        if (err == null) return null;
        if (D.Floors == null) return err;
        int oldY = D.CurrentY;
        foreach (var f in D.Floors)
        {
            if (f.Y == oldY) continue;
            List<HSLiftCell> found;
            if (Capture(world, f.Y, out found) != null || found == null || found.Count == 0) continue;
            D.CurrentY = f.Y;
            HSLiftConfiguration.Save();
            cells = found;
            baseY = f.Y;
            HSLiftDebug.Info("Car found at floor " + f.Name + " (Y" + f.Y + "), not Y" + oldY + "; park floor updated.");
            return null;
        }
        return err;
    }

    public static string Capture(World world, int baseY, out List<HSLiftCell> cells)
    {
        cells = new List<HSLiftCell>();
        if (world == null) return "no world";
        if (!D.HasCar) return "car not set (hslift corner1 / corner2)";
        if (DropInteriorFloorExcludes() + AutoExcludeDoorPlatforms(world) > 0) HSLiftConfiguration.Save();
        for (int dy = 0; dy < (D.IsVehicleType ? Math.Max(2, D.SizeY) : D.SizeY); dy++)
        for (int dx = 0; dx < D.SizeX; dx++)
        for (int dz = 0; dz < D.SizeZ; dz++)
        {
            if (IsExcludedCell(D.MinX + dx, dy, D.MinZ + dz)) continue;
            var pos = new Vector3i(D.MinX + dx, baseY + dy, D.MinZ + dz);
            var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
            if (chunk == null) return "chunk not loaded at " + pos;
            var bv = world.GetBlock(pos);
            if (bv.isair) continue;
            var block = bv.Block;
            var name = block.GetBlockName();
            // Vehicle: garage / roll-up doors stay at the landing. Only the floor slab and the inside panel ride.
            if (D.IsVehicleType && HSLiftDoors.IsGarageOrRollUpName(name)) continue;
            // Sheets / extra door-trim stay at the landing. Plate corner + door trim 1m / corner ride with the car.
            if (IsPassThrough(bv) && !IsRidePiece(bv)) continue;
            if (dy >= D.SizeY && !IsInsidePanel(block)) continue;
            if (block.shape.IsTerrain()) return "terrain (" + name + ") " + (D.IsVehicleType ? "on the platform" : "inside the car box") + " at " + pos;
            if (bv.ischild)
            {
                if (D.IsVehicleType && HSLiftDoors.IsGarageOrRollUpName(name)) continue;
                var parent = block.multiBlockPos.GetParentPos(pos, bv);
                if (IsInsidePanel(block) && VehiclePanelFits(parent, pos, baseY)) continue;
                if (!InBox(parent, baseY)) return name + " at " + pos + " sticks out of the " + (D.IsVehicleType ? "platform" : "car box");
                continue;
            }
            if (block.isOversized) return name + " at " + pos + " is an oversized block (not supported in v0.1)";
            if (block.isMultiBlock)
            {
                for (int i = 0; i < block.multiBlockPos.Length; i++)
                {
                    var c = pos + block.multiBlockPos.Get(i, bv.type, bv.rotation);
                    if (InBox(c, baseY) || (IsInsidePanel(block) && VehiclePanelFits(pos, c, baseY))) continue;
                    return name + " at " + pos + " sticks out of the " + (D.IsVehicleType ? "platform" : "car box");
                }
            }
            var add = AddCaptured(world, cells, pos, baseY);
            if (add != null) return add;
            if (IsInsidePanel(block) && block.isMultiBlock)
            {
                for (int i = 0; i < block.multiBlockPos.Length; i++)
                {
                    var extra = pos + block.multiBlockPos.Get(i, bv.type, bv.rotation);
                    if (InBox(extra, baseY)) continue;
                    if (!VehiclePanelFits(pos, extra, baseY)) continue;
                    add = AddCaptured(world, cells, extra, baseY);
                    if (add != null) return add;
                }
            }
        }
        if (cells.Count == 0) return D.IsVehicleType ? "no blocks on the platform at Y" + baseY : "no blocks in the car box at Y" + baseY;
        return null;
    }

    // Cells the car sweeps between the two positions, excluding where it is now.
    // Plate corner / door-trim ride pieces may travel through any block.
    public static string CheckPath(World world, int fromY, int toY, List<HSLiftCell> cells)
    {
        if (cells == null) return null;
        foreach (var c in cells)
        {
            if (IsRidePiece(c.Bv)) continue;
            int x = D.MinX + c.Dx, z = D.MinZ + c.Dz;
            if (IsExcludedCell(x, c.Dy, z)) continue;
            int yFrom = fromY + c.Dy, yTo = toY + c.Dy;
            int lo = Math.Min(yFrom, yTo), hi = Math.Max(yFrom, yTo);
            for (int y = lo; y <= hi; y++)
            {
                if (y == yFrom) continue;
                // Floor/wall cells sweep through the parked cabin (other dy at this XZ). That is the car, not a shaft block.
                if (InBox(new Vector3i(x, y, z), fromY)) continue;
                var err = CheckClear(world, new Vector3i(x, y, z), "is blocking the lift shaft", true);
                if (err != null) return err;
            }
        }
        return null;
    }

    // Car blocks riding in landing columns skip the shaft check, so their stop position is checked here.
    public static string CheckDestination(World world, int fromY, int toY, List<HSLiftCell> cells)
    {
        foreach (var c in cells)
        {
            if (IsRidePiece(c.Bv)) continue;
            var p = Pos(c, toY);
            if (!IsExcluded(p.x, p.z) || InBox(p, fromY)) continue;
            var err = CheckClear(world, p, "is in the way where the car stops", true);
            if (err != null) return err;
        }
        return null;
    }

    public static string CheckRow(World world, int y, string what, int targetY)
    {
        bool pass = true;
        for (int x = D.MinX; x < D.MinX + D.SizeX; x++)
        for (int z = D.MinZ; z < D.MinZ + D.SizeZ; z++)
        {
            if (IsExcluded(x, z)) continue;
            var err = CheckClear(world, new Vector3i(x, y, z), what, pass);
            if (err != null) return err;
        }
        return null;
    }

    public static string CheckClear(World world, Vector3i pos, string what, bool allowPassThrough = false)
    {
        if (world.GetChunkFromWorldPos(pos) == null) return "part of the shaft is too far away to load (" + Where(pos) + ")";
        var bv = world.GetBlock(pos);
        if (bv.isair) return null;
        var name = bv.Block.GetBlockName();
        if (D.IsVehicleType && HSLiftDoors.IsGarageOrRollUpName(name)) return null;
        if (allowPassThrough && (IsPassThrough(bv) || IsRidePiece(bv))) return null;
        HSLiftDebug.Verbose("Obstruction " + name + " at " + pos);
        return DisplayName(bv) + " " + what + " (" + Where(pos) + ")";
    }

    static string DisplayName(BlockValue bv)
    {
        try
        {
            var n = bv.Block.GetLocalizedBlockName();
            if (!string.IsNullOrEmpty(n)) return n;
        }
        catch (Exception) { }
        return bv.Block.GetBlockName();
    }

    // "3 blocks above floor G, at 2170 68 -1742"
    static string Where(Vector3i pos)
    {
        HSLiftFloor below = null;
        foreach (var f in D.Floors) if (f.Y <= pos.y) below = f;
        var rel = below == null ? "below the lowest floor"
            : pos.y == below.Y ? "at floor " + below.Name
            : (pos.y - below.Y) + (pos.y - below.Y == 1 ? " block" : " blocks") + " above floor " + below.Name;
        return rel + ", at " + pos.x + " " + pos.y + " " + pos.z;
    }

    static bool IsFullCopyAt(World world, int baseY, List<HSLiftCell> cells)
    {
        foreach (var c in cells)
        {
            var p = Pos(c, baseY);
            if (world.GetChunkFromWorldPos(p) == null) return false;
            var bv = world.GetBlock(p);
            if (bv.type != c.Bv.type || bv.rotation != c.Bv.rotation) return false;
        }
        return true;
    }

    // A complete block-for-block copy of the car in its own path can only be a car left behind by an interrupted move.
    public static int RemoveLeftoverCopies(World world, int fromY, int toY, List<HSLiftCell> cells)
    {
        int lo = Math.Min(fromY, toY) - D.SizeY + 1, hi = Math.Max(fromY, toY) + D.SizeY - 1;
        int removed = 0;
        for (int y = lo; y <= hi; y++)
        {
            if (Math.Abs(y - fromY) < D.SizeY || !IsFullCopyAt(world, y, cells)) continue;
            var changes = new List<BlockChangeInfo>();
            foreach (var c in cells) changes.Add(new BlockChangeInfo(Pos(c, y), BlockValue.Air, MarchingCubes.DensityAir));
            world.SetBlocksRPC(changes);
            HSLiftDebug.Info("Removed a leftover copy of the lift car at Y" + y + " (" + cells.Count + " blocks, left by an interrupted move).");
            removed++;
            y += D.SizeY - 1;
        }
        return removed;
    }

    public static bool IsPassThrough(BlockValue bv)
    {
        if (bv.Block == null) return false;
        if (IsPassThroughName(bv.Block.GetBlockName())) return true;
        try
        {
            if (bv.Block.shape != null && IsPassThroughName(bv.Block.shape.GetName())) return true;
        }
        catch { }
        try
        {
            if (IsPassThroughName(bv.Block.GetLocalizedBlockName())) return true;
        }
        catch { }
        return false;
    }

    public static bool IsPassThrough(string blockName)
    {
        return IsPassThroughName(blockName);
    }

    static bool IsPassThroughName(string blockName)
    {
        if (string.IsNullOrEmpty(blockName) || D.PassThroughBlocks == null) return false;
        foreach (var p in D.PassThroughBlocks)
        {
            if (string.IsNullOrEmpty(p)) continue;
            if (blockName.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var compact = Compact(blockName);
            var token = Compact(p);
            if (token.Length > 0 && compact.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // On the car: ride with it and sweep through landing blocks. Not left behind like sheets.
    public static bool IsRidePiece(BlockValue bv)
    {
        if (bv.Block == null) return false;
        if (IsRidePieceName(bv.Block.GetBlockName())) return true;
        try
        {
            if (bv.Block.shape != null && IsRidePieceName(bv.Block.shape.GetName())) return true;
        }
        catch { }
        try
        {
            if (IsRidePieceName(bv.Block.GetLocalizedBlockName())) return true;
        }
        catch { }
        return false;
    }

    static bool IsRidePieceName(string blockName)
    {
        if (string.IsNullOrEmpty(blockName)) return false;
        var compact = Compact(blockName);
        return compact.IndexOf("plateCorner", StringComparison.OrdinalIgnoreCase) >= 0
            || compact.IndexOf("doorTrim1m", StringComparison.OrdinalIgnoreCase) >= 0
            || compact.IndexOf("doorTrimCorner", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string Compact(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var chars = new char[s.Length];
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == ' ' || c == '-' || c == '_') continue;
            chars[n++] = c;
        }
        return new string(chars, 0, n);
    }

    // 0 walls/doors (brace the shaft first), 1 floor, 2 ceiling, 3 interior.
    static int PlaceRank(HSLiftCell c)
    {
        if (c.Bv.Block != null && HSLiftDoors.IsElevatorDoor(c.Bv.Block)) return 0;
        if (D.IsVehicleType || D.SizeY <= 1) return 1;
        bool edge = c.Dx == 0 || c.Dx == D.SizeX - 1 || c.Dz == 0 || c.Dz == D.SizeZ - 1;
        if (edge && c.Dy > 0) return 0;
        if (c.Dy == 0) return 1;
        if (c.Dy == D.SizeY - 1) return 2;
        return 3;
    }

    // Separate RPCs so 7DTD stability sees walls against the shaft before the floor exists.
    // Snapshot the 1-block ring outside the car and put back anything a cube/door RPC knocked off (lintel plates).
    static void ApplyLayered(World world, int baseY, List<HSLiftCell> cells, bool remove)
    {
        var ring = SnapshotKeepers(world, baseY);
        var groups = new List<HSLiftCell>[] {
            new List<HSLiftCell>(), new List<HSLiftCell>(), new List<HSLiftCell>(), new List<HSLiftCell>()
        };
        foreach (var c in cells) groups[PlaceRank(c)].Add(c);
        int[] order = remove ? new[] { 3, 2, 1, 0 } : new[] { 0, 1, 2, 3 };
        foreach (var i in order)
        {
            if (groups[i].Count == 0) continue;
            var changes = new List<BlockChangeInfo>();
            foreach (var c in groups[i])
            {
                var p = Pos(c, baseY);
                if (remove) changes.Add(new BlockChangeInfo(p, BlockValue.Air, MarchingCubes.DensityAir));
                else changes.Add(new BlockChangeInfo(p, c.Bv, c.Density, c.Tex));
            }
            world.SetBlocksRPC(changes);
        }
        RestoreKeepers(world, ring);
    }

    struct OutsideCell
    {
        public Vector3i Pos;
        public BlockValue Bv;
        public sbyte Density;
        public TextureFullArray Tex;
    }

    static List<OutsideCell> SnapshotKeepers(World world, int baseY)
    {
        var list = new List<OutsideCell>();
        if (world == null || !D.HasCar) return list;
        int y1 = baseY + D.SizeY;
        for (int y = baseY; y <= y1; y++)
        for (int x = D.MinX - 1; x <= D.MinX + D.SizeX; x++)
        for (int z = D.MinZ - 1; z <= D.MinZ + D.SizeZ; z++)
        {
            var pos = new Vector3i(x, y, z);
            var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
            if (chunk == null) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair || bv.ischild) continue;
            if (HSLiftDoors.IsCandidateDoor(bv.Block)) continue;
            bool inside = InBox(pos, baseY);
            bool ring = !InFootprint(x, z) && D.DistOutsideXZ(x, z) == 1;
            if (inside && (!IsPassThrough(bv) || IsRidePiece(bv))) continue;
            if (!inside && !ring) continue;
            int lx = World.toBlockXZ(pos.x), ly = World.toBlockY(pos.y), lz = World.toBlockXZ(pos.z);
            list.Add(new OutsideCell
            {
                Pos = pos,
                Bv = bv,
                Density = chunk.GetDensity(lx, ly, lz),
                Tex = chunk.GetTextureFullArray(lx, ly, lz)
            });
        }
        return list;
    }

    static void RestoreKeepers(World world, List<OutsideCell> ring)
    {
        if (ring == null || ring.Count == 0) return;
        var changes = new List<BlockChangeInfo>();
        foreach (var s in ring)
        {
            if (world.GetChunkFromWorldPos(s.Pos) == null) continue;
            if (!world.GetBlock(s.Pos).isair) continue;
            changes.Add(new BlockChangeInfo(s.Pos, s.Bv, s.Density, s.Tex));
        }
        if (changes.Count > 0)
        {
            world.SetBlocksRPC(changes);
            HSLiftDebug.Verbose("Put back " + changes.Count + " sheet/plate(s) knocked off by the car moving");
        }
    }

    public static string CheckShaftSupport(World world, int baseY)
    {
        if (world == null || D.IsVehicleType || D.SizeY <= 1) return null;
        int sides = 0;
        if (SideHasSupport(world, baseY, -1, 0)) sides++;
        if (SideHasSupport(world, baseY, 1, 0)) sides++;
        if (SideHasSupport(world, baseY, 0, -1)) sides++;
        if (SideHasSupport(world, baseY, 0, 1)) sides++;
        if (sides >= 2) return null;
        return "need at least 2 shaft walls against the car (they hold the cabin up); found " + sides;
    }

    static bool SideHasSupport(World world, int baseY, int nx, int nz)
    {
        int y0 = baseY + 1, y1 = baseY + D.SizeY - 1;
        if (y1 < y0) y1 = y0;
        if (nx != 0)
        {
            int x = nx < 0 ? D.MinX - 1 : D.MinX + D.SizeX;
            for (int y = y0; y <= y1; y++)
            for (int z = D.MinZ; z < D.MinZ + D.SizeZ; z++)
                if (!world.GetBlock(new Vector3i(x, y, z)).isair) return true;
            return false;
        }
        int zWall = nz < 0 ? D.MinZ - 1 : D.MinZ + D.SizeZ;
        for (int y = y0; y <= y1; y++)
        for (int x = D.MinX; x < D.MinX + D.SizeX; x++)
            if (!world.GetBlock(new Vector3i(x, y, zWall)).isair) return true;
        return false;
    }

    public static void RemoveFromWorld(World world, int baseY, List<HSLiftCell> cells)
    {
        ApplyLayered(world, baseY, cells, true);
        HSLiftDebug.Verbose("Lifted " + cells.Count + " car blocks out of the grid at Y" + baseY + " (floor/ceiling first, walls last)");
    }

    // Only places when every target cell (including multi-block children) is air.
    public static string PlaceInWorld(World world, int baseY, List<HSLiftCell> cells)
    {
        foreach (var c in cells)
        {
            if (IsRidePiece(c.Bv)) continue;
            var p = Pos(c, baseY);
            var err = CheckClear(world, p, "is in the way where the car stops", true);
            if (err != null) return err;
            var block = c.Bv.Block;
            if (!block.isMultiBlock) continue;
            for (int i = 0; i < block.multiBlockPos.Length; i++)
            {
                var cp = p + block.multiBlockPos.Get(i, c.Bv.type, c.Bv.rotation);
                if (cp == p) continue;
                err = CheckClear(world, cp, "is in the way where the car stops", true);
                if (err != null) return err;
            }
        }
        var place = new List<HSLiftCell>();
        foreach (var c in cells)
        {
            var dest = world.GetBlock(Pos(c, baseY));
            if (!dest.isair && IsRidePiece(c.Bv)) continue;
            if (!dest.isair && IsPassThrough(dest) && !IsRidePiece(c.Bv)) continue;
            place.Add(c);
        }
        ApplyLayered(world, baseY, place, false);
        RestoreTileEntities(world, baseY, place);
        HSLiftDebug.Verbose("Placed " + place.Count + " car blocks back at Y" + baseY + " (walls first, then floor and ceiling; " + (cells.Count - place.Count) + " left as pass-through sheets)");
        return null;
    }

    static void RestoreTileEntities(World world, int baseY, List<HSLiftCell> cells)
    {
        if (world == null || cells == null) return;
        foreach (var c in cells)
        {
            if (c.Te == null) continue;
            var p = Pos(c, baseY);
            var dest = world.GetTileEntity(p);
            if (dest == null) continue;
            try
            {
                dest.CopyFrom(c.Te);
                dest.localChunkPos = new Vector3i(World.toBlockXZ(p.x), World.toBlockY(p.y), World.toBlockXZ(p.z));
                dest.SetModified();
            }
            catch (Exception e)
            {
                HSLiftDebug.Warn("Could not restore " + c.Bv.Block.GetBlockName() + " at " + p + ": " + e.Message);
            }
        }
    }

    static Vector3i Pos(HSLiftCell c, int baseY)
    {
        return new Vector3i(D.MinX + c.Dx, baseY + c.Dy, D.MinZ + c.Dz);
    }

    // Moving copy of the car: block models (with paint) plus mesh colliders on the layer the player controller rides.
    public static GameObject BuildVisual(World world, List<HSLiftCell> cells, bool withColliders, int baseY)
    {
        if (GameManager.IsDedicatedServer)
        {
            var empty = new GameObject("HSLiftCar_" + D.ElevatorId);
            var erb = empty.AddComponent<Rigidbody>();
            erb.isKinematic = true;
            erb.useGravity = false;
            empty.transform.position = UnityPos(baseY);
            return empty;
        }
        var root = new GameObject("HSLiftCar_" + D.ElevatorId);
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Start where the real blocks are so world-space fittings (panel backing) line up with their cells.
        root.transform.position = UnityPos(baseY);
        var off = new Vector3(D.ModelOffset[0], D.ModelOffset[1], D.ModelOffset[2]);
        int models = 0, boxCols = 0;
        int layer = withColliders ? DetectBlockLayer(baseY, cells) : 0;
        var pin = root.AddComponent<HSLiftPinnedLooks>();

        foreach (var c in cells)
        {
            var holder = new GameObject("cell");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = new Vector3(c.Dx, c.Dy, c.Dz) + off;
            try
            {
                var ic = ItemClass.GetForId(c.Bv.ToItemType());
                Transform model = null;
                if (ic != null)
                {
                    var worldPos = new Vector3(D.MinX + c.Dx, baseY + c.Dy, D.MinZ + c.Dz);
                    model = ic.CloneModel(world, c.Bv.ToItemValue(), worldPos, holder.transform, _textureFullArray: c.Tex);
                }
                if (model != null)
                {
                    models++;
                    model.localPosition = Vector3.zero;
                    model.localRotation = c.Bv.Block.shape.GetRotation(c.Bv);
                    foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                    foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                    HideFocusHelpers(model);
                    var cellPos = new Vector3i(D.MinX + c.Dx, baseY + c.Dy, D.MinZ + c.Dz);
                    // A cloned door animator starts in its default pose and would swing open then shut on departure.
                    var te = world.GetTileEntity(cellPos) as TileEntityComposite;
                    if (te != null && te.GetFeature<TEFeatureDoor>() != null)
                        foreach (var anim in model.GetComponentsInChildren<Animator>(true))
                        {
                            anim.enabled = true;
                            anim.keepAnimatorStateOnDisable = true;
                            anim.SetBool(AnimatorDoorState.IsOpenHash, false);
                            anim.Play(AnimatorDoorState.CloseHash, 0, 1f);
                        }
                    // Own copies of the maps, taken while the real block still exists. After RemoveFromWorld
                    // the game unloads the shared textures and the moving copy would go magenta without this.
                    pin.Keep(model, c.Bv.Block is BlockHSLiftOutsidePanel || c.Bv.Block is BlockHSLiftInsidePanel);
                    foreach (var lit in model.GetComponentsInChildren<Light>(true)) lit.enabled = true;
                }
            }
            catch (Exception e)
            {
                HSLiftDebug.Warn("Model for " + c.Bv.Block.GetBlockName() + " failed: " + e.Message);
            }
            try
            {
                byte lv = c.Bv.Block.GetLightValue(c.Bv);
                if (lv > 0 && holder.GetComponentInChildren<Light>(true) == null)
                {
                    var lit = holder.AddComponent<Light>();
                    lit.type = LightType.Point;
                    lit.range = 8f;
                    lit.intensity = Mathf.Clamp01(lv / 15f) * 1.4f;
                    lit.color = Color.white;
                }
            }
            catch { }

            if (!withColliders || !c.Bv.Block.IsCollideMovement || IsRidePiece(c.Bv)) continue;
            // Same per-block movement bounds the game uses (Block.GetCollisionAABB): rotated, relative to the block corner.
            Bounds[] bounds = null;
            try { bounds = c.Bv.Block.shape.GetBounds(c.Bv); } catch (Exception) { }
            if (bounds == null || bounds.Length == 0) bounds = new[] { new Bounds(Vector3.one * 0.5f, Vector3.one) };
            foreach (var b in bounds)
            {
                if (b.size.x < 0.001f || b.size.y < 0.001f || b.size.z < 0.001f) continue;
                var bgo = new GameObject("col");
                bgo.transform.SetParent(root.transform, false);
                bgo.transform.localPosition = new Vector3(c.Dx, c.Dy, c.Dz) + b.center;
                bgo.layer = layer;
                bgo.AddComponent<BoxCollider>().size = b.size;
                boxCols++;
            }
        }
        HSLiftDebug.Verbose(string.Format("Car visual: {0} cells, {1} models, {2} colliders on layer {3}", cells.Count, models, boxCols, layer));
        return root;
    }

    // CloneModel copies the vanilla "look at me" green activate arrow. The script that hides it is switched off on the moving copy.
    static void HideFocusHelpers(Transform model)
    {
        if (model == null) return;
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == model) continue;
            var n = t.name;
            if (n.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("highlight", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("focus", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("interact", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("selector", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("overlay", StringComparison.OrdinalIgnoreCase) >= 0)
                t.gameObject.SetActive(false);
        }
    }

    // Layers the player's ground check (vp_FPController.FixedMove SphereCast) can see; layer 28 is not among them.
    public const int PlayerGroundMask = 1084850184;
    const int FallbackBlockLayer = 16;

    // Copy the layer of the real block colliders under the car so the player stands on the moving copy like on blocks.
    static int DetectBlockLayer(int baseY, List<HSLiftCell> cells)
    {
        foreach (var c in cells)
        {
            if (c.Dy != 0 || !c.Bv.Block.IsCollideMovement) continue;
            var top = HSLiftCar.UnityPos(baseY) + new Vector3(c.Dx + 0.5f, c.Dy + 1.2f, c.Dz + 0.5f);
            RaycastHit hit;
            if (Physics.Raycast(top, Vector3.down, out hit, 1.5f, PlayerGroundMask, QueryTriggerInteraction.Ignore) && hit.collider != null)
                return hit.collider.gameObject.layer;
        }
        return FallbackBlockLayer;
    }

    public static Vector3 UnityPos(float y)
    {
        return new Vector3(D.MinX, y, D.MinZ) - Origin.position;
    }

    // --- crash-safety journal ---

    static string LegacyJournalPath { get { return Path.Combine(HSLiftMod.ModPath ?? ".", "HSLift.journal.json"); } }

    static string JournalPath { get { return JournalPathFor(D.ElevatorId); } }

    static string JournalPathFor(string id)
    {
        var safe = string.IsNullOrEmpty(id) ? "lift1" : id;
        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        return Path.Combine(HSLiftMod.ModPath ?? ".", "HSLift." + safe + ".journal.json");
    }

    static int TexChannels
    {
        get { return System.Runtime.InteropServices.Marshal.SizeOf(typeof(TextureFullArray)) / 8; }
    }

    public static int NetTexChannels { get { return TexChannels; } }

    public static void WriteJournal(int baseY, List<HSLiftCell> cells)
    {
        var j = new HSLiftJournal { ElevatorId = D.ElevatorId, MinX = D.MinX, MinZ = D.MinZ, BaseY = baseY };
        int n = TexChannels;
        foreach (var c in cells)
        {
            var tex = new long[n];
            for (int i = 0; i < n; i++) tex[i] = c.Tex[i];
            j.Cells.Add(new HSLiftJournalCell { Dx = c.Dx, Dy = c.Dy, Dz = c.Dz, Raw = c.Bv.rawData, Damage = c.Bv.damage, Density = c.Density, Tex = tex });
        }
        File.WriteAllText(JournalPath, JsonConvert.SerializeObject(j));
    }

    public static void ClearJournal()
    {
        try
        {
            if (File.Exists(JournalPath)) File.Delete(JournalPath);
            if (D.ElevatorId == "lift1" && File.Exists(LegacyJournalPath)) File.Delete(LegacyJournalPath);
        }
        catch (Exception e) { HSLiftDebug.Error("Journal delete failed", e); }
    }

    public static bool HasJournalFor(string id)
    {
        if (File.Exists(JournalPathFor(id))) return true;
        return id == "lift1" && File.Exists(LegacyJournalPath);
    }

    public static bool HasJournal { get { return HasJournalFor(D.ElevatorId); } }

    // Returns true when there is nothing left to recover.
    public static bool TryRecoverJournal(World world)
    {
        HSLiftJournal j;
        try
        {
            var path = File.Exists(JournalPath) ? JournalPath : (File.Exists(LegacyJournalPath) ? LegacyJournalPath : null);
            if (path == null) return true;
            j = JsonConvert.DeserializeObject<HSLiftJournal>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Journal unreadable; left in place for manual recovery", e);
            return true;
        }
        if (j == null || j.Cells == null || j.Cells.Count == 0) { ClearJournal(); return true; }

        if (D.CurrentY != j.BaseY)
        {
            bool loaded, present = CarPresentAt(world, j, D.CurrentY, out loaded);
            if (!loaded) return false;
            if (present)
            {
                HSLiftDebug.Info("Journal recovery: car is already whole at Y" + D.CurrentY + "; discarding stale journal.");
                ClearJournal();
                return true;
            }
        }

        var restore = new List<HSLiftCell>();
        int already = 0;
        foreach (var jc in j.Cells)
        {
            var pos = new Vector3i(j.MinX + jc.Dx, j.BaseY + jc.Dy, j.MinZ + jc.Dz);
            if (world.GetChunkFromWorldPos(pos) == null) return false;
            var bv = new BlockValue(jc.Raw, jc.Damage);
            var cur = world.GetBlock(pos);
            if (cur.type == bv.type) { already++; continue; }
            if (!cur.isair)
            {
                HSLiftDebug.Error("Journal recovery blocked: " + cur.Block.GetBlockName() + " at " + pos + ". Journal kept at " + JournalPath);
                return true;
            }
            var tex = TextureFullArray.Default;
            if (jc.Tex != null)
                for (int i = 0; i < jc.Tex.Length && i < TexChannels; i++) tex[i] = jc.Tex[i];
            restore.Add(new HSLiftCell { Dx = jc.Dx, Dy = jc.Dy, Dz = jc.Dz, Bv = bv, Density = jc.Density, Tex = tex });
        }
        if (restore.Count > 0) ApplyLayered(world, j.BaseY, restore, false);
        HSLiftDebug.Info("Journal recovery: restored " + restore.Count + " car blocks at Y" + j.BaseY + " (" + already + " already in place).");
        D.CurrentY = j.BaseY;
        HSLiftConfiguration.Save();
        ClearJournal();
        return true;
    }

    // Removes only blocks at baseY that match the real car cell-for-cell (same block type and rotation).
    public static string RemoveDuplicateAt(World world, int baseY)
    {
        if (world == null || !D.HasCar) return "No car set.";
        if (Math.Abs(baseY - D.CurrentY) < D.SizeY) return "Y" + baseY + " overlaps the real car at Y" + D.CurrentY + ".";
        List<HSLiftCell> cells;
        var problem = Capture(world, D.CurrentY, out cells);
        if (problem != null) return "Cannot read the real car: " + problem;
        var changes = new List<BlockChangeInfo>();
        foreach (var c in cells)
        {
            var p = Pos(c, baseY);
            if (world.GetChunkFromWorldPos(p) == null) return "Chunk not loaded at " + p;
            var bv = world.GetBlock(p);
            if (bv.type == c.Bv.type && bv.rotation == c.Bv.rotation) changes.Add(new BlockChangeInfo(p, BlockValue.Air, MarchingCubes.DensityAir));
        }
        if (changes.Count == 0) return "No leftover car blocks found at Y" + baseY + ".";
        world.SetBlocksRPC(changes);
        HSLiftDebug.Info("Cleanup: removed " + changes.Count + " leftover car blocks at Y" + baseY);
        return "Removed " + changes.Count + " of " + cells.Count + " leftover car blocks at Y" + baseY + ".";
    }

    // Terrain / dirt / rubble in the car's XZ footprint between floors. Never the parked car, doors, panels, or pass-through.
    public static string ClearDebris(World world)
    {
        if (world == null || !D.HasCar) return "No car set.";
        int lo = D.LowerY, hi = D.LowerY + Math.Max(1, D.SizeY) - 1;
        if (D.Floors != null)
            foreach (var f in D.Floors)
            {
                if (f.Y < lo) lo = f.Y;
                int top = f.Y + Math.Max(1, D.SizeY) - 1;
                if (top > hi) hi = top;
            }
        var changes = new List<BlockChangeInfo>();
        var names = new List<string>();
        for (int y = lo; y <= hi; y++)
        for (int x = D.MinX; x < D.MinX + D.SizeX; x++)
        for (int z = D.MinZ; z < D.MinZ + D.SizeZ; z++)
        {
            if (IsExcluded(x, z)) continue;
            var pos = new Vector3i(x, y, z);
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            if (InBox(pos, D.CurrentY)) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair || bv.ischild) continue;
            if (IsPassThrough(bv)) continue;
            if (HSLiftDoors.IsCandidateDoor(bv.Block)) continue;
            if (bv.Block is BlockHSLiftOutsidePanel || bv.Block is BlockHSLiftInsidePanel) continue;
            if (!IsDebris(bv)) continue;
            changes.Add(new BlockChangeInfo(pos, BlockValue.Air, MarchingCubes.DensityAir));
            var n = bv.Block.GetBlockName();
            if (!names.Contains(n)) names.Add(n);
        }
        if (changes.Count == 0) return "No debris in the shaft of " + D.ElevatorId + " (Y" + lo + "-" + hi + "). Car, doors, panels, and pass-through were left alone.";
        world.SetBlocksRPC(changes);
        HSLiftDebug.Info("Debris: removed " + changes.Count + " blocks from " + D.ElevatorId + " (" + string.Join(", ", names.ToArray()) + ")");
        return "Removed " + changes.Count + " debris block(s) from " + D.ElevatorId + " shaft Y" + lo + "-" + hi + ": " + string.Join(", ", names.ToArray()) + ".";
    }

    static bool IsDebris(BlockValue bv)
    {
        if (bv.Block == null) return false;
        try
        {
            if (bv.Block.shape != null && bv.Block.shape.IsTerrain()) return true;
        }
        catch { }
        var n = bv.Block.GetBlockName();
        if (string.IsNullOrEmpty(n)) return false;
        if (n.StartsWith("terr", StringComparison.OrdinalIgnoreCase)) return true;
        return n.IndexOf("dirt", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("rubble", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("debris", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("pebble", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("gravel", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool CarPresentAt(World world, HSLiftJournal j, int baseY, out bool loaded)
    {
        loaded = true;
        foreach (var jc in j.Cells)
        {
            var pos = new Vector3i(j.MinX + jc.Dx, baseY + jc.Dy, j.MinZ + jc.Dz);
            if (world.GetChunkFromWorldPos(pos) == null) { loaded = false; return false; }
            if (world.GetBlock(pos).type != new BlockValue(jc.Raw, jc.Damage).type) return false;
        }
        return true;
    }
}

// Instantiates clone materials. Panel-face maps are duplicated so removing the car does not
// unload the shared elevator-panel atlas.
public class HSLiftPinnedLooks : MonoBehaviour
{
    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

    public void Keep(Transform model, bool panel)
    {
        if (model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;
            var copies = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null) continue;
                var m = new Material(shared[i]) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                owned.Add(m);
                foreach (var prop in m.GetTexturePropertyNames())
                {
                    var src = m.GetTexture(prop);
                    if (src != null && panel)
                    {
                        var copy = CopyMap(src);
                        if (copy != null) m.SetTexture(prop, copy);
                        else FillEmptyMap(m, prop);
                    }
                    else if (src == null && panel) FillEmptyMap(m, prop);
                }
                if (panel)
                {
                    foreach (var colorProp in new[] { "_Color", "_BaseColor", "_TintColor" })
                        if (m.HasProperty(colorProp)) m.SetColor(colorProp, Color.white);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                }
                copies[i] = m;
            }
            r.sharedMaterials = copies;
            if (panel) r.SetPropertyBlock(null);
            else
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                r.SetPropertyBlock(block);
            }
        }
    }

    Texture2D CopyMap(Texture src)
    {
        try
        {
            int w = Mathf.Clamp(src.width, 1, 1024);
            int h = Mathf.Clamp(src.height, 1, 1024);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false, false);
            copy.hideFlags = HideFlags.DontUnloadUnusedAsset;
            copy.name = "HSLiftCopy";
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            owned.Add(copy);
            return copy;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Panel texture copy failed: " + e.Message);
            return null;
        }
    }

    static Texture2D whiteMap;
    static Texture2D blackMap;
    static Texture2D flatNormal;

    static void FillEmptyMap(Material m, string prop)
    {
        var n = prop.ToLowerInvariant();
        if (n.Contains("normal") || n.Contains("bump"))
            m.SetTexture(prop, FlatNormal());
        else if (n.Contains("emiss"))
            m.SetTexture(prop, BlackMap());
        else if (n.Contains("tint") || n.Contains("mask") || n.Contains("rmom"))
            m.SetTexture(prop, WhiteMap());
    }

    static Texture2D WhiteMap()
    {
        if (whiteMap == null) whiteMap = Make(Color.white);
        return whiteMap;
    }

    static Texture2D BlackMap()
    {
        if (blackMap == null) blackMap = Make(Color.black);
        return blackMap;
    }

    static Texture2D FlatNormal()
    {
        if (flatNormal == null) flatNormal = Make(new Color(0.5f, 0.5f, 1f, 0.5f));
        return flatNormal;
    }

    static Texture2D Make(Color c)
    {
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
        for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) t.SetPixel(x, y, c);
        t.Apply();
        t.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return t;
    }

    void OnDestroy()
    {
        foreach (var o in owned)
            if (o != null) Destroy(o);
        owned.Clear();
    }
}
