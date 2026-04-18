#!/usr/bin/env python3
from __future__ import annotations

import argparse
import html
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate an HTML report from TRX files.")
    parser.add_argument("--results-dir", required=True, help="Directory that contains .trx files.")
    parser.add_argument("--title", required=True, help="Report title.")
    args = parser.parse_args()

    results_dir = Path(args.results_dir)
    html_dir = results_dir / "html"
    html_dir.mkdir(parents=True, exist_ok=True)
    report_file = html_dir / "index.html"

    trx_files = sorted(results_dir.glob("*.trx"))
    rows: list[str] = []

    if trx_files:
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        for trx_file in trx_files:
            root = ET.parse(trx_file).getroot()
            for test in root.findall(".//t:UnitTestResult", ns):
                name = test.attrib.get("testName", "Unknown")
                outcome = test.attrib.get("outcome", "Unknown")
                duration = test.attrib.get("duration", "")
                rows.append(
                    f"<tr><td>{html.escape(name)}</td><td>{html.escape(outcome)}</td><td>{html.escape(duration)}</td></tr>"
                )

    body = "".join(rows) if rows else "<tr><td colspan='3'>No test results were found.</td></tr>"
    report_file.write_text(
        f"<!doctype html><html><head><meta charset='utf-8'><title>{html.escape(args.title)}</title>"
        "<style>body{font-family:Arial,sans-serif;margin:20px;}table{border-collapse:collapse;width:100%;}"
        "th,td{border:1px solid #ddd;padding:8px;text-align:left;}th{background:#f3f3f3;}</style></head>"
        f"<body><h1>{html.escape(args.title)}</h1><table><thead><tr><th>Test</th><th>Outcome</th><th>Duration</th></tr></thead>"
        f"<tbody>{body}</tbody></table></body></html>",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
