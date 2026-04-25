# VM Probe — PR #65 Review Findings

This document captures the code-review findings, known issues, and outstanding work items
identified during the PR #65 agent session
(*feat: VM-based interactive UI automation scripts, agent environment report, and experimental CI probe with screen recording*).
Use it as a reference backlog when fixing issues in future PRs.

---

## 1. Known Runtime Issues (from CI runs)

### 1.1 ffmpeg screen recording not starting — `RECORDING_STARTED=False`

**Affected file:** `scripts/Invoke-UIAutomationProbe.ps1`
**Observed in:** CI run 366 (last run of the PR session)

**Symptom:** `RECORDING_STARTED=False`, recording file size 0, no duration/frame output.

**Root cause (likely):** `ffmpeg -f gdigrab` requires a GDI-accessible desktop window station.
The GitHub-hosted `windows-latest` runner spawns workflow steps in a non-interactive Windows
session. The runner agent itself runs as a service, and the step process may inherit a session
that does not have a visible desktop — so `gdigrab` finds no input surface and exits immediately.

**Evidence:** Steps 3–4 (app launch, window found, UI interaction) all succeed in the same run,
proving the executable and window handle are accessible. Only ffmpeg's capture fails.

**Possible fixes (in order of preference):**
1. Use `StartInfo.UseShellExecute = true` and `WindowStyle = ProcessWindowStyle.Normal` when
   launching ffmpeg so it runs in the interactive desktop session (not inherited from the
   PowerShell host).
2. Launch ffmpeg via `cmd /c start /B ffmpeg ...` to force a new shell in the active session.
3. Replace `gdigrab` with `ddagrab` (Desktop Duplication API), which works in non-interactive
   sessions and is the recommended alternative in newer ffmpeg builds
   (`-f lavfi -i ddagrab=framerate=10`).
4. If a real display is needed: register the runner agent as a scheduled task running under
   an interactive logon session (out-of-scope for CI, applicable for self-hosted runners).

**Workaround in place:** Recording step has `continue-on-error: true`; the PR comment reports
the failure but does not block the build.

---

### 1.2 Stale "Next steps" comment in `Write-ProbeSummary.ps1`

**Affected file:** `scripts/Write-ProbeSummary.ps1`, lines 89–90

**Symptom:** The summary always prints:
```
Screen recording confirmed working. Next: verify VIDEO_HAS_CONTENT=True…
```
…even when `RECORDING_STARTED=False` (as in run 366).

**Fix:** Make the next-steps message conditional on the actual recording outcome, or remove the
hardcoded message and dynamically emit a status-appropriate note based on `RECORDING_STARTED`.

---

## 2. Code Quality Issues

### 2.1 `Take-Screenshot` function not following PowerShell naming convention

**Affected file:** `scripts/Invoke-UIAutomationProbe.ps1`, line 40

**Issue:** PowerShell approved verbs require a `Verb-Noun` pattern. `Take-Screenshot` uses
`Take`, which is not in the approved verb list. This triggers a `PSUseApprovedVerbs` warning
when the script is module-imported.

**Fix:** Rename to `Save-Screenshot` or `Get-Screenshot`.

---

### 2.2 `IDisposable` resources not disposed in `Take-Screenshot`

**Affected file:** `scripts/Invoke-UIAutomationProbe.ps1`, lines 43–47

```powershell
# ✗ Current — Graphics and Bitmap not disposed on exception
$bmp = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bmp.Save($path)
$g.Dispose(); $bmp.Dispose()
```

If `$bmp.Save($path)` throws, neither `$g` nor `$bmp` is disposed. The `catch` block in the
function does not dispose them either.

**Fix:** Use a `try/finally` block:
```powershell
try {
    $bmp = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bmp.Save($path)
} finally {
    if ($g)   { $g.Dispose() }
    if ($bmp) { $bmp.Dispose() }
}
```

---

### 2.3 `IDisposable` not disposed for validation bitmap

**Affected file:** `scripts/Invoke-UIAutomationProbe.ps1`, lines 194–201

```powershell
$vbmp = [System.Drawing.Bitmap]::new((Resolve-Path 'validation-frame.png').Path)
# ... pixel sampling ...
$vbmp.Dispose()  # only called if no exception is thrown mid-loop
```

The bitmap is disposed at the end of the block, but if `GetPixel` throws, the object leaks.

**Fix:** Wrap in `try/finally`.

---

### 2.4 Complex inline parameter expansion in `ci.yml` workflow steps

**Affected file:** `.github/workflows/ci.yml`, lines 382–424 (Write probe summary / Post PR comment steps)

**Issue:** The `run:` blocks construct very long inline PowerShell one-liners that expand
GitHub Actions step outputs directly into PowerShell hashtable literals. This approach:
- Is fragile — any step output containing a single quote or backslash will break the hashtable
  literal at expansion time.
- Is hard to read and maintain.
- Cannot be linted or tested locally without emulating the Actions expansion.

**Fix:** Pass step outputs as individual `-Parameter` values (strings, not hashtable literals)
and let the script build the hashtable internally. This avoids the special-character hazard and
makes the call site readable.

---

### 2.5 Missing `[OutputType]` and `[Parameter]` attributes in scripts

**Affected files:** Most scripts under `scripts/`

**Issue:** None of the scripts declare `[OutputType(...)]` or annotate parameters with
`[Parameter(Mandatory, HelpMessage)]`. This makes PowerShell tooling (ISE, VS Code PS
extension) less helpful and reduces self-documentation quality.

**Severity:** Low — does not affect correctness.

---

## 3. Compliance Items Missed in PR #65

The following items from the end-of-PR compliance checklist (§15 of
`.github/copilot-instructions.md`) were not completed in the PR #65 session:

| Item | Status | Notes |
|---|---|---|
| All tests pass (`dotnet test Arbor.HttpClient.slnx`) | ⚠️ Not verified in session | No test run evidence in PR description |
| Vulnerability audit passes | ⚠️ Not verified | `dotnet list ... --vulnerable` not run |
| `docs/ux-ideas.md` reviewed | ⚠️ Skipped | No UX changes in PR #65, but review was not documented |
| Instruction Retrospective block | ❌ Missing | Not included in PR description |
| Proposed instruction improvement | ❌ Missing | Not applied or tracked |
| `docs/coverage.md` unchanged | ✅ N/A | No production code changes |
| Screenshots embedded in PR description | ✅ N/A | No UI changes (only scripts/docs) |

---

## 4. Outstanding Sub-Tasks (from `docs/vm-ui-automation.md` §8)

These sub-tasks were identified in the PR and are not yet implemented:

| Sub-task | Description | Priority |
|---|---|---|
| **Sub-task 1** | Base VM image preparation guide (step-by-step VHDX / qcow2 instructions) | Medium |
| **Sub-task 5** | Windows UI Automation integration (`System.Windows.Automation`) replacing coordinate-based clicking | High |
| **Sub-task 6** | MSIX installation support inside the VM (end-to-end packaging coverage) | Medium |
| **Sub-task 7** | CI integration via large Linux runners (`/dev/kvm`) or self-hosted Windows runner | High |
| **Sub-task 8** | Result reporting with inline screenshot thumbnails in PR comment | Low |
| **Sub-task 9** | VM pool for parallel runs | Low |

---

## 5. Architecture / Design Notes

### 5.1 `windows-latest` runner is not the right target for full UI automation

All proof-of-concept runs in PR #65 used `windows-latest`. The conclusions from run analysis:
- ✅ Nested Hyper-V (no-OS VM creation) works
- ✅ App launch and window handle detection works
- ✅ `SendKeys` keyboard injection works
- ❌ `ffmpeg gdigrab` screen recording fails (non-interactive session issue — see §1.1)

For reliable screen capture, a **self-hosted Windows runner** or a Windows runner with a
configured interactive desktop session is required.

### 5.2 `gdigrab` vs `ddagrab` for screen capture

The current script uses `ffmpeg -f gdigrab` which is a GDI-based screen grabber and requires
a desktop window station. `ddagrab` (Desktop Duplication API) is the modern alternative for
headless/service contexts and should be preferred on Windows 10/11:

```
ffmpeg -f lavfi -i ddagrab=framerate=10 -t 5 -c:v libx264 -pix_fmt yuv420p -y app-recording.mp4
```

### 5.3 Coordinate-based UI interaction is fragile

`Invoke-InVMAutomation.ps1` uses hardcoded screen coordinates from the default 1280×800 layout.
If the application layout changes or the display resolution differs, clicks will miss their
targets. Sub-task 5 (UI Automation) is the correct long-term fix.

---

## 6. How to Use This Document

When working on a PR that touches the VM probe infrastructure:

1. Check §1 for known runtime issues and implement the suggested fixes.
2. Check §2 for code quality items — fix any that are in code you are modifying.
3. Run the full PR compliance checklist (`.github/prompts/pr-checklist.prompt.md`) and ensure
   the items in §3 are all completed.
4. If a sub-task from §4 is implemented, update this file and `docs/vm-ui-automation.md`
   with the sub-task number, PR reference, and commit SHA.

When a finding in this document is resolved, move it to a **Resolved** section at the bottom
with the PR number and commit SHA that fixed it.

---

## Resolved Findings

*(none yet)*
