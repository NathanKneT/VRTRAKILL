"""Writes the left-handed SteamVR bindings: every button and stick binding moves to the other hand.

Run from the repository root after changing a right-handed binding file:

    python tools/make-lefthanded-bindings.py

It reads ULTRAKILL_COPYTOROOT/ULTRAKILL_Data/StreamingAssets/SteamVR/actions.json and the bindings it lists, and writes
bindings_<controller>_lefthanded.json plus actions_lefthanded.json, whose default bindings point at them. The mod uses
that manifest when "Left handed" is on (Input/LeftHandedBindings.cs).

What moves: the "sources" (buttons, triggers, sticks, with their click/hold/double press modes) and "chords".
What stays: "poses", "skeleton" and "haptics", which follow the physical hand; the mod swaps the hands itself.

Buttons with a different name on each hand are swapped with the controller driver's own mirror table ("mirror_mappings"
in its input profile), copied below. When SteamVR and the PS VR2 app are installed, every written path is checked
against the installed input profiles.
"""
import copy
import json
import os
import sys

FOLDER = os.path.join("ULTRAKILL_COPYTOROOT", "ULTRAKILL_Data", "StreamingAssets", "SteamVR")

# From each driver's input profile, "mirror_mappings" (left component <-> right component). Controllers that use the
# same names on both hands have none.
MIRROR_NAMES = {
    # SteamVR/drivers/oculus/resources/input/touch_profile.json
    "oculus_touch": {"x": "a", "y": "b"},
    # PlayStation VR2 App/SteamVR_Plug-In/resources/input/playstation_vr2_sense_controller_profile.json
    "playstation_vr2_sense": {"l1": "r1", "l1_sensor": "r1_sensor", "l2": "r2", "l2_sensor": "r2_sensor",
                              "square": "cross", "triangle": "circle", "create": "options",
                              "left_stick": "right_stick", "left_ps": "right_ps"},
    "knuckles": {},
    "vive_controller": {},
    "holographic_controller": {},
}

STEAM = os.environ.get("STEAM_DIR", r"C:\Program Files (x86)\Steam")
PROFILES = {
    "oculus_touch": r"steamapps\common\SteamVR\drivers\oculus\resources\input\touch_profile.json",
    "knuckles": r"steamapps\common\SteamVR\drivers\indexcontroller\resources\input\index_controller_profile.json",
    "vive_controller": r"steamapps\common\SteamVR\drivers\htc\resources\input\vive_controller_profile.json",
    "playstation_vr2_sense": r"steamapps\common\PlayStation VR2 App\SteamVR_Plug-In\resources\input\playstation_vr2_sense_controller_profile.json",
}


def mirror_path(path, names):
    """/user/hand/left/input/x -> /user/hand/right/input/a, using the controller's mirror table."""
    parts = path.split("/")
    if len(parts) < 6 or parts[1] != "user" or parts[2] != "hand" or parts[3] not in ("left", "right"):
        raise ValueError("not a hand input path: " + path)
    side = parts[3]
    other = "right" if side == "left" else "left"
    component = parts[5]
    renamed = names.get(component) if side == "left" else {v: k for k, v in names.items()}.get(component)
    parts[3] = other
    parts[5] = renamed or component
    return "/".join(parts)


def mirror_binding(binding, names):
    out = copy.deepcopy(binding)
    for action_set in out["bindings"].values():
        for source in action_set.get("sources", []):
            source["path"] = mirror_path(source["path"], names)
        for chord in action_set.get("chords", []):
            for entry in chord.get("inputs", []):
                entry[0] = mirror_path(entry[0], names)
    out["name"] = "VRTRAKILL Left-handed"
    out["description"] = binding.get("description", "") + ", left-handed: buttons and sticks swapped between the hands"
    return out


def check_against_profile(controller, binding):
    """Every source path must name a component the controller has, on that hand. Returns the problems found."""
    relative = PROFILES.get(controller)
    if relative is None:
        return []
    path = os.path.join(STEAM, relative)
    if not os.path.exists(path):
        print(f"  (no installed profile for {controller}, not checked)")
        return []
    with open(path, encoding="utf-8") as f:
        sources = json.load(f)["input_source"]
    problems = []
    for action_set in binding["bindings"].values():
        for source in action_set.get("sources", []):
            parts = source["path"].split("/")
            component = "/" + "/".join(parts[4:6])
            info = sources.get(component)
            if info is None:
                problems.append(f"{controller}: {source['path']} has no component {component}")
            elif info.get("side") not in (None, parts[3]):
                problems.append(f"{controller}: {source['path']} is on the {info.get('side')} hand only")
    return problems


def bound_actions(binding):
    """(action, input mode) pairs, to check that mirroring kept every binding."""
    found = []
    for action_set in binding["bindings"].values():
        for source in action_set.get("sources", []):
            for mode, entry in source.get("inputs", {}).items():
                found.append((entry["output"], source["mode"], mode))
    return sorted(found)


def main():
    with open(os.path.join(FOLDER, "actions.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    problems = []
    lefthanded = copy.deepcopy(manifest)
    for entry in lefthanded["default_bindings"]:
        controller = entry["controller_type"]
        if controller not in MIRROR_NAMES:
            continue  # headset buttons and unused placeholder files stay as they are
        with open(os.path.join(FOLDER, entry["binding_url"]), encoding="utf-8") as f:
            right = json.load(f)
        left = mirror_binding(right, MIRROR_NAMES[controller])
        if bound_actions(left) != bound_actions(right):
            problems.append(f"{controller}: the mirrored file does not bind the same actions")
        problems += check_against_profile(controller, right)
        problems += check_against_profile(controller, left)
        name = entry["binding_url"].replace(".json", "_lefthanded.json")
        with open(os.path.join(FOLDER, name), "w", encoding="utf-8", newline="\n") as f:
            json.dump(left, f, indent=2)
            f.write("\n")
        entry["binding_url"] = name
        print(f"{controller}: {name}")
    with open(os.path.join(FOLDER, "actions_lefthanded.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(lefthanded, f, indent=2)
        f.write("\n")
    for p in problems:
        print("PROBLEM: " + p)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
