// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers.Markdown;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterfaceV2
{
    public partial class MarkdownTooltip : VisibilityContainer, osu.Framework.Graphics.Cursor.ITooltip<LocalisableString>
    {
        private const float max_width = 500;

        private Box background = null!;
        private OsuMarkdownContainer? markdown;
        private TextFlowContainer? textFlow;

        private bool instantMovement = true;
        private LocalisableString lastContent;

        public MarkdownTooltip()
        {
            Alpha = 0;
            AutoSizeAxes = Axes.Both;
            CornerRadius = 5;
            Masking = true;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Color4.Black.Opacity(0.4f),
                Radius = 5,
            };
        }

        public void SetContent(LocalisableString content)
        {
            if (content.Equals(lastContent))
                return;

            string s = content.ToString();

            // Detect markdown table by presence of pipes and a separator row (---)
            bool isTable;

            try
            {
                string[] lines = s.Split('\n');
                isTable = s.Contains('|') && lines.Any(l => Regex.IsMatch(l, @"^\s*\|?\.*\|.*"));
            }
            catch
            {
                isTable = s.Contains('|');
            }

            if (isTable && markdown != null)
            {
                markdown.Show();
                textFlow?.Hide();
                // Use a reduced line spacing for tooltip context
                markdown.LineSpacing = 16;
                markdown.Text = s;
            }
            else if (textFlow != null)
            {
                markdown?.Hide();
                textFlow.Show();
                textFlow.Clear();
                parseAndRenderRichText(s, textFlow);
            }

            lastContent = content;
        }

        void osu.Framework.Graphics.Cursor.ITooltip.SetContent(object content) => SetContent((LocalisableString)content);

        public void Move(Vector2 pos)
        {
            if (instantMovement)
            {
                Position = pos;
                instantMovement = false;
            }
            else
            {
                Position = Interpolation.ValueAt(Time.Elapsed, Position, pos, 0, 120, Easing.OutQuint);
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0.9f,
            };

            textFlow = new TextFlowContainer(f => f.Font = OsuFont.GetFont(weight: FontWeight.Regular))
            {
                Margin = new MarginPadding(5),
                AutoSizeAxes = Axes.Both,
                MaximumSize = new Vector2(max_width, float.PositiveInfinity),
            };

            markdown = new OsuMarkdownContainer
            {
                AutoSizeAxes = Axes.Y,
                Width = max_width,
            };

            Children = new Drawable[]
            {
                background,
                new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            textFlow,
                            markdown
                        }
                    }
                }
            };

            background.Colour = colour.Gray3;
            // default visibility
            markdown.Hide();
            textFlow.Show();
        }

        protected override void PopIn()
        {
            instantMovement |= !IsPresent;
            this.FadeIn(300, Easing.OutQuint);
        }

        protected override void PopOut() => this.Delay(150).FadeOut(300, Easing.OutQuint);

        private void parseAndRenderRichText(string content, TextFlowContainer text)
        {
            string[] lines = content.Split('\n');

            List<List<string>> groups = new List<List<string>>();
            List<bool> isTableGroup = new List<bool>();
            List<string> currentGroup = new List<string>();
            bool? currentIsTable = null;

            foreach (string line in lines)
            {
                int pipeCount = line.Count(c => c == '|');
                bool isTable = pipeCount >= 2;

                currentIsTable ??= isTable;

                if (currentIsTable != isTable && currentGroup.Count > 0)
                {
                    groups.Add(new List<string>(currentGroup));
                    isTableGroup.Add(currentIsTable.Value);
                    currentGroup.Clear();
                    currentIsTable = isTable;
                }

                currentGroup.Add(line);
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
                isTableGroup.Add(currentIsTable ?? false);
            }

            bool firstLine = true;

            for (int g = 0; g < groups.Count; g++)
            {
                if (isTableGroup[g])
                {
                    foreach (string line in groups[g])
                    {
                        if (!firstLine)
                            text.AddText("\n");

                        string[] columns = line.Split('|');
                        renderPipeTableRow(columns, text);
                        firstLine = false;
                    }
                }
                else
                {
                    foreach (string line in groups[g])
                    {
                        if (!firstLine)
                            text.AddText("\n");

                        processMonospaceLine(line, text);
                        firstLine = false;
                    }
                }
            }
        }

        private void renderPipeTableRow(string[] columns, TextFlowContainer text)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (i > 0)
                    addMonospaceTextPreserveSpaces("  ", text);

                string content = columns[i];
                addMonospaceTextPreserveSpaces(content, text);
            }
        }

        private void processMonospaceLine(string line, TextFlowContainer text)
        {
            const string pattern = @"\[MONO\](.*?)\[/MONO\]";
            var matches = Regex.Matches(line, pattern, RegexOptions.Singleline);

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    string normalText = line.Substring(lastIndex, match.Index - lastIndex);
                    addNormalText(normalText, text);
                }

                string monoText = match.Groups[1].Value;
                addMonospaceTextPreserveSpaces(monoText, text);

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < line.Length)
            {
                string remainingText = line.Substring(lastIndex);
                addNormalText(remainingText, text);
            }
        }

        private void addNormalText(string content, TextFlowContainer text)
        {
            text.AddText(content, t => t.Font = OsuFont.GetFont(weight: FontWeight.Regular));
        }

        private void addMonospaceTextPreserveSpaces(string content, TextFlowContainer text)
        {
            foreach (char c in content)
            {
                bool isDigitOrSpace = char.IsDigit(c) || c == ' ';

                text.AddText(c.ToString(), t =>
                {
                    t.Font = OsuFont.GetFont(weight: FontWeight.Regular, fixedWidth: isDigitOrSpace);

                    if (c == ' ')
                    {
                        t.UseFullGlyphHeight = false;
                    }
                });
            }
        }
    }
}
