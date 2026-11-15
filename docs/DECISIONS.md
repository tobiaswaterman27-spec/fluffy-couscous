# Technical decisions

## Engine: Unity 6 (any 6.x), built for Windows, macOS, Linux
- Best realistic indoor lighting available to a small team, mature physics (PhysX), navigation meshes,
  animation retargeting (Mixamo humanoid animations), and an official LAN-capable networking library.
- The editor runs on the developer's Linux PC and can produce Windows, macOS and Linux builds.
- Chromebook / browser play is **not** a target (agreed). Code is kept platform-neutral in case.

## Renderer: decided before Part 4
- Parts 1–3 are renderer-independent (editor tools pick URP, HDRP or built-in shaders automatically).
- HDRP if the PC has a capable dedicated GPU; otherwise URP with baked lighting, SSAO, decals and post-processing.
  Pending: output of `lspci | grep -i vga` from the developer's PC.

## How visual quality is achieved without a person modelling everything
- Lighting/post-processing configured by editor scripts (IES-shaped fluorescents, AO, reflections, baked GI,
  grain, grading, auto-exposure).
- Geometry built procedurally with bevels, trim and rule-based dense detail (decals for stains, cracks, scuffs,
  gum, tape marks; fixtures such as sprinklers, sockets, extinguishers, CCTV, vents, cable trays). Seeded, so the
  mall is identical every run.
- Textures generated with AI image tools, converted to normal/roughness maps by script.
- **Review Shots tool** (Part 3/4): renders ~20 fixed camera views to `ReviewShots/`; the developer pushes them and
  Claude inspects them to tune visuals.
- Human animations from Mixamo (free). Creature = distorted humanoid rig + procedural layers. Death/eat sequence
  authored in code so first-person and third-person views are driven by the same timeline.

## Player body and movement
- **CharacterController** (swept capsule): cannot pass through colliders, handles steps/slopes, no tunnelling.
- Stance changes resize the real capsule over time and only grow when an overlap test proves there is room
  ("NO ROOM" in the F3 panel). You cannot stand up in a vent or under a low ceiling.
- **Crawling body = torso capsule + legs capsule.** A vertical capsule cannot represent a 1.4 m long crawling body,
  so the legs are a second capsule trailing behind, dragged like a towed trailer. Effects:
  legs follow ducts round bends; you cannot back legs-first into a wall; you cannot spin round in a duct
  (must back out or reach a junction); the head can still turn ±75° to look around.
  Known simplification: legs may slightly cut an inside corner while being dragged forward (they never block or
  push the player, so it cannot cause clipping of the player body).
- Momentum: gradual sprint build-up, sharp turns at speed cost up to ~22% speed per 90°, reversing brakes.
  Sprint 6.2 m/s; creature will be ~5.9 m/s.
- Backwards movement is a first-class direction: slower walk-back, backward run (3.4 m/s), backward crouch and
  crawl, with shorter strides. Animator parameter `MoveZ` is negative when moving backwards.
- Footsteps are triggered by distance travelled (stride length), so audio, camera bob, creature-audible noise
  and (later) animation foot contacts stay in sync.

## Architecture for co-op from day one
- Movement reads a `PlayerInputFrame` struct, never the keyboard. Local players fill it from devices; remote
  players will fill it from the network. Same movement code everywhere.
- Gameplay noise is a separate `NoiseBus` event stream (position + audible radius + kind) from the audio you hear.
  Creatures will listen to this; Part 10 adds occlusion/echo/masking; Part 15 adds voice chat volume.
- Networking plan (Part 14): Netcode for GameObjects + Unity Transport (direct UDP, no relay/internet needed),
  custom UDP-broadcast LAN discovery, host-authoritative world/creatures/catches, client-side prediction with
  reconciliation for the local player, interpolation for remote players and creatures.
- Voice chat plan (Part 15): microphone capture, compressed audio over the LAN connection, played back as 3D sources
  with distance + wall/door occlusion filtering; voice loudness also emitted as NoiseBus events.

## Content decisions from the developer
- Gore is allowed and wanted. Jumpscares should be genuinely terrifying.
- Death: creature picks the player up, screams at them, then eats them. Variants by context (corridor, hiding
  place, vent, doorway, dark room, mid-chase). Seen in first person by the victim; teammates see the identical
  sequence in third person.
- Saves: atomic write (temp file, then swap, previous kept as .bak). Used for settings now, save games in Part 13.
