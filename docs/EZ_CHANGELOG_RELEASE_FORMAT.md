# Ez2Lazer Release / 游戏内更新日志格式

游戏内更新日志的**数据源为 [GitHub Wiki](https://github.com/SK-la/Ez2Lazer/wiki)** 上的清单文件 `changelog-releases.json`（与 Wiki 正文同一套 `raw.githubusercontent.com/wiki/...` 拉取方式），不按发版 CI 生成快照，也不依赖 GitHub Releases REST API。

Release 网页上的 Description 仍可作为对外说明；**以 Wiki 清单为准**时，发版后请在 Wiki 更新 `changelog-releases.json`，游戏下次打开更新日志即可拉取新内容。

正文按 **Markdown 原样排版** 显示（标题、列表、emoji、插图等），不做 `add` / `fix` / `misc` 类型解析。

## Wiki 清单（维护者）

- 在 Wiki 仓库新建/编辑 **`changelog-releases.json`**（与 `locale-pages.json` 同级，见 [raw 地址](https://raw.githubusercontent.com/wiki/SK-la/Ez2Lazer/changelog-releases.json)）。
- 结构示例见仓库内 [`changelog-releases.example.json`](./changelog-releases.example.json)。
- 每条 `releases[]` 至少包含：`tag_name`（如 `2026.1122.0-ez2lazer`）、`body`（含 `## zh` / `## en`）、`published_at`（ISO 8601）。
- 发版后补写说明：在 Wiki 改 JSON 保存即可，无需改 CI、无需重传安装包。

## 语言切换（必備）

- `## zh` / `## en`：两套完整正文，小写、单独一行。
- 游戏语言：**zh / zh_hant** → `## zh` 块；**其余** → `## en` 块（overlay 内无单独语言按钮）。
- `## zh` 之前的段落（摘要、上游同步提示等）两种语言都会显示。

## 推荐排版

- 可用 `#` / `##` / `###`、`-` 列表、emoji、`(#123)` 或 `(SK-la/Ez2Lazer#123)`、图片链接等。
- GitHub 上的 `<details>` 仅作用于网页折叠；游戏内不解析 HTML，可把同一内容直接写在 `## zh` / `## en` 下。

## 旧标记

- `<!-- ez-changelog-format: 1 -->` 若仍存在，游戏会忽略该行，不再走结构化条目列表。可删可留。

## 网络与缓存

- 打开更新日志时优先拉 Wiki raw 清单；成功则写入本地 `EzData/github-changelog-releases.json`。
- 拉取失败且本地有缓存时继续展示缓存，并提示 manifest 不可用。
- 可选：`EZ2LAZER_GITHUB_TOKEN` / `GITHUB_TOKEN` 与游戏内 Wiki 相同，用于 raw 请求头（公开 Wiki 一般不必配置）。

维护流程与 Agent 约定见 `.cursor/skills/ez-release-notes/SKILL.md`。