# textkit

[![CI](https://github.com/lelegaga/test/actions/workflows/ci.yml/badge.svg)](https://github.com/lelegaga/test/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<p align="center">
  <img src="assets/crocodile-bike.svg" alt="An animated crocodile riding a bicycle" width="640">
</p>

A small Python toolkit for analyzing and transforming text, usable as a library or from the command line.

> **Also in this repo:** [`game/`](game/README.md), *Iron Tide: Coastal
> Assault*. It's a complete Godot 4 arcade run-and-gun demo with procedurally
> generated pixel art and audio. Run it with `godot --path game`.

## Installation

```bash
git clone https://github.com/lelegaga/test.git
cd test
pip install -e .
```

## Command-line usage

```console
$ textkit stats "Hello world. How are you?"
          characters: 25
               words: 5
               lines: 1
           sentences: 2
 average_word_length: 3.8

$ echo "the cat and the dog and the bird" | textkit top -n 2
     3  the
     2  and

$ textkit slug "Hello World!"
hello-world

$ textkit palindrome "A man, a plan, a canal: Panama"
yes
```

You can also run it as `python -m textkit`.

## Library usage

```python
from textkit import analyze, slugify, top_words

analyze("Hello world.").words        # 2
slugify("Café au lait")              # "cafe-au-lait"
top_words("a b a c a", n=1)          # [("a", 3)]
```

## Project structure

```
.
├── src/textkit/        # Package source
│   ├── core.py         # Text utilities
│   └── cli.py          # Command-line interface
├── tests/              # pytest test suite
├── .github/            # CI workflow, issue and PR templates
├── assets/             # Images, including the animated crocodile SVG
├── game/               # Godot 4 arcade shooter demo (see game/README.md)
├── pyproject.toml      # Package metadata and tool config
├── CHANGELOG.md
├── CONTRIBUTING.md
└── LICENSE
```

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE)
