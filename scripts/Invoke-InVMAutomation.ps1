#Requires -Version 5.1
<#
.SYNOPSIS
    Drives Arbor.HttpClient.Desktop with real keyboard and mouse input inside a VM guest.

.DESCRIPTION
    This script is uploaded into the Hyper-V (or KVM) guest VM by Start-UIAutomation.ps1
    and executed via PowerShell Direct. It must NOT be run directly on the host.

    The script:
      1. Launches Arbor.HttpClient.Desktop.exe and waits for the main window.
      2. Executes a fixed sequence of automation steps:
           Step 01 — App opened (initial state)
           Step 02 — Click the URL bar and type a demo URL
           Step 03 — Click the Send button
           Step 04 — Wait for and capture the response panel
           Step 05 — Open the Variables panel
           Step 06 — Navigate to the Scheduled Jobs panel
      3. Saves a PNG screenshot after every step to -ScreenshotDir.
      4. Optionally pauses and waits for ENTER after each step (-PauseAfterEachStep).
      5. Closes the application and exits.

    Window coordinates assume a 1280×800 guest screen with the application window
    maximised. Update the coordinate constants at the top of the script if the
    resolution or layout changes.

.PARAMETER AppExe
    Full path to Arbor.HttpClient.Desktop.exe inside the guest.

.PARAMETER ScreenshotDir
    Directory (inside the guest) where PNG screenshots are saved.

.PARAMETER PauseAfterEachStep
    When set, the script prints the step name and waits for ENTER before continuing.
    Useful when the operator is watching via Hyper-V Manager or a VNC viewer.

.PARAMETER StartupWaitSeconds
    Seconds to wait for the application window to appear after launch. Default: 15.

.PARAMETER StepDelaySeconds
    Seconds to wait between automation steps for UI to settle. Default: 1.

.EXAMPLE
    # Typically invoked by Start-UIAutomation.ps1 via PowerShell Direct:
    .\Invoke-InVMAutomation.ps1 -AppExe "C:\automation\app\Arbor.HttpClient.Desktop.exe" `
                                 -ScreenshotDir "C:\automation\screenshots"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AppExe,

    [string]$ScreenshotDir       = "C:\automation\screenshots",
    [switch]$PauseAfterEachStep,
    [int]$StartupWaitSeconds     = 15,
    [int]$StepDelaySeconds       = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Win32 P/Invoke helpers
# ---------------------------------------------------------------------------

if (-not ([System.Management.Automation.PSTypeName]'Win32').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32 {
    // Mouse
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP   = 0x0004;

    // Keyboard
    [DllImport("user32.dll", SetLastError = true)]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    public const uint KEYEVENTF_KEYDOWN = 0x0000;
    public const uint KEYEVENTF_KEYUP   = 0x0002;

    // Window management
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static void ClickAt(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(80);
        mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(80);
        mouse_event(MOUSEEVENTF_LEFTUP,   x, y, 0, IntPtr.Zero);
    }

    public static void DoubleClickAt(int x, int y) {
        ClickAt(x, y);
        System.Threading.Thread.Sleep(150);
        ClickAt(x, y);
    }

    // VK codes
    public const byte VK_CONTROL = 0x11;
    public const byte VK_A       = 0x41;
    public const byte VK_RETURN  = 0x0D;
    public const byte VK_DELETE  = 0x2E;

    public static void SelectAll() {
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
        keybd_event(VK_A,       0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        keybd_event(VK_A,       0, KEYEVENTF_KEYUP,   IntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP,   IntPtr.Zero);
    }

    public static void PressEnter() {
        keybd_event(VK_RETURN, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP,   IntPtr.Zero);
    }
}
'@ -Language CSharp
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# ---------------------------------------------------------------------------
# Layout constants — 1280 × 800 maximised window
# ---------------------------------------------------------------------------
# These coordinates are relative to the full virtual screen (origin = top-left
# of the primary monitor). Adjust if the screen resolution or app layout changes.

$WINDOW_X         = 0
$WINDOW_Y         = 0
$WINDOW_W         = 1280
$WINDOW_H         = 800

# URL bar (AvaloniaEdit TextEditor, centre of the text area)
$URL_BAR_X        = 640
$URL_BAR_Y        = 69

# "Send" button (right side of the toolbar)
$SEND_BTN_X       = 1224
$SEND_BTN_Y       = 69

# "Variables" tab in the left panel (approximate)
$VARS_TAB_X       = 80
$VARS_TAB_Y       = 320

# "Scheduled Jobs" tab in the left panel (approximate)
$SCHED_TAB_X      = 80
$SCHED_TAB_Y      = 370

$DEMO_URL         = "https://postman-echo.com/get?hello=world"

# ---------------------------------------------------------------------------
# Helper functions
# ---------------------------------------------------------------------------

function Write-Step {
    param([string]$Msg)
    Write-Verbose "==> $Msg"
    Write-Host    "==> $Msg" -ForegroundColor Cyan
}

function Invoke-AutoStep {
    param([string]$StepName, [scriptblock]$Action)
    Write-Step $StepName
    & $Action
    Start-Sleep -Seconds $StepDelaySeconds
    Save-Screenshot -Name $StepName
    if ($PauseAfterEachStep) {
        Write-Host "`n[PAUSE] '$StepName' — inspect the VM, then press ENTER to continue." -ForegroundColor Yellow
        $null = Read-Host
    }
}

function Save-Screenshot {
    param([string]$Name)
    $safe     = $Name -replace '[\\/:*?"<>|]', '_'
    $filename = Join-Path $ScreenshotDir "step-$($script:StepCounter.ToString('D2'))-$safe.png"
    $script:StepCounter++

    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp    = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $g      = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([System.Drawing.Point]::Empty, [System.Drawing.Point]::Empty, $bounds.Size)
    $g.Dispose()
    $bmp.Save($filename, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    Write-Host "    Screenshot: $filename" -ForegroundColor DarkGray
}

function Wait-AppWindow {
    param([string]$ProcessName, [int]$TimeoutSec)
    Write-Host "    Waiting for '$ProcessName' window (up to ${TimeoutSec}s)..." -ForegroundColor DarkGray
    $deadline = [DateTime]::Now.AddSeconds($TimeoutSec)
    while ([DateTime]::Now -lt $deadline) {
        $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
        if ($proc) {
            Start-Sleep -Seconds 2   # Give the window time to render
            [Win32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
            [Win32]::ShowWindow($proc.MainWindowHandle, 3) | Out-Null  # SW_MAXIMIZE
            Start-Sleep -Seconds 1
            return $proc
        }
        Start-Sleep -Seconds 1
    }
    throw "Application window did not appear within ${TimeoutSec} seconds."
}

function Bring-AppToFront {
    param([System.Diagnostics.Process]$Proc)
    [Win32]::SetForegroundWindow($Proc.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
}

# ---------------------------------------------------------------------------
# Main automation sequence
# ---------------------------------------------------------------------------

New-Item -ItemType Directory -Force -Path $ScreenshotDir | Out-Null
$script:StepCounter = 1

Write-Step "Starting Arbor.HttpClient.Desktop"
$exeName = [System.IO.Path]::GetFileNameWithoutExtension($AppExe)
$proc = Start-Process -FilePath $AppExe -PassThru

$proc = Wait-AppWindow -ProcessName $exeName -TimeoutSec $StartupWaitSeconds
Bring-AppToFront $proc
Start-Sleep -Seconds 2

# Step 01 — Initial state (app just opened)
Invoke-AutoStep "App opened (initial state)" {
    # Nothing to do — just capture the initial window
    Bring-AppToFront $proc
}

# Step 02 — Click URL bar and type demo URL
Invoke-AutoStep "Type URL into request bar" {
    Bring-AppToFront $proc
    [Win32]::ClickAt($URL_BAR_X, $URL_BAR_Y)
    Start-Sleep -Milliseconds 300
    [Win32]::SelectAll()
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait($DEMO_URL)
    Start-Sleep -Milliseconds 500
}

# Step 03 — Click the Send button
Invoke-AutoStep "Click Send button" {
    Bring-AppToFront $proc
    [Win32]::ClickAt($SEND_BTN_X, $SEND_BTN_Y)
}

# Step 04 — Wait for and capture response
Invoke-AutoStep "HTTP response received" {
    Bring-AppToFront $proc
    # Allow up to 10 s for the real HTTP response to arrive
    Start-Sleep -Seconds ([Math]::Max(0, 10 - $StepDelaySeconds))
}

# Step 05 — Open the Variables panel
Invoke-AutoStep "Variables panel" {
    Bring-AppToFront $proc
    [Win32]::ClickAt($VARS_TAB_X, $VARS_TAB_Y)
    Start-Sleep -Milliseconds 500
}

# Step 06 — Open the Scheduled Jobs panel
Invoke-AutoStep "Scheduled Jobs panel" {
    Bring-AppToFront $proc
    [Win32]::ClickAt($SCHED_TAB_X, $SCHED_TAB_Y)
    Start-Sleep -Milliseconds 500
}

# ---------------------------------------------------------------------------
# Shutdown
# ---------------------------------------------------------------------------

Write-Step "Closing application"
try {
    $proc.CloseMainWindow() | Out-Null
    if (-not $proc.WaitForExit(5000)) {
        $proc.Kill()
    }
}
catch {
    # Process may have already exited
}

$count = (Get-ChildItem -Path $ScreenshotDir -Filter "*.png" -ErrorAction SilentlyContinue).Count
Write-Host "`n    Automation complete. $count screenshots saved to $ScreenshotDir" -ForegroundColor Green
return "OK:$count"
