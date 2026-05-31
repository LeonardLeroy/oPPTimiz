# oPPTimiz is a tool that reduces Office documents size.
# Copyright (C) 2025 EDF
# This program is free software: you can redistribute it and/or modify it under
# the terms of the GNU General Public License as published by the Free Software
# Foundation, either version 3 of the License, or (at your option) any later version.
# This program is distributed in the hope that it will be useful, but WITHOUT ANY
# WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
# PARTICULAR PURPOSE. See the GNU General Public License for more details.

"""Cross-platform OOXML (.pptx/.docx/.xlsx) size optimizer.

An OOXML file is a ZIP of XML parts plus media. The document is optimized without
any Office dependency by recompressing the images under */media/, dropping media
that nothing references, and repacking the archive with maximum DEFLATE.
"""

import io
import os
import posixpath
import zipfile
from dataclasses import dataclass

from . import images

LEVELS = {
    "maximal": {"max_dimension": 1920, "jpeg_quality": 72},
    "intermediate": {"max_dimension": 2560, "jpeg_quality": 85},
}

SUPPORTED_SUFFIXES = (".pptx", ".docx", ".xlsx")


@dataclass
class OptimizationResult:
    initial_size: int
    new_size: int
    images_optimized: int
    media_removed: int

    @property
    def gain(self) -> int:
        return self.initial_size - self.new_size

    @property
    def percentage(self) -> float:
        if self.initial_size == 0:
            return 0.0
        return round(self.gain / self.initial_size * 100, 2)


def _is_media(name: str) -> bool:
    return "/media/" in name.lower() and not name.endswith("/")


def _find_unused_media(parts: dict[str, bytes], media_names: list[str]) -> set[str]:
    """Return media parts whose file name appears in no other part.

    Media is referenced from .rels targets (and occasionally inline) by its base
    name. A part is considered removable only when that name is found nowhere
    else; when unsure, the media is kept.
    """
    haystack = bytearray()
    for name, data in parts.items():
        if not _is_media(name):
            haystack += data

    unused = set()
    for name in media_names:
        basename = posixpath.basename(name).encode("utf-8", "ignore")
        if basename and basename not in haystack:
            unused.add(name)
    return unused


def optimize_bytes(
    content: bytes,
    level: str = "maximal",
    prune_unused: bool = True,
) -> tuple[bytes, OptimizationResult]:
    if level not in LEVELS:
        raise ValueError(f"Unknown level '{level}', expected one of {sorted(LEVELS)}")
    preset = LEVELS[level]

    with zipfile.ZipFile(io.BytesIO(content)) as archive:
        order = [info.filename for info in archive.infolist() if not info.is_dir()]
        parts = {name: archive.read(name) for name in order}

    media_names = [name for name in order if _is_media(name)]

    images_optimized = 0
    for name in media_names:
        ext = os.path.splitext(name)[1]
        smaller = images.recompress(parts[name], ext, preset["max_dimension"], preset["jpeg_quality"])
        if smaller is not None:
            parts[name] = smaller
            images_optimized += 1

    media_removed = 0
    if prune_unused:
        for name in _find_unused_media(parts, media_names):
            del parts[name]
            media_removed += 1

    out = io.BytesIO()
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name in order:
            if name in parts:
                archive.writestr(name, parts[name])

    new_content = out.getvalue()
    result = OptimizationResult(
        initial_size=len(content),
        new_size=len(new_content),
        images_optimized=images_optimized,
        media_removed=media_removed,
    )
    return new_content, result


def optimize_file(
    source: str,
    level: str = "maximal",
    keep_original: bool = False,
    output: str | None = None,
    prune_unused: bool = True,
) -> tuple[str, OptimizationResult]:
    if not source.lower().endswith(SUPPORTED_SUFFIXES):
        raise ValueError(f"Unsupported file type: {source} (expected {SUPPORTED_SUFFIXES})")

    with open(source, "rb") as handle:
        content = handle.read()

    new_content, result = optimize_bytes(content, level=level, prune_unused=prune_unused)

    if output:
        destination = output
    elif keep_original:
        root, ext = os.path.splitext(source)
        destination = f"{root}_oPPTimiz{ext}"
    else:
        destination = source

    with open(destination, "wb") as handle:
        handle.write(new_content)

    return destination, result
