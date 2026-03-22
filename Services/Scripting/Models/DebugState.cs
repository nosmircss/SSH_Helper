using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// The action that caused the debugger to resume.
    /// </summary>
    public enum DebugResumeAction
    {
        Step,
        Continue
    }

    /// <summary>
    /// Tracks debug state for script execution including breakpoints and step mode.
    /// </summary>
    public class DebugState
    {
        private TaskCompletionSource<DebugResumeAction>? _resumeSignal;

        /// <summary>
        /// Set of line numbers where execution should pause.
        /// </summary>
        public HashSet<int> Breakpoints { get; } = new();

        /// <summary>
        /// When true, execution pauses after each step.
        /// </summary>
        public bool StepMode { get; set; }

        /// <summary>
        /// When true, execution is currently paused.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// The line number where execution is currently paused.
        /// </summary>
        public int? PausedAtLine { get; set; }

        /// <summary>
        /// Request to continue execution (set by UI).
        /// Also signals the async resume mechanism.
        /// </summary>
        public bool ContinueRequested
        {
            get => _continueRequested;
            set
            {
                _continueRequested = value;
                if (value)
                    _resumeSignal?.TrySetResult(DebugResumeAction.Continue);
            }
        }
        private bool _continueRequested;

        /// <summary>
        /// Request to step to next instruction (set by UI).
        /// Also signals the async resume mechanism.
        /// </summary>
        public bool StepRequested
        {
            get => _stepRequested;
            set
            {
                _stepRequested = value;
                if (value)
                    _resumeSignal?.TrySetResult(DebugResumeAction.Step);
            }
        }
        private bool _stepRequested;

        /// <summary>
        /// Waits asynchronously for a resume signal (step or continue) from the UI.
        /// Replaces the 100ms polling loop for responsive debug pausing.
        /// </summary>
        public async Task<DebugResumeAction> WaitForResumeAsync(CancellationToken cancellationToken)
        {
            _resumeSignal = new TaskCompletionSource<DebugResumeAction>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => _resumeSignal.TrySetCanceled());
            return await _resumeSignal.Task;
        }

        /// <summary>
        /// Adds a breakpoint at the specified line.
        /// </summary>
        public void AddBreakpoint(int lineNumber) => Breakpoints.Add(lineNumber);

        /// <summary>
        /// Removes a breakpoint at the specified line.
        /// </summary>
        public void RemoveBreakpoint(int lineNumber) => Breakpoints.Remove(lineNumber);

        /// <summary>
        /// Toggles a breakpoint at the specified line.
        /// </summary>
        public void ToggleBreakpoint(int lineNumber)
        {
            if (Breakpoints.Contains(lineNumber))
                Breakpoints.Remove(lineNumber);
            else
                Breakpoints.Add(lineNumber);
        }

        /// <summary>
        /// Clears all breakpoints.
        /// </summary>
        public void ClearBreakpoints() => Breakpoints.Clear();

        /// <summary>
        /// Checks if execution should pause at the given line.
        /// </summary>
        public bool ShouldPauseAt(int lineNumber)
        {
            return StepMode || Breakpoints.Contains(lineNumber);
        }

        /// <summary>
        /// Resets the debug state for a new execution.
        /// </summary>
        public void Reset()
        {
            IsPaused = false;
            PausedAtLine = null;
            _continueRequested = false;
            _stepRequested = false;
            _resumeSignal = null;
            // Keep breakpoints and step mode setting
        }
    }
}
