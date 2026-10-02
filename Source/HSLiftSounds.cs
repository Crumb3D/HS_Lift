using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Optional custom clips from <mod>/Sounds: move.ogg|wav (looped on the moving car) and ding.ogg|wav (arrival).
// Missing files fall back to the vanilla sound names in HSLift.json.
public static class HSLiftSounds
{
    public static AudioClip Move;
    public static AudioClip Ding;

    static HSLiftConfigData D { get { return HSLiftConfiguration.Data; } }
    static string Dir { get { return Path.Combine(HSLiftMod.ModPath ?? ".", "Sounds"); } }

    public static IEnumerator LoadAll()
    {
        yield return Load("move", c => Move = c);
        yield return Load("ding", c => Ding = c);
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
