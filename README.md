# AFTER HOURS

First-person psychological / survival horror. You wake up alone in an enormous shopping centre at
11:30 PM. Something is hunting you. (Behind the scenes, the creature is called **Elliot**.) Single-player and LAN co-op (up to 4). Unity 6, for Windows, macOS and Linux.

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

---

## Getting new updates later

Close nothing; just run this in a terminal, then click back into Unity (it reloads automatically):

```bash
cd ~/AfterHoursGame && git pull
```

If a part says to rebuild the test scene, run the menu item again.

## When something goes wrong

1. Open **Window > General > Console**.
2. Copy the **red** lines (click one, the full text appears at the bottom; select it and copy).
3. Paste them to Claude. Yellow warnings are usually fine but can be pasted too.

---

## Controls (development build)

All controls can be changed: **Esc > Controls**, click a binding, press the new key. Saved automatically.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move (S = walk backwards) | WASD / arrows | Left stick |
| Look | Mouse | Right stick |
| Sprint (works backwards too, slower) | Left Shift | Left stick click |
| Crouch (hold; can be set to toggle in Settings) | Left Ctrl | B / Circle |
| Crawl on / off (slow and very quiet) | C | D-pad down |
| Grab heavy object (hold), push / pull by walking | E | A / Cross |
| Drop (used from Part 6) | G | X / Square |
| Pause, settings, key rebinding | Esc | Start |
| Developer info panel | F3 | |
| See your own collision body (debug) | F5 | |
