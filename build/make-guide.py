"""Builds the in-app user guide: docs/ without the IT pages (docs/quan-tri/) into one offline HTML file.

The installer is public on GitHub Releases, so the IT pages stay out of it. They are only in the
full guide: python <guidebook.py> build docs  ->  docs/_site/index.html

    python build/make-guide.py <path to guidebook.py> <out.html>

guidebook.py is the "guidebook" Claude skill (~/.claude/skills/guidebook/scripts/guidebook.py).
Exits non-zero if the user edition has a broken link, a leftover link to an IT page, or any other
error reported by the guidebook checker.
"""
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

DOCS = Path(__file__).resolve().parent.parent / "docs"
ADMIN = "quan-tri"
SUBTITLE = "Cho người đưa nội dung lên TV"
ADMIN_LINK = re.compile(r"\[([^\]]+)\]\((?:\.\./)?" + ADMIN + r"/[^)]*\)")
ADMIN_CARD = re.compile(r"[ \t]*<tr>(?:(?!</tr>).)*?" + ADMIN + r"/.*?</tr>[ \t]*\r?\n", re.S)
EMPTY_SECTION = re.compile(r"^## [^\n]*\n(?:[ \t]*\n)*(?=^## |\Z)", re.M)


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8")


def write(p: Path, text: str) -> None:
    p.write_text(text, encoding="utf-8", newline="")


def make_user_edition(book: Path) -> None:
    shutil.copytree(DOCS, book, ignore=shutil.ignore_patterns("_site", ADMIN))

    summary = book / "SUMMARY.md"
    lines = read(summary).replace("\r\n", "\n").split("\n")
    write(summary, EMPTY_SECTION.sub("", "\n".join(l for l in lines if ADMIN + "/" not in l)))

    readme = book / "README.md"
    write(readme, ADMIN_CARD.sub("", read(readme)))

    for page in book.rglob("*.md"):
        text = read(page)
        stripped = ADMIN_LINK.sub(r"\1", text)
        if stripped != text:
            write(page, stripped)
        if ADMIN + "/" in stripped:
            sys.exit(f"{page.relative_to(book)} still points to {ADMIN}/ after filtering")

    meta_path = book / "guidebook.json"
    meta = json.loads(read(meta_path))
    meta["subtitle"] = SUBTITLE
    write(meta_path, json.dumps(meta, ensure_ascii=False, indent=2) + "\n")


def main() -> int:
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    guidebook, out = Path(sys.argv[1]), Path(sys.argv[2]).resolve()
    if not guidebook.is_file():
        sys.exit(f"guidebook.py not found: {guidebook}")
    with tempfile.TemporaryDirectory() as tmp:
        book = Path(tmp) / "docs"
        make_user_edition(book)
        out.parent.mkdir(parents=True, exist_ok=True)
        return subprocess.call([sys.executable, str(guidebook), "build", str(book), "--out", str(out)])


if __name__ == "__main__":
    sys.exit(main())
