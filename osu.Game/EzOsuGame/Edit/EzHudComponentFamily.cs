// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Visual family bucket for skinnable HUD components listed in the Ez skin editor library.
    /// </summary>
    public enum EzHudComponentFamily
    {
        Ez,
        Argon,
        Legacy,
        Other,
    }

    /// <summary>
    /// Overrides <see cref="EzHudComponentFamilyResolver"/> for a single component type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EzHudComponentFamilyAttribute : Attribute
    {
        public EzHudComponentFamily Family { get; }

        public EzHudComponentFamilyAttribute(EzHudComponentFamily family) => Family = family;
    }

    public static class EzHudComponentFamilyResolver
    {
        public static EzHudComponentFamily Resolve(Type type)
        {
            var attribute = type.GetCustomAttribute<EzHudComponentFamilyAttribute>();

            if (attribute != null)
                return attribute.Family;

            string name = type.Name;
            string ns = type.Namespace ?? string.Empty;

            if (name.StartsWith("Legacy", StringComparison.Ordinal) || ns.Contains(".Legacy.", StringComparison.Ordinal))
                return EzHudComponentFamily.Legacy;

            if (name.StartsWith("Argon", StringComparison.Ordinal) || ns.Contains(".Argon.", StringComparison.Ordinal))
                return EzHudComponentFamily.Argon;

            if (isEzOrigin(name, ns))
                return EzHudComponentFamily.Ez;

            return EzHudComponentFamily.Other;
        }

        private static bool isEzOrigin(string name, string ns)
        {
            if (ns.Contains("osu.Game.EzOsuGame", StringComparison.Ordinal))
                return true;

            if (ns.Contains("Rulesets.", StringComparison.Ordinal) && ns.Contains(".Ez", StringComparison.Ordinal))
                return true;

            return name.StartsWith("Ez", StringComparison.Ordinal);
        }
    }
}
