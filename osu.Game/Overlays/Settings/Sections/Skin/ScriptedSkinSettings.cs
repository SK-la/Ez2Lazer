// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Skinning;

namespace osu.Game.Overlays.Settings.Sections.Skin
{
    public partial class ScriptedSkinSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzEditorStrings.GROUP_SCRIPTED_SKIN;

        [Resolved]
        private SkinManager skins { get; set; }

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; }

        /// <summary>
        /// 脚本皮肤开关。刻意不使用 <c>ezConfig.GetBindable</c>：启动期只读一次，改动直接写回 ini。
        /// </summary>
        private Bindable<bool> enableScriptedSkinsBindable;

        [BackgroundDependencyLoader]
        private void load()
        {
            enableScriptedSkinsBindable = new Bindable<bool>(ezConfig.Get<bool>(Ez2Setting.EnableScriptedSkins));

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzEditorStrings.SETTINGS_ENABLE_SCRIPTED_SKINS,
                    Current = enableScriptedSkinsBindable,
                })
                {
                    Note = { Value = new SettingsNote.Data(EzEditorStrings.SETTINGS_ENABLE_SCRIPTED_SKINS_NOTE, SettingsNote.Type.Informational) },
                },
                new SettingsButtonV2
                {
                    Text = EzEditorStrings.SETTINGS_RELOAD_SCRIPTED_SKINS,
                    TooltipText = EzEditorStrings.SETTINGS_RELOAD_SCRIPTED_SKINS_TOOLTIP,
                    Action = reloadScriptedSkins,
                    Enabled = { Value = skins.ScriptedSkinsEnabled },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enableScriptedSkinsBindable.BindValueChanged(onEnableScriptedSkinsChanged);
        }

        /// <summary>
        /// 写回 ini 即可（保存走 100ms 去抖，不作任何 <c>Load()</c>，避免本项被磁盘值顶回）。
        /// 该开关的生效时机是启动期，见 <see cref="SkinManager.ScriptedSkinsEnabled"/>。
        /// </summary>
        private void onEnableScriptedSkinsChanged(ValueChangedEvent<bool> change)
        {
            if (ezConfig.Get<bool>(Ez2Setting.EnableScriptedSkins) != change.NewValue)
                ezConfig.SetValue(Ez2Setting.EnableScriptedSkins, change.NewValue);
        }

        private void reloadScriptedSkins()
        {
            _ = Task.Run(async () =>
            {
                int reloaded = await skins.ReloadAllScriptedSkins().ConfigureAwait(false);
                Logger.Log($"Scripted skins reloaded: {reloaded}", LoggingTarget.Information);
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            enableScriptedSkinsBindable?.UnbindAll();
        }
    }
}
