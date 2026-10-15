#!/usr/bin/env python3
"""Writes down what GitHub counts about the project, for .github/workflows/stats.yml.

GitHub keeps a release asset's download count, but only as a running total, and a repository's
visitors for 14 days only. This adds today's numbers to CSV files in a folder - the stats branch,
checked out - and writes a README.md there that sums them up.

    scripts/collect-stats.py OWNER/REPO FOLDER

Release downloads are public. Visitors need a token that can read the repository's Administration
settings, in TRAFFIC_TOKEN; without one the gh CLI's own login is used, and where that cannot read
them either - the workflow's token cannot - they are left as they were, with a warning.
docs/usage-statistics.md says what the numbers mean and how to set the token up.
"""

import csv
import datetime
import json
import os
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

DOWNLOAD_FIELDS = ["date", "release", "published", "asset", "downloads"]
VIEW_FIELDS = ["date", "views", "visitors"]
CLONE_FIELDS = ["date", "clones", "cloners"]
REFERRER_FIELDS = ["date", "referrer", "views", "visitors"]
PATH_FIELDS = ["date", "path", "title", "views", "visitors"]

# How many days the charts and the daily tables in the README go back.
RECENT_DAYS = 30


def gh(path, token=None):
    """GETs a GitHub API path through the gh CLI, which handles the login."""
    env = dict(os.environ)
    if token:
        env["GH_TOKEN"] = token
    result = subprocess.run(
        ["gh", "api", "-H", "Accept: application/vnd.github+json", path],
        capture_output=True,
        text=True,
        env=env,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "gh api %s failed" % path)
    return json.loads(result.stdout)


def releases(repo):
    found, page = [], 1
    while True:
        batch = gh("repos/%s/releases?per_page=100&page=%d" % (repo, page))
        found += [release for release in batch if not release.get("draft")]
        if len(batch) < 100:
            return found
        page += 1


def read(path):
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8") as file:
        return list(csv.DictReader(file))


def write(path, fields, rows):
    with path.open("w", newline="", encoding="utf-8") as file:
        writer = csv.DictWriter(file, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def replace_day(path, fields, today, rows):
    """A snapshot taken today replaces one taken earlier today, so a second run doesn't count twice."""
    kept = [row for row in read(path) if row["date"] != today]
    write(path, fields, kept + rows)


def merge_days(path, fields, entries, key, unique_key, value_name, unique_name):
    """GitHub's last 14 days, merged into the history by day: a newer count for a day replaces the
    older one, which for the day the last run was on was not over yet."""
    by_day = {row["date"]: row for row in read(path)}
    for entry in entries:
        day = entry["timestamp"][:10]
        by_day[day] = {"date": day, value_name: str(entry[key]), unique_name: str(entry[unique_key])}
    write(path, fields, [by_day[day] for day in sorted(by_day)])


def collect_downloads(repo, folder, today):
    rows = []
    for release in releases(repo):
        for asset in release.get("assets", []):
            rows.append({
                "date": today,
                "release": release["tag_name"],
                "published": (release.get("published_at") or "")[:10],
                "asset": asset["name"],
                "downloads": str(asset["download_count"]),
            })
    replace_day(folder / "downloads.csv", DOWNLOAD_FIELDS, today, rows)


def collect_traffic(repo, folder, today, token):
    views = gh("repos/%s/traffic/views" % repo, token)
    clones = gh("repos/%s/traffic/clones" % repo, token)
    referrers = gh("repos/%s/traffic/popular/referrers" % repo, token)
    paths = gh("repos/%s/traffic/popular/paths" % repo, token)

    merge_days(folder / "views.csv", VIEW_FIELDS, views.get("views", []), "count", "uniques", "views", "visitors")
    merge_days(folder / "clones.csv", CLONE_FIELDS, clones.get("clones", []), "count", "uniques", "clones", "cloners")
    replace_day(folder / "referrers.csv", REFERRER_FIELDS, today, [
        {"date": today, "referrer": item["referrer"], "views": str(item["count"]), "visitors": str(item["uniques"])}
        for item in referrers
    ])
    replace_day(folder / "paths.csv", PATH_FIELDS, today, [
        {"date": today, "path": item["path"], "title": item["title"], "views": str(item["count"]), "visitors": str(item["uniques"])}
        for item in paths
    ])


def kind(asset):
    name = asset.lower()
    if name.endswith(".dmg"):
        return "dmg"
    if name.endswith(".zip"):
        return "zip"
    return None


def cell(text):
    return str(text).replace("|", "\\|")


def chart(title, labels, bars=None, line=None):
    """A Mermaid chart, which GitHub draws in place of the code block."""
    lines = ["```mermaid", "xychart-beta", '    title "%s"' % title]
    lines.append("    x-axis [%s]" % ", ".join('"%s"' % label for label in labels))
    if bars is not None:
        lines.append("    bar [%s]" % ", ".join(str(value) for value in bars))
    if line is not None:
        lines.append("    line [%s]" % ", ".join(str(value) for value in line))
    lines.append("```")
    return lines


def days_back(today, count):
    end = datetime.date.fromisoformat(today)
    return [(end - datetime.timedelta(days=offset)).isoformat() for offset in range(count - 1, -1, -1)]


def summarize(repo, folder, today, traffic_note):
    out = ["# Usage statistics", ""]
    out.append(
        "Written every day by [the stats workflow](https://github.com/%s/blob/main/.github/workflows/stats.yml); "
        "[what the numbers mean](https://github.com/%s/blob/main/docs/usage-statistics.md). Last run %s (UTC)."
        % (repo, repo, today)
    )
    out.append("")

    # Downloads: today's running totals per release, and the downloads each day from one day's
    # totals to the next.
    downloads = read(folder / "downloads.csv")
    latest = [row for row in downloads if row["date"] == today]
    per_release = defaultdict(lambda: {"dmg": 0, "zip": 0, "published": ""})
    for row in latest:
        if kind(row["asset"]):
            per_release[row["release"]][kind(row["asset"])] += int(row["downloads"])
            per_release[row["release"]]["published"] = row["published"]

    out += ["## Downloads", ""]
    out.append(
        "Every download counts, not every person: one person downloading twice is two. The `.zip` is what "
        "the app's own updater, `install.sh` and Homebrew download, so it is roughly the copies that "
        "installed or updated to that version; the `.dmg` is downloaded by hand."
    )
    out.append("")
    out += ["| Release | Published | .dmg | .zip |", "| --- | --- | ---: | ---: |"]
    for release, counts in sorted(per_release.items(), key=lambda item: item[1]["published"], reverse=True):
        out.append("| %s | %s | %d | %d |" % (cell(release), counts["published"], counts["dmg"], counts["zip"]))
    total_dmg = sum(counts["dmg"] for counts in per_release.values())
    total_zip = sum(counts["zip"] for counts in per_release.values())
    out.append("| **All** | | **%d** | **%d** |" % (total_dmg, total_zip))
    out.append("")

    totals = defaultdict(int)
    for row in downloads:
        if kind(row["asset"]):
            totals[row["date"]] += int(row["downloads"])
    dates = sorted(totals)
    daily = [(dates[i], totals[dates[i]] - totals[dates[i - 1]]) for i in range(1, len(dates))][-RECENT_DAYS:]
    if daily:
        out.append("Downloads a day (`.dmg` and `.zip`), from one run's totals to the next:")
        out.append("")
        out += chart("Downloads a day", [day[5:] for day, _ in daily], bars=[count for _, count in daily])
    else:
        out.append("Downloads a day appear from the second run on: each day is the difference between two runs.")
    out.append("")

    # Visitors: GitHub's own counts, kept here for longer than its 14 days.
    views = {row["date"]: row for row in read(folder / "views.csv")}
    clones = {row["date"]: row for row in read(folder / "clones.csv")}
    out += ["## Visitors", ""]
    if traffic_note:
        out += [traffic_note, ""]
    if views:
        first = min(views)
        recent = [day for day in days_back(today, RECENT_DAYS) if day >= first]
        out.append(
            "Views of the repository's pages on GitHub and the people behind them, a day. Bars: views. "
            "Line: different visitors."
        )
        out.append("")
        out += chart(
            "Visitors a day",
            [day[5:] for day in recent],
            bars=[int(views.get(day, {}).get("views", 0)) for day in recent],
            line=[int(views.get(day, {}).get("visitors", 0)) for day in recent],
        )
        out.append("")
        out += ["| Day | Views | Visitors | Clones |", "| --- | ---: | ---: | ---: |"]
        for day in reversed(recent):
            out.append("| %s | %s | %s | %s |" % (
                day,
                views.get(day, {}).get("views", "0"),
                views.get(day, {}).get("visitors", "0"),
                clones.get(day, {}).get("clones", "0"),
            ))
        out.append("")
        out.append("Clones are almost all automated - CI and bots - and say little about people.")
        out.append("")
    elif not traffic_note:
        out += ["No visitors recorded yet.", ""]

    referrers = read(folder / "referrers.csv")
    if referrers:
        last = max(row["date"] for row in referrers)
        out += ["### Where they came from", "", "GitHub's top sites for the 14 days up to %s." % last, ""]
        out += ["| Site | Views | Visitors |", "| --- | ---: | ---: |"]
        for row in (row for row in referrers if row["date"] == last):
            out.append("| %s | %s | %s |" % (cell(row["referrer"]), row["views"], row["visitors"]))
        out.append("")

    paths = read(folder / "paths.csv")
    if paths:
        last = max(row["date"] for row in paths)
        out += ["### What they looked at", "", "GitHub's top pages for the 14 days up to %s." % last, ""]
        out += ["| Page | Views | Visitors |", "| --- | ---: | ---: |"]
        prefix = "/" + repo
        for row in (row for row in paths if row["date"] == last):
            page = row["path"][len(prefix):] if row["path"].startswith(prefix) else row["path"]
            out.append("| %s | %s | %s |" % (cell(page or "/"), row["views"], row["visitors"]))
        out.append("")

    (folder / "README.md").write_text("\n".join(out), encoding="utf-8")


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: collect-stats.py OWNER/REPO FOLDER")
    repo, folder = sys.argv[1], Path(sys.argv[2])
    folder.mkdir(parents=True, exist_ok=True)
    today = datetime.datetime.now(datetime.timezone.utc).date().isoformat()

    collect_downloads(repo, folder, today)

    traffic_note = None
    try:
        collect_traffic(repo, folder, today, os.environ.get("TRAFFIC_TOKEN") or None)
    except RuntimeError as error:
        # Not a failure of the run: downloads were still written, and visitors only need the token.
        print("::warning title=Visitors not collected::%s" % str(error).splitlines()[0])
        traffic_note = (
            "Visitors were not updated on %s: reading them needs the `STATS_TRAFFIC_TOKEN` secret "
            "(see the docs linked above)." % today
        )

    summarize(repo, folder, today, traffic_note)
    print("Collected %s into %s." % (today, folder))


if __name__ == "__main__":
    main()
