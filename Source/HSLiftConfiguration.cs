using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class HSLiftPanelEntry
{
    public string Stop = "lower";
    public int X, Y, Z;

    [JsonIgnore]
    public Vector3i Pos { get { return new Vector3i(X, Y, Z); } }
}

public class HSLiftFloor
{
    public string Name;
    public int Y;
}

public class HSLiftConfigData
{
    public string ElevatorId = "lift1";
    public string Type = "ped";

    public int[] Corner1;
    public int[] Corner2;

    // Car box: X/Z footprint and height are fixed; CurrentY is the box's bottom row right now.
    public bool HasCar;
    public int MinX, MinZ, SizeX, SizeY, SizeZ;
    public int CurrentY;

    // Static lid on the SHAFT well at the real top of the walls. Half-cube, not cabin blocks. 0 = none yet.
    public int AutoShaftRoofY;

    // Car floor Y at each landing, kept sorted bottom to top.
    public List<HSLiftFloor> Floors = new List<HSLiftFloor>();

    // Lowest / highest floor, derived from Floors by SyncFloors (older configs only had these two stops).
    public int LowerY;
    public int UpperY;
    public bool HasUpper;

    public float SpeedBlocksPerSecond = 1.5f;
    public float[] ModelOffset = { 0.5f, 0.5f, 0.5f };

    public List<HSLiftPanelEntry> Panels = new List<HSLiftPanelEntry>();

    // Elevator doors this many blocks out from the car footprint count as lift doors.
    public int DoorReach = 2;

    // World X,Z columns inside the car box that are not part of the car (landing doors in the car's wall line).
    public List<int[]> ExcludedColumns = new List<int[]>();

    // Car cells that were not placed at GaveWayY because a landing block already sat there (sheet, plate double,
    // landing slab). The car still owns these; when it leaves, it takes them and leaves the landing block alone.
    public int GaveWayY;
    public List<HSLiftJournalCell> GaveWay = new List<HSLiftJournalCell>();
    // Car cells placed at GaveWayY inside a parked neighbour's box (shared wall line, cell was empty).
    // Without this a block there with no GaveWay entry on either lift is taken to be the neighbour's.
    public List<HSLiftJournalCell> SharedPlaced = new List<HSLiftJournalCell>();

    // Solid plate behind button panels so the empty part of their block isn't see-through.
    public bool PanelBacking; // leftover JSON field; ignored (old silver backing removed)

    // Block names containing any of these may sit in the shaft; the car moves past them without touching them.
    public List<string> PassThroughBlocks = new List<string> { "doorTrim", "doorTrim1m", "doorTrimCorner", "plateCorner", "sheet", "billboard", "plateDouble", "ladderRound" };

    // Vanilla sound names; empty string = silent.
    public string ArriveSound = "elevator_ding1";
    public string MoveLoopSound = "generator_large_run_lp";

    // Volume for custom clips in Sounds/ (these bypass the game's volume slider).
    public float CustomSoundVolume = 0.8f;

    public bool Debug;

    // gb (default): G, 1, 2. us: G shows as 1, 1 as 2, 2 as 3. Stored floor names stay GB.
    public string FloorScheme = "gb";

    [JsonIgnore]
    public bool IsVehicleType { get { return string.Equals(Type, "vehicle", StringComparison.OrdinalIgnoreCase); } }

    [JsonIgnore]
    public int Reach { get { return Math.Max(1, DoorReach <= 0 ? 2 : DoorReach); } }

    // Ped: cabin height. Vehicle: wall buttons and garage doors sit above the 1-high platform.
    [JsonIgnore]
    public int DoorHeight { get { return IsVehicleType ? Math.Max(4, SizeY) : Math.Max(1, SizeY); } }

    [JsonIgnore]
    public int RideHeight { get { return IsVehicleType ? Math.Max(5, SizeY) : Math.Max(1, SizeY); } }

    // 0 = inside the car footprint; 1, 2, ... = that many blocks outside (king-move / Chebyshev).
    public int DistOutsideXZ(int x, int z)
    {
        if (!HasCar) return int.MaxValue;
        int dx = 0, dz = 0;
        if (x < MinX) dx = MinX - x;
        else if (x > MinX + SizeX - 1) dx = x - (MinX + SizeX - 1);
        if (z < MinZ) dz = MinZ - z;
        else if (z > MinZ + SizeZ - 1) dz = z - (MinZ + SizeZ - 1);
        return Math.Max(dx, dz);
    }

    // DistOutsideXZ with ties broken by dx + dz: of two side-by-side shafts, the one the cell is square in
    // front of is nearer than the one it is only diagonal to.
    public int NearRank(int x, int z)
    {
        if (!HasCar) return int.MaxValue;
        int dx = 0, dz = 0;
        if (x < MinX) dx = MinX - x;
        else if (x > MinX + SizeX - 1) dx = x - (MinX + SizeX - 1);
        if (z < MinZ) dz = MinZ - z;
        else if (z > MinZ + SizeZ - 1) dz = z - (MinZ + SizeZ - 1);
        return Math.Max(dx, dz) * 1000 + dx + dz;
    }

    public bool InOutsideRing(int x, int z)
    {
        int d = DistOutsideXZ(x, z);
        return d >= 1 && d <= Reach;
    }

    public bool InDoorRing(int x, int z)
    {
        return DistOutsideXZ(x, z) <= Reach;
    }

    public bool YOnShaft(int y)
    {
        if (!HasCar) return false;
        int lo = LowerY;
        int hi = (HasUpper ? UpperY : LowerY) + DoorHeight - 1;
        return y >= lo && y <= hi + 1;
    }
}

public class HSLiftFile
{
    public string ActiveId;
    public bool Debug;
    // gb (default): G, 1, 2. us: G shows as 1, 1 as 2. Edit this; restart the game. No rebuild.
    public string FloorScheme = "gb";
    public bool? Music;
    public bool? FlickerLights;
    public List<HSLiftConfigData> Lifts = new List<HSLiftConfigData>();
}

public static class HSLiftConfiguration
{
    public static HSLiftConfigData Data = new HSLiftConfigData();
    public static List<HSLiftConfigData> Lifts = new List<HSLiftConfigData>();
    public static string ActiveId;
    public static string FileFloorScheme = "gb";

    // Never write into Mods/. The game hashes that folder; a server-created HSLift.json
    // makes joining clients look like they have the wrong mod.
    public static string RuntimeDir
    {
        get
        {
            try
            {
                var save = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(save)) return save;
            }
            catch { }
            return string.IsNullOrEmpty(HSLiftMod.UserDataPath) ? "." : HSLiftMod.UserDataPath;
        }
    }

    static string FilePath { get { return Path.Combine(RuntimeDir, "HSLift.json"); } }

    public static void EvacuateRuntimeFilesFromModFolder()
    {
        var mod = HSLiftMod.ModPath;
        var dest = HSLiftMod.UserDataPath;
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(dest) || !Directory.Exists(mod)) return;
        Directory.CreateDirectory(dest);
        MoveRuntimeFile(Path.Combine(mod, "HSLift.json"), Path.Combine(dest, "HSLift.json"));
        foreach (var f in Directory.GetFiles(mod, "HSLift*.journal.json"))
            MoveRuntimeFile(f, Path.Combine(dest, Path.GetFileName(f)));
    }

    static void MoveRuntimeFile(string from, string to)
    {
        if (!File.Exists(from)) return;
        try
        {
            if (!File.Exists(to)) File.Copy(from, to);
            File.Delete(from);
            HSLiftDebug.Info("Moved " + Path.GetFileName(from) + " out of Mods so server and clients keep the same folder.");
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Could not move " + from + " out of the mod folder", e);
        }
    }

    static void AdoptPendingSaveIfNeeded()
    {
        var pending = string.IsNullOrEmpty(HSLiftMod.UserDataPath) ? null : Path.Combine(HSLiftMod.UserDataPath, "HSLift.json");
        if (string.IsNullOrEmpty(pending) || !File.Exists(pending)) return;
        if (File.Exists(FilePath)) return;
        var dir = RuntimeDir;
        if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(HSLiftMod.UserDataPath), StringComparison.OrdinalIgnoreCase)) return;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.Copy(pending, FilePath);
        HSLiftDebug.Info("Copied lift list into this world save.");
    }

    // The lift the setup tool is editing. Only New Lift and Use This Lift call this.
    public static void Use(HSLiftConfigData d)
    {
        if (d == null) return;
        Data = d;
        ActiveId = d.ElevatorId;
    }

    // A door, call button or move. Does not change which lift is being edited.
    public static void Operate(HSLiftConfigData d)
    {
        if (d == null) return;
        Data = d;
    }

    public static void Editing()
    {
        var d = ById(ActiveId);
        if (d != null) Data = d;
    }

    public static string HoldingHud()
    {
        var d = ById(ActiveId);
        if (d == null) return "No lift";
        int corners = (d.Corner1 != null ? 1 : 0) + (d.Corner2 != null ? 1 : 0);
        var corner = d.HasCar ? "Corners set" : "Corners " + corners + " of 2";
        int floors = d.Floors != null ? d.Floors.Count : 0;
        int panels = d.Panels != null ? d.Panels.Count : 0;
        return d.ElevatorId + "\n" + Lifts.Count + " lifts\n" + corner + "\nFloors " + floors + "\nPanels " + panels;
    }

    static HSLiftFile MakeFile()
    {
        return new HSLiftFile
        {
            ActiveId = ActiveId,
            Debug = HSLiftDebug.Enabled,
            FloorScheme = FileFloorScheme,
            Music = HSLiftSettings.Music,
            FlickerLights = HSLiftSettings.FlickerLights,
            Lifts = Lifts
        };
    }

    public static string ToSyncJson()
    {
        return JsonConvert.SerializeObject(MakeFile());
    }

    public static void ApplyFromServer(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var file = JsonConvert.DeserializeObject<HSLiftFile>(json, settings) ?? new HSLiftFile();
            HSLiftSettings.ApplyFromServer(
                file.Music.HasValue ? file.Music.Value : true,
                file.FlickerLights.HasValue ? file.FlickerLights.Value : true);
            Lifts = file.Lifts != null ? file.Lifts : new List<HSLiftConfigData>();
            ActiveId = file.ActiveId;
            FileFloorScheme = NormalizeFloorScheme(string.IsNullOrEmpty(file.FloorScheme) ? "gb" : file.FloorScheme);
            if (Lifts.Count == 0) Lifts.Add(new HSLiftConfigData());
            foreach (var d in Lifts) Normalize(d);
            var active = ById(ActiveId) ?? Lifts[0];
            Use(active);
            HSLiftDebug.Enabled = file.Debug;
            foreach (var d in Lifts)
            {
                if (d.Debug) HSLiftDebug.Enabled = true;
                HSLiftController.Ensure(d);
            }
            HSLiftDebug.Info("Got " + Lifts.Count + " lift(s) from server. Editing " + Data.ElevatorId);
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Could not apply lift list from server", e);
        }
    }

    public static void Load()
    {
        if (HSLiftNet.IsRemoteClient)
        {
            Lifts = new List<HSLiftConfigData>();
            Lifts.Add(new HSLiftConfigData());
            FileFloorScheme = "gb";
            Use(Lifts[0]);
            HSLiftDebug.Info("Client: waiting for the server lift list");
            return;
        }
        Lifts = new List<HSLiftConfigData>();
        FileFloorScheme = "gb";
        try
        {
            AdoptPendingSaveIfNeeded();
            if (File.Exists(FilePath))
            {
                var raw = File.ReadAllText(FilePath);
                var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
                if (raw.IndexOf("\"Lifts\"", StringComparison.Ordinal) >= 0)
                {
                    var file = JsonConvert.DeserializeObject<HSLiftFile>(raw, settings) ?? new HSLiftFile();
                    if (file.Lifts != null) Lifts.AddRange(file.Lifts);
                    ActiveId = file.ActiveId;
                    FileFloorScheme = ResolveFloorScheme(file.FloorScheme, Lifts);
                    Data = new HSLiftConfigData { Debug = file.Debug };
                    HSLiftSettings.TakeFromFile(file);
                }
                else
                {
                    var one = JsonConvert.DeserializeObject<HSLiftConfigData>(raw, settings);
                    if (one != null)
                    {
                        Lifts.Add(one);
                        FileFloorScheme = ResolveFloorScheme(one.FloorScheme, Lifts);
                    }
                }
            }
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Config load failed, using defaults", e);
            Lifts.Clear();
        }
        if (Lifts.Count == 0) Lifts.Add(new HSLiftConfigData());
        FileFloorScheme = NormalizeFloorScheme(string.IsNullOrEmpty(FileFloorScheme) ? ResolveFloorScheme(null, Lifts) : FileFloorScheme);
        foreach (var d in Lifts) Normalize(d);
        DedupePanels();
        var active = ById(ActiveId) ?? Lifts[0];
        Use(active);
        HSLiftDebug.Enabled = Data.Debug;
        foreach (var d in Lifts)
            if (d.Debug) HSLiftDebug.Enabled = true;
        Save();
        HSLiftDebug.Info("Config loaded: " + Lifts.Count + " lift(s), active " + Data.ElevatorId + " — " + Summary());
    }

    static void Normalize(HSLiftConfigData d)
    {
        if (d.Panels == null) d.Panels = new List<HSLiftPanelEntry>();
        if (d.Floors == null) d.Floors = new List<HSLiftFloor>();
        if (d.PassThroughBlocks == null) d.PassThroughBlocks = new List<string>();
        if (d.ExcludedColumns == null) d.ExcludedColumns = new List<int[]>();
        d.PassThroughBlocks.RemoveAll(p => string.Equals(p, "plate", StringComparison.OrdinalIgnoreCase));
        d.PassThroughBlocks = new List<string>(new HashSet<string>(d.PassThroughBlocks, StringComparer.OrdinalIgnoreCase));
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "sheet", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("sheet");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "billboard", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("billboard");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "signLetter", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("signLetter");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "signNumber", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("signNumber");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "plateDouble", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("plateDouble");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "ladderRound", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("ladderRound");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "doorTrim", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("doorTrim");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "doorTrim1m", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("doorTrim1m");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "doorTrimCorner", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("doorTrimCorner");
        if (!d.PassThroughBlocks.Exists(p => string.Equals(p, "plateCorner", StringComparison.OrdinalIgnoreCase)))
            d.PassThroughBlocks.Add("plateCorner");
        if (string.IsNullOrEmpty(d.ElevatorId)) d.ElevatorId = "lift1";
        d.FloorScheme = FileFloorScheme;
        if (d.Floors.Count == 0 && d.HasCar)
        {
            d.Floors.Add(new HSLiftFloor { Name = "G", Y = d.LowerY });
            if (d.HasUpper && d.UpperY != d.LowerY) d.Floors.Add(new HSLiftFloor { Name = "1", Y = d.UpperY });
        }
        if (d.ModelOffset == null || d.ModelOffset.Length != 3) d.ModelOffset = new[] { 0.5f, 0.5f, 0.5f };
        if (d.SpeedBlocksPerSecond <= 0.05f) d.SpeedBlocksPerSecond = 1.5f;
        if (d.DoorReach <= 0) d.DoorReach = 2;
        if (d.GaveWay == null) d.GaveWay = new List<HSLiftJournalCell>();
        if (d.SharedPlaced == null) d.SharedPlaced = new List<HSLiftJournalCell>();
        var saved = Data;
        Data = d;
        SyncFloors();
        HSLiftCar.DropInteriorFloorExcludes();
        Data = saved;
    }

    public static void Save()
    {
        try
        {
            if (Data != null)
            {
                var i = Lifts.FindIndex(l => l.ElevatorId == Data.ElevatorId);
                if (i >= 0) Lifts[i] = Data;
                else if (!Lifts.Contains(Data)) Lifts.Add(Data);
            }
            if (HSLiftNet.IsRemoteClient) return;
            var dir = RuntimeDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(MakeFile(), Formatting.Indented));
            HSLiftNet.BroadcastConfig();
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Config save failed", e);
        }
    }

    public static HSLiftConfigData ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Lifts.Find(l => string.Equals(l.ElevatorId, id, StringComparison.OrdinalIgnoreCase));
    }

    public static HSLiftConfigData NewLift(string type)
    {
        if (type != "ped" && type != "vehicle") type = "ped";
        var prefix = type == "vehicle" ? "vehicle" : "lift";
        int n = 1;
        while (ById(prefix + n) != null) n++;
        var d = new HSLiftConfigData
        {
            ElevatorId = prefix + n,
            Type = type,
            DoorReach = 2,
            PassThroughBlocks = type == "vehicle"
                ? new List<string> { "doorTrim", "doorTrim1m", "doorTrimCorner", "plateCorner", "GarageDoor", "rollUpDoor", "rollUpGate", "sheet", "billboard", "plateDouble", "ladderRound" }
                : new List<string> { "doorTrim", "doorTrim1m", "doorTrimCorner", "plateCorner", "sheet", "billboard", "plateDouble", "ladderRound" },
            ArriveSound = "elevator_ding1",
            MoveLoopSound = "generator_large_run_lp",
            CustomSoundVolume = 0.8f,
            FloorScheme = FileFloorScheme
        };
        Lifts.Add(d);
        Use(d);
        HSLiftController.Ensure(d);
        Save();
        return d;
    }

    public static string SelectNearest(Vector3i pos)
    {
        var d = LiftAt(pos);
        if (d == null) return "No lift at this block (2 blocks outside a registered car). Start one: hslift type ped | vehicle, then set corners.";
        Use(d);
        Save();
        return "Now editing " + d.ElevatorId + " (" + d.Type + "). " + Summary();
    }

    // Registered outside panel, or the lift whose 2-block outside ring contains this cell.
    // Only a panel that was registered to a lift belongs to it. Nearby unregistered panels stay unlinked.
    // Registered to more than one lift (older saves): the one whose shaft it is beside wins.
    public static HSLiftConfigData RegisteredPanelOwner(Vector3i pos)
    {
        HSLiftConfigData best = null;
        int bestD = int.MaxValue;
        foreach (var d in Lifts)
        {
            if (d.Panels == null || !d.Panels.Exists(p => p.X == pos.x && p.Y == pos.y && p.Z == pos.z)) continue;
            int dist = d.NearRank(pos.x, pos.z);
            if (best == null || dist < bestD) { best = d; bestD = dist; }
        }
        return best;
    }

    // Keep each panel position on one lift only, so pressing it never checks another shaft's panels.
    static void DedupePanels()
    {
        foreach (var d in Lifts)
        {
            if (d.Panels == null) continue;
            foreach (var p in d.Panels.ToArray())
            {
                var pos = p.Pos;
                var owner = RegisteredPanelOwner(pos);
                if (owner == null || owner == d) continue;
                d.Panels.RemoveAll(e => e.X == pos.x && e.Y == pos.y && e.Z == pos.z);
                HSLiftDebug.Info("Panel at " + pos + " was registered to " + d.ElevatorId + " and " + owner.ElevatorId + "; kept on " + owner.ElevatorId + " (its shaft is beside it).");
            }
        }
    }

    public static void ForgetPanelOnOtherLifts(HSLiftConfigData keep, Vector3i pos)
    {
        foreach (var d in Lifts)
        {
            if (d == keep || d.Panels == null) continue;
            if (d.Panels.RemoveAll(e => e.X == pos.x && e.Y == pos.y && e.Z == pos.z) > 0)
                HSLiftDebug.Info("Panel at " + pos + " moved from " + d.ElevatorId + " to " + keep.ElevatorId);
        }
    }

    public static HSLiftConfigData LiftForOutsidePanel(Vector3i pos)
    {
        return RegisteredPanelOwner(pos) ?? LiftNearOutside(pos);
    }

    public static string ListLifts()
    {
        if (Lifts.Count == 0) return "No lifts.";
        var parts = new List<string>();
        foreach (var d in Lifts)
        {
            var mark = d.ElevatorId == ActiveId ? "* " : "  ";
            var car = d.HasCar ? ("X" + d.MinX + " Z" + d.MinZ + " Y" + d.CurrentY + " " + d.SizeX + "x" + d.SizeY + "x" + d.SizeZ) : "no car yet";
            parts.Add(mark + d.ElevatorId + " (" + d.Type + ") " + car);
        }
        return "Lifts:\n" + string.Join("\n", parts.ToArray());
    }

    public static HSLiftConfigData LiftNearOutside(Vector3i pos)
    {
        HSLiftConfigData best = null;
        int bestD = int.MaxValue;
        foreach (var d in Lifts)
        {
            if (!d.HasCar || !d.YOnShaft(pos.y) || !d.InOutsideRing(pos.x, pos.z)) continue;
            int dist = d.NearRank(pos.x, pos.z);
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    public static HSLiftConfigData LiftForInsidePanel(Vector3i pos)
    {
        foreach (var d in Lifts)
        {
            if (!d.HasCar) continue;
            var saved = Data;
            Data = d;
            bool inCar = HSLiftCar.InBox(pos, d.CurrentY);
            if (!inCar && d.IsVehicleType && HSLiftCar.InFootprint(pos.x, pos.z) && pos.y >= d.CurrentY && pos.y <= d.CurrentY + 1)
            {
                var world = GameManager.Instance.World;
                inCar = world != null && world.GetBlock(pos).Block is BlockHSLiftInsidePanel;
            }
            Data = saved;
            if (inCar) return d;
        }
        return null;
    }

    public static HSLiftConfigData LiftForDoor(Vector3i pos)
    {
        HSLiftConfigData best = null;
        int bestD = int.MaxValue;
        foreach (var d in Lifts)
        {
            if (!d.HasCar || !d.YOnShaft(pos.y) || !d.InDoorRing(pos.x, pos.z)) continue;
            int dist = d.DistOutsideXZ(pos.x, pos.z);
            if (dist >= 2 && WalledOff(d, pos)) continue;
            int rank = d.NearRank(pos.x, pos.z);
            if (rank < bestD) { bestD = rank; best = d; }
        }
        return best;
    }

    // A door further out than the shaft wall only belongs to this car if nothing solid stands between them
    // (thick doorway). A door behind a wall is another shaft's door, even when that shaft has no lift yet.
    public static bool WalledOff(HSLiftConfigData d, Vector3i door)
    {
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return false;
        int cx = Math.Max(d.MinX, Math.Min(d.MinX + d.SizeX - 1, door.x));
        int cz = Math.Max(d.MinZ, Math.Min(d.MinZ + d.SizeZ - 1, door.z));
        int sx = Math.Sign(cx - door.x), sz = Math.Sign(cz - door.z);
        var saved = Data;
        Data = d;
        try
        {
            int x = door.x + sx, z = door.z + sz;
            while (x != cx || z != cz)
            {
                var bv = world.GetBlock(new Vector3i(x, door.y, z));
                if (!bv.isair && !HSLiftDoors.IsCandidateDoor(bv.Block) && !HSLiftCar.IsPassThrough(bv)) return true;
                if (x != cx) x += sx;
                if (z != cz) z += sz;
            }
            return false;
        }
        finally { Data = saved; }
    }

    public static HSLiftConfigData LiftAt(Vector3i pos)
    {
        return LiftForInsidePanel(pos) ?? LiftForDoor(pos) ?? LiftForOutsidePanel(pos);
    }

    public const int NearLiftBlocks = 8;

    public static HSLiftConfigData LiftNearXZ(Vector3i pos)
    {
        HSLiftConfigData best = null;
        int bestD = int.MaxValue;
        foreach (var d in Lifts)
        {
            if (!d.HasCar) continue;
            int dist = d.DistOutsideXZ(pos.x, pos.z);
            if (dist > NearLiftBlocks) continue;
            int rank = d.NearRank(pos.x, pos.z);
            if (rank < bestD || (rank == bestD && d.ElevatorId == ActiveId)) { bestD = rank; best = d; }
        }
        return best;
    }

    public static HSLiftConfigData LiftForPlayer(EntityPlayerLocal player)
    {
        if (player == null) return null;
        var feet = new Vector3i(UnityEngine.Mathf.FloorToInt(player.position.x), UnityEngine.Mathf.FloorToInt(player.position.y + 0.1f), UnityEngine.Mathf.FloorToInt(player.position.z));
        foreach (var d in Lifts)
        {
            if (!d.HasCar) continue;
            var saved = Data;
            Data = d;
            bool inCar = HSLiftCar.InFootprint(feet.x, feet.z) && feet.y >= d.CurrentY && feet.y <= d.CurrentY + d.RideHeight;
            Data = saved;
            if (inCar) return d;
        }
        return null;
    }

    public static bool IsVehicle { get { return string.Equals(Data.Type, "vehicle", StringComparison.OrdinalIgnoreCase); } }

    // Sort floors, refresh the derived lowest/highest stop, and relabel panels with the floor they serve.
    public static void SyncFloors()
    {
        var d = Data;
        d.Floors.RemoveAll(f => f == null || string.IsNullOrEmpty(f.Name));
        d.Floors.Sort((a, b) => a.Y.CompareTo(b.Y));
        d.HasUpper = d.Floors.Count >= 2;
        if (d.Floors.Count > 0)
        {
            d.LowerY = d.Floors[0].Y;
            d.UpperY = d.Floors[d.Floors.Count - 1].Y;
        }
        foreach (var p in d.Panels)
        {
            var f = FloorForPanel(p.Y);
            p.Stop = f != null ? f.Name : "?";
        }
    }

    public static HSLiftFloor FloorAt(int y)
    {
        return Data.Floors.Find(f => f.Y == y);
    }

    public static HSLiftFloor FloorByName(string name)
    {
        return Data.Floors.Find(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static int FloorIndexAt(int y)
    {
        return Data.Floors.FindIndex(f => f.Y == y);
    }

    // A panel serves the highest floor at or below it, as long as it sits within the car's height of that floor.
    public static HSLiftFloor FloorForPanel(int panelY)
    {
        return FloorForPanel(Data, panelY);
    }

    public static HSLiftFloor FloorForPanel(HSLiftConfigData d, int panelY)
    {
        if (d == null || d.Floors == null) return null;
        int h = d.DoorHeight;
        HSLiftFloor best = null;
        foreach (var f in d.Floors)
            if (f.Y <= panelY && panelY < f.Y + h) best = f;
        return best;
    }

    public static string FloorLabel(int y)
    {
        var f = FloorAt(y);
        return f != null ? FloorDisplayName(f.Name) : "Y" + y + " (between floors)";
    }

    public static bool IsUsFloorScheme()
    {
        return string.Equals(FileFloorScheme, "us", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeFloorScheme(string scheme)
    {
        if (string.IsNullOrEmpty(scheme)) return "gb";
        var s = scheme.Trim().ToLowerInvariant();
        if (s == "us" || s == "usa") return "us";
        return "gb";
    }

    static string ResolveFloorScheme(string fileScheme, List<HSLiftConfigData> lifts)
    {
        if (!string.IsNullOrEmpty(fileScheme)) return NormalizeFloorScheme(fileScheme);
        if (lifts != null)
        {
            foreach (var d in lifts)
                if (d != null && !string.IsNullOrEmpty(d.FloorScheme))
                    return NormalizeFloorScheme(d.FloorScheme);
        }
        return "gb";
    }

    public static void SetFloorScheme(string scheme)
    {
        FileFloorScheme = NormalizeFloorScheme(scheme);
        foreach (var d in Lifts)
            if (d != null) d.FloorScheme = FileFloorScheme;
        if (Data != null) Data.FloorScheme = FileFloorScheme;
        Save();
    }

    static bool IsGroundName(string n)
    {
        return n.Equals("G", StringComparison.OrdinalIgnoreCase)
            || n.Equals("GF", StringComparison.OrdinalIgnoreCase)
            || n.Equals("ground", StringComparison.OrdinalIgnoreCase);
    }

    // Wall letter/number for this stored floor name (G stays G in GB, becomes 1 in US).
    public static string FloorSignToken(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var n = name.Trim();
        if (IsUsFloorScheme())
        {
            if (IsGroundName(n)) return "1";
            int shifted;
            if (int.TryParse(n, out shifted) && shifted >= 0)
            {
                shifted++;
                if (shifted > 9) shifted = 9;
                return shifted.ToString();
            }
        }
        return n[0].ToString();
    }

    // Full short label for writable signs and menu icons (G, B2, 10; US scheme shifts numbers up one).
    public static string FloorSignText(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var n = name.Trim();
        if (IsGroundName(n)) return IsUsFloorScheme() ? "1" : "G";
        int num;
        if (IsUsFloorScheme() && int.TryParse(n, out num) && num >= 0) return (num + 1).ToString();
        return n.ToUpperInvariant();
    }

    // Menu and tooltips. Console still uses the short stored name (G, 1, B1).
    public static string FloorDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown Floor";
        var n = name.Trim();
        bool us = IsUsFloorScheme();
        if (IsGroundName(n))
            return us ? "1st Floor" : "Ground Floor";
        if (n.Length >= 2 && (n[0] == 'B' || n[0] == 'b') && char.IsDigit(n[1]))
        {
            int basement;
            if (int.TryParse(n.Substring(1), out basement) && basement > 0)
                return "Basement " + basement;
        }
        int num;
        if (int.TryParse(n, out num) && num >= 0)
            return Ordinal(us ? num + 1 : num) + " Floor";
        return n;
    }

    static string Ordinal(int n)
    {
        int m = n % 100;
        if (m >= 11 && m <= 13) return n + "th";
        switch (n % 10)
        {
            case 1: return n + "st";
            case 2: return n + "nd";
            case 3: return n + "rd";
            default: return n + "th";
        }
    }

    public static string FloorList()
    {
        var d = Data;
        if (d.Floors.Count == 0) return "no floors";
        var parts = new List<string>();
        for (int i = d.Floors.Count - 1; i >= 0; i--)
            parts.Add(d.Floors[i].Name + "=Y" + d.Floors[i].Y + (d.Floors[i].Y == d.CurrentY ? "*" : ""));
        return string.Join(", ", parts.ToArray());
    }

    public static string Summary()
    {
        var d = Data;
        var car = d.HasCar
            ? string.Format("car {0}x{1}x{2} at X{3} Z{4}, now Y{5}", d.SizeX, d.SizeY, d.SizeZ, d.MinX, d.MinZ, d.CurrentY)
            : "car not set";
        return string.Format("id={0} type={1} {2}, floors [{3}] {4}, speed {5}, panels {6}, debug {7}",
            d.ElevatorId, d.Type, car, FloorList(), d.FloorScheme, d.SpeedBlocksPerSecond, d.Panels.Count, d.Debug);
    }
}
