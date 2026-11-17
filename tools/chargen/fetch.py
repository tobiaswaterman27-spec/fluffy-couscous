"""Downloads the MakeHuman data the character builder uses (CC0, from the MakeHuman 1.x repository) into ./mhdata.

    python3 fetch.py
"""
import os, json, urllib.request
from concurrent.futures import ThreadPoolExecutor

BASE = "https://raw.githubusercontent.com/makehumancommunity/makehuman/master/makehuman/data/"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "mhdata")


def get(rel):
    dst = os.path.join(OUT, rel)
    if os.path.exists(dst):
        return
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    try:
        urllib.request.urlretrieve(BASE + rel, dst)
    except Exception:
        pass   # some combinations (e.g. the neutral breast target) don't exist upstream


def main():
    for f in ("3dobjs/base.obj", "rigs/default.mhskel", "rigs/default_weights.mhw", "modifiers/modeling_modifiers.json"):
        get(f)
    for a, b in (("3dobjs/base.obj", "base.obj"), ("rigs/default.mhskel", "default.mhskel"), ("rigs/default_weights.mhw", "default_weights.mhw"),
                 ("modifiers/modeling_modifiers.json", "modeling_modifiers.json")):
        if not os.path.exists(os.path.join(OUT, b)):
            os.replace(os.path.join(OUT, a), os.path.join(OUT, b))
    d = json.load(open(os.path.join(OUT, "modeling_modifiers.json")))
    paths = set()
    for g in d:
        grp = g["group"]
        if grp.startswith("macrodetails") or grp in ("genitals", "breast"):
            continue
        for m in g["modifiers"]:
            t = m.get("target")
            if t:
                paths.update([f"{grp}/{t}-{m['min']}", f"{grp}/{t}-{m['max']}"] if "min" in m else [f"{grp}/{t}"])
    for gen in ("female", "male"):
        for age in ("young", "old"):
            for mu in ("minmuscle", "averagemuscle", "maxmuscle"):
                for we in ("minweight", "averageweight", "maxweight"):
                    paths.add(f"macrodetails/universal-{gen}-{age}-{mu}-{we}")
                    for h in ("minheight", "maxheight"):
                        paths.add(f"macrodetails/height/{gen}-{age}-{mu}-{we}-{h}")
                    if gen == "female":
                        for cup in ("mincup", "maxcup"):
                            paths.add(f"breast/female-{age}-{mu}-{we}-{cup}-averagefirmness")
            for race in ("african", "asian", "caucasian"):
                paths.add(f"macrodetails/{race}-{gen}-{age}")
    with ThreadPoolExecutor(16) as ex:
        list(ex.map(lambda p: get("targets/" + p + ".target"), sorted(paths)))
    print("MakeHuman data ready in", OUT)


if __name__ == "__main__":
    main()
