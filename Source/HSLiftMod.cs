using System;
using System.Reflection;
using HarmonyLib;

public class HSLiftMod : IModApi
{
    public static string ModPath;

    public void InitMod(Mod _modInstance)
    {
        ModPath = _modInstance.Path;
        HSLiftDebug.Info("Init v1.0.0 - setup tool hold E; admin console: hslift");
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
        ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
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
