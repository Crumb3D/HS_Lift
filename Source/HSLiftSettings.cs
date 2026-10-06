using System;
using System.IO;
using Newtonsoft.Json;

public class HSLiftSettingsData
{
    // When false, the moving car keeps the blocks you painted. Paintbrush still has Lift Floor / Wall / Ceiling.
    public bool AutoPaintInterior = true;
    // When false, no cabin music even if MP3s are in assets/music.
    public bool Music = true;
}

public static class HSLiftSettings
{
    public static bool AutoPaintInterior = true;
    public static bool Music = true;

    static string FileName { get { return "HSLiftSettings.json"; } }

    static string UserFile
    {
        get
        {
            var dir = HSLiftMod.UserDataPath;
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, FileName);
        }
    }

    static string SaveFile
    {
        get
        {
            try
            {
                var save = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(save)) return Path.Combine(save, FileName);
            }
            catch { }
            return null;
        }
    }

    public static void Load()
    {
        if (HSLiftNet.IsRemoteClient)
        {
            HSLiftDebug.Info("Client: auto-paint / music come from the server, local file ignored");
            return;
        }
        AutoPaintInterior = true;
        Music = true;
        TryRead(UserFile);
        TryRead(SaveFile);
        WriteHostIfMissing();
        HSLiftDebug.Info("AutoPaintInterior=" + AutoPaintInterior + " Music=" + Music + " (host)");
    }

    public static void ApplyFromServer(bool autoPaint, bool music)
    {
        AutoPaintInterior = autoPaint;
        Music = music;
        HSLiftDebug.Info("AutoPaintInterior=" + AutoPaintInterior + " Music=" + Music + " (from server)");
    }

    public static void TakeFromFile(HSLiftFile file)
    {
        if (file == null) return;
        if (file.AutoPaintInterior.HasValue) AutoPaintInterior = file.AutoPaintInterior.Value;
        if (file.Music.HasValue) Music = file.Music.Value;
    }

    public static string SetAutoPaint(bool on)
    {
        if (HSLiftNet.IsRemoteClient)
            return "Auto-paint is host-only. Run this on the server, or in single player.";
        AutoPaintInterior = on;
        return Persist("Auto-paint interior is " + (on ? "ON" : "OFF") + ". Host value is used on every client. Paintbrush still works.");
    }

    public static string SetMusic(bool on)
    {
        if (HSLiftNet.IsRemoteClient)
            return "Cabin music is host-only. Run this on the server, or in single player.";
        Music = on;
        return Persist("Cabin music is " + (on ? "ON" : "OFF") + ". Host value is used on every client. Empty assets/music is always silent.");
    }

    public static bool TryParseOnOff(string arg, out bool on)
    {
        arg = (arg ?? "").Trim().ToLowerInvariant();
        if (arg == "on" || arg == "true" || arg == "1" || arg == "yes") { on = true; return true; }
        if (arg == "off" || arg == "false" || arg == "0" || arg == "no") { on = false; return true; }
        on = false;
        return false;
    }

    static string Persist(string ok)
    {
        try
        {
            Write(UserFile);
            Write(SaveFile);
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Could not save lift settings", e);
            return ok + " File did not save: " + e.Message;
        }
        HSLiftConfiguration.Save();
        return ok;
    }

    static void TryRead(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var data = JsonConvert.DeserializeObject<HSLiftSettingsData>(File.ReadAllText(path));
            if (data == null) return;
            AutoPaintInterior = data.AutoPaintInterior;
            Music = data.Music;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Could not read " + path + ": " + e.Message);
        }
    }

    static void WriteHostIfMissing()
    {
        if (!string.IsNullOrEmpty(UserFile) && !File.Exists(UserFile)) Write(UserFile);
        if (!string.IsNullOrEmpty(SaveFile) && !File.Exists(SaveFile)) Write(SaveFile);
    }

    static void Write(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonConvert.SerializeObject(new HSLiftSettingsData
        {
            AutoPaintInterior = AutoPaintInterior,
            Music = Music
        }, Formatting.Indented));
    }
}
