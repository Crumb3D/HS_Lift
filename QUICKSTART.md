# HS Lift — YouTuber quick start (admin)

Drop the **HS_Lift** folder into `Mods`. Do **not** put `HSLift.json` in that folder — the game then tells clients they have the wrong mod. The server stores the lift list in the world save.

On a **dedicated server**, the same folder must be in the **server Mods** and in **every player’s Mods**. If you already ran an older HS Lift, delete `HSLift.json` and `HSLift*.journal.json` from `Mods/HS_Lift` on the server and every client (or launch this build once; it moves them out).

Needs **Harmony** (`0_TFP_Harmony`, ships with 7DTD). Restart the game after installing.

You build the cabin. The mod only **moves** it. It will not chew through blocks.

---

## Spawn (F1 console)

You need admin. Creative menu (**U**) → search **Elevator** also works.

```
giveself hsliftTool
giveself hsliftHandbook
giveself hsliftOutsidePanel 4
giveself hsliftInsidePanel
giveself elevatorDoorDouble 4
giveself elevatorDoor 4
```

Power: any vanilla generator or battery bank, plus wire. Wire **one** registered outside panel — that powers the whole lift.

---

## Passenger lift in 5 minutes

1. Build a **3D cabin** (floor + plate walls/ceiling + elevator doors). Smallest interior about **1 wide × 2 high**. Need **at least two** shaft walls.
2. Hold the **Elevator Setup Tool**, aim at a **placed** block (not dirt), **hold E**.
3. **New Ped Lift**.
4. **Corners (do this, skip grow):** place a cheap building block on the **outside** of two **opposite corners** of the cabin (the cells just outside the walls). Aim at each helper → **Set Corner 1**, then **Set Corner 2**. Delete the helpers. The box now includes the walls. Aiming at the *inside* of the cabin makes the box too small and you would need `hslift grow 1`.
5. Corners name the car’s current height **G**. If the cabin is already on 1, aim at the real ground slab → **Set Ground**. That height becomes G; the car’s floor becomes 1. The cabin does not move.
6. Park / stand at the next landing, aim at its **floor** block, hold E → **Add Floor** (1, 2, B1, …). Repeat per stop.
7. Place an **outside button** beside the shaft on **each** floor. Aim at each → **Register Panel**. **Never** register the inside panel.
8. Put an **inside panel** in the car. It rides. No wires. **Hold E** on it to pick a floor (a tap does nothing).
9. Wire **one** registered outside panel to power.
10. Doorway slab (any block between car and exit) stays put — auto-excluded, or hold E → **Exclude landing**. Plate corners pass through anything while moving.

**Doors:** elevator door on the car **and** on the landing, same opening. They open/close together. Locked outside door **calls** the car. Cabin door stays shut if there is a drop / wall outside that side.

---

## Vehicle lift (garage pad)

Same tool, **New Vehicle Lift**. Corners are the **floor slab only**, both at the **same height**. Use the same outside-corner helper blocks so you capture the whole pad.

Garage / roll-up doors **stay at the landing**. Inside panel sits **on the pad**. Do **not** build the pad out of Plate Double.

---

## Plates vs sheets

| Use | Effect |
|---|---|
| **Plates** / hatches for walls and ceiling | Ride with the car |
| **Any solid** for the floor | Rides |
| **Sheets**, Plate Double, round ladders, wall letters/numbers | Stay at the landing |
| **Plate Corner**, **Door Trim 1m**, **Door Trim Corner** | Ride with the car; sweep through landing blocks while moving |

Sheets around the buttons are correct. **Do not** build cabin **walls** out of sheets.

Floor letter: vanilla **sign G / 1 / 2** within 3 blocks of each outside door. They follow the car.

---

## Demo tips

- **Call** from another floor with the outside button (or E on a locked landing door).
- Car will **not leave** until the doorway is clear. Standing *in* the car is fine.
- Doors auto-close after **20s**. Walk through while closing → they reopen.
- Several lifts: **Use Lift Here** before adding floors / panels so you edit the shaft you are looking at.

---

## Admin extras

| Command | What |
|---|---|
| `hslift` | Help |
| `hslift status` | What’s missing |
| `hslift scheme us` | G shows as 1, 1 as 2 (or set `"FloorScheme": "us"` in `HSLift.json` and restart) |
| `hslift scheme gb` | G, 1, 2 (default) |
| `hslift debris` | Wipe dirt/rubble in this shaft |
| `hslift go G` | Send the car |

Full write-up: in-game **Elevator Handbook** (1 paper), **Journal → Challenges → HS Lift**, or `PLAYER_HANDBOOK.md` / `Handbook.html`.
