# Ez2Lazer 文档索引

判定 / Session / 性能相关已集中到 [`scoring/`](./scoring/)。其它主题仍在本目录或子目录。

## 判定 · Session · Timeline · 性能

入口：[`scoring/README.md`](./scoring/README.md)

| 文件 | 用途 |
|------|------|
| [`scoring/MANIA-JUDGEMENT-RUNTIME.md`](./scoring/MANIA-JUDGEMENT-RUNTIME.md) | Mania 局内拓扑、M/N 共享边界、删双份 U1–U3（**现行架构唯一入口**） |
| [`scoring/MANIA-SCORE-DATA-SOURCE-REGISTRY.md`](./scoring/MANIA-SCORE-DATA-SOURCE-REGISTRY.md) | 成绩数据从哪读 / 写不写 Realm |
| [`scoring/REPLAY_JUDGE_MERGE-Mania.md`](./scoring/REPLAY_JUDGE_MERGE-Mania.md) | Mania Session 黄金标准与 parity |
| [`scoring/REPLAY_JUDGE_MERGE-Osu.md`](./scoring/REPLAY_JUDGE_MERGE-Osu.md) | Osu Session 状态 |
| [`scoring/REPLAY_JUDGE_SHADOW.md`](./scoring/REPLAY_JUDGE_SHADOW.md) | Shadow 桥归档（非终态） |
| [`scoring/EZ-SR-TL-REGISTRY.md`](./scoring/EZ-SR-TL-REGISTRY.md) | 跨模式 Session / Timeline / Race |
| [`scoring/EZ-PERFORMANCE.md`](./scoring/EZ-PERFORMANCE.md) | FPS / 性能汇总 + 高 KPS 未结 backlog |

**已删除（内容并入上表）**：`MANIA-JUDGEMENT-TOPOLOGY.md` → RUNTIME；`HIGH_KPS_JUDGE_BACKLOG.md` → PERFORMANCE §8。

## Skills

| 文件 | 用途 |
|------|------|
| [`skills/Skills-README.md`](./skills/Skills-README.md) | Skills 管线说明（原 `EzOsuGame/Skills/README.md`） |
| [`skills/DATA-FOLLOWUPS.md`](./skills/DATA-FOLLOWUPS.md) | 数据跟进 |
| [`skills/418K-Alignment-PLAN.md`](./skills/418K-Alignment-PLAN.md) | 418K 对齐计划 |

## 其它

| 文件 | 用途 |
|------|------|
| [`EZ_ANALYSIS_STORAGE_REDESIGN.md`](./EZ_ANALYSIS_STORAGE_REDESIGN.md) | 分析存储 / 启动 GC |
| [`EZ-UPSTREAM-MERGE.md`](./EZ-UPSTREAM-MERGE.md) | 上游合并纪律 |
| [`EzSkinSystemNotes.md`](./EzSkinSystemNotes.md) | 皮肤系统 |
| [`ScriptedSkin-Security.md`](./ScriptedSkin-Security.md) | ScriptedSkin 安全 |
| [`桌宠使用说明.md`](./桌宠使用说明.md) / [`Pets/`](./Pets/) | 桌宠 |

仓库根 `README.md` / `CONTRIBUTING.md` 仍留在根目录（上游约定）。
