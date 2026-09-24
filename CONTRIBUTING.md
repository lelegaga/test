# Contributing

Thanks for helping improve textkit!

## Development setup

```bash
python -m venv .venv
source .venv/bin/activate
pip install -e ".[dev]"
```

## Before opening a pull request

```bash
ruff check .   # lint
pytest         # tests
```

Please add tests for new behavior and a line to `CHANGELOG.md`.
