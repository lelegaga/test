"""Command-line interface for textkit."""

from __future__ import annotations

import argparse
import sys
from collections.abc import Sequence
from dataclasses import asdict

from textkit import __version__
from textkit.core import analyze, is_palindrome, slugify, top_words


def _read_input(text: str | None) -> str:
    return text if text is not None else sys.stdin.read()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="textkit", description=__doc__)
    parser.add_argument("--version", action="version", version=f"%(prog)s {__version__}")
    sub = parser.add_subparsers(dest="command", required=True)

    stats = sub.add_parser("stats", help="show statistics about the text")
    stats.add_argument("text", nargs="?", help="text to analyze (reads stdin if omitted)")

    top = sub.add_parser("top", help="show the most common words")
    top.add_argument("text", nargs="?", help="text to analyze (reads stdin if omitted)")
    top.add_argument("-n", type=int, default=5, help="number of words to show (default: 5)")

    slug = sub.add_parser("slug", help="convert text into a URL slug")
    slug.add_argument("text", help="text to convert")

    pal = sub.add_parser("palindrome", help="check whether text is a palindrome")
    pal.add_argument("text", help="text to check")

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)

    if args.command == "stats":
        for key, value in asdict(analyze(_read_input(args.text))).items():
            print(f"{key:>20}: {value}")
    elif args.command == "top":
        for word, count in top_words(_read_input(args.text), args.n):
            print(f"{count:>6}  {word}")
    elif args.command == "slug":
        print(slugify(args.text))
    elif args.command == "palindrome":
        result = is_palindrome(args.text)
        print("yes" if result else "no")
        return 0 if result else 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
