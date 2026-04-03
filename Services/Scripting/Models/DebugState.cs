using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    /// Thread-safe: properties may be set from the UI thread while execution runs on a background thread.
    /// </summary>
    public class DebugState
    {
        private readonly object _signalLock = new();
        private TaskCompletionSource<DebugResumeAction>? _resumeSignal;
        private readonly ConcurrentDictionary<int, byte> _breakpoints = new();

        /// <summary>
        /// Set of breakpoint line numbers. Thread-safe via ConcurrentDictionary.
        /// </summary>
        public IReadOnlyCollection<int> Breakpoints => _breakpoints.Keys.ToList();

        /// <summary>
        /// When true, execution pauses after each step.
        /// </summary>
        public volatile bool StepMode;

        /// <summary>
        /// When true, execution is currently paused.
        /// </summary>
        public volatile bool IsPaused;

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
            get => Volatile.Read(ref _continueRequested);
            set
            {
                Volatile.Write(ref _continueRequested, value);
                if (value)
                {
                    lock (_signalLock)
                    {
                        _resumeSignal?.TrySetResult(DebugResumeAction.Continue);
                    }
                }
            }
        }
        private bool _continueRequested;

        /// <summary>
        /// Request to step to next instruction (set by UI).
        /// Also signals the async resume mechanism.
        /// </summary>
        public bool StepRequested
        {
            get => Volatile.Read(ref _stepRequested);
            set
            {
                Volatile.Write(ref _stepRequested, value);
                if (value)
                {
                    lock (_signalLock)
                    {
                        _resumeSignal?.TrySetResult(DebugResumeAction.Step);
                    }
                }
            }
        }
        private bool _stepRequested;

        /// <summary>
        /// Waits asynchronously for a resume signal (step or continue) from the UI.
        /// Replaces the 100ms polling loop for responsive debug pausing.
        /// Parallel pause waiters share one active signal so a single resume action
        /// can release all simultaneously paused branches.
        /// </summary>
        public async Task<DebugResumeAction> WaitForResumeAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource<DebugResumeAction> tcs;
            lock (_signalLock)
            {
                if (_resumeSignal == null || _resumeSignal.Task.IsCompleted)
                {
                    Volatile.Write(ref _continueRequested, false);
                    Volatile.Write(ref _stepRequested, false);
                    _resumeSignal = new TaskCompletionSource<DebugResumeAction>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }

                tcs = _resumeSignal;
            }
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());
            return await tcs.Task;
        }

        /// <summary>
        /// Adds a breakpoint at the specified line.
        /// </summary>
        public void AddBreakpoint(int lineNumber) => _breakpoints.TryAdd(lineNumber, 0);

        /// <summary>
        /// Removes a breakpoint at the specified line.
        /// </summary>
        public void RemoveBreakpoint(int lineNumber) => _breakpoints.TryRemove(lineNumber, out _);

        /// <summary>
        /// Toggles a breakpoint at the specified line.
        /// </summary>
        public void ToggleBreakpoint(int lineNumber)
        {
            if (!_breakpoints.TryRemove(lineNumber, out _))
                _breakpoints.TryAdd(lineNumber, 0);
        }

        /// <summary>
        /// Clears all breakpoints.
        /// </summary>
        public void ClearBreakpoints() => _breakpoints.Clear();

        /// <summary>
        /// Checks if execution should pause at the given line.
        /// </summary>
        public bool ShouldPauseAt(int lineNumber)
        {
            return StepMode || _breakpoints.ContainsKey(lineNumber);
        }

        // Node-ID-based breakpoints (for FlowCanvas integration)
        private readonly ConcurrentDictionary<string, byte> _nodeBreakpoints = new();

        // Disabled nodes (skipped during execution)
        private readonly ConcurrentDictionary<string, byte> _disabledNodes = new();

        // Node-ID-to-StepPath mapping (set before execution starts)
        private readonly ConcurrentDictionary<string, string> _nodeToStepPathMap = new();
        // Reverse: StepPath-to-NodeId for fast lookup during execution
        private readonly ConcurrentDictionary<string, string> _stepPathToNodeMap = new();

        public void SetNodeToStepPathMap(Dictionary<string, string> map)
        {
            _nodeToStepPathMap.Clear();
            _stepPathToNodeMap.Clear();

            foreach (var kvp in map)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                    continue;

                _nodeToStepPathMap[kvp.Key] = kvp.Value;
                // Keep first writer for duplicate paths to maintain deterministic mapping.
                _stepPathToNodeMap.TryAdd(kvp.Value, kvp.Key);
            }
        }

        /// <summary>
        /// Compatibility helper for legacy step-index maps.
        /// </summary>
        public void SetNodeToStepIndexMap(Dictionary<string, int> map)
        {
            var converted = map.ToDictionary(
                pair => pair.Key,
                pair => $"steps/{pair.Value}",
                System.StringComparer.Ordinal);

            SetNodeToStepPathMap(converted);
        }

        public void ToggleNodeBreakpoint(string nodeId)
        {
            if (_nodeBreakpoints.ContainsKey(nodeId))
                _nodeBreakpoints.TryRemove(nodeId, out _);
            else
                _nodeBreakpoints[nodeId] = 0;
        }

        public bool HasNodeBreakpoint(string nodeId) => _nodeBreakpoints.ContainsKey(nodeId);

        /// <summary>
        /// Checks if execution should pause at the given step path.
        /// Checks both line-number breakpoints AND node-ID breakpoints.
        /// </summary>
        public bool ShouldPauseAtStep(string? stepPath, int lineNumber)
        {
            if (StepMode) return true;
            if (_breakpoints.ContainsKey(lineNumber)) return true;

            // Check node breakpoints via step path mapping
            if (!string.IsNullOrWhiteSpace(stepPath) &&
                _stepPathToNodeMap.TryGetValue(stepPath, out var nodeId))
            {
                return _nodeBreakpoints.ContainsKey(nodeId);
            }

            return false;
        }

        /// <summary>
        /// Compatibility helper for legacy step-index callers.
        /// </summary>
        public bool ShouldPauseAtStep(int stepIndex, int lineNumber)
            => ShouldPauseAtStep($"steps/{stepIndex}", lineNumber);

        public void ToggleNodeDisabled(string nodeId)
        {
            if (_disabledNodes.ContainsKey(nodeId))
                _disabledNodes.TryRemove(nodeId, out _);
            else
                _disabledNodes[nodeId] = 0;
        }

        public bool IsNodeDisabled(string nodeId) => _disabledNodes.ContainsKey(nodeId);

        public string? GetNodeIdForStepPath(string? stepPath)
        {
            if (string.IsNullOrWhiteSpace(stepPath))
                return null;

            _stepPathToNodeMap.TryGetValue(stepPath, out var nodeId);
            return nodeId;
        }

        public string? GetNodeIdForStepIndex(int stepIndex)
            => GetNodeIdForStepPath($"steps/{stepIndex}");

        /// <summary>
        /// Resets the debug state for a new execution.
        /// </summary>
        public void Reset()
        {
            IsPaused = false;
            PausedAtLine = null;
            Volatile.Write(ref _continueRequested, false);
            Volatile.Write(ref _stepRequested, false);
            lock (_signalLock)
            {
                _resumeSignal = null;
            }
            // Keep breakpoints and step mode setting
        }
    }
}
