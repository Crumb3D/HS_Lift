# 7D2DMods listing (paste pack)

Do not publish until you are ready. Copy these fields into the site form. Link the public GitHub repo and a **GitHub Release** that has `HS_Lift-v1.0.0.zip` attached. Follow the live steps on:

https://7daystodiemods.com/posts/guide-github-setup

Add 1–3 screenshots on the site (the listing looks empty without them).

---

## Title

HS Lift

## Short description

Build your own passenger or vehicle elevators in 7 Days to Die. HS Lift moves the cabin or vehicle platform you build, supports multiple floors, call panels, vanilla power and synchronized elevator doors.

## Category

Building

(If the form also offers Gameplay / Mechanics, Building is the best single fit.)

## Version

1.0.0

## Game version

7 Days to Die 3.2

Do not tick 1.0, 2.0, or 3.3 Experimental unless you test those builds.

## Requirements

- Harmony (`0_TFP_Harmony`, ships with 7 Days to Die)
- Restart the game after install

## Credits

Crumb

## Full description

HS Lift lets you build a working elevator from the blocks you already place.

You build the cabin or the vehicle pad. The mod only moves that structure. It does not spawn a premade car and it will not break blocks to force a path.

**Passenger lift**
- 3D cabin: floor, plate walls and ceiling, vanilla elevator doors.
- Smallest interior about 1 wide by 2 high. Needs at least two shaft walls.
- Elevator Setup Tool: hold E, mark two opposite outside corners (cheap helper blocks on the outside of the cabin, then delete them).
- Add as many floors as you want. Ground (G) is created when both corners are set.
- Outside call button on each floor. Register each one. Wire any one registered panel to a generator or battery — that powers the whole lift.
- Inside panel rides with the car. No wires. Hold E and pick a floor.
- Cabin and landing elevator doors on the same opening work together. A locked outside door calls the car.
- The car will not leave while the doorway is blocked. Doors auto-close after 20 seconds and reopen if you walk through.

**Vehicle lift**
- Same tool, New Vehicle Lift. Corners are the floor slab only, both at the same height.
- Moves vehicles sitting on the pad.
- Garage and roll-up doors stay at the landing.
- Inside panel sits on the pad. Do not build the pad out of Plate Double.

**Blocks**
- Plates and hatches used as walls or ceiling ride with the car.
- The cabin or pad floor rides (any solid you choose).
- Plate Corner, Door Trim 1m, and Door Trim Corner ride and pass through landing blocks while moving.
- The doorway slab between the car and the exit stays put (any block). Auto-excluded, or mark it with Exclude landing on the setup wheel.
- Sheets, Plate Double, other door trim, round ladders, and wall letters/numbers stay at the landing.

**Install**
Unzip to `Mods/HS_Lift/` so `ModInfo.xml` is inside that folder. Do not copy another player’s `HSLift.json`. Restart the game.

**Admin**
Creative menu: search Elevator. Console: `giveself hsliftTool`, `hslift`, `hslift status`, `hslift scheme gb|us`, `hslift go G`, `hslift debris`.

In-game handbook: 1 paper, or Journal → Challenges → HS Lift.

## Changelog (v1.0.0)

Initial public release.

- Passenger cabins and vehicle pads you build yourself
- Multi-floor stops, outside call panels, inside floor menu
- Vanilla generator / battery power (one wired outside panel)
- Paired elevator doors; garage / roll-up doors stay on vehicle landings
- Elevator Setup Tool (hold E)
- Doorway safety: wait to close, auto-close, reopen if blocked
- GB / US floor labels
- Admin `hslift` commands

## Installation instructions (short)

1. Extract `HS_Lift-v1.0.0.zip` into your `Mods` folder.
2. Confirm you have `Mods/HS_Lift/ModInfo.xml` and `HSLift.dll`.
3. Leave `0_TFP_Harmony` enabled.
4. Restart 7 Days to Die.

## GitHub

Repository: *(paste your public repo URL)*  
Release asset: `HS_Lift-v1.0.0.zip` on tag `v1.0.0`
