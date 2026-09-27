# textkit

[![CI](https://github.com/lelegaga/test/actions/workflows/ci.yml/badge.svg)](https://github.com/lelegaga/test/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<p align="center">
  <img src="assets/crocodile-bike.svg" alt="An animated crocodile riding a bicycle" width="640">
</p>

A small Python toolkit for analyzing and transforming text, usable as a library or from the command line.

## Iron Assault (钢铁突击)

`games/iron-assault/` holds a browser run-and-gun game in the style of classic arcade
side-scrollers. Open `games/iron-assault/index.html` in a browser; nothing needs to be
built or installed. Graphics are painted procedurally on a canvas, and all sound effects,
music and announcer lines are generated in the browser (Web Audio + Speech Synthesis).

- **5 missions + Boss Rush**: jungle, desert ruins, snowy peak, a ruined city at night
  and the enemy HQ. Each has its own scenery, weather, music and boss (heavy tank,
  mechanical scorpion, gunship, bipedal mech, fortress).
- **4 difficulty levels** (新兵 / 老兵 / 精英 / 地狱) that change lives, health, enemy
  fire rate, bullet speed, enemy density, boss health and score multiplier.
- **Weapons**: pistol, heavy machine gun, homing rockets, flame shot, shotgun, laser,
  grenades and a melee knife. Rescue prisoners for supplies.
- **Controls**: keyboard (arrows/WASD, J fire, K jump, L grenade, P pause), gamepad,
  and on-screen touch controls on phones.

## 三国身份卡牌对战 (Sanguo Cards)

`games/sanguo-cards/` holds a Unity 6 project for a multiplayer Three Kingdoms identity card game
(server-authoritative host, LAN play, identity and team modes, AI seats). The rules engine is plain
C# and is built and tested without Unity from `games/sanguo-cards/DotNet`. See
[games/sanguo-cards/README.md](games/sanguo-cards/README.md) and
[docs/ARCHITECTURE.md](games/sanguo-cards/docs/ARCHITECTURE.md).

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
├── games/iron-assault/ # Browser run-and-gun game (HTML5 canvas, no build step)
├── games/sanguo-cards/ # Unity 6 multiplayer card game (C# rules engine + .NET test harness)
├── pyproject.toml      # Package metadata and tool config
├── CHANGELOG.md
├── CONTRIBUTING.md
└── LICENSE
```

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE)
