using System;

// 3.2 API: Constants.cVersionInformation (Major 3, Minor 20 = 3.2; Minor 30 = 3.3).
public static class HSGameVersion
{
    public static readonly string[] Supported = { "3.0", "3.1", "3.2", "3.3" };

    public static string Current { get; private set; }

    public static bool Is33
    {
        get { return Current == "3.3"; }
    }

    public static bool AllowLoad(string logPrefix)
    {
        string game;
        string detail;
        try
        {
            var v = Constants.cVersionInformation;
            game = ReleaseKey(v);
            Current = game;
            detail = v != null ? v.LongString : "unknown";
        }
        catch (Exception e)
        {
            Current = "";
            Log.Error(logPrefix + " REFUSING TO LOAD: could not read 7DTD version. " + e.Message);
            return false;
        }
        if (string.IsNullOrEmpty(game) || Array.IndexOf(Supported, game) < 0)
        {
            Log.Error(logPrefix + " REFUSING TO LOAD: this mod supports 7DTD "
                + string.Join(", ", Supported) + ". Game is "
                + (string.IsNullOrEmpty(game) ? "unknown" : game)
                + " (" + detail + "). Remove this folder from Mods or use a supported game version.");
            return false;
        }
        Log.Out(logPrefix + " 7DTD " + game + " (" + detail + ") is on the supported list: "
            + string.Join(", ", Supported) + "."
            + (Is33 ? " Using 3.3 CreateMesh/emitted net packages." : " Using 3.2 CloneModel/GetLength."));
        return true;
    }

    public static string ReleaseKey(VersionInformation v)
    {
        if (v == null || !v.IsValid) return "";
        if (v.ReleaseType == VersionInformation.EGameReleaseType.V && v.Major >= 3)
            return v.Major + "." + (v.Minor / 10);
        return v.Major + "." + v.Minor;
    }
}
