# AFTER HOURS — development checklist

Status key: ✅ built and confirmed working in Unity · 🧪 built, waiting for in-editor test · 🔨 foundation only ·
⬜ not started

Nothing on this list may be dropped silently. If something changes, it is noted here with the reason.

## Part 1 — Project foundation
- 🧪 Unity 6 project structure (`Assets/AfterHours/...`), assembly definitions
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
- 🧪 Hold controls: double-tap + hold a move key = sprint, hold Shift = crouch, hold Ctrl = crawl;
  releasing without room keeps you low; crawling is slow (0.6 m/s) and very quiet; reduced crawl camera sway
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
- 🧪 Fixed master plan in code (`MallLayout`): no randomness, identical every run
- 🧪 3 shopping floors (G, 1, 2) at 6 m floor-to-floor, 4.5 m ceilings with service voids above (for vents)
- 🧪 Central atrium: 20 x 20 m void through all floors, fountain, information desk, seating, planters,
  hanging clock (11:30), glass balustrades, escalators G-1 (east) and 1-2 (west), L2 bridge, public lifts, skylight
- 🧪 4 wings x 3 floors: 12 m concourses, ~190 shops/units with fixed names and categories, signage,
  benches/planters/bins, lights
- 🧪 Shop fronts: closed shutters, jammed shutters (crawl under), locked glass, open shutters, broken-lock doors;
  enterable shops have fittings, counters, stock rooms and back doors
- 🧪 Staff passages (and public toilet passages) through to service corridors behind every shop row → loops
- 🧪 Anchor stores: department store (G+1, internal staircase), supermarket, main entrance hall, fire-exit hall
  with toilets + sports store, electronics, home store, toy store, bowling/arcade, cinema (3 screens), gym, food court
- 🧪 Back-of-house blocks: loading bay (dock shutters), security, staff room, toilets, electrical, maintenance,
  plant rooms, mall management (restricted), records, storage, staff canteen, water tanks
- 🧪 4 switchback stairwells car park → L2 (north-east one continues to the roof)
- 🧪 Lift shafts (2 public, 1 service) with closed landing doors on every level (cars in Part 12)
- 🧪 Enormous single-level car park (200 x 200 m): pillar grid with bay labels, split-level raised section with
  ramps and crash barriers, lift lobby, maintenance + plant rooms, fenced delivery bay, dead/dim light areas, no cars
- 🧪 Car park exit ramp tunnel with barrier and shutter, service tunnel (80 m), rooftop area with fencing
- 🧪 All 8 escape route exit points placed and marked (puzzles in Part 7)
- 🧪 Every doorway marked with a DoorSlot (kind, lock state) for Part 5 to turn into real doors
- 🧪 Physical directory board generated from the same data (4 floor plans, store lists, symbols, you-are-here)
- 🧪 Windows to outside: none visible; entrance glass is black
- 🧪 Review Shots tool (20 fixed views → PNG) and F12 in-game screenshots
- 🧪 Dev teleport menu (Esc > Teleport) for testing the large building
- ⬜ Vent network inside the ceiling voids → Part 5 (covers) / Part 11 (crawl network)

## Part 4 — Environment art & atmosphere
- 🧪 Procedural PBR textures (colour + normal + metal/AO/smoothness): porcelain tiles, terrazzo, ceiling tiles,
  painted plaster, blockwork, concrete, car park slab, vinyl, carpet, brushed metal, shutters, wood, wall tiles,
  painted metal, laminate, dark panels (`tools/texgen/generate.py`, re-runnable)
- 🧪 Decals: stains, scuffs, gum, cracks, oil, tyre marks, puddles, ceiling water damage, wall grime, damp, tape marks
- 🧪 Real-world texture scale on every surface (world-space box mapping in the mesh combiner)
- 🧪 Floor finishes by zone (terrazzo atrium, porcelain concourses, vinyl service areas, tiled toilets, carpet cinema)
  and by shop type (carpet / wood / vinyl)
- 🧪 Ceilings: fluorescent panels (working / dead / flickering with buzz), sprinklers, smoke detectors, speakers,
  vents, CCTV domes, water stains, missing, hanging and fallen tiles
- 🧪 Walls: skirting, grime, sockets, switches, extinguishers + signs, call points, crash rails, damp, toilet tiling,
  car park bands/stripes, glowing advert panels, tape marks where posters were
- 🧪 Floors: thousands of stains, scuffs, gum, cracks, puddles
- 🧪 Door frames on every doorway, green EXIT signs at stairs and fire exits
- 🧪 Concourses: plants, vending machines (glowing), payphones, ATMs, advert pillars, hanging direction signs,
  kiddie ride, abandoned wet-floor signs
- 🧪 Shops: tills, mannequins, TVs, menu boards, coffee machines, security gates
- 🧪 Toilets: WCs and cisterns, mirrors, hand dryers
- 🧪 Car park: painted bays and arrows, wheel stops, pillar hazard bands, drain pipes, sprinkler mains, jet fans,
  light battens (dead / flickering), oil stains, tyre marks, puddles with drains, cracks, signs
- 🧪 Service corridors: pipes and cable trays; stairwell handrails
- 🧪 Post-processing look: ACES, desaturated slightly green early-2000s grade, bloom, vignette, film grain, slight CA
- 🧪 Eye adaptation (light-probe based) for dark/bright transitions
- 🧪 Baked lighting pipeline: lightmap settings, ~14k light probes, ~70 box-projected reflection probes,
  "5 - Bake Lighting" (+ occlusion culling) and "6 - Undo Baked Lighting"
- 🧪 Ambient sound: fluorescent hum, ventilation, plant machinery, car park fans, fountain water; reverb zones per space
- 🧪 Performance: tens of thousands of objects merged into a few thousand meshes; colliders preserved
- ⬜ Real recorded audio (Part 10), TextMeshPro signage and higher-quality fonts (polish)

## Part 5 — Interaction & physics
- 🧪 Look-at interaction prompt (small), hold-to-interact, range + line-of-sight checks
- 🧪 Doors (shop, staff, fire, storage, security, maintenance, lift, restricted) with weights, hinges, locks, faults, squeaks
- 🧪 Shutters (incl. jammed/obstructed), lockers, cupboards, drawers, wardrobes
- 🔨 Heavy object push/drag (built in Part 2; networking of co-op grips in Part 17)
- 🧪 Item pickup / drop / throw
- 🧪 Vent covers: remove with tools, **player can close vents again**
- 🧪 Machinery, switches, mechanisms

## Part 6 — Inventory & progression
- 🧪 One-item inventory, drop anywhere, items persist where left
- 🧪 Keys (shop, staff, storage, maintenance, security, plant room, restricted, exit keys), access cards
- 🧪 Tools (screwdriver, pliers, spanner, wrench, pry tool, utility tool, wire cutters)
- 🧪 Electrical components, puzzle components, special progression parts
- 🔨 Item state tracking (items reset to where they started; creature moving items not built yet)

## Part 7 — Puzzles & 8 escape routes
- 🧪 Puzzle framework (physical, logic, environmental), no timers, subtle physical feedback
- 🧪 Main entrance · Car park exit · Loading bay · Emergency exit · Rooftop · Service tunnel · Restricted staff exit · Hidden route
- 🧪 Shared early progression, distinct final sequences, alternative solutions, no softlocks

## Part 8 — Creature (Elliot) model & animation
- 🧪 Tall thin pale distorted humanoid, dark pupil-less eyes
- 🧪 Idle, still, look around, walk, fast walk, jerky moves, investigate, search, turn, chase start, run, lose sight
- 🧪 Inspect hiding places, open cupboards/lockers, bang on doors, object interactions, enter/leave areas
- 🧪 **Crawl animation for vents**
- 🧪 Root motion, foot placement, head/upper-body tracking, blending
- 🔨 Player character animations incl. **walk/run/crouch/crawl backwards** (procedural body; Mixamo set optional later)

## Part 9 — Creature (Elliot) AI
- 🧪 Wander (starts in car park, unaware), investigate, search, hunt — any order
- 🧪 Vision (FOV, distance, line of sight, light level; sees well in darkness)
- 🧪 Hearing via NoiseBus with uncertainty/echo confusion
- 🧪 Memory (sightings, hiding places, routes, noisy areas, items), decaying
- 🧪 Rare mistakes, hesitation, loss of interest; 1–2 s chase acceleration delay
- 🧪 **Closed doors / closed vents lower its belief you are behind them unless it saw/heard you**
- 🧪 **Can open closed vents and crawl through vents, but tends not to (especially closed ones); some vents too narrow**
- 🔨 Environmental interactions (doors, shutters, vent covers, hiding places done; knocking shelves/chairs later)

## Part 10 — Sound & audio intelligence
- 🧪 Propagation: walls, doors, room size, reverb, background masking
- 🔨 Real recorded footsteps/doors/object sounds replacing procedural placeholders (procedural sounds in place; recorded set can drop in later)
- 🧪 Creature footsteps from animation events, breathing, spatial audio
- 🧪 Distractions (coins, metal, delayed-noise items, alarms incl. lift alarm bell)

## Part 11 — Hiding, vents & catching
- 🧪 Hiding places (lockers, cupboards) with smooth entry/exit, limited look while hidden (no hiding under desks)
- 🧪 Creature checks hiding places it saw you enter; can wait, leave, return
- 🧪 Vent ducts on every shop floor beside the service corridors; ground-floor ducts fit Elliot, upper ones do not
- 🧪 Catch detection (real body distances, host-authoritative later)
- 🧪 **Death sequence: creature picks player up, screams in their face, eats them — gore allowed**
- 🧪 **Context variants: corridor, chase, doorway, dark room, hiding place, vent (dragged out), etc.**
- 🧪 **First-person view and teammates' third-person view show the exact same sequence**
- 🧪 Full run reset on death (single-player)

## Part 12 — Electricity & machinery
- 🧪 Power grid: main switchboard (car park plant room) with breakers per circuit; areas go dark with emergency lights; flicker on failure
- 🧪 Escalators (working/stopped/faulty, emergency stop) and lifts (2 public + service) with real doors, buttons, indicators, alarm
- 🧪 Rare lift drop malfunction to car park (only with someone inside; doors jam part-open; Elliot hears it)
- 🧪 Random malfunctions every few minutes: breaker trips, escalator faults, lift supply cuts

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
