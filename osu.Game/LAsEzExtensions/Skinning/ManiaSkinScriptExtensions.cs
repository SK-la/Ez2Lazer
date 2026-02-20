using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Objects;

namespace osu.Game.LAsEzExtensions.Skinning
{
    [MoonSharpUserData]
    public class ManiaSkinScriptExtensions
    {
        private readonly ISkinScriptHost host;

        public ManiaSkinScriptExtensions(ISkinScriptHost host)
        {
            this.host = host;
        }

        [MoonSharpVisible(true)]
        public int GetColumnCount()
        {
            if (!isManiaRuleset())
                return 0;

            int maxColumn = -1;

            foreach (HitObject hitObject in enumerateHitObjects(host.CurrentBeatmap))
            {
                int? column = readColumn(hitObject);
                if (column != null)
                    maxColumn = Math.Max(maxColumn, column.Value);
            }

            return maxColumn + 1;
        }

        [MoonSharpVisible(true)]
        public string GetColumnBinding(int column) => $"Column{column + 1}";

        [MoonSharpVisible(true)]
        public float GetColumnWidth(int column)
        {
            int count = GetColumnCount();
            return count <= 0 ? 0 : 1f / count;
        }

        [MoonSharpVisible(true)]
        public int GetNoteColumn(object note)
        {
            int? column = readColumn(note);
            return column ?? -1;
        }

        private bool isManiaRuleset() => string.Equals(host.CurrentRuleset?.Name, "mania", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<HitObject> enumerateHitObjects(dynamic? beatmap)
        {
            if (beatmap?.HitObjects == null)
                yield break;

            var stack = new Stack<HitObject>();

            foreach (HitObject hitObject in beatmap.HitObjects)
                stack.Push(hitObject);

            while (stack.Count > 0)
            {
                HitObject current = stack.Pop();
                yield return current;

                foreach (HitObject nested in current.NestedHitObjects)
                    stack.Push(nested);
            }
        }

        private static int? readColumn(object obj)
        {
            var property = obj.GetType().GetProperty("Column");
            if (property == null)
                return null;

            object? raw = property.GetValue(obj);
            if (raw == null)
                return null;

            return raw switch
            {
                int i => i,
                IConvertible convertible => Convert.ToInt32(convertible),
                _ => null,
            };
        }
    }
}
