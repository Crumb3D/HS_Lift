using System;
using System.Collections.Generic;
using UnityEngine;

// Ped: elevator doors in/near the cabin. Vehicle: garage / roll-up doors in a 2-block ring around the platform.
// Locked unless that lift's car/platform is parked at that door.
public static class HSLiftDoors
{
    static HSLiftConfigData D { get { return HSLiftConfiguration.Data; } }

    public static bool IsElevatorDoor(Block block)
    {
        return IsCandidateDoor(block);
    }

    public static bool IsCandidateDoor(Block block)
    {
        if (block == null || !(block is BlockCompositeTileEntity)) return false;
        var n = block.GetBlockName();
        if (n.IndexOf("VariantHelper", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return IsElevatorDoorName(n) || IsGarageOrRollUpName(n);
    }

    static bool IsElevatorDoorName(string n)
    {
        return n.StartsWith("elevatorDoor", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGarageOrRollUpName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        return n.IndexOf("GarageDoor", StringComparison.OrdinalIgnoreCase) >= 0
            || n.StartsWith("rollUpDoor", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("rollUpGate", StringComparison.OrdinalIgnoreCase);
    }

    static bool DoorKindMatches(Block block)
    {
        var n = block.GetBlockName();
        if (D.IsVehicleType) return IsGarageOrRollUpName(n);
        return IsElevatorDoorName(n);
    }

    static int Reach { get { return Math.Max(1, D.DoorReach); } }

    static bool InShaftRing(Vector3i p)
    {
        return D.HasCar && D.YOnShaft(p.y) && D.InDoorRing(p.x, p.z);
    }

    static int ShaftTop { get { return Math.Max(D.LowerY, D.HasUpper ? D.UpperY : D.LowerY) + D.DoorHeight - 1; } }

    // Checks every cell of the door (multi-block doors can touch the shaft with any part).
    public static bool IsLiftDoor(Vector3i parentPos, BlockValue parentBv)
    {
        return DoorDistance(parentPos, parentBv) >= 0;
    }

    // NearRank of the door's closest cell to this lift's car (lower = nearer), or -1 when it is not this
    // lift's door (out of reach, or behind a solid wall: that is a neighbouring shaft's door).
    static int DoorDistance(Vector3i parentPos, BlockValue parentBv)
    {
        if (!D.HasCar || !DoorKindMatches(parentBv.Block)) return -1;
        int best = int.MaxValue;
        var bestCell = parentPos;
        foreach (var p in DoorCells(parentPos, parentBv))
        {
            if (!InShaftRing(p) || p.y < D.LowerY || p.y > ShaftTop) continue;
            int rank = D.NearRank(p.x, p.z);
            if (rank < best) { best = rank; bestCell = p; }
        }
        if (best == int.MaxValue) return -1;
        if (D.DistOutsideXZ(bestCell.x, bestCell.z) >= 2 && HSLiftConfiguration.WalledOff(D, bestCell)) return -1;
        return best;
    }

    // How squarely this door sits in front of the current car. Breaks a tie when two shafts share a wall.
    static int DoorCenterDist(Vector3i parentPos, BlockValue parentBv)
    {
        int cx = D.MinX + D.SizeX / 2, cz = D.MinZ + D.SizeZ / 2;
        int best = int.MaxValue;
        foreach (var p in DoorCells(parentPos, parentBv))
        {
            if (!InShaftRing(p)) continue;
            int d = Math.Abs(p.x - cx) + Math.Abs(p.z - cz);
            if (d < best) best = d;
        }
        return best;
    }

    public static IEnumerable<Vector3i> DoorCells(Vector3i parentPos, BlockValue bv)
    {
        yield return parentPos;
        var block = bv.Block;
        if (!block.isMultiBlock) yield break;
        for (int i = 0; i < block.multiBlockPos.Length; i++)
            yield return parentPos + block.multiBlockPos.Get(i, bv.type, bv.rotation);
    }

    // Bind Data to the lift that owns this door (2-block ring outside that lift's car). Null if none.
    public static HSLiftConfigData BindDoor(Vector3i parentPos, BlockValue parentBv)
    {
        if (!IsElevatorDoor(parentBv.Block)) return null;
        var saved = HSLiftConfiguration.Data;
        HSLiftConfigData owner = null;
        int ownerDist = int.MaxValue;
        int ownerCenter = int.MaxValue;
        foreach (var d in HSLiftConfiguration.Lifts)
        {
            HSLiftConfiguration.Operate(d);
            int dist = DoorDistance(parentPos, parentBv);
            if (dist < 0) continue;
            int center = DoorCenterDist(parentPos, parentBv);
            if (dist < ownerDist || (dist == ownerDist && center < ownerCenter))
            {
                owner = d;
                ownerDist = dist;
                ownerCenter = center;
            }
        }
        if (owner != null) HSLiftConfiguration.Operate(owner);
        else if (saved != null) HSLiftConfiguration.Operate(saved);
        return owner;
    }

    public static bool CarIsBehind(Vector3i parentPos, BlockValue bv)
    {
        var c = HSLiftController.Of(D);
        if (c != null && c.IsThisMoving) return false;
        int hi = D.CurrentY + D.DoorHeight;
        foreach (var p in DoorCells(parentPos, bv))
            if (p.y >= D.CurrentY && p.y < hi) return true;
        return false;
    }

    // Parent positions of lift doors whose cells overlap rows [yLo, yHi].
    public static List<Vector3i> FindDoors(World world, int yLo, int yHi)
    {
        var found = new List<Vector3i>();
        if (world == null || !D.HasCar) return found;
        for (int y = yLo - 1; y <= yHi; y++)
        for (int x = D.MinX - Reach; x <= D.MinX + D.SizeX - 1 + Reach; x++)
        for (int z = D.MinZ - Reach; z <= D.MinZ + D.SizeZ - 1 + Reach; z++)
        {
            var p = new Vector3i(x, y, z);
            if (!InShaftRing(p) || world.GetChunkFromWorldPos(p) == null) continue;
            var bv = world.GetBlock(p);
            if (!DoorKindMatches(bv.Block)) continue;
            var parent = bv.ischild ? bv.Block.multiBlockPos.GetParentPos(p, bv) : p;
            if (found.Contains(parent)) continue;
            var pbv = world.GetBlock(parent);
            if (!IsLiftDoor(parent, pbv)) continue;
            // A neighbour's door can sit inside this shaft's reach when two lifts share a wall.
            // Open, close, and call only the door that is actually this lift's.
            var here = HSLiftConfiguration.Data;
            var owner = BindDoor(parent, pbv);
            if (here != null) HSLiftConfiguration.Operate(here);
            if (owner != here) continue;
            bool overlaps = false;
            foreach (var c in DoorCells(parent, pbv))
                if (c.y >= yLo && c.y <= yHi) { overlaps = true; break; }
            if (overlaps) found.Add(parent);
        }
        return found;
    }

    public static int SetDoors(World world, int yLo, int yHi, bool open)
    {
        int changed = 0;
        foreach (var pos in FindDoors(world, yLo, yHi))
        {
            try
            {
                var te = world.GetTileEntity(pos) as TileEntityComposite;
                var door = te != null ? te.GetFeature<TEFeatureDoor>() : null;
                if (door == null) continue;
                bool wantOpen = open && (!IsCarDoor(world, pos) || HasLandingOutside(world, pos));
                if (door.IsOpen() == wantOpen) continue;
                door.SetOpen(wantOpen, true);
                changed++;
                if (open && !wantOpen) HSLiftDebug.Verbose("Left door shut (no landing) at " + pos);
            }
            catch (Exception e)
            {
                HSLiftDebug.Error("Door " + (open ? "open" : "close") + " failed at " + pos, e);
            }
        }
        HSLiftDebug.Info((open ? "Opened/shut " : "Closed ") + changed + " lift door(s) for rows Y" + yLo + "-" + yHi);
        return changed;
    }

    // Cabin / car door (touches the footprint). Outside landing doors are not locked by the floor check.
    public static bool IsCarDoor(World world, Vector3i parent)
    {
        if (world == null || !D.HasCar) return false;
        var pbv = world.GetBlock(parent);
        foreach (var c in DoorCells(parent, pbv))
            if (HSLiftCar.InFootprint(c.x, c.z)) return true;
        return false;
    }

    // Walkable floor just outside a *car* door (plates count). Air = drop; solid at chest height = wall.
    public static bool HasLandingOutside(World world, Vector3i parent)
    {
        if (world == null || !D.HasCar) return false;
        if (!IsCarDoor(world, parent)) return true;
        var pbv = world.GetBlock(parent);
        var dir = OutwardDir(parent, pbv);
        if (dir.x == 0 && dir.z == 0) return true;
        int floorY = D.CurrentY;
        var seen = new HashSet<Vector3i>();
        foreach (var c in DoorCells(parent, pbv))
        {
            var t = new Vector3i(c.x + dir.x, floorY, c.z + dir.z);
            int n = 0;
            while (n < 3 && (HSLiftCar.InFootprint(t.x, t.z) || IsDoorBlock(world, t)))
            {
                t.x += dir.x;
                t.z += dir.z;
                n++;
            }
            for (int extra = 0; extra < 2; extra++)
            {
                if (seen.Add(t) && WalkableLanding(world, t)) return true;
                t.x += dir.x;
                t.z += dir.z;
            }
        }
        return false;
    }

    static Vector3i OutwardDir(Vector3i parent, BlockValue bv)
    {
        int cx = D.MinX + D.SizeX / 2, cz = D.MinZ + D.SizeZ / 2;
        int ax = 0, az = 0;
        foreach (var c in DoorCells(parent, bv))
        {
            ax += c.x - cx;
            az += c.z - cz;
        }
        if (Math.Abs(az) >= Math.Abs(ax))
            return new Vector3i(0, 0, az == 0 ? 0 : Math.Sign(az));
        return new Vector3i(Math.Sign(ax), 0, 0);
    }

    static bool IsDoorBlock(World world, Vector3i pos)
    {
        if (world.GetChunkFromWorldPos(pos) == null) return false;
        var bv = world.GetBlock(pos);
        return IsCandidateDoor(bv.Block);
    }

    static bool WalkableLanding(World world, Vector3i floor)
    {
        if (world.GetChunkFromWorldPos(floor) == null) return false;
        if (HSLiftCar.InFootprint(floor.x, floor.z)) return false;
        var ground = world.GetBlock(floor);
        if (ground.isair || IsCandidateDoor(ground.Block)) return false;
        var body = world.GetBlock(new Vector3i(floor.x, floor.y + 1, floor.z));
        if (body.isair || HSLiftCar.IsPassThrough(body) || IsCandidateDoor(body.Block)) return true;
        return false;
    }

    struct BesideDoor
    {
        public Vector3i Cell;
        public HSLiftConfigData Lift;
        public int OutX, OutZ;
    }

    static List<BesideDoor> besideDoors;

    // Every lift's own door cells, so a sign or call panel can be matched to the door it sits on.
    public static void CollectDoors(World world)
    {
        besideDoors = new List<BesideDoor>();
        if (world == null) return;
        var saved = HSLiftConfiguration.Data;
        foreach (var d in HSLiftConfiguration.Lifts)
        {
            if (d == null || !d.HasCar) continue;
            HSLiftConfiguration.Operate(d);
            int top = Math.Max(d.LowerY, d.HasUpper ? d.UpperY : d.LowerY) + Math.Max(1, d.DoorHeight);
            foreach (var parent in FindDoors(world, d.LowerY, top))
            {
                var bv = world.GetBlock(parent);
                var outward = OutwardDir(parent, bv);
                foreach (var c in DoorCells(parent, bv))
                    besideDoors.Add(new BesideDoor { Cell = c, Lift = d, OutX = outward.x, OutZ = outward.z });
            }
        }
        if (saved != null) HSLiftConfiguration.Operate(saved);
    }

    // The lift whose door this block is above, or immediately left/right of.
    // Two doors sharing a wall can be the same distance; the block on a door's right wins that tie
    // (panels are on the right of each door). A sign directly above its door is closer and does not tie.
    public static HSLiftConfigData LiftBeside(Vector3i pos)
    {
        if (besideDoors == null) return null;
        HSLiftConfigData best = null;
        int bestH = int.MaxValue;
        int bestDy = int.MaxValue;
        int bestSide = int.MinValue;
        foreach (var r in besideDoors)
        {
            int dx = pos.x - r.Cell.x;
            int dz = pos.z - r.Cell.z;
            int h = Math.Max(Math.Abs(dx), Math.Abs(dz));
            if (h > 2) continue;
            int dy = Math.Abs(pos.y - r.Cell.y);
            if (dy > 6) continue;
            // Right, standing on the landing looking at the door. Positive means this block is on that side.
            int side = dz * r.OutX - dx * r.OutZ;
            if (h < bestH || (h == bestH && dy < bestDy) || (h == bestH && dy == bestDy && side > bestSide))
            {
                bestH = h;
                bestDy = dy;
                bestSide = side;
                best = r.Lift;
            }
        }
        return best;
    }

    public static void OpenAtCar(World world)
    {
        OpenAtCar(world, true);
    }

    public static void OpenAtCar(World world, bool autoClose)
    {
        if (!D.HasCar) return;
        if (HSLiftConfiguration.FloorAt(D.CurrentY) == null) return;
        SetDoors(world, D.CurrentY, D.CurrentY + D.DoorHeight - 1, true);
        HSLiftFloorSigns.UpdateToCurrentFloor(world);
        var c = HSLiftController.Of(D);
        if (c == null) return;
        c.ClearDoorWatch();
        if (autoClose) c.ScheduleAutoClose();
        else c.CancelAutoClose();
    }

    public static void CloseAtCar(World world)
    {
        if (!D.HasCar) return;
        var c = HSLiftController.Of(D);
        if (c != null) c.CancelAutoClose();
        SetDoors(world, D.CurrentY, D.CurrentY + D.DoorHeight - 1, false);
        if (c != null) c.WatchDoorClose();
    }

    public static int CloseAllShaftDoors(World world)
    {
        return CloseAllShaftDoors(world, true);
    }

    public static int CloseAllShaftDoors(World world, bool watchObstruction)
    {
        if (!D.HasCar) return 0;
        var c = HSLiftController.Of(D);
        if (c != null) c.CancelAutoClose();
        int n = SetDoors(world, D.LowerY, ShaftTop, false);
        if (watchObstruction && c != null) c.WatchDoorClose();
        return n;
    }

    public static bool AnyOpenFor(World world, HSLiftConfigData lift)
    {
        if (world == null || lift == null || !lift.HasCar) return false;
        var prev = HSLiftConfiguration.Data;
        HSLiftConfiguration.Operate(lift);
        bool open = AnyOpenAtCar(world);
        if (prev != null) HSLiftConfiguration.Operate(prev);
        return open;
    }

    public static bool AnyOpenAtCar(World world)
    {
        if (world == null || !D.HasCar) return false;
        foreach (var pos in FindDoors(world, D.CurrentY, D.CurrentY + D.DoorHeight - 1))
        {
            var te = world.GetTileEntity(pos) as TileEntityComposite;
            var door = te != null ? te.GetFeature<TEFeatureDoor>() : null;
            if (door != null && door.IsOpen()) return true;
        }
        return false;
    }

    static bool DoorIsOpen(World world, Vector3i parent)
    {
        var te = world.GetTileEntity(parent) as TileEntityComposite;
        var door = te != null ? te.GetFeature<TEFeatureDoor>() : null;
        return door != null && door.IsOpen();
    }

    // Open door cells, plus the 1-block gap between a cabin door and its landing door (or two leaves).
    // Does not scan the car, the opposite door set, or anything else.
    public static bool DoorwayBlocked(World world, out string why)
    {
        why = null;
        if (world == null || !D.HasCar) return false;
        int yLo = D.CurrentY, yHi = D.CurrentY + D.DoorHeight - 1;
        var doors = FindDoors(world, yLo, yHi);
        var openDoors = new List<Vector3i>();
        foreach (var parent in doors)
            if (DoorIsOpen(world, parent)) openDoors.Add(parent);
        if (openDoors.Count == 0) return false;
        var doorCells = new HashSet<Vector3i>();
        foreach (var parent in openDoors)
            foreach (var c in DoorCells(parent, world.GetBlock(parent)))
                doorCells.Add(c);
        var ents = new List<Entity>();
        foreach (var c in doorCells)
            if (EntityInCell(world, c, ents)) { why = "in the doorway"; return true; }

        if (D.IsVehicleType) return false;
        for (int i = 0; i < openDoors.Count; i++)
        for (int j = i + 1; j < openDoors.Count; j++)
        {
            int pairLo, pairHi;
            if (!SameOpeningPair(world, openDoors[i], openDoors[j], out pairLo, out pairHi)) continue;
            foreach (var mid in CellsBetween(openDoors[i], openDoors[j], pairLo, pairHi))
            {
                if (doorCells.Contains(mid)) continue;
                if (EntityInCell(world, mid, ents)) { why = "between the doors"; return true; }
                if (HSLiftCar.InFootprint(mid.x, mid.z)) continue;
                if (PassThroughCell(world, mid)) continue;
                if (SolidInWay(world, mid)) { why = "a block is between the doors"; return true; }
            }
        }
        return false;
    }

    // Someone walking through the opening — every door at the car, even while they report closed.
    public static bool PersonInDoorway(World world, out string why)
    {
        why = null;
        if (world == null || !D.HasCar) return false;
        int yLo = D.CurrentY, yHi = D.CurrentY + D.DoorHeight - 1;
        var doors = FindDoors(world, yLo, yHi);
        if (doors.Count == 0) return false;
        var doorCells = new HashSet<Vector3i>();
        foreach (var parent in doors)
            foreach (var c in DoorCells(parent, world.GetBlock(parent)))
                doorCells.Add(c);
        var ents = new List<Entity>();
        foreach (var c in doorCells)
            if (EntityOverlapsCell(world, c, ents)) { why = "in the doorway"; return true; }

        if (D.IsVehicleType) return false;
        for (int i = 0; i < doors.Count; i++)
        for (int j = i + 1; j < doors.Count; j++)
        {
            int pairLo, pairHi;
            if (!SameOpeningPair(world, doors[i], doors[j], out pairLo, out pairHi)) continue;
            foreach (var mid in CellsBetween(doors[i], doors[j], pairLo, pairHi))
            {
                if (doorCells.Contains(mid)) continue;
                if (EntityOverlapsCell(world, mid, ents)) { why = "between the doors"; return true; }
            }
        }
        return false;
    }

    // Cabin door + landing door (or two leaves) on the same opening: 1-2 blocks apart, same axis.
    // Not front vs back across the car (that gap is the lift itself).
    static bool SameOpeningPair(World world, Vector3i a, Vector3i b, out int yLo, out int yHi)
    {
        yLo = 0;
        yHi = 0;
        int dx = Math.Abs(a.x - b.x), dz = Math.Abs(a.z - b.z);
        int d = Math.Max(dx, dz);
        if (d < 1 || d > 2) return false;
        if (a.x != b.x && a.z != b.z) return false;
        if (OppositeSides(a, b)) return false;
        int aLo, aHi, bLo, bHi;
        DoorYRange(world, a, out aLo, out aHi);
        DoorYRange(world, b, out bLo, out bHi);
        yLo = Math.Max(aLo, bLo);
        yHi = Math.Min(aHi, bHi);
        return yHi >= yLo;
    }

    static bool OppositeSides(Vector3i a, Vector3i b)
    {
        int minX = D.MinX, maxX = D.MinX + D.SizeX - 1;
        int minZ = D.MinZ, maxZ = D.MinZ + D.SizeZ - 1;
        if ((a.z <= minZ && b.z >= maxZ) || (b.z <= minZ && a.z >= maxZ)) return true;
        if ((a.x <= minX && b.x >= maxX) || (b.x <= minX && a.x >= maxX)) return true;
        return false;
    }

    static void DoorYRange(World world, Vector3i parent, out int lo, out int hi)
    {
        lo = int.MaxValue;
        hi = int.MinValue;
        foreach (var c in DoorCells(parent, world.GetBlock(parent)))
        {
            if (c.y < lo) lo = c.y;
            if (c.y > hi) hi = c.y;
        }
        if (lo == int.MaxValue) { lo = parent.y; hi = parent.y; }
    }

    static IEnumerable<Vector3i> CellsBetween(Vector3i a, Vector3i b, int yLo, int yHi)
    {
        if (a.x == b.x)
        {
            int z0 = Math.Min(a.z, b.z), z1 = Math.Max(a.z, b.z);
            for (int z = z0 + 1; z < z1; z++)
            for (int y = yLo; y <= yHi; y++)
                yield return new Vector3i(a.x, y, z);
        }
        else if (a.z == b.z)
        {
            int x0 = Math.Min(a.x, b.x), x1 = Math.Max(a.x, b.x);
            for (int x = x0 + 1; x < x1; x++)
            for (int y = yLo; y <= yHi; y++)
                yield return new Vector3i(x, y, a.z);
        }
    }

    static bool PassThroughCell(World world, Vector3i pos)
    {
        if (world.GetChunkFromWorldPos(pos) == null) return false;
        var bv = world.GetBlock(pos);
        return !bv.isair && HSLiftCar.IsPassThrough(bv);
    }

    static bool SolidInWay(World world, Vector3i pos)
    {
        if (world.GetChunkFromWorldPos(pos) == null) return false;
        var bv = world.GetBlock(pos);
        if (bv.isair) return false;
        if (IsCandidateDoor(bv.Block)) return false;
        if (HSLiftCar.IsPassThrough(bv)) return false;
        if (bv.Block is BlockHSLiftOutsidePanel || bv.Block is BlockHSLiftInsidePanel) return false;
        if (HSLiftCar.InFootprint(pos.x, pos.z)) return false;
        var n = bv.Block.GetBlockName();
        if (!string.IsNullOrEmpty(n) && n.IndexOf("VariantHelper", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    static bool EntityInCell(World world, Vector3i pos, List<Entity> buf)
    {
        buf.Clear();
        var bb = new Bounds(new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f), new Vector3(1.05f, 1.2f, 1.05f));
        world.GetEntitiesInBounds(typeof(Entity), bb, buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i];
            if (e == null || e is EntityFallingBlock) continue;
            if (FeetInCar(e)) continue;
            int x = Mathf.FloorToInt(e.position.x);
            int y = Mathf.FloorToInt(e.position.y + 0.1f);
            int z = Mathf.FloorToInt(e.position.z);
            if (x != pos.x || z != pos.z) continue;
            if (y == pos.y || y + 1 == pos.y) return true;
        }
        return false;
    }

    static bool EntityOverlapsCell(World world, Vector3i pos, List<Entity> buf)
    {
        buf.Clear();
        var bb = new Bounds(new Vector3(pos.x + 0.5f, pos.y + 0.6f, pos.z + 0.5f), new Vector3(1.05f, 2.0f, 1.05f));
        world.GetEntitiesInBounds(typeof(Entity), bb, buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i];
            if (e == null || e is EntityFallingBlock) continue;
            if (FeetInCar(e)) continue;
            int x = Mathf.FloorToInt(e.position.x);
            int z = Mathf.FloorToInt(e.position.z);
            if (x != pos.x || z != pos.z) continue;
            return true;
        }
        return false;
    }

    static bool FeetInCar(Entity e)
    {
        if (e == null || !D.HasCar) return false;
        return HSLiftCar.InFootprint(Mathf.FloorToInt(e.position.x), Mathf.FloorToInt(e.position.z));
    }
}
