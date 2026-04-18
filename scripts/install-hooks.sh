#!/usr/bin/env bash
# install-hooks.sh
#
# Installs Git hooks for the Arbor.HttpClient repository.
# Run this once after cloning:
#
#   ./scripts/install-hooks.sh
#
# The pre-commit hook runs the full test suite and blocks the commit if any
# test fails.  To bypass in exceptional circumstances use:
#
#   git commit --no-verify

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HOOKS_DIR="$REPO_ROOT/.git/hooks"
HOOK_FILE="$HOOKS_DIR/pre-commit"

if [ ! -d "$HOOKS_DIR" ]; then
    echo "ERROR: .git/hooks directory not found. Are you inside a Git repository?" >&2
    exit 1
fi

cat > "$HOOK_FILE" << 'EOF'
#!/usr/bin/env bash
# pre-commit – run the full test suite before every commit.
# Install via: ./scripts/install-hooks.sh
# Bypass (exceptional use only): git commit --no-verify

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"

echo "pre-commit: running tests..."
if dotnet test "$REPO_ROOT/Arbor.HttpClient.slnx" --nologo -v minimal; then
    echo "pre-commit: all tests passed."
else
    echo ""
    echo "pre-commit: tests FAILED – commit blocked."
    echo "Fix the failing tests, or use 'git commit --no-verify' to bypass (use sparingly)."
    exit 1
fi
EOF

chmod +x "$HOOK_FILE"
echo "Git pre-commit hook installed at $HOOK_FILE"
