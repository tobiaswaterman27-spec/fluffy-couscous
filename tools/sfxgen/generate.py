"""Renders every AFTER HOURS sound into Assets/AfterHours/Resources/Audio (run: python3 generate.py)."""
import os
import sys
import time
import numpy as np
from dsp import write
import gen_steps, gen_mech, gen_body, gen_world

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "AfterHours", "Resources", "Audio")


def main(only=None):
    rng = np.random.default_rng(20031130)
    count = [0]

    def out(name, x):
        from dsp import soften
        x = soften(x, 11000)  # nothing in the game needs more air than this; it reads as hiss
        path = os.path.join(OUT, name + ".wav")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        write(path, x)
        count[0] += 1

    for mod in (gen_steps, gen_mech, gen_body, gen_world):
        if only and mod.__name__ not in only:
            continue
        t0 = time.time()
        mod.render(out, rng)
        print(f"{mod.__name__}: done in {time.time() - t0:.1f}s ({count[0]} files so far)")


if __name__ == "__main__":
    main(sys.argv[1:] or None)
