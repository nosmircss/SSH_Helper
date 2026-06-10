namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// One level of the live loop-iteration stack: which loop (by canonical step path,
    /// e.g. "steps/2"), which iteration (0-based; -1 = pushed but no iteration started yet),
    /// and an optional display label (the foreach item value, truncated). Immutable —
    /// event consumers keep references to frames without copying.
    /// </summary>
    public sealed record IterationFrame(string LoopStepPath, int Index, string? Label = null);
}
