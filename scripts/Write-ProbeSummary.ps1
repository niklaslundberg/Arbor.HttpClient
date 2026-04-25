<#
.SYNOPSIS
    Writes a Markdown probe summary table to probe-summary.md and (when running in
    GitHub Actions) appends it to the job summary ($GITHUB_STEP_SUMMARY).

.PARAMETER CpuVirt
    CPU virtualisation firmware enabled (from probe-env step output CPU_VIRT).

.PARAMETER HvState
    Hyper-V Windows feature state (from probe-env step output HV_STATE).

.PARAMETER HvModule
    Hyper-V PS module available (from probe-env step output HV_MODULE).

.PARAMETER HvEnable
    Result of Enable-WindowsOptionalFeature (from enable-hv step output RESTART_NEEDED).

.PARAMETER RestoreOutcome
    Outcome string from the restore step (e.g. "success", "failure", "skipped").

.PARAMETER PublishOutcome
    Outcome string from the publish step.

.PARAMETER PublishOk
    Whether the exe was found after publish ("True"/"False").

.PARAMETER VmResult
    Result of New-VHD + New-VM (from vm-create step output VM_RESULT).

.PARAMETER VmStart
    Result of Start-VM (from vm-create step output VM_START).

.PARAMETER AppRunning
    Whether the app was still running after init (from launch-app output APP_RUNNING).

.PARAMETER WindowFound
    Whether the main window handle was found (from launch-app output WINDOW_FOUND).

.PARAMETER UiInteraction
    Result of keyboard interaction (from launch-app output UI_INTERACTION).

.PARAMETER ScreenshotBefore
    Whether the before-interaction screenshot was saved (SCREENSHOT_BEFORE).

.PARAMETER ScreenshotAfter
    Whether the after-interaction screenshot was saved (SCREENSHOT_AFTER).

.PARAMETER RecordingStarted
    Whether ffmpeg recording was started (RECORDING_STARTED).

.PARAMETER RecordingExit
    ffmpeg exit code or "timeout" (RECORDING_EXIT).

.PARAMETER RecordingSizeMb
    Recording file size in MB (RECORDING_SIZE_MB).

.PARAMETER VideoDuration
    Video duration in seconds (VIDEO_DURATION).

.PARAMETER VideoFrames
    Video frame count (VIDEO_FRAMES).

.PARAMETER VideoHasContent
    Whether the validation frame contained non-blank content (VIDEO_HAS_CONTENT).

.PARAMETER VideoUniqueColors
    Number of unique pixel colors in the validation frame sample (VIDEO_UNIQUE_COLORS).
#>
[CmdletBinding()]
param(
    [string]$CpuVirt           = '',
    [string]$HvState           = '',
    [string]$HvModule          = '',
    [string]$HvEnable          = '',
    [string]$RestoreOutcome    = '',
    [string]$PublishOutcome    = '',
    [string]$PublishOk         = '',
    [string]$VmResult          = '',
    [string]$VmStart           = '',
    [string]$AppRunning        = '',
    [string]$WindowFound       = '',
    [string]$UiInteraction     = '',
    [string]$ScreenshotBefore  = '',
    [string]$ScreenshotAfter   = '',
    [string]$RecordingStarted  = '',
    [string]$RecordingExit     = '',
    [string]$RecordingSizeMb   = '',
    [string]$VideoDuration     = '',
    [string]$VideoFrames       = '',
    [string]$VideoHasContent   = '',
    [string]$VideoUniqueColors = ''
)

$nextStep = if ($RecordingStarted -eq 'True') {
    if ($VideoHasContent -eq 'True') {
        '- ✅ Recording confirmed has content. Check validation-frame.png in ui-automation-artifacts.'
    } elseif ($VideoHasContent -eq 'False') {
        '- ⚠️ Recording exists but appears blank. Check desktop-before.png and rec-log.txt for clues.'
    } else {
        '- Recording started. Check ui-automation-artifacts for app-recording.mp4 and validation-frame.png.'
    }
} else {
    '- ❌ Recording did not start. ffmpeg may not be in PATH or the capture device was inaccessible.'
}

$lines = @(
    "## Experimental VM / Hyper-V Probe Results"
    ""
    "This job is non-blocking (continue-on-error: true). Results are for diagnostic"
    "purposes only and do not affect the CI outcome."
    ""
    "### Environment"
    "| Check | Result |"
    "|---|---|"
    "| CPU virtualisation firmware enabled | $CpuVirt |"
    "| Hyper-V Windows feature state | $HvState |"
    "| Hyper-V PS module available | $HvModule |"
    "| Enable-WindowsOptionalFeature | $HvEnable |"
    ""
    "### App Build"
    "| Step | Outcome |"
    "|---|---|"
    "| dotnet restore | $RestoreOutcome |"
    "| dotnet publish (win-x64 self-contained) | $PublishOutcome ($PublishOk) |"
    ""
    "### VM Creation (Hyper-V nested virtualisation)"
    "| Step | Outcome |"
    "|---|---|"
    "| New-VHD + New-VM (no OS) | $VmResult |"
    "| Start-VM | $VmStart |"
    ""
    "### App launch and UI automation (direct on runner)"
    "| Step | Outcome |"
    "|---|---|"
    "| App running after init | $AppRunning |"
    "| Window handle found | $WindowFound |"
    "| UI keyboard interaction | $UiInteraction |"
    "| Before-interaction screenshot | $ScreenshotBefore |"
    "| After-interaction screenshot | $ScreenshotAfter |"
    ""
    "### Screen recording (ffmpeg ddagrab/gdigrab)"
    "| Check | Result |"
    "|---|---|"
    "| Recording started | $RecordingStarted |"
    "| ffmpeg exit code | $RecordingExit |"
    "| File size (MB) | $RecordingSizeMb |"
    "| Duration (s) | $VideoDuration |"
    "| Frame count | $VideoFrames |"
    "| Frame has content | $VideoHasContent |"
    "| Unique pixel colors in sample | $VideoUniqueColors |"
    ""
    "### Next steps"
    $nextStep
)

$md = $lines -join "`n"
$md | Out-File -FilePath probe-summary.md -Encoding UTF8

if ($env:GITHUB_STEP_SUMMARY) {
    $md | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Encoding UTF8 -Append
}

Write-Host $md
