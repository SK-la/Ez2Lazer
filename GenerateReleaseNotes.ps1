param(
    [Parameter(Mandatory = $true)]
    [string]$FromRef,

    [Parameter(Mandatory = $true)]
    [string]$ToRef,

    [string]$Output = "release-notes-draft.md",
    [string]$Title = "",
    [ValidateSet("zh", "zh-en")]
    [string]$LanguageMode = "zh-en"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-CategoryFromText {
    param([string]$Text)

    $t = $Text.ToLowerInvariant()

    # 界面（含选歌、结算、统计）
    if ($t -match "选歌|songselect|preview|分支库|sqlite|xxy|kps|radar|pp|branch|结算|result|统计|statistics") { return "界面" }
    
    # 游戏设置（避免使用“设置”关键词，防止误匹配）
    if ($t -match "config|option|路径|path|cache|account|登录|login|description|描述|本地化|localization") { return "游戏设置" }
    
    # HUD
    if ($t -match "hud|graph|judgement graph|score graph|雷达|fastslow") { return "HUD" }
    
    # Skin
    if ($t -match "skin|ezpro|sbi|o2|theme|sprite|贴图|皮肤|列宽|column.*width|note.*比例|ratio") { return "Skin" }
    
    # Mod
    if ($t -match "mod|random|double space|lp|slice|seed|转谱|键型|krrln|dp refiner|一卡通|od|判定过滤") { return "Mod" }
    
    # 机制
    if ($t -match "judge|judgement|hitmode|health|判定|血量|ex score|poor|perspective|shader|playfield") { return "机制" }
    
    # 音频
    if ($t -match "audio|keysound|bass|mixer|wasapi|asio|音频") { return "音频" }
    
    # Bug 修复
    if ($t -match "fix|修复|屏蔽|双面板|保存|报错|显示问题|crash|nre|stability|memory|leak") { return "Bug 修复" }

    return "其他"
}

function Get-CommitItems {
    param([string]$Range, [string]$BaseTag)

    $raw = git log --reverse --pretty=format:"%h`t%s`n%an" $Range
    if (-not $raw) { return @() }

    $items = @()
    $frameworkSync = $null
    $i = 0
    while ($i -lt $raw.Count) {
        $line = $raw[$i]
        $parts = $line -split "`t", 2
        if ($parts.Count -lt 2) { 
            $i++
            continue 
        }

        $hash = $parts[0].Trim()
        $subject = $parts[1].Trim()
        $author = if (($i + 1) -lt $raw.Count) { $raw[$i + 1].Trim() } else { "Unknown" }
        
        # Skip official commits except framework sync
        if ($subject -match "^Merge tag|^Update framework|^合并标签") {
            $frameworkSync = $subject
            $i += 2
            continue
        }
        
        $isMergePR = $subject -match "合并拉取请求\s*#(\d+)"
        $prNumber = $null
        if ($isMergePR) {
            $prNumber = $matches[1]
        }
        
        if ($isMergePR) {
            $category = "PR 合并"
        } else {
            $category = Get-CategoryFromText -Text $subject
        }

        $items += [PSCustomObject]@{
            Hash = $hash
            Subject = $subject
            Category = $category
            Author = $author
            IsMergePR = $isMergePR
            PRNumber = $prNumber
        }
        
        $i += 2
    }

    # Add framework sync as a special item at the end
    if ($frameworkSync) {
        $items += [PSCustomObject]@{
            Hash = ""
            Subject = $frameworkSync
            Category = "框架同步"
            Author = ""
            IsMergePR = $false
            PRNumber = $null
        }
    }

    return $items
}

function Add-CategoryBlocks {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [object[]]$Commits,
        [string[]]$Categories,
        [string]$TitlePrefix = "",
        [bool]$IncludeHash = $false,
        [string]$BaseTag = ""
    )

    foreach ($cat in $Categories) {
        $group = $Commits | Where-Object { 
            $_.Category -eq $cat -and 
            -not $_.IsMergePR
        }
        
        if (@($group).Count -eq 0) { continue }

        $heading = if ([string]::IsNullOrWhiteSpace($TitlePrefix)) { "### $cat" } else { "### $TitlePrefix$cat" }
        $Lines.Add("")
        $Lines.Add($heading)
        foreach ($c in $group) {
            if ($IncludeHash) {
                $Lines.Add(("- {0} (`{1}`)" -f $c.Subject, $c.Hash))
            } else {
                $Lines.Add(("- {0}" -f $c.Subject))
            }
        }
    }
}

function Add-PRMergesBlock {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [object[]]$Commits,
        [string]$TitlePrefix = "",
        [string]$BaseTag = ""
    )

    $prMerges = $Commits | Where-Object { $_.IsMergePR } | Sort-Object { [int]$_.PRNumber }
    
    if (@($prMerges).Count -eq 0) { return }

    $heading = if ([string]::IsNullOrWhiteSpace($TitlePrefix)) { "### PR 合并" } else { "### PR Merges" }
    $Lines.Add("")
    $Lines.Add($heading)
    
    foreach ($pr in $prMerges) {
        $prAuthor = Get-PRAuthor -PRNumber $pr.PRNumber -BaseTag $BaseTag
        $authorTag = if ($prAuthor) { " @$prAuthor" } else { "" }
        
        $prDesc = $pr.Subject -replace "合并拉取请求\s*#\d+\s*:?\s*", ""
        
        if ([string]::IsNullOrWhiteSpace($TitlePrefix)) {
            $Lines.Add(("- **合并 #{0}{1}** ：{2}" -f $pr.PRNumber, $authorTag, $prDesc))
        } else {
            $Lines.Add(("- **Merge #{0}{1}**: {2}" -f $pr.PRNumber, $authorTag, $prDesc))
        }
    }
}

function Get-PRAuthor {
    param([string]$PRNumber, [string]$BaseTag)
    
    # Find the merge commit for this PR
    $mergeCommit = git log --oneline --grep="合并拉取请求 #$PRNumber" "$BaseTag..HEAD" | Select-Object -First 1
    if (-not $mergeCommit) { return $null }
    
    $mergeHash = $mergeCommit.Split(' ')[0]
    
    # Get the author name from commits in this PR (excluding the merge commit itself)
    # Format: "Author Name <email@example.com>"
    $authorInfo = git log "$mergeHash~5..$mergeHash" --format="%aN <%aE>" --no-merges | Select-Object -First 1
    
    if (-not $authorInfo) { return $null }
    
    # Extract username from "Username <email>" format
    if ($authorInfo -match "^(.+?)\s*<") {
        return $matches[1].Trim()
    }
    
    return $null
}

$range = "$FromRef..$ToRef"

# Validate that the refs exist
try {
    $fromExists = git rev-parse --verify $FromRef 2>$null
    if (-not $fromExists) {
        throw "Reference '$FromRef' does not exist. Please check if it's a valid commit hash, branch, or tag."
    }
    
    $toExists = git rev-parse --verify $ToRef 2>$null
    if (-not $toExists) {
        throw "Reference '$ToRef' does not exist. Please check if it's a valid commit hash, branch, or tag."
    }
}
catch {
    Write-Host "Error validating references:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Available tags matching '2026':" -ForegroundColor Yellow
    git tag | Select-String "2026" | Select-Object -First 10
    throw "Invalid Git references provided"
}

$commits = Get-CommitItems -Range $range -BaseTag $FromRef
$commitCount = @($commits).Count

if ($commitCount -eq 0) {
    Write-Host "Warning: No commits found in range: $range" -ForegroundColor Yellow
    Write-Host "From: $FromRef ($(git log -1 --format='%h %s' $FromRef))" -ForegroundColor Cyan
    Write-Host "To:   $ToRef ($(git log -1 --format='%h %s' $ToRef))" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This could mean:" -ForegroundColor Yellow
    Write-Host "  1. Both refs point to the same commit" -ForegroundColor Yellow
    Write-Host "  2. The FromRef is a descendant of ToRef" -ForegroundColor Yellow
    Write-Host "  3. The refs are on different branches without common history" -ForegroundColor Yellow
    throw "No commits found in range: $range"
}

if ([string]::IsNullOrWhiteSpace($Title)) {
    $Title = "Release $ToRef"
}

$orderedCategories = @(
    "界面",
    "游戏设置",
    "HUD",
    "Skin",
    "Mod",
    "机制",
    "Bug 修复",
    "其他"
)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# $Title")
$lines.Add("")

if ($LanguageMode -eq "zh") {
    $lines.Add("## 变更摘要")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories -IncludeHash $false -BaseTag $FromRef
    Add-PRMergesBlock -Lines $lines -Commits $commits -BaseTag $FromRef
}
else {
    $lines.Add("语言切换 / Language")
    $lines.Add("- [中文](#中文) | [English](#english)")
    $lines.Add("")
    $lines.Add("## 中文")
    $lines.Add("")
    $lines.Add("<details open>")
    $lines.Add("<summary>展开/收起中文内容</summary>")
    $lines.Add("")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories -IncludeHash $false -BaseTag $FromRef
    Add-PRMergesBlock -Lines $lines -Commits $commits -BaseTag $FromRef
    $lines.Add("")
    $lines.Add("</details>")
    $lines.Add("")
    $lines.Add("## English")
    $lines.Add("")
    $lines.Add("<details>")
    $lines.Add("<summary>Expand/Collapse English section</summary>")
    $lines.Add("")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories -IncludeHash $false -BaseTag $FromRef -TitlePrefix ""
    Add-PRMergesBlock -Lines $lines -Commits $commits -BaseTag $FromRef -TitlePrefix ""
    $lines.Add("")
    $lines.Add("### Others")
    $lines.Add("")
    $lines.Add("</details>")
}

$lines.Add("")

$content = [string]::Join([Environment]::NewLine, $lines)
Set-Content -Path $Output -Value $content -Encoding UTF8

Write-Host "Release notes draft generated: $Output"
