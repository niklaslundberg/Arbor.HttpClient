#!/usr/bin/env bash
# start-ui-automation-kvm.sh
#
# Runs interactive UI automation for Arbor.HttpClient.Desktop inside a
# QEMU/KVM virtual machine on an Ubuntu 22.04+ host.
#
# The script:
#   1. Validates host prerequisites (kvm, qemu, sshpass, xdotool, scrot, ffmpeg, dotnet).
#   2. Creates an overlay qcow2 image on top of a base Ubuntu Desktop cloud image so
#      the base is never modified.
#   3. Starts the VM with a VNC display and an SSH-forwarded port.
#   4. Waits for the guest SSH service to be reachable.
#   5. Builds the application (Linux x64, self-contained) on the host and copies it
#      into the VM via SCP.
#   6. Inside the VM: starts Xvfb, launches the application, and uses xdotool to
#      drive keyboard and mouse input through each automation step.
#   7. Uses scrot inside the VM to take screenshots after each step.
#   8. Optionally records the Xvfb display as an MP4 via ffmpeg inside the VM
#      (requires --record-video).
#   9. Copies all screenshots and the video (if any) back to the host output directory.
#  10. Shuts down and deletes the overlay image.
#
# Usage:
#   ./scripts/start-ui-automation-kvm.sh [options]
#
# Options:
#   --base-image PATH      Path to the base Ubuntu qcow2 image [REQUIRED]
#                          Download: https://cloud-images.ubuntu.com/jammy/current/jammy-server-cloudimg-amd64.img
#   --vm-name NAME         Name prefix for the overlay image (default: arbor-ui-test)
#   --images-dir DIR       Directory to store the overlay image (default: /tmp/arbor-vms)
#   --output-dir DIR       Host directory for screenshots/video (default: docs/screenshots)
#   --guest-user USER      SSH username inside the guest (default: ubuntu)
#   --guest-password PASS  SSH password for the guest user (default: ubuntu)
#   --ssh-port PORT        Host port forwarded to guest SSH (default: 52222)
#   --vnc-port N           VNC display number, :N (default: :10, i.e. port 5910)
#   --screen WxH           Guest screen resolution (default: 1280x800)
#   --memory MB            VM memory in megabytes (default: 4096)
#   --cpus N               Number of virtual CPUs (default: 2)
#   --record-video         Record the automation as an MP4 inside the guest
#   --pause                Pause after each step (prints message; operator connects via VNC)
#   --keep-vm              Keep the overlay image after the run (for debugging)
#   -h / --help            Print this help message
#
# Host prerequisites:
#   sudo apt-get install -y \
#     qemu-kvm libvirt-daemon-system cloud-image-utils \
#     sshpass xdotool scrot ffmpeg \
#     dotnet-sdk-10
#
# CI note (GitHub Actions):
#   All GitHub-hosted runners run on Azure VMs. The official docs state: "While nested
#   virtualization is technically possible while using runners, it is not officially
#   supported. Any use of nested VMs is experimental and done at your own risk."
#   (https://docs.github.com/en/actions/concepts/runners/github-hosted-runners)
#   In practice, standard runners are unreliable for nested KVM. GitHub's LARGE Linux
#   runners (4+ vCPU, paid tier) officially expose /dev/kvm and are the recommended path
#   for running this script in CI without a self-hosted runner.
#   Add `runs-on: ubuntu-latest-4-cores` (or equivalent large SKU) to your workflow job.
#   See docs/vm-ui-automation.md sub-task 7 for the full CI integration plan.
#
# Guest prerequisites:
#   The base image must have:
#   - A desktop environment (GNOME or XFCE) or just an X11 stack (xorg, openbox)
#   - An SSH server (openssh-server) running on port 22
#   - A known user/password (see --guest-user / --guest-password)
#   - .NET 10 runtime (or SDK) installed (the script installs it if missing)
#
# Prepare a base image (example using Ubuntu 22.04 cloud image + cloud-init):
#   # Download base
#   wget https://cloud-images.ubuntu.com/jammy/current/jammy-server-cloudimg-amd64.img \
#       -O /tmp/ubuntu-base.img
#   # Resize to 20 GB
#   qemu-img resize /tmp/ubuntu-base.img 20G
#   # Create a cloud-init config to set the password:
#   cat > /tmp/user-data.yaml <<'EOF'
#   #cloud-config
#   users:
#     - name: ubuntu
#       plain_text_passwd: ubuntu
#       lock_passwd: false
#       sudo: ALL=(ALL) NOPASSWD:ALL
#   packages: [openssh-server, xorg, openbox, scrot, xdotool, ffmpeg]
#   EOF
#   cloud-localds /tmp/cloud-init.iso /tmp/user-data.yaml
#   # Boot once to apply cloud-init, then shut down:
#   qemu-system-x86_64 -enable-kvm -m 2048 -smp 2 \
#     -drive file=/tmp/ubuntu-base.img,format=qcow2 \
#     -drive file=/tmp/cloud-init.iso,format=raw \
#     -nographic
#   # (log in, verify SSH works, install .NET, shut down)
#   # The result is your base image ready for use with this script.

set -euo pipefail

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------

BASE_IMAGE=""
VM_NAME="arbor-ui-test"
IMAGES_DIR="/tmp/arbor-vms"
OUTPUT_DIR=""
GUEST_USER="ubuntu"
GUEST_PASSWORD=""
SSH_PORT=52222
VNC_DISPLAY=10          # :10 = port 5910
SCREEN_W=1280
SCREEN_H=800
MEMORY_MB=4096
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
        --base-image)     BASE_IMAGE="$2";    shift 2 ;;
        --vm-name)        VM_NAME="$2";       shift 2 ;;
        --images-dir)     IMAGES_DIR="$2";    shift 2 ;;
        --output-dir)     OUTPUT_DIR="$2";    shift 2 ;;
        --guest-user)     GUEST_USER="$2";    shift 2 ;;
        --guest-password) GUEST_PASSWORD="$2"; shift 2 ;;
        --ssh-port)       SSH_PORT="$2";      shift 2 ;;
        --vnc-port)       VNC_DISPLAY="$2";   shift 2 ;;
        --screen)         SCREEN_W="${2%%x*}"; SCREEN_H="${2##*x}"; shift 2 ;;
        --memory)         MEMORY_MB="$2";     shift 2 ;;
        --cpus)           CPU_COUNT="$2";     shift 2 ;;
        --record-video)   RECORD_VIDEO=true;  shift ;;
        --pause)          PAUSE_STEPS=true;   shift ;;
        --keep-vm)        KEEP_VM=true;       shift ;;
        -h|--help)
            sed -n '2,60p' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1 ;;
    esac
done

if [[ -z "$BASE_IMAGE" ]]; then
    echo "ERROR: --base-image is required." >&2
    echo "       See the script header for preparation instructions." >&2
    exit 1
fi

if [[ ! -f "$BASE_IMAGE" ]]; then
    echo "ERROR: Base image not found: $BASE_IMAGE" >&2
    exit 1
fi

if [[ -z "$OUTPUT_DIR" ]]; then
    OUTPUT_DIR="$REPO_ROOT/docs/screenshots"
fi

if [[ -z "$GUEST_PASSWORD" ]]; then
    read -rsp "Enter SSH password for guest user '$GUEST_USER': " GUEST_PASSWORD
    echo
fi

# Expose password via environment variable so sshpass never sees it on the command line,
# making it invisible in process listings and shell history.
export SSHPASS="$GUEST_PASSWORD"
unset GUEST_PASSWORD   # prevent accidental use of the plain variable below

OVERLAY_IMAGE="$IMAGES_DIR/${VM_NAME}-run.qcow2"
PUBLISH_DIR="/tmp/arbor-publish-$$"
VNC_PORT=$((5900 + VNC_DISPLAY))
GUEST_APP_DIR="/home/$GUEST_USER/automation/app"
GUEST_SHOT_DIR="/home/$GUEST_USER/automation/screenshots"
SSH_OPTS="-o StrictHostKeyChecking=no -o ConnectTimeout=5 -o BatchMode=no -p $SSH_PORT"

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
        echo "[PAUSE] '$step_name' — connect to the VM via VNC at localhost:$VNC_PORT"
        echo "        (use any VNC viewer: vncviewer localhost:$VNC_DISPLAY)"
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

ssh_guest() {
    sshpass -e ssh $SSH_OPTS "$GUEST_USER@localhost" "$@"
}

scp_to_guest() {
    sshpass -e scp $SSH_OPTS -r "$1" "$GUEST_USER@localhost:$2"
}

scp_from_guest() {
    sshpass -e scp $SSH_OPTS -r "$GUEST_USER@localhost:$1" "$2"
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
    done
    echo "    SSH ready."
}

cleanup() {
    local qemu_pid="$1"
    if [[ "$KEEP_VM" == "true" ]]; then
        echo ""
        echo "WARN: --keep-vm set — overlay image kept at $OVERLAY_IMAGE"
        echo "      QEMU process: $qemu_pid"
        return
    fi
    step "Shutting down VM and cleaning up"
    ssh_guest "sudo shutdown -h now" 2>/dev/null || true
    sleep 5
    kill "$qemu_pid" 2>/dev/null || true
    rm -f "$OVERLAY_IMAGE"
    rm -rf "$PUBLISH_DIR"
    echo "    VM shut down and overlay image removed."
}

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------

step "Checking prerequisites"

check_cmd "qemu-system-x86_64" "sudo apt-get install -y qemu-kvm"
check_cmd "qemu-img"           "sudo apt-get install -y qemu-utils"
check_cmd "sshpass"            "sudo apt-get install -y sshpass"
check_cmd "xdotool"            "sudo apt-get install -y xdotool"
check_cmd "scrot"              "sudo apt-get install -y scrot"
check_cmd "dotnet"             "See https://dot.net for installation instructions"

if [[ "$RECORD_VIDEO" == "true" ]]; then
    check_cmd "ffmpeg" "sudo apt-get install -y ffmpeg"
fi

if ! lsmod | grep -q kvm; then
    echo "WARN: kvm kernel module not loaded — QEMU will run without hardware acceleration (slow)." >&2
fi

echo "    All prerequisites satisfied."

# ---------------------------------------------------------------------------
# Setup directories
# ---------------------------------------------------------------------------

mkdir -p "$IMAGES_DIR" "$OUTPUT_DIR" "$PUBLISH_DIR"

# ---------------------------------------------------------------------------
# Step 1: Create overlay image
# ---------------------------------------------------------------------------

step "Creating overlay qcow2 image"
if [[ -f "$OVERLAY_IMAGE" ]]; then
    rm -f "$OVERLAY_IMAGE"
fi
qemu-img create -f qcow2 -b "$BASE_IMAGE" -F qcow2 "$OVERLAY_IMAGE"
echo "    Overlay: $OVERLAY_IMAGE"

# ---------------------------------------------------------------------------
# Step 2: Start VM
# ---------------------------------------------------------------------------

step "Starting QEMU/KVM VM"

KVM_FLAG=""
if lsmod | grep -q kvm; then
    KVM_FLAG="-enable-kvm"
fi

qemu-system-x86_64 $KVM_FLAG \
    -m "${MEMORY_MB}M" \
    -smp "$CPU_COUNT" \
    -drive "file=$OVERLAY_IMAGE,format=qcow2" \
    -nic "user,hostfwd=tcp::${SSH_PORT}-:22" \
    -vnc ":${VNC_DISPLAY}" \
    -vga virtio \
    -daemonize \
    -pidfile "/tmp/${VM_NAME}.pid"

QEMU_PID=$(cat "/tmp/${VM_NAME}.pid")
echo "    QEMU PID: $QEMU_PID  VNC: localhost:$VNC_DISPLAY  SSH port: $SSH_PORT"

# Register cleanup on exit
trap "cleanup $QEMU_PID" EXIT

# ---------------------------------------------------------------------------
# Step 3: Wait for SSH
# ---------------------------------------------------------------------------

wait_for_ssh

# ---------------------------------------------------------------------------
# Step 4: Build application on host
# ---------------------------------------------------------------------------

step "Building Arbor.HttpClient.Desktop (linux-x64 self-contained)"
dotnet publish \
    "$REPO_ROOT/src/Arbor.HttpClient.Desktop/Arbor.HttpClient.Desktop.csproj" \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    -o "$PUBLISH_DIR" \
    -v quiet
echo "    Published to: $PUBLISH_DIR"

# ---------------------------------------------------------------------------
# Step 5: Copy application into VM
# ---------------------------------------------------------------------------

step "Copying application into VM"
ssh_guest "mkdir -p '$GUEST_APP_DIR' '$GUEST_SHOT_DIR'"
scp_to_guest "$PUBLISH_DIR/." "$GUEST_APP_DIR/"
ssh_guest "chmod +x '$GUEST_APP_DIR/Arbor.HttpClient.Desktop'"
echo "    Application copied to $GUEST_APP_DIR"

# ---------------------------------------------------------------------------
# Step 6: Start Xvfb in guest
# ---------------------------------------------------------------------------

step "Starting Xvfb display server in guest"
ssh_guest "pkill Xvfb || true; DISPLAY=:99 Xvfb :99 -screen 0 ${SCREEN_W}x${SCREEN_H}x24 &"
sleep 2
echo "    Xvfb running on DISPLAY=:99"

# ---------------------------------------------------------------------------
# Step 7: (Optional) Start video recording in guest
# ---------------------------------------------------------------------------

VIDEO_PID_FILE="/tmp/${VM_NAME}-ffmpeg.pid"
if [[ "$RECORD_VIDEO" == "true" ]]; then
    step "Starting screen recording in guest"
    ssh_guest "DISPLAY=:99 ffmpeg -f x11grab -framerate 25 \
        -video_size ${SCREEN_W}x${SCREEN_H} -i :99 \
        -c:v libx264 -preset ultrafast -crf 22 -pix_fmt yuv420p \
        ~/automation/demo-vm.mp4 -y &>/tmp/ffmpeg.log & echo \$! > $VIDEO_PID_FILE"
    echo "    Recording started."
fi

# ---------------------------------------------------------------------------
# Step 8: Launch the application in guest
# ---------------------------------------------------------------------------

step "Launching application in guest"
ssh_guest "DISPLAY=:99 '$GUEST_APP_DIR/Arbor.HttpClient.Desktop' &>/tmp/app.log &"
sleep "$([[ $RECORD_VIDEO == true ]] && echo 5 || echo 3)"
echo "    Application launched."
pause_if_enabled "App opened (initial state)"

# ---------------------------------------------------------------------------
# Automation helper (runs xdotool inside the guest)
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

auto_step() {
    local name="$1"
    shift
    echo ""
    echo "--- Step $(printf '%02d' $STEP_NUM): $name"
    # Execute the automation commands passed as arguments
    for cmd in "$@"; do
        ssh_guest "DISPLAY=:99 $cmd" 2>/dev/null || true
    done
    sleep 1
    # Take screenshot inside guest, copy to host
    local filename
    filename="step-$(printf '%02d' $STEP_NUM)-$(echo "$name" | tr ' /()\t' '_____').png"
    ssh_guest "DISPLAY=:99 scrot '$GUEST_SHOT_DIR/$filename'"
    scp_from_guest "$GUEST_SHOT_DIR/$filename" "$OUTPUT_DIR/$filename"
    echo "    Screenshot: $OUTPUT_DIR/$filename"
    STEP_NUM=$((STEP_NUM + 1))
    pause_if_enabled "$name"
}

# ---------------------------------------------------------------------------
# Step 9: UI automation sequence
# ---------------------------------------------------------------------------

step "Running UI automation steps"

# Step 01 — initial state (already captured above; take again cleanly)
auto_step "App opened (initial state)"

# Step 02 — click URL bar and type demo URL
auto_step "Type URL into request bar" \
    "xdotool mousemove $URL_BAR_X $URL_BAR_Y click 1" \
    "xdotool key ctrl+a" \
    "xdotool type --clearmodifiers --delay 40 '$DEMO_URL'"

# Step 03 — click Send button
auto_step "Click Send button" \
    "xdotool mousemove $SEND_BTN_X $SEND_BTN_Y click 1"

# Step 04 — wait for response (give HTTP time to complete)
echo "    Waiting 10s for HTTP response..."
sleep 10
auto_step "HTTP response received"

# Step 05 — open Variables panel
auto_step "Variables panel" \
    "xdotool mousemove $VARS_TAB_X $VARS_TAB_Y click 1"

# Step 06 — open Scheduled Jobs panel
auto_step "Scheduled Jobs panel" \
    "xdotool mousemove $SCHED_TAB_X $SCHED_TAB_Y click 1"

# ---------------------------------------------------------------------------
# Step 10: Stop video recording
# ---------------------------------------------------------------------------

if [[ "$RECORD_VIDEO" == "true" ]]; then
    step "Stopping video recording"
    ssh_guest "kill \$(cat $VIDEO_PID_FILE) 2>/dev/null || true; sleep 2"
    scp_from_guest "~/automation/demo-vm.mp4" "$(dirname "$OUTPUT_DIR")/demo-vm.mp4"
    echo "    Video: $(dirname "$OUTPUT_DIR")/demo-vm.mp4"
fi

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

echo ""
echo "============================================================"
echo " UI Automation run complete!"
echo "============================================================"
echo " Screenshots : $OUTPUT_DIR"
if [[ "$RECORD_VIDEO" == "true" ]]; then
    echo " Video       : $(dirname "$OUTPUT_DIR")/demo-vm.mp4"
fi
ls -1 "$OUTPUT_DIR"/step-*.png 2>/dev/null | wc -l | xargs -I{} echo " Step count  : {} screenshots"
echo ""
