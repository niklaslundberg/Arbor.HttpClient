# VM-Based Interactive UI Automation

This document reports on the feasibility of running interactive, real-GUI UI automation for
Arbor.HttpClient.Desktop, analyses what the agent/CI environment can do, and proposes a
developer-machine solution using Hyper-V (Windows 11, primary) and QEMU/KVM (Ubuntu, secondary).

---

## 1. Agent Environment Analysis

GitHub Actions hosted runners vary significantly in what virtualisation features they expose.
All GitHub-hosted runners (Linux and Windows) run as **Azure virtual machines**
([source](https://docs.github.com/en/actions/concepts/runners/github-hosted-runners#cloud-hosts-used-by-github-hosted-runners)).

The official GitHub documentation states:

> *"While nested virtualization is technically possible while using runners, it is not
> officially supported. Any use of nested VMs is experimental and done at your own risk,
> we offer no guarantees regarding stability, performance, or compatibility."*

Because the runners are Azure VMs, nested virtualisation depends on the underlying Azure VM
SKU allocated to the job. Azure supports nested virt on many modern SKUs, so it can work —
but GitHub makes no guarantee that any given job lands on a capable SKU.

The table below reflects this nuanced reality (see also
[actions/runner-images#183](https://github.com/actions/runner-images/issues/183),
[community discussion #8305](https://github.com/orgs/community/discussions/8305), and
[runner-images#8882](https://github.com/actions/runner-images/issues/8882)):

| Capability | Standard runners (`ubuntu-latest`, `windows-latest`) | Large Linux runners (4+ vCPU, paid) | Notes |
|---|---|---|---|
| Headless rendering (Skia / Avalonia Headless) | ✅ | ✅ | Current approach — works everywhere |
| Display server (X11 / Wayland / Win desktop) | ❌ | ❌ | No GPU, no physical display on any hosted runner |
| Nested KVM (`/dev/kvm`) | ⚠️ experimental | ✅ officially supported | Standard runners: technically possible on Azure but undocumented, unreliable — must not be relied upon; large runners: KVM is guaranteed |
| Nested Hyper-V (Windows runners) | ⚠️ experimental | N/A | Windows runners are Azure VMs; Hyper-V nesting may work on some SKU allocations but is not officially supported |
| Interactive keyboard/mouse injection | ❌ | ❌ without Xvfb | A QEMU/KVM guest on a large Linux runner can run Xvfb + xdotool; the *host* runner itself has no display |
| Real-application video recording | ❌ | ⚠️ possible in QEMU guest | With KVM + Xvfb inside the guest, `ffmpeg -f x11grab` works; no display on the runner host itself |

**Key conclusions:**

1. **Standard runners** should be treated as headless-only. Nested virt is technically
   possible (all runners are Azure VMs) but is experimental, unreliable, and explicitly
   unsupported. Do not build CI pipelines that depend on it working.
2. **Large Linux runners** officially support KVM — guaranteed by GitHub's runner image
   configuration, not merely a side-effect of the Azure SKU. This is the basis for
   sub-task 7 (CI integration via large runners).
3. **Windows hosted runners** may support Hyper-V nested virtualisation on some SKU
   allocations (same Azure VM reasoning), but this is experimental and unsupported. For
   reliable Windows VM automation, use a self-hosted Windows 11 runner with Hyper-V enabled.
4. For production-quality interactive UI automation on the **current** free-tier setup,
   the scripts must run on the **code owner's local machine**.

---

## 2. Current State (Headless Rendering)

The existing E2E test suite uses Avalonia's `HeadlessUnitTestSession` to render application
windows off-screen with the Skia backend:

- `src/Arbor.HttpClient.Desktop.E2E.Tests/ScreenshotGenerator.cs` — produces `.png` files
- `src/Arbor.HttpClient.Desktop.E2E.Tests/MainWindowUiTests.cs` — UI behaviour tests
- `scripts/take-screenshots.sh` — convenience wrapper
- `scripts/record-demo.sh` — assembles a demo video from headless frames using ffmpeg

**Limitation:** Headless rendering does not exercise the real rendering pipeline, compositor,
font rasteriser, or OS-level accessibility APIs. Mouse and keyboard events are simulated at
the Avalonia event-dispatcher level, not from the OS message queue.

---

## 3. Proposed Solution: Developer-Machine VM Automation

Run the scripts below on a Windows 11 machine (or Ubuntu 24.04 LTS) with hardware
virtualisation enabled. The scripts:

1. Create a disposable virtual machine (Hyper-V or QEMU/KVM).
2. Provision the machine with .NET and the application.
3. Launch the real application inside the VM.
4. Drive it with real OS-level keyboard and mouse events.
5. Capture full-screen screenshots at each step.
6. Optionally record a proper MP4 video of the session.
7. (Optional) Pause after each step for live inspection.
8. Shut down and delete the VM when finished.

### Script inventory

| Script | Platform | Purpose |
|---|---|---|
| `scripts/Test-HyperVAvailability.ps1` | Windows | Fast non-destructive probe — checks if Hyper-V is available and usable. Exit 0 = available. |
| `scripts/Start-UIAutomation.ps1` | Windows 11 + Hyper-V | Orchestrator — manages VM lifecycle, deploys app, runs automation, assembles video |
| `scripts/Invoke-InVMAutomation.ps1` | Windows guest (run via PowerShell Direct) | Inner automation driver — launches app, sends keyboard/mouse, saves screenshots. Supports `-Theme Light/Dark/Default`. |
| `scripts/start-ui-automation-kvm.sh` | Ubuntu 22.04+ host + QEMU/KVM | Bash orchestrator using KVM, SSH, xdotool, and ffmpeg |

---

## 4. Windows 11 / Hyper-V Architecture

```
Host (Windows 11, Hyper-V enabled)
│
├── Start-UIAutomation.ps1
│     │
│     ├── New-VM (creates differencing VHDX off a sysprepped base image)
│     ├── Start-VM
│     ├── Wait for PowerShell Direct connection
│     ├── Copy app build into VM  (Invoke-Command -VMName + [io.file]::WriteAllBytes)
│     ├── Invoke-Command -VMName → Invoke-InVMAutomation.ps1
│     │       ├── Launches Arbor.HttpClient.Desktop.exe
│     │       ├── Win32 mouse_event / SendKeys → real OS events
│     │       ├── System.Drawing.Bitmap screenshots saved to C:\automation\
│     │       └── (optional) ffmpeg -f gdigrab → in-guest video
│     ├── Retrieve screenshot files from VM (via Invoke-Command byte read)
│     ├── Assemble video with ffmpeg on host (from frame images)
│     └── Stop-VM / Remove-VM
│
└── docs/screenshots/  ←── final artifacts
    docs/demo.mp4
```

### Host prerequisites

| Tool | Version | How to install |
|---|---|---|
| Windows 11 Pro / Enterprise / Education | 23H2+ | Required edition for Hyper-V |
| Hyper-V feature | built-in | `Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All` |
| PowerShell | 7.4+ | `winget install Microsoft.PowerShell` |
| ffmpeg | 6.0+ | `winget install Gyan.FFmpeg` |
| .NET SDK | 10 | `winget install Microsoft.DotNet.SDK.10` |

### Guest prerequisites

The scripts expect a **sysprepped Windows VHDX** at the path you supply via `-BaseVhdx`.
Supported guest OS: Windows 10 22H2+, Windows 11, Windows Server 2022.

Prepare a base VHDX once:

```powershell
# 1. Download a Windows evaluation ISO from Microsoft
# 2. Create a generation-2 VHDX and install Windows:
$vhdx = "C:\HyperV\Base\windows11-base.vhdx"
New-VHD -Path $vhdx -SizeBytes 40GB -Dynamic
# Attach to a VM, install Windows, create a local Administrator account,
# enable WinRM:
#   Set-Item WSMan:\localhost\Client\TrustedHosts -Value * -Force
#   Enable-PSRemoting -Force
# Run sysprep to generalise:
#   C:\Windows\System32\Sysprep\Sysprep.exe /oobe /generalize /shutdown
```

A differencing VHDX is created automatically for each run so the base image is never modified.

---

## 5. Ubuntu / QEMU-KVM Architecture

```
Host (Ubuntu 22.04+, KVM enabled)
│
├── start-ui-automation-kvm.sh
│     │
│     ├── Creates overlay qcow2 off a base Ubuntu Desktop image
│     ├── Starts QEMU/KVM VM with VNC display at :10
│     ├── Waits for SSH to be available
│     ├── Copies app build via scp
│     ├── SSH: DISPLAY=:99 Xvfb &; runs app in Xvfb
│     ├── SSH: xdotool to type URL, click Send
│     ├── SSH: scrot to take screenshots
│     ├── ffmpeg captures VNC stream → MP4
│     ├── scp retrieves screenshots
│     └── Shuts down and deletes overlay image
│
└── docs/screenshots/  ←── final artifacts
```

### Host prerequisites (Ubuntu)

```bash
sudo apt-get install -y \
    qemu-kvm libvirt-daemon-system virtinst \
    sshpass xdotool scrot ffmpeg \
    dotnet-sdk-10
```

---

## 6. Pause / Inspection Mode

Both scripts support a `-PauseAfterEachStep` / `--pause` flag:

- The orchestrator prints the step name and waits for the operator to press **ENTER**.
- While paused, connect to the VM in Hyper-V Manager (Windows) or via VNC viewer (Linux)
  to inspect the live application state.
- Resume by pressing ENTER in the terminal.

---

## 7. Video Recording

| Approach | Platform | Quality | Notes |
|---|---|---|---|
| Frame assembly (screenshots → ffmpeg) | Both | Good | No real-time recording; gaps possible between steps |
| `ffmpeg -f gdigrab` (inside guest) | Windows | Excellent | Records the full desktop in real time |
| `ffmpeg -f x11grab` (inside guest) | Linux | Excellent | Requires Xvfb or real Xorg |
| VNC stream capture via `ffmpeg -f vnc` | Both | Good | Captures from outside the guest |

The PowerShell script defaults to frame assembly (screenshot images → ffmpeg slideshow) for
reliability. Pass `-RecordVideo` to enable real-time gdigrab inside the guest.

---

## 8. Sub-Tasks (Ordered Implementation Plan)

The following sub-tasks represent the incremental implementation path. Each maps to a focused
GitHub issue:

### Sub-task 1 — Base VM image preparation guide *(documentation)*
Write step-by-step instructions for preparing the sysprepped Windows VHDX and Ubuntu qcow2
base images that the automation scripts depend on.

### Sub-task 2 — Hyper-V VM provisioning script *(this PR — `Start-UIAutomation.ps1`)*
Create, configure, snapshot, and clean up Hyper-V VMs via PowerShell.

### Sub-task 3 — In-VM automation driver *(this PR — `Invoke-InVMAutomation.ps1`)*
Launch the real application, drive it with Win32 keyboard/mouse events, take screenshots,
and support step-level pause.

### Sub-task 4 — Ubuntu/KVM alternative *(this PR — `start-ui-automation-kvm.sh`)*
Bash implementation using QEMU/KVM, SSH, xdotool, scrot, and ffmpeg.

### Sub-task 5 — Windows UI Automation integration *(future)*
Replace coordinate-based clicking with `System.Windows.Automation` element lookup so tests
remain valid even if the window moves or resizes.

### Sub-task 6 — MSIX installation support *(future)*
Install the signed MSIX package (from the release workflow) inside the VM instead of a raw
publish directory, giving end-to-end coverage of the package + installer path.

### Sub-task 7 — System tests via on-demand GitHub Actions workflow ✅ Implemented

Two on-demand workflows have been added to the repository:

- **`.github/workflows/system-tests.yml`** — triggers via `workflow_dispatch`. Accepts
  optional `base_vhdx_path` (for self-hosted runners with a prepared base VHDX) and
  `commit_screenshots` inputs. When Hyper-V + a base VHDX are available it runs the full
  VM-based automation; otherwise it falls back to direct runner UI automation. Captures
  screenshots in both light and dark themes; records video in light theme.
- **`.github/workflows/vm-probe.yml`** — on-demand diagnostics (`workflow_dispatch` only).
  Contains the Hyper-V environment probe steps that were previously part of the standard
  CI pipeline. Use this when further investigation of the runner environment is needed.

The standard CI pipeline (`ci.yml`) no longer contains the `experimental-vm-probe` job.
All Hyper-V/VM activity is on-demand only.

### Sub-task 8 — Result reporting *(future)*
Parse step results and screenshots from the VM run and post a summary comment to the pull
request via `gh pr comment` or the GitHub API, with inline screenshot thumbnails.

### Sub-task 9 — VM pool for parallel runs *(future)*
Manage a pool of persistent VMs at known snapshots so multiple test sequences can run in
parallel without the full provisioning overhead.

---

## 9. Limitations and Caveats

- **Standard GitHub Actions runners** should be treated as headless-only for CI. Nested
  virtualisation is *technically possible* on all runners (they run on Azure VMs and Azure
  supports nesting on many SKUs), but GitHub explicitly classifies this as experimental and
  unsupported: *"Any use of nested VMs is experimental and done at your own risk, we offer
  no guarantees regarding stability, performance, or compatibility."*
  ([GitHub docs](https://docs.github.com/en/actions/concepts/runners/github-hosted-runners#cloud-hosts-used-by-github-hosted-runners),
  [runner-images#183](https://github.com/actions/runner-images/issues/183)). Do not build
  CI pipelines that depend on it working.
- **Large Linux runners (paid)** officially expose `/dev/kvm`. A future CI workflow can use
  them to run QEMU/KVM-based UI automation without any self-hosted infrastructure (sub-task 7,
  option A).
- **Windows hosted runners** may support Hyper-V nested virtualisation experimentally (same
  Azure VM reasoning as above), but this is unsupported. For reliable Windows VM automation,
  use a self-hosted Windows 11 runner or the developer's own machine with Hyper-V enabled.
- **Base image required** — the developer must prepare the guest OS image separately; the
  scripts do not download or install the guest OS.
- **Windows guest only for primary script** — `Invoke-InVMAutomation.ps1` uses Win32 APIs
  and requires a Windows guest; the KVM script targets Ubuntu guests.
- **Coordinate-based clicking** — control positions are hardcoded from the default 1280×800
  layout; changes to the application layout will require coordinate updates until sub-task 5
  (UI Automation) is implemented.
- **Antivirus / Defender SmartScreen** — inside the VM, Defender may block the unsigned
  xcopy deployment; the MSIX approach (sub-task 6) avoids this.
