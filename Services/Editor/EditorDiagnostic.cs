namespace SSH_Helper.Services.Editor
{
    public enum DiagnosticSeverity
    {
        Error,
        Warning,
        Info
    }

    public sealed class EditorDiagnostic
    {
        public int LineNumber { get; init; }
        public int ColumnStart { get; init; }
        public int ColumnEnd { get; init; }
        public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Error;
        public string Message { get; init; } = string.Empty;

        public bool Contains(int lineNumber, int column)
        {
            if (lineNumber != LineNumber)
                return false;

            return column >= ColumnStart && column <= ColumnEnd;
        }

        public static EditorDiagnostic CreateLineSpan(
            int lineNumber,
            int lineLength,
            DiagnosticSeverity severity,
            string message)
        {
            var safeLength = Math.Max(1, lineLength);
            return new EditorDiagnostic
            {
                LineNumber = Math.Max(1, lineNumber),
                ColumnStart = 1,
                ColumnEnd = safeLength,
                Severity = severity,
                Message = message ?? string.Empty
            };
        }
    }
}
