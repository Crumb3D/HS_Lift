# Make just the lift (the moving car)

This is only the **cabin that travels**. Landing buttons, landing doors, signs, and power are not the lift.

Your current passenger car is a **5 × 5 × 5** box. Smallest that works: interior about **1 wide × 2 high**, plus walls.

You build it. The mod only **moves** what is inside the two corners.

---

## 1. Corners first (so the box is exact)

Place a cheap block — or **Door Trim 1m** / **Door Trim Corner** — on the **outside** of two **opposite** corners of the cabin (bottom one corner, top the other).

Setup tool → **New Ped Lift** → aim at each → **Set Corner 1** / **Set Corner 2**. Delete helper cubes if you used those.

That box is the lift. Everything below goes **inside** it.

**Doorway platform (does not move):** the block under / between the cabin door and the exit — cube, platform, plate, whatever you like. The mod marks that floor cell as **excluded** when it sees the elevator door. Or aim at it with the setup tool → hold E → **Exclude landing**. Plate Corner above the door still rides and **passes through any block** on the way.

---

## 2. What to build (these RIDE)

| Piece | Shape / item | Notes |
|---|---|---|
| **Floor** | **Any** solid you want (cube, platform, wedge, …) | Cabin floor **rides**. The slab **between the car and the exit** does **not** — any block, left at the landing. |
| **Walls** | **Plate** (normal plate) | Not Sheet. Not Plate Double. |
| **Ceiling** | **Plate** and/or **hatch** | Hatch can open; it rides. |
| **Cabin door** | **Elevator Door** or **Elevator Door Double** | This set travels with the car. |
| **Inside panel** | **Elevator Inside Button Panel** (half / plate-offset if you want flush) | In the car. **Do not Register Panel.** Hold E to pick a floor. |
| **Square corners / lintel** | **Door Trim 1m**, **Door Trim Corner**, **Plate Corner** | These **ride**. Use trim 1m to trim corner (opp corners) as the corner markers. Plate Corner above the cabin door fills the gap so it stays square. |

Need **at least two** solid shaft walls against the car (the building around it), or the cabin has nothing to sit on. If the **shaft well** has no roof, the mod caps it with **concrete half-cubes** at the **real top of the shaft walls**. Build the shaft higher and that lid moves up. This is not the cabin ceiling.

---

## 3. What is NOT the lift (leave at each landing)

| Piece | Why |
|---|---|
| **Landing elevator door** | Stays on that floor; pairs with the cabin door. |
| **Outside button panel** | One per floor, **beside** the shaft. Register these. Wire **one** to power. |
| **Sheets** around the outside button | Stay put (pass-through). |
| **Plate Double** | Stay put. Do not use as cabin walls or the vehicle pad. |
| Other **door trim** (not 1m / not corner) | Stay put (door frames). |
| **Sign letter / number** | **Inside the car:** rides. On the landing: stays and updates to the car’s floor. |
| **Round ladder** | Stay put. |

---

## 4. Stuff you put in the car

If it is **inside the two corners**, it **rides**: chests, crates, workstations, lanterns, a generator, wires, hats on the floor — whatever you placed.

Keep **terrain / dirt** out (the game treats that as ground, not a build). Oversized blocks that stick out of the box still will not go.

**Lift power** still comes from a registered **outside** panel on the shaft. A generator in the car is cargo; it does not replace that wire.

Lanterns ride and stay lit on the moving copy. World block-light comes back when the car parks.

---

## 5. One landing, then copy the idea up

At **this** floor, also place (not in the moving box, or only the landing half):

1. Landing elevator door in the same opening as the cabin door  
2. Walkable floor outside the cabin door (or the cabin door stays shut)  
3. Outside button + optional sheets  
4. Optional wall letter/number within 3 blocks of the outside door  

If the cabin is already on 1 when you set corners, aim at the G landing → **Set Ground**. Then park at the next height → **Add Floor** on that floor slab → repeat landing door + outside button.

---

## 6. Register (not building, but you need it)

1. Hold **Elevator Setup Tool**, aim, hold E  
2. **Register Panel** on each **outside** button only  
3. Wire **one** registered outside panel to a generator / battery  
4. **Finished editing** → Ready  

---

## Quick spawn (admin)

```
giveself hsliftTool
giveself hsliftInsidePanel
giveself hsliftOutsidePanel 4
giveself elevatorDoorDouble 4
```

Creative: search **Plate**, **Door Trim 1m**, **Door Trim Corner**, **Plate Corner**, **Hatch**, **Elevator**.

On a server, put **HS_Lift** in the server Mods **and** every client Mods. One player sets the corners; everyone else sees and uses that car.
