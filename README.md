# WordleApp

A polished Wordle clone as a Terminal User Interface, built in C# / .NET 8 with [Spectre.Console](https://spectreconsole.net/).

```bash
cd WordleApp
dotnet run
```

## Documentation

- **Docs site** (MkDocs Material): source in [`docs/`](docs/), config in [`mkdocs.yml`](mkdocs.yml).
  Auto-deploys to GitHub Pages on push to `main` via [`.github/workflows/docs.yml`](.github/workflows/docs.yml).
  Preview locally:
  ```bash
  pip install -r requirements-docs.txt
  mkdocs serve
  ```
- **Slide deck** (PowerPoint): [`presentation/WordleApp.pptx`](presentation/WordleApp.pptx) (also available as `presentation/WordleApp.pdf`).
  Built from [`presentation/slides.md`](presentation/slides.md) with [Marp](https://marp.app/). Regenerate after edits:
  ```bash
  npx @marp-team/marp-cli presentation/slides.md --pptx --allow-local-files -o presentation/WordleApp.pptx
  ```
- **Original deep-dive** (single file, German): [`documentation.md`](documentation.md) — this is the source the docs site was split from.
