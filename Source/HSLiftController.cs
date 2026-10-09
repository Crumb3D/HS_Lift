using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum HSLiftState { Idle, Closing, MovingUp, MovingDown, Stopping, Error }

public class HSLiftController : MonoBehaviour
{
    public HSLiftConfigData Bound;
    public HSLiftState LiftState = HSLiftState.Idle;
    public bool IsThisMoving { get { return LiftState != HSLiftState.Idle; } }
    public bool IsTraveling
    {
        get
        {
            return LiftState == HSLiftState.MovingUp || LiftState == HSLiftState.MovingDown || LiftState == HSLiftState.Stopping;
        }
    }

    static readonly List<HSLiftController> all = new List<HSLiftController>();
    static bool soundsStarted;

    public static HSLiftState State { get { var c = Active(); return c != null ? c.LiftState : HSLiftState.Idle; } }
    public static bool IsMoving { get { var c = Active(); return c != null && c.IsThisMoving; } }

    static HSLiftConfigData D { get { return HSLiftConfiguration.Data; } }

    static HSLiftController instance { get { return Of(HSLiftConfiguration.Data); } }

    static HSLiftController Active()
    {
        return Of(HSLiftConfiguration.Data);
    }

    public static bool Bind(HSLiftConfigData d)
    {
        if (d == null) return false;
        HSLiftConfiguration.Operate(d);
        Ensure(d);
        return true;
    }

    public static HSLiftController Of(HSLiftConfigData d)
    {
        if (d == null) return null;
        return all.Find(c => c != null && c.Bound != null && c.Bound.ElevatorId == d.ElevatorId);
    }

    void Push()
    {
        if (Bound != null) HSLiftConfiguration.Operate(Bound);
    }

    public static void Forget(HSLiftConfigData d)
    {
        var c = Of(d);
        if (c == null) return;
        all.Remove(c);
        c.StopMoveSound();
        c.StopPreviewInternal();
        c.FinishRelease();
        c.move.Destroy();
        Destroy(c.gameObject);
    }

    public static void EnsureCreated()
    {
        foreach (var d in HSLiftConfiguration.Lifts) Ensure(d);
    }

    public static HSLiftController Ensure(HSLiftConfigData d)
    {
        if (d == null) return null;
        var c = Of(d);
        if (c != null)
        {
            c.Bound = d;
            return c;
        }
        var go = new GameObject("HSLift_" + d.ElevatorId);
        DontDestroyOnLoad(go);
        c = go.AddComponent<HSLiftController>();
        c.Bound = d;
        all.Add(c);
        if (!soundsStarted)
        {
            c.StartCoroutine(HSLiftSounds.LoadAll());
            soundsStarted = true;
        }
        c.LiftState = HSLiftState.Idle;
        c.recovering = HSLiftCar.HasJournalFor(d.ElevatorId);
        if (c.recovering) HSLiftDebug.Info("Journal found for " + d.ElevatorId + "; restoring the car once its chunks load.");
        return c;
    }

    const float PowerCheckInterval = 0.25f;
    const float ReleaseDelay = 0.75f;
    const float RetryInterval = 2f;
    const float PreviewSeconds = 30f;

    readonly HSLiftMovement move = new HSLiftMovement();
    List<HSLiftCell> cells;
    int fromY;
    float nextPowerCheck;
    float nextRetry;
    string errorReason;
    bool recovering;
    bool signsPrimed;
    float autoCloseAt;
    float nextDoorBlockedWarn;
    bool watchDoorClose;
    const float AutoCloseSeconds = 20f;

    GameObject releasing;
    float releaseAt;
    public bool HoldingClone { get { return releasing != null; } }

    GameObject preview;
    float previewUntil;

    float nextShaftRoof;

    public void ScheduleAutoClose()
    {
        autoCloseAt = Time.unscaledTime + AutoCloseSeconds;
    }

    public void CancelAutoClose()
    {
        autoCloseAt = 0f;
    }

    public void WatchDoorClose()
    {
        watchDoorClose = true;
    }

    public void ClearDoorWatch()
    {
        watchDoorClose = false;
    }

    // Reopen immediately if something enters while the doors are closing.
    void TickReopenIfBlocked()
    {
        if (!watchDoorClose) return;
        if (LiftState != HSLiftState.Idle && LiftState != HSLiftState.Closing)
        {
            watchDoorClose = false;
            return;
        }
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        string why;
        if (HSLiftDoors.PersonInDoorway(world, out why) || (LiftState != HSLiftState.Closing && HSLiftDoors.DoorwayBlocked(world, out why)))
        {
            bool idle = LiftState == HSLiftState.Idle;
            HSLiftDoors.OpenAtCar(world, idle);
            if (LiftState == HSLiftState.Closing) doorsToldToClose = false;
            WarnDoorBlocked(world, why);
            HSLiftDebug.Info("Reopened doors (obstruction): " + why);
            return;
        }
        if (!HSLiftDoors.AnyOpenAtCar(world) && !HSLiftDoors.PersonInDoorway(world, out why))
            watchDoorClose = false;
    }

    void TickAutoClose()
    {
        if (autoCloseAt <= 0f || LiftState != HSLiftState.Idle) return;
        if (Time.unscaledTime < autoCloseAt) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        if (!HSLiftDoors.AnyOpenAtCar(world))
        {
            autoCloseAt = 0f;
            return;
        }
        string why;
        if (HSLiftDoors.DoorwayBlocked(world, out why))
        {
            autoCloseAt = Time.unscaledTime + 2f;
            WarnDoorBlocked(world, why);
            return;
        }
        HSLiftDoors.CloseAtCar(world);
        HSLiftDebug.Verbose("Auto-closed doors after " + AutoCloseSeconds + "s");
    }

    void WarnDoorBlocked(World world, string why)
    {
        if (Time.unscaledTime < nextDoorBlockedWarn) return;
        nextDoorBlockedWarn = Time.unscaledTime + 8f;
        if (!string.IsNullOrEmpty(why)) HSLiftDebug.Info("Doors blocked: " + why);
    }

    void StayPutAfterFailedClose(World world)
    {
        LiftState = HSLiftState.Idle;
        doorsToldToClose = false;
        if (world != null) HSLiftDoors.OpenAtCar(world, false);
        HSLiftDebug.Info("Did not leave: doors did not shut");
        HSLiftNet.TellLocal(Localization.Get("hsliftDidNotLeave"));
    }

    // --- requests (outside panels, inside panels, console all end up in RequestMoveTo) ---

    // Console up/down: the next floor in that direction.
    public static string RequestMove(bool up, string source)
    {
        if (!D.HasUpper) return "needs at least two floors (hslift floor add <name>)";
        HSLiftFloor next = null;
        foreach (var f in D.Floors)
        {
            if (up && f.Y > D.CurrentY) { next = f; break; }
            if (!up && f.Y < D.CurrentY) next = f;
        }
        if (next == null) return "already at the " + (up ? "top" : "bottom") + " floor";
        return RequestMoveTo(next.Y, source);
    }

    public static string RequestFloor(string name, string source)
    {
        var f = HSLiftConfiguration.FloorByName(name);
        if (f == null) return "no floor named '" + name + "' (" + HSLiftConfiguration.FloorList() + ")";
        return RequestMoveTo(f.Y, source);
    }

    // Outside panels only call the car to their own floor; the destination is chosen inside the car.
    public static string RequestFromOutsidePanel(Vector3i pos)
    {
        if (HSLiftNet.IsRemoteClient)
        {
            HSLiftNet.SendUsePanel(pos);
            return null;
        }
        var lift = HSLiftConfiguration.RegisteredPanelOwner(pos);
        if (lift != null)
        {
            HSLiftDoors.CollectDoors(GameManager.Instance.World);
            var beside = HSLiftDoors.LiftBeside(pos);
            if (beside != null && beside != lift)
            {
                var besideFloor = HSLiftConfiguration.FloorForPanel(beside, pos.y);
                if (besideFloor != null)
                {
                    if (!beside.Panels.Exists(p => p.X == pos.x && p.Y == pos.y && p.Z == pos.z))
                        beside.Panels.Add(new HSLiftPanelEntry { Stop = besideFloor.Name, X = pos.x, Y = pos.y, Z = pos.z });
                    HSLiftConfiguration.ForgetPanelOnOtherLifts(beside, pos);
                    HSLiftConfiguration.Save();
                    HSLiftDebug.Info("Call panel at " + pos + " sits beside " + beside.ElevatorId + "'s door, so it calls that lift.");
                    lift = beside;
                }
            }
        }
        if (!Bind(lift))
            return "this panel is not registered (aim at it: hslift panel)";
        var entry = lift.Panels.Find(p => p.X == pos.x && p.Y == pos.y && p.Z == pos.z);
        if (entry == null) return "this panel is not registered (aim at it: hslift panel)";
        var floor = lift.Floors.Find(f => string.Equals(f.Name, entry.Stop, StringComparison.OrdinalIgnoreCase));
        if (floor == null)
            floor = HSLiftConfiguration.FloorForPanel(lift, entry.Y);
        if (floor == null) return "this panel is not at a floor (" + HSLiftConfiguration.FloorList() + ")";
        var ctrl = Of(lift);
        RelocateParkedCar();
        if (lift.CurrentY == floor.Y && (ctrl == null || !ctrl.IsThisMoving))
        {
            HSLiftDoors.OpenAtCar(GameManager.Instance.World);
            return null;
        }
        return RequestMoveTo(floor.Y, "called from floor " + floor.Name);
    }

    // Pressing a locked lift door calls the car to that door's floor, like its outside panel.
    public static string RequestFromLockedDoor(Vector3i doorPos)
    {
        if (HSLiftNet.IsRemoteClient)
        {
            HSLiftNet.SendUseDoor(doorPos);
            return null;
        }
        var doorLift = HSLiftConfiguration.LiftForDoor(doorPos);
        if (!Bind(doorLift))
            return "this door is not part of a lift";
        var floor = HSLiftConfiguration.FloorForPanel(doorLift, doorPos.y);
        if (floor == null) return "this door is not at a floor (" + HSLiftConfiguration.FloorList() + ")";
        RelocateParkedCar();
        if (D.CurrentY == floor.Y && !IsMoving)
        {
            HSLiftDoors.OpenAtCar(GameManager.Instance.World);
            return null;
        }
        return RequestMoveTo(floor.Y, "locked door on floor " + floor.Name);
    }

    // floorIndex is the hold-E menu slot. A plain press has no destination.
    public static string RequestFromInsidePanel(Vector3i pos, int floorIndex)
    {
        if (HSLiftNet.IsRemoteClient)
        {
            HSLiftNet.SendUseInside(pos, floorIndex);
            return null;
        }
        if (!Bind(HSLiftConfiguration.LiftForInsidePanel(pos)))
            return "this inside panel is not part of the lift car";
        return RequestFromInside(floorIndex, "inside panel");
    }

    public static string RequestFromDoorMenu(EntityPlayerLocal player, int floorIndex)
    {
        if (HSLiftNet.IsRemoteClient)
        {
            var clientLift = HSLiftConfiguration.LiftForPlayer(player);
            if (clientLift == null || !PlayerInCar(player)) return "stand inside the lift car to choose a floor";
            HSLiftNet.SendUseFloor(clientLift.ElevatorId, 0, floorIndex);
            return null;
        }
        if (!Bind(HSLiftConfiguration.LiftForPlayer(player))) return "stand inside the lift car to choose a floor";
        if (!PlayerInCar(player)) return "stand inside the lift car to choose a floor";
        return RequestFromInside(floorIndex, "door menu");
    }

    public static string RequestInsideFloor(int floorIndex, string source)
    {
        return RequestFromInside(floorIndex, source);
    }

    public static string RequestMoveToY(int targetY, string source)
    {
        return RequestMoveTo(targetY, source);
    }

    static string RequestFromInside(int floorIndex, string source)
    {
        if (!D.HasUpper) return "needs at least two floors (hslift floor add <name>)";
        if (floorIndex < 0 || floorIndex >= D.Floors.Count) return Localization.Get("hsliftChooseFloor");
        var f = D.Floors[floorIndex];
        return RequestMoveTo(f.Y, source + " -> floor " + f.Name);
    }

    public static bool PlayerInCar(EntityPlayerLocal player)
    {
        if (player == null || !D.HasCar) return false;
        var feet = new Vector3i(Mathf.FloorToInt(player.position.x), Mathf.FloorToInt(player.position.y + 0.1f), Mathf.FloorToInt(player.position.z));
        return HSLiftCar.InFootprint(feet.x, feet.z)
            && feet.y >= D.CurrentY && feet.y <= D.CurrentY + D.RideHeight;
    }

    // Closing a door no longer sends the car. Hold E on the inside panel (or the door if there is no panel) to pick a floor.
    public static void OnLiftDoorClosed(EntityPlayerLocal player)
    {
    }

    static string RequestMoveTo(int targetY, string source)
    {
        try
        {
            Ensure(D);
            if (instance == null) return "HSLift not started";
            if (HSLiftNet.IsRemoteClient)
            {
                HSLiftNet.SendUseFloor(D.ElevatorId, targetY, -1);
                return null;
            }
            if (instance.IsTraveling)
            {
                HSLiftDebug.Info("Ignored " + source + " request: lift is " + instance.LiftState);
                return "lift is moving";
            }
            if (instance.LiftState == HSLiftState.Closing)
            {
                instance.pendingTarget = targetY;
                instance.pendingSource = source;
                HSLiftDebug.Info("Already closing; new floor Y" + targetY + " (" + source + ")");
                return null;
            }
            if (instance.recovering) return "restoring the car from an interrupted move";
            if (!D.HasCar) return "car not set (hslift corner1 / corner2)";

            var world = GameManager.Instance.World;
            List<HSLiftCell> captured;
            int fromY;
            var problem = CheckCanMove(world, targetY, out captured, out fromY);
            if (problem != null) return Refuse(source, problem);
            if (fromY == targetY)
            {
                HSLiftDoors.OpenAtCar(world);
                return "already at " + HSLiftConfiguration.FloorLabel(targetY);
            }

            instance.pendingTarget = targetY;
            instance.pendingSource = source;
            instance.doorsToldToClose = false;
            instance.closingSince = Time.time;
            instance.LiftState = HSLiftState.Closing;
            HSLiftDebug.Info("Closing doors, then Y" + D.CurrentY + " -> Y" + targetY + " (" + source + ")");
            return null;
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Move request failed", e);
            return "error (see log)";
        }
    }

    static void RelocateParkedCar()
    {
        var world = GameManager.Instance.World;
        if (world == null || !D.HasCar) return;
        List<HSLiftCell> cells;
        int fromY;
        HSLiftCar.CaptureParked(world, out cells, out fromY);
    }

    static string CheckCanMove(World world, int targetY, out List<HSLiftCell> captured)
    {
        int fromY;
        return CheckCanMove(world, targetY, out captured, out fromY);
    }

    static string CheckCanMove(World world, int targetY, out List<HSLiftCell> captured, out int fromY)
    {
        captured = null;
        fromY = D.CurrentY;
        string problem;
        if (!HSLiftPower.HasWorkingPower(out problem)) return problem;
        problem = HSLiftCar.CaptureParked(world, out captured, out fromY);
        if (problem != null) return problem;
        if (fromY == targetY) return null;
        HSLiftCar.RemoveLeftoverCopies(world, fromY, targetY, captured);
        return HSLiftCar.CheckShaftSupport(world, fromY)
            ?? HSLiftCar.CheckShaftSupport(world, targetY)
            ?? HSLiftCar.CheckPath(world, fromY, targetY, captured)
            ?? HSLiftCar.CheckDestination(world, fromY, targetY, captured);
    }

    static float DoorCloseDelay { get { return HSLiftConfiguration.IsVehicle ? 2.75f : 1.5f; } }
    const float ClosingGiveUp = 6f;
    int pendingTarget;
    string pendingSource;
    float departAt;
    float closingSince;
    bool doorsToldToClose;

    void FinishClosing()
    {
        Push();
        var world = GameManager.Instance.World;
        string why;
        if (HSLiftDoors.PersonInDoorway(world, out why))
        {
            HSLiftDoors.OpenAtCar(world, false);
            doorsToldToClose = false;
            WarnDoorBlocked(world, why);
            return;
        }
        if (HSLiftDoors.AnyOpenAtCar(world))
        {
            doorsToldToClose = false;
            return;
        }
        List<HSLiftCell> captured;
        int fromY;
        var problem = CheckCanMove(world, pendingTarget, out captured, out fromY);
        if (problem != null)
        {
            LiftState = HSLiftState.Idle;
            Refuse(pendingSource, problem);
            HSLiftDoors.OpenAtCar(world);
            HSLiftNet.TellLocal(string.Format(Localization.Get("hsliftNotReady"), problem));
            return;
        }
        if (fromY == pendingTarget)
        {
            LiftState = HSLiftState.Idle;
            HSLiftDoors.OpenAtCar(world);
            return;
        }
        Depart(world, captured, fromY, pendingTarget, pendingSource);
    }

    static string Refuse(string source, string problem)
    {
        HSLiftDebug.Info("Move refused (" + source + "): " + problem);
        return problem;
    }

    void Depart(World world, List<HSLiftCell> captured, int parkedY, int targetY, string source)
    {
        Push();
        FinishRelease();
        StopPreviewInternal();
        fromY = parkedY;
        if (HSLiftDoors.CloseAllShaftDoors(world, false) > 0)
        {
            // Car doors changed state; capture again so they travel closed.
            List<HSLiftCell> recaptured;
            if (HSLiftCar.Capture(world, fromY, out recaptured) == null) captured = recaptured;
        }
        cells = captured;
        HSLiftCar.WriteJournal(fromY, cells);
        var root = HSLiftCar.BuildVisual(world, cells, true, fromY);
        move.Begin(root, fromY, targetY);
        HSLiftCar.RemoveFromWorld(world, fromY, cells);
        LiftState = targetY > fromY ? HSLiftState.MovingUp : HSLiftState.MovingDown;
        cabinTrack = HSLiftSounds.ChooseTrack();
        StartMoveSound(root);
        nextPowerCheck = Time.time + PowerCheckInterval;
        HSLiftNet.BroadcastMoveStart(D.ElevatorId, fromY, targetY, fromY, cells, cabinTrack);
        HSLiftDebug.Info(string.Format("{0}: Y{1} -> Y{2} ({3} blocks, {4})", State, fromY, targetY, cells.Count, source));
    }

    // --- update loop ---

    void FixedUpdate()
    {
        try
        {
            Push();
            if (!HSLiftNet.IsAuthority)
            {
                if (LiftState == HSLiftState.Idle || LiftState == HSLiftState.Error || LiftState == HSLiftState.Closing)
                {
                    move.Apply();
                    return;
                }
                float beforeY = move.CurY;
                bool done = move.Step(Time.fixedDeltaTime, D.SpeedBlocksPerSecond);
                CarryRiders(beforeY, move.CurY - beforeY);
                if (done) move.Apply();
                return;
            }
            if (LiftState == HSLiftState.Closing)
            {
                var world = GameManager.Instance.World;
                string why;
                if (Time.time >= closingSince + ClosingGiveUp)
                {
                    if (!HSLiftDoors.PersonInDoorway(world, out why) && !HSLiftDoors.AnyOpenAtCar(world))
                        FinishClosing();
                    else
                        StayPutAfterFailedClose(world);
                    return;
                }
                if (!doorsToldToClose)
                {
                    bool anyOpen = HSLiftDoors.AnyOpenAtCar(world);
                    HSLiftDoors.CloseAllShaftDoors(world, false);
                    doorsToldToClose = true;
                    departAt = anyOpen ? Time.time + DoorCloseDelay : Time.time;
                    HSLiftDebug.Info((anyOpen ? "Closing doors" : "Doors already shut") + " before leaving for " + HSLiftConfiguration.FloorLabel(pendingTarget));
                    return;
                }
                if (HSLiftDoors.PersonInDoorway(world, out why))
                {
                    HSLiftDoors.OpenAtCar(world, false);
                    doorsToldToClose = false;
                    WarnDoorBlocked(world, why);
                    return;
                }
                if (Time.time >= departAt) FinishClosing();
                return;
            }
            if (LiftState == HSLiftState.Idle || LiftState == HSLiftState.Error)
            {
                move.Apply();
                return;
            }
            float before = move.CurY;
            bool arrived = move.Step(Time.fixedDeltaTime, D.SpeedBlocksPerSecond);
            CarryRiders(before, move.CurY - before);
            HSLiftFloorSigns.UpdatePassing(GameManager.Instance.World, move.CurY, fromY, move.TargetY);
            if (arrived)
            {
                Arrive(move.TargetY);
                return;
            }
            if (LiftState != HSLiftState.Stopping && Time.time >= nextPowerCheck)
            {
                nextPowerCheck = Time.time + PowerCheckInterval;
                string problem;
                if (!HSLiftPower.HasWorkingPower(out problem)) BeginStopping("power lost: " + problem);
                else
                {
                    problem = CheckAhead();
                    if (problem != null) BeginStopping("obstruction: " + problem);
                }
            }
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Movement tick failed", e);
            EnterError("exception during movement");
        }
    }

    // Players, vehicles, dropped items — anything sitting in the car / on the pad rides with it.
    void CarryRiders(float carY, float dy)
    {
        if (Mathf.Approximately(dy, 0f) || !D.HasCar) return;
        CarryPlayer(carY, dy);
        CarryEntities(carY, dy);
    }

    // The player controller only rides layer-28 platforms, which its ground check can't see, so move riders with the car.
    void CarryPlayer(float carY, float dy)
    {
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var locals = world != null ? world.GetLocalPlayers() : null;
        if (locals == null) return;
        var delta = new Vector3(0f, dy, 0f);
        for (int i = 0; i < locals.Count; i++)
        {
            var player = locals[i] as EntityPlayerLocal;
            var fp = player != null ? player.vp_FPController : null;
            if (fp == null || player.AttachedToEntity != null) continue;
            if (!EntityOnCar(player, carY)) continue;
            fp.SetPosition(fp.Transform.position + delta);
        }
    }

    readonly List<Entity> riders = new List<Entity>();

    void CarryEntities(float carY, float dy)
    {
        var world = GameManager.Instance.World;
        if (world == null) return;
        riders.Clear();
        var bb = new Bounds();
        bb.SetMinMax(
            new Vector3(D.MinX - 0.5f, carY - 1f, D.MinZ - 0.5f),
            new Vector3(D.MinX + D.SizeX + 0.5f, carY + D.RideHeight + 1f, D.MinZ + D.SizeZ + 0.5f));
        world.GetEntitiesInBounds(typeof(Entity), bb, riders);
        var delta = new Vector3(0f, dy, 0f);
        for (int i = 0; i < riders.Count; i++)
        {
            var e = riders[i];
            if (e == null || e.AttachedToEntity != null) continue;
            if (e is EntityPlayer) continue;
            if (e is EntityFallingBlock) continue;
            if (!EntityOnCar(e, carY)) continue;
            e.SetPosition(e.position + delta, true);
            var v = e as EntityVehicle;
            if (v != null) v.PhysicsResetAndSleep();
        }
    }

    static bool EntityOnCar(Entity e, float carY)
    {
        if (e == null || !D.HasCar) return false;
        var box = e.boundingBox;
        if (box.max.y < carY - 0.25f || box.min.y > carY + D.RideHeight) return false;
        var c = box.center;
        if (HSLiftCar.InFootprint(Mathf.FloorToInt(c.x), Mathf.FloorToInt(c.z))) return true;
        return HSLiftCar.InFootprint(Mathf.FloorToInt(e.position.x), Mathf.FloorToInt(e.position.z));
    }

    void Update()
    {
        try
        {
            Push();
            if (Bound != null && Bound.HasCar)
                HSLiftCabinLights.Tick(Bound, LiftState == HSLiftState.Idle);
            if (releasing != null && Time.time >= releaseAt) FinishRelease();
            if (!HSLiftNet.IsAuthority)
            {
                if (preview != null && Time.time >= previewUntil) StopPreviewInternal();
                return;
            }
            TickAutoClose();
            TickReopenIfBlocked();
            if (LiftState == HSLiftState.Idle && Time.time >= nextShaftRoof)
            {
                nextShaftRoof = Time.time + 2f;
                var wRoof = GameManager.Instance != null ? GameManager.Instance.World : null;
                if (wRoof != null && D != null && D.HasCar)
                {
                    int before = D.AutoShaftRoofY;
                    if (HSLiftCar.EnsureShaftRoof(wRoof) > 0 || D.AutoShaftRoofY != before)
                        HSLiftConfiguration.Save();
                }
            }
            if (preview != null)
            {
                if (Time.time >= previewUntil) StopPreviewInternal();
                else preview.transform.position = HSLiftCar.UnityPos(D.CurrentY) + new Vector3(D.SizeX + 3, 0, 0);
            }
            if (Time.time < nextRetry) return;
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null) return;

            if (recovering)
            {
                nextRetry = Time.time + RetryInterval;
                if (HSLiftCar.TryRecoverJournal(world)) recovering = false;
            }
            else if (!signsPrimed && LiftState == HSLiftState.Idle)
            {
                HSLiftFloorSigns.UpdateToCurrentFloor(world);
                signsPrimed = true;
            }
            else if (LiftState == HSLiftState.Error && cells != null)
            {
                nextRetry = Time.time + RetryInterval;
                Arrive(Mathf.RoundToInt(move.CurY));
            }
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Controller update failed", e);
            nextRetry = Time.time + RetryInterval;
        }
    }

    // Rows the car overlaps now or enters next, limited to the swept path, never its original cells.
    string CheckAhead()
    {
        var world = GameManager.Instance.World;
        int dir = move.Direction;
        if (cells != null)
        {
            int pathLo = Math.Min(fromY, move.TargetY), pathHi = Math.Max(fromY, move.TargetY);
            foreach (var c in cells)
            {
                if (HSLiftCar.IsRidePiece(c.Bv)) continue;
                int x = D.MinX + c.Dx, z = D.MinZ + c.Dz;
                if (HSLiftCar.IsExcludedCell(x, c.Dy, z)) continue;
                int y = (dir > 0 ? Mathf.CeilToInt(move.CurY) : Mathf.FloorToInt(move.CurY)) + c.Dy + dir;
                if (y < pathLo + c.Dy || y > pathHi + c.Dy) continue;
                if (y == fromY + c.Dy) continue;
                if (HSLiftCar.InBox(new Vector3i(x, y, z), fromY)) continue;
                if (HSLiftCar.IsRegisteredFloorY(y) && y != fromY) continue;
                if (HSLiftCar.CellIsParkedNeighbour(new Vector3i(x, y, z))) continue;
                var err = HSLiftCar.CheckClear(world, new Vector3i(x, y, z), "is blocking the lift shaft", true);
                if (err != null) return err;
            }
            return null;
        }
        for (int x = D.MinX; x < D.MinX + D.SizeX; x++)
        for (int z = D.MinZ; z < D.MinZ + D.SizeZ; z++)
        {
            if (HSLiftCar.IsExcluded(x, z)) continue;
            int h = HSLiftCar.ParkedHeight;
            int lo = Mathf.FloorToInt(move.CurY) + (dir < 0 ? -1 : 0);
            int hi = Mathf.CeilToInt(move.CurY) + h - 1 + (dir > 0 ? 1 : 0);
            int pathLo = Math.Min(fromY, move.TargetY), pathHi = Math.Max(fromY, move.TargetY) + h - 1;
            for (int y = Math.Max(lo, pathLo); y <= Math.Min(hi, pathHi); y++)
            {
                if (y >= fromY && y < fromY + h) continue;
                if (HSLiftCar.IsRegisteredFloorY(y) && y != fromY) continue;
                if (HSLiftCar.CellIsParkedNeighbour(new Vector3i(x, y, z))) continue;
                var err = HSLiftCar.CheckClear(world, new Vector3i(x, y, z), "is blocking the lift shaft", true);
                if (err != null) return err;
            }
        }
        return null;
    }

    void BeginStopping(string reason)
    {
        int settle = move.SettleY(fromY);
        HSLiftDebug.Info("Stopping safely at Y" + settle + " - " + reason);
        move.TargetY = settle;
        LiftState = HSLiftState.Stopping;
    }

    void Arrive(int y)
    {
        Push();
        var world = GameManager.Instance.World;
        var problem = HSLiftCar.PlaceInWorld(world, y, cells);
        if (problem != null)
        {
            EnterError("cannot put the car back at Y" + y + ": " + problem);
            return;
        }
        D.CurrentY = y;
        HSLiftConfiguration.Save();
        HSLiftCar.ClearJournal();
        StopMoveSound();
        PlayAt(D.ArriveSound, y);
        HSLiftNet.BroadcastMoveEnd(D.ElevatorId, y);
        HSLiftDebug.Info("Arrived at Y" + y + " (" + HSLiftConfiguration.FloorLabel(y) + ")");

        // Keep the moving copy a moment so the player stays supported while the chunk rebuilds its collision.
        releasing = move.Root;
        releaseAt = Time.time + ReleaseDelay;
        move.Root = null;
        cells = null;
        errorReason = null;
        LiftState = HSLiftState.Idle;
        HSLiftDoors.OpenAtCar(world);
    }

    // --- sounds (vanilla sound names from config) ---

    string loopPlaying;
    AudioSource customLoop;
    AudioSource cabinMusic;
    string cabinTrack;

    void StartMoveSound(GameObject carRoot)
    {
        try
        {
            customLoop = HSLiftSounds.StartMoveLoop(carRoot);
            StartCoroutine(HSLiftSounds.PlayCabinMusic(carRoot, cabinTrack, src => cabinMusic = src));
            if (customLoop != null) return;
            var name = D.MoveLoopSound;
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (string.IsNullOrEmpty(name) || player == null) return;
            Audio.Manager.PlayInsidePlayerHead(name, player.entityId, 0f, true, true);
            loopPlaying = name;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Move sound failed: " + e.Message);
        }
    }

    void StopMoveSound()
    {
        try
        {
            if (customLoop != null) { customLoop.Stop(); Destroy(customLoop); }
            customLoop = null;
            if (cabinMusic != null)
            {
                cabinMusic.Stop();
                UnityEngine.Object.Destroy(cabinMusic.gameObject);
            }
            cabinMusic = null;
            if (loopPlaying == null) return;
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player != null) Audio.Manager.StopLoopInsidePlayerHead(loopPlaying, player.entityId);
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Move sound stop failed: " + e.Message);
        }
        loopPlaying = null;
    }

    void PlayAt(string sound, int baseY)
    {
        try
        {
            var center = new Vector3(D.MinX + D.SizeX * 0.5f, baseY + 1f, D.MinZ + D.SizeZ * 0.5f);
            if (HSLiftSounds.PlayDingAt(center) || string.IsNullOrEmpty(sound)) return;
            Audio.Manager.BroadcastPlay(center, sound);
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Sound " + sound + " failed: " + e.Message);
        }
    }

    void EnterError(string reason)
    {
        StopMoveSound();
        if (LiftState != HSLiftState.Error || errorReason != reason)
            HSLiftDebug.Error(reason + ". The car copy stays in place; retrying every " + RetryInterval + "s.");
        errorReason = reason;
        LiftState = HSLiftState.Error;
        nextRetry = Time.time + RetryInterval;
    }

    void FinishRelease()
    {
        if (releasing != null) Destroy(releasing);
        releasing = null;
    }

    // --- shutdown ---

    public static void BeginRemoteMove(string liftId, int parkedY, int targetY, float curY, List<HSLiftCell> captured, string musicFile)
    {
        var d = HSLiftConfiguration.ById(liftId);
        if (!Bind(d) || captured == null || captured.Count == 0) return;
        var c = Ensure(d);
        if (c == null) return;
        c.Push();
        c.FinishRelease();
        c.StopPreviewInternal();
        c.fromY = parkedY;
        c.cells = captured;
        c.cabinTrack = musicFile;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var root = HSLiftCar.BuildVisual(world, captured, true, parkedY);
        c.move.Begin(root, parkedY, targetY);
        c.move.CurY = curY;
        c.move.Apply();
        c.LiftState = targetY > parkedY ? HSLiftState.MovingUp : HSLiftState.MovingDown;
        c.StartMoveSound(root);
        HSLiftDebug.Info("Client car " + liftId + " moving Y" + parkedY + " -> Y" + targetY);
    }

    public static void FinishRemoteMove(string liftId, int y)
    {
        var d = HSLiftConfiguration.ById(liftId);
        if (!Bind(d)) return;
        var c = Of(d);
        if (c == null) return;
        c.Push();
        d.CurrentY = y;
        c.StopMoveSound();
        c.PlayAt(d.ArriveSound, y);
        c.releasing = c.move.Root;
        c.releaseAt = Time.time + ReleaseDelay;
        c.move.Root = null;
        c.cells = null;
        c.errorReason = null;
        c.LiftState = HSLiftState.Idle;
        HSLiftDebug.Info("Client car " + liftId + " arrived Y" + y);
    }

    public static void SendActiveMoves(ClientInfo ci)
    {
        if (ci == null) return;
        foreach (var c in all)
        {
            if (c == null || c.Bound == null || !c.IsThisMoving || c.cells == null) continue;
            HSLiftNet.SendMoveStartTo(ci, c.Bound.ElevatorId, c.fromY, c.move.TargetY, c.move.CurY, c.cells, c.cabinTrack);
        }
    }

    public static void OnWorldShuttingDown()
    {
        foreach (var c in all.ToArray())
        {
            if (c == null) continue;
            c.Push();
            c.StopMoveSound();
            c.StopPreviewInternal();
            c.FinishRelease();
            if (!HSLiftNet.IsAuthority)
            {
                c.move.Destroy();
                c.cells = null;
                c.LiftState = HSLiftState.Idle;
                continue;
            }
            if (c.cells == null)
            {
                c.LiftState = HSLiftState.Idle;
                continue;
            }
            var world = GameManager.Instance.World;
            int settle = c.move.SettleY(c.fromY);
            var problem = world != null ? HSLiftCar.PlaceInWorld(world, settle, c.cells) : "no world";
            if (problem != null && world != null && settle != c.fromY)
            {
                settle = c.fromY;
                problem = HSLiftCar.PlaceInWorld(world, settle, c.cells);
            }
            if (problem == null)
            {
                D.CurrentY = settle;
                HSLiftConfiguration.Save();
                HSLiftCar.ClearJournal();
                HSLiftDebug.Info("World closing mid-move: " + D.ElevatorId + " put back at Y" + settle + ".");
            }
            else
            {
                HSLiftDebug.Error("World closing mid-move and " + D.ElevatorId + " could not be placed (" + problem + "). Journal kept for next load.");
            }
            c.move.Destroy();
            c.cells = null;
            c.LiftState = HSLiftState.Idle;
        }
    }

    // --- preview ---

    public static string StartPreview()
    {
        if (instance == null) return "HSLift not started";
        if (IsMoving) return "lift is moving";
        var world = GameManager.Instance.World;
        List<HSLiftCell> captured;
        int fromY;
        var problem = HSLiftCar.CaptureParked(world, out captured, out fromY);
        if (captured == null || captured.Count == 0) return "Nothing to preview: " + problem;
        instance.StopPreviewInternal();
        instance.preview = HSLiftCar.BuildVisual(world, captured, true, fromY);
        instance.preview.transform.position = HSLiftCar.UnityPos(D.CurrentY) + new Vector3(D.SizeX + 3, 0, 0);
        instance.previewUntil = Time.time + PreviewSeconds;
        return "Preview of " + captured.Count + " blocks shown " + (D.SizeX + 3) + " blocks east of the car for " + PreviewSeconds + "s. No world change."
            + (problem != null ? "\nNote: a real move would be refused: " + problem : "");
    }

    public static string StopPreview()
    {
        if (instance == null) return "HSLift not started";
        instance.StopPreviewInternal();
        return "Preview removed.";
    }

    void StopPreviewInternal()
    {
        if (preview != null) Destroy(preview);
        preview = null;
    }

    // --- status ---

    public static string StatusText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(HSLiftConfiguration.ListLifts());
        sb.AppendLine("Editing: " + HSLiftConfiguration.Summary());
        sb.AppendLine("State: " + State + (instance != null && instance.errorReason != null ? " (" + instance.errorReason + ")" : "")
            + (instance != null && instance.recovering ? " [restoring from journal]" : ""));
        foreach (var p in D.Panels)
            sb.AppendLine("  floor " + p.Stop + " panel " + p.Pos + " wired to power=" + HSLiftPower.IsPanelPowered(p.Pos));
        var missing = new List<string>();
        if (!D.HasCar) missing.Add("car (hslift corner1 / corner2)");
        if (!D.HasUpper) missing.Add("at least two floors (hslift floor add <name>)");
        if (D.HasCar && HSLiftConfiguration.FloorAt(D.CurrentY) == null) missing.Add("car is not at a floor (Y" + D.CurrentY + ")");
        string problem;
        if (!HSLiftPower.HasWorkingPower(out problem)) missing.Add(problem);
        if (D.HasCar && State == HSLiftState.Idle)
        {
            var carProblem = HSLiftCar.ValidateCarAt(GameManager.Instance.World, D.CurrentY);
            if (carProblem != null) missing.Add(carProblem);
        }
        sb.Append(missing.Count == 0 ? "Ready." : "Not ready: " + string.Join("; ", missing.ToArray()));
        return sb.ToString();
    }
}
