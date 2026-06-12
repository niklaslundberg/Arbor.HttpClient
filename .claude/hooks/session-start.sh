#!/bin/bash
# SessionStart hook for Claude Code on the web: installs the .NET SDK so
# builds and tests work inside the remote container.
#
# The default network policy blocks the official .NET download hosts
# (builds.dotnet.microsoft.com / download.visualstudio.microsoft.com), so the
# SDK is installed from the Ubuntu apt archive instead. global.json pins the
# 10.0.100 feature band with rollForward=latestFeature, which accepts both the
# Ubuntu-packaged 10.0.1xx SDKs and newer official feature bands.
set -euo pipefail

# Only needed in remote (web) sessions; local machines manage their own SDK.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Idempotent: skip when an SDK that satisfies global.json is already installed.
if command -v dotnet >/dev/null 2>&1 && dotnet --version >/dev/null 2>&1; then
  echo ".NET SDK already available: $(dotnet --version)"
else
  export DEBIAN_FRONTEND=noninteractive
  SUDO=""
  if [ "$(id -u)" -ne 0 ]; then
    SUDO="sudo"
  fi
  # Unrelated third-party apt sources in the image may fail to refresh; the
  # Ubuntu archive that carries dotnet-sdk-10.0 still updates, so do not abort.
  $SUDO apt-get update -qq || true
  $SUDO apt-get install -y -qq dotnet-sdk-10.0
  echo "Installed .NET SDK: $(dotnet --version)"
fi

# Warm the NuGet cache so the first build/test in the session is fast.
cd "$CLAUDE_PROJECT_DIR"
dotnet restore Arbor.HttpClient.slnx --locked-mode
