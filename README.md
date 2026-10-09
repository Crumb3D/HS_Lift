# HS Lift

Build your own passenger or vehicle elevators in **7 Days to Die**. You build the cabin or garage pad from vanilla blocks. HS Lift **moves** that structure — it does not spawn a premade car, and it will not chew through blocks in the shaft.

**Version 1.0.33** — dual-load **7 Days to Die 3.2 and 3.3**. Cabin paints use free opaque atlas slots on 3.2; on 3.3 the array is enlarged for the Lift paints.

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
3. Keep the **same files** on the server and every client. Do **not** leave `HSLift.json` or `HSLift*.journal.json` in `Mods/HS_Lift` — the game hashes that folder and then says you have the wrong mod. Lift data lives in the **world save**. This build moves those files out of Mods on launch.
4. Restart the game (and the dedicated server if you use one).

---

## Passenger lift

- You build a **3D cabin**: floor, plate walls/ceiling, elevator doors.
- Smallest interior is about **1 wide × 2 high**. Need **at least two** shaft walls around the car.
- Setup tool marks two **opposite outside corners**. Ground floor **G** is created when both are set.
- Add more landings with **Add Floor**.
- Register an **outside** button on each floor. Wire **one** of those to a generator or battery — that powers the whole lift.
- The **inside** panel rides with the car. No wires. **Hold E** and pick a floor button (G, B1, 10 ...). With 3+ floors a tap only opens the doors; with 2 floors a tap goes to the other floor.
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
| Set Ground | Aimed slab becomes **G**. Floors above become 1, 2, … The car stays put. |
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
| **Sheets**, **Plate Double**, other door trim, **round ladders** | Ride if they are **inside** the car box. Same shapes on the landing stay and the car passes them |
| Vanilla wall **letters / numbers** | Ride if they are **inside** the car. Landing signs stay and update to the car’s floor (first character only: B2 shows B, 10 shows 1) |
| Vanilla **Wood Sign** (1x1, 1x3, 2x5) by a landing door | Write a floor label on it (G, B2, 10 ...). It then reads **Floor: 3** (this landing) and **Lift: 6** (where the car is) and keeps up as the car moves. Signs with any other text are left alone |
| Cabin elevator door | Rides |
| Landing elevator / garage door | Stays |
| Inside panel | Rides |
| Outside panel | Stays (register it) |
| Chests, workstations, lanterns, a generator, wires | Ride if they are inside the car box (terrain / dirt does not) |

Do **not** build cabin **walls** out of sheets.

When the car stops at a floor, any landing block already sitting in a car cell (sheets, Plate Double, door trim, the floor slab) stays where it is, and the car leaves it behind when it moves on.

**Two lifts side by side** can share one wall line: set the second lift's corners so its wall sits in the same blocks as the first lift's wall. The corners refuse any deeper overlap. When both cars are parked at the same height, each pair of plates in the shared line joins into one **Plate Double**, and the trims at its ends join into **Door Trim 1m Double** / **Door Trim Corner Double** (same material and shape only). When either car leaves, the other car's single plate goes back. A landing door belongs to the lift it is in front of: a door is never bound to a lift through a solid wall.

Anything you place **inside the car box** rides with it, including chests and lanterns. Terrain / dirt cannot. Keep the lift’s power wire on an **outside** panel.

Floor scheme (menus and signs): default **GB** is G, 1, 2. **US** shows G as 1, 1 as 2. Set `"FloorScheme": "us"` in the world-save `HSLift.json` and restart, or `hslift scheme us`.

More build detail: [MAKE_THE_LIFT.md](MAKE_THE_LIFT.md), [QUICKSTART.md](QUICKSTART.md), [PLAYER_HANDBOOK.md](PLAYER_HANDBOOK.md). In-game: craft the Elevator Handbook (1 paper) or Journal → Challenges → HS Lift. Printable: `Handbook.html` / `HS-Lift-Handbook.pdf`.

---

## Craft / spawn

Read **Wiring 101** until **Electrician 25** (same rank as a light switch). Then craft the setup tool and panels at a workbench. Handbook is 1 paper in the inventory.

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
| `hslift delete <id>` | Forget a lift's setup (e.g. a duplicate). Its blocks stay in the world |

---

## Troubleshooting

- **Not Ready** — hold the setup tool and run `hslift status`. Corners, a second floor, registered outside panels, and power are the usual gaps.
- **No power** — wire a **registered** outside panel on **this** lift.
- **Car will not leave** — doorway blocked, or a solid in the shaft that is not pass-through. The lift will not break blocks.
- **Cabin falls apart** — walls were sheets, or the corner box missed the walls (use outside helper blocks).
- **Wrong lift edited** — **Use Lift Here** first.
- **Need two shaft walls** — the cabin has to sit against the building.
- After a new `HSLift.dll` or XML change, **restart** the game.
- **Cabin look** — paint the cabin yourself with the **paintbrush**, Metal group: **Lift Floor**, **Lift Wall**, **Lift Ceiling**, **Lift Outside**. Lift Wall is one continuous sheet. Lift Outside is the darker metal for the outside of the car and the shaft.
- **Cabin music** — `hslift music on|off` (host). MP3s live in `assets/music` on each machine. It loops from a speaker inside the car. Quiet background in the cabin. Open doors let a muffled version out that gets clearer as you walk in. It does not stop when you step out.

---

## Building from source

1. Copy `Source/Directory.Build.props.example` to `Source/Directory.Build.props`.
2. Set `GameDir` to your 7 Days to Die install. Set `HarmonyDir` to the folder that contains `0Harmony.dll` (usually `0_TFP_Harmony` in your Mods directory).
3. MSBuild `Source/HSLift.csproj` `/p:Configuration=Release`. Output is `HSLift.dll` in this folder.

---

## Bugs and contributing

Open an issue on the GitHub repository with the game version, what you built, and what happened. Pull requests that keep the current movement, door, and save behaviour are welcome. Do not send a copy of your `HSLift.json` if it contains your world coordinates unless you mean to.

---

## Changelog

**1.0.33** — A floor sign inside the cabin shows the floor the car is on, and the floor it is passing while it moves. It no longer sticks on G or goes blank on the way.

**1.0.32** — Cabin music stays looping inside the car. It is background volume, not a stereo in your ears. Open doors let it out muffled, and it fades up as you come in. Stepping out does not cut it off.

**1.0.31** — Lift Wall is one continuous sheet when blocks sit together. Lift Outside is a darker metal in the same paint group, for the outside of the car and the shaft walls. Cabin music plays quietly while you are in the cabin, parked or moving. Restart the game so the brush and the music both reload.

**1.0.30** — Basement buttons go through B99, and a long floor list pages instead of stopping. A lift draws 5W for every floor from a battery bank only, and the batteries drain. Not enough watts and the lift stays put until the bank can supply it.

**1.0.29** — Plate Double and Door Trim 1m Double on a shared wall stay on the same face as the two singles. Door Trim Corner Double is unchanged.

**1.0.28** — The other lift's shared wall is not a blockage. A car called past a parked neighbour finishes the trip and sits on the floor. Doors stay shut unless the car is actually on a landing, and a door on the shared wall stays with the lift it faces.

**1.0.27** — A sign above a door follows that door's lift. A call panel beside a door (left or right; a panel in the crack belongs to the door it is on the right of) calls that lift. Only the lift that arrived opens its doors.

**1.0.26** — The landing shelf and the Plate Doubles on it stay put. The car move no longer takes them, and puts them back if the game knocks them off.

**1.0.25** — Floors Needing Panels also names those floors, for example `2 (G, 2)`.

**1.0.24** — Setup tool readout sits on the right and shows the active lift, total lifts, cabin, floors, panels, floors still needing a panel, power, where the car is, and whether it is parked.

**1.0.23** — A quick tap of E no longer starts a new lift. Hold E and pick New Ped Lift. Destroying an outside panel unregisters it.

**1.0.22** — The lift being edited only changes when you start a new lift or pick Use This Lift. Adding a floor or registering a panel stays on that lift. While the setup tool is in hand, a small readout beside the crosshair shows that lift, how many lifts there are, and its corners, floors and panels. The full list of every lift is in the F1 console.

**1.0.21** — Setup tool: a newly selected lift with no car yet stays selected; Register Panel and Add Floor no longer jump to the finished lift next door. Panels and doors between two side-by-side shafts go to the shaft they are square in front of.

**1.0.20** — Shared wall line: Door Trim 1m and Door Trim Corner pairs join into their Double shapes too. The Double is turned to cover both singles.

**1.0.19** — Shared wall line: a car never takes the neighbouring lift's plates, including when only one of the two lifts has a wall in that line.

**1.0.18** — The car no longer carries landing blocks (sheets, Plate Double, slabs) away when it leaves a floor. Landing doors only call the lift they face. Side-by-side lifts can share a wall line, and parked shared plates join into a Plate Double. Corners refuse deeper overlaps. Adds `hslift delete <id>`. On 3.3 the block texture array is enlarged so the Lift paints show.

**1.0.11** — Removed AutoPaintInterior from settings and world JSON. Cabin look is paintbrush only.

**1.0.3** — Vehicle garage wells get the same shaft-lid as passenger lifts. Cabin interior paints (paintbrush), host-controlled cabin music (quiet, looping, all clients hear the same track).

**1.0.2** — Dedicated clients no longer get kicked when the lift list arrives (config packet length). If the shaft well has no roof, the mod caps it with concrete half-cubes at the real top of the shaft walls; blocks sitting on that lid move with it.

**1.0.1** — Lift saves stay in the world folder, not Mods.

---

## Credits

Crumb — HS Lift. 7 Days to Die and its assets belong to The Fun Pimps. Harmony is included with the game.
