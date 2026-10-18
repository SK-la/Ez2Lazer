// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.Overlays.Settings;

namespace osu.Game.Tests.Visual.Edit
{
    /// <summary>
    /// Walks every setting exposed by the skin editor sidebar in visual order and moves each one to a different value.
    /// Items are re-collected on every step because some sections rebuild their controls (for example per-key skin.ini colours).
    /// </summary>
    internal sealed class EzSkinEditorSceneInteractionDriver
    {
        private const string string_marker = " [test]";

        private readonly EzSkinEditorScreen screen;
        private readonly List<string> visited = new List<string>();

        private int position;

        public EzSkinEditorSceneInteractionDriver(EzSkinEditorScreen screen)
        {
            this.screen = screen;
        }

        public int Count => collectItems().Count;

        public bool IsComplete => position >= Count;

        public IReadOnlyList<string> Visited => visited;

        /// <summary>
        /// Moves the next unvisited setting to a different value.
        /// </summary>
        /// <returns>Whether a setting was changed.</returns>
        public bool CycleNext()
        {
            var current = collectItems();

            while (position < current.Count)
            {
                Drawable item = current[position];
                position++;

                if (!tryChange(item))
                    continue;

                visited.Add(getLabel(item));
                return true;
            }

            return false;
        }

        private List<Drawable> collectItems() =>
            screen.ChildrenOfType<Drawable>()
                  .Where(d => d is ISettingsItem && d.IsLoaded && isInsideSettingsGroup(d))
                  .OrderBy(d => d.ScreenSpaceDrawQuad.TopLeft.Y)
                  .ThenBy(d => d.ScreenSpaceDrawQuad.TopLeft.X)
                  .ToList();

        private static bool isInsideSettingsGroup(Drawable drawable)
        {
            for (var parent = drawable.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is EzSkinEditorSettingsGroup)
                    return true;
            }

            return false;
        }

        private static string getLabel(Drawable item)
        {
            if (item.GetType().GetProperty("LabelText")?.GetValue(item) is { } label)
            {
                string text = label.ToString() ?? string.Empty;

                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return item.GetType().Name;
        }

        private static bool tryChange(Drawable item)
        {
            var currentProperty = item.GetType().GetProperty("Current");

            if (currentProperty?.GetValue(item) is not IBindable bindable || bindable.Disabled)
                return false;

            var valueProperty = bindable.GetType().GetProperty("Value");

            if (valueProperty == null)
                return false;

            object? currentValue = valueProperty.GetValue(bindable);

            if (!tryBuildValue(item, bindable, valueProperty, currentValue, out object? newValue))
                return false;

            if (EqualityComparer<object?>.Default.Equals(currentValue, newValue))
                return false;

            var parse = bindable.GetType().GetMethod("Parse", new[] { typeof(object), typeof(IFormatProvider) });

            if (parse == null)
                return false;

            parse.Invoke(bindable, new[] { newValue, CultureInfo.InvariantCulture });
            return true;
        }

        private static bool tryBuildValue(Drawable item, IBindable bindable, PropertyInfo valueProperty, object? currentValue, out object? newValue)
        {
            newValue = null;

            if (currentValue is bool toggle)
            {
                newValue = !toggle;
                return true;
            }

            if (currentValue is string text)
            {
                string[] dropdownItems = getDropdownItems(item);

                if (dropdownItems.Length > 1)
                {
                    int index = Array.IndexOf(dropdownItems, text);
                    newValue = dropdownItems[(index + 1) % dropdownItems.Length];
                }
                else
                {
                    newValue = text.Contains(string_marker, StringComparison.Ordinal)
                        ? text.Replace(string_marker, string.Empty, StringComparison.Ordinal)
                        : text + string_marker;
                }

                return true;
            }

            Type valueType = valueProperty.PropertyType;

            if (valueType.IsEnum)
            {
                var values = Enum.GetValues(valueType);

                for (int i = 0; i < values.Length; i++)
                {
                    if (!EqualityComparer<object?>.Default.Equals(values.GetValue(i), currentValue))
                        continue;

                    newValue = values.GetValue((i + 1) % values.Length);
                    return true;
                }

                newValue = values.GetValue(0);
                return true;
            }

            if (valueType == typeof(Colour4))
            {
                newValue = new Colour4(0.72f, 0.36f, 0.91f, 1f);
                return true;
            }

            if (isNumeric(valueType))
            {
                newValue = nextNumber(bindable, valueType, currentValue);
                return true;
            }

            return false;
        }

        private static object nextNumber(IBindable bindable, Type valueType, object? currentValue)
        {
            double current = Convert.ToDouble(currentValue, CultureInfo.InvariantCulture);
            double? min = readNumber(bindable, "MinValue");
            double? max = readNumber(bindable, "MaxValue");

            double next;

            if (min is { } lo && max is { } hi && double.IsFinite(lo) && double.IsFinite(hi) && hi > lo && hi - lo <= 1e7)
            {
                double step = (hi - lo) / 4;

                next = current + step;

                if (next >= hi - step * 0.5)
                    next = lo + step;
            }
            else
            {
                next = current == 0 ? 1 : current + Math.Max(1, Math.Abs(current) * 0.1);
            }

            if (valueType == typeof(int))
                return (int)Math.Round(next);

            if (valueType == typeof(float))
                return (float)next;

            if (valueType == typeof(long))
                return (long)Math.Round(next);

            return next;
        }

        private static double? readNumber(IBindable bindable, string propertyName)
        {
            object? value = bindable.GetType().GetProperty(propertyName)?.GetValue(bindable);

            return value == null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static bool isNumeric(Type type) =>
            type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(long);

        private static string[] getDropdownItems(Drawable item)
        {
            if (item.GetType().GetProperty("Items")?.GetValue(item) is not IEnumerable enumerable)
                return Array.Empty<string>();

            return enumerable.Cast<object>()
                             .Select(o => o?.ToString() ?? string.Empty)
                             .Where(s => s.Length > 0)
                             .ToArray();
        }
    }
}
