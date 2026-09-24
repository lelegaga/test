"""Core text utilities."""

from __future__ import annotations

import re
import unicodedata
from collections import Counter
from dataclasses import dataclass

_WORD_RE = re.compile(r"\w+(?:'\w+)?")
_SENTENCE_RE = re.compile(r"[^.!?。！？]+[.!?。！？]*")


def _words(text: str) -> list[str]:
    return _WORD_RE.findall(text.lower())


def word_count(text: str) -> int:
    """Return the number of words in ``text``."""
    return len(_words(text))


def top_words(text: str, n: int = 5) -> list[tuple[str, int]]:
    """Return the ``n`` most common words with their counts."""
    if n < 1:
        raise ValueError("n must be at least 1")
    return Counter(_words(text)).most_common(n)


def slugify(text: str) -> str:
    """Turn ``text`` into a URL-friendly slug, e.g. ``"Hello World!"`` -> ``"hello-world"``."""
    normalized = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "-", normalized.lower()).strip("-")


def is_palindrome(text: str) -> bool:
    """Return whether ``text`` reads the same backwards, ignoring case and punctuation."""
    chars = [c for c in text.lower() if c.isalnum()]
    return bool(chars) and chars == chars[::-1]


@dataclass(frozen=True)
class TextStats:
    characters: int
    words: int
    lines: int
    sentences: int
    average_word_length: float


def analyze(text: str) -> TextStats:
    """Return basic statistics about ``text``."""
    words = _words(text)
    sentences = [s for s in _SENTENCE_RE.findall(text) if s.strip()]
    average = sum(map(len, words)) / len(words) if words else 0.0
    return TextStats(
        characters=len(text),
        words=len(words),
        lines=len(text.splitlines()),
        sentences=len(sentences),
        average_word_length=round(average, 2),
    )
