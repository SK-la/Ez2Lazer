// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Pets;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzPetSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsDesktopPet.DESKTOP_PET_SETTINGS_HEADER;

        private EzPetPackLoader loader = null!;
        private OsuSpriteText packStatusText = null!;
        private FormDropdown<string> packDropdown = null!;
        private Bindable<string> packBindable = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, Storage storage)
        {
            loader = new EzPetPackLoader(storage);
            packBindable = ezConfig.GetBindable<string>(Ez2Setting.DesktopPetPack);

            // The pack list needs a directory scan and the status row reads pet.json from disk. Both are
            // deferred to LoadComplete so the settings preload window does not touch the filesystem.
            // Seed the dropdown with just the bound value so the current selection still renders.
            AddRange(new[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_ENABLED,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetEnabled),
                })
                {
                    Keywords = new[] { "pet", "desktop", "mascot", "桌宠" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_MENU,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_MENU_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnMenu),
                })
                {
                    Keywords = new[] { "pet", "menu", "桌宠", "主菜单" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_SONG_SELECT,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_SONG_SELECT_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnSongSelect),
                })
                {
                    Keywords = new[] { "pet", "song select", "桌宠", "选歌" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_GAMEPLAY,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_GAMEPLAY_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnGameplay),
                })
                {
                    Keywords = new[] { "pet", "gameplay", "桌宠", "游玩" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_RESULTS,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_SHOW_ON_RESULTS_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetShowOnResults),
                })
                {
                    Keywords = new[] { "pet", "results", "桌宠", "结算" }
                },
                new SettingsItemV2(packDropdown = new FormDropdown<string>
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_PACK,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_PACK_TOOLTIP,
                    Current = packBindable,
                    Items = initialPackItems(),
                })
                {
                    Keywords = new[] { "pet", "pack", "mascot", "桌宠", "live2d" }
                },
                createPackStatusRow(),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_SCALE,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_SCALE_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.DesktopPetScale),
                    KeyboardStep = 0.05f,
                })
                {
                    Keywords = new[] { "pet", "scale", "size", "桌宠" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsDesktopPet.DESKTOP_PET_LIP_SYNC,
                    HintText = EzSettingsDesktopPet.DESKTOP_PET_LIP_SYNC_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetLive2DLipSync),
                })
                {
                    Keywords = new[] { "pet", "live2d", "music", "bpm", "关联", "晃头", "桌宠" }
                },
            });

            packBindable.BindValueChanged(_ => refreshPackStatus(packBindable.Value));
        }

        private List<string> initialPackItems()
        {
            // Only what is needed to render the current selection; the full list is filled in on first display.
            var items = new List<string>();

            if (!string.IsNullOrEmpty(packBindable.Value))
                items.Add(packBindable.Value);
            else
                items.Add(EzDefaultPetPack.NAME);

            return items;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Scheduler.AddOnce(populatePackList);
        }

        private void populatePackList()
        {
            var packNames = new List<string>(loader.ListPackNames());

            if (!string.IsNullOrEmpty(packBindable.Value) && !packNames.Contains(packBindable.Value))
                packNames.Insert(0, packBindable.Value);

            if (packNames.Count == 0)
                packNames.Add(EzDefaultPetPack.NAME);

            packDropdown.Items = packNames;
            refreshPackStatus(packBindable.Value);
        }

        private Drawable createPackStatusRow()
        {
            packStatusText = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Font = OsuFont.GetFont(size: 12),
                Colour = Colour4.Gray,
                Text = string.Empty,
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = packStatusText,
            };
        }

        private void refreshPackStatus(string? packName)
        {
            var pack = loader.Load(string.IsNullOrWhiteSpace(packName) ? EzDefaultPetPack.NAME : packName);
            bool corePresent = EzPetLive2DAccess.HasCubismCoreOnDisk(loader.PetsStorage);

            if (pack == null)
            {
                packStatusText.Text = EzSettingsDesktopPet.DESKTOP_PET_STATUS_MISSING_PACK;
                return;
            }

            if (pack.Live2DAuthorized)
            {
                if (corePresent)
                {
                    packStatusText.Text = EzSettingsDesktopPet.DESKTOP_PET_STATUS_LIVE2D_READY;
                }
                else
                {
                    packStatusText.Text =
                        $"{EzSettingsDesktopPet.DESKTOP_PET_STATUS_LIVE2D_MISSING_CORE}\nEzResources/Pets/{EzPetCubismNative.GetExpectedCoreRelativePath()}";
                }

                return;
            }

            if (EzPetLive2DAccess.ParseRenderer(pack.Definition.Renderer) == EzPetRendererKind.Live2D)
            {
                packStatusText.Text = EzSettingsDesktopPet.DESKTOP_PET_STATUS_LIVE2D_MISSING_MODEL;
                return;
            }

            packStatusText.Text = pack.HasRasterFrames
                ? EzSettingsDesktopPet.DESKTOP_PET_STATUS_PNG_OK
                : EzSettingsDesktopPet.DESKTOP_PET_STATUS_EMPTY;
        }
    }
}
