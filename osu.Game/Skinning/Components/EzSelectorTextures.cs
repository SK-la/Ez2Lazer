// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK.Graphics;

namespace osu.Game.Skinning.Components
{
    //TODO 代码不对, 无法加载, 用于缩略图选择纹理
    public partial class EzSelectorTextures : SettingsItem<string>
    {
        [Resolved]
        private Storage storage { get; set; } = null!;

        private List<string> availableThemes = new List<string>();

        public EzSelectorTextures()
        {
            Current = new Bindable<string>();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            availableThemes = loadAvailableThemes();
            if (availableThemes.Count > 0)
                Current.Value = availableThemes[0];
        }

        protected override Drawable CreateControl()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = createPreviewItems().ToList()
                }
            };
        }

        private IEnumerable<Drawable> createPreviewItems()
        {
            foreach (string value in availableThemes)
            {
                yield return new PreviewContainer
                {
                    Value = value,
                    Selected = value == Current.Value,
                    Action = () => Current.Value = value
                };
            }
        }

        private List<string> loadAvailableThemes()
        {
            var themes = new List<string>();

            try
            {
                string gameThemePath = storage.GetFullPath("EzResources/GameTheme");

                if (Directory.Exists(gameThemePath))
                {
                    string[] directories = Directory.GetDirectories(gameThemePath);
                    themes.AddRange(directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load GameTheme folders: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }

            return themes;
        }

        private partial class PreviewContainer : Container
        {
            public string Value { get; set; } = string.Empty;
            public Action? Action { get; set; }

            private Box? background;
            private bool selected;

            public bool Selected
            {
                set
                {
                    selected = value;
                    background?.FadeTo(selected ? 0.4f : 0f, 200, Easing.OutQuint);
                }
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                Height = 90;
                Margin = new MarginPadding { Bottom = 5 };
                Masking = true;
                CornerRadius = 5;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.1f),
                        Alpha = 0
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(10),
                        Child = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension()
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = Value.ToString(),
                                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold)
                                    }
                                },
                                new[] { createPreview() }
                            }
                        }
                    }
                };
            }

            private Drawable createPreview()
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 5,
                    Children = new[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black
                        },
                        // 这里添加实际的预览内容
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(0.2f, 200, Easing.OutQuint);
                this.ScaleTo(1.02f, 200, Easing.OutQuint);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeTo(selected ? 0.4f : 0f, 200, Easing.OutQuint);
                this.ScaleTo(1f, 200, Easing.OutQuint);
            }

            protected override bool OnClick(ClickEvent e)
            {
                Action?.Invoke();
                return true;
            }
        }
    }
}
