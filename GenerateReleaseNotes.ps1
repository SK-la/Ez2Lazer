param(
    [string]$FromRef = "",
    [string]$ToRef = "",

    [string]$Output = "release-notes-draft.md",
    [string]$Title = "",
    [ValidateSet("zh", "zh-en")]
    [string]$LanguageMode = "zh-en",
    [ValidateSet("none", "ai")]
    [string]$TranslationProvider = "ai",
    [string]$TranslationApiBase = "",
    [string]$TranslationApiKey = "",
    [string]$TranslationModel = "",
    [switch]$ForceOverwriteOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:TranslationCache = @{}

function Resolve-MainBranchRef {
    $candidates = @(
        @{ Name = "origin/main"; Ref = "refs/remotes/origin/main" },
        @{ Name = "main"; Ref = "refs/heads/main" },
        @{ Name = "origin/master"; Ref = "refs/remotes/origin/master" },
        @{ Name = "master"; Ref = "refs/heads/master" }
    )

    foreach ($c in $candidates) {
        git show-ref --verify --quiet $c.Ref
        if ($LASTEXITCODE -eq 0) {
            return $c.Name
        }
    }

    throw "Unable to determine main branch. Tried: origin/main, main, origin/master, master."
}

function Resolve-LatestTwoTagsFromMain {
    param([string]$MainRef)

    $tags = git for-each-ref --merged $MainRef --sort=-creatordate --format="%(refname:short)" refs/tags
    if (-not $tags -or @($tags).Count -lt 2) {
        throw "Need at least two tags merged into '$MainRef' to auto-select FromRef/ToRef."
    }

    return @($tags[1], $tags[0])
}

function Get-CategoryFromText {
    param([string]$Text)

    $t = $Text.ToLowerInvariant()

    # realm 数据管理
    if ($t -match "realm|sqlite|xxy|pp|tag|lastappliedxxydifficultyversion|回填|迁移") { return "realm数据管理" }

    # 谱面预览
    if ($t -match "preview|预览|staticfullmap|staticscroll|全谱|卷轴") { return "谱面预览" }

    # 界面（含选歌、结算、统计）
    if ($t -match "选歌|songselect|分支库|kps|radar|branch|结算|result|统计|statistics") { return "界面" }
    
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
        
        $isMergePR = $subject -match "(?:合并拉取请求|merge pull request)\s*#(\d+)"
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
            MergeDetails = $null
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
        [hashtable]$CategoryMap,
        [hashtable]$ExcludedHashes = @{},
        [bool]$IsEnglish = $false,
        [string]$TitlePrefix = "",
        [bool]$IncludeHash = $false,
        [string]$BaseTag = ""
    )

    foreach ($cat in $Categories) {
        $group = $Commits | Where-Object { 
            $_.Category -eq $cat -and 
            -not $_.IsMergePR -and
            -not $ExcludedHashes.ContainsKey($_.Hash)
        }
        
        if (@($group).Count -eq 0) { continue }

        $displayCat = if ($CategoryMap.ContainsKey($cat)) { $CategoryMap[$cat] } else { $cat }
        $heading = if ([string]::IsNullOrWhiteSpace($TitlePrefix)) { "### $displayCat" } else { "### $TitlePrefix$displayCat" }
        $Lines.Add("")
        $Lines.Add($heading)
        foreach ($c in $group) {
            $subjectText = if ($IsEnglish) { Convert-ToEnglishText -Text $c.Subject } else { $c.Subject }
            if ($IncludeHash) {
                $Lines.Add(("- {0} (`{1}`)" -f $subjectText, $c.Hash))
            } else {
                $Lines.Add(("- {0}" -f $subjectText))
            }
        }
    }
}

function Get-PRProcessCommitHashSet {
    param([object[]]$Commits)

    $set = @{}
    $prMerges = $Commits | Where-Object { $_.IsMergePR }
    foreach ($pr in $prMerges) {
        $details = Get-PRMergeDetails -PRCommitHash $pr.Hash
        foreach ($d in $details) {
            if (-not [string]::IsNullOrWhiteSpace($d.Hash)) {
                $set[$d.Hash] = $true
            }
        }
    }

    return $set
}

function Add-PRMergesBlock {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [object[]]$Commits,
        [bool]$IsEnglish = $false,
        [string]$TitlePrefix = "",
        [string]$BaseTag = ""
    )

    $prMerges = $Commits | Where-Object { $_.IsMergePR } | Sort-Object { [int]$_.PRNumber }
    
    if (@($prMerges).Count -eq 0) { return }

    $heading = if ($IsEnglish) { "### PR Merges" } else { "### PR 合并" }
    $Lines.Add("")
    $Lines.Add($heading)
    
    foreach ($pr in $prMerges) {
        $prAuthor = Get-PRAuthor -PRCommitHash $pr.Hash
        $authorTag = if ($prAuthor) { " @$prAuthor" } else { "" }
        
        $prDesc = $pr.Subject -replace "(?i)(?:合并拉取请求|merge pull request)\s*#\d+\s*:?\s*", ""
        $prDescText = if ($IsEnglish) { Convert-ToEnglishText -Text $prDesc } else { $prDesc }
        
        if ($IsEnglish) {
            $Lines.Add(("#### Merge #{0}{1}: {2}" -f $pr.PRNumber, $authorTag, $prDescText))
        } else {
            $Lines.Add(("#### 合并 #{0}{1}：{2}" -f $pr.PRNumber, $authorTag, $prDescText))
        }

        $details = Get-PRMergeDetails -PRCommitHash $pr.Hash
        if (@($details).Count -eq 0) {
            if ($IsEnglish) {
                $Lines.Add("- No branch commits found in this merge range.")
            } else {
                $Lines.Add("- 未在该合并范围内找到分支过程提交。")
            }
            $Lines.Add("")
            continue
        }

        if ($IsEnglish) {
            $Lines.Add("- Branch process commits:")
        } else {
            $Lines.Add("- 合并分支过程提交：")
        }

        foreach ($d in $details) {
            $detailText = if ($IsEnglish) { Convert-ToEnglishText -Text $d.Subject } else { $d.Subject }
            $Lines.Add(("  - {0} (`{1}`)" -f $detailText, $d.Hash))
        }
        $Lines.Add("")
    }
}

function Get-PRMergeDetails {
    param([string]$PRCommitHash)

    $parents = git rev-list --parents -n 1 $PRCommitHash
    if (-not $parents) { return @() }

    $parts = $parents.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -lt 3) { return @() }

    $mainParent = $parts[1]
    $branchParent = $parts[2]

    $lines = git rev-list --reverse --no-merges --pretty=format:"%h`t%s" "$mainParent..$branchParent"
    if (-not $lines) { return @() }

    $details = @()
    foreach ($line in $lines) {
        if (-not $line -or $line -eq "commit") { continue }
        if ($line -match "^commit\s+") { continue }

        $parsed = $line -split "`t", 2
        if ($parsed.Count -lt 2) { continue }

        $details += [PSCustomObject]@{
            Hash = $parsed[0].Trim()
            Subject = $parsed[1].Trim()
        }
    }

    return $details
}

function Get-PRAuthor {
    param([string]$PRCommitHash)
    
    $authorInfo = git log -1 --format="%aN" $PRCommitHash
    
    if (-not $authorInfo) { return $null }
    return (Normalize-AuthorName -Name $authorInfo.Trim())
}

function Normalize-AuthorName {
    param([string]$Name)

    if ([string]::IsNullOrWhiteSpace($Name)) { return $Name }

    $authorMap = @{
        "LA" = "SK-la"
    }

    if ($authorMap.ContainsKey($Name)) {
        return $authorMap[$Name]
    }

    return $Name
}

function Initialize-TranslationConfig {
    if ($TranslationProvider -eq "none") { return }

    if ([string]::IsNullOrWhiteSpace($TranslationApiBase)) {
        $TranslationApiBase = if ($env:OPENAI_BASE_URL) { $env:OPENAI_BASE_URL } elseif ($env:OPENAI_API_BASE) { $env:OPENAI_API_BASE } else { "https://api.openai.com/v1" }
    }
    if ([string]::IsNullOrWhiteSpace($TranslationApiKey)) {
        $TranslationApiKey = if ($env:OPENAI_API_KEY) { $env:OPENAI_API_KEY } elseif ($env:AZURE_OPENAI_API_KEY) { $env:AZURE_OPENAI_API_KEY } else { "" }
    }
    if ([string]::IsNullOrWhiteSpace($TranslationModel)) {
        $TranslationModel = if ($env:OPENAI_MODEL) { $env:OPENAI_MODEL } else { "gpt-4o-mini" }
    }

    if ([string]::IsNullOrWhiteSpace($TranslationApiKey)) {
        throw "Translation API key is missing. Set -TranslationApiKey or OPENAI_API_KEY."
    }
}

function Resolve-OutputPath {
    param(
        [string]$RequestedPath,
        [bool]$AllowOverwrite
    )

    if ($AllowOverwrite -or -not (Test-Path -LiteralPath $RequestedPath)) {
        return $RequestedPath
    }

    $directory = Split-Path -Path $RequestedPath -Parent
    if ([string]::IsNullOrWhiteSpace($directory)) {
        $directory = "."
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($RequestedPath)
    $extension = [System.IO.Path]::GetExtension($RequestedPath)
    if ([string]::IsNullOrWhiteSpace($extension)) {
        $extension = ".md"
    }

    $candidate = Join-Path $directory ("{0}.generated{1}" -f $baseName, $extension)
    $index = 1
    while (Test-Path -LiteralPath $candidate) {
        $candidate = Join-Path $directory ("{0}.generated.{1}{2}" -f $baseName, $index, $extension)
        $index++
    }

    Write-Host "Output exists, writing to: $candidate (use -ForceOverwriteOutput to overwrite)." -ForegroundColor Yellow
    return $candidate
}

function Invoke-AITranslation {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }
    if ($script:TranslationCache.ContainsKey($Text)) { return $script:TranslationCache[$Text] }

    $endpoint = ("{0}/chat/completions" -f $TranslationApiBase.TrimEnd('/'))
    $payload = @{
        model = $TranslationModel
        temperature = 0.1
        messages = @(
            @{
                role = "system"
                content = "Translate Chinese software release-note bullet text into concise, natural English. Keep product names, commit hashes, PR numbers, and branch names unchanged unless translating human language words around them. Output translation only."
            },
            @{
                role = "user"
                content = $Text
            }
        )
    } | ConvertTo-Json -Depth 8

    $response = Invoke-RestMethod -Method Post -Uri $endpoint -Headers @{
        "Authorization" = "Bearer $TranslationApiKey"
    } -ContentType "application/json" -Body $payload

    $translated = $response.choices[0].message.content
    if ([string]::IsNullOrWhiteSpace($translated)) {
        throw "Translation API returned empty content for: $Text"
    }

    $translated = $translated.Trim()
    $script:TranslationCache[$Text] = $translated
    return $translated
}

function Convert-ToEnglishText {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }
    if ($TranslationProvider -eq "none") { return $Text }
    return (Invoke-AITranslation -Text $Text)
}

if ([string]::IsNullOrWhiteSpace($FromRef) -or [string]::IsNullOrWhiteSpace($ToRef)) {
    $mainRef = Resolve-MainBranchRef
    $resolvedTags = Resolve-LatestTwoTagsFromMain -MainRef $mainRef
    $FromRef = $resolvedTags[0]
    $ToRef = $resolvedTags[1]
    Write-Host "Auto-selected tags from ${mainRef}: $FromRef .. $ToRef" -ForegroundColor Cyan
}

if ($LanguageMode -eq "zh-en") {
    Initialize-TranslationConfig
}

$range = "$FromRef..$ToRef"
$resolvedOutputPath = Resolve-OutputPath -RequestedPath $Output -AllowOverwrite $ForceOverwriteOutput.IsPresent

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
$prProcessCommitHashSet = Get-PRProcessCommitHashSet -Commits $commits
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
    "realm数据管理",
    "谱面预览",
    "界面",
    "游戏设置",
    "Skin",
    "Mod",
    "HUD",
    "音频",
    "机制",
    "Bug 修复",
    "其他"
)

$zhCategoryMap = @{
    "realm数据管理" = "realm数据管理"
    "谱面预览" = "谱面预览"
    "界面" = "界面"
    "游戏设置" = "游戏设置"
    "HUD" = "HUD"
    "Skin" = "Skin"
    "Mod" = "Mod"
    "音频" = "音频"
    "机制" = "机制"
    "Bug 修复" = "Bug 修复"
    "其他" = "其他"
}

$enCategoryMap = @{
    "realm数据管理" = "Realm Data Management"
    "谱面预览" = "Beatmap Preview"
    "界面" = "UI / Selection / Stats"
    "游戏设置" = "Game Settings"
    "HUD" = "HUD"
    "Skin" = "Skin"
    "Mod" = "Mods"
    "音频" = "Audio"
    "机制" = "Gameplay Mechanics"
    "Bug 修复" = "Bug Fixes"
    "其他" = "Others"
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# $Title")
$lines.Add("")

if ($LanguageMode -eq "zh") {
    $lines.Add("## 变更摘要")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories -CategoryMap $zhCategoryMap -ExcludedHashes $prProcessCommitHashSet -IncludeHash $false -BaseTag $FromRef
    Add-PRMergesBlock -Lines $lines -Commits $commits -BaseTag $FromRef -IsEnglish $false
}
else {
    $lines.Add("语言切换 / Language")
    $lines.Add("- [中文](#中文) | [English](#english)")
    $lines.Add("")
    $lines.Add("重大更新 / Major Changes")
    $lines.Add("")
    $lines.Add("## 中文")
    $lines.Add("")
    $lines.Add("<details open>")
    $lines.Add("<summary>展开/收起中文内容</summary>")
    $lines.Add("")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories -CategoryMap $zhCategoryMap -ExcludedHashes $prProcessCommitHashSet -IsEnglish $false -IncludeHash $false -BaseTag $FromRef
    Add-PRMergesBlock -Lines $lines -Commits $commits -BaseTag $FromRef -IsEnglish $false
    $lines.Add("")
    $lines.Add("</details>")
    $lines.Add("")
    $lines.Add("## English")
    $lines.Add("")
    $lines.Add("<details>")
    $lines.Add("<summary>Expand/Collapse English section</summary>")
    $lines.Add("")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories -CategoryMap $enCategoryMap -ExcludedHashes $prProcessCommitHashSet -IsEnglish $true -IncludeHash $false -BaseTag $FromRef -TitlePrefix ""
    Add-PRMergesBlock -Lines $lines -Commits $commits -BaseTag $FromRef -TitlePrefix "" -IsEnglish $true
    $lines.Add("")
    $lines.Add("</details>")
}

$lines.Add("")

$content = [string]::Join([Environment]::NewLine, $lines)
Set-Content -Path $resolvedOutputPath -Value $content -Encoding UTF8

Write-Host "Release notes draft generated: $resolvedOutputPath"
