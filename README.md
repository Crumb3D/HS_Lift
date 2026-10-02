# HS Lift

Build your own passenger or vehicle elevators in **7 Days to Die**. You build the cabin or garage pad from vanilla blocks. HS Lift **moves** that structure — it does not spawn a premade car, and it will not chew through blocks in the shaft.

**Version 1.0.0** — tested on **7 Days to Die 3.2**. Not claimed for 1.0, 2.0, or 3.3 Experimental.

License: [MIT](LICENSE). Author: Crumb.

---

## What it solves

Vanilla elevator doors and panels are decoration. HS Lift turns a cabin or vehicle pad you built into a working multi-floor lift with call buttons, an inside floor menu, vanilla generator/battery power, and paired elevator (or garage) doors.

---

## Requirements

- 7 Days to Die **3.2** (PC).
- **Harmony** — `0_TFP_Harmony`, ships with the game. Do not remove it.
- A game restart after installing or replacing `HSLift.dll`.
- **Multiplayer:** the same `HS_Lift` folder on the **dedicated server and every client**. Lifts live on the server; everyone can see and use cars other players set up.

---

## Installation

1. Unzip so you have `7 Days To Die/Mods/HS_Lift/` (or the same path under `%AppData%/7DaysToDie/Mods/`). On a dedicated server, put the same folder in the server `Mods` directory.
2. `ModInfo.xml` and `HSLift.dll` must sit **directly** in that `HS_Lift` folder — not `Mods/HS_Lift/HS_Lift/`.
3. **Do not** copy someone else’s `HSLift.json`. The **server** writes the live one. Clients receive the lift list from the server.
4. Restart the game (and the dedicated server if you use one).

---

## Passenger lift

- You build a **3D cabin**: floor, plate walls/ceiling, elevator doors.
- Smallest interior is about **1 wide × 2 high**. Need **at least two** shaft walls around the car.
- Setup tool marks two **opposite outside corners**. Ground floor **G** is created when both are set.
- Add more landings with **Add Floor**.
- Register an **outside** button on each floor. Wire **one** of those to a generator or battery — that powers the whole lift.
- The **inside** panel rides with the car. No wires. **Hold E** and pick a floor (a tap does nothing).
- Cabin door and landing door on the same opening open and close together.
- A locked outside door **calls** the car.
- Cabin doors stay shut if there is no walkable floor outside that side (a drop or a wall).
- The car will not leave while the **doorway** is blocked. Standing in the car is fine.
- Doors auto-close after **20 seconds** (real time) and reopen if something enters the opening.

---

## Vehicle lift

- Same tool → **New Vehicle Lift**.
- Corners define the **floor slab only**, both at the **same height**.
- Garage / roll-up doors **stay at the landing**.
- Inside panel sits **on the pad** and rides. Do not register it.
- Do **not** build the pad out of Plate Double (that shape is pass-through).

---

## Setup tool (hold E)

Aim at a **placed** block (wood through steel, not dirt), then hold E.

| Wheel | What it does |
|---|---|
| New Ped Lift | Starts a **new** passenger lift |
| New Vehicle Lift | Starts a **new** vehicle pad |
| Use Lift Here | Edit the shaft you are aiming at (several lifts) |
| Set Corner 1 / 2 | Opposite corners of the cabin (or pad) |
| Add Floor | Register this landing height |
| Register Panel | **Outside** buttons only |
| Exclude landing | Mark the doorway slab so it stays put |

**Corners:** place a cheap block (or Door Trim) on the **outside** of two opposite corners, set Corner 1 and 2 on those, then delete the helpers. That includes the walls. Aiming at the inside of the cabin makes the box too small.

**Doorway platform:** any block between the car and the exit stays at the landing. The mod auto-excludes the floor cell under the elevator door. You can also aim at it and choose **Exclude landing**.

Admin: `hslift status` lists what is still missing.

---

## Power

- Compatible with vanilla generators and battery banks.
- Register only outside panels that belong to **this** shaft.
- Wire **any one** registered outside panel. XML draw is 1.
- The inside panel does not take a wire.
- Unregistered panels do nothing and cannot borrow power from another lift.

---

## What rides vs what stays

| Build this | Behaviour |
|---|---|
| Floor — any solid you choose | Rides (cabin / pad) |
| Walls and ceiling — **plates** or **hatches** | Ride |
| **Plate Corner**, **Door Trim 1m**, **Door Trim Corner** | Ride; sweep through landing blocks while moving |
| Doorway slab between car and exit | Stays (excluded). Any block. |
| **Sheets**, **Plate Double**, other door trim, **round ladders** | Stay at the landing |
| Vanilla wall **letters / numbers** | Stay; they update to the car’s floor |
| Cabin elevator door | Rides |
| Landing elevator / garage door | Stays |
| Inside panel | Rides |
| Outside panel | Stays (register it) |
| Chests, workstations, lanterns, a generator, wires | Ride if they are inside the car box (terrain / dirt does not) |

Do **not** build cabin **walls** out of sheets.

Anything you place **inside the car box** rides with it, including chests and lanterns. Terrain / dirt cannot. Keep the lift’s power wire on an **outside** panel.

Floor scheme (menus and signs): default **GB** is G, 1, 2. **US** shows G as 1, 1 as 2. Set `"FloorScheme": "us"` in `HSLift.json` and restart, or `hslift scheme us`.

More build detail: [MAKE_THE_LIFT.md](MAKE_THE_LIFT.md), [QUICKSTART.md](QUICKSTART.md), [PLAYER_HANDBOOK.md](PLAYER_HANDBOOK.md). In-game: craft the Elevator Handbook (1 paper) or Journal → Challenges → HS Lift. Printable: `Handbook.html` / `HS-Lift-Handbook.pdf`.

---

## Craft / spawn

Workbench (electrician / Advanced Engineering) for the setup tool and panels. Handbook is 1 paper in the inventory.

Admin:

```
giveself hsliftTool
giveself hsliftHandbook
giveself hsliftOutsidePanel 4
giveself hsliftInsidePanel
giveself elevatorDoorDouble 4
giveself elevatorDoor 4
```

Creative menu (**U**) → search **Elevator**.

---

## Admin commands

F1 console (admin). `hslift` prints the full list. Common:

| Command | What |
|---|---|
| `hslift` | Help |
| `hslift status` | What’s missing |
| `hslift scheme gb` / `us` | Floor labels |
| `hslift go G` | Send the car |
| `hslift debris` | Clear dirt/rubble in this shaft |
| `hslift exclude` | Aim at a landing slab; it stays put |

---

## Troubleshooting

- **Not Ready** — hold the setup tool and run `hslift status`. Corners, a second floor, registered outside panels, and power are the usual gaps.
- **No power** — wire a **registered** outside panel on **this** lift.
- **Car will not leave** — doorway blocked, or a solid in the shaft that is not pass-through. The lift will not break blocks.
- **Cabin falls apart** — walls were sheets, or the corner box missed the walls (use outside helper blocks).
- **Wrong lift edited** — **Use Lift Here** first.
- **Need two shaft walls** — the cabin has to sit against the building.
- After a new `HSLift.dll` or XML change, **restart** the game.

---

## Building from source

1. Copy `Source/Directory.Build.props.example` to `Source/Directory.Build.props`.
2. Set `GameDir` to your 7 Days to Die install. Set `HarmonyDir` to the folder that contains `0Harmony.dll` (usually `0_TFP_Harmony` in your Mods directory).
3. MSBuild `Source/HSLift.csproj` `/p:Configuration=Release`. Output is `HSLift.dll` in this folder.

---

## Bugs and contributing

Open an issue on the GitHub repository with the game version, what you built, and what happened. Pull requests that keep the current movement, door, and save behaviour are welcome. Do not send a copy of your `HSLift.json` if it contains your world coordinates unless you mean to.

---

## Credits

Crumb — HS Lift. 7 Days to Die and its assets belong to The Fun Pimps. Harmony is included with the game.
