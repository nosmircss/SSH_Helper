namespace SSH_Helper.Utilities
{
    public static class ExecutionDialogPolicy
    {
        public static bool ShouldPromptForPresetExecutionOptions(int hostCount)
        {
            return hostCount > 1;
        }
    }
}
