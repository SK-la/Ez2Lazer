// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.EzOsuGame.Scoring
{
    public static class EzScoreInfoExtensions
    {
        /// <summary>
        /// 成绩上的 Mania 判定/血量模式。Mania 成绩恒为 true：没有"未设置"这一档，Lazer(0) 即默认。
        /// </summary>
        /// <remarks>
        /// 存量负值（EZ1 迁移、<c>.osr</c> 导入当年写入的 -1）按默认 Lazer 处理，不视为"无模式"。
        /// 判定为"非默认"只能靠值本身是否 != Lazer，不能靠是否有值。
        /// </remarks>
        public static bool TryGetManiaGameplayModes(this ScoreInfo score, out int judgeMode, out int healthMode)
        {
            judgeMode = score.JudgeMode;
            healthMode = score.HealthMode;

            if (score.Ruleset.OnlineID != 3)
                return false;

            if (judgeMode < 0)
                judgeMode = (int)EzEnumJudgeMode.Lazer;

            if (healthMode < 0)
                healthMode = (int)EzEnumHealthMode.Lazer;

            return true;
        }

        /// <summary>
        /// Mania 展示层 JudgeMode：嵌入成绩 &gt; 局内全局（score 为 null）&gt; 旧成绩 Lazer 回退。
        /// 供 HUD 判定计数器与 Ruleset 展示名共用。
        /// </summary>
        public static EzEnumJudgeMode ResolveDisplayJudgeMode(ScoreInfo? score)
        {
            if (score != null && score.TryGetManiaGameplayModes(out int judgeMode, out _))
                return (EzEnumJudgeMode)judgeMode;

            if (score == null)
                return GlobalConfigStore.EzConfig.Get<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode);

            return EzEnumJudgeMode.Lazer;
        }

        public static void ApplyManiaGameplayModes(this ScoreInfo score, DrawableRuleset? drawableRuleset)
        {
            if (drawableRuleset is not IManiaGameplayModeSnapshot snapshot)
                return;

            // 游玩与 Replay 结束进结算均写入当场 Drawable 环境，供 ForStored 解析与左栏 Statistics 对齐。
            score.JudgeMode = snapshot.JudgeMode;
            score.HealthMode = snapshot.HealthMode;
        }

        public static string GetJudgeModeDisplayName(int judgeMode) => getEnumDescription<EzEnumJudgeMode>(judgeMode);

        public static string GetHealthModeDisplayName(int healthMode) => getEnumDescription<EzEnumHealthMode>(healthMode);

        /// <summary>
        /// 非 mania 成绩的 std 判定轨（OSL-013）。语义同 <see cref="TryGetManiaGameplayModes"/>：
        /// 档位以成绩嵌入的 <see cref="ScoreInfo.JudgeMode"/> 为准，存量负值（当初写入的 -1）按默认 Lazer
        /// 处理，不视为"无模式"；故不要回读实时配置，否则同一成绩在重算与展示两处会得到不同档位。
        /// 仅 Osu 规则集返回 true。
        /// </summary>
        public static bool TryGetStdGameplayMode(this ScoreInfo score, out int stdMode)
        {
            stdMode = score.JudgeMode;

            if (score.Ruleset.OnlineID != 0)
                return false;

            if (stdMode < 0)
                stdMode = (int)EzEnumJudgeMode.Lazer;

            return true;
        }

        /// <summary>
        /// Creates a vertical mode label block for use outside <see cref="FillDirection.Full"/> containers.
        /// Lazer 是默认档，不展示：只画非默认的档位（Mania HIT/HP；Osu 判定轨 JUDGE）；全默认时返回透明占位。
        /// </summary>
        public static Drawable CreateDisplayDrawable(ScoreInfo score, float fontSize = 11, Anchor anchor = Anchor.TopLeft)
        {
            if (score.TryGetManiaGameplayModes(out int judgeMode, out int healthMode))
            {
                return createModeBlock(anchor, fontSize,
                    ("Hit", judgeMode == (int)EzEnumJudgeMode.Lazer ? null : GetJudgeModeDisplayName(judgeMode)),
                    ("HP", healthMode == (int)EzEnumHealthMode.Lazer ? null : GetHealthModeDisplayName(healthMode)));
            }

            if (score.TryGetStdGameplayMode(out int stdMode) && stdMode != (int)EzEnumJudgeMode.Lazer)
                return createModeBlock(anchor, fontSize, ("Judge", GetJudgeModeDisplayName(stdMode)));

            return createPlaceholder(anchor);
        }

        /// <summary>
        /// 按给定档位行构建纵向标签块；值为 <see langword="null"/> 的行（默认 Lazer）跳过。全为空时返回占位。
        /// </summary>
        private static Drawable createModeBlock(Anchor anchor, float fontSize, params (string Label, string? Value)[] lines)
        {
            var children = new List<Drawable>();

            foreach (var (label, value) in lines)
            {
                if (value == null)
                    continue;

                children.Add(createModeLine(label, value, fontSize, anchor));
            }

            if (children.Count == 0)
                return createPlaceholder(anchor);

            return new FillFlowContainer
            {
                Anchor = anchor,
                Origin = anchor,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                ChildrenEnumerable = children,
            };
        }

        private static Drawable createPlaceholder(Anchor anchor) => new Container
        {
            Anchor = anchor,
            Origin = anchor,
            AutoSizeAxes = Axes.Both,
            Alpha = 0f,
        };

        private static Drawable createModeLine(string label, string value, float fontSize, Anchor anchor) => new FillFlowContainer
        {
            Anchor = anchor,
            Origin = anchor,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4, 0),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = label,
                    Anchor = anchor,
                    Origin = anchor,
                    Font = OsuFont.GetFont(size: fontSize, weight: FontWeight.Bold),
                    Colour = OsuColour.Gray(0.65f),
                },
                new OsuSpriteText
                {
                    Text = value,
                    Anchor = anchor,
                    Origin = anchor,
                    Font = OsuFont.GetFont(size: fontSize, weight: FontWeight.SemiBold),
                },
            }
        };

        private static string getEnumDescription<T>(int value) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
                return value.ToString();

            var enumValue = (T)Enum.ToObject(typeof(T), value);
            var field = typeof(T).GetField(enumValue.ToString());

            return field?.GetCustomAttribute<DescriptionAttribute>()?.Description
                   ?? enumValue.ToString();
        }
    }
}
