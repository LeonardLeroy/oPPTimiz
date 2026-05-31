# oPPTimiz is a tool that reduces Office documents size.
# Copyright (C) 2025 EDF
# This program is free software: you can redistribute it and/or modify it under
# the terms of the GNU General Public License as published by the Free Software
# Foundation, either version 3 of the License, or (at your option) any later version.
# This program is distributed in the hope that it will be useful, but WITHOUT ANY
# WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
# PARTICULAR PURPOSE. See the GNU General Public License for more details.

"""Raster image recompression, keeping the source format and extension."""

from io import BytesIO

from PIL import Image

SUPPORTED_EXTENSIONS = {".png", ".jpg", ".jpeg"}


def recompress(data: bytes, ext: str, max_dimension: int, jpeg_quality: int) -> bytes | None:
    """Downscale then re-encode an image in its original format.

    Keeping the format means relationships and [Content_Types].xml never have to
    change, which makes the operation safe. Returns the new bytes only when they
    are smaller than the input, otherwise None.
    """
    ext = ext.lower()
    if ext not in SUPPORTED_EXTENSIONS:
        return None

    try:
        image = Image.open(BytesIO(data))
        image.load()
    except Exception:
        return None

    width, height = image.size
    longest_side = max(width, height)
    if longest_side > max_dimension:
        ratio = max_dimension / longest_side
        image = image.resize(
            (max(1, round(width * ratio)), max(1, round(height * ratio))),
            Image.LANCZOS,
        )

    buffer = BytesIO()
    if ext in (".jpg", ".jpeg"):
        if image.mode not in ("RGB", "L"):
            image = image.convert("RGB")
        image.save(buffer, format="JPEG", quality=jpeg_quality, optimize=True, progressive=True)
    else:
        image.save(buffer, format="PNG", optimize=True)

    result = buffer.getvalue()
    return result if len(result) < len(data) else None
