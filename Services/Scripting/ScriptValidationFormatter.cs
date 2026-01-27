namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Formats script validation output for UI display.
    /// </summary>
    public static class ScriptValidationFormatter
    {
        public static string FormatSuccessMessage()
        {
            return "Script validation succeeded (no errors found).";
        }

        public static string FormatFailureMessage(IEnumerable<string> errors)
        {
            var errorList = errors?.ToList() ?? new List<string>();
            if (errorList.Count == 0)
                return "Script validation failed.";

            return "Script validation failed:" + Environment.NewLine + string.Join(Environment.NewLine, errorList);
        }

        public static string FormatExceptionMessage(Exception ex)
        {
            var message = ex?.Message ?? "Unknown error";
            return $"Script validation error: {message}";
        }
    }
}
