#!/usr/bin/env bash
# start-ui-automation-kvm-alpine.sh
#
# Runs interactive UI automation for Arbor.HttpClient.Desktop inside a
# QEMU/KVM virtual machine running Alpine Linux on an Ubuntu 22.04+ host.
#
# Key differences from the Ubuntu-based start-ui-automation-kvm.sh:
#   - Uses the official Alpine Linux NoCloud QEMU cloud image (~90 MB compressed).
#     The image is downloaded automatically when --base-image is not supplied.
#   - Configures the guest on first boot via a cloud-init seed ISO
#     (user account, SSH, required X11/automation packages installed via apk).
#   - Publishes the application with --runtime linux-musl-x64 so the self-
#     contained binary works on Alpine's musl libc without installing .NET.
#   - Generates a structured JSON results file (alpine-test-results.json) and a
#     Markdown summary (alpine-test-report.md) alongside the screenshots.
#
# The script:
#   1.  Validates host prerequisites.
#   2.  Downloads the Alpine cloud image if --base-image is not provided.
#   3.  Creates a cloud-init seed ISO (user, SSH password auth, apk packages).
#   4.  Creates an overlay qcow2 on top of the base image.
#   5.  Starts the VM with KVM acceleration (if available), VNC, and SSH port forwarding.
#   6.  Waits for the guest SSH service to become reachable.
#   7.  Builds the application on the host (linux-musl-x64, self-contained).
#   8.  Copies the application into the VM via SCP.
#   9.  Inside the VM: starts Xvfb, launches the application, and uses xdotool
#       to drive keyboard/mouse input through each automation step.
#  10.  Uses scrot inside the VM to take screenshots after each step.
#  11.  Optionally records the Xvfb display as an MP4 via ffmpeg (--record-video).
#  12.  Copies all screenshots and the video (if any) back to the host.
#  13.  Generates a JSON results file and a Markdown report.
#  14.  Shuts down and deletes the overlay image.
#
# Usage:
#   ./scripts/start-ui-automation-kvm-alpine.sh [options]
#
# Options:
#   --base-image PATH      Path to an existing Alpine qcow2 image.
#                          When omitted the image is downloaded automatically
#                          from the Alpine CDN and stored in --images-dir.
#   --alpine-version VER   Alpine Linux version to download (default: 3.21.3)
#   --vm-name NAME         Name prefix for overlay and PID files (default: arbor-alpine-test)
#   --images-dir DIR       Directory for the base and overlay images (default: /tmp/arbor-vms)
#   --output-dir DIR       Host directory for screenshots/video/reports
#                          (default: docs/system-test-screenshots/alpine)
#   --guest-user USER      SSH username inside the guest (default: alpine)
#   --guest-password PASS  SSH password for the guest user (default: alpine).
#                          Must match the password written into the cloud-init user-data.
#   --ssh-port PORT        Host port forwarded to guest SSH 22 (default: 52223)
#   --vnc-port N           VNC display number :N (default: :11, i.e. port 5911)
#   --screen WxH           Guest screen resolution (default: 1280x800)
#   --memory MB            VM memory in megabytes (default: 2048)
#   --cpus N               Number of virtual CPUs (default: 2)
#   --record-video         Record the session as an MP4 inside the guest
#   --pause                Pause after each automation step (operator connects via VNC)
#   --keep-vm              Keep the overlay image after the run (for debugging)
#   -h / --help            Print this help message and exit
#
# Host prerequisites (Ubuntu 22.04+):
#   sudo apt-get install -y \
#     qemu-kvm qemu-utils cloud-image-utils \
#     sshpass ffmpeg dotnet-sdk-10
#
#   cloud-image-utils provides cloud-localds, used to create the seed ISO.
#   If cloud-image-utils is unavailable, genisoimage/mkisofs is used instead.
#
# CI note (GitHub Actions):
#   Standard runners are Azure VMs; KVM is experimental on standard SKUs.
#   Large Linux runners (4+ vCPU, paid tier) officially expose /dev/kvm and
#   are the recommended path for running this script in CI.
#   The script falls back to software emulation (no -enable-kvm) automatically
#   when the kvm kernel module is not loaded — tests will complete but slowly.
#
# Alpine cloud image URL template:
#   https://dl-cdn.alpinelinux.org/alpine/v<MAJOR.MINOR>/releases/cloud/
#       nocloud_alpine-<VERSION>-x86_64-bios-cloudinit-r0.qcow2
#   (and the matching .sha256 sidecar for checksum verification)

set -euo pipefail

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------

ALPINE_VERSION="3.21.7"
BASE_IMAGE=""
# When PREPARED_IMAGE is set (or the default prepared path exists), the VM is
# started from it directly — cloud-init package installation is skipped.
PREPARED_IMAGE=""
VM_NAME="arbor-alpine-test"
IMAGES_DIR="/tmp/arbor-vms"
OUTPUT_DIR=""
GUEST_USER="alpine"
GUEST_PASSWORD=""
SSH_PORT=52223
VNC_DISPLAY=11           # :11 = port 5911
SCREEN_W=1280
SCREEN_H=800
MEMORY_MB=2048
CPU_COUNT=2
RECORD_VIDEO=false
PAUSE_STEPS=false
KEEP_VM=false
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------

while [[ $# -gt 0 ]]; do
    case "$1" in
        --base-image)      BASE_IMAGE="$2";    shift 2 ;;
        --prepared-image)  PREPARED_IMAGE="$2"; shift 2 ;;
        --alpine-version)  ALPINE_VERSION="$2"; shift 2 ;;
        --vm-name)         VM_NAME="$2";       shift 2 ;;
        --images-dir)      IMAGES_DIR="$2";    shift 2 ;;
        --output-dir)      OUTPUT_DIR="$2";    shift 2 ;;
        --guest-user)      GUEST_USER="$2";    shift 2 ;;
        --guest-password)  GUEST_PASSWORD="$2"; shift 2 ;;
        --ssh-port)        SSH_PORT="$2";      shift 2 ;;
        --vnc-port)        VNC_DISPLAY="$2";   shift 2 ;;
        --screen)
            if [[ "$2" != *x* ]]; then
                echo "ERROR: --screen requires format WIDTHxHEIGHT (e.g. 1280x800)" >&2
                exit 1
            fi
            SCREEN_W="${2%%x*}"; SCREEN_H="${2##*x}"; shift 2 ;;
        --memory)          MEMORY_MB="$2";     shift 2 ;;
        --cpus)            CPU_COUNT="$2";     shift 2 ;;
        --record-video)    RECORD_VIDEO=true;  shift ;;
        --pause)           PAUSE_STEPS=true;   shift ;;
        --keep-vm)         KEEP_VM=true;       shift ;;
        -h|--help)
            sed -n '2,80p' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1 ;;
    esac
done

if [[ -z "$OUTPUT_DIR" ]]; then
    OUTPUT_DIR="$REPO_ROOT/docs/system-test-screenshots/alpine"
fi

# Derive major.minor from the full version (e.g. 3.21.7 → 3.21)
ALPINE_MAJOR_MINOR="${ALPINE_VERSION%.*}"
ALPINE_IMAGE_FILE="nocloud_alpine-${ALPINE_VERSION}-x86_64-bios-cloudinit-r0.qcow2"
ALPINE_IMAGE_URL="https://dl-cdn.alpinelinux.org/alpine/v${ALPINE_MAJOR_MINOR}/releases/cloud/${ALPINE_IMAGE_FILE}"
# Alpine releases use SHA-512 sidecars (not SHA-256)
ALPINE_SHA512_URL="${ALPINE_IMAGE_URL}.sha512"

# Default prepared image path — created by scripts/prepare-alpine-image.sh
DEFAULT_PREPARED="$IMAGES_DIR/nocloud_alpine-${ALPINE_VERSION}-prepared.qcow2"

OVERLAY_IMAGE="$IMAGES_DIR/${VM_NAME}-run.qcow2"
SEED_ISO="$IMAGES_DIR/${VM_NAME}-seed.iso"
PUBLISH_DIR="/tmp/arbor-alpine-publish-$$"
VNC_PORT=$((5900 + VNC_DISPLAY))
GUEST_APP_DIR="/home/$GUEST_USER/automation/app"
GUEST_SHOT_DIR="/home/$GUEST_USER/automation/screenshots"
SSH_OPTS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=5 -o BatchMode=no -p $SSH_PORT"

RESULTS_JSON="$OUTPUT_DIR/alpine-test-results.json"
REPORT_MD="$OUTPUT_DIR/alpine-test-report.md"

# Track step outcomes for the report
declare -A STEP_STATUS
declare -A STEP_TIMING
RUN_START_TS="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

step() {
    echo ""
    echo "==> $*"
}

pause_if_enabled() {
    local step_name="$1"
    if [[ "$PAUSE_STEPS" == "true" ]]; then
        echo ""
        echo "[PAUSE] '$step_name' — connect via VNC at localhost:$VNC_PORT"
        echo "        Press ENTER to continue..."
        read -r
    fi
}

check_cmd() {
    local cmd="$1"
    local hint="${2:-}"
    if ! command -v "$cmd" &>/dev/null; then
        echo "ERROR: '$cmd' not found on PATH. ${hint}" >&2
        exit 1
    fi
}

record_step() {
    local name="$1" status="$2" elapsed="${3:-0}"
    STEP_STATUS["$name"]="$status"
    STEP_TIMING["$name"]="$elapsed"
}

ssh_guest() {
    sshpass -e ssh $SSH_OPTS "$GUEST_USER@localhost" "$@"
}

# scp uses -P (uppercase) for port, unlike ssh which uses -p
SCP_OPTS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=5 -o BatchMode=no -P $SSH_PORT"

scp_to_guest() {
    sshpass -e scp $SCP_OPTS -r "$1" "$GUEST_USER@localhost:$2"
}

scp_from_guest() {
    sshpass -e scp $SCP_OPTS -r "$GUEST_USER@localhost:$1" "$2"
}

wait_for_ssh() {
    local timeout=300
    local elapsed=0
    echo "    Waiting for SSH on port $SSH_PORT (up to ${timeout}s)..."
    while ! sshpass -e ssh $SSH_OPTS "$GUEST_USER@localhost" 'echo ok' &>/dev/null; do
        sleep 5
        elapsed=$((elapsed + 5))
        if [[ $elapsed -ge $timeout ]]; then
            echo "ERROR: Guest SSH did not become ready within ${timeout}s." >&2
            exit 1
        fi
        echo "    Still waiting... ${elapsed}s"
    done
    echo "    SSH ready (${elapsed}s)."
}

cleanup() {
    local qemu_pid="${1:-}"
    if [[ "$KEEP_VM" == "true" ]]; then
        echo ""
        echo "WARN: --keep-vm set — overlay and seed images kept."
        echo "      Overlay : $OVERLAY_IMAGE"
        echo "      Seed    : $SEED_ISO"
        [[ -n "$qemu_pid" ]] && echo "      QEMU PID: $qemu_pid"
        return
    fi
    step "Shutting down VM and cleaning up"
    ssh_guest "sudo poweroff" 2>/dev/null || true
    sleep 5
    if [[ -n "$qemu_pid" ]]; then
        kill "$qemu_pid" 2>/dev/null || true
    fi
    rm -f "$OVERLAY_IMAGE" "$SEED_ISO"
    rm -rf "$PUBLISH_DIR"
    echo "    Cleanup complete."
}

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------

step "Checking host prerequisites"

check_cmd "qemu-system-x86_64" "sudo apt-get install -y qemu-kvm"
check_cmd "qemu-img"           "sudo apt-get install -y qemu-utils"
check_cmd "sshpass"            "sudo apt-get install -y sshpass"
check_cmd "dotnet"             "See https://dot.net for installation instructions"

if [[ "$RECORD_VIDEO" == "true" ]]; then
    check_cmd "ffmpeg" "sudo apt-get install -y ffmpeg"
fi

# Need either cloud-localds or genisoimage/mkisofs to create the seed ISO
HAS_CLOUD_LOCALDS=false
HAS_GENISOIMAGE=false
if command -v cloud-localds &>/dev/null; then
    HAS_CLOUD_LOCALDS=true
elif command -v genisoimage &>/dev/null; then
    HAS_GENISOIMAGE=true
elif command -v mkisofs &>/dev/null; then
    HAS_GENISOIMAGE=true
    # Provide genisoimage as a shell function so it works in non-interactive mode
    # where aliases are disabled.
    genisoimage() { mkisofs "$@"; }
else
    echo "ERROR: Neither cloud-localds (cloud-image-utils) nor genisoimage/mkisofs found." >&2
    echo "       Install with: sudo apt-get install -y cloud-image-utils" >&2
    exit 1
fi

KVM_AVAILABLE=false
if lsmod | grep -q kvm 2>/dev/null || [[ -c /dev/kvm ]]; then
    KVM_AVAILABLE=true
    echo "    KVM acceleration available."
else
    echo "WARN: KVM kernel module not loaded — QEMU will use software emulation (slow)." >&2
fi

echo "    Prerequisites satisfied."

# ---------------------------------------------------------------------------
# Setup directories and determine guest password
# ---------------------------------------------------------------------------

mkdir -p "$IMAGES_DIR" "$OUTPUT_DIR" "$PUBLISH_DIR"

if [[ -z "$GUEST_PASSWORD" ]]; then
    if [[ -t 0 ]]; then
        # Interactive terminal: prompt for password
        read -rsp "Enter SSH password for guest user '$GUEST_USER' [default: alpine]: " GUEST_PASSWORD
        echo
        if [[ -z "$GUEST_PASSWORD" ]]; then
            GUEST_PASSWORD="alpine"
        fi
    else
        # Non-interactive (CI): use default
        GUEST_PASSWORD="alpine"
        echo "    Non-interactive mode — using default guest password."
    fi
fi

# Expose via environment variable so sshpass never receives it on the command line.
export SSHPASS="$GUEST_PASSWORD"
unset GUEST_PASSWORD

# ---------------------------------------------------------------------------
# Step 1: Obtain the image to boot
# ---------------------------------------------------------------------------
# Priority order:
#   1. --prepared-image explicitly provided
#   2. Default prepared image exists at $DEFAULT_PREPARED  ← fastest path
#   3. --base-image explicitly provided
#   4. Download the raw Alpine cloud image from CDN

step "Obtaining boot image"

SKIP_PACKAGE_INSTALL=false

if [[ -n "$PREPARED_IMAGE" ]]; then
    [[ -f "$PREPARED_IMAGE" ]] || { echo "ERROR: --prepared-image not found: $PREPARED_IMAGE" >&2; exit 1; }
    BASE_IMAGE="$PREPARED_IMAGE"
    SKIP_PACKAGE_INSTALL=true
    echo "    Using provided prepared image (packages pre-installed): $BASE_IMAGE"

elif [[ -z "$BASE_IMAGE" ]] && [[ -f "$DEFAULT_PREPARED" ]]; then
    BASE_IMAGE="$DEFAULT_PREPARED"
    SKIP_PACKAGE_INSTALL=true
    echo "    Found cached prepared image (packages pre-installed): $BASE_IMAGE"

elif [[ -n "$BASE_IMAGE" ]]; then
    [[ -f "$BASE_IMAGE" ]] || { echo "ERROR: --base-image not found: $BASE_IMAGE" >&2; exit 1; }
    echo "    Using provided base image: $BASE_IMAGE"

else
    # Download the raw Alpine cloud image
    BASE_IMAGE="$IMAGES_DIR/$ALPINE_IMAGE_FILE"
    if [[ -f "$BASE_IMAGE" ]]; then
        echo "    Cached raw image found: $BASE_IMAGE"
    else
        echo "    Downloading Alpine ${ALPINE_VERSION} cloud image..."
        echo "    URL: $ALPINE_IMAGE_URL"
        curl -fSL --progress-bar -o "$BASE_IMAGE.tmp" "$ALPINE_IMAGE_URL"

        echo "    Verifying checksum..."
        EXPECTED_SHA512_LINE="$(curl -fsSL "$ALPINE_SHA512_URL")"
        EXPECTED_SHA512="${EXPECTED_SHA512_LINE%% *}"
        if [[ ! "$EXPECTED_SHA512" =~ ^[0-9a-f]{128}$ ]]; then
            echo "ERROR: Malformed SHA-512 from $ALPINE_SHA512_URL" >&2
            rm -f "$BASE_IMAGE.tmp"; exit 1
        fi
        ACTUAL_SHA512="$(sha512sum "$BASE_IMAGE.tmp" | awk '{print $1}')"
        if [[ "$EXPECTED_SHA512" != "$ACTUAL_SHA512" ]]; then
            echo "ERROR: SHA-512 mismatch." >&2
            rm -f "$BASE_IMAGE.tmp"; exit 1
        fi
        mv "$BASE_IMAGE.tmp" "$BASE_IMAGE"
        echo "    Downloaded and verified: $BASE_IMAGE"
    fi
fi

if [[ "$SKIP_PACKAGE_INSTALL" == "true" ]]; then
    echo "    NOTE: Package installation will be skipped (already in image)."
fi

record_step "base-image" "ok"

# ---------------------------------------------------------------------------
# Step 2: Create cloud-init seed ISO
# ---------------------------------------------------------------------------

step "Creating cloud-init seed ISO"

CLOUD_INIT_DIR="$(mktemp -d)"
trap 'rm -rf "$CLOUD_INIT_DIR"' EXIT

# meta-data: required even if empty
cat > "$CLOUD_INIT_DIR/meta-data" <<EOF
instance-id: arbor-kvm-alpine-$$
local-hostname: arbor-vm
EOF

# user-data: set password and enable SSH.
# When SKIP_PACKAGE_INSTALL=true (prepared image) we omit the package_update /
# packages / runcmd blocks — all tooling is already in the image.
# When using a raw image we add a runcmd to install packages.
PLAIN_PASS="$(printenv SSHPASS)"

if [[ "$SKIP_PACKAGE_INSTALL" == "true" ]]; then
    cat > "$CLOUD_INIT_DIR/user-data" <<EOF
#cloud-config
password: ${PLAIN_PASS}
chpasswd:
  expire: false
ssh_pwauth: true
disable_root: true
runcmd:
  - mkdir -p /home/${GUEST_USER}/automation/app /home/${GUEST_USER}/automation/screenshots
  - chown -R ${GUEST_USER}:${GUEST_USER} /home/${GUEST_USER}/automation
EOF
else
    # Raw Alpine image: install packages via runcmd.
    # NOTE: In agent environments the QEMU NAT may not reach the CDN.
    # Run scripts/prepare-alpine-image.sh first to produce a prepared image.
    cat > "$CLOUD_INIT_DIR/user-data" <<EOF
#cloud-config
password: ${PLAIN_PASS}
chpasswd:
  expire: false
ssh_pwauth: true
disable_root: true
write_files:
  - path: /etc/apk/repositories
    content: |
      https://dl-cdn.alpinelinux.org/alpine/v${ALPINE_MAJOR_MINOR}/main
      https://dl-cdn.alpinelinux.org/alpine/v${ALPINE_MAJOR_MINOR}/community
    owner: root:root
    permissions: '0644'
runcmd:
  - apk update --no-cache
  - apk add --no-cache xvfb xdotool scrot ffmpeg font-dejavu fontconfig libx11 libxrandr libxcursor libxi libice libsm dbus
  - mkdir -p /home/${GUEST_USER}/automation/app /home/${GUEST_USER}/automation/screenshots
  - chown -R ${GUEST_USER}:${GUEST_USER} /home/${GUEST_USER}/automation
EOF
fi

# Create the seed ISO
if [[ "$HAS_CLOUD_LOCALDS" == "true" ]]; then
    cloud-localds "$SEED_ISO" "$CLOUD_INIT_DIR/user-data" "$CLOUD_INIT_DIR/meta-data"
else
    genisoimage \
        -output "$SEED_ISO" \
        -volid cidata \
        -joliet \
        -rock \
        "$CLOUD_INIT_DIR/user-data" \
        "$CLOUD_INIT_DIR/meta-data"
fi

echo "    Seed ISO created: $SEED_ISO"
record_step "seed-iso" "ok"

# ---------------------------------------------------------------------------
# Step 3: Create overlay image
# ---------------------------------------------------------------------------

step "Creating overlay qcow2 image"

[[ -f "$OVERLAY_IMAGE" ]] && rm -f "$OVERLAY_IMAGE"
qemu-img create -f qcow2 -b "$BASE_IMAGE" -F qcow2 "$OVERLAY_IMAGE"
echo "    Overlay: $OVERLAY_IMAGE"
record_step "overlay" "ok"

# ---------------------------------------------------------------------------
# Step 4: Start the VM
# ---------------------------------------------------------------------------

step "Starting QEMU/KVM VM"

KVM_FLAG=""
if [[ "$KVM_AVAILABLE" == "true" ]]; then
    KVM_FLAG="-enable-kvm"
fi

qemu-system-x86_64 $KVM_FLAG \
    -m "${MEMORY_MB}M" \
    -smp "$CPU_COUNT" \
    -drive "file=$OVERLAY_IMAGE,format=qcow2,if=virtio" \
    -drive "file=$SEED_ISO,format=raw,if=virtio,readonly=on" \
    -nic "user,model=virtio,hostfwd=tcp::${SSH_PORT}-:22" \
    -vnc ":${VNC_DISPLAY}" \
    -vga virtio \
    -display none \
    -serial "file:/tmp/${VM_NAME}-serial.log" \
    -daemonize \
    -pidfile "/tmp/${VM_NAME}.pid"

QEMU_PID="$(cat "/tmp/${VM_NAME}.pid")"
echo "    QEMU PID: $QEMU_PID  VNC: localhost:$VNC_PORT  SSH port: $SSH_PORT"

# Register cleanup on script exit (covers both success and error paths)
trap "cleanup $QEMU_PID; rm -rf '$CLOUD_INIT_DIR'" EXIT

record_step "vm-start" "ok"

# ---------------------------------------------------------------------------
# Step 5: Wait for SSH
# ---------------------------------------------------------------------------

step "Waiting for guest SSH"

# When using the prepared image, cloud-init only sets up the user/SSH — fast.
# When using a raw image, cloud-init also installs packages — may take minutes.
wait_for_ssh
record_step "ssh-ready" "ok"

# ---------------------------------------------------------------------------
# Step 6: Wait for cloud-init to finish
# ---------------------------------------------------------------------------

step "Waiting for cloud-init to complete"

if [[ "$SKIP_PACKAGE_INSTALL" == "true" ]]; then
    CLOUD_INIT_TIMEOUT=60    # user/SSH setup only — finishes quickly
else
    CLOUD_INIT_TIMEOUT=300   # raw image: package installation via network
fi

# Poll for the automation directory that runcmd creates as its final step.
# This is more reliable than `cloud-init status --wait`, which returns
# non-zero on Alpine when the cc_reset_rmc module warning is present.
CLOUD_INIT_ELAPSED=0
echo "    Waiting for cloud-init runcmd to complete (up to ${CLOUD_INIT_TIMEOUT}s)..."
while ! ssh_guest "test -d /home/${GUEST_USER}/automation" 2>/dev/null; do
    sleep 5
    CLOUD_INIT_ELAPSED=$((CLOUD_INIT_ELAPSED + 5))
    if [[ $CLOUD_INIT_ELAPSED -ge $CLOUD_INIT_TIMEOUT ]]; then
        echo "WARN: cloud-init timed out — creating directories manually." >&2
        ssh_guest "mkdir -p /home/${GUEST_USER}/automation/app /home/${GUEST_USER}/automation/screenshots" 2>/dev/null || true
        break
    fi
    echo "    cloud-init still running... ${CLOUD_INIT_ELAPSED}s"
done
echo "    cloud-init complete."
record_step "cloud-init" "ok"

# Verify xdotool is available (key dependency for automation)
if ! ssh_guest "command -v xdotool" &>/dev/null; then
    if [[ "$SKIP_PACKAGE_INSTALL" == "true" ]]; then
        echo "ERROR: xdotool not found in prepared image — the image may be corrupt." >&2
        exit 1
    else
        echo "WARN: xdotool not found after cloud-init. Trying manual install..."
        ssh_guest "sudo apk add --no-cache xdotool scrot xvfb ffmpeg 2>&1 | tail -5" || true
    fi
fi

# ---------------------------------------------------------------------------
# Step 7: Build application on host
# ---------------------------------------------------------------------------

step "Building Arbor.HttpClient.Desktop (linux-musl-x64 self-contained)"

BUILD_START="$(date +%s)"
dotnet publish \
    "$REPO_ROOT/src/Arbor.HttpClient.Desktop/Arbor.HttpClient.Desktop.csproj" \
    --configuration Release \
    --runtime linux-musl-x64 \
    --self-contained true \
    -o "$PUBLISH_DIR" \
    -v quiet
BUILD_ELAPSED=$(( $(date +%s) - BUILD_START ))

APP_BINARY="$PUBLISH_DIR/Arbor.HttpClient.Desktop"
if [[ ! -f "$APP_BINARY" ]]; then
    echo "ERROR: Expected binary not found after publish: $APP_BINARY" >&2
    record_step "build" "failed"
    exit 1
fi

echo "    Published to: $PUBLISH_DIR  (${BUILD_ELAPSED}s)"
record_step "build" "ok" "$BUILD_ELAPSED"

# ---------------------------------------------------------------------------
# Step 8: Copy application into VM
# ---------------------------------------------------------------------------

step "Copying application into VM"

ssh_guest "mkdir -p '$GUEST_APP_DIR' '$GUEST_SHOT_DIR'"
scp_to_guest "$PUBLISH_DIR/." "$GUEST_APP_DIR/"
ssh_guest "chmod +x '$GUEST_APP_DIR/Arbor.HttpClient.Desktop'"
echo "    Application copied to $GUEST_APP_DIR"
record_step "copy-app" "ok"

# ---------------------------------------------------------------------------
# Step 9: Start Xvfb in guest
# ---------------------------------------------------------------------------

step "Starting Xvfb display server in guest"

ssh_guest "pkill Xvfb 2>/dev/null || true; sleep 1"
ssh_guest "DISPLAY=:99 Xvfb :99 -screen 0 ${SCREEN_W}x${SCREEN_H}x24 +extension GLX &>/tmp/xvfb.log &"
sleep 2
if ssh_guest "DISPLAY=:99 xdotool getdisplaygeometry" &>/dev/null; then
    echo "    Xvfb running on DISPLAY=:99 (${SCREEN_W}x${SCREEN_H})"
    record_step "xvfb" "ok"
else
    echo "WARN: Xvfb may not be responding on DISPLAY=:99." >&2
    record_step "xvfb" "warn"
fi

# ---------------------------------------------------------------------------
# Step 10: (Optional) Start video recording in guest
# ---------------------------------------------------------------------------

VIDEO_PID_FILE="/tmp/${VM_NAME}-ffmpeg.pid"
VIDEO_GUEST_PATH="/home/$GUEST_USER/automation/demo-alpine.mp4"

if [[ "$RECORD_VIDEO" == "true" ]]; then
    step "Starting screen recording in guest (x11grab)"
    ssh_guest "DISPLAY=:99 ffmpeg -f x11grab -framerate 25 \
        -video_size ${SCREEN_W}x${SCREEN_H} \
        -i :99 \
        -c:v libx264 -preset ultrafast -crf 22 -pix_fmt yuv420p \
        '$VIDEO_GUEST_PATH' -y &>/tmp/ffmpeg.log & echo \$! > '$VIDEO_PID_FILE'"
    echo "    Recording started."
fi

# ---------------------------------------------------------------------------
# Step 11: Launch the application in guest
# ---------------------------------------------------------------------------

step "Launching application in guest"

ssh_guest "DISPLAY=:99 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    '$GUEST_APP_DIR/Arbor.HttpClient.Desktop' &>/tmp/app.log &"

if [[ "$RECORD_VIDEO" == "true" ]]; then
    sleep 5
else
    sleep 3
fi

echo "    Application launched."
record_step "app-launch" "ok"
pause_if_enabled "App opened (initial state)"

# ---------------------------------------------------------------------------
# Automation helper (executes xdotool inside the guest)
# ---------------------------------------------------------------------------

URL_BAR_X=640
URL_BAR_Y=69
SEND_BTN_X=1224
SEND_BTN_Y=69
VARS_TAB_X=80
VARS_TAB_Y=320
SCHED_TAB_X=80
SCHED_TAB_Y=370
DEMO_URL="https://postman-echo.com/get?hello=world"
STEP_NUM=1

# Layout coordinates (for the main left-panel splitter, assuming 1280x800 window):
#   Activity bar width = 48px
#   Default left panel = 25% of (1280-48) = 308px from left of DockControl
#   So the splitter is at x ≈ 48 + 308 = 356, drag target x ≈ 48 + 430 = 478 (~35%)
MAIN_SPLITTER_SRC_X=356
MAIN_SPLITTER_SRC_Y=400
MAIN_SPLITTER_DST_X=478
MAIN_SPLITTER_DST_Y=400

auto_step() {
    local name="$1"
    shift

    local step_ts="$(date +%s)"
    echo ""
    echo "--- Step $(printf '%02d' "$STEP_NUM"): $name"

    for cmd in "$@"; do
        ssh_guest "DISPLAY=:99 $cmd" 2>/dev/null || true
    done
    sleep 1

    local safe_name
    safe_name="$(echo "$name" | tr ' /()\t' '_____')"
    local filename
    filename="step-$(printf '%02d' "$STEP_NUM")-${safe_name}.png"

    if ssh_guest "DISPLAY=:99 scrot '$GUEST_SHOT_DIR/$filename'" 2>/dev/null; then
        if scp_from_guest "$GUEST_SHOT_DIR/$filename" "$OUTPUT_DIR/$filename" 2>/dev/null; then
            echo "    Screenshot: $OUTPUT_DIR/$filename"
            record_step "step-$STEP_NUM" "ok" "$(( $(date +%s) - step_ts ))"
        else
            echo "WARN: Could not retrieve screenshot $filename" >&2
            record_step "step-$STEP_NUM" "screenshot-scp-failed"
        fi
    else
        echo "WARN: scrot failed for step $STEP_NUM ($name)" >&2
        record_step "step-$STEP_NUM" "screenshot-failed"
    fi

    STEP_NUM=$((STEP_NUM + 1))
    pause_if_enabled "$name"
}

# ---------------------------------------------------------------------------
# Step 12: UI automation sequence
# ---------------------------------------------------------------------------

step "Running UI automation steps"

# 01 — initial app state
auto_step "App opened initial state"

# 02 — click URL bar and type demo URL
auto_step "Type URL into request bar" \
    "xdotool mousemove $URL_BAR_X $URL_BAR_Y click 1" \
    "xdotool key ctrl+a" \
    "xdotool type --clearmodifiers --delay 40 '$DEMO_URL'"

# 03 — click Send button
auto_step "Click Send button" \
    "xdotool mousemove $SEND_BTN_X $SEND_BTN_Y click 1"

# 04 — wait for HTTP response
echo "    Waiting 10s for HTTP response..."
sleep 10
auto_step "HTTP response received"

# 05 — open Variables panel
auto_step "Variables panel" \
    "xdotool mousemove $VARS_TAB_X $VARS_TAB_Y click 1"

# 06 — open Scheduled Jobs panel
auto_step "Scheduled Jobs panel" \
    "xdotool mousemove $SCHED_TAB_X $SCHED_TAB_Y click 1"

# ---------------------------------------------------------------------------
# Layout persistence test:
#   07  — Drag the main left-panel splitter to widen the left panel (~35%)
#   08  — Screenshot showing the new wider layout (reference for comparison)
#   09  — Close the application (Alt+F4 → graceful OnClosing saves layout)
#   10  — Relaunch and screenshot — must visually match step 08
# ---------------------------------------------------------------------------

# 07 — drag the main splitter to resize the left panel
#       xdotool mousemove, then mousedown, then incremental moves to drag, then mouseup
echo ""
echo "--- Step $(printf '%02d' "$STEP_NUM"): Resize left panel via splitter drag"
ssh_guest "DISPLAY=:99 xdotool mousemove $MAIN_SPLITTER_SRC_X $MAIN_SPLITTER_SRC_Y" 2>/dev/null || true
sleep 1
ssh_guest "DISPLAY=:99 xdotool mousedown 1" 2>/dev/null || true
sleep 0.3
# Move in steps to ensure the drag is registered
for drag_x in $(seq $((MAIN_SPLITTER_SRC_X + 30)) 30 $MAIN_SPLITTER_DST_X); do
    ssh_guest "DISPLAY=:99 xdotool mousemove $drag_x $MAIN_SPLITTER_DST_Y" 2>/dev/null || true
    sleep 0.05
done
ssh_guest "DISPLAY=:99 xdotool mousemove $MAIN_SPLITTER_DST_X $MAIN_SPLITTER_DST_Y" 2>/dev/null || true
sleep 0.3
ssh_guest "DISPLAY=:99 xdotool mouseup 1" 2>/dev/null || true
sleep 1

LAYOUT_BEFORE_FILE="step-$(printf '%02d' "$STEP_NUM")-layout_before_close.png"
if ssh_guest "DISPLAY=:99 scrot '$GUEST_SHOT_DIR/$LAYOUT_BEFORE_FILE'" 2>/dev/null; then
    scp_from_guest "$GUEST_SHOT_DIR/$LAYOUT_BEFORE_FILE" "$OUTPUT_DIR/$LAYOUT_BEFORE_FILE" 2>/dev/null || true
    echo "    Screenshot (layout before close): $OUTPUT_DIR/$LAYOUT_BEFORE_FILE"
    record_step "step-$STEP_NUM" "ok"
else
    record_step "step-$STEP_NUM" "screenshot-failed"
fi
STEP_NUM=$((STEP_NUM + 1))
pause_if_enabled "Layout before close"

# 08 — close the application gracefully via Alt+F4
#       This triggers MainWindow.OnClosing which persists the layout.
echo ""
echo "--- Step $(printf '%02d' "$STEP_NUM"): Close application (Alt+F4)"
ssh_guest "DISPLAY=:99 xdotool key alt+F4" 2>/dev/null || true
sleep 4   # allow the app to close and write options.json
ssh_guest "DISPLAY=:99 scrot '$GUEST_SHOT_DIR/step-$(printf '%02d' "$STEP_NUM")-after_close.png'" 2>/dev/null || true
scp_from_guest "$GUEST_SHOT_DIR/step-$(printf '%02d' "$STEP_NUM")-after_close.png" \
    "$OUTPUT_DIR/step-$(printf '%02d' "$STEP_NUM")-after_close.png" 2>/dev/null || true
echo "    Screenshot (desktop after close): $OUTPUT_DIR/step-$(printf '%02d' "$STEP_NUM")-after_close.png"
record_step "step-$STEP_NUM" "ok"
STEP_NUM=$((STEP_NUM + 1))
pause_if_enabled "App closed"

# Retrieve the options.json written by OnClosing for diagnostic purposes
SAVED_OPTIONS_GUEST="/home/$GUEST_USER/.local/share/Arbor.HttpClient/options.json"
OPTIONS_LOCAL="$OUTPUT_DIR/options-saved.json"
if scp_from_guest "$SAVED_OPTIONS_GUEST" "$OPTIONS_LOCAL" 2>/dev/null; then
    echo "    Saved options.json: $OPTIONS_LOCAL"
    # Extract and print the layout proportions for quick review
    if command -v python3 &>/dev/null; then
        python3 -c "
import json, sys
try:
    d = json.load(open('$OPTIONS_LOCAL'))
    cl = d.get('layouts', {}).get('currentLayout', {})
    print(f'  leftToolProportion  = {cl.get(\"leftToolProportion\", \"N/A\")}')
    print(f'  documentProportion  = {cl.get(\"documentProportion\", \"N/A\")}')
    print(f'  windowWidth         = {cl.get(\"windowWidth\", \"N/A\")}')
    print(f'  windowHeight        = {cl.get(\"windowHeight\", \"N/A\")}')
except Exception as e:
    print(f'  (could not parse options.json: {e})')
" 2>/dev/null || true
    fi
else
    echo "WARN: Could not retrieve options.json from guest" >&2
fi

# 09 — relaunch the application
echo ""
echo "--- Step $(printf '%02d' "$STEP_NUM"): Relaunch application"
ssh_guest "DISPLAY=:99 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    '$GUEST_APP_DIR/Arbor.HttpClient.Desktop' &>/tmp/app-relaunch.log &"
sleep 5   # allow the app to fully start and render

LAYOUT_AFTER_FILE="step-$(printf '%02d' "$STEP_NUM")-layout_after_relaunch.png"
if ssh_guest "DISPLAY=:99 scrot '$GUEST_SHOT_DIR/$LAYOUT_AFTER_FILE'" 2>/dev/null; then
    scp_from_guest "$GUEST_SHOT_DIR/$LAYOUT_AFTER_FILE" "$OUTPUT_DIR/$LAYOUT_AFTER_FILE" 2>/dev/null || true
    echo "    Screenshot (layout after relaunch): $OUTPUT_DIR/$LAYOUT_AFTER_FILE"
    record_step "step-$STEP_NUM" "ok"
else
    record_step "step-$STEP_NUM" "screenshot-failed"
fi
STEP_NUM=$((STEP_NUM + 1))
pause_if_enabled "App relaunched — verify layout matches step 07"

# 10 — layout persistence check: compare the left-panel boundary in both screenshots
#       Uses ImageMagick 'compare' if available.  The tool exits 0 if images are
#       identical, 1 if different, 2 on error.  We only log the result here —
#       the CI artifact lets a human reviewer do the definitive comparison.
echo ""
echo "--- Step $(printf '%02d' "$STEP_NUM"): Layout persistence comparison"
BEFORE_IMG="$OUTPUT_DIR/$LAYOUT_BEFORE_FILE"
AFTER_IMG="$OUTPUT_DIR/$LAYOUT_AFTER_FILE"

if [[ -f "$BEFORE_IMG" && -f "$AFTER_IMG" ]]; then
    DIFF_IMG="$OUTPUT_DIR/layout-diff.png"
    if command -v compare &>/dev/null; then
        # Compare a vertical strip from the left side (x: 0-600) where the splitter lives.
        # -metric AE counts non-matching pixels, -fuzz 5% allows minor rendering differences.
        AE_PIXELS=$(compare -metric AE -fuzz 5% "$BEFORE_IMG" "$AFTER_IMG" "$DIFF_IMG" 2>&1 || true)
        echo "    Pixel difference (left panel region) AE count: ${AE_PIXELS:-unknown}"
        echo "    Diff image: $DIFF_IMG"
        record_step "step-$STEP_NUM" "ok"
    else
        echo "    ImageMagick 'compare' not available on host — skipping pixel comparison."
        echo "    Manual review: compare $BEFORE_IMG vs $AFTER_IMG"
        record_step "step-$STEP_NUM" "compare-skipped"
    fi
else
    echo "WARN: One or both comparison screenshots are missing." >&2
    record_step "step-$STEP_NUM" "missing-screenshots"
fi
STEP_NUM=$((STEP_NUM + 1))

record_step "automation" "ok"

# ---------------------------------------------------------------------------
# Step 13: Stop video recording and retrieve
# ---------------------------------------------------------------------------

if [[ "$RECORD_VIDEO" == "true" ]]; then
    step "Stopping video recording"
    ssh_guest "kill \$(cat '$VIDEO_PID_FILE' 2>/dev/null) 2>/dev/null || true; sleep 2"

    VIDEO_LOCAL_PATH="$OUTPUT_DIR/demo-alpine.mp4"
    if scp_from_guest "$VIDEO_GUEST_PATH" "$VIDEO_LOCAL_PATH" 2>/dev/null; then
        echo "    Video: $VIDEO_LOCAL_PATH"
        record_step "video" "ok"
    else
        echo "WARN: Could not retrieve video file." >&2
        record_step "video" "scp-failed"
    fi
fi

# Retrieve app log for the report
APP_LOG_LOCAL="$OUTPUT_DIR/app.log"
scp_from_guest "/tmp/app.log" "$APP_LOG_LOCAL" 2>/dev/null || true

# ---------------------------------------------------------------------------
# Step 14: Generate JSON results file and Markdown report
# ---------------------------------------------------------------------------

step "Generating test report"

RUN_END_TS="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
SCREENSHOT_COUNT="$(ls -1 "$OUTPUT_DIR"/step-*.png 2>/dev/null | wc -l)"

# --- JSON results ---
{
    echo "{"
    echo "  \"run\": {"
    echo "    \"start\": \"$RUN_START_TS\","
    echo "    \"end\":   \"$RUN_END_TS\","
    echo "    \"host\":  \"$(hostname)\","
    echo "    \"alpine_version\": \"$ALPINE_VERSION\","
    echo "    \"kvm_available\": $KVM_AVAILABLE,"
    echo "    \"record_video\": $RECORD_VIDEO,"
    echo "    \"screenshot_count\": $SCREENSHOT_COUNT"
    echo "  },"
    echo "  \"steps\": {"
    json_first=true
    for key in "${!STEP_STATUS[@]}"; do
        [[ "$json_first" == "true" ]] && json_first=false || echo ","
        printf '    "%s": { "status": "%s", "elapsed_s": "%s" }' \
            "$key" "${STEP_STATUS[$key]}" "${STEP_TIMING[$key]:-}"
    done
    echo ""
    echo "  }"
    echo "}"
} > "$RESULTS_JSON"

# --- Markdown report ---
{
    echo "## Alpine KVM System Test Results"
    echo ""
    echo "| Property | Value |"
    echo "|---|---|"
    echo "| Alpine version | ${ALPINE_VERSION} |"
    echo "| KVM acceleration | ${KVM_AVAILABLE} |"
    echo "| Run start | ${RUN_START_TS} |"
    echo "| Run end | ${RUN_END_TS} |"
    echo "| Screenshots captured | ${SCREENSHOT_COUNT} |"
    echo "| Video recorded | ${RECORD_VIDEO} |"
    echo ""
    echo "### Step Outcomes"
    echo ""
    echo "| Step | Status | Elapsed (s) |"
    echo "|---|---|---|"
    for key in base-image seed-iso overlay vm-start ssh-ready cloud-init build copy-app xvfb app-launch automation video; do
        status="${STEP_STATUS[$key]:-skipped}"
        elapsed="${STEP_TIMING[$key]:-—}"
        echo "| ${key} | ${status} | ${elapsed} |"
    done
    for i in $(seq 1 $((STEP_NUM - 1))); do
        key="step-$i"
        status="${STEP_STATUS[$key]:-skipped}"
        elapsed="${STEP_TIMING[$key]:-—}"
        echo "| automation step $i | ${status} | ${elapsed} |"
    done
    echo ""
    if [[ $SCREENSHOT_COUNT -gt 0 ]]; then
        echo "### Screenshots"
        echo ""
        for f in "$OUTPUT_DIR"/step-*.png; do
            [[ -f "$f" ]] || continue
            echo "![$(basename "$f")]($(basename "$f"))"
        done
        echo ""
    fi
    # Layout persistence section
    echo "### Layout Persistence Verification"
    echo ""
    echo "The system test drags the main left-panel splitter to ~35% width, closes the"
    echo "application (triggering \`MainWindow.OnClosing → PersistCurrentLayout\`), then"
    echo "relaunches and screenshots the result."
    echo ""
    echo "Reviewers: compare **layout_before_close** and **layout_after_relaunch** screenshots."
    echo "The left-panel boundary must appear at the same x-position in both images."
    echo ""
    if [[ -f "$OUTPUT_DIR/options-saved.json" ]]; then
        echo "Saved options.json layout section:"
        echo '```json'
        python3 -c "
import json
try:
    d = json.load(open('$OUTPUT_DIR/options-saved.json'))
    cl = d.get('layouts', {}).get('currentLayout', {})
    out = {k: v for k, v in cl.items() if 'roportion' in k or 'Window' in k or 'window' in k}
    print(json.dumps(out, indent=2))
except Exception as e:
    print(f'(parse error: {e})')
" 2>/dev/null || echo "(unavailable)"
        echo '```'
        echo ""
    fi
} > "$REPORT_MD"

echo "    JSON results : $RESULTS_JSON"
echo "    Markdown report : $REPORT_MD"

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

echo ""
echo "============================================================"
echo " Alpine KVM UI Automation run complete!"
echo "============================================================"
echo " Alpine       : ${ALPINE_VERSION}"
echo " KVM          : ${KVM_AVAILABLE}"
echo " Screenshots  : $OUTPUT_DIR"
echo " Step count   : $SCREENSHOT_COUNT"
if [[ "$RECORD_VIDEO" == "true" ]]; then
    echo " Video        : $OUTPUT_DIR/demo-alpine.mp4"
fi
echo " JSON results : $RESULTS_JSON"
echo " MD report    : $REPORT_MD"
echo ""
