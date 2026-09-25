#!/usr/bin/env python3
"""Analyze, delete unused Unity assets, and reorganize kept assets."""
from __future__ import annotations

import json
import os
import re
import shutil
from collections import Counter, deque
from pathlib import Path, PurePosixPath

ROOT = Path(r"D:\Github\Tuyoo-group-4\Tuyoo group 4")
ASSETS = ROOT / "Assets"

BUILD_SCENES = [
    "Scenes/MainMenuScene.unity",
    "Scenes/IntroVideo.unity",
    "Scenes/SampleScene.unity",
    "Scenes/level2.unity",
    "Scenes/Real level2.unity",
    "Scenes/Level3.unity",
    "Scenes/Level4.unity",
]

ALWAYS_KEEP_PREFIXES = [
    "Settings/",
    "Scripts/",
    "TextMesh Pro/",
    "Resources/",
]

ALWAYS_KEEP_FILES = {
    "InputSystem_Actions.inputactions",
    "Readme.asset",
}

REF_RE = re.compile(r"guid:\s*([0-9a-f]{32})", re.I)


def to_posix(rel: str) -> str:
    return str(PurePosixPath(rel.replace("\\", "/")))


def build_guid_map() -> dict[str, str]:
    guid_to_path: dict[str, str] = {}
    for meta_path in ASSETS.rglob("*.meta"):
        asset_path = meta_path.with_suffix("")
        try:
            text = meta_path.read_text(encoding="utf-8", errors="ignore")[:4096]
        except OSError:
            continue
        match = REF_RE.search(text)
        if not match:
            continue
        rel = to_posix(str(asset_path.relative_to(ASSETS)))
        guid_to_path[match.group(1)] = rel
    return guid_to_path


def refs_in_file(path: Path) -> list[str]:
    try:
        return REF_RE.findall(path.read_text(encoding="utf-8", errors="ignore"))
    except OSError:
        return []


def analyze() -> tuple[set[str], set[str]]:
    guid_to_path = build_guid_map()
    entry_paths: set[str] = set()

    for scene in BUILD_SCENES:
        if (ASSETS / scene.replace("/", os.sep)).exists():
            entry_paths.add(scene)

    for rel in guid_to_path.values():
        for prefix in ALWAYS_KEEP_PREFIXES:
            if rel.startswith(prefix):
                entry_paths.add(rel)
        if Path(rel).name in ALWAYS_KEEP_FILES:
            entry_paths.add(rel)

    used: set[str] = set(entry_paths)
    queue: deque[str] = deque(entry_paths)

    while queue:
        rel = queue.popleft()
        full = ASSETS / rel.replace("/", os.sep)
        if not full.exists():
            continue
        for guid in refs_in_file(full):
            dep = guid_to_path.get(guid)
            if dep and dep not in used:
                used.add(dep)
                queue.append(dep)

    all_assets = set(guid_to_path.values())
    unused = all_assets - used
    return used, unused


def delete_unused(unused: set[str]) -> int:
    deleted = 0
    for rel in sorted(unused, key=len, reverse=True):
        asset = ASSETS / rel.replace("/", os.sep)
        meta = Path(str(asset) + ".meta")
        for path in (asset, meta):
            if path.exists():
                path.unlink()
                deleted += 1
    return deleted


def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def move_asset(rel: str, dest_rel: str, moves: list[tuple[str, str]]) -> None:
    src = ASSETS / rel.replace("/", os.sep)
    dst = ASSETS / dest_rel.replace("/", os.sep)
    if not src.exists():
        return
    if src.resolve() == dst.resolve():
        return
    ensure_dir(dst.parent)
    if dst.exists():
        return
    shutil.move(str(src), str(dst))
    src_meta = Path(str(src) + ".meta")
    dst_meta = Path(str(dst) + ".meta")
    if src_meta.exists():
        shutil.move(str(src_meta), str(dst_meta))
    moves.append((rel, dest_rel))


def categorize(rel: str) -> str | None:
    p = PurePosixPath(rel)
    name = p.name
    suffix = p.suffix.lower()
    parts = p.parts

    if rel.startswith("Scenes/"):
        return f"Game/Scenes/{name}"
    if rel.startswith("Scripts/"):
        return f"Game/Scripts/{name}"
    if rel.startswith("Prefabs/"):
        return f"Game/Prefabs/{name}"
    if rel.startswith("Settings/"):
        return f"Game/Settings/{'/'.join(parts[1:])}" if len(parts) > 1 else f"Game/Settings/{name}"
    if rel.startswith("TextMesh Pro/"):
        return None  # keep vendor layout
    if rel.startswith("Resources/"):
        return f"Game/Resources/{'/'.join(parts[1:])}" if len(parts) > 1 else f"Game/Resources/{name}"

    if suffix in {".mp3", ".wav", ".ogg"}:
        return f"Game/Audio/{name}"
    if suffix in {".mp4", ".mov"}:
        return f"Game/Video/{name}"
    if suffix == ".renderTexture".lower() or name.endswith(".renderTexture"):
        return f"Game/RenderTextures/{name}"
    if suffix == ".shader" or suffix == ".shadergraph":
        return f"Game/Shaders/{name}"
    if suffix == ".mat":
        if "mirror" in rel.lower():
            return f"Game/Materials/Mirrors/{name}"
        return f"Game/Materials/{name}"
    if suffix in {".png", ".jpg", ".jpeg", ".tga", ".tif", ".psd", ".exr", ".hdr"}:
        if "skybox" in rel.lower() or "Skybox" in rel:
            return f"Game/Art/Skyboxes/{name}"
        return f"Game/Art/Textures/{name}"
    if suffix in {".fbx", ".glb", ".obj"}:
        return f"Game/Art/Models/{name}"
    if suffix in {".anim", ".controller"}:
        return f"Game/Art/Animation/{name}"
    if suffix == ".prefab" and not rel.startswith("Game/"):
        return f"Game/Prefabs/Vendor/{'/'.join(parts)}"
    if suffix == ".cs" and not rel.startswith("Game/Scripts/"):
        return f"Game/Scripts/Vendor/{'/'.join(parts)}"
    if suffix == ".inputactions":
        return f"Game/Input/{name}"
    if suffix == ".asset" and name == "Readme.asset":
        return f"Game/Documentation/{name}"

    if rel.startswith("UI Soundpack/"):
        return f"Game/Audio/UI/{'/'.join(parts[1:])}"
    if rel.startswith("GhostCharacter_Free/"):
        return f"Game/Art/Characters/Ghost/{'/'.join(parts[1:])}"
    if rel.startswith("PlayerModel/"):
        return f"Game/Art/Characters/Player/{'/'.join(parts[1:])}"
    if rel.startswith("SimpleNaturePack/"):
        return f"Game/Art/Environment/SimpleNaturePack/{'/'.join(parts[1:])}"
    if rel.startswith("SkyboxHandPainted/"):
        return f"Game/Art/Skyboxes/HandPainted/{'/'.join(parts[1:])}"
    if rel.startswith("Models/"):
        return f"Game/Art/Models/Props/{'/'.join(parts[1:])}"
    if rel.startswith("Mirror material/"):
        return f"Game/Materials/Mirrors/Legacy/{'/'.join(parts[1:])}"
    if rel.startswith("L3 mirror/"):
        return f"Game/Materials/Mirrors/Level3/{'/'.join(parts[1:])}"
    if rel.startswith("Important/"):
        return f"Game/Materials/Core/{'/'.join(parts[1:])}"
    if rel.startswith("LMHPOLY/"):
        return f"Game/Art/Environment/LMHPOLY/{'/'.join(parts[1:])}"
    if rel.startswith("Runemark Studio/"):
        return f"Game/Art/Environment/Runemark/{'/'.join(parts[1:])}"
    if rel.startswith("ithappy/"):
        return f"Game/Art/Environment/Ithappy/{'/'.join(parts[1:])}"
    if rel.startswith("Fantasy Skybox FREE/"):
        return f"Game/Art/Skyboxes/Fantasy/{'/'.join(parts[1:])}"
    if rel.startswith("Video/"):
        return f"Game/Video/{'/'.join(parts[1:])}"
    if rel.startswith("TutorialInfo/"):
        return f"Game/Documentation/TutorialInfo/{'/'.join(parts[1:])}"

    if "/" not in rel:
        if suffix in {".mp3", ".wav"}:
            return f"Game/Audio/{name}"
        if suffix in {".mat"}:
            return f"Game/Materials/{name}"
        if suffix in {".png", ".jpg"}:
            return f"Game/Art/Textures/{name}"
        if suffix in {".fbx"}:
            return f"Game/Art/Models/{name}"
        if suffix in {".shader"}:
            return f"Game/Shaders/{name}"
        if suffix in {".renderTexture"}:
            return f"Game/RenderTextures/{name}"
        if suffix in {".mp4"}:
            return f"Game/Video/{name}"

    return None


def reorganize(used: set[str]) -> list[tuple[str, str]]:
    moves: list[tuple[str, str]] = []
    # Move deepest paths first to avoid folder conflicts.
    for rel in sorted(used, key=lambda x: x.count("/"), reverse=True):
        dest = categorize(rel)
        if dest and dest != rel:
            move_asset(rel, dest, moves)
    return moves


def remove_empty_dirs() -> None:
    for dirpath, dirnames, filenames in os.walk(ASSETS, topdown=False):
        path = Path(dirpath)
        if path == ASSETS:
            continue
        # keep folder if it has any non-meta file or child folder with content
        entries = list(path.iterdir())
        if not entries:
            path.rmdir()
            meta = Path(str(path) + ".meta")
            if meta.exists():
                meta.unlink()


def update_build_settings_scene_paths(moves: list[tuple[str, str]]) -> None:
    mapping = {src: dst for src, dst in moves}
    build_settings = ROOT / "ProjectSettings" / "EditorBuildSettings.asset"
    if not build_settings.exists():
        return
    text = build_settings.read_text(encoding="utf-8")
    for src, dst in mapping.items():
        old = f"Assets/{src}"
        new = f"Assets/{dst}"
        text = text.replace(old, new)
    build_settings.write_text(text, encoding="utf-8")


def main() -> None:
    print("Analyzing dependencies...")
    used, unused = analyze()
    print(f"Used: {len(used)} | Unused: {len(unused)}")

    used_tops = Counter(to_posix(p).split("/")[0] for p in used)
    print("Used top-level folders:")
    for name, count in used_tops.most_common(20):
        print(f"  {name}: {count}")

    print("Deleting unused assets...")
    deleted_files = delete_unused(unused)
    print(f"Deleted {deleted_files} files")

    print("Removing empty directories...")
    remove_empty_dirs()

    print("Reorganizing kept assets...")
    moves = reorganize(used)
    print(f"Moved {len(moves)} assets")
    update_build_settings_scene_paths(moves)

    remove_empty_dirs()

    report = {
        "used_count": len(used),
        "unused_count": len(unused),
        "deleted_files": deleted_files,
        "moves": moves,
        "used": sorted(used),
    }
    (ROOT / "_cleanup_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("Done. Report written to _cleanup_report.json")


if __name__ == "__main__":
    main()
