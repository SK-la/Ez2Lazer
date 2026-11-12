# Ez2Lazer Release / 游戏内更新日志格式

游戏从 `SK-la/Ez2Lazer` GitHub Release 的 **Description（body）** 读取更新日志，按 **Markdown 原样排版** 显示（标题、列表、emoji、插图等自行书写），不做 `add` / `fix` / `misc` 类型解析。

## 语言切换（必備）

- `## zh` / `## en`：两套完整正文，小写、单独一行。
- 游戏语言：**zh / zh_hant** → `## zh` 块；**其余** → `## en` 块（overlay 内无单独语言按钮）。
- `## zh` 之前的段落（摘要、上游同步提示等）两种语言都会显示。

## 推荐排版（与历史 GitHub 发布说明一致）

- 可用 `#` / `##` / `###`、`-` 列表、emoji、`(#123)` 或 `(SK-la/Ez2Lazer#123)`、图片链接等，与网页 Release 一致即可。
- GitHub 上的 `<details>` 仅作用于网页折叠；游戏内不解析 HTML，可把同一内容直接写在 `## zh` / `## en` 下。

## 旧标记

- `<!-- ez-changelog-format: 1 -->` 若仍存在，游戏会忽略该行，不再走结构化条目列表。可删可留。

迁移说明见 [EZ_CHANGELOG_MIGRATION.md](./EZ_CHANGELOG_MIGRATION.md)。

## 网络与 GitHub 限流

- 在线刷新时只拉 **最新 2 页 × 每页 10 条**（与更新检查的 `per_page=10` 一致），按 `tag_name` **合并进本地缓存**，不每次请求 `per_page=100` 整表。
- 完整列表与旧版正文依赖缓存 `EzData/github-changelog-releases.json`；缓存约 6 小时内新鲜时不再打 API。
- API 失败时使用**已有缓存**（即便过期）。
- 可选：`EZ2LAZER_GITHUB_TOKEN` / `GITHUB_TOKEN`（`public_repo` 读权限）提高 GitHub API 配额。
