# EzAnalysis 重构总设计

## 版本基线

- 当前重构直接以 v6 作为新起点，不考虑向后兼容旧的整套持久化结构。
- 旧版分支库使用 `xxy_sr_branch`，当前统一收敛到 `songs_branch`。
- 通用分析字段统一使用 `kps_avg`、`kps_max`、`kps_list_json` 作为主命名。

## 设计结论

这份文档是对旧版“EzAnalysis 存储重构设计”和当前重构 plan 的统一裁决版本。后续实现应直接以这里为准，不再在“旧主表方案”“属性组化方案”“吞吐优化补丁”之间来回切换。

最终方向如下：

1. `EzAnalysisResult` 只做读模型，不再承担持久化写入模型职责。
2. SQLite 采用“自建主体主表 + mania 扩展表 + 轻量标签属性组”的结构，不再让每个 slice 兼职主体，也不直接复用官方 `BeatmapInfo` 作为 EzAnalysis 的持久化主体。
3. 启动 warmup 补核心 analysis，并一并补齐轻量 tag group，但不在启动阶段批量补 `beatmap data`。
4. 运行时选中谱面继续按需补齐遗漏项；若运行中开启 sqlite 开关，应主动触发一次预热扫描，而不是强制重启游戏。
5. warmup/回填写库保留后台批写器，但它只能是统一回填入口的落库执行器，不能再演变成第二套悬空业务链。
6. 标签相关数据只允许走轻量文本解析，不允许回退到 storyboard 完整解码。

## 主体方案选择

综合结论：借官方的“主体 + 扩展字段”架构思路，但 EzAnalysis 自己单独实现持久化主体模型更好。

推荐理由：

1. 官方 `BeatmapInfo` 的价值在于提供了主体与派生字段分层的参考，不在于它本身适合作为 EzAnalysis 的 SQLite 实体。
2. `BeatmapInfo` 属于上游核心数据模型，直接拿来当 EzAnalysis 主体会把分析持久化层和上游数据模型强耦合，后续上游字段变更会无意义地波及 EzAnalysis schema。
3. EzAnalysis 需要的是“稳定、最小、可控”的主体快照，只承载身份字段、ruleset 和本分析链路自己的更新时间戳，而不是把上游主体模型整套映射进来。
4. EzAnalysis 的缺失判定、切片更新时间、后台批写和标签属性组，都属于分析域自身语义，用自建主体更容易保证边界清晰。
5. 如果直接借官方主体 info，短期看像是省代码，长期更容易出现 realm/sqlite 语义混用、责任边界漂移、上游改动连带重构的问题。

因此推荐做法是：

1. 借官方思路，不借官方实体。
2. 自建 `EzAnalysisEntry` 作为 EzAnalysis 的 SQLite 主体快照。
3. 主体字段只保留 EzAnalysis 真正需要的最小身份集和切片状态。

## 现状问题

旧设计真正的问题不在“拆了几张表”，而在职责和生命周期混在一起：

1. `EzAnalysisResult` 同时承担读模型、待写入模型、持久化聚合模型三个职责。
2. `common`、`mania`、`beatmap data` 在旧方案里都重复携带 `beatmap_id/hash/md5/version` 这一整套主体身份信息。
3. 预热、运行时补算、SQLite 写入没有统一入口，导致一旦为了吞吐加速，就容易长出第二条难以收口的写入链。
4. 标签类轻量字段直接并入核心版本语义，导致未来只要再加几个展示字段，就会污染 `ANALYSIS_VERSION`。
5. 启动阶段如果同时做核心分析和标签/资源补算，很容易把真实热点从计算链扩散到 I/O 和对象构建，最终出现“速度没明显提升但卡顿更重”的情况。

## 目标

1. 保持接近官方 `BackgroundDataStoreProcessor` 的职责边界，但不机械照抄完全串行行为。
2. 让分析计算、标签解析、持久化写库各自边界清晰。
3. 保留核心分析吞吐，优先避免启动期 GC 爆炸和持续卡顿。
4. 让未来新增轻量 UI 属性时，不需要再靠整体版本升级触发整库重算。
5. 在结构上统一 panel、tag、store、warmup 的数据流，减少重复分支。

## 非目标

1. 不为旧结构做兼容迁移设计。
2. 不为了最小改动保留所有历史 API 形态。
3. 不把官方主链本身当默认根因方向，优先收敛 Ez fork 自己的架构问题。
4. 不扩散到 `StarDifficulty`、`GetWorkingBeatmap` 上游行为改造。

## 模型分层

### 1. 读模型

`EzAnalysisResult`

- 只返回给 cache、panel、UI。
- 聚合 `KpsSummary`、`EzManiaSummary`，以及一个单独的轻量标签属性组。
- 不再包含任何“待写回 SQLite”的中间状态。

推荐新增：

- `EzBeatmapTagSummary` 或 `EzAnalysisTagSummary`
- 当前至少包含：
  - `HasVideo`
  - `HasStoryboard`

用途：

- 专门承载轻量展示属性。
- 以后同类字段继续往这个组里扩，不再扩散成更多顶层 bool。

### 2. 持久化主体模型

`EzAnalysisEntry`

- 只存在于持久化层与缓存层。
- 表示“当前这张谱面的 SQLite 主体快照”。
- 不直接映射或复用官方 `BeatmapInfo`，只借它“主体挂派生字段”的结构思路。
- 负责统一承载：
  - `beatmap_id`
  - `beatmap_hash`
  - `beatmap_md5`
  - `ruleset_online_id`
  - 通用分析切片的更新时间与数据
  - 轻量标签属性组的更新时间与数据投影

这样做的原因：

1. EzAnalysis 主体只需要最小快照，不需要承担官方主体的全部语义。
2. SQLite 主体结构要围绕分析切片组织，而不是围绕上游通用领域对象组织。
3. 后续无论 tag group、mania 扩展还是 warmup 缺失判定演进，都不会反向污染上游主体模型。

### 3. 规则集扩展模型

`EzManiaSummary`

- 作为 ruleset-specific 扩展单独存储。
- 不再重复保存主体 hash/md5/version。
- 通过 `beatmap_id` 与主体表关联。

## 存储结构

### 主体主表 `ez_analysis_entry`

建议保留主体主表，并只让它承载通用主体字段与高频直接消费字段：

- `beatmap_id`
- `beatmap_hash`
- `beatmap_md5`
- `ruleset_online_id`
- `common_updated_at`
- `kps_avg`
- `kps_max`
- `kps_list_json`
- `tag_updated_at`
- `tag_payload_json` 或等价的独立 tag 列组

设计含义：

1. `common` 属于主表的核心通用切片。
2. 标签属性组属于主表的轻量附加切片，但不再绑定到核心分析版本语义。
3. `*_updated_at == 0` 表示对应切片尚未补齐。

关于 tag 存储形式：

1. 推荐方案：`tag_payload_json` + `tag_updated_at`。
2. 可接受方案：独立 `has_video`、`has_storyboard` 列组，但仍必须视为单独 tag group，而不是继续混入核心版本语义。

### mania 扩展表 `ez_analysis_mania`

- `beatmap_id`
- `updated_at`
- `xxy_sr`
- `column_counts_json`
- `hold_note_counts_json`

设计含义：

1. 只保存 mania 专属 payload。
2. 身份校验依赖主表，不再在扩展表重复保存主体身份字段。
3. `mania` 的缺失判定和更新时钟应与 `common`、`tag` 分开。

## 生命周期设计

### 读取

1. `common`：直接从主表读取。
2. `tag group`：直接从主表读取并反序列化。
3. `mania`：通过主表和 mania 扩展表聚合读取。
4. `EzAnalysisResult`：只在数据库门面层聚合，不进入 accessor 层作为主语。

### 写入

1. `common`：upsert 主表中的 common 列组。
2. `tag group`：upsert 主表中的 tag group 列组或 payload。
3. `mania`：先确保主表主体存在，再写 mania 扩展表。

### 回填唯一入口

`BackfillStoredData()` 作为唯一回填入口，负责：

1. 为单张谱面只创建一个 `WorkingBeatmap`。
2. `common` 和 `mania` 同时缺失时，在同一个 working beatmap 上一次算完。
3. 需要取消时，把取消注册到 `CancelAsyncLoad()`。
4. `skipExistingComparison=true` 的路径统一走后台批写。

## 预热与运行时策略

### 启动 warmup

启动 warmup 的最终结论是：

1. 处理核心 analysis backfill，并同时补齐轻量 tag group。
2. 不在启动阶段批量补 `beatmap data`，也不允许回退到 storyboard 完整对象解码。
3. 队列只保留 beatmap id 和回填计划，不缓存全量 detached beatmap 集合。
4. worker 使用有限并发，目标是接近旧版 `单总队列 + 有限 worker` 的吞吐，而不是完全串行。

原因：

1. 启动阶段把重资源解析一起做，会显著扩大真实热点范围；但轻量 tag 文本解析仍可接受。
2. 完全串行虽然更像官方表面行为，但在 EzAnalysis 这条链上已经验证会导致吞吐明显下降。
3. 全量 detached beatmap cache 已验证会带来启动期 GC 爆炸和卡顿。

### 运行中开启 sqlite

1. 当 sqlite 开关从关闭切到开启时，应立即启动后台 worker 并主动触发一次预热扫描。
2. 该扫描应复用现有 warmup/backfill 主链，不额外分叉另一套临时补算逻辑。
3. 行为目标是“开启即可开始补库”，避免为了触发预热强制重启游戏。

### 运行时选中谱面补算

运行时 selected beatmap worker 负责：

1. 检查当前谱面缺失的 `common`、`mania`、`tag group`。
2. 兜底补齐当前谱面仍然缺失的轻量标签组或其他遗漏切片。
3. 继续复用统一的 `BackfillStoredData()`，不再单独长出另一套补算 API。

### 与官方机制的关系

对齐原则不是“所有行为都照抄官方”，而是：

1. 参考 `BackgroundDataStoreProcessor` 的职责拆分方式。
2. 参考 `RealmArchiveModelImporter` 的保守并发思路，而不是无上限并发。
3. 保留 gameplay 期间降低后台处理影响的节流逻辑。
4. 在 EzAnalysis 自己的热路径上，以实际吞吐和卡顿表现优先，而不是机械追求完全串行。

## 持久化写入策略

旧文档里“不再保留独立后台写入队列”的结论，需要修正。

新的裁决是：

1. 保留后台批写器。
2. 但后台批写器只能是统一回填入口的落库执行器，不能形成第二条独立业务链。
3. 批写粒度按 beatmap 合并 `common`、`mania`、`tag group` payload。
4. 落库使用单连接、单事务批处理，避免 warmup 期间逐项同步 SQLite 写入。

这样做的原因：

1. 实测里逐项同步写入不利于吞吐。
2. 只要回填入口统一，后台批写并不会天然造成架构悬空。
3. 真正的问题不是“有后台写队列”，而是“计算入口和写入链路分裂”。

## 标签属性组与 beatmap data 策略

标签组是这次重构里必须单独收敛的一层。

### 标签组职责

1. 承载 `HasVideo`、`HasStoryboard` 这类 UI 轻量属性。
2. 为 panel/tag 组件提供统一输入对象。
3. 为未来同类字段提供扩展容器。

### 解析策略

只允许轻量文本解析：

1. 先扫描 `UnhandledEventLines`。
2. 若不足以得出结果，再按官方主 `.osb` 文件名规则扫描 storyboard 文本。
3. 不允许走 `workingBeatmap.Storyboard` 的完整对象解码。

### UI 分发策略

1. `PanelBeatmap` 与 `PanelBeatmapStandalone` 不再分别塞 `ExternalHasVideo`、`ExternalHasStoryboard`。
2. 两者统一向 tag 组件分发单一 `TagSummary` 对象。
3. 若外部 tag group 缺失，`EzDisplay.Tag` 才允许走内部回退解析。

## 代码落点

### 核心分析与存储

- `osu.Game/EzOsuGame/Analysis/EzAnalysisResult.cs`
- `osu.Game/EzOsuGame/Analysis/EzAnalysisModels.cs`
- `osu.Game/EzOsuGame/Analysis/EzAnalysisDatabase.cs`
- `osu.Game/EzOsuGame/Analysis/EzAnalysisPersistentStore.cs`
- `osu.Game/EzOsuGame/Analysis/AnalysisDataAccessor.cs`
- `osu.Game/EzOsuGame/Analysis/EzManiaAnalysisDataAccessor.cs`

### 预热与运行时补算

- `osu.Game/EzOsuGame/Analysis/EzAnalysisWarmupProcessor.cs`
- `osu.Game/Database/BackgroundDataStoreProcessor.cs`

### 标签组与 UI 分发

- `osu.Game/EzOsuGame/Analysis/EzBeatmapDataParser.cs`
- `osu.Game/Screens/Select/PanelBeatmap.cs`
- `osu.Game/Screens/Select/PanelBeatmapStandalone.cs`
- `osu.Game/EzOsuGame/UserInterface/EzDisplay.Tag.cs`

### schema 与版本语义

- `osu.Game/EzOsuGame/Analysis/DatabaseSchemaManager.cs`

## 实施顺序

### Phase 1：重建核心回填主链

1. 重建 `EzAnalysisPersistentStore` 的后台批写器。
2. 重建 `EzAnalysisDatabase.BackfillStoredData()` 单入口。
3. 确保 `common + mania` 的计算复用同一个 `WorkingBeatmap`。

### Phase 2：重建 warmup 和运行时补算

1. 启动阶段做 analysis + 轻量 tag group backfill。
2. 运行时 selected beatmap on-demand backfill。
3. 运行中开启 sqlite 开关时，主动触发一次预热扫描。
4. 保留取消、通知、节流逻辑。

### Phase 3：收敛标签属性组

1. 引入单独的 `TagSummary` 读模型。
2. 把 panel/tag 的分发统一成“单对象输入”。
3. 把标签持久化从核心版本语义中拆出来。

### Phase 4：schema 与缺失判定收口

1. 统一建表、补列、重建表的列定义来源。
2. 让 `common`、`mania`、`tag group` 各自拥有明确的缺失判定入口。
3. 避免再通过整体 `ANALYSIS_VERSION` 触发轻量标签组重算。

## 明确不要再走的方向

1. 启动阶段为所有谱面批量补 `beatmap data`。
2. 用全量 detached beatmap cache 换吞吐。
3. 为了“更像官方”把 warmup 固定成完全串行。
4. 回到 storyboard 对象解码式的标签提取。
5. 继续把轻量标签字段扩散成更多顶层 bool。
6. 让 accessor、store、UI 各自维护一份独立标签分发逻辑。

## 验证清单

1. 使用干净库启动，确认启动阶段出现 analysis + 轻量 tag group backfill，但不出现 `beatmap data` 或 storyboard 对象解码式预热。
2. 在大量谱面环境下启动游戏，确认不会再出现明显 GC 爆炸和持续卡顿。
3. 在运行中关闭再开启 sqlite，确认无需重启游戏也会主动触发一次预热扫描。
4. 在缺失缓存的谱面上打开选歌，确认 panel 可以先显示核心信息，并由后台兜底补齐仍遗漏的 tag group。
5. 在已有完整 tag group 的谱面上快速滚动选歌，确认不会重新触发 storyboard 回退解析。
6. 在 mania 谱面上确认 `common` 和 `mania` 可通过同一次回填完成，并且写入仍保持正确。
7. 验证 SQLite schema 的建表、补列、重建表三条路径完全一致。

## 最终取舍

这次重构的最终取舍是：

1. 保留“自建主体主表 + mania 扩展表”的主体方向，借官方主体分层思路，但不直接复用官方 `BeatmapInfo` 作为持久化主体。
2. 但标签类轻量字段不再继续污染核心分析版本语义，而是收敛成独立属性组。
3. 保留后台批写，但它必须从属于统一回填主链。
4. 启动 warmup 做核心 analysis + 轻量 tag group，运行时补算只负责兜底遗漏项；运行中开启 sqlite 开关时也要主动触发一次 warmup。

这套结构同时吸收了旧版存储设计和后续吞吐/卡顿排查的结论，后续实现应直接围绕这份文档展开。
