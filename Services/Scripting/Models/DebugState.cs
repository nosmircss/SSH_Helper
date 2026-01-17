using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// Tracks debug state for script execution including breakpoints and step mode.
    /// </summary>
    public class DebugState
    {
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
        /// </summary>
        public bool ContinueRequested { get; set; }

        /// <summary>
        /// Request to step to next instruction (set by UI).
        /// </summary>
        public bool StepRequested { get; set; }

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
            ContinueRequested = false;
            StepRequested = false;
            // Keep breakpoints and step mode setting
        }
    }
}
