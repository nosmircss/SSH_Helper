using SSH_Helper.Models;

namespace SSH_Helper.Utilities
{
    internal readonly record struct ManualExecutionStatusProgressState(
        int CompletedOperations,
        int TotalOperations,
        string StatusText);

    internal static class ManualExecutionStatusProgress
    {
        public static bool ShouldShowProgress(int totalOperations)
            => totalOperations > 1;

        public static ManualExecutionStatusProgressState Advance(
            int previousCompletedOperations,
            FolderExecutionProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);

            int totalOperations = Math.Max(0, progress.TotalOperations);
            if (totalOperations <= 0)
            {
                return new ManualExecutionStatusProgressState(
                    Math.Max(0, previousCompletedOperations),
                    0,
                    "Running...");
            }

            int completedOperations = Math.Clamp(
                Math.Max(previousCompletedOperations, progress.CompletedOperations),
                0,
                totalOperations);
            int percent = (int)((long)completedOperations * 100 / totalOperations);

            return new ManualExecutionStatusProgressState(
                completedOperations,
                totalOperations,
                $"Running... {percent}%");
        }
    }
}
