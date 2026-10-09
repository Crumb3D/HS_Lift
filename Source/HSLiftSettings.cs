using System;
using System.IO;
using Newtonsoft.Json;

public class HSLiftSettingsData
{
    public bool Music = true;
    public bool FlickerLights = true;
}

public static class HSLiftSettings
{
    public static bool Music = true;
    public static bool FlickerLights = true;

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
            HSLiftDebug.Info("Client: music / lights come from the server, local file ignored");
            return;
        }
        Music = true;
        FlickerLights = true;
        TryRead(UserFile);
        TryRead(SaveFile);
        Write(UserFile);
        Write(SaveFile);
        HSLiftDebug.Info("Music=" + Music + " FlickerLights=" + FlickerLights + " (host)");
    }

    public static void ApplyFromServer(bool music, bool flickerLights)
    {
        Music = music;
        FlickerLights = flickerLights;
        HSLiftDebug.Info("Music=" + Music + " FlickerLights=" + FlickerLights + " (from server)");
    }

    public static void TakeFromFile(HSLiftFile file)
    {
        if (file == null) return;
        if (file.Music.HasValue) Music = file.Music.Value;
        if (file.FlickerLights.HasValue) FlickerLights = file.FlickerLights.Value;
    }

    public static string SetMusic(bool on)
    {
        if (HSLiftNet.IsRemoteClient)
            return "Cabin music is host-only. Run this on the server, or in single player.";
        Music = on;
        return Persist("Cabin music is " + (on ? "ON" : "OFF") + ". Host value is used on every client. Empty assets/music is silent.");
    }

    public static string SetFlickerLights(bool on)
    {
        if (HSLiftNet.IsRemoteClient)
            return "Light flicker is host-only. Run this on the server, or in single player.";
        FlickerLights = on;
        return Persist("Cabin light flicker is " + (on ? "ON" : "OFF") + ". Host value is used on every client.");
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
            Music = data.Music;
            FlickerLights = data.FlickerLights;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Could not read " + path + ": " + e.Message);
        }
    }

    static void Write(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonConvert.SerializeObject(new HSLiftSettingsData
        {
            Music = Music,
            FlickerLights = FlickerLights
        }, Formatting.Indented));
    }
}
