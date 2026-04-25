## Experimental VM / Hyper-V Probe Results

This job is non-blocking (continue-on-error: true). Results are for diagnostic
purposes only and do not affect the CI outcome.

### Environment
| Check | Result |
|---|---|
| CPU virtualisation firmware enabled | True |
| Hyper-V Windows feature state | Enabled |
| Hyper-V PS module available | True |
| Enable-WindowsOptionalFeature | RestartNeeded=False |

### App Build
| Step | Outcome |
|---|---|
| dotnet restore | success |
| dotnet publish (win-x64 self-contained) | success (True) |

### VM Creation (Hyper-V nested virtualisation)
| Step | Outcome |
|---|---|
| New-VHD + New-VM (no OS) | VM-created-OK |
| Start-VM | started-state-Running |

### App launch and UI automation (direct on runner)
| Step | Outcome |
|---|---|
| App running after init | True |
| Window handle found | True |
| UI keyboard interaction | ok |
| Before-interaction screenshot | True |
| After-interaction screenshot | True |

### Screen recording (ffmpeg gdigrab)
| Check | Result |
|---|---|
| Recording started | True |
| ffmpeg exit code | 0 |
| File size (MB) | 1.23 |
| Duration (s) | 5.01 |
| Frame count | 50 |
| Frame has content | True |
| Unique pixel colors in sample | 42 |

### Next steps
- Screen recording confirmed working. Next: verify VIDEO_HAS_CONTENT=True
  in the PR comment, and check validation-frame.png in the ui-automation-artifacts artifact.
