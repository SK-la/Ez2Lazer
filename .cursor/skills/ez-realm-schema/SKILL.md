---
name: ez-realm-schema
description: >-
  Ez2Lazer Realm 与 SQLite schema 约定。改 schema、迁移、EZ_REALM_SCHEMA_VERSION、
  ScoreInfo、BeatmapInfo、client.realm 或成绩/Tag 存储时先读。
---

# Realm 与 SQLite

- `schema_version` 与 ppy 上游对齐，只在合并上游时递增。
- Ez 字段变更时递增 `EZ_REALM_SCHEMA_VERSION`。磁盘文件头版本 = `schema_version * 1000 + EZ`。
- 可以扩展 `ScoreInfo` / `BeatmapInfo`。
- 大表可以留在独立 sqlite。
- `DELETE` 必须带 `WHERE`。清空全表时写明 `WHERE TRUE`。
- 开发期 schema 对不齐不算产品 bug，临时处理不要提交。模型变更需要持久化时，才在合并后递增 Ez 版本。
