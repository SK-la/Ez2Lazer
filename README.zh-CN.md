<p align="center">
  <img width="640" alt="osu! logo" src="assets/Ez2Lazer-previewr.png">
</p>

<div align="center">
    <a href="https://github.com/SK-la/Ez2Lazer/releases" target="_blank"><img src="https://img.shields.io/badge/Releases-d73a49.svg?logo=github" height="22px" alt="Releases"></a>  
    <a href="https://github.com/SK-la/Ez2Lazer/wiki" target="_blank"><img src="https://img.shields.io/badge/Wiki-0366d6.svg?logo=github" height="22px" alt="Wiki"></a>
    <a href="https://github.com/SK-la/Ez2Lazer" target="_blank"><img src="https://img.shields.io/badge/Game-181717.svg?logo=github" height="22px" alt="Game"></a>
    <a href="https://github.com/SK-la/ez2lazer-framework" target="_blank"><img src="https://img.shields.io/badge/Framework-black.svg?logo=github" height="22px" alt="Framework"></a>
    <a href="https://github.com/SK-la/ez2lazer-resources" target="_blank"><img src="https://img.shields.io/badge/Resources-2ea44f.svg?logo=github" height="22px" alt="Resources"></a>
    <a href="https://space.bilibili.com/4100834" target="_blank"><img src="https://img.shields.io/badge/Bilibili-00A1D6.svg?logo=bilibili&logoColor=white" height="22px" alt="Bilibili"></a>

</div>

<div align="center">

[English](README.md) | [**中文**](README.zh-CN.md)

</div>

# Ez2Lazer

Ez2Lazer 是基于 osu! lazer 的深度改造分支，聚焦 Mania/BMS 生态、高可定制 HUD、判定系统切换和谱面分析工具链。

## 许可

Ez2Lazer 基于 [osu!](https://github.com/ppy/osu)，采用 [MIT 许可](https://opensource.org/licenses/MIT)。  
详见 [LICENCE](LICENCE)。上游版权归 ppy Pty Ltd；Ez2Lazer 的修改由 SK-la 完成。

本许可不涵盖 “osu!” 或 “ppy” 品牌的使用，这些名称受商标法保护。  
游戏资源适用 [ppy/osu-resources](https://github.com/ppy/osu-resources) 中的单独许可。

## 下载与运行

- 最新版本发布页：[SK-la/Ez2Lazer Releases](https://github.com/SK-la/Ez2Lazer/releases)
- 资源包：[EzResources (OneDrive)](https://la1225-my.sharepoint.com/:f:/g/personal/la_la1225_onmicrosoft_com/EiosAbw_1C9ErYCNRD1PQvkBaYvhflOkt8G9ZKHNYuppLg?e=DWY1kn)
- 运行时要求：[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

**自动更新（推荐）**  
- Windows：下载 Release 中的 `ez2lazer-win-Setup.exe` 安装（需已安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)）；之后可在游戏内接收增量更新。  
- 手动安装请用 `Ez2Lazer_release_*.zip` 解压运行；zip 无法使用增量更新，需改用 Setup 安装一次。

> 未安装 EzResources 时，Ez Pro Skin 和 Ez HUD 组件会缺失贴图。

## 文档入口

主文档迁移到 Wiki，README 只保留快速入口。

- Wiki 首页：[Ez2Lazer Wiki](https://github.com/SK-la/Ez2Lazer/wiki)
- 中文总览：[功能总览（中文）](https://github.com/SK-la/Ez2Lazer/wiki/%E5%8A%9F%E8%83%BD%E6%80%BB%E8%A7%88-%E4%B8%AD%E6%96%87)
- 发布说明规范：[发布说明规范](https://github.com/SK-la/Ez2Lazer/wiki/%E5%8F%91%E5%B8%83%E8%AF%B4%E6%98%8E%E8%A7%84%E8%8C%83-%E4%B8%AD%E6%96%87)

### 功能板块
- [选歌界面](https://github.com/SK-la/Ez2Lazer/wiki/%E9%80%89%E6%AD%8C%E7%95%8C%E9%9D%A2-%E4%B8%AD%E6%96%87)
- [游戏设置](https://github.com/SK-la/Ez2Lazer/wiki/%E6%B8%B8%E6%88%8F%E8%AE%BE%E7%BD%AE-%E4%B8%AD%E6%96%87)
- [Skin 系统](https://github.com/SK-la/Ez2Lazer/wiki/Skin-%E7%B3%BB%E7%BB%9F-%E4%B8%AD%E6%96%87)
- [Mod 系统](https://github.com/SK-la/Ez2Lazer/wiki/Mod-%E7%B3%BB%E7%BB%9F-%E4%B8%AD%E6%96%87)
- [HUD 组件](https://github.com/SK-la/Ez2Lazer/wiki/HUD-%E7%BB%84%E4%BB%B6-%E4%B8%AD%E6%96%87)
- [编辑器](https://github.com/SK-la/Ez2Lazer/wiki/%E7%BC%96%E8%BE%91%E5%99%A8-%E4%B8%AD%E6%96%87)
- [判定与血量](https://github.com/SK-la/Ez2Lazer/wiki/%E5%88%A4%E5%AE%9A%E4%B8%8E%E8%A1%80%E9%87%8F-%E4%B8%AD%E6%96%87)

## 快速安装

1. 下载程序并解压到任意目录。
2. 进入设置，使用 `更改osu!文件夹位置` 指向你的数据路径。
3. 下载并解压 EzResources 到该路径下（形成 `.../EzResources`）。

详细步骤请查看 [安装指南（中文）](https://github.com/SK-la/Ez2Lazer/wiki/%E5%AE%89%E8%A3%85%E6%8C%87%E5%8D%97-(%E4%B8%AD%E6%96%87))。

## 编译说明

```bash
git clone https://github.com/SK-la/Ez2Lazer
git clone https://github.com/SK-la/ez2lazer-framework
git clone https://github.com/SK-la/ez2lazer-resources
```

`ez2lazer.Framework`、`ez2lazer.Game.Resources` 的版本在 [Ez2Lazer.Dependencies.props](Ez2Lazer.Dependencies.props) 中维护。

如果要改用同级工程联调，可分别控制两个开关：
- `<UseEz2LazerLocalFrameworkProject>true</UseEz2LazerLocalFrameworkProject>` / `-p:UseEz2LazerLocalFrameworkProject=true`
- `<UseEz2LazerLocalResourcesProject>true</UseEz2LazerLocalResourcesProject>` / `-p:UseEz2LazerLocalResourcesProject=true`

需要回到 NuGet 引用时，把对应开关设为 `false` 即可。

自编译版本不会显示游戏内更新选项，也不会从 SK-la/Ez2Lazer Releases 拉取更新。

## 特别感谢
- [osu!](https://github.com/ppy/osu)：原版游戏与框架。
- [YuLiangSSS](https://osu.ppy.sh/users/15889644)：贡献了许多有趣的 Mod。
- [g0v0](https://github.com/GooGuTeam/g0v0-server)：提供私服支持。
- [mania-hub](https://github.com/aleju03/mania-hub)：数据展示灵感。
- [MinaCalc](https://github.com/etternagame/etterna)：技能算法。
