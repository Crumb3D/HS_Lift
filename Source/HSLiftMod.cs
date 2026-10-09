using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

public class HSLiftMod : IModApi
{
    public static string ModPath;
    public static string UserDataPath;

    public void InitMod(Mod _modInstance)
    {
        if (!HSGameVersion.AllowLoad("[HSLift]"))
            return;
        ModPath = _modInstance.Path;
        try
        {
            UserDataPath = Path.Combine(GameIO.GetUserGameDataDir(), "HSLift");
            Directory.CreateDirectory(UserDataPath);
            HSLiftConfiguration.EvacuateRuntimeFilesFromModFolder();
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Could not move lift save out of the mod folder", e);
        }
        HSLiftDebug.Info("Init v1.0.27 - a sign, call panel, and door belong to the lift they sit on; admin: hslift");
        HSLiftNet.RegisterPackage();
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
        ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(HSLiftNet.OnPlayerSpawned);
        try
        {
            new Harmony("HSLift").PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Harmony patch failed (lift doors will not lock)", e);
        }
    }

    static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        try
        {
            HSLiftSettings.Load();
            HSLiftConfiguration.Load();
            HSLiftController.EnsureCreated();
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Start failed", e);
        }
    }

    static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        try
        {
            HSLiftController.OnWorldShuttingDown();
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Shutdown handling failed", e);
        }
    }
}
