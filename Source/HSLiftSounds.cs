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

    public static IEnumerator PlayCabinMusic(GameObject carRoot, string trackName, HSLiftConfigData lift, Action<AudioSource> set)
    {
        if (carRoot == null || set == null) yield break;
        if (!HSLiftNet.IsRemoteClient) HSLiftSettings.Load();
        if (!HSLiftSettings.Music) { set(null); yield break; }
        string path = ResolveMp3(trackName);
        if (path == null) { set(null); yield break; }
        AudioClip clip;
        if (!music.TryGetValue(path, out clip) || clip == null)
        {
            yield return LoadMp3(path, c => clip = c);
            if (clip == null) { set(null); yield break; }
            music[path] = clip;
        }
        var src = MakeMusicSource(carRoot, clip, lift);
        src.Play();
        set(src);
    }

    static GameObject parkedHold;
    static AudioSource parkedSource;
    static string parkedFor;
    static bool parkedLoading;
    static float parkedRetryAt;

    // The speaker stays in the parked car. Stepping out does not stop it; distance and the doors do.
    public static void TickParked(MonoBehaviour host)
    {
        if (host == null || GameManager.IsDedicatedServer) return;
        if (!HSLiftSettings.Music) { StopParked(); return; }
        if (parkedHold == null && !parkedLoading && Time.time < parkedRetryAt) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var player = world != null ? world.GetPrimaryPlayer() : null;
        var lift = player != null ? NearParked(player) : null;
        if (lift == null) { StopParked(); return; }
        if (parkedFor == lift.ElevatorId && parkedHold != null)
        {
            parkedHold.transform.position = HSLiftCar.UnityPos(lift, lift.CurrentY);
            return;
        }
        if (parkedLoading && parkedFor == lift.ElevatorId) return;
        StopParked();
        parkedFor = lift.ElevatorId;
        parkedLoading = true;
        var hold = new GameObject("HSLiftParkedMusic");
        hold.transform.position = HSLiftCar.UnityPos(lift, lift.CurrentY);
        parkedHold = hold;
        host.StartCoroutine(PlayCabinMusic(hold, null, lift, src =>
        {
            parkedLoading = false;
            if (src == null)
            {
                if (parkedHold == hold)
                {
                    UnityEngine.Object.Destroy(hold);
                    parkedHold = null;
                }
                parkedRetryAt = Time.time + 5f;
                return;
            }
            if (parkedHold != hold)
            {
                UnityEngine.Object.Destroy(src.gameObject);
                return;
            }
            parkedRetryAt = 0f;
            parkedSource = src;
        }));
    }

    public static void StopParked()
    {
        parkedLoading = false;
        parkedFor = null;
        parkedRetryAt = 0f;
        parkedSource = null;
        if (parkedHold != null) UnityEngine.Object.Destroy(parkedHold);
        parkedHold = null;
    }

    static HSLiftConfigData NearParked(EntityPlayer player)
    {
        var p = player.position;
        HSLiftConfigData best = null;
        float bestD = 16f * 16f;
        foreach (var o in HSLiftConfiguration.Lifts)
        {
            if (o == null || !o.HasCar) continue;
            var ctrl = HSLiftController.Of(o);
            if (ctrl != null && ctrl.IsTraveling) continue;
            float dx = p.x - (o.MinX + o.SizeX * 0.5f);
            float dy = p.y - (o.CurrentY + Mathf.Max(1, o.SizeY) * 0.45f);
            float dz = p.z - (o.MinZ + o.SizeZ * 0.5f);
            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 < bestD) { bestD = d2; best = o; }
        }
        return best;
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

    static AudioSource MakeMusicSource(GameObject carRoot, AudioClip clip, HSLiftConfigData lift)
    {
        if (lift == null) lift = D;
        int sx = lift != null ? Mathf.Max(1, lift.SizeX) : 1;
        int sy = lift != null ? Mathf.Max(1, lift.SizeY) : 1;
        int sz = lift != null ? Mathf.Max(1, lift.SizeZ) : 1;
        var hold = new GameObject("HSLiftCabinMusic");
        hold.transform.SetParent(carRoot.transform, false);
        float y = sy <= 1 ? 0.55f : Mathf.Clamp(sy * 0.45f, 0.7f, sy - 0.3f);
        hold.transform.localPosition = new Vector3(sx * 0.5f, y, sz * 0.5f);
        var src = hold.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        float reach = 0.5f * Mathf.Sqrt(sx * sx + sz * sz);
        src.spatialBlend = 1f;
        src.spatialize = false;
        src.dopplerLevel = 0f;
        src.spread = 40f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = reach + 0.5f;
        src.maxDistance = reach + 12f;
        src.priority = 80;
        src.volume = 0.03f;
        var lp = hold.AddComponent<AudioLowPassFilter>();
        lp.cutoffFrequency = 700f;
        var ride = hold.AddComponent<HSLiftCabinMusicRide>();
        ride.Src = src;
        ride.Lp = lp;
        ride.Car = carRoot.transform;
        ride.Lift = lift;
        ride.CabinSize = new Vector3(sx, sy, sz);
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
        src.volume = Mathf.Clamp01(D.CustomSoundVolume) / 3f;
        return src;
    }

    // The vanilla move loop is played in the player's head at full volume. Keep a third of it.
    public static void PlayQuietHeadLoop(string name, EntityPlayer player)
    {
        if (player == null || string.IsNullOrEmpty(name)) return;
        var before = new HashSet<AudioSource>();
        var existing = player.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < existing.Length; i++)
            if (existing[i] != null) before.Add(existing[i]);
        Audio.Manager.PlayInsidePlayerHead(name, player.entityId, 0f, true, true);
        var after = player.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < after.Length; i++)
        {
            var src = after[i];
            if (src == null || before.Contains(src)) continue;
            src.volume /= 3f;
        }
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

// Speaker stays in the car and loops. Inside it is quiet background. Open doors let a muffled version out, which fades up as you come in.
public class HSLiftCabinMusicRide : MonoBehaviour
{
    public const float InsideVol = 0.11f;

    public AudioSource Src;
    public AudioLowPassFilter Lp;
    public Transform Car;
    public HSLiftConfigData Lift;
    public Vector3 CabinSize;
    float nextDoor;
    bool doorsOpen;

    void LateUpdate()
    {
        if (Src == null || Car == null) return;
        Src.spatialBlend = 1f;
        if (Time.time >= nextDoor)
        {
            nextDoor = Time.time + 0.3f;
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            doorsOpen = HSLiftDoors.AnyOpenFor(world, Lift);
        }
        float outside = OutsideMeters();
        bool inside = outside < 0.2f;
        float open = doorsOpen ? 1f : 0f;
        float reach = Mathf.Lerp(4f, 11f, open);
        float proximity = inside ? 1f : Mathf.Clamp01(1f - outside / reach);
        float loud = inside ? 1f : proximity * Mathf.Lerp(0.18f, 0.55f, open);
        float targetVol = InsideVol * loud;
        float targetHz = inside ? 4200f : Mathf.Lerp(480f, 1900f, proximity * Mathf.Lerp(0.35f, 1f, open));
        Src.volume = Mathf.MoveTowards(Src.volume, targetVol, Time.deltaTime * 0.08f);
        if (Lp != null) Lp.cutoffFrequency = Mathf.MoveTowards(Lp.cutoffFrequency, targetHz, Time.deltaTime * 2200f);
    }

    float OutsideMeters()
    {
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var p = world != null ? world.GetPrimaryPlayer() : null;
            if (p == null) return 30f;
            var lp = Car.InverseTransformPoint(p.position - Origin.position);
            float dx = lp.x < 0f ? -lp.x : (lp.x > CabinSize.x ? lp.x - CabinSize.x : 0f);
            float dy = lp.y < -0.2f ? -0.2f - lp.y : (lp.y > CabinSize.y ? lp.y - CabinSize.y : 0f);
            float dz = lp.z < 0f ? -lp.z : (lp.z > CabinSize.z ? lp.z - CabinSize.z : 0f);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        catch { return 30f; }
    }
}
