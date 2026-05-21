// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 脚本编译时发生的错误。
    /// </summary>
    public class ScriptCompilationException : Exception
    {
        /// <summary>
        /// 编译诊断信息列表。
        /// </summary>
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        /// <summary>
        /// 创建编译异常。
        /// </summary>
        /// <param name="diagnostics">编译诊断信息。</param>
        public ScriptCompilationException(IEnumerable<Diagnostic> diagnostics)
            : base($"Script compilation failed with {diagnostics.Count()} error(s).")
        {
            Diagnostics = diagnostics.ToList();
        }

        /// <summary>
        /// 获取格式化的错误消息。
        /// </summary>
        /// <returns>包含所有编译错误的字符串。</returns>
        public string GetFormattedErrors()
        {
            var errors = Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            return string.Join("\n", errors.Select(d => $"[{d.Id}] {d.GetMessage()} (Line {d.Location.GetLineSpan().StartLinePosition.Line + 1})"));
        }
    }

    /// <summary>
    /// 脚本执行时发生的错误。
    /// </summary>
    public class ScriptExecutionException : Exception
    {
        /// <summary>
        /// 创建执行异常。
        /// </summary>
        /// <param name="message">错误消息。</param>
        /// <param name="innerException">内部异常。</param>
        public ScriptExecutionException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 脚本安全验证失败时抛出的异常。
    /// </summary>
    public class ScriptSecurityException : Exception
    {
        /// <summary>
        /// 创建安全异常。
        /// </summary>
        /// <param name="message">错误消息。</param>
        public ScriptSecurityException(string message)
            : base(message)
        {
        }
    }
}
