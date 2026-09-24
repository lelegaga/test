import io

from textkit.cli import main


def test_slug(capsys):
    assert main(["slug", "Hello World"]) == 0
    assert capsys.readouterr().out.strip() == "hello-world"


def test_palindrome_exit_codes(capsys):
    assert main(["palindrome", "racecar"]) == 0
    assert main(["palindrome", "python"]) == 1
    assert capsys.readouterr().out.split() == ["yes", "no"]


def test_top_reads_stdin(monkeypatch, capsys):
    monkeypatch.setattr("sys.stdin", io.StringIO("a b a c a b"))
    assert main(["top", "-n", "2"]) == 0
    lines = capsys.readouterr().out.splitlines()
    assert lines[0].split() == ["3", "a"]
    assert lines[1].split() == ["2", "b"]


def test_stats(capsys):
    assert main(["stats", "One two three."]) == 0
    assert "words: 3" in capsys.readouterr().out
