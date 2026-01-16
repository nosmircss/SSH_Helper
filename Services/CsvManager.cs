using System.Data;
using System.Text;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Handles CSV file import and export operations for the host grid.
    /// </summary>
    public class CsvManager
    {
        public const string HostColumnName = "Host_IP";

        /// <summary>
        /// Loads a CSV file into a DataTable.
        /// </summary>
        /// <param name="filePath">Path to the CSV file</param>
        /// <returns>DataTable with the CSV data</returns>
        public DataTable LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("CSV file not found", filePath);

            var dt = new DataTable();

            using var sr = new StreamReader(filePath);

            // Read header line
            string? headerLine = sr.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
                throw new InvalidDataException("CSV file is empty or has no header");

            string[] headers = ParseCsvLine(headerLine);

            // First column is always Host_IP
            dt.Columns.Add(HostColumnName);

            // Add remaining columns
            for (int i = 1; i < headers.Length; i++)
            {
                string headerName = headers[i].Trim();
                if (string.IsNullOrEmpty(headerName))
                {
                    headerName = $"Column{i}";
                }
                headerName = headerName.Replace(" ", "_");
                dt.Columns.Add(headerName);
            }

            // Read data rows
            while (!sr.EndOfStream)
            {
                string? line = sr.ReadLine();
                if (string.IsNullOrEmpty(line))
                    continue;

                string[] values = ParseCsvLine(line);

                // Skip completely empty rows
                if (values.All(string.IsNullOrWhiteSpace))
                    continue;

                // Ensure array fits column structure
                if (values.Length > headers.Length)
                    Array.Resize(ref values, headers.Length);

                dt.Rows.Add(values);
            }

            return dt;
        }

        /// <summary>
        /// Saves a DataGridView's contents to a CSV file.
        /// </summary>
        /// <param name="filePath">Path to save the CSV file</param>
        /// <param name="columns">Columns in display order</param>
        /// <param name="rows">Row data</param>
        public void SaveToFile(string filePath, IEnumerable<(string Name, string Header)> columns, IEnumerable<IEnumerable<string?>> rows)
        {
            using var sw = new StreamWriter(filePath, false, new UTF8Encoding(false));

            var columnList = columns.ToList();

            // Write headers
            for (int i = 0; i < columnList.Count; i++)
            {
                sw.Write(EscapeCsvValue(columnList[i].Header));
                if (i < columnList.Count - 1)
                    sw.Write(",");
            }
            sw.WriteLine();

            // Write data rows
            foreach (var row in rows)
            {
                var values = row.ToList();
                for (int i = 0; i < values.Count; i++)
                {
                    sw.Write(EscapeCsvValue(values[i] ?? ""));
                    if (i < values.Count - 1)
                        sw.Write(",");
                }
                sw.WriteLine();
            }
        }

        /// <summary>
        /// Creates a new DataTable with the default Host_IP column.
        /// </summary>
        public DataTable CreateEmptyTable()
        {
            var dt = new DataTable();
            dt.Columns.Add(HostColumnName);
            return dt;
        }

        /// <summary>
        /// Parses a CSV line, handling quoted values.
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var currentValue = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Check for escaped quote
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentValue.Append('"');
                            i++; // Skip next quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentValue.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        values.Add(currentValue.ToString());
                        currentValue.Clear();
                    }
                    else
                    {
                        currentValue.Append(c);
                    }
                }
            }

            values.Add(currentValue.ToString());
            return values.ToArray();
        }

        /// <summary>
        /// Escapes a value for CSV output.
        /// </summary>
        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // If value contains comma, quote, or newline, wrap in quotes
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
