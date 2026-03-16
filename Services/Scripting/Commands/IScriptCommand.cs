using System;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Interface for script command executors.
    /// </summary>
    public interface IScriptCommand
    {
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="step">The script step containing command parameters.</param>
        /// <param name="context">The execution context with variables and session.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result indicating success/failure and optional message.</returns>
        Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Result of a command execution.
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// Whether the command succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Optional message from the command.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// If true, script execution should stop (e.g., exit command).
        /// </summary>
        public bool ShouldExit { get; set; }

        /// <summary>
        /// Exit status if ShouldExit is true.
        /// </summary>
        public ScriptExitStatus ExitStatus { get; set; } = ScriptExitStatus.Success;

        /// <summary>
        /// If true, break out of the current loop.
        /// </summary>
        public bool ShouldBreak { get; set; }

        /// <summary>
        /// If true, continue to next loop iteration.
        /// </summary>
        public bool ShouldContinue { get; set; }

        /// <summary>
        /// If true, return from the current subroutine.
        /// </summary>
        public bool ShouldReturn { get; set; }

        /// <summary>
        /// If true, the command encountered an error that was explicitly suppressed
        /// (for example, via on_error: continue).
        /// </summary>
        public bool SuppressedError { get; set; }

        /// <summary>
        /// Creates a success result.
        /// </summary>
        public static CommandResult Ok(string? message = null) => new() { Success = true, Message = message };

        /// <summary>
        /// Creates a failure result.
        /// </summary>
        public static CommandResult Fail(string message) => new() { Success = false, Message = message };

        /// <summary>
        /// Creates an exit result.
        /// </summary>
        public static CommandResult Exit(ScriptExitStatus status, string message) => new()
        {
            Success = status == ScriptExitStatus.Success,
            Message = message,
            ShouldExit = true,
            ExitStatus = status
        };

        /// <summary>
        /// Creates a break result (exit current loop).
        /// </summary>
        public static CommandResult Break() => new() { Success = true, ShouldBreak = true };

        /// <summary>
        /// Creates a continue result (next loop iteration).
        /// </summary>
        public static CommandResult Continue() => new() { Success = true, ShouldContinue = true };

        /// <summary>
        /// Creates a return result (exit current subroutine).
        /// </summary>
        public static CommandResult Return() => new() { Success = true, ShouldReturn = true };

        /// <summary>
        /// Creates a success result that carries a suppressed error message.
        /// </summary>
        public static CommandResult Suppressed(string message) => new()
        {
            Success = true,
            Message = message,
            SuppressedError = true
        };

        /// <summary>
        /// Whether this result carries a control flow signal (exit, break, continue, or return).
        /// </summary>
        public bool IsControlFlow => ShouldExit || ShouldBreak || ShouldContinue || ShouldReturn;

        /// <summary>
        /// Returns Suppressed or Fail based on the step's on_error setting.
        /// </summary>
        public static CommandResult ApplyOnError(ScriptStep step, string message)
        {
            if (step.IsOnErrorContinue)
                return Suppressed(message);
            return Fail(message);
        }
    }

    /// <summary>
    /// Shared utility methods for script command implementations.
    /// </summary>
    public static class ScriptingHelpers
    {
        /// <summary>
        /// Truncates a string for debug/display output, replacing newlines with literal \n.
        /// </summary>
        public static string TruncateForDisplay(string value, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            value = value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + "...";
        }
    }
}
