using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// Server owns HSLift.json, block capture/place, and doors. Clients send uses/setup
// and play a moving copy so everyone sees and rides the same cars.
public static class HSLiftNet
{
    public const byte Config = 1;
    public const byte Setup = 2;
    public const byte UsePanel = 3;
    public const byte UseInside = 4;
    public const byte UseDoor = 5;
    public const byte UseFloor = 6;
    public const byte Doors = 7;
    public const byte MoveStart = 8;
    public const byte MoveEnd = 9;
    public const byte Tip = 10;

    public static bool IsAuthority
    {
        get
        {
            try
            {
                if (GameManager.IsDedicatedServer) return true;
                var cm = ConnectionManager.Instance;
                if (cm != null) return cm.IsServer;
            }
            catch { }
            return true;
        }
    }

    public static bool IsRemoteClient
    {
        get
        {
            try
            {
                var cm = ConnectionManager.Instance;
                return cm != null && cm.IsClient && !cm.IsServer;
            }
            catch { }
            return false;
        }
    }

    static Type pkgType;

    public static void RegisterPackage()
    {
        try
        {
            var t = HSGameVersion.Is33
                ? HSGameApi.NetPackageType33("NetPackageHSLift", typeof(NetPackageHSLiftCore))
                : HSGameApi.NetPackageType32("NetPackageHSLift", typeof(NetPackageHSLiftCore));
            pkgType = t;
            var f = typeof(NetPackageManager).GetField("knownPackageTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;
            var dict = f.GetValue(null) as IDictionary;
            if (dict == null) return;
            var args = f.FieldType.GetGenericArguments();
            if (args != null && args.Length >= 1 && args[0] == typeof(string))
                dict[t.Name] = t;
            else if (args != null && args.Length >= 1 && args[0] == typeof(Type))
                dict[t] = t.Name;
            else
                dict[t.Name] = t;
            HSLiftDebug.Info("Registered " + t.Name + " (" + (HSGameVersion.Is33 ? "3.3 emit" : "3.2 emit") + ")");
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Net package register failed", e);
        }
    }

    static NetPackageHSLiftCore Pkg()
    {
        if (pkgType == null) RegisterPackage();
        return (NetPackageHSLiftCore)HSGameApi.GetNetPackage(pkgType);
    }

    static void ToServer(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null) return;
        cm.SendToClientsOrServer(pkg);
    }

    static void ToClients(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer) return;
        cm.SendPackage(pkg);
    }

    static void ToClient(ClientInfo ci, NetPackage pkg)
    {
        if (ci != null) ci.SendPackage(pkg);
        else ToClients(pkg);
    }

    public static string SendSetup(string sub, string arg, string extra, EntityPlayerLocal player)
    {
        Vector3i aim = Vector3i.zero;
        bool hasAim = HSLiftSetup.AimedBlock(player, out aim) == null;
        ToServer(Pkg().SetupCmd(Setup, sub ?? "", arg ?? "", extra ?? "", "", 0, 0, hasAim, aim, false, null, 0f));
        return null;
    }

    public static void SendUsePanel(Vector3i pos)
    {
        ToServer(Pkg().SetupCmd(UsePanel, "", "", "", "", 0, 0, true, pos, false, null, 0f));
    }

    public static void SendUseDoor(Vector3i pos)
    {
        ToServer(Pkg().SetupCmd(UseDoor, "", "", "", "", 0, 0, true, pos, false, null, 0f));
    }

    public static void SendUseInside(Vector3i pos, int floorIndex)
    {
        HSLiftDebug.Info("Send UseInside floor " + floorIndex + " at " + pos);
        ToServer(Pkg().SetupCmd(UseInside, "", "", "", "", floorIndex, 0, true, pos, false, null, 0f));
    }

    public static void SendUseFloor(string liftId, int targetY, int floorIndex)
    {
        HSLiftDebug.Info("Send UseFloor " + liftId + " floor " + floorIndex + " Y" + targetY);
        ToServer(Pkg().SetupCmd(UseFloor, "", "", "", liftId ?? "", floorIndex, targetY, false, Vector3i.zero, false, null, 0f));
    }

    public static void SendDoors(string liftId, bool open)
    {
        ToServer(Pkg().SetupCmd(Doors, "", "", "", liftId ?? "", 0, 0, false, Vector3i.zero, open, null, 0f));
    }

    public static void BroadcastConfig()
    {
        if (!IsAuthority) return;
        try
        {
            var json = HSLiftConfiguration.ToSyncJson();
            ToClients(Pkg().SetupCmd(Config, json, "", "", "", 0, 0, false, Vector3i.zero, false, null, 0f));
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Broadcast lift list failed", e);
        }
    }

    public static void SendConfigTo(ClientInfo ci)
    {
        if (!IsAuthority || ci == null) return;
        try
        {
            var json = HSLiftConfiguration.ToSyncJson();
            ToClient(ci, Pkg().SetupCmd(Config, json, "", "", "", 0, 0, false, Vector3i.zero, false, null, 0f));
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Send lift list failed", e);
        }
    }

    public static void BroadcastMoveStart(string liftId, int fromY, int targetY, float curY, List<HSLiftCell> cells, string musicFile)
    {
        if (!IsAuthority) return;
        ToClients(Pkg().SetupCmd(MoveStart, musicFile ?? "", "", "", liftId ?? "", 0, targetY, true, new Vector3i(0, fromY, 0), false, cells, curY));
    }

    public static void SendMoveStartTo(ClientInfo ci, string liftId, int fromY, int targetY, float curY, List<HSLiftCell> cells, string musicFile)
    {
        if (!IsAuthority || ci == null) return;
        ToClient(ci, Pkg().SetupCmd(MoveStart, musicFile ?? "", "", "", liftId ?? "", 0, targetY, true, new Vector3i(0, fromY, 0), false, cells, curY));
    }

    public static void BroadcastMoveEnd(string liftId, int y)
    {
        if (!IsAuthority) return;
        ToClients(Pkg().SetupCmd(MoveEnd, "", "", "", liftId ?? "", 0, y, false, Vector3i.zero, false, null, 0f));
    }

    public static void ReplyTip(ClientInfo ci, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (ci != null)
        {
            ToClient(ci, Pkg().SetupCmd(Tip, msg, "", "", "", 0, 0, false, Vector3i.zero, false, null, 0f));
            return;
        }
        TellLocal(msg);
    }

    public static string NotReady(string problem)
    {
        if (string.IsNullOrEmpty(problem)) return null;
        return string.Format(Localization.Get("hsliftNotReady"), problem);
    }

    public static void TellLocal(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var locals = world != null ? world.GetLocalPlayers() : null;
            if (locals == null) return;
            for (int i = 0; i < locals.Count; i++)
            {
                var p = locals[i] as EntityPlayerLocal;
                if (p != null) GameManager.ShowTooltip(p, msg);
            }
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Local tip failed: " + e.Message);
        }
    }

    public static void TellPlayer(EntityPlayer player, string msg)
    {
        if (string.IsNullOrEmpty(msg) || player == null) return;
        var local = player as EntityPlayerLocal;
        if (local != null)
        {
            GameManager.ShowTooltip(local, msg);
            return;
        }
        try { GameManager.ShowTooltipMP(player, msg); }
        catch { }
    }

    public static EntityPlayer PlayerFromSender(ClientInfo ci, World world)
    {
        if (ci == null || world == null) return null;
        return world.GetEntity(ci.entityId) as EntityPlayer;
    }

    public static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!IsAuthority || data.ClientInfo == null) return;
        SendConfigTo(data.ClientInfo);
        HSLiftController.SendActiveMoves(data.ClientInfo);
    }
}

public abstract class NetPackageHSLiftCore : NetPackage
{
    protected byte kind;
    protected string text;
    protected string arg;
    protected string extra;
    protected string liftId;
    protected int floorIndex;
    protected int targetY;
    protected bool hasPos;
    protected Vector3i pos;
    protected bool flag;
    protected float curY;
    protected List<HSLiftCell> cells;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.Both; } }

    public NetPackageHSLiftCore SetupCmd(byte k, string t, string a, string e, string id, int floor, int y, bool has, Vector3i p, bool f, List<HSLiftCell> c, float cy)
    {
        kind = k;
        text = t ?? "";
        arg = a ?? "";
        extra = e ?? "";
        liftId = id ?? "";
        floorIndex = floor;
        targetY = y;
        hasPos = has;
        pos = p;
        flag = f;
        curY = cy;
        cells = c;
        return this;
    }

    public override void read(PooledBinaryReader br)
    {
        kind = br.ReadByte();
        text = br.ReadString();
        arg = br.ReadString();
        extra = br.ReadString();
        liftId = br.ReadString();
        floorIndex = br.ReadInt32();
        targetY = br.ReadInt32();
        hasPos = br.ReadBoolean();
        pos = new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
        flag = br.ReadBoolean();
        curY = br.ReadSingle();
        int n = br.ReadInt32();
        int texN = br.ReadInt32();
        cells = null;
        if (n <= 0) return;
        cells = new List<HSLiftCell>(n);
        for (int i = 0; i < n; i++)
        {
            var cell = new HSLiftCell();
            cell.Dx = br.ReadInt32();
            cell.Dy = br.ReadInt32();
            cell.Dz = br.ReadInt32();
            uint raw = br.ReadUInt32();
            int dmg = br.ReadInt32();
            cell.Bv = new BlockValue(raw, dmg);
            cell.Density = br.ReadSByte();
            var tex = TextureFullArray.Default;
            for (int t = 0; t < texN; t++)
            {
                long v = br.ReadInt64();
                if (t < 32) tex[t] = v;
            }
            cell.Tex = tex;
            cells.Add(cell);
        }
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(kind);
        bw.Write(text ?? "");
        bw.Write(arg ?? "");
        bw.Write(extra ?? "");
        bw.Write(liftId ?? "");
        bw.Write(floorIndex);
        bw.Write(targetY);
        bw.Write(hasPos);
        bw.Write(pos.x);
        bw.Write(pos.y);
        bw.Write(pos.z);
        bw.Write(flag);
        bw.Write(curY);
        int n = cells != null ? cells.Count : 0;
        bw.Write(n);
        int texN = n > 0 ? HSLiftCar.NetTexChannels : 0;
        bw.Write(texN);
        if (n == 0) return;
        for (int i = 0; i < n; i++)
        {
            var c = cells[i];
            bw.Write(c.Dx);
            bw.Write(c.Dy);
            bw.Write(c.Dz);
            bw.Write(c.Bv.rawData);
            bw.Write(c.Bv.damage);
            bw.Write(c.Density);
            for (int t = 0; t < texN; t++)
                bw.Write(c.Tex[t]);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        try
        {
            if (world == null) return;
            switch (kind)
            {
                case HSLiftNet.Config:
                    if (HSLiftNet.IsAuthority) return;
                    HSLiftConfiguration.ApplyFromServer(text);
                    break;
                case HSLiftNet.Setup:
                    if (!HSLiftNet.IsAuthority) return;
                    HSLiftSetup.HasForcedAim = hasPos;
                    HSLiftSetup.ForcedAim = pos;
                    string result;
                    try { result = HSLiftSetup.Execute(text, arg, extra, null); }
                    finally { HSLiftSetup.HasForcedAim = false; }
                    HSLiftNet.ReplyTip(Sender, result);
                    break;
                case HSLiftNet.UsePanel:
                    if (!HSLiftNet.IsAuthority) return;
                    HSLiftNet.ReplyTip(Sender, HSLiftNet.NotReady(HSLiftController.RequestFromOutsidePanel(pos)));
                    break;
                case HSLiftNet.UseInside:
                    if (!HSLiftNet.IsAuthority) return;
                    HSLiftDebug.Info("Got UseInside floor " + floorIndex + " at " + pos);
                    HSLiftNet.ReplyTip(Sender, HSLiftNet.NotReady(HSLiftController.RequestFromInsidePanel(pos, floorIndex)));
                    break;
                case HSLiftNet.UseDoor:
                    if (!HSLiftNet.IsAuthority) return;
                    HSLiftNet.ReplyTip(Sender, HSLiftNet.NotReady(HSLiftController.RequestFromLockedDoor(pos)));
                    break;
                case HSLiftNet.UseFloor:
                    if (!HSLiftNet.IsAuthority) return;
                    if (!string.IsNullOrEmpty(liftId))
                    {
                        var lift = HSLiftConfiguration.ById(liftId);
                        if (lift != null) HSLiftConfiguration.Operate(lift);
                    }
                    if (floorIndex >= 0)
                        HSLiftNet.ReplyTip(Sender, HSLiftNet.NotReady(HSLiftController.RequestInsideFloor(floorIndex, "remote")));
                    else
                        HSLiftNet.ReplyTip(Sender, HSLiftNet.NotReady(HSLiftController.RequestMoveToY(targetY, "remote")));
                    break;
                case HSLiftNet.Doors:
                    if (!HSLiftNet.IsAuthority) return;
                    if (!string.IsNullOrEmpty(liftId))
                    {
                        var doorLift = HSLiftConfiguration.ById(liftId);
                        if (doorLift != null) HSLiftConfiguration.Operate(doorLift);
                    }
                    if (flag) HSLiftDoors.OpenAtCar(world);
                    else HSLiftDoors.CloseAtCar(world);
                    break;
                case HSLiftNet.MoveStart:
                    if (HSLiftNet.IsAuthority) return;
                    HSLiftController.BeginRemoteMove(liftId, pos.y, targetY, curY, cells, text);
                    break;
                case HSLiftNet.MoveEnd:
                    if (HSLiftNet.IsAuthority) return;
                    HSLiftController.FinishRemoteMove(liftId, targetY);
                    break;
                case HSLiftNet.Tip:
                    HSLiftNet.TellLocal(text);
                    break;
            }
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Net package failed (" + kind + ")", e);
        }
    }

}

[HarmonyPatch(typeof(NetPackageManager), "SetupBaseMapping")]
public static class HSLiftNetRegister
{
    static void Postfix()
    {
        HSLiftNet.RegisterPackage();
    }
}
