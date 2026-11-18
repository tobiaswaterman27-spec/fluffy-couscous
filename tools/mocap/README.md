# Player motion capture

The player characters walk, run, crouch-walk and stand with real recorded human motion from the
**CMU Graphics Lab Motion Capture Database** (http://mocap.cs.cmu.edu/ - "free for all uses"; the database was
created with funding from NSF EIA-0196217). BVH conversion by Bruce Hahne (cgspeed), mirrored at
github.com/una-dinosauria/cmu-mocap.

`python3 build_mocap.py` downloads the clips it uses into `bvh/` (not committed) and writes
`Assets/AfterHours/Resources/Player/mocap.bytes`:

| Name | CMU clip | What |
|---|---|---|
| walk | 08_01 | brisk walk (one gait cycle) |
| walk_slow | 16_15 | unhurried walk |
| jog | 02_03 | run/jog |
| run | 16_45 | run |
| crouch | 77_32 | creeping walk, low |
| crouch_back | 136_11 | walking backwards, crouched |
| idle | 137_28 | standing, waiting (kept almost still: only ~10% of the captured sway) |
| idle_scared | 142_16 | scared, standing (18 s loop) |
| crawl | 111_03 | crawling on hands and knees |

Each pose is stored without anyone's proportions (segment directions and twists in the character's own space,
distances in leg lengths), so `PlayerBodyAnimator` rebuilds it on each character's own bones, pins the feet to the
real floor, warps the stride to the actual speed and keeps the head where the camera is.
