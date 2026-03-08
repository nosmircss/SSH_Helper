using SSH_Helper.Services;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Shows a preview of jobs to import with conflict resolution.
    /// Stub: full implementation in Plan 05-02.
    /// </summary>
    internal sealed class ImportPreviewDialog : Form
    {
        public IReadOnlyList<JobExportService.ImportJobEntry>? AcceptedEntries { get; private set; }

        public ImportPreviewDialog(IReadOnlyList<JobExportService.ImportJobEntry> entries,
            bool darkMode, string? fontFamily = null, float fontSize = 9f)
        {
            Text = "Import Preview";
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
        }
    }
}
