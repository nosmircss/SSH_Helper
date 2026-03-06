namespace SSH_Helper.Models
{
    /// <summary>
    /// Lightweight file identity used to detect when a remembered CSV changed on disk.
    /// </summary>
    public sealed class CsvFileFingerprint
    {
        public DateTime LastWriteTimeUtc { get; set; }
        public long FileSizeBytes { get; set; }

        public CsvFileFingerprint Clone()
        {
            return new CsvFileFingerprint
            {
                LastWriteTimeUtc = LastWriteTimeUtc,
                FileSizeBytes = FileSizeBytes
            };
        }

        public void Normalize()
        {
            if (LastWriteTimeUtc.Kind == DateTimeKind.Local)
            {
                LastWriteTimeUtc = LastWriteTimeUtc.ToUniversalTime();
            }
            else if (LastWriteTimeUtc.Kind == DateTimeKind.Unspecified)
            {
                LastWriteTimeUtc = DateTime.SpecifyKind(LastWriteTimeUtc, DateTimeKind.Utc);
            }

            if (FileSizeBytes < 0)
            {
                FileSizeBytes = 0;
            }
        }
    }
}
