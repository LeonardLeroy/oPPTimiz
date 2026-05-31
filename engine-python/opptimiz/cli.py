# oPPTimiz is a tool that reduces Office documents size.
# Copyright (C) 2025 EDF
# Licensed under the GNU General Public License v3 or later. See COPYING.

"""Command-line entry point: ``opptimiz <file> [options]``."""

import argparse
import sys

from .optimizer import LEVELS, optimize_file


def _human(size: int) -> str:
    value = float(size)
    for unit in ("octets", "Ko", "Mo", "Go"):
        if value < 1024 or unit == "Go":
            return f"{value:.0f} {unit}" if unit == "octets" else f"{value:.1f} {unit}"
        value /= 1024
    return f"{value:.1f} Go"


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="opptimiz",
        description="Reduce the size of an Office document (.pptx/.docx/.xlsx).",
    )
    parser.add_argument("file", help="Document to optimize")
    parser.add_argument(
        "-l", "--level",
        choices=sorted(LEVELS),
        default="maximal",
        help="Compression level (default: maximal)",
    )
    parser.add_argument(
        "-k", "--keep",
        action="store_true",
        help="Keep the original file and write to <name>_oPPTimiz.<ext>",
    )
    parser.add_argument(
        "-o", "--output",
        help="Explicit output path (overrides --keep)",
    )
    parser.add_argument(
        "--no-prune",
        action="store_true",
        help="Do not remove unreferenced media parts",
    )
    parser.add_argument(
        "-q", "--quiet",
        action="store_true",
        help="Print nothing on success",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)

    try:
        destination, result = optimize_file(
            args.file,
            level=args.level,
            keep_original=args.keep,
            output=args.output,
            prune_unused=not args.no_prune,
        )
    except (ValueError, OSError) as error:
        print(f"oPPTimiz: {error}", file=sys.stderr)
        return 1

    if not args.quiet:
        print(f"Fichier optimise : {destination}")
        print(f"  Taille initiale  : {_human(result.initial_size)}")
        print(f"  Taille optimisee : {_human(result.new_size)}")
        print(f"  Gain             : {_human(result.gain)} ({result.percentage}%)")
        print(f"  Images traitees  : {result.images_optimized}")
        print(f"  Medias supprimes : {result.media_removed}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
