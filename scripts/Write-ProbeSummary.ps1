<#
.SYNOPSIS
    Writes a Markdown probe summary table to probe-summary.md and (when running in
    GitHub Actions) appends it to the job summary ($GITHUB_STEP_SUMMARY).

.PARAMETER ProbeEnvOutputs
    Hashtable of outputs from the probe-env step (CPU_VIRT, HV_STATE, HV_MODULE).

.PARAMETER EnableHvOutputs
    Hashtable of outputs from the enable-hv step (RESTART_NEEDED).

.PARAMETER RestoreOutcome
    Outcome string from the restore step (e.g. "success", "failure", "skipped").

.PARAMETER PublishOutcome
    Outcome string from the publish step.

.PARAMETER PublishOk
    Whether the exe was found after publish ("True"/"False").

.PARAMETER VmOutputs
    Hashtable of outputs from the vm-create step (VM_RESULT, VM_START).

.PARAMETER LaunchOutputs
    Hashtable of outputs from the launch-app step.
#>
[CmdletBinding()]
param(
    [hashtable]$ProbeEnvOutputs = @{},
    [hashtable]$EnableHvOutputs = @{},
    [string]   $RestoreOutcome  = '',
    [string]   $PublishOutcome  = '',
    [string]   $PublishOk       = '',
    [hashtable]$VmOutputs       = @{},
    [hashtable]$LaunchOutputs   = @{}
)

function Get-Val([hashtable]$ht, [string]$key) {
    if ($ht.ContainsKey($key)) { return $ht[$key] } else { return '' }
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
    "| CPU virtualisation firmware enabled | $(Get-Val $ProbeEnvOutputs 'CPU_VIRT') |"
    "| Hyper-V Windows feature state | $(Get-Val $ProbeEnvOutputs 'HV_STATE') |"
    "| Hyper-V PS module available | $(Get-Val $ProbeEnvOutputs 'HV_MODULE') |"
    "| Enable-WindowsOptionalFeature | $(Get-Val $EnableHvOutputs 'RESTART_NEEDED') |"
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
    "| New-VHD + New-VM (no OS) | $(Get-Val $VmOutputs 'VM_RESULT') |"
    "| Start-VM | $(Get-Val $VmOutputs 'VM_START') |"
    ""
    "### App launch and UI automation (direct on runner)"
    "| Step | Outcome |"
    "|---|---|"
    "| App running after init | $(Get-Val $LaunchOutputs 'APP_RUNNING') |"
    "| Window handle found | $(Get-Val $LaunchOutputs 'WINDOW_FOUND') |"
    "| UI keyboard interaction | $(Get-Val $LaunchOutputs 'UI_INTERACTION') |"
    "| Before-interaction screenshot | $(Get-Val $LaunchOutputs 'SCREENSHOT_BEFORE') |"
    "| After-interaction screenshot | $(Get-Val $LaunchOutputs 'SCREENSHOT_AFTER') |"
    ""
    "### Screen recording (ffmpeg gdigrab)"
    "| Check | Result |"
    "|---|---|"
    "| Recording started | $(Get-Val $LaunchOutputs 'RECORDING_STARTED') |"
    "| ffmpeg exit code | $(Get-Val $LaunchOutputs 'RECORDING_EXIT') |"
    "| File size (MB) | $(Get-Val $LaunchOutputs 'RECORDING_SIZE_MB') |"
    "| Duration (s) | $(Get-Val $LaunchOutputs 'VIDEO_DURATION') |"
    "| Frame count | $(Get-Val $LaunchOutputs 'VIDEO_FRAMES') |"
    "| Frame has content | $(Get-Val $LaunchOutputs 'VIDEO_HAS_CONTENT') |"
    "| Unique pixel colors in sample | $(Get-Val $LaunchOutputs 'VIDEO_UNIQUE_COLORS') |"
    ""
    "### Next steps"
    '- Screen recording confirmed working. Next: verify VIDEO_HAS_CONTENT=True'
    '  in the PR comment, and check validation-frame.png in the ui-automation-artifacts artifact.'
)

$md = $lines -join "`n"
$md | Out-File -FilePath probe-summary.md -Encoding UTF8

if ($env:GITHUB_STEP_SUMMARY) {
    $md | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Encoding UTF8 -Append
}

Write-Host $md
