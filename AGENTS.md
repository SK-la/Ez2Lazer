# Ez2Lazer 协作约定

## 范围

- 本仓库是 ppy/osu 的 Ez2Lazer fork。优先改 Ez 代码。
- 搜索限定在当前仓库、项目工作区、osu 数据库文件夹或用户指定目录。

## 改动

- 先对齐再做改动。跨文件重构、语义变更、schema 或迁移，先确认方案再改。
- 一次只处理一个关切。
- 需要修改 Framework 时先征询意见，用户同意后再继续。
- 改 Drawable 或 UI 前先读 `.cursor/rules/ui-layout.mdc`。那里是 osu!framework 布局约束，违反会在布局阶段崩溃。

## Git

- 禁止向 `ppy/*` 或 `upstream` 推送、开官方 PR、提交到官方分支、删除其 tag。
- 合并上游只用 `fetch` 之后的本地 merge 或 rebase。
- 只有用户明确要求创建 PR 时才推送。先签出 PR 分支，推送一次，再执行 `gh pr create --repo SK-la/<仓库名>`。
- PR 已存在时，后续提交留在本地，不要自动推送。
- 大改动按模块分成多次提交。
- 提交 C# 前运行 `Format-AgentCommit.ps1`。它只补上缺少的 ppy MIT 文件头、删除 IDE0005 确认的未使用 using、把连续空行收成一行、补上缺少的文件末尾换行。不要另外跑 `dotnet format`。

## C#

- 命名以仓库根目录 `.editorconfig` 为准。
- 注释只写非显然的契约、边界和副作用。XML 注释只用于 public API 和通用工具类。不给 private 成员写 XML，注释不作修订记录。
- 用户未明确要求时，不在加载或启动路径改写已保存的配置。可以改新安装的默认值。
- 优先自动属性。

## Realm 与 SQLite

- 改了 schema 或迁移之后，不要 `dotnet run`，不要打开用户库。
- 版本号、`ScoreInfo` / `BeatmapInfo`、sqlite 和 `DELETE` 的细则在 `.cursor/skills/ez-realm-schema/SKILL.md`。改这些之前先读它。

## 验证与回复

- 大改动后至少 `dotnet build` 相关项目。小改动不必为了验证而 build。
- 用户可见的 bug 修复，在回复里说明验证场景。
- 回复和 PLAN 只写必要结论。不贴长文件、长日志、长 diff、代码路径。必要时给摘要。
- 用户是在提问时，只回答，不修改代码。
- 用户追问刚刚那次提交时，用这次对话里已经有的内容回答，不要再读文件或查 git。

## Cursor Cloud

- .NET SDK 10 在 `/usr/local/share/dotnet`。直接跑 `osu.Desktop` 的 apphost 时依赖 `DOTNET_ROOT`；环境安装会把它链到 `/usr/share/dotnet`。
- 不要构建 `osu.sln`。解决方案引用了仓库外的 `ez2lazer-framework` 和 `ez2lazer-resources`。默认用 NuGet：构建 `osu.Desktop/osu.Desktop.csproj`，测试用 `osu.Game.Tests`。
- `fonts-noto-color-emoji` 会让 `EzFontSettingsOverlay` 在启动时因 FreeType `invalid pixel size` 退出。环境安装会卸掉这个包。
- 冒烟测试：`dotnet test osu.Game.Tests/osu.Game.Tests.csproj --filter FullyQualifiedName~FilterQueryParserTest`。
