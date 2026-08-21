# Ez2Lazer — 上游 tag 合并与漂移治理

合并官方 tag 后「上游改了但 Ez 没吃进」，通常来自：大量就地改上游文件 + 冲突时保 Ez，缺少事后核对。

## 默认策略（解冲突时）

| 情况 | 做法 |
|------|------|
| 上游改行为/数据源，Ez 只是叠加 UI | **拿上游逻辑**，再把 Ez UI/字段接回去 |
| 上游删除了 Ez 仍依赖的 API | **保留 Ez 侧适配**，在本表注明 |
| 纯 Ez 字段/控件（如 `EzKeyModeSelector`、`SortModeDropdown` xxySR） | **保持 Ez** |
| 解冲突后出现「声明未用 / 双路径并存」 | 当漏合信号，清到只剩一条真源 |

新功能优先放 `osu.Game/EzOsuGame/**` 或 `*.Ez.cs` partial，避免继续扩大上游文件 patch 面。

## 合并步骤

1. `git fetch upstream --tags`
2. `git merge <tag>`（例如 `2026.819.0-tachyon`）
3. 按上表解冲突；Realm 遵守双版本号（`schema_version` 跟上游，`EZ_REALM_SCHEMA_VERSION` 仅 Ez 字段）
4. **强制审计**：`pwsh ./scripts/AuditUpstreamHotspots.ps1 -OldTag <上一合并tag> -NewTag <本次tag>`
5. 对报告中可疑项：补吃上游或标 `intentional-Ez`
6. `dotnet build osu.Desktop`（**不要**擅自 `dotnet run` / 打开用户 `client.realm`）
7. 领域 checklist：Realm sidecar 跨版本、Select 过滤/排序冒烟、Mania 见 [REPLAY_JUDGE_MERGE-Mania.md](REPLAY_JUDGE_MERGE-Mania.md)、BMS 见 wiki

Push / PR 只走 `SK-la` fork，禁止推 `ppy` / `upstream`。

## 审计怎么读

对每个热点文件，脚本给出：

1. **UpstreamDelta**：`OldTag..NewTag` 上游改了什么
2. **EzDelta**：`NewTag..HEAD` 合并后相对上游还偏什么

人工标注：

- `ok` — 上游意图已在 HEAD 中，EzDelta 仅为有意 patch
- `missed` — UpstreamDelta 有改动，HEAD 仍像旧上游 / 双真源并存
- `intentional-Ez` — 与上游不同是故意的（写清意图）

## P0 热点（几乎每次合并）

| 路径 | 意图 | 合并策略 | 冒烟点 |
|------|------|----------|--------|
| `osu.Game/Screens/Select/FilterControl.cs` | Ez 过滤行 + xxySR 排序；集合过滤跟上游 config GUID | 上游行为真源 + 保留 Ez UI；勿留未赋值字段 | 集合过滤重启仍记住；排序含 xxySR/PP |
| `osu.Game/Screens/Select/SongSelect.cs` | Ez 选歌挂钩 | take-upstream 再接 Ez | 进选歌、切谱 |
| `osu.Game/Screens/Select/BeatmapCarousel.cs` | Ez 分析过滤等 | take-upstream 再接 Ez | 过滤/分组 |
| `osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs` | Ez 分组 | 同上 | 分组模式 |
| `osu.Game/Screens/Select/FilterCriteria.cs` | Ez 条件 + 上游 Collection MD5 | 上游字段优先 | 集合过滤结果 |
| `osu.Game/Screens/Select/PanelBeatmap.cs` / `PanelBeatmapStandalone.cs` | Ez 面板展示 | 保留 Ez，跟上游布局 | 星级/标签显示 |
| `osu.Game/Database/RealmAccess.cs` | 双版本号、sidecar、Ez migration | schema 跟上游；Ez case 保留；sidecar 须跨上游回退 | 启动不空库；`client_{N}000+EZ` |
| `osu.Game/OsuGame.cs` / `OsuGameBase.cs` | Ez 注入、DI | take-upstream 再接 Ez | 启动 |
| `osu.Game/Screens/Play/Player.cs` | 诊断/角逐挂钩 | take-upstream 再接 Ez | 进图 |
| `osu.Game/Beatmaps/BeatmapInfo.cs` / `osu.Game/Scoring/ScoreInfo.cs` | Ez 持久化字段 | 跟 schema；禁止写进官方提交 JSON | 成绩/谱面字段 |
| `osu.Game.Rulesets.Osu/OsuRuleset.cs` | Ez 统计图 + 上游结算布局 | 上游布局 + 保留 Ez StatisticItem | 结算面板 |
| `osu.Game.Rulesets.Taiko/TaikoRuleset.cs` | 同上 | 同上 | 结算面板 |
| `osu.Game.Rulesets.Mania/ManiaRuleset.cs` | Ez 统计 + 上游布局 | 同上 | 结算面板 |
| `osu.Game.Rulesets.Mania/ManiaSettingsSubsection.cs` | Ez 速度绑定；Header 已被上游 sealed | 勿 override sealed Header | 设置页 |
| `osu.Game.Rulesets.Catch/CatchRuleset.cs` | 少改 | 默认 take-upstream | — |
| `osu.Game/Users/Drawables/DrawableAvatar.cs` | Ez 自定义头像 + 上游 OnlineAsset 缓存 | 两边都要 | 头像显示 |
| `osu.Game/Overlays/SettingsOverlay.cs` | `EzGameSection` 插入 load() sections | 跟上游结构，插入 Ez 段 | 设置侧栏 |
| `osu.Game/Rulesets/Edit/ScrollingHitObjectComposer.cs` | 偶发冲突 | 合并 using / 行为 | 编辑器 |
| `osu.Game.Rulesets.BMS/BMSRuleset.cs` | Ez ruleset；跟上游 API 改名 | 适配 `GameplayVariants` 等 | BMS 可玩 |

## P1 热点

| 路径 | 意图 | 合并策略 | 冒烟点 |
|------|------|----------|--------|
| `osu.Game/Overlays/SkinEditor/*` | Ez 菜单 partial | 优先 `*.Ez.cs` | 皮肤编辑 |
| `osu.Game/Overlays/FirstRunSetup/ScreenBehaviour.cs` | 与 Settings 段列表同步 | 跟 SettingsOverlay | 首次引导 |
| Mania 判定/计分链路 | 见 REPLAY_JUDGE_MERGE-Mania | 按该文档 | parity 测试 |
| `osu.Game/Configuration/OsuConfigManager.cs` | 上游新 Setting + Ez | 保留双方 setting | 配置读写 |

## 2026.819 清查记录

审计命令：

```powershell
pwsh ./scripts/AuditUpstreamHotspots.ps1 -OldTag 2026.807.0-tachyon -NewTag 2026.819.0-tachyon -SummaryOnly
```

| 文件 | 标注 | 处理 |
|------|------|------|
| `FilterControl.cs` | missed → ok | 删除未使用的 `collectionDropdown` 字段；保留 `CollectionDropdown` UI + `configCollectionFilter`；保留 Ez `SortModeDropdown` |
| `FilterCriteria.cs` | ok | 上游 `CollectionBeatmapMD5Hashes` 属性已在；Ez 另有 Pp/HasVideo 等 |
| `OsuConfigManager.cs` | intentional-Ez | 上游 `SongSelectCollectionFilter` 已在；若干默认值与上游不同为有意 |
| `SongSelect.cs` | ok | `checkBeatmapValidForSelection` / `unscopeBeatmapSet` 已吃进 |
| `BeatmapCarousel.cs` | ok | `refreshedNewBeatmap` 路径已吃进 |
| `BeatmapCarouselFilterGrouping.cs` / `PanelBeatmap*.cs` | ok | `GameplayVariants` 重命名已吃进 |
| `RealmAccess.cs` | intentional-Ez | schema 52 + Ez 双版本/sidecar；另见 sidecar 跨上游回退修复 |
| `OsuGame.cs` | ok | `nowPlayingOverlay` 互斥已吃进 |
| `OsuGameBase.cs` | ok | `OnlineAssetCachingStore` 已缓存 |
| `DrawableAvatar.cs` | intentional-Ez | 上游 onlineTextures + Ez 自定义头像 |
| `SettingsOverlay.cs` | intentional-Ez | 上游 load() sections + `EzGameSection` |
| `ScreenBehaviour.cs` | intentional-Ez | 去掉上游 `RulesetSection` 后插入 `EzGameSection` |
| `*Ruleset.cs` (Osu/Taiko/Mania/Catch) | intentional-Ez / ok | 上游结算布局已接；Ez 额外 StatisticItem 保留 |
| `ManiaSettingsSubsection.cs` | ok | sealed Header 已去掉 |
| `ScrollingHitObjectComposer.cs` | ok | `TAction` 泛型已吃进 |
| `Player.cs` / `BeatmapInfo` / `ScoreInfo` / `BMSRuleset` | intentional-Ez | 本区间无上游 delta（ez-only） |

本轮代码修补：仅 `FilterControl` 死字段。其余 P0 抽查为 ok / intentional-Ez，未发现第二处 confirmed missed-upstream。
