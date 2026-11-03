// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Serialises a <see cref="Type"/> as an assembly-qualified name without assembly metadata, and resolves it back the same way.
    /// </summary>
    /// <remarks>
    /// Json.NET 解析类型走 <see cref="Type.GetType(string)"/>，而皮肤布局里存的是写入时的程序集版本。用户的皮肤文件跨版本沿用
    /// （Release 存 Version=1.0.0.0，Debug 程序集却是 0.0.0.0），请求的版本高于已加载的版本时解析必然失败，整个布局会被丢弃。
    /// 这里读写都去掉 <c>Version</c> / <c>Culture</c> / <c>PublicKeyToken</c>，让布局只绑定类型与程序集简单名。
    /// </remarks>
    public class TypeNameJsonConverter : JsonConverter
    {
        private static readonly Regex assembly_metadata = new Regex(@",\s*(Version|Culture|PublicKeyToken)=[^,\]]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override bool CanConvert(Type objectType) => objectType == typeof(Type);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.Value is not string typeName || string.IsNullOrEmpty(typeName))
                throw new JsonSerializationException($"Expected a type name, got {reader.TokenType}.");

            Type? resolved = tryResolve(typeName) ?? tryResolve(withoutAssemblyMetadata(typeName));

            // 解析不到就返回 null：让调用方丢掉这一个组件，而不是让整个布局解析失败被清空。
            if (resolved == null)
                Logger.Log($"Could not resolve serialised type \"{typeName}\"; the component will be dropped.", LoggingTarget.Runtime, LogLevel.Important);

            return resolved;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not Type type)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(withoutAssemblyMetadata(type.AssemblyQualifiedName ?? type.FullName ?? type.Name));
        }

        private static Type? tryResolve(string typeName)
        {
            // 请求的程序集版本与已加载的不一致时，加载器会抛 FileLoadException 而不是返回 null，两种都要当作解析失败。
            try
            {
                return Type.GetType(typeName);
            }
            catch
            {
                return null;
            }
        }

        private static string withoutAssemblyMetadata(string typeName) => assembly_metadata.Replace(typeName, string.Empty);
    }
}
