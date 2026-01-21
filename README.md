<p align="center">
  <img width="500" alt="osu! logo" src="assets/lazer.png">
</p>

# Ez2Lazer!

This is always a pre-release version, maintained by me personally

## Latest release: [Windows 10+ (x64)](https://github.com/SK-la/Ez2Lazer/releases)
- **Setup [EzResources](https://la1225-my.sharepoint.com/:f:/g/personal/la_la1225_onmicrosoft_com/EiosAbw_1C9ErYCNRD1PQvkBaYvhflOkt8G9ZKHNYuppLg?e=DWY1kn) Pack to osu datebase path.**

- A desktop platform with the [.NET 8.0 RunTime](https://dotnet.microsoft.com/download) installed.

- Develop modifications using Rider + VS Code

When working with the codebase, we recommend using an IDE with intelligent code completion and syntax highlighting, such as the latest version of [Visual Studio](https://visualstudio.microsoft.com/vs/), [JetBrains Rider](https://www.jetbrains.com/rider/), or [Visual Studio Code](https://code.visualstudio.com/) with the [EditorConfig](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) and [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) plugin installed.

## Build Instructions
- Clone the repository
```bash

git clone SK-la/Ez2Lazer
git clone SK-la/osu-framework
git clone SK-la/osu-resources
// There is a lack of special texture resources in Resource, so it is recommended that you use the DLL in the release package to replace it after building

build Ez2Lazer
```

## Feature support 
(It's not always updated here)

### Vedio Main Background
- Support vedio as main background (.webm)
<img width="3440" height="1440" alt="img_10" src="https://github.com/user-attachments/assets/f0277860-8db5-4244-8dd0-e6eb8ac9fcea" />
<img width="1039" height="156" alt="img_13" src="https://github.com/user-attachments/assets/18da55c5-a996-48ba-be45-7071d9c71922" />


### SongSelect Ez to Filter
- Keys Filter (One\Multi)
- Notes by column
- Avg\Max KPS
<img width="1524" height="637" alt="img_12" src="https://github.com/user-attachments/assets/8caae7a3-74d0-42fa-a9de-a15385541ca7" />


### Freedom Speed Adjust System

| - | Speed     | + |
|--|-----------|--|
| ← | Base Speed | → |
| 0 | Scroll Speed | 401 |

Base Speed - Setting Speed(0~401) * MPS(Gaming ±Speed)

<img width="1055" height="677" alt="img_11" src="https://github.com/user-attachments/assets/f2878062-5e12-40a3-9f51-90637f617053" />



### New Skin System
- Ez Pro SKin System
   - New Ez Style SKin Sprites - 全新Ez风格皮肤素材
   - New Dynamic real-time preview SKin Options - 全新动态实时预览皮肤选项
   - Built-in skin.ini settings - 内置skin.ini设置
   - New color settings, column type setting system - 全新颜色设置、列类型设置系统

  <img width="3440" height="1440" alt="img_5" src="https://github.com/user-attachments/assets/89cb4ea0-3a03-4252-8378-91e15789e229" />
  <img width="3440" height="1440" alt="img_4" src="https://github.com/user-attachments/assets/246bbc63-f6a5-47e1-a05c-d5f7606bdae2" />

- Preload skin resources when entering the game interface to reduce lag in the early stages of the game
- Change to the Smart Subfolder drop-down list
   <img width="1167" height="759" alt="Snipaste_2025-12-07_21-37-22" src="https://github.com/user-attachments/assets/6485aa3f-f153-4cbf-be57-d5bb7f85a615" />

- Mania Playfield Support Blur and Dim Effect

   <img width="1129" height="1131" alt="Snipaste_2025-12-07_21-29-40" src="https://github.com/user-attachments/assets/d1959f40-a90a-4803-9d0a-3cd36663b8dd" />

- HUD Components
- <img width="443" height="974" alt="img_16" src="https://github.com/user-attachments/assets/71dd717c-b4c6-43ec-90b3-c5d974575e80" />


### Pool Judgment (Empty Judgment)

- Pool判定不影响ACC、Combo，仅严格扣血，连续的Pool判将累加扣血幅度.
- The pool hit result does not affect ACC and Combo, only strict blood deduction, and continuous pools will accumulate the blood deduction amplitude.
> -500 < -Pool < miss < +Pool < +150
>
> <img width="629" height="77" alt="img_9" src="https://github.com/user-attachments/assets/523d62d3-9796-4657-b1a8-359586c7ab83" />


### New Judgment Mode

> For the time being, only the settings are implemented, and the actual parameters will be matched in the future
>
> 暂时仅实现设置，未来匹配实际参数

<img width="1041" height="585" alt="img_14" src="https://github.com/user-attachments/assets/d4264792-db76-478a-9351-31527a030368" />

- Ez2AC: LN-NoRelease (Press and hold LN-tail to perfect)
> { 18.0, 32.0, 64.0, 80.0, 100.0, 120.0 }

- O2Jam: None-Press is miss
>       coolRange = 7500.0 / bpm;
>       goodRange = 22500.0 / bpm;
>       badRange = 31250.0 / bpm;

- IIDX (instant): LN-NoRelease
> { 20.0, 40.0, 60.0, 80.0, 100.0, 120.0 }

- Malody (instant): LN-NoRelease
> { 20.0, 40.0, 60.0, 80.0, 100.0, 120.0 }

Audio System

- 增加采样打断重放（防止全key音谱多音轨重叠变成噪音）
- Added sampling interruption playback (to prevent overlapping multiple tracks of the full key note spectrum from becoming noise)
- 选歌界面增加预览keysound和故事板背景音乐
- Added preview keysound and storyboard background music to the song selection interface

### Static Score
- Space Graph
  <img width="2511" height="464" alt="img_7" src="https://github.com/user-attachments/assets/681064a3-d632-41cf-a575-984d6f7e3c10" />


- Column One by One
  <img width="2560" height="889" alt="img_8" src="https://github.com/user-attachments/assets/d245f649-c64a-4e4b-ad43-365f657ef155" />


### Other
- Scale Only Mode
  <img width="1023" height="162" alt="img_15" src="https://github.com/user-attachments/assets/fd6f26e6-ffce-421a-930e-7b29bb7c6281" />


## Mod

<img width="1136" height="932" alt="img_1" src="https://github.com/user-attachments/assets/f09e8c19-6459-4431-a40d-bfb3700fd24f" />


## Special Thanks
- [osu!](https://github.com/ppy/osu): The original game and framework. The code is very strong and elegant.
- [YuLiangSSS](https://osu.ppy.sh/users/15889644): Many fun mods contributed.



## Licence

*osu!*'s code and framework are licensed under the [MIT licence](https://opensource.org/licenses/MIT). Please see [the licence file](LICENCE) for more information. [tl;dr](https://tldrlegal.com/license/mit-license) you can do whatever you want as long as you include the original copyright and license notice in any copy of the software/source.

Please note that this *does not cover* the usage of the "osu!" or "ppy" branding in any software, resources, advertising or promotion, as this is protected by trademark law.

Please also note that game resources are covered by a separate licence. Please see the [ppy/osu-resources](https://github.com/ppy/osu-resources) repository for clarifications.
