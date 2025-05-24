// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;

namespace osu.Game.Skinning.Components
{
    public partial class EzSelectorEnumList : SettingsDropdown<EzSelectorNameSet>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            Items = Enum.GetValues(typeof(EzSelectorNameSet)).Cast<EzSelectorNameSet>().ToList();
        }
    }

    public partial class AnchorDropdown : SettingsDropdown<Anchor>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 限制选项范围
            Items = new List<Anchor>
            {
                Anchor.TopCentre,
                Anchor.Centre,
                Anchor.BottomCentre
            };
        }
    }

    public enum EffectType
    {
        Scale,
        Bounce,
        None
    }

    public enum EzSelectorNameSet
    {
        // ReSharper disable InconsistentNaming
        EZ2DJ_1st,
        EZ2DJ_1stSE,
        EZ2DJ_2nd,
        EZ2DJ_3rd,
        EZ2DJ_4th,
        EZ2DJ_6th,
        EZ2DJ_7th,
        AIR,
        AZURE_EXPRESSION,
        CV_CRAFT,
        D2D_Station,
        Dark_Concert,
        DJMAX,
        EC_1304,
        EC_Wheel,
        EVOLVE,
        EZ2ON,
        FIND_A_WAY,
        Fortress2,
        Fortress3_Future,
        Fortress3_Gear,
        Fortress3_Green,
        Fortress3_Modern,
        GC,
        GC_EZ,
        Gem,
        HX_1121,
        HX_STANDARD,
        JIYU,
        Kings,
        Limited,
        NIGHT_FALL,
        O2_A9100,
        O2_EA05,
        O2_Jam,
        Platinum,
        QTZ_01,
        QTZ_02,
        REBOOT,
        SG_701,
        SH_512,
        Star,
        TANOc,
        TANOc2,
        TECHNIKA,
        TIME_TRAVELER,
        TOMATO,
        Turtle,
        Various_Ways,
        ArcadeScore,
        // ReSharper restore InconsistentNaming
    }

    public partial class EzSelector
    {
        /// <summary>
        /// 子文件夹路径，默认为Gameplay/Fonts/
        /// </summary>
        protected virtual string SetPath => @"Gameplay/Fonts/";

        /// <summary>
        /// 当前选择的子文件夹
        /// </summary>
        public Bindable<string> Selected { get; } = new Bindable<string>();

        private Dropdown<string> dropdown = null!;

        /// <summary>
        /// 存储服务，用于访问文件系统
        /// </summary>
        [Resolved]
        protected Storage Storage { get; private set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // 加载子文件夹并创建下拉列表
            LoadSubfoldersToDropdown();
        }

        /// <summary>
        /// 加载指定路径下的所有子文件夹到下拉列表
        /// </summary>
        protected void LoadSubfoldersToDropdown()
        {
            // 确保路径存在
            EnsureDirectoryExists(SetPath);

            // 获取所有子文件夹
            List<string> subfolders = GetSubfolders(SetPath);

            // 创建下拉列表
            InternalChild = dropdown = new OsuDropdown<string>
            {
                RelativeSizeAxes = Axes.X,
                Items = subfolders
            };

            dropdown.Current.BindTo(Selected);

            // 默认选择第一个子文件夹，如果有的话
            if (subfolders.Count > 0)
                Selected.Value = subfolders[0];
        }

        /// <summary>
        /// 确保目录存在，不存在则创建
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        /// <returns>完整路径</returns>
        protected string EnsureDirectoryExists(string relativePath)
        {
            try
            {
                string fullPath = Storage.GetFullPath(relativePath);

                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Logger.Log($"已创建目录: {fullPath}");
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"创建目录 {relativePath} 失败");
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取指定路径下的所有子文件夹
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        /// <returns>子文件夹名称列表</returns>
        protected List<string> GetSubfolders(string relativePath)
        {
            List<string> result = new List<string>();

            try
            {
                string fullPath = Storage.GetFullPath(relativePath);

                // 获取所有子文件夹
                string[] directories = Directory.GetDirectories(fullPath);

                // 提取文件夹名称
                foreach (string dir in directories)
                {
                    string dirName = Path.GetFileName(dir);
                    if (!string.IsNullOrEmpty(dirName))
                        result.Add(dirName);
                }

                Logger.Log($"在 {fullPath} 中找到 {result.Count} 个子文件夹");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"获取 {relativePath} 的子文件夹时出错");
            }

            // 如果没有找到子文件夹，添加一个默认选项
            if (result.Count == 0)
                result.Add("Default");

            return result;
        }

        public Dropdown<string> InternalChild { get; set; } = null!;
    }

    /// <summary>
    /// 专门用于加载Note套图的选择器
    /// </summary>
    public partial class NoteSetSelector : EzSelector
    {
        /// <summary>
        /// 重写路径指向note套图目录
        /// </summary>
        protected override string SetPath => @"Resource\Textures\note";
    }
}
