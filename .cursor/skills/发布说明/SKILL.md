---
name: ez2lazer-release-notes

description: >
  【发布说明】撰写 Ez2Lazer 发行说明、维护游戏内更新日志 Wiki 清单 changelog-releases.json，
  以及可选的 GitHub Release 网页说明。游戏内以 Wiki raw 为准，不经发版 CI，不依赖 Releases REST API。
  在代写发布说明、起草 changelog、发版后补说明、迁移旧 Release 正文到 Wiki 时使用。
---

# Ez2Lazer 发布说明（发行版 / 游戏内）

> **账号同步 skill**：本文件位于 Cursor 账号同步目录（云端 `/cursor/stores/user/skills/发布说明/`，本地 `~/.cursor/skills/发布说明/`）。  
> 主仓另有副本 `.cursor/skills/发布说明/SKILL.md`，内容应与本文件一致。

## 真源（必读）

| 渠道 | 作用 | 维护位置 |
|------|------|----------|
| **Wiki `changelog-releases.json`** | **游戏内**更新日志列表与正文 | [Ez2Lazer Wiki](https://github.com/SK-la/Ez2Lazer/wiki) |
| GitHub Release **Description** | 下载页 / 网页读者（可选） | Releases 页面 |
| `docs/EZ_CHANGELOG_RELEASE_FORMAT.md` | 格式契约 | 主仓库 |
| Wiki [发布说明规范（中文）](https://github.com/SK-la/Ez2Lazer/wiki/%E5%8F%91%E5%B8%83%E8%AF%B4%E6%98%8E%E8%A7%84%E8%8C%83-%E4%B8%AD%E6%96%87) | 排版与用语习惯 | Wiki |

**约定**：含 Wiki manifest 的客户端在「更新日志」中展示 **Wiki JSON 的 `body`**。只改 Release 不会更新游戏内；未改 Wiki 则游戏内不变。

Raw（与客户端一致）：

`https://raw.githubusercontent.com/wiki/SK-la/Ez2Lazer/changelog-releases.json`

## 禁止

- 不要在 `auto-release` 或其它 CI 里生成/上传 `changelog-releases.json`。
- 不要把「只改 GitHub Release Description」当作游戏内已更新。
- 不要把 `api.github.com/.../releases` 当作游戏内数据源（旧客户端除外）。
- **不要**写「合并的 PR / Merged PRs」等单独汇总段（见下节）。

## Wiki 清单结构

根对象含 `releases` 数组。每条至少：

- `tag_name`：如 `2026.1122.0-ez2lazer`
- `body`：Markdown，**必须**含 `## zh` 与 `## en`（小写、单独一行）
- `published_at`：ISO 8601 UTC
- 建议：`prerelease`、`html_url`

示例：主仓 `docs/changelog-releases.example.json`。JSON 内 `body` 用 `\n`，注意转义；保存前校验 JSON。

新发版在 `releases` 中**新增一条**；客户端按 `published_at` 排序。

## 正文格式（`body`）

1. 可选：`## zh` 之前的共用摘要（中英界面都显示）。
2. `## zh` / `## en` 各为完整正文。
3. 标题、列表、emoji、图片链接可用；游戏内不解析 `<details>`。
4. `<!-- ez-changelog-format: 1 -->` 可删可留。

## 条目与 PR / 作者（必守）

- **禁止**单独开「合并的 PR」「Merged PRs」「🔀 合并的 PR」等**汇总段**，不要在文末或文内把已在正文中写过的 PR **再列一遍**。
- 每条功能 / 修复在**该条目**上标明 PR 与作者（条目前或条末均可，全篇风格统一即可）。
- **推荐（中文）**：`- 描述文字（#312 @贡献者）`；多 PR：`- 描述（#311 @a、#312 @b）`。
- **推荐（英文）**：`- Description (#312 @contributor)`.
- 也可用前缀：`- **#312** @user：描述`（与上一种勿混用同一段落内两种风格）。
- 无单独 PR 的杂项可不写作者；有 PR 则**只出现一次**，勿正文 + 汇总各写一次。
- Issue 与 PR 若相同编号习惯用 `#n`；链接可用 `(SK-la/Ez2Lazer#n)`。

## 推荐发版流程

1. 打 tag / CI 出包（无需 changelog CI）。
2. **Wiki** 编辑 `changelog-releases.json`，写入新 tag 的 `body`。
3. 可选：同文粘贴 GitHub Release Description。
4. 核对 raw URL；游戏内打开更新日志应见新 tag。
5. **首次上 Wiki 源**：近几版从历史 Release **一次性迁入** JSON。

## Agent 代写时的输出

- 给出可贴入 JSON 的 `body`（或 zh/en 块 + 提醒转义）。
- 每条列表项自带 `#PR @作者`；**不要**生成「合并的 PR」附录。
- 给出 `tag_name`、`published_at`、`html_url`。
- **明确提醒**在 Wiki 保存 manifest，勿只给 Release 网页 Markdown。
- 勿擅自改客户端 Wiki URL（除非用户要求）。

## 旧客户端

未含 Wiki manifest 的版本仍用 Releases API + 本地缓存；新流程合入后长期只维护 Wiki 即可。