// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Screens
{
    public static class EzConfig
    {
        public static Bindable<float> GetNoteSize(this EzSkinSettingsManager setting, int keyMode, int columnIndex, int x = 0)
        {
            var result = new Bindable<float>();

            var columnWidthBindable = setting.GetBindable<double>(EzSkinSetting.ColumnWidth);
            var specialFactorBindable = setting.GetBindable<double>(EzSkinSetting.SpecialFactor);
            var heightScaleBindable = setting.GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth);

            void updateNoteSize()
            {
                bool isSpecialColumn = setting.GetColumnType(keyMode, columnIndex) == "S";
                double baseWidth = columnWidthBindable.Value;
                double specialFactor = specialFactorBindable.Value;

                if (x != 0)
                {
                    result.Value = (float)(baseWidth * (isSpecialColumn ? specialFactor : 1.0));
                    return;
                }

                double heightScale = heightScaleBindable.Value;
                double columnWidth = baseWidth * (isSpecialColumn ? specialFactor : 1.0);
                result.Value = (float)(columnWidth * heightScale);
            }

            columnWidthBindable.BindValueChanged(e => {
                System.Diagnostics.Debug.WriteLine($"ColumnWidth changed: {e.NewValue}");
                updateNoteSize();
            });
            specialFactorBindable.BindValueChanged(_ => updateNoteSize());

            if (x == 0)
                heightScaleBindable.BindValueChanged(_ => updateNoteSize());

            updateNoteSize();

            return result;
        }
    }
}
