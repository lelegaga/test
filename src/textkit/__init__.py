"""textkit: a small toolkit for analyzing and transforming text."""

from textkit.core import (
    TextStats,
    analyze,
    is_palindrome,
    slugify,
    top_words,
    word_count,
)

__all__ = [
    "TextStats",
    "analyze",
    "is_palindrome",
    "slugify",
    "top_words",
    "word_count",
]
__version__ = "0.1.0"
