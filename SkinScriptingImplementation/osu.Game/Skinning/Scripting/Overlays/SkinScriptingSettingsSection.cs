using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Skinning.Scripting.Overlays
{
    public class SkinScriptingSettingsSection : SettingsSection
    {
        protected override string Header => "皮肤脚本";

        [Resolved]
        private SkinManager skinManager { get; set; }

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; }

        [Resolved]
        private GameHost host { get; set; }

        [Resolved]
        private Storage storage { get; set; }

        [Resolved(CanBeNull = true)]
        private SkinScriptingConfig scriptConfig { get; set; }

        private readonly Bindable<bool> scriptingEnabled = new Bindable<bool>(true);
        private readonly BindableList<string> allowedScripts = new BindableList<string>();
        private readonly BindableList<string> blockedScripts = new BindableList<string>();

        private FillFlowContainer scriptListFlow;
        private OsuButton importButton;

        [BackgroundDependencyLoader]        private void load()
        {
            if (scriptConfig != null)
            {
                scriptConfig.BindWith(SkinScriptingSettings.ScriptingEnabled, scriptingEnabled);
                scriptConfig.BindWith(SkinScriptingSettings.AllowedScripts, allowedScripts);
                scriptConfig.BindWith(SkinScriptingSettings.BlockedScripts, blockedScripts);
            }

            Children = new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = "启用皮肤脚本",
                    TooltipText = "允许皮肤使用Lua脚本来自定义外观和行为",
                    Current = scriptingEnabled
                },
                new SettingsButton
                {
                    Text = "从文件导入脚本",
                    Action = ImportScriptFromFile
                },
                new OsuSpriteText
                {
                    Text = "可用脚本",
                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                    Margin = new MarginPadding { Top = 20, Bottom = 10 }
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 300,
                    Child = scriptListFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5)
                    }
                }
            };

            // 监听皮肤变化
            skinManager.CurrentSkin.BindValueChanged(_ => RefreshScriptList(), true);
        }

        private void RefreshScriptList()
        {
            scriptListFlow.Clear();

            if (skinManager.CurrentSkin.Value is LegacySkin skin)
            {
                var scripts = skin.Scripts;
                if (scripts.Count == 0)
                {
                    scriptListFlow.Add(new OsuSpriteText
                    {
                        Text = "当前皮肤没有可用的脚本",
                        Font = OsuFont.GetFont(size: 16),
                        Colour = Colours.Gray9
                    });
                }
                else
                {
                    foreach (var script in scripts)
                    {
                        scriptListFlow.Add(new ScriptListItem(script, allowedScripts, blockedScripts));
                    }
                }
            }
            else
            {
                scriptListFlow.Add(new OsuSpriteText
                {
                    Text = "当前皮肤不支持脚本",
                    Font = OsuFont.GetFont(size: 16),
                    Colour = Colours.Gray9
                });
            }
        }

        private void ImportScriptFromFile()
        {
            Task.Run(async () =>
            {
                try
                {
                    string[] paths = await host.PickFilesAsync(new FilePickerOptions
                    {
                        Title = "选择Lua脚本文件",
                        FileTypes = new[] { ".lua" }
                    }).ConfigureAwait(false);

                    if (paths == null || paths.Length == 0)
                        return;

                    Schedule(() =>
                    {
                        foreach (string path in paths)
                        {
                            try
                            {
                                // 获取目标路径（当前皮肤文件夹）
                                if (skinManager.CurrentSkin.Value is not LegacySkin skin || skin.SkinInfo.Files == null)
                                {
                                    dialogOverlay.Push(new FileImportFaultDialog("当前皮肤不支持脚本导入"));
                                    return;
                                }

                                string fileName = Path.GetFileName(path);
                                string destPath = Path.Combine(storage.GetFullPath($"skins/{skin.SkinInfo.ID}/{fileName}"));

                                // 复制文件
                                File.Copy(path, destPath, true);

                                // 刷新皮肤（重新加载脚本）
                                skinManager.RefreshCurrentSkin();

                                // 刷新脚本列表
                                RefreshScriptList();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, $"导入脚本失败: {ex.Message}");
                                dialogOverlay.Push(new FileImportFaultDialog(ex.Message));
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "选择脚本文件失败");
                    Schedule(() => dialogOverlay.Push(new FileImportFaultDialog(ex.Message)));
                }
            });
        }

        private class ScriptListItem : CompositeDrawable
        {
            private readonly SkinScript script;
            private readonly BindableList<string> allowedScripts;
            private readonly BindableList<string> blockedScripts;

            private readonly BindableBool isEnabled = new BindableBool(true);

            public ScriptListItem(SkinScript script, BindableList<string> allowedScripts, BindableList<string> blockedScripts)
            {
                this.script = script;
                this.allowedScripts = allowedScripts;
                this.blockedScripts = blockedScripts;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                // 根据配置设置初始状态
                string scriptName = script.ScriptName;
                if (blockedScripts.Contains(scriptName))
                    isEnabled.Value = false;
                else if (allowedScripts.Count > 0 && !allowedScripts.Contains(scriptName))
                    isEnabled.Value = false;
                else
                    isEnabled.Value = true;

                isEnabled.ValueChanged += e =>
                {
                    if (e.NewValue)
                    {
                        // 启用脚本
                        blockedScripts.Remove(scriptName);
                        if (allowedScripts.Count > 0)
                            allowedScripts.Add(scriptName);
                    }
                    else
                    {
                        // 禁用脚本
                        blockedScripts.Add(scriptName);
                        allowedScripts.Remove(scriptName);
                    }
                };
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 10, Vertical = 5 },
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(10, 0),
                                Children = new Drawable[]
                                {
                                    new OsuCheckbox
                                    {
                                        Current = isEnabled,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                        Width = 0.9f,
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Text = script.ScriptName,
                                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold)
                                            },
                                            new OsuSpriteText
                                            {
                                                Text = $"脚本描述: {script.Description ?? "无描述"}",
                                                Font = OsuFont.GetFont(size: 14),
                                                Colour = colours.Gray9
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.FadeColour(Colour4.LightGray, 200);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeColour(Colour4.White, 200);
                base.OnHoverLost(e);
            }
        }
    }
}
