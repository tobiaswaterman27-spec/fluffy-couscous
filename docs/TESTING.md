# AFTER HOURS — test plan

Work down this list after each big update. Note anything odd (and paste red Console lines to Claude).

## 0. Before playing
1. **After Hours > Get Latest Update**, wait for compiling to finish.
2. **After Hours > 3 - Build Mall**.
3. **After Hours > 7 - Check Mall** → should say *Everything checks out*. If not, send the list.

## 1. Single player
| # | Try this | Expect |
|---|---|---|
| 1 | Press Play | Main menu over a slowly turning view of the atrium |
| 2 | New game | Black, ringing ears, blinking, sitting up, standing; "11:30 PM" |
| 3 | Walk / double-tap W to sprint / hold Shift / hold Ctrl / walk backwards | Smooth; crawl is slow and quiet |
| 4 | Open a locker, look in, hold E | You step in and the door shuts; limited looking; E gets you out |
| 5 | Crawl into a low grille in a service corridor | You fit; the duct is dark and loud metal |
| 6 | Call a lift, ride it | Doors part, car moves, chime on arrival |
| 7 | Step onto a moving escalator | It carries you; red button at the bottom stops it |
| 8 | Car park plant room switchboard: switch a wing off | That wing goes dark, emergency lights come on; switch on: tubes struggle then light |
| 9 | Get seen by Elliot and caught | Grab, lift, silence, scream, bite, blood, dropped, black; run resets |
| 10 | Esc > Save and quit to main menu, then Continue | Back where you were, same doors/items |
| 11 | Esc > Settings: gore off, get caught | No blood |
| 12 | Finish any route (Esc > Teleport helps) | White, daylight, the figure behind the glass, credits |

## 2. Co-op (2-4 PCs on the same network)
| # | Try this | Expect |
|---|---|---|
| 1 | Host on one PC, Join on the others | Game appears in the list; names show in the lobby |
| 2 | Start | Everyone wakes up side by side |
| 3 | Walk, crouch, crawl in view of each other | Smooth coloured figures |
| 4 | Talk | Heard from the right direction, quieter through walls; V mutes |
| 5 | One opens a door, picks up / drops / throws an item | Everyone sees it |
| 6 | Two push the same heavy object | Moves faster together |
| 7 | One gets caught | Everyone sees the same sequence; only that player respawns |
| 8 | One reaches an open exit | "GET OUT 5" for all; those in an exit get out, others are left behind |
| 9 | Close the game on a joined PC | The host carries on; the figure disappears |
| 10 | Close the host's game | Joined players return to the menu |

## 3. Performance
- F3 shows frame time. Aim for 60 fps on the PC: atrium, car park, crowded shop, during a power cut, during a catch.
- After **5 - Bake Lighting** everything should look better *and* run at least as fast.

## 4. Builds (needs the Build Support modules in Unity Hub)
- **After Hours > 8 - Make Game Builds > Linux / Windows / macOS**. Builds appear in the `Builds` folder.
- Run the Linux build on this PC. Copy the Windows build to a Windows PC to test it; same for a Mac.
