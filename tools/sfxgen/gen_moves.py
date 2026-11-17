"""Sounds for the player's and Elliot's physical actions with doors and vents (own seed, so rendering these
never changes any other sound). Run: python3 gen_moves.py"""
import os
import numpy as np
from dsp import *
from gen_steps import thump, creak
from gen_mech import click, click_pair, door_body, rattle

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "AfterHours", "Resources", "Audio")


def wall_hit(rng, level=1.0):
    """A door flung open, crashing into the wall/doorstop: the leaf booms, the handle cracks into plaster."""
    x = silence(1.4)
    place(x, thump(rng, 52, 0.3, 1.5 * level), 0)
    place(x, door_body(rng, 1.4 * level, 1.15), 0)
    place(x, strike_object(rng, "steel", 0.4, 0.8, 0.5 * level, 0.25, damp=0.3), 0.004)     # the handle hits
    place(x, bp(pink(0.25, rng), 400, 3000) * env_exp(int(0.25 * SR), 0.02, 0.0003) * 0.6 * level, 0.002)  # plaster
    for k in range(2):
        place(x, door_body(rng, 0.35 * level, 1.0), 0.12 + k * rng.uniform(0.08, 0.14))     # bounce back, settle
    return fade(reverb(soften(x, 7000), 1.4, 0.8, 0.3, 0.3, rng), 0.0003, 0.25)


def door_barge(rng):
    """Running straight through a door: shoulder hits the leaf, the latch gives, it swings and smashes the wall."""
    x = silence(1.8)
    place(x, thump(rng, 70, 0.2, 1.4), 0)                       # shoulder / hands on the door
    place(x, door_body(rng, 1.1, 1.1), 0.003)
    place(x, click_pair(rng, 0.12, gap=0.012, level=1.0), 0.01)  # latch tongue forced out
    whoosh = bp(pink(0.3, rng), 150, 900) * np.sin(np.linspace(0, np.pi, int(0.3 * SR))) * 0.25
    place(x, whoosh.astype(np.float32), 0.04)
    place(x, wall_hit(rng, 0.95), rng.uniform(0.26, 0.34))
    return fade(soften(x, 7500), 0.0003, 0.2)


def locked_try(rng):
    """One firm try of a locked handle: lever pushed to its stop, the door shifts a millimetre and knocks."""
    x = silence(0.6)
    place(x, creak(0.12, rng, 0.12), 0)
    place(x, click_pair(rng, 0.08, gap=0.01, level=0.9), 0.07)
    place(x, door_body(rng, 0.7, 1.0), 0.08)
    place(x, click(rng, (1500, 6000), 0.003, 0.5), 0.26)           # lever springs back up
    return fade(reverb(soften(x, 7500), 0.6, 0.4, 0.14, 0.3, rng), 0.0005, 0.06)


def body_door(rng):
    """Running full tilt into a door that doesn't open: a dull body impact, the door rattling in its frame."""
    x = silence(1.4)
    place(x, thump(rng, 48, 0.4, 1.8), 0)
    place(x, door_body(rng, 1.3, 1.2), 0.002)
    place(x, rattle(rng) * 0.5, 0.03)
    place(x, lp(pink(0.25, rng), 500) * env_exp(int(0.25 * SR), 0.05, 0.001) * 0.7, 0)   # clothes, body
    return fade(reverb(soften(x, 6000), 1.2, 0.7, 0.25, 0.25, rng), 0.0005, 0.2)


def duct_boom(rng, level=1.0, size=1.6):
    """Thin sheet-metal duct struck from inside: a hollow, ringing boom."""
    x = strike_object(rng, "steel", size, 0.35, level, 1.4, damp=0.25)
    place(x, thump(rng, 60, 0.25, 0.9 * level), 0)
    return x


def grille_pull(rng):
    """Prising a vent grille off: the sheet flexes and pops, the last screw tears out, it comes free."""
    x = silence(1.6)
    t = 0.0
    for k in range(rng.integers(2, 4)):                           # oil-can pops as it flexes
        place(x, strike_object(rng, "steel", 0.7, 0.5, rng.uniform(0.25, 0.5), 0.3, damp=0.2), t)
        t += rng.uniform(0.12, 0.25)
    place(x, creak(0.35, rng, 0.35), 0.05)
    place(x, strike_object(rng, "steel", 0.15, 0.95, 0.6, 0.4, damp=0.3), t)       # the screw pings out
    place(x, strike_object(rng, "steel", 0.8, 0.6, 0.8, 0.7, damp=0.2), t + 0.05)  # free: the grille rings
    return fade(reverb(soften(x, 8000), 0.9, 0.5, 0.22, 0.35, rng), 0.0003, 0.2)


def grille_rip(rng):
    """Elliot tearing a grille out of the wall and flinging it: screeching metal, plaster, a clattering landing."""
    x = silence(2.2)
    n = int(0.45 * SR)
    tear = bp(rng.normal(size=n).astype(np.float32), 900, 5000) * np.linspace(1, 0.2, n) * 0.5
    place(x, tear * (0.6 + 0.4 * np.sin(np.linspace(0, 60, n))), 0)
    place(x, creak(0.4, rng, 0.8), 0)
    place(x, bp(pink(0.3, rng), 300, 2500) * env_exp(int(0.3 * SR), 0.05, 0.0005) * 0.8, 0.1)
    place(x, strike_object(rng, "steel", 0.9, 0.7, 1.0, 0.8, damp=0.2), 0.42)
    place(x, strike_object(rng, "steel", 0.9, 0.6, 0.6, 0.6, damp=0.2), 0.62)
    place(x, strike_object(rng, "steel", 0.9, 0.5, 0.3, 0.5, damp=0.2), 0.75)
    return fade(reverb(soften(x, 7500), 1.3, 0.7, 0.3, 0.3, rng), 0.0003, 0.3)


def vent_climb(rng):
    """Hauling yourself into (or out of) a duct: knees and elbows booming on the sheet metal, cloth scraping."""
    x = silence(1.6)
    t = 0.0
    for k in range(rng.integers(3, 5)):
        place(x, duct_boom(rng, rng.uniform(0.35, 0.7)), t)
        t += rng.uniform(0.2, 0.32)
    place(x, bp(pink(1.2, rng), 300, 3000) * np.sin(np.linspace(0, np.pi, int(1.2 * SR))) * 0.15, 0.05)
    return fade(soften(x, 7000), 0.0003, 0.2)


def vent_drop(rng):
    """Falling out of a vent opening: the duct lip clangs, a body hits the floor hard."""
    x = silence(1.6)
    place(x, duct_boom(rng, 0.8, 1.2), 0)
    place(x, thump(rng, 42, 0.45, 2.0), rng.uniform(0.32, 0.4))
    place(x, lp(pink(0.3, rng), 700) * env_exp(int(0.3 * SR), 0.06, 0.001) * 0.9, 0.36)
    return fade(reverb(soften(x, 6000), 1.0, 0.6, 0.25, 0.25, rng), 0.0003, 0.25)


def duct_buckle(rng):
    """The duct buckling under weight: a single deep oil-can BONG that rings down the metal."""
    x = duct_boom(rng, 1.0, rng.uniform(2.0, 2.6))
    return fade(reverb(soften(x, 5000), 1.8, 0.9, 0.35, 0.25, rng), 0.0003, 0.3)


def render(out):
    rng = np.random.default_rng(7171)
    for i in range(4):
        out(f"sfx/sfx_doorbarge_{i}", door_barge(rng))
        out(f"sfx/sfx_wallhit_{i}", wall_hit(rng))
        out(f"sfx/sfx_lockedtry_{i}", locked_try(rng))
        out(f"sfx/sfx_bodydoor_{i}", body_door(rng))
        out(f"sfx/sfx_grillepull_{i}", grille_pull(rng))
        out(f"sfx/sfx_ventclimb_{i}", vent_climb(rng))
        out(f"sfx/sfx_ventdrop_{i}", vent_drop(rng))
        out(f"sfx/sfx_ductbuckle_{i}", duct_buckle(rng))
    for i in range(3):
        out(f"sfx/sfx_grillerip_{i}", grille_rip(rng))


if __name__ == "__main__":
    n = [0]
    def out(name, x):
        x = soften(x, 11000)
        path = os.path.join(OUT, name + ".wav")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        write(path, x); n[0] += 1
    render(out)
    print("wrote", n[0], "sounds")
