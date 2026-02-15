using Newtonsoft.Json;
using SSH_Helper.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Persists execution history metadata and per-run payload files alongside config.json.
    /// </summary>
    public sealed class HistoryStorageService
    {
        private const string IndexFileName = "history.index.json";
        private const string RunFolderName = "history";
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private readonly string _indexPath;
        private readonly string _runFolderPath;

        public HistoryStorageService(string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new ArgumentException("Configuration path is required.", nameof(configFilePath));

            var baseDirectory = Path.GetDirectoryName(configFilePath);
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Configuration path must include a directory.", nameof(configFilePath));

            _indexPath = Path.Combine(baseDirectory, IndexFileName);
            _runFolderPath = Path.Combine(baseDirectory, RunFolderName);
        }

        public IReadOnlyList<HistoryIndexEntry> LoadIndex()
        {
            var document = ReadIndexDocument();
            return document.Entries;
        }

        public bool TryLoadRunPayload(
            string entryId,
            out HistoryRunPayload? payload,
            bool includeDetails = true,
            bool includeHostOutputs = true,
            int? maxOutputChars = null)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            var runPath = GetDefaultRunFilePath(entryId);
            if (!File.Exists(runPath))
            {
                var indexEntry = ReadIndexDocument().Entries
                    .FirstOrDefault(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
                if (indexEntry == null)
                    return false;

                runPath = GetRunFilePath(indexEntry);
                if (!File.Exists(runPath))
                    return false;
            }

            try
            {
                if (!includeDetails && !includeHostOutputs)
                {
                    payload = DeserializePayloadLightweight(runPath, maxOutputChars);
                    if (payload == null)
                        return false;

                    payload.Id = string.IsNullOrWhiteSpace(payload.Id) ? entryId : payload.Id;
                    payload.Output ??= string.Empty;
                    return true;
                }

                using var fileStream = File.OpenRead(runPath);
                using var streamReader = new StreamReader(fileStream, Utf8NoBom, detectEncodingFromByteOrderMarks: true);
                using var jsonReader = new JsonTextReader(streamReader);
                var serializer = Newtonsoft.Json.JsonSerializer.CreateDefault();
                payload = includeDetails && includeHostOutputs
                    ? serializer.Deserialize<HistoryRunPayload>(jsonReader)
                    : DeserializePayloadSelective(jsonReader, serializer, includeDetails, includeHostOutputs);
                if (payload == null)
                    return false;

                payload.Id = string.IsNullOrWhiteSpace(payload.Id) ? entryId : payload.Id;
                payload.Output ??= string.Empty;
                return true;
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        private static HistoryRunPayload? DeserializePayloadLightweight(string runPath, int? maxOutputChars)
        {
            var jsonBytes = File.ReadAllBytes(runPath);
            var reader = new Utf8JsonReader(jsonBytes, isFinalBlock: true, state: default);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return null;

            var payload = new HistoryRunPayload();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var isId = reader.ValueTextEquals("Id") || reader.ValueTextEquals("id");
                var isOutput = reader.ValueTextEquals("Output") || reader.ValueTextEquals("output");
                var isHostResults = reader.ValueTextEquals("HostResults") || reader.ValueTextEquals("hostResults");

                if (!reader.Read())
                    break;

                if (isId)
                {
                    payload.Id = ReadStringValue(ref reader);
                }
                else if (isOutput)
                {
                    payload.Output = ReadBoundedStringValue(ref reader, maxOutputChars);
                }
                else if (isHostResults)
                {
                    payload.HostResults = ReadHostResultsMetadata(ref reader);
                }
                else
                {
                    SkipValue(ref reader);
                }
            }

            payload.Output ??= string.Empty;
            return payload;
        }

        private static List<HostHistoryEntry>? ReadHostResultsMetadata(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                SkipValue(ref reader);
                return null;
            }

            var results = new List<HostHistoryEntry>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    SkipValue(ref reader);
                    continue;
                }

                var host = new HostHistoryEntry();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;

                    var isHostAddress = reader.ValueTextEquals("HostAddress") || reader.ValueTextEquals("hostAddress");
                    var isSuccess = reader.ValueTextEquals("Success") || reader.ValueTextEquals("success");
                    var isTimestamp = reader.ValueTextEquals("Timestamp") || reader.ValueTextEquals("timestamp");

                    if (!reader.Read())
                        break;

                    if (isHostAddress)
                    {
                        host.HostAddress = ReadStringValue(ref reader);
                    }
                    else if (isSuccess)
                    {
                        host.Success = ReadBooleanValue(ref reader);
                    }
                    else if (isTimestamp)
                    {
                        host.Timestamp = ReadDateTimeValue(ref reader);
                    }
                    else
                    {
                        SkipValue(ref reader);
                    }
                }

                results.Add(host);
            }

            return results;
        }

        private static string ReadBoundedStringValue(ref Utf8JsonReader reader, int? maxChars)
        {
            var value = ReadStringValue(ref reader);
            if (!maxChars.HasValue || maxChars.Value <= 0 || value.Length <= maxChars.Value)
                return value;

            var removed = value.Length - maxChars.Value;
            var marker = $"[... output trimmed {removed:N0} chars from start ...]{Environment.NewLine}";
            var tailChars = maxChars.Value - marker.Length;
            if (tailChars <= 0)
                return marker.Substring(0, Math.Min(marker.Length, maxChars.Value));

            return marker + value.Substring(value.Length - tailChars, tailChars);
        }

        private static string ReadStringValue(ref Utf8JsonReader reader)
        {
            return reader.TokenType == JsonTokenType.String
                ? reader.GetString() ?? string.Empty
                : string.Empty;
        }

        private static bool ReadBooleanValue(ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                _ => false
            };
        }

        private static DateTime ReadDateTimeValue(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.String &&
                reader.TryGetDateTime(out var dateTime))
            {
                return dateTime;
            }

            return default;
        }

        private static void SkipValue(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject &&
                reader.TokenType != JsonTokenType.StartArray)
            {
                return;
            }

            var depth = reader.CurrentDepth;
            while (reader.Read())
            {
                if ((reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray) &&
                    reader.CurrentDepth == depth)
                {
                    break;
                }
            }
        }

        private static HistoryRunPayload? DeserializePayloadSelective(
            JsonTextReader reader,
            Newtonsoft.Json.JsonSerializer serializer,
            bool includeDetails,
            bool includeHostOutputs)
        {
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                return null;

            var payload = new HistoryRunPayload();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var propertyName = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
                if (propertyName == null || !reader.Read())
                    break;

                switch (propertyName)
                {
                    case var _ when propertyName.Equals("id", StringComparison.OrdinalIgnoreCase):
                        payload.Id = reader.TokenType == JsonToken.Null
                            ? string.Empty
                            : Convert.ToString(reader.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                        break;

                    case var _ when propertyName.Equals("output", StringComparison.OrdinalIgnoreCase):
                        payload.Output = reader.TokenType == JsonToken.Null
                            ? string.Empty
                            : Convert.ToString(reader.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                        break;

                    case var _ when propertyName.Equals("hostResults", StringComparison.OrdinalIgnoreCase):
                        if (reader.TokenType == JsonToken.Null)
                        {
                            payload.HostResults = null;
                        }
                        else if (reader.TokenType == JsonToken.StartArray)
                        {
                            payload.HostResults = includeHostOutputs
                                ? serializer.Deserialize<List<HostHistoryEntry>>(reader)
                                : DeserializeHostResultsWithoutOutputs(reader);
                        }
                        else
                        {
                            reader.Skip();
                        }

                        break;

                    case var _ when propertyName.Equals("details", StringComparison.OrdinalIgnoreCase):
                        if (includeDetails)
                        {
                            payload.Details = serializer.Deserialize<ExecutionDetails>(reader);
                        }
                        else
                        {
                            // History list selection does not need details; skip to avoid loading huge transcripts into memory.
                            reader.Skip();
                        }
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            }

            payload.Output ??= string.Empty;
            return payload;
        }

        private static List<HostHistoryEntry> DeserializeHostResultsWithoutOutputs(JsonTextReader reader)
        {
            var results = new List<HostHistoryEntry>();

            // Reader is currently positioned at StartArray.
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                    break;

                if (reader.TokenType != JsonToken.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                var host = new HostHistoryEntry();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject)
                        break;

                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    var propertyName = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
                    if (propertyName == null || !reader.Read())
                        break;

                    if (propertyName.Equals("HostAddress", StringComparison.OrdinalIgnoreCase))
                    {
                        host.HostAddress = reader.TokenType == JsonToken.Null
                            ? string.Empty
                            : Convert.ToString(reader.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                    }
                    else if (propertyName.Equals("Success", StringComparison.OrdinalIgnoreCase))
                    {
                        host.Success = reader.TokenType != JsonToken.Null &&
                            Convert.ToBoolean(reader.Value, CultureInfo.InvariantCulture);
                    }
                    else if (propertyName.Equals("Timestamp", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType == JsonToken.Date && reader.Value is DateTime dt)
                        {
                            host.Timestamp = dt;
                        }
                        else if (reader.TokenType != JsonToken.Null &&
                                 DateTime.TryParse(
                                     Convert.ToString(reader.Value, CultureInfo.InvariantCulture),
                                     CultureInfo.InvariantCulture,
                                     DateTimeStyles.RoundtripKind,
                                     out var parsed))
                        {
                            host.Timestamp = parsed;
                        }
                    }
                    else if (propertyName.Equals("Output", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip large host output text in lightweight mode.
                        reader.Skip();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                results.Add(host);
            }

            return results;
        }

        public void SaveRun(HistoryIndexEntry indexEntry, HistoryRunPayload payload, int maxEntries)
        {
            if (indexEntry == null)
                throw new ArgumentNullException(nameof(indexEntry));
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(indexEntry.Id))
                throw new ArgumentException("History index entry id is required.", nameof(indexEntry));

            var entryId = indexEntry.Id.Trim();
            var normalizedEntry = new HistoryIndexEntry
            {
                Id = entryId,
                Label = string.IsNullOrWhiteSpace(indexEntry.Label) ? entryId : indexEntry.Label,
                CreatedAtUtc = indexEntry.CreatedAtUtc == default ? DateTime.UtcNow : indexEntry.CreatedAtUtc,
                HasHostResults = payload.HostResults != null && payload.HostResults.Count > 0,
                HasDetails = payload.Details != null,
                RunFileName = NormalizeRunFileName(entryId, indexEntry.RunFileName)
            };

            var normalizedPayload = new HistoryRunPayload
            {
                Id = entryId,
                Output = payload.Output ?? string.Empty,
                HostResults = payload.HostResults,
                Details = payload.Details
            };

            EnsureStorageFolders();
            WriteJsonAtomic(GetRunFilePath(normalizedEntry), Serialize(normalizedPayload), createBackup: false);

            var document = ReadIndexDocument();
            document.Entries.RemoveAll(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
            document.Entries.Insert(0, normalizedEntry);

            EnforceRetention(document, maxEntries);
            WriteIndexDocument(document);
        }

        public bool DeleteRun(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            var normalizedId = entryId.Trim();
            var document = ReadIndexDocument();
            var removedEntries = document.Entries
                .Where(entry => string.Equals(entry.Id, normalizedId, StringComparison.Ordinal))
                .ToList();

            var removed = removedEntries.Count > 0;
            if (removed)
            {
                document.Entries.RemoveAll(entry => string.Equals(entry.Id, normalizedId, StringComparison.Ordinal));
                WriteIndexDocument(document);
            }

            foreach (var removedEntry in removedEntries)
            {
                removed |= DeleteRunFile(removedEntry);
            }

            removed |= DeleteRunFile(new HistoryIndexEntry
            {
                Id = normalizedId,
                RunFileName = $"{normalizedId}.json"
            });

            return removed;
        }

        public void DeleteAll()
        {
            var document = ReadIndexDocument();
            foreach (var entry in document.Entries)
            {
                DeleteRunFile(entry);
            }

            if (Directory.Exists(_runFolderPath))
            {
                foreach (var file in Directory.GetFiles(_runFolderPath, "*.json", SearchOption.TopDirectoryOnly))
                {
                    TryDeleteFile(file);
                }
            }

            document.Entries.Clear();
            WriteIndexDocument(document);
        }

        public int ImportLegacyHistory(IEnumerable<HistoryEntry> legacyEntries, int maxEntries)
        {
            if (legacyEntries == null)
                return 0;

            var cap = Math.Max(1, maxEntries);
            var importedEntries = new List<HistoryIndexEntry>();

            EnsureStorageFolders();
            foreach (var legacy in legacyEntries.Take(cap))
            {
                if (legacy == null)
                    continue;

                var entryId = string.IsNullOrWhiteSpace(legacy.Id)
                    ? HistoryIdGenerator.NewId()
                    : legacy.Id.Trim();
                var label = string.IsNullOrWhiteSpace(legacy.Timestamp)
                    ? entryId
                    : legacy.Timestamp;

                var indexEntry = new HistoryIndexEntry
                {
                    Id = entryId,
                    Label = label,
                    CreatedAtUtc = ParseCreatedAtUtc(label),
                    HasHostResults = legacy.HostResults != null && legacy.HostResults.Count > 0,
                    HasDetails = legacy.Details != null,
                    RunFileName = NormalizeRunFileName(entryId, $"{entryId}.json")
                };

                var payload = new HistoryRunPayload
                {
                    Id = entryId,
                    Output = legacy.Output ?? string.Empty,
                    HostResults = legacy.HostResults,
                    Details = legacy.Details
                };

                WriteJsonAtomic(GetRunFilePath(indexEntry), Serialize(payload), createBackup: false);
                importedEntries.Add(indexEntry);
            }

            if (importedEntries.Count == 0)
                return 0;

            var document = ReadIndexDocument();
            var importedIds = new HashSet<string>(importedEntries.Select(entry => entry.Id), StringComparer.Ordinal);
            var remaining = document.Entries
                .Where(entry => !importedIds.Contains(entry.Id))
                .ToList();

            document.Entries = importedEntries
                .Concat(remaining)
                .ToList();

            EnforceRetention(document, cap);
            WriteIndexDocument(document);
            return importedEntries.Count;
        }

        private HistoryIndexDocument ReadIndexDocument()
        {
            if (!File.Exists(_indexPath))
                return new HistoryIndexDocument();

            try
            {
                var json = File.ReadAllText(_indexPath);
                var parsed = JsonConvert.DeserializeObject<HistoryIndexDocument>(json);
                return NormalizeIndexDocument(parsed);
            }
            catch
            {
                TryBackupCorruptIndex();
                return new HistoryIndexDocument();
            }
        }

        private void WriteIndexDocument(HistoryIndexDocument document)
        {
            var normalized = NormalizeIndexDocument(document);
            WriteJsonAtomic(_indexPath, Serialize(normalized), createBackup: true);
        }

        private static string Serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value, Formatting.Indented);
        }

        private static DateTime ParseCreatedAtUtc(string label)
        {
            if (!string.IsNullOrWhiteSpace(label) &&
                label.Length >= 19 &&
                DateTime.TryParseExact(
                    label.Substring(0, 19),
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsedLocal))
            {
                return parsedLocal.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static HistoryIndexDocument NormalizeIndexDocument(HistoryIndexDocument? document)
        {
            var normalized = new HistoryIndexDocument
            {
                SchemaVersion = document?.SchemaVersion ?? 1,
                Entries = new List<HistoryIndexEntry>()
            };

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document?.Entries ?? Enumerable.Empty<HistoryIndexEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    continue;

                var id = entry.Id.Trim();
                if (!seenIds.Add(id))
                    continue;

                var label = string.IsNullOrWhiteSpace(entry.Label)
                    ? id
                    : entry.Label;
                var createdAt = entry.CreatedAtUtc == default
                    ? ParseCreatedAtUtc(label)
                    : entry.CreatedAtUtc;

                normalized.Entries.Add(new HistoryIndexEntry
                {
                    Id = id,
                    Label = label,
                    CreatedAtUtc = createdAt,
                    HasHostResults = entry.HasHostResults,
                    HasDetails = entry.HasDetails,
                    RunFileName = NormalizeRunFileName(id, entry.RunFileName)
                });
            }

            return normalized;
        }

        private void EnforceRetention(HistoryIndexDocument document, int maxEntries)
        {
            var cap = Math.Max(1, maxEntries);
            while (document.Entries.Count > cap)
            {
                var removed = document.Entries[^1];
                document.Entries.RemoveAt(document.Entries.Count - 1);
                DeleteRunFile(removed);
            }
        }

        private static string NormalizeRunFileName(string entryId, string? runFileName)
        {
            var fileName = string.IsNullOrWhiteSpace(runFileName)
                ? $"{entryId}.json"
                : Path.GetFileName(runFileName);

            return string.IsNullOrWhiteSpace(fileName)
                ? $"{entryId}.json"
                : fileName;
        }

        private void EnsureStorageFolders()
        {
            if (!Directory.Exists(_runFolderPath))
            {
                Directory.CreateDirectory(_runFolderPath);
            }
        }

        private string GetRunFilePath(HistoryIndexEntry indexEntry)
        {
            return Path.Combine(_runFolderPath, NormalizeRunFileName(indexEntry.Id, indexEntry.RunFileName));
        }

        private string GetDefaultRunFilePath(string entryId)
        {
            return Path.Combine(_runFolderPath, $"{entryId}.json");
        }

        private bool DeleteRunFile(HistoryIndexEntry indexEntry)
        {
            var path = GetRunFilePath(indexEntry);
            return TryDeleteFile(path);
        }

        private static bool TryDeleteFile(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void TryBackupCorruptIndex()
        {
            try
            {
                if (!File.Exists(_indexPath))
                    return;

                var corruptPath = $"{_indexPath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Copy(_indexPath, corruptPath, overwrite: false);
            }
            catch
            {
                // Best effort.
            }
        }

        private static void WriteJsonAtomic(string path, string json, bool createBackup)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json, Utf8NoBom);

                if (File.Exists(path))
                {
                    if (createBackup)
                    {
                        try
                        {
                            File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
                            return;
                        }
                        catch
                        {
                            // Fall back to copy + move path replacement.
                            try
                            {
                                File.Copy(path, path + ".bak", overwrite: true);
                            }
                            catch
                            {
                                // Best effort backup.
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                            return;
                        }
                        catch
                        {
                            // Fall back to delete + move below.
                        }
                    }

                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Best effort cleanup.
                    }
                }
            }
        }
    }
}
