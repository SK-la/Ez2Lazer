// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.EzMania
{
    public class HitWindowTemplate
    {
        public double TemplatePerfect { get; set; }
        public double TemplateGreat { get; set; }
        public double TemplateGood { get; set; }
        public double TemplateOk { get; set; }
        public double TemplateMeh { get; set; }
        public double TemplateMiss { get; set; }
    }

    public static class HitWindowTemplateDictionary
    {
        private static readonly Dictionary<string, HitWindowTemplate> templates = new Dictionary<string, HitWindowTemplate>
        {
            ["EZ2AC"] = new HitWindowTemplate
            {
                TemplatePerfect = 22,
                TemplateGreat = 32,
                TemplateGood = 64,
                TemplateOk = 80,
                TemplateMeh = 100,
                TemplateMiss = 120
            },
            ["IIDX"] = new HitWindowTemplate
            {
                TemplatePerfect = 20,
                TemplateGreat = 40,
                TemplateGood = 60,
                TemplateOk = 80,
                TemplateMeh = 100,
                TemplateMiss = 120
            },
            ["Melody"] = new HitWindowTemplate
            {
                TemplatePerfect = 20,
                TemplateGreat = 40,
                TemplateGood = 60,
                TemplateOk = 80,
                TemplateMeh = 100,
                TemplateMiss = 120
            }
        };

        public static HitWindowTemplate GetTemplate(string mode)
        {
            return templates.TryGetValue(mode, out var template)
                ? template
                : throw new InvalidOperationException($"Hit window template for mode '{mode}' is not defined.");
        }

        public static readonly HitWindowTemplate EASY = new HitWindowTemplate
        {
            TemplatePerfect = 50,
            TemplateGreat = 100,
            TemplateGood = 150,
            TemplateOk = 200,
            TemplateMeh = 250,
            TemplateMiss = 300
        };

        public static readonly HitWindowTemplate HARD = new HitWindowTemplate
        {
            TemplatePerfect = 20,
            TemplateGreat = 40,
            TemplateGood = 60,
            TemplateOk = 80,
            TemplateMeh = 100,
            TemplateMiss = 120
        };
    }
}
