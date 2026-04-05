# Ez 皮肤系统说明（统一EzSkinSetting、皮肤工厂、消费端用法）

## 文件用途（放在仓库中的作用）

- 本文件用于团队快速理解 EzPro 皮肤系统，统一准则：为皮肤相关交互（列配色、note 大小、预览）提供可复用的原则、禁止项与实现思路，并列出主要的代码归属位置，便于开发与排查时参考。

## 原则（高层指导）

- 性能优先：优先保证游戏中的性能，其次是保证所有设置状态是运行时动态更新，即时游戏时钟暂停也应该保证能更新。
- 任何使用Ez皮肤系统的功能，都应该保持相似的消费用法。
- 立即可见：视觉状态（selected/hover/glow/shadow、位移等）在数据改变时必须立即重算并应用，保证交互反馈一致。
- 上游优先：关于 note 尺寸与列布局的问题，先排查上游设置与广播链路，再定位到单个 drawable 的问题。
- 禁止在绑定路线上调用 `SkinInfo.TriggerChange()` 或等效强制全局重载，否则会造成高频重载的恶劣问题，只推荐按钮一次性触发时可以用来强制刷新。
- 禁止让预览容器或临时视图写回共享配置/绑定（例如直接把 preview 的 note size/colour 写回 `Ez2ConfigManager` 的 bindable）。
- 禁止在热路径大量创建绑定副本或频繁执行 BindTo/Unbind 操作以避免内存分配与 GC 压力。

## 基本情况（常见现象与原因归类）

- 颜色/尺寸不同步：可能是预览容器（EzSkinEditor系列）对共享 bindable 的污染、或 pooled drawable 未被完整重置。
- 性能下降：在热路径创建绑定副本、频繁 BindTo/Unbind 或大量临时分配会导致帧率与 GC 问题。
  - 注意 new 构建时，load()加载时，LoadComplete()加载完成时，负担是不同的。
  - 事件绑定、纹理刷新优先加载时。
  - 与刷新有关的绑定回调，优先在加载完成时。
- 编辑器重进失效：预览树或依赖未在 OnEntering/OnResuming 等生命周期中重建，导致后续改动看似不生效。
- EzPro皮肤中，Middle这个LN面身是需要二次重载计算的，完整刷新至少需要2帧（立即刷新+下一帧刷新）。

## 实现思路（开发指引）

- 颜色变更：
  - 拖动：直接更新本地 selector/button 显示。
  - 确认/保存：强制动作仅用于检查绑定状态是否失效，用户操作时非必要的不使用的。

- 外观与状态重算：在按钮或 selector 的 `UpdateColor`/`SetColorMapping` 入口中，立即重算并应用当前 selected/hover 状态的 glow、shadow、位移等视觉参数。

- 预览与隔离：静态或预览容器必须使用只读或本地映射的依赖，禁止直接 Set/Cache 回共享运行时 bindable；若需对照 live 状态，使用拷贝或映射视图。

- Pooled drawable 管理：复用时显式重置颜色、尺寸、alpha、Transform、Bindable 订阅状态等，确保复用对象不带残留状态。

## 文件分工与层级（谁负责什么）

- `EzColumnTab.cs`（EzOsuGame/Screens）
  - 负责列级配置 UI（键位选择、基本颜色映射、面板级设置）。
  - 负责将配置变化映射到本地 selector/preview 显示，并提供显式的“保存/应用”按钮做一次性持久化。

- `EzSkinColorButton.cs`（EzOsuGame/Screens）
  - 负责单个颜色选择按钮的视觉与交互：实现 `UpdateColor()`，在其中立即重算 selected/hover 等状态外观（glow/shadow/offset）。

- `EzSkinEditorScreen.cs`（EzOsuGame/Edit）
  - 负责 skin 编辑器屏幕级生命周期管理，确保在 `OnEntering()` / `OnResuming()` 重建设置面板与预览树。
  - 分区管理，这套界面在未来完善后，移动到Ez设置分区，由独立按钮直接载入，绕开皮肤编辑器。
  - TODO: 未来改为keymode选择器切换不同key，然后提供per-key的完整编辑设置，并提供回写到skin.ini的手动按钮。
  - 这套界面原则上动态刷新，但是要手动保存。这是与皮肤编辑器不同的。

- `EzSkinLNEditorProvider_Static.cs`（EzMania/Editor）
  - 负责静态 LN 预览容器；必须隔离依赖并避免写回共享 bindable，提供 `PreviewDependencyContainer` 供 preview-only 使用。

- `EzNoteBase.cs` / `EzHoldNote*`（Skinning）
  - 负责 note 的颜色/尺寸应用逻辑，订阅列级广播或直接订阅对应 bindable（但不要触发全局重载）。

- `EzLocalTextureFactory.cs` / `Ez2ConfigManager.cs` / `Column.cs` / `ColumnFlow.cs`
  - 负责上游配置、纹理缓存、列尺寸广播与实际列宽管理；这些是排查 note 尺寸/纹理问题的首要位置。

## 层级与交互图（简述）

- 用户操作（拖动/选择/保存） → `EzColumnTab` UI → 本地 selector 映射 / preview-only 显示
- 保存点击 → `EzColumnTab`（一次性持久化）→ 可选：触发非频繁的全局刷新（仅由用户显式触发）
- Preview 渲染 → `EzSkinLNEditorProvider_Static` 提供隔离依赖 → `EzNoteBase` / `DrawableHitObject` 从列/工厂读取映射（只读或映射视图）
- 上游变化（列宽/比例/纹理名） → `Ez2ConfigManager` / `EzLocalTextureFactory` 广播 → `Column` / note drawable 响应（此链路需要优先检查）

## 详细文件参考（便于排查）

- `osu.Game/EzOsuGame/Screens/EzColumnTab.cs` — UI、颜色映射、保存入口。
- `osu.Game/EzOsuGame/Screens/EzSkinColorButton.cs` — 颜色按钮视觉与 selected/hover 重算。
- `osu.Game/EzOsuGame/Edit/EzSkinEditorScreen.cs` — 编辑器生命周期与设置/预览重建。
- `osu.Game.Rulesets.Mania/EzMania/Editor/EzSkinLNEditorProvider_Static.cs` — 静态预览容器与 `PreviewDependencyContainer` 实现。
- `osu.Game.Rulesets.Mania/Skinning/EzStylePro/EzNoteBase.cs` — note 基类的颜色/尺寸应用与列 watcher。
- `osu.Game.EzOsuGame/EzLocalTextureFactory.cs` — 纹理/尺寸缓存与刷新广播入口。
- `osu.Game.EzOsuGame/Configuration/Ez2ConfigManager.cs` — Ez 专用配置 bindable 与列级设置入口。
- `osu.Game.Rulesets.Mania/UI/Column.cs` & `ColumnFlow.cs` — 列宽、列级广播与布局变更处。

---

备注：本文件为团队实现与排查的准则文档，建议在代码评审或改动涉及上述文件时一并参考。本次操作中仓库记忆同步未就绪，因此请以此文件为团队共享的实际说明。
