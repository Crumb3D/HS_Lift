using System;
using System.Collections.Generic;

// Shared setup used by the Elevator Setup Tool (hold E) and the admin-only hslift console command.
public static class HSLiftSetup
{
    public static bool HasForcedAim;
    public static Vector3i ForcedAim;

    public static void Tell(EntityPlayerLocal player, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (player != null) GameManager.ShowTooltip(player, msg);
        else SdtdConsole.Instance.Output(msg);
    }

    public static string Execute(string sub, string arg, string extra, EntityPlayerLocal player)
    {
        if (HSLiftNet.IsRemoteClient && sub != "status" && sub != "list" && sub != "preview")
            return HSLiftNet.SendSetup(sub, arg, extra, player);
        if (sub == "floor") return FloorCommand(arg, extra, player);
        if (sub == "go") return HSLiftController.RequestFloor(arg, "console") ?? "Going.";
        return Run(sub, arg, player);
    }

    static string Run(string sub, string arg, EntityPlayerLocal player)
    {
        var d = HSLiftConfiguration.Data;
        switch (sub)
        {
            case "status":
                return HSLiftController.StatusText();
            case "debug":
                d.Debug = !d.Debug;
                HSLiftDebug.Enabled = d.Debug;
                HSLiftConfiguration.Save();
                return "HSLift debug " + (d.Debug ? "ON" : "OFF");
            case "up":
                return HSLiftController.RequestMove(true, "console") ?? "Moving up.";
            case "down":
                return HSLiftController.RequestMove(false, "console") ?? "Moving down.";
            case "preview":
                return arg == "off" ? HSLiftController.StopPreview() : HSLiftController.StartPreview();
            case "list":
                return HSLiftConfiguration.ListLifts() + "\nEditing " + HSLiftConfiguration.Summary();
            case "select":
            {
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                return HSLiftConfiguration.SelectNearest(p);
            }
            case "music":
            {
                bool on;
                if (!HSLiftSettings.TryParseOnOff(arg, out on))
                    return "Usage: hslift music on | off   (host / single player). Now: Music=" + HSLiftSettings.Music;
                return HSLiftSettings.SetMusic(on);
            }
            case "flicker":
            case "flickerlights":
            {
                bool on;
                if (!HSLiftSettings.TryParseOnOff(arg, out on))
                    return "Usage: hslift flicker on | off   (host / single player). Now: FlickerLights=" + HSLiftSettings.FlickerLights;
                return HSLiftSettings.SetFlickerLights(on);
            }
            case "scheme":
            case "numbering":
            {
                if (arg != "gb" && arg != "uk" && arg != "us" && arg != "usa")
                    return "Floor labels: gb (default, G stays G) or us (G becomes 1, 1 becomes 2). Current: " + HSLiftConfiguration.FileFloorScheme
                        + "\nEdit FloorScheme in the world-save HSLift.json and restart, or: hslift scheme gb | us";
                HSLiftConfiguration.SetFloorScheme(arg);
                HSLiftFloorSigns.Invalidate();
                var world = GameManager.Instance.World;
                if (world != null) HSLiftFloorSigns.UpdateToCurrentFloor(world);
                if (HSLiftConfiguration.FileFloorScheme == "us")
                    return "US floors: G shows as 1, 1 as 2, 2 as 3. Basements stay B. Signs and the floor menu use this; console names stay G/1/2.";
                return "GB floors: G, 1, 2 (Ground, 1st, 2nd). Signs and the floor menu match.";
            }
            case "delete":
            case "forget":
            {
                var target = string.IsNullOrEmpty(arg) ? null : HSLiftConfiguration.ById(arg);
                if (target == null) return "Usage: hslift delete <id>   (forgets that lift's setup; blocks in the world are not touched)\n" + HSLiftConfiguration.ListLifts();
                var ctrl = HSLiftController.Of(target);
                if (ctrl != null && ctrl.IsThisMoving) return target.ElevatorId + " is moving. Wait until it stops.";
                if (HSLiftCar.HasJournalFor(target.ElevatorId)) return target.ElevatorId + " is still restoring its car after an interrupted move. Try again once it is back.";
                HSLiftController.Forget(target);
                HSLiftConfiguration.Lifts.Remove(target);
                if (HSLiftConfiguration.Lifts.Count == 0) HSLiftConfiguration.NewLift("ped");
                else if (HSLiftConfiguration.Data == target) HSLiftConfiguration.Use(HSLiftConfiguration.Lifts[0]);
                HSLiftConfiguration.Save();
                return target.ElevatorId + " deleted (setup only; its blocks stay in the world). Now editing " + HSLiftConfiguration.Data.ElevatorId + ".\n" + HSLiftConfiguration.ListLifts();
            }
            case "type":
            case "new":
            {
                if (arg != "ped" && arg != "vehicle") return "Usage: hslift type ped | vehicle   (always starts a NEW lift; existing lifts are not changed)";
                var created = HSLiftConfiguration.NewLift(arg);
                var hint = arg == "vehicle"
                    ? " Mark opposite corners of the FLOOR only (same Y). Garage / roll-up doors stay at each landing."
                    : " Mark opposite corners of the cabin (3D box). Elevator doors.";
                return "Started " + created.ElevatorId + " (" + created.Type + ")." + hint + " Other lifts were not changed.\n" + HSLiftConfiguration.ListLifts();
            }
        }

        if (HSLiftController.IsMoving) return "This lift is moving; setup is ignored until it stops.";

        switch (sub)
        {
            case "corner1":
            case "corner2":
            {
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                var c = new[] { p.x, p.y, p.z };
                if (sub == "corner1") d.Corner1 = c; else d.Corner2 = c;
                var msg = (sub == "corner1" ? "Corner 1" : "Corner 2") + " = " + p;
                if (d.Corner1 != null && d.Corner2 != null) msg += "\n" + ApplyCorners();
                HSLiftConfiguration.Save();
                return msg;
            }
            case "upper":
            {
                int n = 1;
                while (HSLiftConfiguration.FloorByName(n.ToString()) != null) n++;
                return FloorCommand("add", n.ToString(), player);
            }
            case "panel":
                return PanelCommand(arg, player);
            case "speed":
            {
                float s;
                if (!float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s) || s < 0.2f || s > 10f)
                    return "Usage: hslift speed <0.2 - 10>";
                d.SpeedBlocksPerSecond = s;
                HSLiftConfiguration.Save();
                return "Speed = " + s + " blocks/sec";
            }
            case "grow":
            {
                int n;
                if (!int.TryParse(arg, out n) || n < 1 || n > 3) n = 1;
                if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
                int sx = d.SizeX + 2 * n, sz = d.SizeZ + 2 * n;
                if (sx * d.SizeY * sz > 512) return "Car would be too big (" + sx + "x" + d.SizeY + "x" + sz + "). Limit is 512 cells.";
                d.MinX -= n; d.MinZ -= n; d.SizeX = sx; d.SizeZ = sz;
                d.Corner1 = new[] { d.MinX, d.CurrentY, d.MinZ };
                d.Corner2 = new[] { d.MinX + sx - 1, d.CurrentY + d.SizeY - 1, d.MinZ + sz - 1 };
                HSLiftConfiguration.Save();
                var inBox = d.Panels.FindAll(p => HSLiftCar.InBox(p.Pos, d.CurrentY) || (p.X >= d.MinX && p.X < d.MinX + sx && p.Z >= d.MinZ && p.Z < d.MinZ + sz));
                var check = HSLiftCar.ValidateCarAt(GameManager.Instance.World, d.CurrentY);
                var what = d.IsVehicleType ? "Platform" : "Car box";
                return string.Format("{0} is now {1}x{2}x{3} from X{4} Z{5}.{6}{7}", what, sx, d.SizeY, sz, d.MinX, d.MinZ,
                    inBox.Count > 0 ? "\nWarning: " + inBox.Count + " outside panel(s) are now on the platform; move them to the wall beside it." : "",
                    check != null ? "\nWarning: " + check : "");
            }
            case "exclude":
                return ExcludeCommand(arg, player);
            case "cleanup":
            {
                int y;
                if (!int.TryParse(arg, out y)) return "Usage: hslift cleanup <y>  (floor Y of a leftover copy of the car)";
                return HSLiftCar.RemoveDuplicateAt(GameManager.Instance.World, y);
            }
            case "debris":
            {
                Vector3i p;
                if (AimedBlock(player, out p) == null) BindAimedLift(p);
                return HSLiftCar.ClearDebris(GameManager.Instance.World);
            }
            case "reset":
                d.Corner1 = d.Corner2 = null;
                d.HasCar = false;
                HSLiftConfiguration.Save();
                return "Car selection cleared. Floors and panels kept.";
        }
        return "Unknown command.";
    }

    static string ExcludeCommand(string arg, EntityPlayerLocal player)
    {
        var d = HSLiftConfiguration.Data;
        if (arg == "clear")
        {
            d.ExcludedColumns.Clear();
            HSLiftConfiguration.Save();
            return "Excluded columns cleared; the whole car box is the car again.";
        }
        if (arg == "list")
        {
            if (d.ExcludedColumns.Count == 0) return "No excluded columns.";
            var parts = new List<string>();
            foreach (var c in d.ExcludedColumns)
                parts.Add("X" + c[0] + " Z" + c[1] + (c.Length >= 4 ? " rows " + (d.CurrentY + c[2]) + "-" + (d.CurrentY + c[3]) + " at the car's floor" : " (whole column)"));
            return "Stationary landing columns: " + string.Join(", ", parts.ToArray());
        }
        int height = 3;
        if (arg != "" && (!int.TryParse(arg, out height) || height < 1 || height > 16))
            height = 3;
        if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        var world = GameManager.Instance.World;
        var bv = world.GetBlock(p);
        var parent = bv.ischild ? bv.Block.multiBlockPos.GetParentPos(p, bv) : p;
        var pbv = world.GetBlock(parent);
        var cells = new List<Vector3i> { parent };
        if (pbv.Block.isMultiBlock)
            for (int i = 0; i < pbv.Block.multiBlockPos.Length; i++) cells.Add(parent + pbv.Block.multiBlockPos.Get(i, pbv.type, pbv.rotation));
        int added = 0;
        var done = new List<string>();
        foreach (var c in cells)
        {
            bool inFootprint = c.x >= d.MinX && c.x < d.MinX + d.SizeX && c.z >= d.MinZ && c.z < d.MinZ + d.SizeZ;
            if (!inFootprint || done.Contains(c.x + "," + c.z)) continue;
            done.Add(c.x + "," + c.z);
            int lo = int.MaxValue, hi = int.MinValue;
            foreach (var o in cells)
                if (o.x == c.x && o.z == c.z) { lo = Math.Min(lo, o.y - d.CurrentY); hi = Math.Max(hi, o.y - d.CurrentY); }
            if (height > 0) hi = Math.Max(hi, lo + height - 1);
            foreach (var e in d.ExcludedColumns)
                if (e != null && e.Length >= 4 && e[0] == c.x && e[1] == c.z) { lo = Math.Min(lo, e[2]); hi = Math.Max(hi, e[3]); }
            d.ExcludedColumns.RemoveAll(e => e != null && e.Length >= 2 && e[0] == c.x && e[1] == c.z);
            d.ExcludedColumns.Add(new[] { c.x, c.z, lo, hi });
            added++;
        }
        if (added == 0) return "Nothing to exclude: the aimed block (" + pbv.Block.GetBlockName() + " at " + parent + ") is not inside the car box.";
        HSLiftConfiguration.Save();
        return "Marked " + pbv.Block.GetBlockName() + " at " + parent + " as a stationary landing (" + added + " column(s)). "
            + "Car blocks above or below it in that column still ride and pass the landings on other floors.";
    }

    static string FloorCommand(string action, string name, EntityPlayerLocal player)
    {
        var d = HSLiftConfiguration.Data;
        switch (action)
        {
            case "":
            case "list":
                return "Floors (top first, * = car): " + HSLiftConfiguration.FloorList();
            case "add":
            {
                if (HSLiftController.IsMoving) return "Lift is moving.";
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                BindAimedLift(p);
                d = HSLiftConfiguration.Data;
                if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
                if (string.IsNullOrEmpty(name) || name.Contains(" ")) name = NextFloorName(p.y);
                if (HSLiftConfiguration.FloorByName(name) != null) return "Floor '" + name + "' already exists. Remove it first.";
                var same = HSLiftConfiguration.FloorAt(p.y);
                if (same != null) return HSLiftConfiguration.FloorDisplayName(same.Name) + " is already at this height.";
                // Two stops closer than the car is tall cannot both be landings: almost always a slab one block off.
                int minGap = Math.Max(2, d.DoorHeight - 1);
                var near = d.Floors.Find(f => Math.Abs(f.Y - p.y) < minGap);
                if (near != null)
                    return "Too close to " + HSLiftConfiguration.FloorDisplayName(near.Name) + " (Y" + near.Y + ", you aimed at Y" + p.y
                        + "). Floors must be at least " + minGap + " blocks apart. Aim at the landing floor slab itself.";
                d.Floors.Add(new HSLiftFloor { Name = name, Y = p.y });
                RelabelIfStopBelowGround();
                HSLiftConfiguration.SyncFloors();
                var worldAdd = GameManager.Instance.World;
                if (worldAdd != null) HSLiftCar.EnsureShaftRoof(worldAdd);
                HSLiftConfiguration.Save();
                var added = HSLiftConfiguration.FloorAt(p.y);
                var shown = added != null ? HSLiftConfiguration.FloorDisplayName(added.Name) : name;
                return shown + " added (car floor at this height).\nFloors: " + HSLiftConfiguration.FloorList();
            }
            case "ground":
            case "g":
            {
                if (HSLiftController.IsMoving) return "Lift is moving.";
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                BindAimedLift(p);
                d = HSLiftConfiguration.Data;
                if (!d.HasCar) return "Set the car first (Corner 1, then Corner 2).";
                var same = HSLiftConfiguration.FloorAt(p.y);
                if (same != null && string.Equals(same.Name, "G", StringComparison.OrdinalIgnoreCase))
                    return "Ground Floor is already at this height.\nFloors: " + HSLiftConfiguration.FloorList();
                if (same == null)
                    d.Floors.Add(new HSLiftFloor { Name = "G", Y = p.y });
                RelabelWithGroundAt(p.y);
                HSLiftConfiguration.SyncFloors();
                var worldG = GameManager.Instance.World;
                if (worldG != null) HSLiftCar.EnsureShaftRoof(worldG);
                HSLiftConfiguration.Save();
                return "Ground Floor set at this height. Other floors were renamed to match.\nFloors: " + HSLiftConfiguration.FloorList();
            }
            case "remove":
            {
                if (HSLiftController.IsMoving) return "Lift is moving.";
                HSLiftFloor f = null;
                if (!string.IsNullOrEmpty(name)) f = HSLiftConfiguration.FloorByName(name);
                if (f == null)
                {
                    Vector3i p;
                    var err = AimedBlock(player, out p);
                    if (err != null) return err;
                    f = HSLiftConfiguration.FloorAt(p.y) ?? HSLiftConfiguration.FloorForPanel(p.y);
                }
                if (f == null) return "Aim at a landing that is a registered floor. Floors: " + HSLiftConfiguration.FloorList();
                if (f.Y == d.CurrentY) return "The car is parked at " + HSLiftConfiguration.FloorDisplayName(f.Name) + ". Send it to another floor first.";
                d.Floors.Remove(f);
                HSLiftConfiguration.SyncFloors();
                var worldRm = GameManager.Instance.World;
                if (worldRm != null) HSLiftCar.EnsureShaftRoof(worldRm);
                HSLiftConfiguration.Save();
                return HSLiftConfiguration.FloorDisplayName(f.Name) + " removed. Floors: " + HSLiftConfiguration.FloorList();
            }
        }
        return "Usage: hslift floor list | add <name> | ground | remove <name>";
    }

    static bool IsBasementName(string name)
    {
        return name != null && name.Length >= 2 && (name[0] == 'B' || name[0] == 'b') && char.IsDigit(name[1]);
    }

    // Aimed height becomes G. Stops above it become 1, 2, …; stops below become B1, B2, … (closest first).
    // The car stays where it is — this only renames / adds the ground stop.
    static void RelabelWithGroundAt(int groundY)
    {
        var d = HSLiftConfiguration.Data;
        if (d.Floors == null) return;
        d.Floors.RemoveAll(f => f == null);
        d.Floors.Sort((a, b) => a.Y.CompareTo(b.Y));
        int below = 0;
        for (int i = d.Floors.Count - 1; i >= 0; i--)
        {
            if (d.Floors[i].Y >= groundY) continue;
            below++;
            d.Floors[i].Name = "B" + below;
        }
        int above = 0;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            var f = d.Floors[i];
            if (f.Y < groundY) continue;
            if (f.Y == groundY) f.Name = "G";
            else
            {
                above++;
                f.Name = above.ToString();
            }
        }
    }

    // Corners always name the car's height G. If you then Add Floor on a landing below,
    // that lower stop becomes G and the car's floor becomes 1 (2, …). Real B1 stays B1.
    static void RelabelIfStopBelowGround()
    {
        var d = HSLiftConfiguration.Data;
        if (d.Floors == null || d.Floors.Count < 2) return;
        d.Floors.Sort((a, b) => a.Y.CompareTo(b.Y));
        HSLiftFloor g = null;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            if (string.Equals(d.Floors[i].Name, "G", StringComparison.OrdinalIgnoreCase))
            {
                g = d.Floors[i];
                break;
            }
        }
        if (g == null) return;
        bool numberedBelowG = false;
        int parsed;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            var f = d.Floors[i];
            if (f.Y < g.Y && int.TryParse(f.Name, out parsed) && parsed >= 1)
            {
                numberedBelowG = true;
                break;
            }
        }
        if (!numberedBelowG) return;
        int num = 1;
        for (int i = 0; i < d.Floors.Count; i++)
        {
            var f = d.Floors[i];
            if (IsBasementName(f.Name)) continue;
            f.Name = num == 1 ? "G" : (num - 1).ToString();
            num++;
        }
    }

    static string NextFloorName(int y)
    {
        var d = HSLiftConfiguration.Data;
        if (d.Floors.Count > 0 && y < d.Floors[0].Y)
        {
            int b = 1;
            while (HSLiftConfiguration.FloorByName("B" + b) != null) b++;
            return "B" + b;
        }
        int n = 1;
        while (HSLiftConfiguration.FloorByName(n.ToString()) != null) n++;
        return n.ToString();
    }

    static string ApplyCorners()
    {
        var d = HSLiftConfiguration.Data;
        int minX = Math.Min(d.Corner1[0], d.Corner2[0]), maxX = Math.Max(d.Corner1[0], d.Corner2[0]);
        int minY = Math.Min(d.Corner1[1], d.Corner2[1]), maxY = Math.Max(d.Corner1[1], d.Corner2[1]);
        int minZ = Math.Min(d.Corner1[2], d.Corner2[2]), maxZ = Math.Max(d.Corner1[2], d.Corner2[2]);
        if (HSLiftConfiguration.IsVehicle && minY != maxY)
        {
            d.HasCar = false;
            return "Vehicle lift is a platform only: both corners must be on the same Y (got Y" + minY + " and Y" + maxY + "). No walls or ceiling.";
        }
        foreach (var other in HSLiftConfiguration.Lifts)
        {
            if (other == d || other == null || !other.HasCar) continue;
            int ox = Math.Min(maxX, other.MinX + other.SizeX - 1) - Math.Max(minX, other.MinX) + 1;
            int oz = Math.Min(maxZ, other.MinZ + other.SizeZ - 1) - Math.Max(minZ, other.MinZ) + 1;
            if (ox <= 0 || oz <= 0 || ox == 1 || oz == 1) continue;
            d.HasCar = false;
            return "These corners overlap " + other.ElevatorId + "'s car (X" + other.MinX + " Z" + other.MinZ + ", " + other.SizeX + "x" + other.SizeZ
                + "). Two lifts may only share one wall line. Pick the other shaft's corners, or remove the old one: hslift delete " + other.ElevatorId;
        }
        bool moved = d.MinX != minX || d.MinZ != minZ || d.SizeX != maxX - minX + 1 || d.SizeZ != maxZ - minZ + 1;
        if (d.GaveWay != null) d.GaveWay.Clear();
        if (d.SharedPlaced != null) d.SharedPlaced.Clear();
        d.MinX = minX; d.MinZ = minZ;
        d.SizeX = maxX - minX + 1;
        d.SizeY = HSLiftConfiguration.IsVehicle ? 1 : maxY - minY + 1;
        d.SizeZ = maxZ - minZ + 1;
        string cleared = "";
        if (moved && (d.Floors.Count > 0 || d.Panels.Count > 0))
        {
            cleared = "\nNew shaft position: old floors and panels were cleared.";
            d.Floors.Clear();
            d.Panels.Clear();
            d.ExcludedColumns.Clear();
        }
        if (d.SizeX * d.SizeY * d.SizeZ > 512)
        {
            d.HasCar = false;
            return "Car too big (" + d.SizeX + "x" + d.SizeY + "x" + d.SizeZ + "). v0.1 limit is 512 cells.";
        }
        d.CurrentY = minY;
        d.HasCar = true;
        if (HSLiftConfiguration.FloorAt(minY) == null)
        {
            var name = "G";
            for (int i = 2; HSLiftConfiguration.FloorByName(name) != null; i++) name = "G" + i;
            d.Floors.Add(new HSLiftFloor { Name = name, Y = minY });
        }
        HSLiftConfiguration.SyncFloors();
        var world = GameManager.Instance.World;
        if (world != null && HSLiftCar.AutoExcludeDoorPlatforms(world) > 0)
            HSLiftConfiguration.Save();
        var check = HSLiftCar.ValidateCarAt(GameManager.Instance.World, d.CurrentY);
        var kind = HSLiftConfiguration.IsVehicle ? "Platform" : "Car";
        var extra = HSLiftConfiguration.IsVehicle ? " Garage / roll-up doors stay at each landing and are not part of the platform." : "";
        return string.Format("{0} set: {1}x{2}x{3} at X{4} Y{5} Z{6} ({7}).\nFloors: {8}{9}{10}{11}",
            kind, d.SizeX, d.SizeY, d.SizeZ, d.MinX, minY, d.MinZ, HSLiftConfiguration.FloorLabel(minY), HSLiftConfiguration.FloorList(),
            cleared, extra, check != null ? "\nWarning: " + check : "");
    }

    static string PanelCommand(string arg, EntityPlayerLocal player)
    {
        var d = HSLiftConfiguration.Data;
        if (arg == "clear")
        {
            d.Panels.Clear();
            HSLiftConfiguration.Save();
            return "All panels forgotten.";
        }
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        BindAimedLift(p);
        d = HSLiftConfiguration.Data;
        var world = GameManager.Instance.World;
        var aimedBv = world.GetBlock(p);
        var aimedParent = BlockHSLiftOutsidePanel.ParentPos(p, aimedBv);
        if (world.GetBlock(aimedParent).Block is BlockHSLiftInsidePanel)
        {
            bool onPad = d.HasCar && HSLiftCar.InFootprint(aimedParent.x, aimedParent.z)
                && aimedParent.y >= d.CurrentY && aimedParent.y <= d.CurrentY + 1;
            if (onPad)
                return "Inside panel is on this platform — it rides with the lift. Do not register it. Aim at an OUTSIDE button panel on the wall at each floor, then Register Panel.";
            return "Inside panel at " + aimedParent + " is not on this platform (Y" + d.CurrentY + ", X" + d.MinX + "-" + (d.MinX + d.SizeX - 1) + " Z" + d.MinZ + "-" + (d.MinZ + d.SizeZ - 1) + "). Place it on the pad.";
        }
        Vector3i found;
        if (!FindPanelNear(world, p, out found))
            return "No Elevator Outside Button Panel at or next to the aimed block (aimed: " + aimedBv.Block.GetBlockName() + " at " + p + "). Inside panels ride with the car and are not registered.";
        p = found;
        if (!d.HasCar || d.DistOutsideXZ(p.x, p.z) > HSLiftConfiguration.NearLiftBlocks)
            return "Panel at " + p + " is not beside " + d.ElevatorId + "'s shaft" + (d.HasCar ? " (car at X" + d.MinX + " Z" + d.MinZ + ")" : " (no car set yet)")
                + ". Set this lift's car corners first, or stand at the right lift.";
        var floor = HSLiftConfiguration.FloorForPanel(p.y);
        if (floor == null) return "Panel at " + p + " is not beside a floor. Add the floor first. Floors: " + HSLiftConfiguration.FloorList();
        d.Panels.RemoveAll(e => e.X == p.x && e.Y == p.y && e.Z == p.z);
        d.Panels.Add(new HSLiftPanelEntry { Stop = floor.Name, X = p.x, Y = p.y, Z = p.z });
        HSLiftConfiguration.ForgetPanelOnOtherLifts(d, p);
        HSLiftConfiguration.SyncFloors();
        HSLiftConfiguration.Save();
        return "Panel registered for " + HSLiftConfiguration.FloorDisplayName(floor.Name) + ". Wired to power: " + HSLiftPower.IsPanelPowered(p)
            + " (one powered panel runs the whole lift)";
    }

    static bool FindPanelNear(World world, Vector3i aimed, out Vector3i panel)
    {
        panel = aimed;
        int best = int.MaxValue;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            var p = new Vector3i(aimed.x + dx, aimed.y + dy, aimed.z + dz);
            var bv = world.GetBlock(p);
            if (!(bv.Block is BlockHSLiftOutsidePanel)) continue;
            int dist = dx * dx + dy * dy + dz * dz;
            if (dist >= best) continue;
            best = dist;
            panel = BlockHSLiftOutsidePanel.ParentPos(p, bv);
        }
        return best != int.MaxValue;
    }

    static void BindAimedLift(Vector3i p)
    {
        var d = HSLiftConfiguration.LiftAt(p) ?? HSLiftConfiguration.LiftNearXZ(p);
        if (d != null) HSLiftConfiguration.Use(d);
    }

    public static string AimedBlock(EntityPlayerLocal player, out Vector3i pos)
    {
        pos = Vector3i.zero;
        if (HasForcedAim)
        {
            pos = ForcedAim;
            return null;
        }
        var world = GameManager.Instance.World;
        if (player == null) player = world != null ? world.GetPrimaryPlayer() : null;
        if (player == null) return "No local player.";
        var hit = player.HitInfo;
        if (hit == null || !hit.bHitValid) return "Aim at a block first.";
        pos = hit.hit.blockPos;
        if (world.GetBlock(pos).isair) return "Aim at a block first.";
        return null;
    }
}
