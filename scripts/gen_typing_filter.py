#!/usr/bin/env python3
"""
gen_typing_filter.py

Generates an ffmpeg drawtext filter chain that animates a URL being typed
character by character.  Used by record-demo.sh.

Usage:
    python3 scripts/gen_typing_filter.py <url> <x> <y> <fontfile> [chars_per_second]

Output:
    One long comma-separated drawtext filter chain, printed to stdout.
"""

import sys

def escape_drawtext(s: str) -> str:
    """Escape characters that are special in ffmpeg's drawtext filter."""
    s = s.replace("\\", "\\\\")
    s = s.replace("'", "\\'")
    s = s.replace(":", "\\:")
    return s

def main() -> None:
    if len(sys.argv) < 5:
        print(f"Usage: {sys.argv[0]} <url> <x> <y> <fontfile> [chars_per_second]",
              file=sys.stderr)
        sys.exit(1)

    url             = sys.argv[1]
    x               = int(sys.argv[2])
    y               = int(sys.argv[3])
    fontfile        = sys.argv[4]
    chars_per_second = float(sys.argv[5]) if len(sys.argv) > 5 else 8.0

    parts = []
    total = len(url)

    for i in range(1, total + 1):
        t_start = round((i - 1) / chars_per_second, 3)
        slice_text = escape_drawtext(url[:i])
        part = (
            f"drawtext=fontfile={fontfile}"
            f":text='{slice_text}'"
            f":x={x}:y={y}"
            f":fontsize=15"
            f":fontcolor=#D0D0D0"
            f":enable='gte(t\\,{t_start})'"
        )
        parts.append(part)

    # Blinking cursor after the final character (approx 9 px per char)
    cursor_x = x + total * 9
    blink = (
        f"drawtext=fontfile={fontfile}"
        f":text='|'"
        f":x={cursor_x}:y={y}"
        f":fontsize=15"
        f":fontcolor=#D0D0D0"
        f":enable='lt(mod(t\\,1.0)\\,0.5)'"
    )
    parts.append(blink)

    print(",".join(parts))


if __name__ == "__main__":
    main()
