# 只处理四件事：补上缺少的 ppy MIT 文件头、IDE0005 确认的未使用 using、连续空行收成一行、补上缺少的文件末尾换行。
# 不跑 dotnet format 的空白、风格或命名修复。
param(
    [string[]]$Path,
    [switch]$TextOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
Set-Location $repoRoot

function Get-ChangedCsFiles {
    $names = New-Object System.Collections.Generic.List[string]
    if ($Path -and $Path.Count -gt 0) {
        foreach ($item in $Path) { $names.Add($item) }
    }
    else {
        $listed = @(git diff --name-only --diff-filter=ACM HEAD) + @(git ls-files --others --exclude-standard)
        foreach ($item in $listed) {
            if (-not [string]::IsNullOrWhiteSpace($item)) { $names.Add($item.Trim()) }
        }
    }

    $files = New-Object System.Collections.Generic.List[string]
    foreach ($name in $names) {
        $leaf = [System.IO.Path]::GetFileName($name)
        if ($name -notlike '*.cs') { continue }
        if ($leaf -like 'g_*.cs') { continue }
        if ([System.IO.Path]::IsPathRooted($name)) { $full = $name }
        else { $full = Join-Path $repoRoot ($name -replace '/', [System.IO.Path]::DirectorySeparatorChar) }
        if (Test-Path -LiteralPath $full) { $files.Add($full) }
    }
    return $files
}

function Test-LineProtected {
    param([string]$text, [int]$lineNumber)
    $inBlock = $false
    $inVerbatim = $false
    $rawQuotes = 0
    $line = 0
    $chars = $text.ToCharArray()
    $count = $chars.Length
    $i = 0
    while ($i -lt $count -and $line -le $lineNumber) {
        $c = $chars[$i]
        if ($c -eq "`r") { $i++; continue }
        if ($c -eq "`n") {
            if ($line -eq $lineNumber -and ($inBlock -or $inVerbatim -or $rawQuotes -gt 0)) { return $true }
            $line++
            $i++
            continue
        }
        if ($line -ne $lineNumber -and -not $inBlock -and -not $inVerbatim -and $rawQuotes -eq 0) {
            if ($c -eq '/' -and ($i + 1) -lt $count -and $chars[$i + 1] -eq '/') {
                while ($i -lt $count -and $chars[$i] -ne "`n") { $i++ }
                continue
            }
            if ($c -eq '/' -and ($i + 1) -lt $count -and $chars[$i + 1] -eq '*') { $inBlock = $true; $i += 2; continue }
            if ($c -eq '"') {
                $i++
                while ($i -lt $count -and $chars[$i] -ne "`n") {
                    if ($chars[$i] -eq '\') { $i += 2; continue }
                    if ($chars[$i] -eq '"') { $i++; break }
                    $i++
                }
                continue
            }
            if ($c -eq '@' -and ($i + 1) -lt $count -and $chars[$i + 1] -eq '"') { $inVerbatim = $true; $i += 2; continue }
            if ($c -eq '$' -and ($i + 2) -lt $count -and $chars[$i + 1] -eq '@' -and $chars[$i + 2] -eq '"') { $inVerbatim = $true; $i += 3; continue }
            if ($c -eq '@' -and ($i + 2) -lt $count -and $chars[$i + 1] -eq '$' -and $chars[$i + 2] -eq '"') { $inVerbatim = $true; $i += 3; continue }
            $start = $i
            if ($c -eq '$') { $start = $i + 1 }
            if ($start -lt $count -and $chars[$start] -eq '"') {
                $run = 0
                while (($start + $run) -lt $count -and $chars[$start + $run] -eq '"') { $run++ }
                if ($run -ge 3) { $rawQuotes = $run; $i = $start + $run; continue }
            }
        }
        elseif ($inBlock) {
            if ($c -eq '*' -and ($i + 1) -lt $count -and $chars[$i + 1] -eq '/') { $inBlock = $false; $i += 2; continue }
        }
        elseif ($rawQuotes -gt 0 -and $c -eq '"') {
            $run = 0
            while (($i + $run) -lt $count -and $chars[$i + $run] -eq '"') { $run++ }
            if ($run -ge $rawQuotes) { $rawQuotes = 0 }
            $i += [Math]::Max($run, 1)
            continue
        }
        elseif ($inVerbatim -and $c -eq '"') {
            if (($i + 1) -lt $count -and $chars[$i + 1] -eq '"') { $i += 2; continue }
            $inVerbatim = $false
        }
        $i++
    }
    return $false
}

$script:MitHeader = "// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.`r`n// See the LICENCE file in the repository root for full licence text."

function Repair-MitHeader([string]$FilePath) {
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $encoding = New-Object System.Text.UTF8Encoding $bom
    $text = $encoding.GetString($bytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
    $newline = if ($text.Contains("`r`n")) { "`r`n" } elseif ($text.Contains("`n")) { "`n" } else { "`r`n" }
    $header = $script:MitHeader -replace "`r`n", $newline
    $trimmed = $text.TrimStart()
    if ($trimmed.StartsWith($header.Split($newline)[0])) { return $false }
    $rebuilt = $header + $newline + $newline + $text.TrimStart("`r", "`n")
    [System.IO.File]::WriteAllText($FilePath, $rebuilt, $encoding)
    return $true
}

function Repair-CsWhitespace([string]$FilePath) {
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $encoding = New-Object System.Text.UTF8Encoding $bom
    $text = $encoding.GetString($bytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
    if ($text.Length -eq 0) { return $false }

    $newline = if ($text.Contains("`r`n")) { "`r`n" } elseif ($text.Contains("`n")) { "`n" } else { "`r`n" }
    $logical = $text.Replace("`r`n", "`n").TrimEnd("`n").Split("`n")
    $kept = New-Object System.Collections.Generic.List[string]
    $previousBlank = $false
    for ($n = 0; $n -lt $logical.Length; $n++) {
        $blank = $logical[$n].Length -eq 0
        if ($blank -and $previousBlank -and -not (Test-LineProtected $text $n)) { continue }
        $kept.Add($logical[$n])
        $previousBlank = $blank
    }
    while ($kept.Count -gt 0 -and $kept[$kept.Count - 1].Length -eq 0) { $kept.RemoveAt($kept.Count - 1) }

    $rebuilt = ($(if ($kept.Count -eq 0) { '' } else { $kept -join $newline })) + $newline
    if ($rebuilt -eq $text) { return $false }
    [System.IO.File]::WriteAllText($FilePath, $rebuilt, $encoding)
    return $true
}

$files = @(Get-ChangedCsFiles)
if ($files.Count -eq 0) { exit 0 }

$changed = New-Object System.Collections.Generic.List[string]
foreach ($file in $files) {
    $headerChanged = Repair-MitHeader $file
    $whitespaceChanged = Repair-CsWhitespace $file
    if ($headerChanged -or $whitespaceChanged) { $changed.Add($file) }
}

if (-not $TextOnly) {
    $formatArgs = @('format', 'analyzers', 'osu.sln', '--diagnostics', 'IDE0005', '--verbosity', 'quiet')
    $root = (Resolve-Path -LiteralPath $repoRoot).Path
    foreach ($file in $files) {
        $full = (Resolve-Path -LiteralPath $file).Path
        if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { continue }
        $relative = $full.Substring($root.Length).TrimStart('\', '/')
        $formatArgs += '--include'
        $formatArgs += ($relative -replace '\\', '/')
    }
    if ($formatArgs -contains '--include') {
        & dotnet @formatArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        foreach ($file in $files) {
            if (Repair-CsWhitespace $file) {
                if (-not $changed.Contains($file)) { $changed.Add($file) }
            }
        }
    }
}

foreach ($file in $changed) { Write-Output $file }
exit 0
