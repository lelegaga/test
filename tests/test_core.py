import pytest

from textkit import analyze, is_palindrome, slugify, top_words, word_count


def test_word_count():
    assert word_count("Hello, world! It's a nice day.") == 6
    assert word_count("") == 0


def test_top_words():
    text = "the cat and the dog and the bird"
    assert top_words(text, 2) == [("the", 3), ("and", 2)]


def test_top_words_rejects_non_positive_n():
    with pytest.raises(ValueError):
        top_words("anything", 0)


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("Hello World!", "hello-world"),
        ("  Multiple   spaces  ", "multiple-spaces"),
        ("Café au lait", "cafe-au-lait"),
        ("!!!", ""),
    ],
)
def test_slugify(text, expected):
    assert slugify(text) == expected


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("A man, a plan, a canal: Panama", True),
        ("racecar", True),
        ("hello", False),
        ("", False),
    ],
)
def test_is_palindrome(text, expected):
    assert is_palindrome(text) is expected


def test_analyze():
    stats = analyze("Hello world. How are you?\nFine!")
    assert stats.words == 6
    assert stats.lines == 2
    assert stats.sentences == 3
    assert stats.average_word_length == 3.83


def test_analyze_empty():
    stats = analyze("")
    assert stats.words == 0
    assert stats.average_word_length == 0.0
