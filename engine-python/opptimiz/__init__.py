# oPPTimiz is a tool that reduces Office documents size.
# Copyright (C) 2025 EDF
# Licensed under the GNU General Public License v3 or later. See COPYING.

"""Cross-platform OOXML document optimizer."""

from .optimizer import (
    LEVELS,
    SUPPORTED_SUFFIXES,
    OptimizationResult,
    optimize_bytes,
    optimize_file,
)

__all__ = [
    "LEVELS",
    "SUPPORTED_SUFFIXES",
    "OptimizationResult",
    "optimize_bytes",
    "optimize_file",
]
