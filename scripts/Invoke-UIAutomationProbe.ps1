<#
.SYNOPSIS
    Launches the Arbor.HttpClient.Desktop.exe on the runner desktop, records a
    5-second screen capture using ffmpeg gdigrab, drives the app with Win32
    SendKeys, and validates the recording contains non-blank content.
    Reports outputs to GITHUB_OUTPUT.

.PARAMETER ExePath
    Path to the self-contained Arbor.HttpClient.Desktop.exe. Defaults to
    publish/win-x64/Arbor.HttpClient.Desktop.exe relative to the working directory.
#>
[CmdletBinding()]
param(
    [string]$ExePath = "publish/win-x64/Arbor.HttpClient.Desktop.exe"
)

Set-StrictMode -Version Latest

if (-not (Test-Path $ExePath)) {
    Write-Host "Exe not found at $ExePath — skipping."
    if ($env:GITHUB_OUTPUT) {
        "APP_RUNNING=False" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$csharp = @(
    'using System;'
    'using System.Runtime.InteropServices;'
    'public class Win32Ui {'
    '    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);'
    '    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);'
    '}'
) -join "`n"
Add-Type -TypeDefinition $csharp -ErrorAction SilentlyContinue

function Take-Screenshot([string]$path) {
    try {
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
        $bmp.Save($path)
        $g.Dispose(); $bmp.Dispose()
        Write-Host "Screenshot saved: $path ($($bounds.Width)x$($bounds.Height))"
        return $true
    }
    catch { Write-Host "Screenshot failed: $_"; return $false }
}

Write-Host "Launching: $ExePath"
$proc = Start-Process -FilePath $ExePath -PassThru -WindowStyle Normal
if (-not $proc) {
    if ($env:GITHUB_OUTPUT) {
        "APP_RUNNING=False" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
    exit 0
}
Write-Host "Started PID: $($proc.Id)"
$proc.WaitForInputIdle(10000) | Out-Null
Start-Sleep -Seconds 2

$proc.Refresh()
$running = -not $proc.HasExited
Write-Host "Running: $running"
if ($env:GITHUB_OUTPUT) {
    "APP_RUNNING=$running" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}

if (-not $running) { exit 0 }

$hwnd = $proc.MainWindowHandle
Write-Host "Main window handle: $hwnd  Title: '$($proc.MainWindowTitle)'"
if ($env:GITHUB_OUTPUT) {
    "WINDOW_FOUND=$($hwnd -ne [IntPtr]::Zero)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}

if ($hwnd -ne [IntPtr]::Zero) {
    [Win32Ui]::ShowWindow($hwnd, 9)        # SW_RESTORE
    [Win32Ui]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 500
}

# Start ffmpeg screen recording: 5s, gdigrab, 10fps, H.264
$ff = Get-Command ffmpeg -ErrorAction SilentlyContinue
$recProc = $null
if ($ff) {
    Write-Host "Starting 5s screen recording (gdigrab 10fps H.264)..."
    $recArgs = '-f gdigrab -framerate 10 -i desktop -t 5 -c:v libx264 -pix_fmt yuv420p -y app-recording.mp4'
    $recProc = Start-Process -FilePath $ff.Source `
        -ArgumentList $recArgs `
        -PassThru -WindowStyle Hidden `
        -RedirectStandardError rec-log.txt
    if ($env:GITHUB_OUTPUT) {
        "RECORDING_STARTED=True" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
} else {
    Write-Host "ffmpeg not found in PATH — recording skipped"
    if ($env:GITHUB_OUTPUT) {
        "RECORDING_STARTED=False" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}

# Before-interaction screenshot (captured at recording start)
$s1 = Take-Screenshot 'desktop-before.png'
if ($env:GITHUB_OUTPUT) {
    "SCREENSHOT_BEFORE=$s1" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}

# Send keyboard input while recording is in progress
if ($hwnd -ne [IntPtr]::Zero) {
    Start-Sleep -Seconds 1
    [System.Windows.Forms.SendKeys]::SendWait("{TAB}")
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait("{TAB}")
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait("https://example.com{ENTER}")
    Start-Sleep -Milliseconds 500
    Write-Host "Sent keyboard input OK"
    if ($env:GITHUB_OUTPUT) {
        "UI_INTERACTION=ok" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
} else {
    Write-Host "No window handle — skipping keyboard input"
    if ($env:GITHUB_OUTPUT) {
        "UI_INTERACTION=no-window" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}

# Wait for ffmpeg to finish (it exits after -t 5s)
if ($recProc) {
    if (-not $recProc.WaitForExit(12000)) {
        Write-Host "Recording timed out — killing ffmpeg"
        $recProc | Stop-Process -Force -ErrorAction SilentlyContinue
        if ($env:GITHUB_OUTPUT) {
            "RECORDING_EXIT=timeout" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
        }
    } else {
        Write-Host "Recording exit code: $($recProc.ExitCode)"
        if ($env:GITHUB_OUTPUT) {
            "RECORDING_EXIT=$($recProc.ExitCode)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
        }
    }
    if (Test-Path 'rec-log.txt') { Get-Content 'rec-log.txt' -Tail 10 | Write-Host }
}

# After-interaction screenshot (after recording has finished)
$s2 = Take-Screenshot 'desktop-after.png'
if ($env:GITHUB_OUTPUT) {
    "SCREENSHOT_AFTER=$s2" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}

$proc | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "App stopped."

# --- Validate the recording ---
if (-not (Test-Path 'app-recording.mp4')) {
    Write-Host "No recording file produced"
    if ($env:GITHUB_OUTPUT) {
        "RECORDING_SIZE_MB=0" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
    exit 0
}

$sizeMB = [math]::Round((Get-Item 'app-recording.mp4').Length / 1MB, 2)
Write-Host "Recording file size: $sizeMB MB"
if ($env:GITHUB_OUTPUT) {
    "RECORDING_SIZE_MB=$sizeMB" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}

# Use ffprobe to read duration and frame count
$fp = Get-Command ffprobe -ErrorAction SilentlyContinue
if ($fp) {
    $probeOut = & $fp.Source -v quiet -print_format json -show_streams app-recording.mp4 2>&1
    try {
        $pj   = $probeOut | ConvertFrom-Json
        $dur  = [math]::Round([double]$pj.streams[0].duration, 2)
        $frms = $pj.streams[0].nb_frames
        Write-Host "Duration: ${dur}s  Frames: $frms"
        if ($env:GITHUB_OUTPUT) {
            "VIDEO_DURATION=$dur" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
            "VIDEO_FRAMES=$frms"  | Out-File -FilePath $env:GITHUB_OUTPUT -Append
        }
    } catch { Write-Host "ffprobe JSON parse failed: $_" }
}

# Extract frame at t=2s then count unique mid-row pixel colors as content check
if ($ff) {
    & $ff.Source -i app-recording.mp4 -ss 2 -vframes 1 -y validation-frame.png 2>$null
    if (Test-Path 'validation-frame.png') {
        $vbmp = [System.Drawing.Bitmap]::new((Resolve-Path 'validation-frame.png').Path)
        $step = [math]::Max(1, [int]($vbmp.Width / 20))
        $argbList = @()
        for ($xi = 0; $xi -lt $vbmp.Width; $xi += $step) {
            $argbList += $vbmp.GetPixel($xi, [int]($vbmp.Height / 2)).ToArgb()
        }
        $vbmp.Dispose()
        $unique     = ($argbList | Sort-Object -Unique).Count
        $hasContent = $unique -gt 2
        Write-Host "Unique pixel colors in mid-row sample: $unique  Has content: $hasContent"
        if ($env:GITHUB_OUTPUT) {
            "VIDEO_HAS_CONTENT=$hasContent" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
            "VIDEO_UNIQUE_COLORS=$unique"   | Out-File -FilePath $env:GITHUB_OUTPUT -Append
        }
    }
}
