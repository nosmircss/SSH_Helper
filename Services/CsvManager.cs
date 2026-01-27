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

            // Read header record (supports multiline/quoted values)
            string[]? headers = ReadCsvRecord(sr);
            if (headers == null || headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("CSV file is empty or has no header");

            headers[0] = headers[0].TrimStart('\uFEFF');

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
            while (true)
            {
                var values = ReadCsvRecord(sr);
                if (values == null)
                    break;

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
        /// Reads a CSV record from a TextReader, supporting quoted fields with embedded newlines.
        /// </summary>
        private static string[]? ReadCsvRecord(TextReader reader)
        {
            var values = new List<string>();
            var currentValue = new StringBuilder();
            bool inQuotes = false;
            bool readAny = false;

            while (true)
            {
                int next = reader.Read();
                if (next == -1)
                {
                    if (!readAny && values.Count == 0 && currentValue.Length == 0)
                        return null;

                    values.Add(currentValue.ToString());
                    return values.ToArray();
                }

                readAny = true;
                char c = (char)next;

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Escaped quote
                        if (reader.Peek() == '"')
                        {
                            reader.Read();
                            currentValue.Append('"');
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
                    else if (c == '\r')
                    {
                        if (reader.Peek() == '\n')
                            reader.Read();
                        values.Add(currentValue.ToString());
                        return values.ToArray();
                    }
                    else if (c == '\n')
                    {
                        values.Add(currentValue.ToString());
                        return values.ToArray();
                    }
                    else
                    {
                        currentValue.Append(c);
                    }
                }
            }
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
