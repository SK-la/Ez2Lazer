<p align="center">
  <img width="500" alt="osu! logo" src="assets/lazer.png">
</p>

# Ez2Lazer

中文 | English

Ez2Lazer 是基于 osu! lazer 的深度改造分支，聚焦 Mania/BMS 生态、高可定制 HUD、判定系统切换和谱面分析工具链。  
Ez2Lazer is a heavily customized branch based on osu! lazer, focused on Mania/BMS workflows, configurable HUD, switchable judgement systems and analysis tools.

## 下载与运行 / Download and Run

- 最新版本发布页 / Latest releases: [SK-la/Ez2Lazer Releases](https://github.com/SK-la/Ez2Lazer/releases)
- 资源包 / Resource pack: [EzResources (OneDrive)](https://la1225-my.sharepoint.com/:f:/g/personal/la_la1225_onmicrosoft_com/EiosAbw_1C9ErYCNRD1PQvkBaYvhflOkt8G9ZKHNYuppLg?e=DWY1kn)
- 运行时要求 / Runtime: [.NET 8.0 Runtime](https://dotnet.microsoft.com/download)

**自动更新（推荐）** / **Auto-update (recommended)**  
- Windows：下载 Release 中的 `ez2lazer-win-Setup.exe` 安装（需已安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download)）；之后可在游戏内接收增量更新。  
- Windows: use `ez2lazer-win-Setup.exe` from Releases (requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download)); in-game updates download deltas afterward.  
- 手动安装请用 `Ez2Lazer_release_*.zip` 解压运行；zip 无法使用增量更新，需改用 Setup 安装一次。  
- For manual installs use `Ez2Lazer_release_*.zip`; zip installs cannot receive delta updates until you switch to Setup once.

> 未安装 EzResources 时，Ez Pro Skin 和 Ez HUD 组件会缺失贴图。  
> Without EzResources, Ez Pro Skin and Ez HUD widgets will miss textures.

## 文档入口 / Documentation

主文档迁移到 Wiki，README 只保留快速入口。  
The full documentation now lives in Wiki; this README stays as a quick index.

- Wiki 首页 / Home: [Ez2Lazer Wiki](https://github.com/SK-la/Ez2Lazer/wiki)
- 中文总览 / CN overview: [功能总览（中文）](https://github.com/SK-la/Ez2Lazer/wiki/%E5%8A%9F%E8%83%BD%E6%80%BB%E8%A7%88-%E4%B8%AD%E6%96%87)
- English overview: [Feature Overview](https://github.com/SK-la/Ez2Lazer/wiki/Feature-Overview-English)
- 发布说明规范 / Release workflow: [发布说明规范](https://github.com/SK-la/Ez2Lazer/wiki/%E5%8F%91%E5%B8%83%E8%AF%B4%E6%98%8E%E8%A7%84%E8%8C%83-%E4%B8%AD%E6%96%87)

### 功能板块（中文）
- [选歌界面](https://github.com/SK-la/Ez2Lazer/wiki/%E9%80%89%E6%AD%8C%E7%95%8C%E9%9D%A2-%E4%B8%AD%E6%96%87)
- [游戏设置](https://github.com/SK-la/Ez2Lazer/wiki/%E6%B8%B8%E6%88%8F%E8%AE%BE%E7%BD%AE-%E4%B8%AD%E6%96%87)
- [Skin 系统](https://github.com/SK-la/Ez2Lazer/wiki/Skin-%E7%B3%BB%E7%BB%9F-%E4%B8%AD%E6%96%87)
- [Mod 系统](https://github.com/SK-la/Ez2Lazer/wiki/Mod-%E7%B3%BB%E7%BB%9F-%E4%B8%AD%E6%96%87)
- [HUD 组件](https://github.com/SK-la/Ez2Lazer/wiki/HUD-%E7%BB%84%E4%BB%B6-%E4%B8%AD%E6%96%87)
- [编辑器](https://github.com/SK-la/Ez2Lazer/wiki/%E7%BC%96%E8%BE%91%E5%99%A8-%E4%B8%AD%E6%96%87)
- [判定与血量](https://github.com/SK-la/Ez2Lazer/wiki/%E5%88%A4%E5%AE%9A%E4%B8%8E%E8%A1%80%E9%87%8F-%E4%B8%AD%E6%96%87)

### Feature Areas (English)
- [Song Select](https://github.com/SK-la/Ez2Lazer/wiki/Song-Select-English)
- [Game Settings](https://github.com/SK-la/Ez2Lazer/wiki/Game-Settings-English)
- [Skin System](https://github.com/SK-la/Ez2Lazer/wiki/Skin-System-English)
- [Mod System](https://github.com/SK-la/Ez2Lazer/wiki/Mod-System-English)
- [HUD Widgets](https://github.com/SK-la/Ez2Lazer/wiki/HUD-Widgets-English)
- [Editor](https://github.com/SK-la/Ez2Lazer/wiki/Editor-English)
- [Judgement and Health](https://github.com/SK-la/Ez2Lazer/wiki/Judgement-and-Health-English)

## 快速安装 / Quick Setup

1. 下载程序并解压到任意目录。  
2. 进入设置，使用 `更改osu!文件夹位置` 指向你的数据路径。  
3. 下载并解压 EzResources 到该路径下（形成 `.../EzResources`）。  

详细步骤请查看 Wiki 安装页：  
- [安装指南（中文）](https://github.com/SK-la/Ez2Lazer/wiki/%E5%AE%89%E8%A3%85%E6%8C%87%E5%8D%97-(%E4%B8%AD%E6%96%87))
- [Installation Guide (English)](https://github.com/SK-la/Ez2Lazer/wiki/Installation-Guide-(English))

## Build Instructions

```bash
git clone https://github.com/SK-la/Ez2Lazer
git clone https://github.com/SK-la/osu-framework
git clone https://github.com/SK-la/osu-resources
```

建议把三个仓库放在同一级目录后再构建。  
It is recommended to place all three repositories side by side before building.

自编译版本不会显示游戏内更新选项，也不会从 SK-la/Ez2Lazer Releases 拉取更新。  
Self-built copies hide in-game update settings and do not check SK-la/Ez2Lazer Releases for updates.

## Release Notes Automation

Use the helper script to generate a categorized draft from commit range:

```powershell
pwsh ./GenerateReleaseNotes.ps1 -FromRef "2026.5.1" -ToRef "2026.5.6" -Output "../release-2026.5.6.md" -Title "Release 2026.5.6"
```

Then polish the draft and publish it on GitHub Releases.

## Special Thanks
- [osu!](https://github.com/ppy/osu): The original game and framework.
- [YuLiangSSS](https://osu.ppy.sh/users/15889644): Contributed many fun mods.

## Licence

*osu!* code and framework are licensed under the [MIT licence](https://opensource.org/licenses/MIT).  
See [LICENCE](LICENCE) for details.

This does not cover usage of "osu!" or "ppy" branding, which is protected by trademark law.  
Game resources are covered by a separate licence in [ppy/osu-resources](https://github.com/ppy/osu-resources).
