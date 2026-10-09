using System;
using System.Collections.Generic;

public class ConsoleCmdHSLift : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[] { "hslift" };
    }

    public override string getDescription()
    {
        return "HSLift admin: leftover setup and debug. Players use the Elevator Setup Tool (hold E).";
    }

    // Same level as help/give-style admin commands; F1 console is locked to admins.
    public override int DefaultPermissionLevel { get { return 1000; } }

    public override string getHelp()
    {
        return
            "Admin only. Players: craft/hold the Elevator Setup Tool and hold E.\n" +
            "Aim at blocks while typing the setup commands.\n" +
            "hslift corner1 | corner2      - mark opposite corners of the car\n" +
            "hslift type ped | vehicle     - START a new lift. ped = 3D cabin + elevator doors. vehicle = floor platform only + garage/roll-up doors. Never overwrites another lift.\n" +
            "hslift list                   - list every lift (* = the one you are editing)\n" +
            "hslift select                 - aim at a lift: make that one the one you are editing\n" +
            "hslift delete <id>            - forget a lift's setup (e.g. a duplicate); its blocks stay in the world\n" +
            "hslift floor add <name>       - aim at a landing floor block: adds a floor (G is set with the car)\n" +
            "hslift floor ground           - aim at a landing: that height becomes G. Floors above become 1, 2, …\n" +
            "hslift floor remove <name>    - forget a floor\n" +
            "hslift floor list             - show floors\n" +
            "hslift scheme gb | us         - floor labels (also FloorScheme in the world-save HSLift.json: gb or us). GB default: G, 1, 2. US: G becomes 1\n" +
            "hslift music on | off         - host: cabin MP3s on/off for every client. Empty assets/music is silent\n" +
            "hslift flicker on | off       - host: cabin bulb flicker (JSON FlickerLights, default on)\n" +
            "hslift panel                  - register the aimed outside button panel (floor found from its height)\n" +
            "hslift panel clear            - forget all registered panels\n" +
            "hslift go <floor>             - send the car to a floor\n" +
            "hslift up | down              - move the car one floor\n" +
            "hslift speed <blocks/sec>     - travel speed\n" +
            "hslift preview [off]          - show the moving copy beside the car, no world change\n" +
            "hslift status                 - show setup and what is missing\n" +
            "hslift reset                  - forget the car selection\n" +
            "hslift grow <n>               - widen the car box by n blocks on every side (to include outer walls)\n" +
            "hslift exclude [rows]         - aim at the doorway platform (any block): it stays put. Setup tool: Exclude landing\n" +
            "hslift exclude list | clear   - show / remove stationary landing columns\n" +
            "hslift cleanup <y>            - remove a leftover copy of the car whose floor is at Y (only blocks matching the car)\n" +
            "hslift debris                 - delete dirt/terrain/rubble in this lift's shaft (not the car, doors, panels, or pass-through)\n" +
            "hslift debug                  - toggle verbose logging";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        try
        {
            var sub = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
            var arg = _params != null && _params.Count > 1 ? _params[1].ToLowerInvariant() : "";
            var extra = _params != null && _params.Count > 2 ? _params[2] : "";
            var world = GameManager.Instance.World;
            EntityPlayerLocal player = null;
            if (world != null)
            {
                player = world.GetPrimaryPlayer();
                if (player == null)
                {
                    var locals = world.GetLocalPlayers();
                    if (locals != null && locals.Count > 0) player = locals[0] as EntityPlayerLocal;
                }
            }
            Out(HSLiftSetup.Execute(sub, arg, extra, player));
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Command failed", e);
            Out("HSLift command failed: " + e.Message);
        }
    }

    static void Out(string s)
    {
        SdtdConsole.Instance.Output(s);
    }
}
