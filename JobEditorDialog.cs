using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Tabbed modal dialog for creating and editing job definitions.
    /// Stub: full implementation in Plan 05-03.
    /// </summary>
    internal sealed class JobEditorDialog : Form
    {
        public JobDefinition? Result { get; private set; }

        public JobEditorDialog(
            JobDefinition? existingJob,
            PresetManager presetManager,
            SchedulingService schedulingService,
            Func<IReadOnlyList<Dictionary<string, string>>>? getMainGridRows,
            Func<IReadOnlyList<string>>? getMainGridColumns,
            bool darkMode,
            string? fontFamily = null,
            float fontSize = 9f)
        {
            Text = existingJob == null ? "New Job" : $"Edit Job - {existingJob.Name}";
            Size = new Size(700, 600);
            StartPosition = FormStartPosition.CenterParent;
        }
    }
}
