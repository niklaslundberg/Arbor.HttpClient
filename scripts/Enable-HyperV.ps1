<#
.SYNOPSIS
    Attempts to enable the Hyper-V Windows optional feature and reports the outcome
    to GITHUB_OUTPUT for the experimental VM probe job.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest

try {
    $result = Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All `
        -All -NoRestart -ErrorAction Stop
    $state = if ($result) { "RestartNeeded=$($result.RestartNeeded)" } else { "Enabled" }
}
catch {
    # Common on Windows Server runners: feature name is different or nested-virt is already
    # active via the hypervisor. This is expected and not a fatal error.
    $state = "Unavailable: $($_.Exception.Message)"
}

Write-Host "Enable-WindowsOptionalFeature result: $state"

if ($env:GITHUB_OUTPUT) {
    "RESTART_NEEDED=$state" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}
