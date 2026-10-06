using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Optional custom clips from <mod>/Sounds: move.ogg|wav (looped on the moving car) and ding.ogg|wav (arrival).
// Cabin music: any .mp3 in <mod>/assets/music (random per trip). Empty folder = silent.
public static class HSLiftSounds
{
    public static AudioClip Move;
    public static AudioClip Ding;

    static readonly Dictionary<string, AudioClip> music = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    static int lastMusicIndex = -1;

    static HSLiftConfigData D { get { return HSLiftConfiguration.Data; } }
    static string Dir { get { return Path.Combine(HSLiftMod.ModPath ?? ".", "Sounds"); } }
    static string MusicDir { get { return Path.Combine(HSLiftMod.ModPath ?? ".", "assets", "music"); } }

    public static IEnumerator LoadAll()
    {
        yield return Load("move", c => Move = c);
        yield return Load("ding", c => Ding = c);
    }

    public static IEnumerator PlayCabinMusic(GameObject carRoot, string trackName, Action<AudioSource> set)
    {
        if (carRoot == null || set == null) yield break;
        if (!HSLiftNet.IsRemoteClient) HSLiftSettings.Load();
        if (!HSLiftSettings.Music) yield break;
        string path = ResolveMp3(trackName);
        if (path == null) yield break;
        AudioClip clip;
        if (!music.TryGetValue(path, out clip) || clip == null)
        {
            yield return LoadMp3(path, c => clip = c);
            if (clip == null) yield break;
            music[path] = clip;
        }
        var src = MakeMusicSource(carRoot, clip);
        src.Play();
        set(src);
    }

    public static string ChooseTrack()
    {
        var path = PickMp3();
        return path == null ? null : Path.GetFileName(path);
    }

    static string ResolveMp3(string trackName)
    {
        if (!string.IsNullOrEmpty(trackName))
        {
            var named = Path.Combine(MusicDir, Path.GetFileName(trackName));
            if (File.Exists(named)) return named;
        }
        return PickMp3();
    }

    static string PickMp3()
    {
        try
        {
            if (!Directory.Exists(MusicDir)) return null;
            var files = Directory.GetFiles(MusicDir, "*.mp3");
            if (files == null || files.Length == 0) return null;
            int i = UnityEngine.Random.Range(0, files.Length);
            if (files.Length > 1 && i == lastMusicIndex)
                i = (i + 1) % files.Length;
            lastMusicIndex = i;
            return files[i];
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Cabin music folder: " + e.Message);
            return null;
        }
    }

    static IEnumerator LoadMp3(string path, Action<AudioClip> set)
    {
        var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path), AudioType.MPEG);
        var dh = req.downloadHandler as DownloadHandlerAudioClip;
        if (dh != null)
        {
            dh.streamAudio = false;
            dh.compressed = false;
        }
        yield return req.SendWebRequest();
        try
        {
            if (req.result != UnityWebRequest.Result.Success)
            {
                HSLiftDebug.Warn("Could not load music " + path + ": " + req.error);
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                HSLiftDebug.Warn("Music " + path + " gave no clip");
                yield break;
            }
            clip.name = "HSLiftMusic_" + Path.GetFileNameWithoutExtension(path);
            set(clip);
            HSLiftDebug.Info("Cabin music " + Path.GetFileName(path) + " (" + clip.length.ToString("0.0") + "s)");
        }
        finally
        {
            req.Dispose();
        }
    }

    static IEnumerator Load(string baseName, Action<AudioClip> set)
    {
        string path = null;
        var type = AudioType.UNKNOWN;
        if (File.Exists(Path.Combine(Dir, baseName + ".ogg"))) { path = Path.Combine(Dir, baseName + ".ogg"); type = AudioType.OGGVORBIS; }
        else if (File.Exists(Path.Combine(Dir, baseName + ".wav"))) { path = Path.Combine(Dir, baseName + ".wav"); type = AudioType.WAV; }
        if (path == null) yield break;

        var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path), type);
        yield return req.SendWebRequest();
        try
        {
            if (req.result != UnityWebRequest.Result.Success)
            {
                HSLiftDebug.Warn("Could not load sound " + path + ": " + req.error);
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null) { HSLiftDebug.Warn("Sound " + path + " gave no clip"); yield break; }
            clip.name = "HSLift_" + baseName;
            set(clip);
            HSLiftDebug.Info("Loaded custom sound " + Path.GetFileName(path) + " (" + clip.length.ToString("0.0") + "s)");
        }
        finally
        {
            req.Dispose();
        }
    }

    static AudioSource MakeMusicSource(GameObject go, AudioClip clip)
    {
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.dopplerLevel = 0f;
        src.volume = 0.3f;
        return src;
    }

    static AudioSource MakeSource(GameObject go, AudioClip clip, bool loop)
    {
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = loop;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 30f;
        src.volume = Mathf.Clamp01(D.CustomSoundVolume);
        return src;
    }

    public static AudioSource StartMoveLoop(GameObject carRoot)
    {
        if (Move == null || carRoot == null) return null;
        var src = MakeSource(carRoot, Move, true);
        src.Play();
        return src;
    }

    public static bool PlayDingAt(Vector3 worldPos)
    {
        if (Ding == null) return false;
        var go = new GameObject("HSLiftDing");
        go.transform.position = worldPos - Origin.position;
        MakeSource(go, Ding, false).Play();
        UnityEngine.Object.Destroy(go, Ding.length + 0.2f);
        return true;
    }
}
