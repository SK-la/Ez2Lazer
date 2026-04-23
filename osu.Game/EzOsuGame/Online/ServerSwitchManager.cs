// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Online.API;

namespace osu.Game.EzOsuGame.Online
{
    /// <summary>
    /// 管理服务器切换逻辑，包括保存/恢复每个服务器的登录凭证
    /// </summary>
    public partial class ServerSwitchManager : Component
    {
        [Resolved]
        private Ez2ConfigManager ez2Config { get; set; } = null!;

        [Resolved]
        private OsuConfigManager osuConfig { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private Bindable<ServerPreset> serverPreset = null!;
        private ServerPreset? previousPreset;

        [BackgroundDependencyLoader]
        private void load()
        {
            serverPreset = ez2Config.GetBindable<ServerPreset>(Ez2Setting.ServerPreset);
            serverPreset.BindValueChanged(onServerPresetChanged, true);
        }

        private void onServerPresetChanged(ValueChangedEvent<ServerPreset> e)
        {
            // 首次加载时不需要处理
            if (!previousPreset.HasValue)
            {
                previousPreset = e.NewValue;
                loadCredentials(e.NewValue);
                return;
            }

            // 保存当前服务器的登录凭证
            saveCurrentCredentials(previousPreset.Value);

            // 加载新服务器的登录凭证
            loadCredentials(e.NewValue);

            previousPreset = e.NewValue;

            // 提示用户需要重启游戏才能应用服务器更改
            // Logger.Log($"服务器已切换到: {e.NewValue.GetDescription()}，请重启游戏以应用更改", LoggingTarget.Network, LogLevel.Important);

            // 如果API已连接，登出以避免使用错误的服务器
            if (api.State.Value == APIState.Online || api.State.Value == APIState.Connecting)
            {
                api.Logout();
            }
        }

        private void saveCurrentCredentials(ServerPreset preset)
        {
            string username = osuConfig.Get<string>(OsuSetting.Username);
            string token = osuConfig.Get<string>(OsuSetting.Token);

            (Ez2Setting usernameKey, Ez2Setting tokenKey) = getCredentialKeys(preset);

            ez2Config.SetValue(usernameKey, username);
            ez2Config.SetValue(tokenKey, token);
            ez2Config.Save();

            Logger.Log($"已保存 {preset.GetDescription()} 的登录凭证", LoggingTarget.Database);
        }

        private void loadCredentials(ServerPreset preset)
        {
            (Ez2Setting usernameKey, Ez2Setting tokenKey) = getCredentialKeys(preset);

            string username = ez2Config.Get<string>(usernameKey);
            string token = ez2Config.Get<string>(tokenKey);

            // 只有当有保存的凭证时才加载
            if (!string.IsNullOrEmpty(username))
            {
                osuConfig.SetValue(OsuSetting.Username, username);
                osuConfig.SetValue(OsuSetting.Token, token);

                Logger.Log($"已加载 {preset.GetDescription()} 的登录凭证: {username}", LoggingTarget.Database);
            }
            else
            {
                // 清空当前凭证
                osuConfig.SetValue(OsuSetting.Username, string.Empty);
                osuConfig.SetValue(OsuSetting.Token, string.Empty);

                Logger.Log($"{preset.GetDescription()} 没有保存的登录凭证", LoggingTarget.Database);
            }
        }

        private (Ez2Setting usernameKey, Ez2Setting tokenKey) getCredentialKeys(ServerPreset preset)
        {
            return preset switch
            {
                ServerPreset.Gu => (Ez2Setting.ServerGuUsername, Ez2Setting.ServerGuToken),
                ServerPreset.Manual => (Ez2Setting.ServerManualUsername, Ez2Setting.ServerManualToken),
                _ => (Ez2Setting.ServerOfficialUsername, Ez2Setting.ServerOfficialToken)
            };
        }
    }
}
