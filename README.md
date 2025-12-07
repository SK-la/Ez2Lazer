<p align="center">
  <img width="500" alt="osu! logo" src="assets/lazer.png">
</p>

# Ez2Lazer!


## Running osu!

If you are just looking to give the game a whirl, you can grab the latest release for your platform:

### Latest release: [Windows 10+ (x64)](https://github.com/SK-la/Ez2Lazer/releases)
- Setup EzResources() Pack to osu datebase path.

- A desktop platform with the [.NET 8.0 SDK](https://dotnet.microsoft.com/download) installed.

When working with the codebase, we recommend using an IDE with intelligent code completion and syntax highlighting, such as the latest version of [Visual Studio](https://visualstudio.microsoft.com/vs/), [JetBrains Rider](https://www.jetbrains.com/rider/), or [Visual Studio Code](https://code.visualstudio.com/) with the [EditorConfig](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) and [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) plugin installed.

## Feature support

### Vedio Main Background
- Support vedio as main background (.webm)
![img_10.png](assets/img_10.png)
![img_13.png](assets/img_13.png)

### SongSelect Ez to Filter
- Keys Filter (One\Multi)
- Notes by column
- Avg\Max KPS
![img_12.png](assets/img_12.png)

### Freedom Speed Adjust System

| - | Speed     | + |
|--|-----------|--|
| ← | Base Speed | → |
| 0 | Scroll Speed | 401 |

Base Speed - Setting Speed(0~401) * MPS(Gaming ±Speed)

![img_11.png](assets/img_11.png)


### New Skin System
- Ez Pro SKin System
   - New Ez Style SKin Sprites - 全新Ez风格皮肤素材
   - New Dynamic real-time preview SKin Options - 全新动态实时预览皮肤选项
   - Built-in skin.ini settings - 内置skin.ini设置
   - New color settings, column type setting system - 全新颜色设置、列类型设置系统

  ![img_5.png](assets/img_5.png)
  ![img_4.png](assets/img_4.png)

- Preload skin resources when entering the game interface to reduce lag in the early stages of the game
- Change to the Smart Subfolder drop-down list
   <img width="1167" height="759" alt="Snipaste_2025-12-07_21-37-22" src="https://github.com/user-attachments/assets/6485aa3f-f153-4cbf-be57-d5bb7f85a615" />

- Mania Playfield Support Blur and Dim Effect

   <img width="1129" height="1131" alt="Snipaste_2025-12-07_21-29-40" src="https://github.com/user-attachments/assets/d1959f40-a90a-4803-9d0a-3cd36663b8dd" />

- HUD Components
- ![img_16.png](assets/img_16.png)


### Pool Judgment (Empty Judgment)

- Pool判定不影响ACC、Combo，仅严格扣血，连续的Pool判将累加扣血幅度.
- The pool hit result does not affect ACC and Combo, only strict blood deduction, and continuous pools will accumulate the blood deduction amplitude.
> -500 < -Pool < miss < +Pool < +150
>
> ![img_9.png](assets/img_9.png)


### New Judgment Mode

> For the time being, only the settings are implemented, and the actual parameters will be matched in the future
>
> 暂时仅实现设置，未来匹配实际参数

![img_14.png](assets/img_14.png)


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
  ![img_7.png](assets/img_7.png)

- Column One by One
  ![img_8.png](assets/img_8.png)

### Other
- Scale Only Mode
  ![img_15.png](assets/img_15.png)

## Mod

![img_1.png](assets/img_1.png)

## Special Thanks
- [YuLiangSSS](https://osu.ppy.sh/users/15889644): Many fun mods contributed

## Licence

*osu!*'s code and framework are licensed under the [MIT licence](https://opensource.org/licenses/MIT). Please see [the licence file](LICENCE) for more information. [tl;dr](https://tldrlegal.com/license/mit-license) you can do whatever you want as long as you include the original copyright and license notice in any copy of the software/source.

Please note that this *does not cover* the usage of the "osu!" or "ppy" branding in any software, resources, advertising or promotion, as this is protected by trademark law.

Please also note that game resources are covered by a separate licence. Please see the [ppy/osu-resources](https://github.com/ppy/osu-resources) repository for clarifications.
