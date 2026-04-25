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

.PARAMETER ProbeEnvOutputs
    Hashtable of outputs from the probe-env step.

.PARAMETER EnableHvOutputs
    Hashtable of outputs from the enable-hv step.

.PARAMETER RestoreOutcome
    Outcome string from the restore step.

.PARAMETER PublishOutcome
    Outcome string from the publish step.

.PARAMETER PublishOk
    Whether the exe was found after publish.

.PARAMETER VmOutputs
    Hashtable of outputs from the vm-create step.

.PARAMETER LaunchOutputs
    Hashtable of outputs from the launch-app step.
#>
[CmdletBinding()]
param(
    [string]   $RunUrl            = '',
    [string]   $RunNumber         = '',
    [string]   $PullRequestNumber = '',
    [string]   $Repository        = '',
    [hashtable]$ProbeEnvOutputs   = @{},
    [hashtable]$EnableHvOutputs   = @{},
    [string]   $RestoreOutcome    = '',
    [string]   $PublishOutcome    = '',
    [string]   $PublishOk         = '',
    [hashtable]$VmOutputs         = @{},
    [hashtable]$LaunchOutputs     = @{}
)

function Get-Val([hashtable]$ht, [string]$key) {
    if ($ht.ContainsKey($key)) { return $ht[$key] } else { return '' }
}

$artifactNote = "Screenshots, recording, and validation frame uploaded as the **ui-automation-artifacts** artifact in the [run]($RunUrl)."

$body = @"
## Experimental VM / Hyper-V Probe - Run $RunNumber

> **This job is non-blocking** (continue-on-error: true). Results below are for
> iterative development of the VM-based UI automation pipeline.

### Environment
| Check | Result |
|---|---|
| CPU virtualisation firmware enabled | $(Get-Val $ProbeEnvOutputs 'CPU_VIRT') |
| Hyper-V PS module available | $(Get-Val $ProbeEnvOutputs 'HV_MODULE') |
| Enable-WindowsOptionalFeature | $(Get-Val $EnableHvOutputs 'RESTART_NEEDED') |

### App Build
| Step | Outcome |
|---|---|
| dotnet restore | $RestoreOutcome |
| dotnet publish win-x64 self-contained | $PublishOutcome (exe found: $PublishOk) |

### Hyper-V Nested VM
| Step | Outcome |
|---|---|
| New-VHD + New-VM (no OS) | $(Get-Val $VmOutputs 'VM_RESULT') |
| Start-VM | $(Get-Val $VmOutputs 'VM_START') |

### App Launch and UI Automation
| Check | Result |
|---|---|
| App running after init | $(Get-Val $LaunchOutputs 'APP_RUNNING') |
| Window handle found | $(Get-Val $LaunchOutputs 'WINDOW_FOUND') |
| UI keyboard interaction | $(Get-Val $LaunchOutputs 'UI_INTERACTION') |
| Before-interaction screenshot | $(Get-Val $LaunchOutputs 'SCREENSHOT_BEFORE') |
| After-interaction screenshot | $(Get-Val $LaunchOutputs 'SCREENSHOT_AFTER') |

### Screen Recording (ffmpeg gdigrab, 5s, 10fps H.264)
| Check | Result |
|---|---|
| Recording started | $(Get-Val $LaunchOutputs 'RECORDING_STARTED') |
| ffmpeg exit code | $(Get-Val $LaunchOutputs 'RECORDING_EXIT') |
| File size (MB) | $(Get-Val $LaunchOutputs 'RECORDING_SIZE_MB') |
| Duration (s) | $(Get-Val $LaunchOutputs 'VIDEO_DURATION') |
| Frame count | $(Get-Val $LaunchOutputs 'VIDEO_FRAMES') |
| Frame has content (not blank) | $(Get-Val $LaunchOutputs 'VIDEO_HAS_CONTENT') |
| Unique pixel colors in sample | $(Get-Val $LaunchOutputs 'VIDEO_UNIQUE_COLORS') |

$artifactNote

---
[Full run logs]($RunUrl)
"@

Write-Host "Posting PR comment to #$PullRequestNumber on $Repository ..."
gh pr comment $PullRequestNumber --body $body --repo $Repository
