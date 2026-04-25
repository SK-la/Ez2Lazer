// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.Cursor
{
    public partial class OsuTooltipContainer : TooltipContainer
    {
        protected override ITooltip CreateTooltip() => new OsuTooltip();

        public OsuTooltipContainer(CursorContainer cursor)
            : base(cursor)
        {
        }

        protected override double AppearDelay => (1 - CurrentTooltip.Alpha) * base.AppearDelay; // reduce appear delay if the tooltip is already partly visible.

        public partial class OsuTooltip : Tooltip
        {
            private const float max_width = 500;

            private readonly Box background;
            private readonly TextFlowContainer text;
            private bool instantMovement = true;

            private LocalisableString lastContent;

            public override void SetContent(LocalisableString content)
            {
                if (content.Equals(lastContent))
                    return;

                string contentStr = content.ToString();

                // 清空文本容器
                text.Clear();

                // 解析并渲染富文本（支持等宽标记）
                parseAndRenderRichText(contentStr);

                if (IsPresent)
                {
                    AutoSizeDuration = 250;
                    background.FlashColour(OsuColour.Gray(0.4f), 1000, Easing.OutQuint);
                }
                else
                    AutoSizeDuration = 0;

                lastContent = content;
            }

            /// <summary>
            /// 解析并渲染富文本，支持以下标记：
            /// - [MONO]...[/MONO]: 等宽字体区域
            /// - 一行有2个及以上|时，使用表格渲染
            /// </summary>
            private void parseAndRenderRichText(string content)
            {
                // 按行分割处理
                string[] lines = content.Split('\n');

                // 分组：表格和非表格
                List<List<string>> groups = new List<List<string>>();
                List<bool> isTableGroup = new List<bool>();
                List<string> currentGroup = new List<string>();
                bool? currentIsTable = null;

                foreach (string line in lines)
                {
                    // 检测是否是表格行（2个或以上的|）
                    int pipeCount = line.Count(c => c == '|');
                    bool isTable = pipeCount >= 2;

                    currentIsTable ??= isTable;

                    // 如果表格类型改变，开启新组
                    if (currentIsTable != isTable && currentGroup.Count > 0)
                    {
                        groups.Add(new List<string>(currentGroup));
                        isTableGroup.Add(currentIsTable.Value);
                        currentGroup.Clear();
                        currentIsTable = isTable;
                    }

                    currentGroup.Add(line);
                }

                // 保存最后一组
                if (currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                    isTableGroup.Add(currentIsTable ?? false);
                }

                // 渲染每组
                bool firstLine = true;

                for (int g = 0; g < groups.Count; g++)
                {
                    if (isTableGroup[g])
                    {
                        // 渲染表格
                        renderTable(groups[g], ref firstLine);
                    }
                    else
                    {
                        // 渲染普通文本（支持[MONO]标记）
                        foreach (string line in groups[g])
                        {
                            if (!firstLine)
                                text.AddText("\n");

                            processMonospaceLine(line);
                            firstLine = false;
                        }
                    }
                }
            }

            /// <summary>
            /// 渲染表格组
            /// </summary>
            private void renderTable(List<string> lines, ref bool firstLine)
            {
                // 渲染每一行（不计算列宽，直接使用用户手动空格）
                foreach (string line in lines)
                {
                    if (!firstLine)
                        text.AddText("\n");

                    string[] columns = line.Split('|');
                    renderPipeTableRow(columns);
                    firstLine = false;
                }
            }

            /// <summary>
            /// 渲染单行表格（|分隔）
            /// </summary>
            private void renderPipeTableRow(string[] columns)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    if (i > 0)
                    {
                        // 列之间添加2个空格（不显示|）
                        addMonospaceTextPreserveSpaces("  ");
                    }

                    // 不trim，保留用户手动添加的空格
                    string content = columns[i];
                    addMonospaceTextPreserveSpaces(content);
                }
            }

            /// <summary>
            /// 处理包含[MONO]标记的行
            /// </summary>
            private void processMonospaceLine(string line)
            {
                // 匹配 [MONO]...[/MONO]
                const string pattern = @"\[MONO\](.*?)\[/MONO\]";
                var matches = Regex.Matches(line, pattern, RegexOptions.Singleline);

                int lastIndex = 0;

                foreach (Match match in matches)
                {
                    // 添加普通文本部分
                    if (match.Index > lastIndex)
                    {
                        string normalText = line.Substring(lastIndex, match.Index - lastIndex);
                        addNormalText(normalText);
                    }

                    // 添加等宽文本部分
                    string monoText = match.Groups[1].Value;
                    addMonospaceTextPreserveSpaces(monoText);

                    lastIndex = match.Index + match.Length;
                }

                // 添加剩余文本
                if (lastIndex < line.Length)
                {
                    string remainingText = line.Substring(lastIndex);
                    addNormalText(remainingText);
                }
            }

            private void addNormalText(string content)
            {
                text.AddText(content, t => t.Font = OsuFont.GetFont(weight: FontWeight.Regular));
            }

            private void addMonospaceTextPreserveSpaces(string content)
            {
                // 逐字符添加，数字和空格使用等宽字体
                foreach (char c in content)
                {
                    // 数字和空格使用等宽字体，其他字符使用普通字体
                    bool isDigitOrSpace = char.IsDigit(c) || c == ' ';
                                            
                    text.AddText(c.ToString(), t =>
                    {
                        t.Font = OsuFont.GetFont(weight: FontWeight.Regular, fixedWidth: isDigitOrSpace);
                        
                        // 对于空格字符，确保它不会被优化掉
                        if (c == ' ')
                        {
                            t.UseFullGlyphHeight = false;
                        }
                    });
                }
            }

            public OsuTooltip()
            {
                AutoSizeEasing = Easing.OutQuint;

                CornerRadius = 5;
                Masking = true;
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = Color4.Black.Opacity(40),
                    Radius = 5,
                };
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.9f,
                    },
                    text = new TextFlowContainer(f =>
                    {
                        f.Font = OsuFont.GetFont(weight: FontWeight.Regular);
                    })
                    {
                        Margin = new MarginPadding(5),
                        AutoSizeAxes = Axes.Both,
                        MaximumSize = new Vector2(max_width, float.PositiveInfinity),
                    }
                };
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colour)
            {
                background.Colour = colour.Gray3;
            }

            protected override void PopIn()
            {
                instantMovement |= !IsPresent;
                this.FadeIn(300, Easing.OutQuint);
            }

            protected override void PopOut() => this.Delay(150).FadeOut(300, Easing.OutQuint);

            public override void Move(Vector2 pos)
            {
                if (instantMovement)
                {
                    Position = pos;
                    instantMovement = false;
                }
                else
                {
                    // This method is called every frame so we can do this safely here.
                    Position = Interpolation.ValueAt(Time.Elapsed, Position, pos, 0, 120, Easing.OutQuint);
                }
            }
        }
    }
}
