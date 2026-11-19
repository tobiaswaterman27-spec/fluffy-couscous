# AFTER HOURS

First-person psychological / survival horror. You wake up alone in an enormous shopping centre at
midnight. Something is hunting you. (Behind the scenes, the creature is called **Elliot**.) Single-player and LAN co-op (up to 4). Unity 6, for Windows, macOS and Linux.

- Feature checklist (what is done / not done): [`docs/CHECKLIST.md`](docs/CHECKLIST.md)
- Technical decisions and why: [`docs/DECISIONS.md`](docs/DECISIONS.md)

---

## First-time setup (Linux PC, Unity 6)

You do this once. It takes about 10 minutes plus Unity's first import.

### 1. Create the Unity project
1. Open **Unity Hub**.
2. Click **New project** (top right).
3. Pick the template called **Universal 3D** (it may also be shown as "3D (URP)"). If it shows a
   download icon, click **Download template** first.
4. **Project name:** `AfterHoursGame`
5. **Location:** your home folder (so the project ends up at `~/AfterHoursGame`).
6. Untick "Connect to Unity Cloud" / "Use Unity Version Control" if shown.
7. Click **Create project** and wait for Unity to open. **Then close Unity.**

### 2. Connect the project folder to this GitHub repo
Open a terminal and run these one at a time:

```bash
cd ~/AfterHoursGame
```

```bash
git init
```

```bash
git remote add origin https://github.com/tobiaswaterman27-spec/fluffy-couscous.git
```

```bash
git fetch origin
```

```bash
git checkout -b claude/beautiful-bell-stspz3 --track origin/claude/beautiful-bell-stspz3
```

This adds the `Assets/AfterHours` folder, `docs/` and this README into your project.
(If git asks for a username/password and the repo is private, run `gh auth login` first, or ask Claude.)

### 3. Open the project and set it up
1. Open Unity Hub and open **AfterHoursGame**. Wait for it to finish importing.
2. Check the **Input System** package is installed: top menu **Window > Package Manager**, choose
   **In Project** on the left, look for **Input System**. If it is missing: choose **Unity Registry**,
   search `Input System`, click **Install**, and click **Yes** when asked to restart.
3. Top menu **Edit > Project Settings > Player > Other Settings > Active Input Handling** must be
   **Input System Package (New)** or **Both**.
4. Top menu **After Hours > 1 - Set Up Project** and click OK.
5. Top menu **After Hours > 2 - Build Movement Test Scene** (click **Don't Save** if asked about
   the sample scene) and click OK.

### 4. Play
Press the **Play** button (▶, top centre). Click inside the **Game** view to capture the mouse.

### 5. The mall (Part 3)
1. **After Hours > 3 - Build Mall** (takes a minute or two; click **Don't Save** if asked about the current scene).
2. Press **Play**. You start in the atrium. **Esc > Teleport (dev)** jumps anywhere in the building.
3. **After Hours > 4 - Capture Review Shots** saves 20 pictures of the mall into the `ReviewShots` folder
   (in Play mode, **F12** saves a single screenshot there). Send them to Claude to check.

### 6. The final look (Part 4)
1. **After Hours > 3 - Build Mall** now also adds all the detail, sound and lighting setup. It takes longer
   (several minutes). Unity may look frozen; the progress bar shows what it is doing.
2. Press **Play** to see the "preview" lighting.
3. Stop Play, then **After Hours > 5 - Bake Lighting (slow)**. This pre-calculates the realistic lighting and can
   take from 30 minutes to a few hours. Leave Unity open (progress is in the bottom-right corner).
   When it finishes, press Play again: that is the real look.
4. To go back to preview lighting: **After Hours > 6 - Undo Baked Lighting**.

### Checking and building
- **After Hours > 7 - Check Mall** inspects the built mall and lists anything missing.
- **After Hours > 8 - Make Game Builds** makes a stand-alone game for Linux, Windows or macOS (in the `Builds`
  folder). Each needs its Build Support module: Unity Hub > Installs > gear icon > Add modules.
- The full test plan is in `docs/TESTING.md`.
- Sounds are made by `tools/sfxgen` (`python3 generate.py`, needs numpy + scipy) into `Assets/AfterHours/Resources/Audio`.

### 7. Playing the whole game (Part 13)
After **3 - Build Mall**, pressing **Play** starts at the **main menu** (the camera drifts through the dark atrium).
- **New game**: you wake up on the atrium floor at midnight.
- **Continue**: carries on from the last save (it saves itself every minute when you're not being chased,
  and when you quit). Being caught resets the run and deletes the save.
- **Settings**: sensitivity, field of view, head bob, camera shake, blood and gore, key bindings.
- Esc while playing: pause, settings, **Save and quit to main menu**.
- Getting out by any of the 8 routes plays the daylight ending. **The full guide to the routes is `docs/PROGRESSION.md`** (spoilers).
- It's meant to be long and hard: the important items are in different rooms every run (and again after you're
  caught), most exits need several parts plus the **standby generator** in the car park plant room, and Elliot gets
  more dangerous as the night goes on and as you get closer to escaping. The time is on the pause screen.

### Things to know when playing
- **Hide**: open a locker or cupboard, look inside, hold **E**. **E** again to get out.
  While hidden, **hold Shift to hold your breath** (panting can be heard; hold it too long and you gasp).
- **Vents**: the grilles in the service corridors sit off the floor (knee-high downstairs, chest-high upstairs).
  Pull a loose grille off, then look at the opening and press **E** to climb in (you crawl inside). From inside,
  **E** at an opening climbs out - from an upstairs duct you tumble out onto the floor. You can push a grille off
  from inside, or pull it back behind you to hide.
- **Doors**: **E** opens with the handle (quietly if you're crouched). Locked: one try of the handle - or, if
  you're panicking (sprinting), you rattle it, which is loud. **Sprint straight into a closed door** to barge it open
  (very loud, it smashes into the wall) - but if it's locked, you'll hit it hard instead.
- **Lifts**: call button beside the doors, floor buttons inside. No power = stuck. The alarm bell is very loud.
- **Power cuts**: breakers trip now and then; the area goes dark. The switchboard is in the car park plant room.
- **Escalators**: the red button at the bottom stops them (and restarts them).
- **Tracks**: on hard floors Elliot leaves bloody footprints. Fresh, dark ones mean he passed by moments ago.
- **Generator**: car park plant room. Petrol (jerry can) in, fuse in GEN CONTROL, then pull the cord until it catches.
  Very loud. Four of the exits need it; their panels have an amber lamp that lights when it's running.
- **Fire axe**: in a break-glass cabinet in one of the stairwells. Hold **E** at a locked wooden door to chop through it
  (very loud). It breaks after three jobs.
- **Fire exit**: pushing the bar sets off the alarm, and the door only lets go after 15 seconds - unless you cut the
  magnetic lock's cable first.
- **Hiding without a door**: crawl under a shop counter (from the staff side) or an office desk, or stand in a
  changing room behind the curtain. Quick to get into, but if Elliot comes right up close and looks, he sees you.
- **Knocking things over**: wet-floor signs, bins, A-boards, stacked boxes and chairs go over if you run into them -
  loudly. Elliot hears it.
- **Weather**: a storm outside. Rain you can hear and see: falling past the smoked-glass main doors onto the wet forecourt under an orange street lamp, splashing on the atrium skylight, dripping through it in places. Lightning lights up the sky through the glass, then thunder.
- **Thunder covers you**: while thunder rolls (a few seconds after each flash) Elliot can barely hear footsteps and small knocks - a close strike also makes every faulty tube in the mall stutter. In co-op everyone gets the same strikes.
- **Cars left overnight**: about twenty parked cars and vans in the car park bays - cover to crouch behind. One has its hazard lights blinking on a dying battery.
- **Wet footprints**: walk through a leak's puddle and you leave shoe prints for a dozen steps, drying over a minute.
- **Difficulty**: Easy / Normal / Hard in Settings (the host's counts in co-op).
- **Your record**: on the main menu - nights, ways out found, fastest escape, and achievements.
- **CCTV**: the Security Office (ground floor, north-west back corridor) has live camera feeds of the atrium,
  the malls, the entrance and the car park. Watch them to see where Elliot is. They need power.

### 8. LAN co-op (Parts 14-17)
Everyone needs the same version: each player does **Get Latest Update** and **3 - Build Mall** first.
1. One player picks **Host LAN game** on the main menu and waits in the lobby.
2. The others pick **Join LAN game**: the host's game appears in the list (or type the host's address).
3. The host presses **Start**. With 2-3 players there are 2 creatures; with 4 there are 3.
- Talk normally: other players hear you from where you stand, and so do the creatures. **V** mutes your mic.
- Caught: only you start over. When someone reaches an open exit, everyone has **5 seconds** to get into it.
- If the game isn't found on Linux, allow UDP ports 47799 and 47800 through the firewall:

```bash
sudo ufw allow 47799:47800/udp
```

---

### 9. Who you play as
Ten people to choose from: Tom, Marcus, Priya, Ruth, Dan, Leah, Kenji, Sofia, Arthur and Jade. Pick one on the main
menu (click "You are ..." to go through them) or in Settings. In co-op everyone is a different person (if someone
already has yours, you get the next one).

You see your own arms, hands and legs in first person; your head isn't drawn for you but still casts your shadow.
The characters are built by `tools/chargen` from MakeHuman's CC0 body (run `python3 fetch.py` there first).
They move with real motion capture (walking, running, a low creep, backing away, standing idle, standing scared)
from the CMU Graphics Lab Motion Capture Database, rebuilt on each person's own bones (`tools/mocap`). You hear other
players' footsteps from where they are.

## The one-click way (use this)

**After Hours > ★ Update and Build Everything** does it all in one go: downloads the latest update, waits for
Unity to reload, sets up the project, builds the whole mall (Elliot, the players, items, escape routes, sound,
lighting setup) and checks it. It takes several minutes; Unity may look frozen while the progress bar runs.
If it asks about saving the current scene, click **Don't Save**. When the box says it's done, press **Play**.
(Only the slow **5 - Bake Lighting** is left out - run that separately when you have time.)

## Getting new updates later

In Unity: **After Hours > Get Latest Update**. (Or in a terminal, then click back into Unity:)

```bash
cd "/home/tobias/AFTER HOURS" && git pull
```

If a part says to rebuild the test scene, run the menu item again.

## When something goes wrong

1. Open **Window > General > Console**.
2. Copy the **red** lines (click one, the full text appears at the bottom; select it and copy).
3. Paste them to Claude. Yellow warnings are usually fine but can be pasted too.

---

## Controls (development build)

Crouch, crawl and sprint are all **hold** controls. If you let go where there is no room to rise (vent, low ceiling), you stay low until there is.

All controls can be changed: **Esc > Controls**, click a binding, press the new key. Saved automatically.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move (S = walk backwards) | WASD / arrows | Left stick |
| Look | Mouse | Right stick |
| Sprint: **double-tap a move key and keep holding it** (any direction; backwards is slower) | e.g. W, W (hold) | Hold left stick click, or flick stick twice |
| Crouch (hold) | Left Shift | Hold B / Circle |
| Crawl (hold; slow and very quiet) | Left Ctrl | Hold RB / R1 |
| Grab heavy object (hold), push / pull by walking | E | A / Cross |
| Use / open / pick up / hide (hold) | E | A / Cross |
| Drop held item | G | X / Square |
| Throw held item | Right mouse | RT / R2 |
| Pause, settings, key rebinding | Esc | Start |
| Developer info panel | F3 | |
| See your own collision body (debug) | F5 | |
| Full-bright view (debug) | F6 | |
| Screenshot | F12 | |
