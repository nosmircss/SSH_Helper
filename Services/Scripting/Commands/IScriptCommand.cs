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
    }
}
