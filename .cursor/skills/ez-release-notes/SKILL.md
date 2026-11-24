---
name: ez-release-notes

description: >
  撰写 Ez2Lazer 发行说明、维护游戏内更新日志清单，以及可选的 GitHub Release 网页说明。
  游戏内数据源为 Wiki 的 changelog-releases.json，不经发版 CI，不依赖 Releases REST API。
  在代写发布说明、起草 changelog、发版后补说明、迁移旧 Release 正文到 Wiki 时使用本 skill。
---

# Ez2Lazer 发布说明（发行版 / 游戏内）

## 真源（必读）

| 渠道 | 作用 | 维护位置 |
|------|------|----------|
| **Wiki `changelog-releases.json`** | **游戏内**更新日志列表与正文 | [Ez2Lazer Wiki](https://github.com/SK-la/Ez2Lazer/wiki) |
| GitHub Release **Description** | 下载页 / 网页读者（可选） | Releases 页面 |
| `docs/EZ_CHANGELOG_RELEASE_FORMAT.md` | 格式契约 | 主仓库 |
| Wiki [发布说明规范（中文）](https://github.com/SK-la/Ez2Lazer/wiki/%E5%8F%91%E5%B8%83%E8%AF%B4%E6%98%8E%E8%A7%84%E8%8C%83-%E4%B8%AD%E6%96%87) | 排版与用语习惯 | Wiki |

**约定**：合并 Wiki manifest 客户端后，玩家在「更新日志」里看到的是 **Wiki JSON 里的 `body`**。Release 正文不会自动同步进游戏；未改 Wiki 则游戏内不更新。

Raw 地址（与游戏拉取一致）：

`https://raw.githubusercontent.com/wiki/SK-la/Ez2Lazer/changelog-releases.json`

## 禁止

- 不要在 `auto-release` 或其它 CI 里生成/上传 `changelog-releases.json`。
- 不要把「只改 GitHub Release Description」当作游戏内已更新。
- 不要用 `api.github.com/.../releases` 作为游戏内数据源说明（旧客户端除外）。

## Wiki 清单结构

根对象含 `releases` 数组。每条至少：

- `tag_name`：完整 tag，如 `2026.1122.0-ez2lazer`
- `body`：Markdown，**必须**含 `## zh` 与 `## en`（小写、单独一行）
- `published_at`：ISO 8601 UTC，如 `2026-11-22T12:00:00Z`
- 建议：`prerelease`（bool）、`html_url`（该 tag 的 Release 链接）

示例见主仓 `docs/changelog-releases.example.json`。

JSON 内 `body` 用 `\n` 换行；注意引号转义。保存前可用 jq 或编辑器校验 JSON。

列表顺序：按 `published_at` **新→旧** 排列即可（客户端会再排序）；**新发版**在数组中**新增一条**（或插入后依赖排序）。

## 正文格式（`body`）

与历史 Release 说明一致：

1. 可选：在 `## zh` 之前写共用摘要（中英文界面都会显示）。
2. `## zh`：完整中文正文。
3. `## en`：完整英文正文。
4. 标题、列表、emoji、Issue/PR 链接、图片 URL 均可；游戏内不解析 HTML `<details>`。
5. `<!-- ez-changelog-format: 1 -->` 若存在会被忽略，可删可留。

## 推荐发版流程

1. **发版**：照常打 tag / CI 出安装包（无需动 changelog CI）。
2. **写说明**（顺序可灵活，但游戏内以 Wiki 为准）：
   - 在 Wiki 编辑 `changelog-releases.json`，为新 tag 写入 `body`（可先写 Release 草稿再粘贴到 JSON）。
   - 可选：把同一份 Markdown 贴到 GitHub Release Description，方便仅逛网页的用户。
3. **核对**：打开 raw URL 确认 JSON 合法；新版本玩家打开游戏「更新日志」应出现该 tag。
4. **首次启用 Wiki 源**：若 manifest 为空，需把近几版 `body` 从历史 Release **一次性迁入** JSON。

## Agent 代写时的输出

- 同时给出 **可直接贴进 JSON 的 `body` 字符串**（或 pretty 的 zh/en 块让用户自行转义）。
- 给出建议的 `tag_name`、`published_at`、`html_url` 字段值。
- 提醒维护者在 Wiki 保存 `changelog-releases.json`，**不要**只输出 Release 网页用 Markdown。
- 不修改主仓代码里硬编码的 Wiki URL（除非用户明确要求改客户端）。

## 旧客户端

未含 Wiki manifest 逻辑的发行版仍读 Releases API + 本地缓存。新流程合入后，长期只需维护 Wiki；可为重要版本在 Release 留一份副本作过渡。