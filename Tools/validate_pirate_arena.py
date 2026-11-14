"""Regression checks for the scene wiring and materials that previously broke arena startup.
Run from the Unity project root with Python 3. Uses only the standard library.
"""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]
arena = root / "Assets/003_Scripts/PirateArena"

def guid(path):
    return re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text(encoding="utf-8-sig"), re.M)[1]

def refs(text):
    return {name: (int(file_id), asset_guid) for name, file_id, asset_guid in re.findall(
        r"^  (\w+): \{fileID: (\d+), guid: (\w+), type: [23]\}", text, re.M)}

model_names = {
    "shipLarge": "Ship_Large", "sharkModel": "Characters_Shark", "cannonModel": "Prop_Cannon",
    "barrelModel": "Prop_Barrel", "bucketModel": "Prop_Bucket_Fishes", "anneModel": "Characters_Anne",
    "captainModel": "Characters_Captain_Barbarossa", "henryModel": "Characters_Henry",
    "makoModel": "Characters_Mako", "skeletonModel": "Characters_Skeleton",
    "rockModel": "Environment_Rock_1", "palmModel": "Environment_PalmTree_1",
    "chestModel": "Prop_Chest_Closed", "anchorModel": "Prop_Anchor"
}
material_names = {
    "atlas": "Pirate_Atlas_URP", "wood": "Deck_WarmWood", "darkWood": "Hull_DarkWood",
    "brass": "Brass_Trim", "rope": "Rope_Sand", "cloth": "Lantern_Cream",
    "water": "Ocean_Turquoise", "foam": "Foam_Ivory", "warning": "Cannon_RedZone", "iron": "Iron_Black"
}
for scene in ["SampleScene", "ArenaDelCaos"]:
    path = root / ("Assets/001_Scenes/" + scene + ".unity")
    text = path.read_text(encoding="utf-8-sig")
    bootstrap = text.split("--- !u!114 &605000000", 1)[1].split("--- !u!", 1)[0]
    linked = refs(bootstrap)
    assert linked["m_Script"] == (11500000, guid(arena / "PirateArenaBootstrap.cs"))
    for field, name in model_names.items():
        assert linked[field] == (919132149155446097, guid(root / ("Assets/002_Modelos/piratas/FBX/" + name + ".fbx"))), field
    for field, name in material_names.items():
        assert linked[field] == (2100000, guid(arena / ("Materials/" + name + ".mat"))), field
    assert linked["settings"] == (11400000, guid(arena / "Settings/ArenaBalance.asset"))
    for field, name in [("fighterGrip", "Fighter_Grip"), ("propGrip", "Props_Grip")]:
        assert linked[field] == (13400000, guid(arena / ("Materials/" + name + ".physicMaterial")))
    assert "- component: {fileID: 605000000}" in text
    print(scene + ": bootstrap, 14 model roots, 10 materials, balance and physics materials OK")
atlas = (arena / "Materials/Pirate_Atlas_URP.mat").read_text()
texture_guid = guid(root / "Assets/002_Modelos/piratas/Atlas_Pirate.png")
assert re.search(r"_BaseMap:\s+m_Texture: \{fileID: 2800000, guid: " + texture_guid, atlas)
assert "933532a4fcc9baf4fa0491de14d08ed7" in atlas
warning = (arena / "Materials/Cannon_RedZone.mat").read_text()
for value in ["_SURFACE_TYPE_TRANSPARENT", "_SrcBlend: 5", "_DstBlend: 10", "_ZWrite: 0"]:
    assert value in warning
settings = (arena / "Settings/ArenaBalance.asset").read_text()
assert guid(arena / "PirateArenaSettings.cs") in settings
ship_length = float(re.search(r"^  shipLength: ([\d.]+)", settings, re.M)[1])
assert 32 <= ship_length <= 56, "Ship length is outside the supported tuning range"
print("Atlas texture, URP shader, warning transparency and settings references OK")
