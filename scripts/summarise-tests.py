#!/usr/bin/env python3
"""Sums up a test run for the CI job's summary, for .github/workflows/ci.yml.

Reads what `dotnet test` left under TestResults/ - the .trx results and the coverage report - and
prints how many tests passed and how much of the code they reach, as Markdown. Both build-and-test
jobs, on macOS and on Windows, run it, so their summaries read alike.

    scripts/summarise-tests.py >> "$GITHUB_STEP_SUMMARY"
"""

import glob
import xml.etree.ElementTree as ET

print("### Tests")
for trx in glob.glob("TestResults/**/*.trx", recursive=True):
    counters = ET.parse(trx).getroot().find(".//{*}Counters")
    if counters is not None:
        total, passed, failed = (int(counters.get(k, 0)) for k in ("total", "passed", "failed"))
        print(f"{passed} of {total} passed, {failed} failed")
rates = []
for report in glob.glob("TestResults/**/coverage.cobertura.xml", recursive=True):
    root = ET.parse(report).getroot()
    rates.append((float(root.get("line-rate", 0)), float(root.get("branch-rate", 0))))
if rates:
    line, branch = rates[0]
    print("\n### Coverage")
    print(f"{line:.1%} of lines, {branch:.1%} of branches")
