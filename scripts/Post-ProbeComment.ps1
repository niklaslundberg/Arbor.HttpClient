<#
.SYNOPSIS
    Posts a Markdown summary of the experimental VM probe results as a comment on the
    pull request that triggered the workflow.

.PARAMETER RunUrl
    Full URL of the GitHub Actions run (e.g. https://github.com/owner/repo/actions/runs/12345).

.PARAMETER RunNumber
    GitHub run number (${{ github.run_number }}).

.PARAMETER PullRequestNumber
    Pull request number (${{ github.event.pull_request.number }}).

.PARAMETER Repository
    GitHub repository in "owner/name" format (${{ github.repository }}).

.PARAMETER CpuVirt
    CPU virtualisation firmware enabled (from probe-env step output CPU_VIRT).

.PARAMETER HvModule
    Hyper-V PS module available (from probe-env step output HV_MODULE).

.PARAMETER HvEnable
    Result of Enable-WindowsOptionalFeature (from enable-hv step output RESTART_NEEDED).

.PARAMETER RestoreOutcome
    Outcome string from the restore step.

.PARAMETER PublishOutcome
    Outcome string from the publish step.

.PARAMETER PublishOk
    Whether the exe was found after publish.

.PARAMETER VmResult
    Result of New-VHD + New-VM (from vm-create step output VM_RESULT).

.PARAMETER VmStart
    Result of Start-VM (from vm-create step output VM_START).

.PARAMETER AppRunning
    Whether the app was still running after init (APP_RUNNING).

.PARAMETER WindowFound
    Whether the main window handle was found (WINDOW_FOUND).

.PARAMETER UiInteraction
    Result of keyboard interaction (UI_INTERACTION).

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
    [string]$RunUrl            = '',
    [string]$RunNumber         = '',
    [string]$PullRequestNumber = '',
    [string]$Repository        = '',
    [string]$CpuVirt           = '',
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

$artifactNote = "Screenshots, recording, and validation frame uploaded as the **ui-automation-artifacts** artifact in the [run]($RunUrl)."

$body = @"
## Experimental VM / Hyper-V Probe - Run $RunNumber

> **This job is non-blocking** (continue-on-error: true). Results below are for
> iterative development of the VM-based UI automation pipeline.

### Environment
| Check | Result |
|---|---|
| CPU virtualisation firmware enabled | $CpuVirt |
| Hyper-V PS module available | $HvModule |
| Enable-WindowsOptionalFeature | $HvEnable |

### App Build
| Step | Outcome |
|---|---|
| dotnet restore | $RestoreOutcome |
| dotnet publish win-x64 self-contained | $PublishOutcome (exe found: $PublishOk) |

### Hyper-V Nested VM
| Step | Outcome |
|---|---|
| New-VHD + New-VM (no OS) | $VmResult |
| Start-VM | $VmStart |

### App Launch and UI Automation
| Check | Result |
|---|---|
| App running after init | $AppRunning |
| Window handle found | $WindowFound |
| UI keyboard interaction | $UiInteraction |
| Before-interaction screenshot | $ScreenshotBefore |
| After-interaction screenshot | $ScreenshotAfter |

### Screen Recording (ffmpeg ddagrab/gdigrab, 5s, 10fps H.264)
| Check | Result |
|---|---|
| Recording started | $RecordingStarted |
| ffmpeg exit code | $RecordingExit |
| File size (MB) | $RecordingSizeMb |
| Duration (s) | $VideoDuration |
| Frame count | $VideoFrames |
| Frame has content (not blank) | $VideoHasContent |
| Unique pixel colors in sample | $VideoUniqueColors |

$artifactNote

---
[Full run logs]($RunUrl)
"@

Write-Host "Posting PR comment to #$PullRequestNumber on $Repository ..."
gh pr comment $PullRequestNumber --body $body --repo $Repository
