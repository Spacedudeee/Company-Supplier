#!/usr/bin/env python3
"""Refresh the version dropdown in the bug-report issue form from GitHub Releases.

Keeps the latest MAX_PER_TRACK *stable* and MAX_PER_TRACK *beta* versions. A GitHub
pre-release counts as beta, a normal (non-prerelease, non-draft) release as stable.
Only the lines between the auto-versions markers are touched; the static
"Built from source" / "Not sure" options are left alone.

Run by .github/workflows/update-issue-versions.yml. Needs `gh` authenticated via
GH_TOKEN (the workflow passes the default GITHUB_TOKEN).
"""
import json
import pathlib
import re
import subprocess

FORM = pathlib.Path(".github/ISSUE_TEMPLATE/bug_report.yml")
START = "# >>> auto-versions (managed by update-issue-versions workflow) >>>"
END = "# <<< auto-versions <<<"
INDENT = " " * 8           # list items sit 8 spaces deep under "options:"
MAX_PER_TRACK = 3          # keep at most 3 stable + 3 beta


def fetch_releases():
    out = subprocess.check_output(
        ["gh", "release", "list", "--limit", "200",
         "--json", "tagName,isPrerelease,isDraft,publishedAt"],
        text=True,
    )
    rels = [r for r in json.loads(out) if not r.get("isDraft")]
    rels.sort(key=lambda r: r.get("publishedAt") or "", reverse=True)
    return rels


def label(tag, is_pre):
    version = tag[1:] if tag.startswith("v") else tag
    return f"{version} (BETA)" if is_pre else f"{version} (stable)"


def main():
    rels = fetch_releases()
    beta = [r for r in rels if r["isPrerelease"]][:MAX_PER_TRACK]
    stable = [r for r in rels if not r["isPrerelease"]][:MAX_PER_TRACK]
    chosen = beta + stable  # newest betas first, then stables

    items = [f"{INDENT}- {label(r['tagName'], r['isPrerelease'])}" for r in chosen]
    region = "\n".join([f"{INDENT}{START}", *items, f"{INDENT}{END}"])

    text = FORM.read_text(encoding="utf-8")
    pattern = re.compile(
        re.escape(INDENT + START) + ".*?" + re.escape(INDENT + END),
        re.DOTALL,
    )
    if not pattern.search(text):
        raise SystemExit(f"auto-versions markers not found in {FORM}")

    new_text = pattern.sub(lambda _: region, text)
    if new_text != text:
        FORM.write_text(new_text, encoding="utf-8")
        print("Updated version dropdown:")
    else:
        print("Version dropdown already up to date:")
    for item in items:
        print(" ", item.strip())


if __name__ == "__main__":
    main()
