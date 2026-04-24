#Requires -Version 7.0
<#
.SYNOPSIS
    Runs interactive UI automation for Arbor.HttpClient.Desktop inside a Hyper-V VM.

.DESCRIPTION
    This script orchestrates a full interactive UI automation run on a developer's
    Windows 11 machine (Hyper-V must be enabled). It:

      1. Validates all prerequisites (admin rights, Hyper-V module, ffmpeg, .NET SDK).
      2. Creates a disposable Hyper-V VM backed by a differencing VHDX so the base
         image is never modified.
      3. Waits for the Windows guest to accept PowerShell Direct connections.
      4. Builds the desktop application with 'dotnet publish' on the host and copies
         the publish output into the VM.
      5. Uploads and executes Invoke-InVMAutomation.ps1 inside the VM via PowerShell
         Direct. That inner script launches the real application and drives it through
         Win32 keyboard and mouse events, saving a screenshot after each step.
      6. Retrieves all screenshot PNG files from the VM back to the host output directory.
      7. Optionally assembles the screenshots into a demo MP4 using ffmpeg (-RecordVideo).
      8. Optionally pauses after every step so the operator can inspect the running VM
         in Hyper-V Manager (-PauseAfterEachStep).
      9. Shuts down and deletes the VM (unless -KeepVm is set).

    See docs/vm-ui-automation.md for a full architecture description and sub-task list.

.PARAMETER VmName
    Display name for the Hyper-V VM. A VM with this name is created fresh each run.
    Defaults to "Arbor-UI-Test".

.PARAMETER BaseVhdx
    Path to a sysprepped Windows VHDX image used as the read-only base disk.
    A differencing VHDX is layered on top for each run so the base is not modified.
    See docs/vm-ui-automation.md section 4 for preparation instructions.

.PARAMETER VhdsDir
    Directory where the run-specific differencing VHDX is stored.
    Defaults to %TEMP%\ArborVMs.

.PARAMETER VhdSizeGB
    Maximum size of the differencing VHDX in GB. Defaults to 40.

.PARAMETER GuestUser
    Local administrator account name inside the Windows guest. Defaults to "Administrator".

.PARAMETER GuestPassword
    Password for the guest administrator account as a SecureString.
    If omitted the script prompts interactively.

.PARAMETER RepoRoot
    Absolute path to the repository root on the host.
    Defaults to the current working directory.

.PARAMETER OutputDir
    Directory on the host where screenshots and the demo video are written.
    Defaults to <RepoRoot>\docs\screenshots.

.PARAMETER RecordVideo
    When set, ffmpeg assembles the captured screenshots into a demo MP4 at
    <OutputDir>\..\demo-vm.mp4.

.PARAMETER PauseAfterEachStep
    When set, the script prints a message and waits for the operator to press ENTER
    after each automation step. While paused, connect to the VM in Hyper-V Manager
    (Enhanced Session or Basic Session) to inspect the live application state.

.PARAMETER KeepVm
    When set, the VM is not shut down or deleted after the run. Useful for debugging.

.PARAMETER MemoryMB
    VM memory in megabytes. Defaults to 4096 (4 GB).

.PARAMETER CpuCount
    Number of virtual CPUs. Defaults to 2.

.PARAMETER ScreenWidth
    Guest screen width in pixels. Defaults to 1280.

.PARAMETER ScreenHeight
    Guest screen height in pixels. Defaults to 800.

.EXAMPLE
    # Minimal run — prompts for base VHDX path and guest password
    .\scripts\Start-UIAutomation.ps1 -BaseVhdx "C:\HyperV\Base\win11-base.vhdx"

.EXAMPLE
    # Full run with video recording
    .\scripts\Start-UIAutomation.ps1 `
        -BaseVhdx "C:\HyperV\Base\win11-base.vhdx" `
        -RecordVideo

.EXAMPLE
    # Step-through debugging mode — keep VM alive after run
    .\scripts\Start-UIAutomation.ps1 `
        -BaseVhdx "C:\HyperV\Base\win11-base.vhdx" `
        -PauseAfterEachStep `
        -KeepVm

.NOTES
    Host prerequisites:
    - Windows 11 Pro / Enterprise / Education (Hyper-V requires these editions)
    - Hyper-V feature enabled:
        Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All
    - PowerShell 7.4+:   winget install Microsoft.PowerShell
    - ffmpeg 6+:          winget install Gyan.FFmpeg  (required with -RecordVideo)
    - .NET SDK 10:        winget install Microsoft.DotNet.SDK.10
    - Run as Administrator (required for Hyper-V management cmdlets)

    Guest prerequisites:
    - Sysprepped Windows 10 22H2+ / Windows 11 / Windows Server 2022 VHDX
    - Local Administrator account with a known password
    - WinRM / PowerShell Direct enabled (no extra config needed for PowerShell Direct
      over Hyper-V VMBus — it works without network)
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$BaseVhdx,

    [string]$VmName               = "Arbor-UI-Test",
    [string]$VhdsDir              = (Join-Path $env:TEMP "ArborVMs"),
    [int]$VhdSizeGB               = 40,
    [string]$GuestUser            = "Administrator",
    [SecureString]$GuestPassword,
    [string]$RepoRoot             = (Get-Location).Path,
    [string]$OutputDir            = "",
    [switch]$RecordVideo,
    [switch]$PauseAfterEachStep,
    [switch]$KeepVm,
    [int]$MemoryMB                = 4096,
    [int]$CpuCount                = 2,
    [int]$ScreenWidth             = 1280,
    [int]$ScreenHeight            = 800,
    # Name of the Hyper-V virtual switch to attach to the VM.
    # If omitted the script selects the first External switch automatically.
    # Run 'Get-VMSwitch' to list available switches.
    [string]$SwitchName           = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Step {
    param([string]$Message, [ConsoleColor]$Color = [ConsoleColor]::Cyan)
    Write-Host "`n==> $Message" -ForegroundColor $Color
}

function Write-Done  { Write-Host "    OK" -ForegroundColor Green }
function Write-Warn  { param([string]$Msg) Write-Host "    WARN: $Msg" -ForegroundColor Yellow }

function Invoke-Step {
    param(
        [string]$StepName,
        [scriptblock]$Action
    )
    Write-Step $StepName
    & $Action
    if ($PauseAfterEachStep) {
        Write-Host "`n[PAUSE] Step '$StepName' complete." -ForegroundColor Yellow
        Write-Host "        Open Hyper-V Manager and connect to '$VmName' to inspect." -ForegroundColor Yellow
        $null = Read-Host "        Press ENTER to continue"
    }
}

function Assert-AdminRights {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This script must be run as Administrator (required for Hyper-V management)."
    }
}

function Assert-Command {
    param([string]$Name, [string]$InstallHint)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "'$Name' not found on PATH. $InstallHint"
    }
}

function Wait-VMReady {
    param([string]$VmNameParam, [PSCredential]$Cred, [int]$TimeoutSeconds = 300)
    Write-Step "Waiting for VM PowerShell Direct to be available (timeout ${TimeoutSeconds}s)..."
    $deadline = [DateTime]::Now.AddSeconds($TimeoutSeconds)
    while ([DateTime]::Now -lt $deadline) {
        try {
            $hostname = Invoke-Command -VMName $VmNameParam -Credential $Cred `
                -ScriptBlock { $env:COMPUTERNAME } -ErrorAction Stop
            Write-Host "    VM online: $hostname" -ForegroundColor Green
            return
        }
        catch {
            Start-Sleep -Seconds 5
        }
    }
    throw "VM '$VmNameParam' did not become ready within ${TimeoutSeconds} seconds."
}

function Retrieve-VMFile {
    param([string]$VmNameParam, [PSCredential]$Cred, [string]$GuestPath, [string]$HostPath)
    $bytes = Invoke-Command -VMName $VmNameParam -Credential $Cred -ScriptBlock {
        param([string]$p)
        [System.IO.File]::ReadAllBytes($p)
    } -ArgumentList $GuestPath -ErrorAction Stop
    [System.IO.File]::WriteAllBytes($HostPath, $bytes)
}

function Remove-VMSafely {
    param([string]$VmNameParam)
    $vm = Get-VM -Name $VmNameParam -ErrorAction SilentlyContinue
    if ($null -eq $vm) { return }
    if ($vm.State -ne 'Off') {
        Stop-VM -Name $VmNameParam -TurnOff -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 5
    }
    Remove-VM -Name $VmNameParam -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------

Write-Step "Checking prerequisites..." Green

Assert-AdminRights
Write-Host "    Admin rights: OK" -ForegroundColor Green

if (-not (Get-Module -ListAvailable -Name Hyper-V)) {
    throw @"
The Hyper-V PowerShell module is not available.
Enable Hyper-V:
  Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All
"@
}
Import-Module Hyper-V -ErrorAction Stop
Write-Host "    Hyper-V module: OK" -ForegroundColor Green

Assert-Command "dotnet" "Install .NET SDK 10: winget install Microsoft.DotNet.SDK.10"
Write-Host "    dotnet: OK" -ForegroundColor Green

if ($RecordVideo) {
    Assert-Command "ffmpeg" "Install ffmpeg: winget install Gyan.FFmpeg"
    Write-Host "    ffmpeg: OK" -ForegroundColor Green
}

if (-not (Test-Path $BaseVhdx)) {
    throw "Base VHDX not found: $BaseVhdx`nSee docs/vm-ui-automation.md section 4 for preparation instructions."
}
Write-Host "    Base VHDX: $BaseVhdx" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------

if (-not $OutputDir) {
    $OutputDir = Join-Path $RepoRoot "docs\screenshots"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $VhdsDir   | Out-Null

$runVhdx        = Join-Path $VhdsDir "$VmName-run.vhdx"
$publishDir     = Join-Path $env:TEMP "arbor-publish-$([System.Guid]::NewGuid().ToString('N')[0..7] -join '')"
$innerScript    = Join-Path $PSScriptRoot "Invoke-InVMAutomation.ps1"

if (-not (Test-Path $innerScript)) {
    throw "Inner automation script not found: $innerScript"
}

# ---------------------------------------------------------------------------
# Guest credentials
# ---------------------------------------------------------------------------

if (-not $GuestPassword) {
    $GuestPassword = Read-Host "Enter guest Administrator password" -AsSecureString
}
$credential = New-Object PSCredential($GuestUser, $GuestPassword)

# ---------------------------------------------------------------------------
# Step 1: Remove any leftover VM from a previous run
# ---------------------------------------------------------------------------

Invoke-Step "Removing previous test VM (if any)" {
    Remove-VMSafely -VmNameParam $VmName
    if (Test-Path $runVhdx) {
        Remove-Item $runVhdx -Force
    }
}

# ---------------------------------------------------------------------------
# Step 2: Create differencing VHDX and VM
# ---------------------------------------------------------------------------

Invoke-Step "Creating differencing VHDX and Hyper-V VM" {
    New-VHD -Path $runVhdx -ParentPath $BaseVhdx -Differencing | Out-Null
    Write-Host "    Differencing VHDX: $runVhdx" -ForegroundColor DarkGray

    # Resolve the virtual switch to attach
    $resolvedSwitch = $SwitchName
    if (-not $resolvedSwitch) {
        $resolvedSwitch = Get-VMSwitch |
            Where-Object { $_.SwitchType -eq 'External' } |
            Select-Object -First 1 -ExpandProperty Name
    }
    if (-not $resolvedSwitch) {
        throw @"
No Hyper-V External virtual switch found.
Create one in Hyper-V Manager (Action → Virtual Switch Manager → External) or
supply the switch name via -SwitchName.
Available switches: $(( Get-VMSwitch | Select-Object -ExpandProperty Name ) -join ', ')
"@
    }
    Write-Host "    Virtual switch: $resolvedSwitch" -ForegroundColor DarkGray

    $vm = New-VM `
        -Name $VmName `
        -Generation 2 `
        -MemoryStartupBytes ($MemoryMB * 1MB) `
        -VHDPath $runVhdx `
        -SwitchName $resolvedSwitch

    Set-VMProcessor      $vm -Count $CpuCount
    Set-VMMemory         $vm -DynamicMemoryEnabled $false
    Set-VMVideo          $vm -HorizontalResolution $ScreenWidth -VerticalResolution $ScreenHeight
    Set-VMSecurity       $vm -VirtualizationBasedSecurityOptOut $true

    # Enable Enhanced Session (clipboard, audio, USB sharing)
    Set-VM $vm -EnhancedSessionTransportType HvSocket

    Write-Host "    VM '$VmName' created (Gen2, ${MemoryMB} MB, $CpuCount vCPUs, ${ScreenWidth}x${ScreenHeight})" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Step 3: Start VM and wait for PowerShell Direct
# ---------------------------------------------------------------------------

Invoke-Step "Starting VM" {
    Start-VM -Name $VmName
    Write-Host "    VM started." -ForegroundColor DarkGray
}

Wait-VMReady -VmNameParam $VmName -Cred $credential

# ---------------------------------------------------------------------------
# Step 4: Build and publish the application on the host
# ---------------------------------------------------------------------------

Invoke-Step "Building Arbor.HttpClient.Desktop (win-x64 self-contained)" {
    $slnx = Join-Path $RepoRoot "Arbor.HttpClient.slnx"
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

    dotnet publish `
        (Join-Path $RepoRoot "src\Arbor.HttpClient.Desktop\Arbor.HttpClient.Desktop.csproj") `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -o $publishDir `
        -v quiet

    Write-Host "    Published to: $publishDir" -ForegroundColor DarkGray
    Write-Host "    Executable:   $(Join-Path $publishDir 'Arbor.HttpClient.Desktop.exe')" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Step 5: Copy app into VM
# ---------------------------------------------------------------------------

Invoke-Step "Copying application into VM" {
    # Create target directory in VM
    Invoke-Command -VMName $VmName -Credential $credential -ScriptBlock {
        New-Item -ItemType Directory -Force -Path "C:\automation\app" | Out-Null
        New-Item -ItemType Directory -Force -Path "C:\automation\screenshots" | Out-Null
    }

    # Upload each file using PowerShell Direct byte transfer
    $files = Get-ChildItem -Path $publishDir -Recurse -File
    $total = $files.Count
    $i = 0
    foreach ($file in $files) {
        $i++
        $rel  = $file.FullName.Substring($publishDir.Length).TrimStart('\', '/')
        $dest = "C:\automation\app\$rel"
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        Invoke-Command -VMName $VmName -Credential $credential -ScriptBlock {
            param([string]$Path, [byte[]]$Data)
            $dir = Split-Path $Path
            if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
            [System.IO.File]::WriteAllBytes($Path, $Data)
        } -ArgumentList $dest, $bytes
        if ($i % 20 -eq 0) {
            Write-Host "    Uploaded $i / $total files..." -ForegroundColor DarkGray
        }
    }
    Write-Host "    Uploaded $total files to C:\automation\app\" -ForegroundColor DarkGray

    # Upload inner automation script
    $scriptBytes = [System.IO.File]::ReadAllBytes($innerScript)
    Invoke-Command -VMName $VmName -Credential $credential -ScriptBlock {
        param([byte[]]$Data)
        [System.IO.File]::WriteAllBytes("C:\automation\Invoke-InVMAutomation.ps1", $Data)
    } -ArgumentList (, $scriptBytes)
    Write-Host "    Uploaded Invoke-InVMAutomation.ps1" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Step 6: Run automation script inside VM
# ---------------------------------------------------------------------------

Invoke-Step "Running UI automation inside VM" {
    $pauseFlag = if ($PauseAfterEachStep) { '$true' } else { '$false' }

    $result = Invoke-Command -VMName $VmName -Credential $credential -ScriptBlock {
        param([bool]$Pause)
        Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
        & "C:\automation\Invoke-InVMAutomation.ps1" `
            -AppExe        "C:\automation\app\Arbor.HttpClient.Desktop.exe" `
            -ScreenshotDir "C:\automation\screenshots" `
            -PauseAfterEachStep:$Pause `
            -Verbose
    } -ArgumentList $PauseAfterEachStep

    Write-Host "    Automation result: $result" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Step 7: Retrieve screenshots from VM
# ---------------------------------------------------------------------------

Invoke-Step "Retrieving screenshots from VM" {
    $pngList = Invoke-Command -VMName $VmName -Credential $credential -ScriptBlock {
        Get-ChildItem -Path "C:\automation\screenshots" -Filter "*.png" |
            Select-Object -ExpandProperty FullName
    }

    foreach ($guestPath in $pngList) {
        $filename = Split-Path $guestPath -Leaf
        $hostPath = Join-Path $OutputDir $filename
        Retrieve-VMFile -VmNameParam $VmName -Cred $credential -GuestPath $guestPath -HostPath $hostPath
        Write-Host "    Retrieved: $filename" -ForegroundColor DarkGray
    }

    Write-Host "    Total screenshots: $($pngList.Count)" -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Step 8 (optional): Assemble video from screenshots
# ---------------------------------------------------------------------------

if ($RecordVideo) {
    Invoke-Step "Assembling demo video with ffmpeg" {
        $videoPath = Join-Path (Split-Path $OutputDir) "demo-vm.mp4"
        $frameList = Join-Path $env:TEMP "arbor-frames.txt"

        # Write ffmpeg concat list — each frame shown for 3 seconds
        $lines = Get-ChildItem -Path $OutputDir -Filter "step-*.png" |
            Sort-Object Name |
            ForEach-Object { "file '$($_.FullName)'`nduration 3" }

        if ($lines) {
            $lines | Set-Content -Path $frameList -Encoding UTF8
            ffmpeg -f concat -safe 0 -i $frameList `
                -vf "scale=1280:800:force_original_aspect_ratio=decrease,pad=1280:800:(ow-iw)/2:(oh-ih)/2" `
                -c:v libx264 -crf 22 -preset medium -pix_fmt yuv420p `
                -movflags +faststart `
                $videoPath -y 2>&1 | Where-Object { $_ -match 'frame|error|warn' }
            Write-Host "    Video saved: $videoPath" -ForegroundColor Green
        }
        else {
            Write-Warn "No step-*.png files found in $OutputDir; skipping video assembly."
        }
    }
}

# ---------------------------------------------------------------------------
# Step 9: Shutdown and cleanup
# ---------------------------------------------------------------------------

if (-not $KeepVm) {
    Invoke-Step "Shutting down and removing VM" {
        Remove-VMSafely -VmNameParam $VmName
        if (Test-Path $runVhdx) {
            Remove-Item $runVhdx -Force
        }
        Write-Host "    VM '$VmName' removed." -ForegroundColor DarkGray
    }
}
else {
    Write-Warn "-KeepVm is set — VM '$VmName' left running for inspection."
}

# Cleanup host publish temp dir
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

Write-Host "`n" -NoNewline
Write-Host "============================================================" -ForegroundColor Green
Write-Host " UI Automation run complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Screenshots : $OutputDir" -ForegroundColor Green
if ($RecordVideo) {
    $videoPath = Join-Path (Split-Path $OutputDir) "demo-vm.mp4"
    Write-Host " Video       : $videoPath" -ForegroundColor Green
}
Write-Host ""
