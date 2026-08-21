#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Three-way hotspot audit after merging an upstream osu tag into Ez2Lazer.

.DESCRIPTION
  For each P0/P1 path, prints:
    1) UpstreamDelta  = git diff OldTag NewTag -- path
    2) EzDelta        = git diff NewTag HEAD -- path
  Does not modify the working tree. Annotate results as ok / missed / intentional-Ez
  per docs/EZ-UPSTREAM-MERGE.md.

.PARAMETER OldTag
  Previous merged upstream tag (e.g. 2026.807.0-tachyon).

.PARAMETER NewTag
  Newly merged upstream tag (e.g. 2026.819.0-tachyon).

.PARAMETER HeadRef
  Ref to treat as post-merge tip (default HEAD).

.PARAMETER SummaryOnly
  Only print whether each path has upstream/ez deltas (no full diffs).

.EXAMPLE
  pwsh ./scripts/AuditUpstreamHotspots.ps1 -OldTag 2026.807.0-tachyon -NewTag 2026.819.0-tachyon -SummaryOnly
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OldTag,

    [Parameter(Mandatory = $true)]
    [string] $NewTag,

    [string] $HeadRef = "HEAD",

    [switch] $SummaryOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

function Assert-GitRef([string] $ref) {
    git rev-parse --verify "$ref^{commit}" 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Git ref not found: $ref"
    }
}

Assert-GitRef $OldTag
Assert-GitRef $NewTag
Assert-GitRef $HeadRef

# Keep in sync with docs/EZ-UPSTREAM-MERGE.md P0/P1 tables.
$hotspots = @(
    # P0
    "osu.Game/Screens/Select/FilterControl.cs"
    "osu.Game/Screens/Select/SongSelect.cs"
    "osu.Game/Screens/Select/BeatmapCarousel.cs"
    "osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs"
    "osu.Game/Screens/Select/FilterCriteria.cs"
    "osu.Game/Screens/Select/PanelBeatmap.cs"
    "osu.Game/Screens/Select/PanelBeatmapStandalone.cs"
    "osu.Game/Database/RealmAccess.cs"
    "osu.Game/OsuGame.cs"
    "osu.Game/OsuGameBase.cs"
    "osu.Game/Screens/Play/Player.cs"
    "osu.Game/Beatmaps/BeatmapInfo.cs"
    "osu.Game/Scoring/ScoreInfo.cs"
    "osu.Game.Rulesets.Osu/OsuRuleset.cs"
    "osu.Game.Rulesets.Taiko/TaikoRuleset.cs"
    "osu.Game.Rulesets.Mania/ManiaRuleset.cs"
    "osu.Game.Rulesets.Mania/ManiaSettingsSubsection.cs"
    "osu.Game.Rulesets.Catch/CatchRuleset.cs"
    "osu.Game/Users/Drawables/DrawableAvatar.cs"
    "osu.Game/Overlays/SettingsOverlay.cs"
    "osu.Game/Rulesets/Edit/ScrollingHitObjectComposer.cs"
    "osu.Game.Rulesets.BMS/BMSRuleset.cs"
    # P1
    "osu.Game/Overlays/FirstRunSetup/ScreenBehaviour.cs"
    "osu.Game/Configuration/OsuConfigManager.cs"
)

Write-Host "Ez2Lazer upstream hotspot audit"
Write-Host "  OldTag  : $OldTag"
Write-Host "  NewTag  : $NewTag"
Write-Host "  HeadRef : $HeadRef"
Write-Host "  Repo    : $repoRoot"
Write-Host ""

$rows = @()

foreach ($path in $hotspots) {
    $upstreamStat = git diff --shortstat "$OldTag" "$NewTag" -- $path 2>$null
    $ezStat = git diff --shortstat "$NewTag" "$HeadRef" -- $path 2>$null

    $upstreamChanged = -not [string]::IsNullOrWhiteSpace($upstreamStat)
    $ezDiverged = -not [string]::IsNullOrWhiteSpace($ezStat)

    $flag = if (-not $upstreamChanged -and -not $ezDiverged) {
        "clean"
    }
    elseif ($upstreamChanged -and -not $ezDiverged) {
        "upstream-only"
    }
    elseif (-not $upstreamChanged -and $ezDiverged) {
        "ez-only"
    }
    else {
        "both-review"
    }

    $rows += [pscustomobject]@{
        Path             = $path
        Flag             = $flag
        UpstreamChanged  = $upstreamChanged
        EzDiverged       = $ezDiverged
        UpstreamShortstat = if ($upstreamChanged) { $upstreamStat.Trim() } else { "-" }
        EzShortstat       = if ($ezDiverged) { $ezStat.Trim() } else { "-" }
    }

    Write-Host ("=" * 80)
    Write-Host "PATH: $path"
    Write-Host "FLAG: $flag  (annotate: ok | missed | intentional-Ez)"
    Write-Host "UpstreamDelta ($OldTag..$NewTag): $(if ($upstreamChanged) { $upstreamStat.Trim() } else { '(none)' })"
    Write-Host "EzDelta       ($NewTag..$HeadRef): $(if ($ezDiverged) { $ezStat.Trim() } else { '(none)' })"

    if (-not $SummaryOnly) {
        if ($upstreamChanged) {
            Write-Host ""
            Write-Host "--- UpstreamDelta ---"
            git --no-pager diff --stat "$OldTag" "$NewTag" -- $path
            git --no-pager diff "$OldTag" "$NewTag" -- $path
        }
        if ($ezDiverged) {
            Write-Host ""
            Write-Host "--- EzDelta ---"
            git --no-pager diff --stat "$NewTag" "$HeadRef" -- $path
            # Cap huge Ez-only files: show stat + first hunks via --stat already; full diff for review files.
            if ($flag -eq "both-review" -or $flag -eq "upstream-only") {
                git --no-pager diff "$NewTag" "$HeadRef" -- $path
            }
            else {
                Write-Host "(ez-only: full diff omitted in default mode; re-run without -SummaryOnly and inspect if needed)"
                git --no-pager diff --stat "$NewTag" "$HeadRef" -- $path
            }
        }
    }

    Write-Host ""
}

Write-Host ("=" * 80)
Write-Host "SUMMARY"
Write-Host ""
$rows | Format-Table -AutoSize Path, Flag, UpstreamShortstat, EzShortstat

$review = @($rows | Where-Object { $_.Flag -eq "both-review" -or $_.Flag -eq "upstream-only" })
Write-Host ("Priority review count (both-review + upstream-only): {0}" -f $review.Count)
foreach ($r in $review) {
    Write-Host ("  [{0}] {1}" -f $r.Flag, $r.Path)
}

Write-Host ""
Write-Host "See docs/EZ-UPSTREAM-MERGE.md for merge strategy and annotation meanings."
