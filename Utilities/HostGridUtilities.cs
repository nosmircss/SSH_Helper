using System.Data;
using System.Text;
using SSH_Helper.Services;

namespace SSH_Helper.Utilities
{
    internal sealed class HostGridSnapshot
    {
        public HostGridSnapshot(IReadOnlyList<string> columns, IReadOnlyList<Dictionary<string, string>> rows)
        {
            Columns = columns;
            Rows = rows;
        }

        public IReadOnlyList<string> Columns { get; }
        public IReadOnlyList<Dictionary<string, string>> Rows { get; }
    }

    internal static class HostGridUtilities
    {
        public const int DefaultRowHeight = 28;
        public const int DefaultColumnHeaderHeight = 36;
        public const int DefaultRowHeaderWidth = 50;
        public const int DefaultHostColumnWidth = 150;
        public const int DefaultAdditionalColumnWidth = 120;

        public static HostGridSnapshot BuildSchedulerCopySnapshot(DataGridView grid)
        {
            var columns = GetCopyableColumns(grid);
            var selectionColumn = GetSelectionColumn(grid);

            var eligibleRows = grid.Rows
                .Cast<DataGridViewRow>()
                .Where(row => IsEligibleHostRow(row))
                .ToList();

            var checkedRows = selectionColumn == null
                ? new List<DataGridViewRow>()
                : eligibleRows
                    .Where(row => row.Cells[selectionColumn.Index].Value is true)
                    .ToList();

            var rowsToCopy = checkedRows.Count > 0 ? checkedRows : eligibleRows;
            var rows = rowsToCopy
                .Select(row => BuildRowSnapshot(row, columns))
                .ToList();

            return new HostGridSnapshot(columns, rows);
        }

        public static HostGridSnapshot BuildSnapshot(DataTable table)
        {
            var columns = table.Columns
                .Cast<DataColumn>()
                .Select(column => column.ColumnName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            var rows = table.Rows
                .Cast<DataRow>()
                .Select(row =>
                {
                    var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var columnName in columns)
                    {
                        rowData[columnName] = row[columnName]?.ToString() ?? string.Empty;
                    }

                    return rowData;
                })
                .ToList();

            return new HostGridSnapshot(columns, rows);
        }

        public static int CountHosts(DataGridView grid)
        {
            return grid.Rows
                .Cast<DataGridViewRow>()
                .Count(row => IsEligibleHostRow(row));
        }

        public static string BuildClipboardText(DataGridView grid)
        {
            bool allSelected = grid.SelectedCells.Count == grid.RowCount * grid.ColumnCount;
            var buffer = new StringBuilder();

            if (allSelected)
            {
                for (int j = 0; j < grid.ColumnCount; j++)
                {
                    buffer.Append(grid.Columns[j].HeaderText);
                    if (j < grid.ColumnCount - 1)
                    {
                        buffer.Append('\t');
                    }
                }

                buffer.AppendLine();

                int rowCount = grid.AllowUserToAddRows ? grid.Rows.Count - 1 : grid.Rows.Count;
                for (int i = 0; i < rowCount; i++)
                {
                    bool isEmpty = true;
                    var rowBuffer = new StringBuilder();

                    for (int j = 0; j < grid.Columns.Count; j++)
                    {
                        string value = grid.Rows[i].Cells[j].Value?.ToString() ?? string.Empty;
                        rowBuffer.Append(value);
                        if (j < grid.Columns.Count - 1)
                        {
                            rowBuffer.Append('\t');
                        }

                        if (!string.IsNullOrEmpty(value))
                        {
                            isEmpty = false;
                        }
                    }

                    if (!isEmpty)
                    {
                        buffer.AppendLine(rowBuffer.ToString());
                    }
                }
            }
            else
            {
                var sortedCells = grid.SelectedCells
                    .Cast<DataGridViewCell>()
                    .OrderBy(cell => cell.RowIndex)
                    .ThenBy(cell => cell.ColumnIndex)
                    .ToList();

                int lastRowIndex = -1;
                foreach (var cell in sortedCells)
                {
                    if (cell.RowIndex != lastRowIndex)
                    {
                        if (lastRowIndex != -1)
                        {
                            buffer.AppendLine();
                        }

                        lastRowIndex = cell.RowIndex;
                    }
                    else
                    {
                        buffer.Append('\t');
                    }

                    buffer.Append(cell.Value?.ToString() ?? string.Empty);
                }
            }

            return buffer.ToString();
        }

        public static void PasteClipboardText(DataGridView grid, string clipboardText)
        {
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                return;
            }

            var startCell = grid.CurrentCell;
            int startCol = startCell?.ColumnIndex ?? 0;
            int startRow = startCell?.RowIndex ?? 0;
            var rows = clipboardText
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            grid.AllowUserToAddRows = false;

            for (int i = 0; i < rows.Length; i++)
            {
                string[] columns = rows[i].Split('\t');
                for (int j = 0; j < columns.Length; j++)
                {
                    int rowIndex = startRow + i;
                    while (rowIndex >= grid.Rows.Count)
                    {
                        grid.Rows.Add(new DataGridViewRow());
                    }

                    int columnIndex = startCol + j;
                    while (columnIndex >= grid.Columns.Count)
                    {
                        var columnName = GetNextGeneratedColumnName(grid.Columns);
                        grid.Columns.Add(CreateTextColumn(columnName));
                    }

                    if (!grid.Columns[columnIndex].ReadOnly)
                    {
                        grid.Rows[rowIndex].Cells[columnIndex].Value = columns[j];
                    }
                }
            }

            grid.AllowUserToAddRows = true;
            grid.ClearSelection();

            int newRowIndex = grid.Rows.Count - 1;
            if (newRowIndex >= 0 && grid.Rows[newRowIndex].IsNewRow && startCol < grid.ColumnCount)
            {
                grid.CurrentCell = grid.Rows[newRowIndex].Cells[startCol];
            }
        }

        public static void ClearSelectedCells(DataGridView grid)
        {
            foreach (DataGridViewCell cell in grid.SelectedCells)
            {
                if (!cell.ReadOnly)
                {
                    cell.Value = null;
                }
            }
        }

        public static string GetNextGeneratedColumnName(DataGridViewColumnCollection columns)
        {
            int nextNumber = columns.Count + 1;
            string defaultName = $"Column{nextNumber}";

            while (columns.Cast<DataGridViewColumn>()
                .Any(column => string.Equals(column.Name, defaultName, StringComparison.OrdinalIgnoreCase)))
            {
                nextNumber++;
                defaultName = $"Column{nextNumber}";
            }

            return defaultName;
        }

        public static DataGridViewTextBoxColumn CreateTextColumn(string columnName)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = columnName,
                HeaderText = columnName,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Width = string.Equals(columnName, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase)
                    ? DefaultHostColumnWidth
                    : DefaultAdditionalColumnWidth
            };
        }

        public static bool IsProtectedHostColumn(DataGridViewColumn? column)
        {
            if (column == null)
            {
                return false;
            }

            return string.Equals(column.Name, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(column.HeaderText, CsvManager.HostColumnName, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> GetCopyableColumns(DataGridView grid)
        {
            var columns = grid.Columns
                .Cast<DataGridViewColumn>()
                .Where(column => !string.IsNullOrWhiteSpace(column.Name))
                .OrderBy(column => column.DisplayIndex)
                .Select(column => column.Name)
                .ToList();

            if (!columns.Contains(CsvManager.HostColumnName, StringComparer.OrdinalIgnoreCase))
            {
                columns.Insert(0, CsvManager.HostColumnName);
            }

            return columns;
        }

        private static DataGridViewColumn? GetSelectionColumn(DataGridView grid)
        {
            return grid.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(column =>
                    column is DataGridViewCheckBoxColumn &&
                    string.IsNullOrWhiteSpace(column.Name) &&
                    string.IsNullOrWhiteSpace(column.HeaderText));
        }

        private static bool IsEligibleHostRow(DataGridViewRow row)
        {
            if (row.IsNewRow)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(GetCellText(row, CsvManager.HostColumnName));
        }

        private static Dictionary<string, string> BuildRowSnapshot(DataGridViewRow row, IReadOnlyList<string> columns)
        {
            var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var columnName in columns)
            {
                rowData[columnName] = GetCellText(row, columnName);
            }

            return rowData;
        }

        private static string GetCellText(DataGridViewRow row, string columnName)
        {
            var grid = row.DataGridView;
            if (grid == null || !grid.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            return row.Cells[columnName].Value?.ToString() ?? string.Empty;
        }
    }
}
