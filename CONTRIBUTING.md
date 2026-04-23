# Contributing Guidelines

感谢你对本项目的关注！再开始之前，请先阅读以下指南及代码的行为守则。在收到补丁后，我会默认你已经阅读了以下内容，并接受我会提出的审阅意见。

## 第三方发行版说明

当前仓库基于由 [ppy](https://github.com/ppy) 的 [osu!](https://github.com/ppy/osu) 项目修改。
- 由于本人能力有限，无法保证所有功能在每个版本均能正常使用。（我无法每次都进行完整的功能测试）
- 可能存在与原版不同的行为。
- 原则上可以与官方osu使用统一的数据库，不干扰配置文件。我使用client_XX.realm文件存储数据，官方使用client.realm（但建议自行备份原文件以防数据不兼容、文件损坏等）。
- 原则上可以与官方osu使用同一账号，release版可以登录并连接官方服务器，可以正常下图、下载排行榜、保存到处成绩等；
- 绝对不支持向官方服务器做出任何上传行为（上传谱面、成绩等）和多人游戏。
- 可以使用第三方服务器（如有），我只能做有限支持，不保证每个版本的可用性。
- 大部分精力会放在mania模式的功能性开发上，接受其他模式功能性补丁。
- 我非常乐意接受任何反馈和建议，通过任何渠道（GitHub issues、邮件、QQ等），但不保证每个建议都能被采纳。

如果你想提交补丁，我希望你能通过某些方式在发布PR前与我联系，这能够节省我大量的时间和精力。
- 在你提交PR前，请先通过邮件或QQ与我联系，说明你想做的改动。
- 我需要先确认我本地的代码是否上传到了GitHub。（否则我需要花费大量精力解决冲突问题）

## 代码的行为守则

- 所有新创建的文件必须包含ppy的版权声明。新建文件时，项目应该会自动添加。
- 遵循现有的代码风格和惯例，在提交前经过测试。
- 新建文件尽可能放在独立文件夹中，在我提供的唯一一级分类文件夹下(如osu.Game.Rulesets.Mania.LAsEzMania)，创建带有byID字样的二级文件夹
- 尽量不要出现硬编码字符串，尽量不要使用反射，除非别无选择。
- 除using外，尽量不要到处拉屎（比如一个地方改一行，然后到处修改）。
- 尽量不要增加新的第三方依赖，除非功能很棒且没有更好的替代品。
- 本地化支持：统一使用EzLocalizationManager及相关继承类进行本地化字符串管理。

# English Version

## Third-Party Distribution Instructions

The current repository is based on [ppy](https://github.com/ppy)'s [osu!](https://github.com/ppy/osu) Project modifications.
- Due to my limited ability, I cannot guarantee that all functions will work properly in each version. (I can't do a full functional test every time)
- There may be different behavior from the original.
- In principle, it is possible to use a unified database with the official OSU without interfering with the configuration file. I use client_XX.realm file to store data, and the official uses client.realm (but it is recommended to back up the original file yourself in case of data incompatibility, file corruption, etc.).
- In principle, you can use the same account as the official OSU, the release version can log in and connect to the official server, you can download the picture normally, download the leaderboard, save the results everywhere, etc.
- Any uploads to official servers (uploading beatmaps, scores, etc.) and multiplayer games are absolutely not supported.
- Third-party servers (if any) are available, I can only do limited support and do not guarantee the availability of each version.
- Most of the effort will be focused on the functional development of the mania mode, accepting functional patches for other modes.
- I am more than happy to accept any feedback and suggestions through any channel (GitHub issues, email, QQ, etc.), but there is no guarantee that every suggestion will be adopted.

If you want to submit a patch, I would like you to contact me before publishing a PR in some way, which will save me a lot of time and effort.
- Before you submit a PR, please contact me via email or QQ with the changes you want to make.
- I need to check if my local code is uploaded to GitHub. (Otherwise I would have to spend a lot of energy resolving conflict issues)

## Code of Conduct for Code

- All newly created files must include a copyright notice from PPY. When you create a new file, the project should be added automatically.
- Follow existing code styles and conventions, tested before committing.
- Place new files in a separate folder as much as possible, under the only first-level classification folder I provide (e.g. osu. Game.Rulesets.Mania.LAsEzMania), creating a secondary folder with the word byID
- Try not to appear hardcoded strings and try not to use reflections unless there is no other choice.
- Try not to shit everywhere except using (like changing a line in one place and then changing it everywhere).
- Try not to add new third-party dependencies unless the functionality is great and there are no better alternatives.
- Localization support: EzLocalizationManager and related inheritance classes are used for localized string management.