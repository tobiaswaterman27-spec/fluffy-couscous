# Development checklist

Status key: ✅ built and confirmed working in Unity · 🧪 built, waiting for in-editor test · 🔨 foundation only ·
⬜ not started

Nothing on this list may be dropped silently. If something changes, it is noted here with the reason.

## Part 1 — Project foundation
- 🧪 Unity 6 project structure (`Assets/MallHorror/...`), assembly definitions
- 🧪 Automatic services bootstrap (settings, input, pause)
- 🧪 Input system built in code (keyboard/mouse + gamepad)
- 🧪 Settings (sensitivity, FOV, invert Y, crouch toggle, head bob, volume, vsync) with crash-safe saving
- 🧪 Physics layers created automatically
- 🧪 Dev pause/settings menu, dev info panel (F3), debug body view (F5)
- 🧪 Key rebinding for every gameplay action (keyboard/mouse and gamepad separately), saved, reset to defaults
- 🧪 One-click test scene builder
- ⬜ Renderer choice (URP vs HDRP) — waiting on GPU info, needed before Part 4

## Part 2 — Player movement & physics
- 🧪 Walking with smooth acceleration/deceleration
- 🧪 Sprinting: unlimited, no stamina bar, gradual build-up, momentum, slightly faster than creature
- 🧪 Sharp turns at speed reduce momentum slightly
- 🧪 Backwards movement: walk, run, crouch and crawl backwards (slower, shorter strides)
- 🧪 Crouching: real collider shrink, very quiet, can't stand up under low ceilings
- 🧪 Crouch and crawl on one button (tap = crouch/stand, hold = crawl/get up), optional separate crawl key
- 🧪 Crawling: torso + thigh + shin body laid along the crawled path; fits vents, follows bends without
  cutting corners, can't back legs-first into walls, can't turn round in narrow ducts
- 🧪 Smooth stance transitions (stand ↔ crouch ↔ crawl), never instant
- 🧪 Ground detection, slopes, too-steep slopes slide, stairs up and down, falls and landings
- 🧪 Collision: walls, doorways, narrow gaps, low beams, vent-size spaces, pillars
- 🧪 Pushing loose physics objects (light ones easily, heavier ones slowly)
- 🧪 Footsteps by stride, per floor material (procedural placeholder sounds)
- 🧪 Noise events for creature hearing (footsteps, landings, impacts)
- 🧪 Head bob synced to footsteps, landing dip, sprint FOV, crawl sway
- 🔨 Animation foundation: Animator parameters incl. backward motion (clips arrive Part 8/15)
- 🧪 Heavy objects: hold E to grab, push/pull/rotate slowly, loud scraping noise; extra players add speed (co-op ready)
- 🧪 Riding moving platforms (lift test platform) and moving walkways; platforms refuse to crush characters

## Part 3 — Mall blockout & layout
- ⬜ 3 shopping floors, central atrium (fountain, info desk, seating, escalators, clock, directory)
- ⬜ 4 shopping wings, looping/branching corridors, dead ends
- ⬜ Shops (enterable + realistic closed ones), restaurants, food court, cinema, toilets
- ⬜ Staff corridors, maintenance, storage, electrical, plant rooms, loading/delivery, management offices, restricted areas
- ⬜ Enormous single-level underground car park (no cars, pillars, ramps within level, lift lobbies, stairwells, service areas)
- ⬜ Rooftop access, service tunnel, lifts, stairs, emergency stairwells
- ⬜ Fixed physical mall directory matching the real layout (no minimap, no markers)
- ⬜ Fogged/black windows, no outside visible
- ⬜ Review Shots tool (fixed camera screenshots for Claude to inspect)

## Part 4 — Environment art & atmosphere
- ⬜ Realistic materials (AI-generated textures + normal/roughness maps)
- ⬜ Dense surface detail: floors (grout, chips, wear paths, scuffs, gum, drains, tape), walls (sockets, switches,
  alarms, extinguishers, exit signs, grilles, torn posters, tape marks, screw holes, trims, corner guards),
  ceilings (tile grid, sagging/stained/missing tiles, sprinklers, detectors, CCTV, speakers, vents),
  car park (oil, tyre marks, bay lines, cracks, puddles, rust drips, pillar numbers, height bars)
- ⬜ Rule-based, seeded detail placement (fixed every run)
- ⬜ Fluorescent, flickering, emergency lighting; shadows; light under doors; reflections
- ⬜ Post-processing: AO, grain, grading (early-2000s), exposure adaptation
- ⬜ Shop interiors, signage, furniture, early-2000s look, slightly neglected
- ⬜ Ambient sound (hum, ventilation, machinery, fountain)
- ⬜ LOD, occlusion culling, batching

## Part 5 — Interaction & physics
- ⬜ Look-at interaction prompt (small), hold-to-interact, range + line-of-sight checks
- ⬜ Doors (shop, staff, fire, storage, security, maintenance, lift, restricted) with weights, hinges, locks, faults, squeaks
- ⬜ Shutters (incl. jammed/obstructed), lockers, cupboards, drawers, wardrobes
- 🔨 Heavy object push/drag (built in Part 2; networking of co-op grips in Part 17)
- ⬜ Item pickup / drop / throw
- ⬜ Vent covers: remove with tools, **player can close vents again**
- ⬜ Machinery, switches, mechanisms

## Part 6 — Inventory & progression
- ⬜ One-item inventory, drop anywhere, items persist where left
- ⬜ Keys (shop, staff, storage, maintenance, security, plant room, restricted, exit keys), access cards
- ⬜ Tools (screwdriver, pliers, spanner, wrench, pry tool, utility tool, wire cutters)
- ⬜ Electrical components, puzzle components, special progression parts
- ⬜ Item state tracking compatible with creature stealing/moving items; always recoverable

## Part 7 — Puzzles & 8 escape routes
- ⬜ Puzzle framework (physical, logic, environmental), no timers, subtle physical feedback
- ⬜ Main entrance · Car park exit · Loading bay · Emergency exit · Rooftop · Service tunnel · Restricted staff exit · Hidden route
- ⬜ Shared early progression, distinct final sequences, alternative solutions, no softlocks

## Part 8 — Creature model & animation
- ⬜ Tall thin pale distorted humanoid, dark pupil-less eyes
- ⬜ Idle, still, look around, walk, fast walk, jerky moves, investigate, search, turn, chase start, run, lose sight
- ⬜ Inspect hiding places, open cupboards/lockers, bang on doors, object interactions, enter/leave areas
- ⬜ **Crawl animation for vents**
- ⬜ Root motion, foot placement, head/upper-body tracking, blending
- ⬜ Player character animations from Mixamo incl. **walk/run/crouch/crawl backwards**

## Part 9 — Creature AI
- ⬜ Wander (starts in car park, unaware), investigate, search, hunt — any order
- ⬜ Vision (FOV, distance, line of sight, light level; sees well in darkness)
- ⬜ Hearing via NoiseBus with uncertainty/echo confusion
- ⬜ Memory (sightings, hiding places, routes, noisy areas, items), decaying
- ⬜ Rare mistakes, hesitation, loss of interest; 1–2 s chase acceleration delay
- ⬜ **Closed doors / closed vents lower its belief you are behind them unless it saw/heard you**
- ⬜ **Can open closed vents and crawl through vents, but tends not to (especially closed ones); some vents too narrow**
- ⬜ Environmental interactions (lights, shelves, chairs, doors, shutters)

## Part 10 — Sound & audio intelligence
- ⬜ Propagation: walls, doors, room size, reverb, background masking
- ⬜ Real recorded footsteps/doors/object sounds replacing procedural placeholders
- ⬜ Creature footsteps from animation events, breathing, spatial audio
- ⬜ Distractions (coins, metal, delayed-noise items, alarms, ringing phones)

## Part 11 — Hiding, vents & catching
- ⬜ Hiding places with entry/exit animations (no hiding under desks)
- ⬜ Creature checks hiding places it saw you enter; can wait, leave, return
- ⬜ Vent network navigation
- ⬜ Catch detection (real body distances, host-authoritative later)
- ⬜ **Death sequence: creature picks player up, screams in their face, eats them — gore allowed**
- ⬜ **Context variants: corridor, chase, doorway, dark room, hiding place, vent (dragged out), etc.**
- ⬜ **First-person view and teammates' third-person view show the exact same sequence**
- ⬜ Full run reset on death (single-player)

## Part 12 — Electricity & machinery
- ⬜ Power grid, fuses, switches, rerouting; flicker/failure
- ⬜ Escalators (working/stopped/faulty), lifts (public/staff/service/restricted) with real doors and movement
- ⬜ Rare lift drop malfunction to car park

## Part 13 — Single-player completion
- ⬜ Opening sequence (11:30 PM, wake on atrium floor, vision clears)
- ⬜ Save/continue on quit (versioned, atomic), reset on death
- ⬜ All 8 escape sequences, daylight ending where something feels slightly wrong
- ⬜ Main menu (New, Continue, Settings, Host, Join)

## Parts 14–17 — LAN co-op
- ⬜ Host, LAN discovery, join/leave, disconnect handling, up to 4 players
- ⬜ Muted solid-colour featureless player figures, partial player collision
- ⬜ Smooth remote movement (interpolation, prediction, reconciliation) and animations
- ⬜ Proximity voice chat (distance, walls, doors), mute; creatures hear whisper/talk/shout/scream
- ⬜ Creature counts 1/2/2/3, independent AI and memory, player recognition
- ⬜ Personal death reset with contribution tracking; 5-second team escape rule; item sharing by dropping
- ⬜ Cooperative heavy-object moving

## Part 18 — Polish & final testing
- ⬜ Full test matrix (1–4 players, all routes, saves, disconnects, performance)
- ⬜ Windows, macOS and Linux builds
