# HS Lift — Player Handbook

Craft the **Elevator Handbook** from **1 paper** (inventory craft) and use it in-game. It is not used up.

The same walkthrough is under **Journal → Challenges → HS Lift**.

A printable copy is `Handbook.html` in this mod folder (open in a browser). A ready-made PDF is `HS-Lift-Handbook.pdf`.

You build the car and shaft. HS Lift only **moves** what belongs to that lift. It will not break blocks to pass an obstruction. After a new `HSLift.dll`, restart the game.

---

## What you craft

| Item | Where | What it is |
|---|---|---|
| Elevator Handbook | 1 paper, hand craft | This guide. Use it to read. Not consumed. |
| Elevator Setup Tool | Workbench after **Wiring 101** (Electrician 25) | Hold it, aim at a **placed** block (wood through steel, **not dirt**), **hold E**. |
| Outside Button Panel | Workbench | One per floor, beside the shaft. Wire **any one** registered panel of that lift to a generator or battery bank. |
| Inside Button Panel | Workbench | Optional. Goes **in the car** (passenger) or **on the pad** (vehicle). No wires. **Do not register it.** |

Vanilla **elevator doors** are what you want on a passenger lift. Vehicle lifts use **garage / roll-up** doors.

Half-block and plate-offset panel variants exist so the buttons sit flush on plates and thin walls.

---

## Setup tool (hold E)

Aim at the block the command needs, then pick from the wheel. **Use Lift Here** first if you have more than one lift, so later commands hit the shaft you are looking at.

1. **New Ped Lift** / **New Vehicle Lift** — always starts a **new** lift. Never overwrites one you already registered.
2. **Use Lift Here** — bind the aimed shaft (or the nearest one on this XZ) so Add Floor, Register Panel, and admin commands edit the right lift.
3. **Set Corner 1** / **Set Corner 2** — opposite corners. **Passenger:** the whole 3D cabin. **Vehicle:** the **floor slab only**, both corners the **same height**.
4. **Add Floor** — park the car at that landing first, then aim at a floor block there. Name it (G, 1, 2, B1, …).
5. **Register Panel** — aim at an **outside** wall button only. Never the inside panel.
6. **Finished editing** — lists what is still missing, or Ready.

Ground floor is created when both corners are set.

---

## Passenger lift (ped)

- Build a **3D cabin**. Smallest interior **1 wide × 2 high**. Floor: **any block**. Walls and ceiling: **plates** and/or **hatches** (a hatch-only ceiling is fine).
- Mark opposite corners of that **whole box**.
- Need **at least two shaft walls** so the car has something to sit against.
- The mod places **walls first**, then floor, then ceiling, then interior — so the cabin does not collapse while it rebuilds. Removal is the reverse.

### Doors

- Place **elevator doors** at each landing. A **cabin door** and a **landing door** on the same opening open and close together.
- You may add a **second set** (back exit). Those doors open and close with the others on that floor.
- Doors stay **locked** until the car is at that floor. Using a locked **outside** door **calls** the car.
- **Cabin doors** will not open on a side with **no walkable floor** outside (a drop, or a wall in your face). **Outside landing doors** are not locked by that check — the floor check is only for doors **on the car**.
- **Hold E** on the **inside panel** and pick a floor. A **tap** of E does **not** send the car up or down.
- If there is **no inside panel**, **hold E** on the cabin door (while standing in the car) to pick a floor. Closing the door does **not** send the car.

### Riding

Players, vehicles, and dropped loot **inside the cabin** ride with it.

---

## Vehicle lift

- **Platform only.** Both corners at the **same Y**. No cabin box.
- **One garage / roll-up set per landing.** Those doors **stay at the landing**; they are not part of the moving pad.
- If the pad is at that floor, E opens that garage. If the pad is elsewhere, E **calls** it; the door opens when it arrives.
- Put the **inside panel on the pad**. It rides. **Do not** Register Panel on it.
- The pad carries **the truck and anything sitting on it**.
- Do **not** build the vehicle pad out of **Plate Double** (that shape is pass-through). Use **full cubes** (or any solid floor block) for the pad.

---

## Plates, sheets, and pass-through

| Build this | What happens |
|---|---|
| **Floor** — any solid block | Rides with the car. |
| **Walls / ceiling** — **plates** or **hatches** | Ride with the car. Regular plates are **not** pass-through. |
| **Sheets** (including *Wood - Sheet* / billboard shapes) | **Pass-through.** Stay at the landing. Use them around button panels. |
| **Plate Double** | **Pass-through** (passenger cabin trim / thin floors). Not for a vehicle pad. |
| **Plate Corner** | **Rides** with the car (lintel / square corners). Sweeps through landing blocks while moving. Regular plates also ride. |
| **Door Trim 1m** / **Door Trim Corner** | **Ride** with the car. Good Corner 1 / Corner 2 markers (bottom trim + opposite top trim corner). Other door trim stays at the landing. |
| **Round ladder** | Pass-through. |
| **Vanilla wall letters / numbers** | Pass-through. Used as floor signs (below). |

Do **not** build cabin **walls** out of sheets — only the floor will move and the car will fall apart.

The lift will **not** overwrite pass-through blocks when it parks, and it treats them as clear when checking the shaft and the doorway.

---

## Power

- Register **only** outside button panels that belong to **this** shaft.
- Unregistered panels do nothing. They cannot take power from another lift.
- Wire **any one** registered outside panel on that lift. That powers the **whole** lift. Other shafts need their own circuit.
- XML power draw is **1** (vanilla electrical).

---

## Calling, floors, and doors in use

- **Outside button:** tap E to **call** the car to that floor (or open the doors if it is already here).
- **Inside panel:** **hold E**, then choose a floor. Tap E does nothing.
- The car **will not leave** while something is in the **doorway** (a person, loot, or a solid that is not pass-through). Pass-through sheets and plate doubles do **not** count as blocked.
- Doors **auto-close** after **20 seconds** (real time) if the opening is clear. If something is in the way, they wait and you get a tooltip.
- If you **walk out while they are closing**, they **open again** (safety edge), then try to close once you are clear.
- **Floor letter:** place a vanilla **sign letter or number** (G, 1, 2, B1 as a letter, …) within **3 blocks** of each **outside** door (elevator door on a ped, garage / roll-up on a vehicle). They change to the floor the car is on, including **while it is passing**.

---

## If it will not move

- **Finished editing** on the setup tool lists what is missing (corners, second floor, panels, power).
- No power, or the wrong lift’s panel wired.
- Shaft blocked — the lift **stops**. It will not chew through your build.
- Doors cannot close — wait until the opening is clear.
- Cabin has fewer than **two** supporting shaft walls.
- Game **restart** after a new `HSLift.dll`. XML (handbook, recipes) also reloads on restart.

Admins: console `hslift` for help. `hslift debris` clears dirt / rubble in the footprint (not the car, doors, panels, or pass-through).
