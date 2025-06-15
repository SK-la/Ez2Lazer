// VideoBackgroundScreen.cs

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Video;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;

namespace osu.Game.Screens.LAsEzExtensions
{
    public partial class VideoBackgroundScreen : Background
    {
        private readonly string videoPath;

        public VideoBackgroundScreen(string videoPath)
        {
            this.videoPath = videoPath;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, EzSkinSettingsManager ezSkinConfig)
        {
            var video = new Video(videoPath)
            {
                RelativeSizeAxes = Axes.Both,
                Loop = true,
            };

            video.FillMode = FillMode.Fill;
            video.FillAspectRatio = 1.0f * video.DrawSize.X / video.DrawSize.Y;

            AddInternal(video);

            //下面只用于注册全局设置
            GlobalConfigStore.Config = config;
            GlobalConfigStore.EzConfig = ezSkinConfig;
            GlobalConfigStore.RegisterConfigListeners();
        }
    }

public static class GlobalConfigStore
{
    private static OsuConfigManager? config;
    private static EzSkinSettingsManager? ezConfig;
    private static bool isRegistered;

    public static OsuConfigManager? Config
    {
        get => config;
        set
        {
            if (config != null && isRegistered)
                UnregisterConfigListeners();

            config = value;

            if (config != null && ezConfig != null)
                RegisterConfigListeners();
        }
    }

    public static EzSkinSettingsManager? EzConfig
    {
        get => ezConfig;
        set
        {
            if (ezConfig != null && isRegistered)
                UnregisterConfigListeners();

            ezConfig = value;

            if (config != null && ezConfig != null)
                RegisterConfigListeners();
        }
    }

    public static event Action? OnRefresh;

    public static void TriggerRefresh() => OnRefresh?.Invoke();

    public static void RegisterConfigListeners()
    {
        if (config == null || ezConfig == null) return;

        ezConfig.GetBindable<double>(EzSkinSetting.ColumnWidth).ValueChanged += OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.SpecialFactor).ValueChanged += OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.HitPosition).ValueChanged += OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.VisualHitPosition).ValueChanged += OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).ValueChanged += OnConfigValueChanged;

        isRegistered = true;
        // Console.WriteLine("GlobalConfigStore: 已注册所有配置监听");
    }

    public static void UnregisterConfigListeners()
    {
        if (ezConfig == null) return;

        // 取消所有订阅
        ezConfig.GetBindable<double>(EzSkinSetting.ColumnWidth).ValueChanged -= OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.SpecialFactor).ValueChanged -= OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.HitPosition).ValueChanged -= OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.VisualHitPosition).ValueChanged -= OnConfigValueChanged;
        ezConfig.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).ValueChanged -= OnConfigValueChanged;

        isRegistered = false;
        // Console.WriteLine("GlobalConfigStore: 已取消所有配置监听");
    }

    private static void OnConfigValueChanged(ValueChangedEvent<double> _)
    {
        // Console.WriteLine("GlobalConfigStore: 配置值已更改，正在触发刷新");
        TriggerRefresh();
    }
}

    public partial class StreamVideoBackgroundScreen : Background
    {
        private readonly Stream videoStream;

        public StreamVideoBackgroundScreen(Stream videoStream)
        {
            this.videoStream = videoStream;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var video = new Video(videoStream)
            {
                RelativeSizeAxes = Axes.Both,
                Loop = true
            };
            AddInternal(video);
        }
    }
}
