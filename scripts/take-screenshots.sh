#!/usr/bin/env bash
# take-screenshots.sh
#
# Generates documentation screenshots of the Arbor.HttpClient desktop app using
# Avalonia's headless rendering.  No display server is required.
#
# Usage:
#   ./scripts/take-screenshots.sh
#
# The screenshots are written to docs/screenshots/ in the repository root and
# should be committed alongside the README.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/docs/screenshots"

mkdir -p "$OUTPUT_DIR"
export SCREENSHOT_OUTPUT_DIR="$OUTPUT_DIR"

echo "Building solution..."
dotnet build "$REPO_ROOT/Arbor.HttpClient.slnx" -c Release -v quiet

echo "Generating screenshots -> $OUTPUT_DIR"
dotnet test "$REPO_ROOT/src/Arbor.HttpClient.Desktop.E2E.Tests" \
    --no-build \
    --configuration Release \
    --filter "Category=Screenshots" \
    -v minimal

echo ""
echo "Screenshots written:"
ls -1 "$OUTPUT_DIR"/*.png 2>/dev/null || echo "(none produced)"
