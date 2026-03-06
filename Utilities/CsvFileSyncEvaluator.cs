using System.Data;
using SSH_Helper.Models;
using SSH_Helper.Services;

namespace SSH_Helper.Utilities
{
    internal enum CsvFileSyncStatus
    {
        NotTracked,
        Current,
        ChangedOnDisk,
        MissingOnDisk,
        Unknown
    }

    internal readonly record struct CsvFileSyncEvaluation(
        CsvFileSyncStatus Status,
        CsvFileFingerprint? CurrentFingerprint,
        string? ErrorMessage = null);

    internal static class CsvFileSyncEvaluator
    {
        public static CsvFileFingerprint? Capture(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            var fileInfo = new FileInfo(filePath);
            return new CsvFileFingerprint
            {
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                FileSizeBytes = fileInfo.Length
            };
        }

        public static CsvFileSyncEvaluation Evaluate(string? filePath, CsvFileFingerprint? baselineFingerprint)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return new CsvFileSyncEvaluation(CsvFileSyncStatus.NotTracked, null);

            if (!File.Exists(filePath))
                return new CsvFileSyncEvaluation(CsvFileSyncStatus.MissingOnDisk, null);

            try
            {
                var currentFingerprint = Capture(filePath);
                if (baselineFingerprint == null)
                    return new CsvFileSyncEvaluation(CsvFileSyncStatus.Unknown, currentFingerprint);

                return new CsvFileSyncEvaluation(
                    Matches(baselineFingerprint, currentFingerprint)
                        ? CsvFileSyncStatus.Current
                        : CsvFileSyncStatus.ChangedOnDisk,
                    currentFingerprint);
            }
            catch (Exception ex)
            {
                return new CsvFileSyncEvaluation(CsvFileSyncStatus.Unknown, null, ex.Message);
            }
        }

        public static CsvFileSyncEvaluation EvaluateEnvironment(EnvironmentConfig environment, CsvManager csvManager)
        {
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));

            if (csvManager == null)
                throw new ArgumentNullException(nameof(csvManager));

            var filePath = environment.LastCsvPath;
            if (string.IsNullOrWhiteSpace(filePath))
                return new CsvFileSyncEvaluation(CsvFileSyncStatus.NotTracked, null);

            if (!File.Exists(filePath))
                return new CsvFileSyncEvaluation(CsvFileSyncStatus.MissingOnDisk, null);

            try
            {
                var currentFingerprint = Capture(filePath);
                if (environment.LastCsvFingerprint != null)
                {
                    return new CsvFileSyncEvaluation(
                        Matches(environment.LastCsvFingerprint, currentFingerprint)
                            ? CsvFileSyncStatus.Current
                            : CsvFileSyncStatus.ChangedOnDisk,
                        currentFingerprint);
                }

                var table = csvManager.LoadFromFile(filePath);
                return new CsvFileSyncEvaluation(
                    SnapshotMatchesCsv(environment, table)
                        ? CsvFileSyncStatus.Current
                        : CsvFileSyncStatus.ChangedOnDisk,
                    currentFingerprint);
            }
            catch (Exception ex)
            {
                return new CsvFileSyncEvaluation(CsvFileSyncStatus.Unknown, null, ex.Message);
            }
        }

        public static bool Matches(CsvFileFingerprint? left, CsvFileFingerprint? right)
        {
            if (left == null || right == null)
                return false;

            left.Normalize();
            right.Normalize();

            return left.LastWriteTimeUtc == right.LastWriteTimeUtc &&
                   left.FileSizeBytes == right.FileSizeBytes;
        }

        private static bool SnapshotMatchesCsv(EnvironmentConfig environment, DataTable table)
        {
            var expectedColumns = NormalizeColumns(environment.HostColumns);
            var actualColumns = table.Columns.Cast<DataColumn>()
                .Select(column => column.ColumnName)
                .ToList();

            if (expectedColumns.Count != actualColumns.Count)
                return false;

            for (int i = 0; i < expectedColumns.Count; i++)
            {
                if (!string.Equals(expectedColumns[i], actualColumns[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            var expectedRows = environment.Hosts ?? new List<Dictionary<string, string>>();
            if (expectedRows.Count != table.Rows.Count)
                return false;

            for (int rowIndex = 0; rowIndex < expectedRows.Count; rowIndex++)
            {
                var expectedRow = expectedRows[rowIndex] ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var columnName in expectedColumns)
                {
                    expectedRow.TryGetValue(columnName, out var expectedValue);
                    var actualValue = table.Rows[rowIndex][columnName]?.ToString() ?? string.Empty;
                    if (!string.Equals(expectedValue ?? string.Empty, actualValue, StringComparison.Ordinal))
                        return false;
                }
            }

            return true;
        }

        private static List<string> NormalizeColumns(IEnumerable<string>? columns)
        {
            var result = (columns ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!result.Contains(CsvManager.HostColumnName, StringComparer.OrdinalIgnoreCase))
            {
                result.Insert(0, CsvManager.HostColumnName);
            }

            return result;
        }
    }
}
