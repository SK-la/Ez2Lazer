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

    if ($t -match "songselect|选歌|preview|分支库|sqlite|xxy|kps|radar|pp|branch") { return "选歌界面" }
    if ($t -match "setting|设置|config|option|路径|cache|account|登录") { return "游戏设置" }
    if ($t -match "skin|ezpro|sbi|o2|theme|sprite|贴图|皮肤") { return "Skin" }
    if ($t -match "mod|random|double space|lp|slice|seed|转谱|键型") { return "Mod" }
    if ($t -match "hud|graph|judgement graph|score graph|雷达|结算|统计") { return "HUD/结算" }
    if ($t -match "editor|编辑器|timeline|note") { return "编辑器" }
    if ($t -match "judge|judgement|hitmode|health|判定|血量|ex score|poor") { return "判定与血量" }
    if ($t -match "audio|keysound|bass|mixer|wasapi|asio|音频") { return "音频" }
    if ($t -match "fix|crash|nre|stability|memory|leak|优化|修复|性能") { return "稳定性与性能" }

    return "其他"
}

function Get-CommitItems {
    param([string]$Range)

    $raw = git log --reverse --pretty=format:"%h`t%s" $Range
    if (-not $raw) { return @() }

    $items = @()
    foreach ($line in $raw) {
        $parts = $line -split "`t", 2
        if ($parts.Count -lt 2) { continue }

        $hash = $parts[0].Trim()
        $subject = $parts[1].Trim()
        $category = Get-CategoryFromText -Text $subject

        $items += [PSCustomObject]@{
            Hash = $hash
            Subject = $subject
            Category = $category
        }
    }

    return $items
}

function Add-CategoryBlocks {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [object[]]$Commits,
        [string[]]$Categories,
        [string]$TitlePrefix = ""
    )

    foreach ($cat in $Categories) {
        $group = $Commits | Where-Object { $_.Category -eq $cat }
        if (@($group).Count -eq 0) { continue }

        $heading = if ([string]::IsNullOrWhiteSpace($TitlePrefix)) { "### $cat" } else { "### $TitlePrefix$cat" }
        $Lines.Add("")
        $Lines.Add($heading)
        foreach ($c in $group) {
            $Lines.Add(("- {0} (`{1}`)" -f $c.Subject, $c.Hash))
        }
    }
}

$range = "$FromRef..$ToRef"
$commits = Get-CommitItems -Range $range
$commitCount = @($commits).Count

if ($commitCount -eq 0) {
    throw "No commits found in range: $range"
}

if ([string]::IsNullOrWhiteSpace($Title)) {
    $Title = "Release $ToRef"
}

$orderedCategories = @(
    "选歌界面",
    "游戏设置",
    "Skin",
    "Mod",
    "HUD/结算",
    "编辑器",
    "判定与血量",
    "音频",
    "稳定性与性能",
    "其他"
)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# $Title")
$lines.Add("")
$lines.Add("> 自动草稿：基于提交范围 ``$range`` 生成，请在发布前人工精修。")

if ($LanguageMode -eq "zh") {
    $lines.Add("")
    $lines.Add("## 变更摘要")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories
}
else {
    $lines.Add("")
    $lines.Add("语言切换 / Language")
    $lines.Add("- [中文](#中文) | [English](#english)")
    $lines.Add("")
    $lines.Add("## 中文")
    $lines.Add("")
    $lines.Add("<details open>")
    $lines.Add("<summary>展开/收起中文内容</summary>")
    $lines.Add("")
    $lines.Add("### 变更摘要")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories
    $lines.Add("")
    $lines.Add("</details>")
    $lines.Add("")
    $lines.Add("## English")
    $lines.Add("")
    $lines.Add("<details>")
    $lines.Add("<summary>Expand/Collapse English section</summary>")
    $lines.Add("")
    $lines.Add("> Draft from the same commit range. Rewrite and translate before publishing.")
    $lines.Add("")
    $lines.Add("### Change Summary")
    Add-CategoryBlocks -Lines $lines -Commits $commits -Categories $orderedCategories -TitlePrefix "[EN] "
    $lines.Add("")
    $lines.Add("</details>")
}

$lines.Add("")
$lines.Add("## 发布检查清单 / Pre-release Checklist")
$lines.Add("- [ ] 术语统一（Song Select / Skin / HUD / Judgement）")
$lines.Add("- [ ] 删除重复或语义相近条目")
$lines.Add("- [ ] 标记实验功能与已知限制")
$lines.Add("- [ ] 对 English 部分做人工润色翻译")
$lines.Add("- [ ] 更新 Wiki 对应板块页")

$content = [string]::Join([Environment]::NewLine, $lines)
Set-Content -Path $Output -Value $content -Encoding UTF8

Write-Host "Release notes draft generated: $Output"
