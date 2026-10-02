using System;

public static class HSLiftDebug
{
    const string Prefix = "[HSLift] ";

    public static bool Enabled;

    public static void Info(string msg)
    {
        Log.Out(Prefix + msg);
    }

    public static void Verbose(string msg)
    {
        if (Enabled) Log.Out(Prefix + "[debug] " + msg);
    }

    public static void Warn(string msg)
    {
        Log.Warning(Prefix + msg);
    }

    public static void Error(string msg, Exception e = null)
    {
        Log.Error(Prefix + msg + (e != null ? " :: " + e : ""));
    }
}
    