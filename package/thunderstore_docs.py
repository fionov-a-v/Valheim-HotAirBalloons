#!/usr/bin/env python3
"""Тексты пакета из README.md: thunderstore_docs.py README.md <папка пакета>.

README пакета — это страница мода на Thunderstore, а версию после загрузки уже не поправить. Поэтому здесь: сначала
английская часть с баннером, русская — под спойлером; ссылки на файлы и якоря (там они не работают) убраны;
журнал изменений — отдельным CHANGELOG.md (у него на сайте своя вкладка).
"""
import re
import sys
from pathlib import Path

REPO = "https://github.com/fionov-a-v/Valheim-HotAirBalloons"
BANNER = "https://raw.githubusercontent.com/fionov-a-v/Valheim-HotAirBalloons/main/media/banner.png"


def absolute_links(text):
    return text.replace("](LICENSE.md)", f"]({REPO}/blob/main/LICENSE.md)")


def changelog_md(text):
    """«- **1.5.1** — фраза. Фраза.» → заголовок версии и список фраз, новые версии сверху (как в README)."""
    out = []
    for version, body in re.findall(r"^- \*\*(\d+\.\d+\.\d+)\*\* — (.+?)(?=^- \*\*|\Z)", text, re.M | re.S):
        out.append(f"## {version}\n")
        for sentence in re.split(r"(?<=\.)\s+(?=[A-Z`*0-9])", " ".join(body.split())):
            out.append(f"- {sentence[0].upper()}{sentence[1:]}")
        out.append("")
    if not out:
        raise SystemExit("В README не найден журнал изменений")
    return "\n".join(out)


def main():
    readme, out_dir = Path(sys.argv[1]), Path(sys.argv[2])
    # Разметка README: русская часть, разделитель с якорем, английская часть, журнал изменений.
    russian, rest = readme.read_text(encoding="utf-8").split('\n---\n\n<a id="english"></a>\n', 1)
    english, changelog = rest.split("\n## Changelog\n", 1)
    en_title, en_body = english.strip().split("\n", 1)
    _, ru_body = russian.strip().split("\n", 1)
    ru_body = ru_body.replace(" · [English below](#english)", "")

    page = (
        f"{en_title}\n\n![Hot Air Balloons]({BANNER})\n\n"
        f"Author: Fionov Alexander · source: {REPO} · описание на русском — в конце страницы\n\n"
        f"{en_body.strip()}\n\n"
        f"<details>\n<summary>Описание на русском (нажмите, чтобы открыть)</summary>\n\n{ru_body.strip()}\n\n</details>\n"
    )
    page = absolute_links(page)
    leftovers = re.findall(r"\]\((?!https?://)[^)]*\)", page)
    if leftovers:
        raise SystemExit(f"Ссылки, которые не работают на странице пакета: {leftovers}")
    (out_dir / "README.md").write_text(page, encoding="utf-8")
    (out_dir / "CHANGELOG.md").write_text(changelog_md(changelog), encoding="utf-8")


if __name__ == "__main__":
    main()
