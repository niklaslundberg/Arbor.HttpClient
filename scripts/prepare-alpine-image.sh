#!/usr/bin/env bash
# prepare-alpine-image.sh
#
# Builds a "prepared" Alpine Linux QEMU image that has all required
# X11/automation packages (Xvfb, xdotool, scrot, ffmpeg, ...) pre-installed.
#
# This script:
#   1.  Downloads the Alpine NoCloud QEMU cloud image (with SHA-512 check) if
#       --base-image is not provided.
#   2.  Creates a copy-on-write overlay on top of the base image.
#   3.  Expands the filesystem in the overlay to --disk-size (default 8 GiB).
#   4.  Starts a local caching HTTP proxy (scripts/apk-proxy.py) that fetches
#       packages from the Alpine CDN and caches them in --apk-cache-dir.
#   5.  Mounts the overlay via qemu-nbd and chroots into it.
#   6.  Configures apk to use the local proxy, then runs `apk add` for every
#       required package.  Because the proxy fetches via the host's network
#       (not the guest VM's NAT), this works even in environments where the
#       QEMU NAT cannot reach external CDNs.
#   7.  Restores real CDN URLs in /etc/apk/repositories.
#   8.  Unmounts and disconnects nbd.
#   9.  Converts the overlay into a standalone compressed qcow2 image
#       (--output-image) that can be used as a base for test runs without any
#       further package installation.
#
# Usage:
#   ./scripts/prepare-alpine-image.sh [options]
#
# Options:
#   --base-image PATH      Existing raw Alpine qcow2 to extend.
#                          When omitted the image is downloaded automatically.
#   --alpine-version VER   Alpine version to download (default: 3.21.7)
#   --images-dir DIR       Directory for downloaded/overlay images
#                          (default: /tmp/arbor-vms)
#   --apk-cache-dir DIR    Directory where the proxy caches downloaded .apk
#                          files (default: /tmp/alpine-mirror)
#   --output-image PATH    Destination for the prepared qcow2 image
#                          (default: /tmp/arbor-vms/nocloud_alpine-<VER>-prepared.qcow2)
#   --proxy-port PORT      Local HTTP port for the apk caching proxy
#                          (default: 8099)
#   --disk-size SIZE       Virtual disk size for the overlay (default: 8G)
#   --nbd-device DEV       nbd device to use (default: /dev/nbd0)
#   -h / --help            Print this help message
#
# Prerequisites (Ubuntu 22.04+):
#   sudo apt-get install -y qemu-utils qemu-nbd e2fsprogs python3
#   sudo modprobe nbd max_part=8
#
# Exit codes:
#   0   Success — prepared image written to --output-image
#   1   Prerequisite or argument error
#   2   Download or checksum failure
#   3   Package installation failure

set -euo pipefail

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------

ALPINE_VERSION="3.21.7"
BASE_IMAGE=""
IMAGES_DIR="/tmp/arbor-vms"
APK_CACHE_DIR="/tmp/alpine-mirror"
OUTPUT_IMAGE=""
PROXY_PORT=8099
DISK_SIZE="8G"
NBD_DEV="/dev/nbd0"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROXY_SCRIPT="$REPO_ROOT/scripts/apk-proxy.py"

# Packages to pre-install into the prepared image.
# These are the exact Alpine 3.21 package names (community + main repos).
PACKAGES=(
    xvfb          # Xvfb (virtual framebuffer X server) — community
    xdotool       # X11 keyboard/mouse automation — community
    scrot         # X11 screenshot capture — community
    ffmpeg        # Video recording / encoding — community
    font-dejavu   # DejaVu fonts for X11 rendering — main
    fontconfig    # Font configuration library — main
    libx11        # X11 client library — main
    libxrandr     # X RandR extension — main
    libxcursor    # X cursor management — main
    libxi         # X Input extension — main
    libice        # Inter-Client Exchange — main
    libsm         # X Session Management — main
    dbus          # D-Bus IPC daemon — main
)

# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------

while [[ $# -gt 0 ]]; do
    case "$1" in
        --base-image)     BASE_IMAGE="$2";    shift 2 ;;
        --alpine-version) ALPINE_VERSION="$2"; shift 2 ;;
        --images-dir)     IMAGES_DIR="$2";    shift 2 ;;
        --apk-cache-dir)  APK_CACHE_DIR="$2"; shift 2 ;;
        --output-image)   OUTPUT_IMAGE="$2";  shift 2 ;;
        --proxy-port)     PROXY_PORT="$2";    shift 2 ;;
        --disk-size)      DISK_SIZE="$2";     shift 2 ;;
        --nbd-device)     NBD_DEV="$2";       shift 2 ;;
        -h|--help)
            sed -n '2,55p' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *)
            echo "ERROR: Unknown option: $1" >&2
            exit 1 ;;
    esac
done

ALPINE_MAJOR_MINOR="${ALPINE_VERSION%.*}"
ALPINE_IMAGE_FILE="nocloud_alpine-${ALPINE_VERSION}-x86_64-bios-cloudinit-r0.qcow2"
ALPINE_IMAGE_URL="https://dl-cdn.alpinelinux.org/alpine/v${ALPINE_MAJOR_MINOR}/releases/cloud/${ALPINE_IMAGE_FILE}"
ALPINE_SHA512_URL="${ALPINE_IMAGE_URL}.sha512"

[[ -z "$OUTPUT_IMAGE" ]] && OUTPUT_IMAGE="$IMAGES_DIR/nocloud_alpine-${ALPINE_VERSION}-prepared.qcow2"

OVERLAY="$IMAGES_DIR/prepare-overlay-$$.qcow2"
MNT="/mnt/alpine-prepare-$$"
PROXY_PID=""

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

step() { echo ""; echo "==> $*"; }

cleanup() {
    echo ""
    echo "--- Cleanup ---"
    [[ -n "$PROXY_PID" ]] && kill "$PROXY_PID" 2>/dev/null || true

    # Unmount in reverse order
    for sub in proc sys dev; do
        mountpoint -q "$MNT/$sub" 2>/dev/null && sudo umount "$MNT/$sub" || true
    done
    mountpoint -q "$MNT" 2>/dev/null && sudo umount "$MNT" || true
    sudo qemu-nbd --disconnect "$NBD_DEV" 2>/dev/null || true
    rm -rf "$MNT" "$OVERLAY"
    echo "Cleanup done."
}
trap cleanup EXIT

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------

step "Checking prerequisites"

missing=()
for cmd in qemu-img qemu-nbd e2fsck resize2fs python3; do
    command -v "$cmd" &>/dev/null || missing+=("$cmd")
done
if [[ ${#missing[@]} -gt 0 ]]; then
    echo "ERROR: Missing commands: ${missing[*]}" >&2
    echo "       Install with: sudo apt-get install -y qemu-utils e2fsprogs python3" >&2
    exit 1
fi

if [[ ! -e "$NBD_DEV" ]]; then
    echo "    nbd module not loaded — loading now..."
    sudo modprobe nbd max_part=8
fi

if [[ ! -f "$PROXY_SCRIPT" ]]; then
    echo "ERROR: Proxy script not found: $PROXY_SCRIPT" >&2
    exit 1
fi

mkdir -p "$IMAGES_DIR" "$APK_CACHE_DIR"
echo "    Prerequisites satisfied."

# ---------------------------------------------------------------------------
# Step 1: Obtain base image
# ---------------------------------------------------------------------------

step "Obtaining Alpine Linux ${ALPINE_VERSION} base image"

if [[ -n "$BASE_IMAGE" ]]; then
    [[ -f "$BASE_IMAGE" ]] || { echo "ERROR: --base-image not found: $BASE_IMAGE" >&2; exit 1; }
    echo "    Using provided base image: $BASE_IMAGE"
else
    BASE_IMAGE="$IMAGES_DIR/$ALPINE_IMAGE_FILE"
    if [[ -f "$BASE_IMAGE" ]]; then
        echo "    Cached base image found: $BASE_IMAGE"
    else
        echo "    Downloading from Alpine CDN..."
        echo "    URL: $ALPINE_IMAGE_URL"
        curl -fSL --progress-bar -o "${BASE_IMAGE}.tmp" "$ALPINE_IMAGE_URL"

        EXPECTED="$(curl -fsSL "$ALPINE_SHA512_URL" | awk '{print $1}')"
        if [[ ! "$EXPECTED" =~ ^[0-9a-f]{128}$ ]]; then
            echo "ERROR: Malformed SHA-512 from $ALPINE_SHA512_URL" >&2
            rm -f "${BASE_IMAGE}.tmp"
            exit 2
        fi
        ACTUAL="$(sha512sum "${BASE_IMAGE}.tmp" | awk '{print $1}')"
        if [[ "$EXPECTED" != "$ACTUAL" ]]; then
            echo "ERROR: SHA-512 mismatch." >&2
            rm -f "${BASE_IMAGE}.tmp"
            exit 2
        fi
        mv "${BASE_IMAGE}.tmp" "$BASE_IMAGE"
        echo "    Downloaded and verified: $BASE_IMAGE"
    fi
fi

# ---------------------------------------------------------------------------
# Step 2: Create overlay and expand filesystem
# ---------------------------------------------------------------------------

step "Creating overlay and expanding filesystem to $DISK_SIZE"

qemu-img create -f qcow2 -b "$BASE_IMAGE" -F qcow2 "$OVERLAY" "$DISK_SIZE"

# Connect to nbd
sudo qemu-nbd --connect="$NBD_DEV" "$OVERLAY"
sleep 1

# Expand the filesystem (qemu-img resize only extends the virtual disk, not the fs)
echo "    Running e2fsck..."
sudo e2fsck -f -y "$NBD_DEV" 2>&1 | tail -3
echo "    Running resize2fs..."
sudo resize2fs "$NBD_DEV" 2>&1 | tail -2

# Mount
sudo mkdir -p "$MNT"
sudo mount "$NBD_DEV" "$MNT"
sudo mount --bind /proc "$MNT/proc"
sudo mount --bind /sys  "$MNT/sys"
sudo mount --bind /dev  "$MNT/dev"

FS_SIZE="$(df -h "$MNT" | awk 'NR==2 {print $2}')"
echo "    Filesystem size: $FS_SIZE"

# ---------------------------------------------------------------------------
# Step 3: Start caching proxy
# ---------------------------------------------------------------------------

step "Starting local APK caching proxy (port $PROXY_PORT)"

python3 "$PROXY_SCRIPT" "$APK_CACHE_DIR" "$PROXY_PORT" 2>/tmp/apk-proxy.log &
PROXY_PID=$!
sleep 2

if ! curl -fsI "http://127.0.0.1:$PROXY_PORT/" &>/dev/null; then
    echo "ERROR: Proxy did not start on port $PROXY_PORT. Check /tmp/apk-proxy.log" >&2
    cat /tmp/apk-proxy.log >&2
    exit 1
fi
echo "    Proxy PID: $PROXY_PID"

# Configure apk to use the local proxy
sudo sh -c "cat > '$MNT/etc/apk/repositories' << EOF
http://127.0.0.1:${PROXY_PORT}/v${ALPINE_MAJOR_MINOR}/main
http://127.0.0.1:${PROXY_PORT}/v${ALPINE_MAJOR_MINOR}/community
EOF"

# ---------------------------------------------------------------------------
# Step 4: Update apk index and install packages
# ---------------------------------------------------------------------------

step "Installing packages into the prepared image"

echo "    Updating package index..."
sudo chroot "$MNT" apk update 2>&1 | tail -3

echo "    Installing: ${PACKAGES[*]}"
PKG_INSTALL_EXIT=0
sudo chroot "$MNT" apk add --no-progress "${PACKAGES[@]}" 2>&1 | \
    grep -E "^(Installing|OK:|ERROR:)" || true

# Verify all expected binaries are present
echo ""
echo "    Verifying installed binaries..."
MISSING_BINS=()
for bin in Xvfb xdotool scrot ffmpeg; do
    if ! sudo chroot "$MNT" which "$bin" &>/dev/null; then
        MISSING_BINS+=("$bin")
    fi
done
if [[ ${#MISSING_BINS[@]} -gt 0 ]]; then
    echo "ERROR: Missing binaries after installation: ${MISSING_BINS[*]}" >&2
    exit 3
fi
echo "    All binaries verified: Xvfb xdotool scrot ffmpeg"

# ---------------------------------------------------------------------------
# Step 5: Restore real CDN repos, unmount, disconnect
# ---------------------------------------------------------------------------

step "Restoring CDN repos and unmounting"

sudo sh -c "cat > '$MNT/etc/apk/repositories' << EOF
https://dl-cdn.alpinelinux.org/alpine/v${ALPINE_MAJOR_MINOR}/main
https://dl-cdn.alpinelinux.org/alpine/v${ALPINE_MAJOR_MINOR}/community
EOF"

echo "    Disk usage: $(df -h "$MNT" | awk 'NR==2 {print $3 " used of " $2}')"

for sub in proc sys dev; do sudo umount "$MNT/$sub" 2>/dev/null || true; done
sudo umount "$MNT"
sudo qemu-nbd --disconnect "$NBD_DEV"
sleep 1

# Stop proxy — no longer needed after chroot is done
kill "$PROXY_PID" 2>/dev/null || true
PROXY_PID=""

# ---------------------------------------------------------------------------
# Step 6: Convert overlay to standalone prepared image
# ---------------------------------------------------------------------------

step "Converting overlay to standalone prepared image"

echo "    Output: $OUTPUT_IMAGE"
echo "    (This merges base + overlay into one self-contained qcow2 file...)"
time qemu-img convert -O qcow2 -c "$OVERLAY" "$OUTPUT_IMAGE"

IMAGE_SIZE="$(du -sh "$OUTPUT_IMAGE" | awk '{print $1}')"
echo ""
echo "============================================================"
echo " Prepared image ready!"
echo "============================================================"
echo " Image   : $OUTPUT_IMAGE"
echo " Size    : $IMAGE_SIZE"
echo " Packages: ${#PACKAGES[@]} groups (${PACKAGES[*]})"
echo " APK cache: $(du -sh "$APK_CACHE_DIR" 2>/dev/null | awk '{print $1}') in $(find "$APK_CACHE_DIR" -name '*.apk' 2>/dev/null | wc -l) .apk files"
echo ""
echo " To use in test runs:"
echo "   ./scripts/start-ui-automation-kvm-alpine.sh \\"
echo "       --base-image $OUTPUT_IMAGE"
echo ""
