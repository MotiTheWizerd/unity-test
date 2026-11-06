"""Fetches a Forge material pack's textures from ambientCG (CC0).

Usage: python Tools/fetch_material_pack.py Assets/_Project/Content/MaterialPacks/<Pack> [resolution]

Reads the pack's pack.json and, for every material with a "source" (an ambientCG asset id), downloads that asset's
JPG set (1K unless a resolution such as 2K is given) into <Pack>/Materials/<folder>/, renamed to Forge's naming
({folder}_Color.jpg, _NormalGL.jpg, _Roughness.jpg, _AmbientOcclusion.jpg, _Metalness.jpg). Folders that already
hold a colour map are skipped, so it can be run again after adding materials. Then run Forge → Materials →
Build Material Packs in Unity.
"""
import io
import json
import os
import sys
import urllib.request
import zipfile

MAPS = ("Color", "NormalGL", "Roughness", "AmbientOcclusion", "Metalness")
URL = "https://ambientcg.com/get?file={source}_{resolution}-JPG.zip"


def fetch(source, folder, destination, resolution):
    request = urllib.request.Request(URL.format(source=source, resolution=resolution), headers={"User-Agent": "Forge"})
    data = urllib.request.urlopen(request, timeout=120).read()
    os.makedirs(destination, exist_ok=True)
    kept = []
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for entry in archive.namelist():
            stem, extension = os.path.splitext(entry)
            if extension.lower() != ".jpg":
                continue
            kind = stem.rsplit("_", 1)[-1]
            if kind in MAPS:
                with open(os.path.join(destination, f"{folder}_{kind}.jpg"), "wb") as out:
                    out.write(archive.read(entry))
                kept.append(kind)
    return kept


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    pack = sys.argv[1]
    resolution = sys.argv[2] if len(sys.argv) > 2 else "1K"
    with open(os.path.join(pack, "pack.json"), encoding="utf-8") as file:
        manifest = json.load(file)

    for material in manifest.get("materials", []):
        folder, source = material["folder"], material.get("source")
        destination = os.path.join(pack, "Materials", folder)
        if not source:
            print(f"{folder}: no source, skipped")
            continue
        if os.path.exists(os.path.join(destination, f"{folder}_Color.jpg")):
            print(f"{folder}: already there")
            continue
        print(f"{folder}: {source} {', '.join(fetch(source, folder, destination, resolution))}")


if __name__ == "__main__":
    main()
