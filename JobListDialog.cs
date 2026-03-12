using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using SSH_Helper.Utilities;

namespace SSH_Helper
{
    /// <summary>
    /// Primary job management dashboard with split-panel layout: job list on top,
    /// run history on bottom. Provides CRUD, export/import, and live refresh.
    /// </summary>
    internal sealed class JobListDialog : Form
    {
        #region Fields

        private readonly JobStorageService _jobStorage;
        private readonly JobExecutionService _executionService;
        private readonly JobHistoryService _historyService;
        private readonly SchedulingService _schedulingService;
        private readonly PresetManager _presetManager;
        private readonly JobExportService _exportService;
        private readonly ICredentialProvider? _credentialProvider;
        private readonly Func<string, Task<bool>>? _runNowInvoker;
        private readonly Func<IReadOnlyList<Dictionary<string, string>>>? _getMainGridRows;
        private readonly Func<IReadOnlyList<string>>? _getMainGridColumns;
        private readonly bool _darkMode;
        private readonly string? _fontFamily;
        private readonly float _fontSize;

        private readonly ToolStrip _toolStrip;
        private readonly SplitContainer _mainSplit;
        private readonly DataGridView _gridJobs;
        private readonly DataGridView _gridHistory;
        private readonly ContextMenuStrip _jobContextMenu;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly Button _btnViewOutput;
        private readonly Button _btnClearHistory;
        private readonly Label _lblHistoryHeader;
        private ToolStripButton? _btnEditJob;
        private ToolStripButton? _btnRunNowJob;
        private ToolStripButton? _btnCancelJob;
        private ToolStripButton? _btnEnableDisableJob;
        private ToolStripButton? _btnDeleteJob;
        private ToolStripButton? _btnDuplicateJob;
        private ToolStripMenuItem? _menuEditJob;
        private ToolStripMenuItem? _menuRunNowJob;
        private ToolStripMenuItem? _menuCancelJob;
        private ToolStripMenuItem? _menuEnableDisableJob;
        private ToolStripMenuItem? _menuDeleteJob;
        private ToolStripMenuItem? _menuDuplicateJob;

        // Track selected job for preservation across refreshes
        private string? _selectedJobId;
        private string? _selectedHistoryRunFileName;
        private bool _suppressJobSelectionChanged;
        private bool _suppressHistorySelectionChanged;
        private readonly Dictionary<string, JobRunRecord> _visibleHistoryRuns = new(StringComparer.Ordinal);

        #endregion

        #region Constructor

        public JobListDialog(
            JobStorageService jobStorage,
            JobExecutionService executionService,
            JobHistoryService historyService,
            SchedulingService schedulingService,
            PresetManager presetManager,
            JobExportService exportService,
            ICredentialProvider? credentialProvider,
            Func<string, Task<bool>>? runNowInvoker,
            Func<IReadOnlyList<Dictionary<string, string>>>? getMainGridRows,
            Func<IReadOnlyList<string>>? getMainGridColumns,
            bool darkMode,
            string? fontFamily = null,
            float fontSize = 9f)
        {
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
            _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
            _presetManager = presetManager ?? throw new ArgumentNullException(nameof(presetManager));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _credentialProvider = credentialProvider;
            _runNowInvoker = runNowInvoker;
            _getMainGridRows = getMainGridRows;
            _getMainGridColumns = getMainGridColumns;
            _darkMode = darkMode;
            _fontFamily = fontFamily;
            _fontSize = fontSize;

            // Dialog chrome
            Text = "Scheduled Jobs";
            Size = new Size(1000, 700);
            MinimumSize = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;

            // Build toolbar
            _toolStrip = BuildToolStrip();

            // Build context menu
            _jobContextMenu = BuildContextMenu();

            // Build job grid
            _gridJobs = BuildJobGrid();

            // Build history panel
            _lblHistoryHeader = new Label
            {
                Text = "Run History",
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
                Padding = new Padding(4, 0, 0, 0)
            };

            _gridHistory = BuildHistoryGrid();

            _btnViewOutput = new Button { Text = "View Output", AutoSize = true, Margin = new Padding(4), Enabled = false };
            _btnClearHistory = new Button { Text = "Clear History", AutoSize = true, Margin = new Padding(4) };

            var historyButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4, 2, 4, 2)
            };
            historyButtonPanel.Controls.AddRange(new Control[] { _btnViewOutput, _btnClearHistory });

            // Build split container
            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BorderStyle = BorderStyle.None,
            };

            _mainSplit.Panel1.Controls.Add(_gridJobs);
            _mainSplit.Panel2.Controls.Add(_gridHistory);
            _mainSplit.Panel2.Controls.Add(_lblHistoryHeader);
            _mainSplit.Panel2.Controls.Add(historyButtonPanel);

            // Add controls to form (reverse order for docking)
            Controls.Add(_mainSplit);
            Controls.Add(_toolStrip);

            // Set splitter distance after layout
            Load += (_, _) =>
            {
                _mainSplit.SplitterDistance = (int)(_mainSplit.Height * 0.6);
            };

            // Apply theme
            if (!string.IsNullOrEmpty(fontFamily))
            {
                var font = new Font(fontFamily, fontSize);
                DialogTheme.SetDialogFont(this, font);
                _lblHistoryHeader.Font = new Font(fontFamily, fontSize, FontStyle.Bold);
            }

            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);
            DialogTheme.StyleDataGridView(_gridJobs, darkMode);
            DialogTheme.StyleDataGridView(_gridHistory, darkMode);
            DialogTheme.StyleButton(_btnViewOutput, darkMode);
            DialogTheme.StyleButton(_btnClearHistory, darkMode);

            // Wire events
            WireEvents();

            // Refresh timer (5 second interval)
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _refreshTimer.Tick += (_, _) => RefreshJobList();

            // Initial load and timer start
            Load += OnFormLoad;
            FormClosing += OnFormClosingHandler;
        }

        #endregion

        #region UI Construction

        private ToolStrip BuildToolStrip()
        {
            var strip = new ToolStrip();

            strip.Items.Add(CreateToolButton("New", "New", OnNewClick));
            _btnEditJob = CreateToolButton("Edit", "Edit", OnEditClick);
            _btnRunNowJob = CreateToolButton("Run Now", "RunNow", OnRunNowClick);
            _btnCancelJob = CreateToolButton("Cancel", "Cancel", OnCancelClick);
            _btnEnableDisableJob = CreateToolButton("Enable/Disable", "EnableDisable", OnEnableDisableClick);
            _btnDeleteJob = CreateToolButton("Delete", "Delete", OnDeleteClick);
            _btnDuplicateJob = CreateToolButton("Duplicate", "Duplicate", OnDuplicateClick);
            strip.Items.Add(_btnEditJob);
            strip.Items.Add(_btnRunNowJob);
            strip.Items.Add(_btnCancelJob);
            strip.Items.Add(_btnEnableDisableJob);
            strip.Items.Add(_btnDeleteJob);
            strip.Items.Add(_btnDuplicateJob);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(CreateToolButton("Export File", "ExportFile", OnExportFileClick));
            strip.Items.Add(CreateToolButton("Export Clipboard", "ExportClipboard", OnExportClipboardClick));
            strip.Items.Add(CreateToolButton("Import File", "ImportFile", OnImportFileClick));
            strip.Items.Add(CreateToolButton("Import Clipboard", "ImportClipboard", OnImportClipboardClick));

            if (_darkMode)
            {
                strip.BackColor = DialogTheme.DarkSurface1;
                strip.ForeColor = DialogTheme.DarkText;
                strip.Renderer = new ToolStripProfessionalRenderer(
                    new DarkToolStripColorTable());
            }

            return strip;
        }

        private static ToolStripButton CreateToolButton(string text, string name, EventHandler handler)
        {
            var btn = new ToolStripButton(text) { Name = name, DisplayStyle = ToolStripItemDisplayStyle.Text };
            btn.Click += handler;
            return btn;
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var menuNew = new ToolStripMenuItem("New Job");
            menuNew.Click += OnNewClick;

            _menuEditJob = new ToolStripMenuItem("Edit Job");
            _menuEditJob.Click += OnEditClick;

            _menuRunNowJob = new ToolStripMenuItem("Run Now");
            _menuRunNowJob.Click += OnRunNowClick;

            _menuCancelJob = new ToolStripMenuItem("Cancel");
            _menuCancelJob.Click += OnCancelClick;

            _menuEnableDisableJob = new ToolStripMenuItem("Enable/Disable");
            _menuEnableDisableJob.Click += OnEnableDisableClick;

            _menuDeleteJob = new ToolStripMenuItem("Delete");
            _menuDeleteJob.Click += OnDeleteClick;

            _menuDuplicateJob = new ToolStripMenuItem("Duplicate");
            _menuDuplicateJob.Click += OnDuplicateClick;

            menu.Items.AddRange(new ToolStripItem[]
            {
                menuNew,
                _menuEditJob,
                _menuRunNowJob,
                _menuCancelJob,
                new ToolStripSeparator(),
                _menuEnableDisableJob,
                _menuDeleteJob,
                _menuDuplicateJob,
                new ToolStripSeparator(),
                CreateMenuItem("Export to File", OnExportFileClick),
                CreateMenuItem("Export to Clipboard", OnExportClipboardClick),
                CreateMenuItem("Import from File", OnImportFileClick),
                CreateMenuItem("Import from Clipboard", OnImportClipboardClick)
            });
            menu.Opening += (_, _) => UpdateJobActionState();

            if (_darkMode)
            {
                menu.BackColor = DialogTheme.DarkSurface2;
                menu.ForeColor = DialogTheme.DarkText;
                menu.Renderer = new ToolStripProfessionalRenderer(
                    new DarkToolStripColorTable());
            }

            return menu;
        }

        private static ToolStripMenuItem CreateMenuItem(string text, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += handler;
            return item;
        }

        private DataGridView BuildJobGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ContextMenuStrip = _jobContextMenu,
            };

            grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn
                {
                    Name = "Name",
                    HeaderText = "Name",
                    FillWeight = 25,
                    MinimumWidth = 80
                },
                new DataGridViewCheckBoxColumn
                {
                    Name = "Enabled",
                    HeaderText = "On",
                    FillWeight = 5,
                    MinimumWidth = 35,
                    ReadOnly = true
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Schedule",
                    HeaderText = "Schedule",
                    FillWeight = 20,
                    MinimumWidth = 80
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "NextRun",
                    HeaderText = "Next Run",
                    FillWeight = 15,
                    MinimumWidth = 80
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "LastResult",
                    HeaderText = "Last Result",
                    FillWeight = 20,
                    MinimumWidth = 70
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Target",
                    HeaderText = "Target",
                    FillWeight = 15,
                    MinimumWidth = 70
                }
            });

            return grid;
        }

        private DataGridView BuildHistoryGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };

            grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn
                {
                    Name = "Started",
                    HeaderText = "Started",
                    FillWeight = 20,
                    MinimumWidth = 100
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Duration",
                    HeaderText = "Duration",
                    FillWeight = 10,
                    MinimumWidth = 60
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Result",
                    HeaderText = "Result",
                    FillWeight = 15,
                    MinimumWidth = 60
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Error",
                    HeaderText = "Error",
                    FillWeight = 30,
                    MinimumWidth = 80
                }
            });

            return grid;
        }

        #endregion

        #region Event Wiring

        private void WireEvents()
        {
            // Grid events
            _gridJobs.SelectionChanged += OnJobSelectionChanged;
            _gridJobs.CellMouseDown += OnJobGridCellMouseDown;
            _gridJobs.CellContentClick += OnJobGridCellContentClick;
            _gridJobs.CellDoubleClick += OnJobGridDoubleClick;
            _gridHistory.SelectionChanged += OnHistorySelectionChanged;
            _gridHistory.CellDoubleClick += OnHistoryGridDoubleClick;

            // Button events
            _btnViewOutput.Click += OnViewOutputClick;
            _btnClearHistory.Click += OnClearHistoryClick;

            // Keyboard shortcuts
            KeyDown += OnKeyDown;

            // External change events
            _jobStorage.JobsChanged += OnJobsChangedExternal;
            _executionService.JobStateChanged += OnJobStateChangedExternal;
            _executionService.JobCompleted += OnJobCompletedExternal;
        }

        private void OnFormLoad(object? sender, EventArgs e)
        {
            DialogTheme.ApplyNativeTheme(this, _darkMode);
            RefreshJobList();
            _refreshTimer.Start();
        }

        private void OnFormClosingHandler(object? sender, FormClosingEventArgs e)
        {
            // Stop timer
            _refreshTimer.Stop();
            _refreshTimer.Dispose();

            // Unsubscribe external events
            _jobStorage.JobsChanged -= OnJobsChangedExternal;
            _executionService.JobStateChanged -= OnJobStateChangedExternal;
            _executionService.JobCompleted -= OnJobCompletedExternal;
        }

        private void OnJobsChangedExternal(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(RefreshJobList);
                return;
            }
            RefreshJobList();
        }

        private void OnJobStateChangedExternal(object? sender, JobExecutionService.JobStateChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(RefreshJobList);
                return;
            }
            RefreshJobList();
        }

        private void OnJobCompletedExternal(object? sender, JobRunResult e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(RefreshJobList);
                return;
            }
            RefreshJobList();
        }

        #endregion

        #region Grid Refresh

        private void RefreshJobList()
        {
            // Preserve the logical active job even if WinForms clears selection during the rebuild.
            var savedJobId = _selectedJobId;
            var currentJobId = GetCurrentGridJobId();

            _gridJobs.SuspendLayout();
            _suppressJobSelectionChanged = true;

            try
            {
                _gridJobs.Rows.Clear();

                var jobs = _jobStorage.Jobs.Values.OrderBy(j => j.Name).ToList();

                foreach (var job in jobs)
                {
                    // Compute schedule description
                    var scheduleText = GetScheduleDescription(job);

                    // Compute next run
                    var nextRunText = GetNextRunText(job);

                    // Compute last result
                    var lastResultText = GetLastResultText(job);

                    // Target display
                    var targetText = job.TargetType switch
                    {
                        JobTargetType.Folder => $"[F] {job.TargetName}",
                        JobTargetType.CustomPreset => "[Custom] Scheduler-local content",
                        _ => job.TargetName
                    };

                    var rowIndex = _gridJobs.Rows.Add(
                        job.Name,
                        job.IsEnabled,
                        scheduleText,
                        nextRunText,
                        lastResultText,
                        targetText);

                    var row = _gridJobs.Rows[rowIndex];
                    row.Tag = job.Id;

                    // Visual indicators for running jobs
                    var nameCell = row.Cells["Name"];
                    if (_executionService.IsJobRunning(job.Id))
                    {
                        nameCell.Style.ForeColor = _darkMode
                            ? Color.FromArgb(0, 255, 0)  // Lime for dark mode
                            : Color.FromArgb(0, 128, 0);  // Green for light mode
                    }

                    // Disabled job visual
                    if (!job.IsEnabled)
                    {
                        row.DefaultCellStyle.ForeColor = _darkMode
                            ? DialogTheme.DarkSecondaryText
                            : DialogTheme.LightSecondaryText;
                    }
                }

                _selectedJobId = SelectActiveJob(savedJobId, currentJobId);
            }
            finally
            {
                _suppressJobSelectionChanged = false;
                _gridJobs.ResumeLayout();
            }

            RefreshHistory(_selectedJobId);
            UpdateJobActionState();
        }

        private string? SelectActiveJob(string? preferredJobId, string? secondaryJobId)
        {
            _gridJobs.ClearSelection();

            if (TrySelectJobRow(preferredJobId))
                return preferredJobId;

            if (TrySelectJobRow(secondaryJobId))
                return secondaryJobId;

            foreach (DataGridViewRow row in _gridJobs.Rows)
            {
                if (TrySelectJobRow(row.Tag as string))
                {
                    return row.Tag as string;
                }
            }

            _gridJobs.CurrentCell = null;
            return null;
        }

        private bool TrySelectJobRow(string? jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                return false;
            }

            foreach (DataGridViewRow row in _gridJobs.Rows)
            {
                if (!string.Equals(row.Tag as string, jobId, StringComparison.Ordinal))
                    continue;

                row.Selected = true;
                _gridJobs.CurrentCell = row.Cells[0];
                return true;
            }

            return false;
        }

        private string? SelectJobRowAt(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _gridJobs.Rows.Count)
            {
                return null;
            }

            var row = _gridJobs.Rows[rowIndex];
            if (row.Tag is not string jobId)
            {
                return null;
            }

            var targetColumnIndex = columnIndex >= 0 && columnIndex < row.Cells.Count
                ? columnIndex
                : 0;
            var targetCell = row.Cells[targetColumnIndex];

            if (_gridJobs.CurrentCell == targetCell && row.Selected)
            {
                _selectedJobId = jobId;
                return jobId;
            }

            _gridJobs.ClearSelection();
            _gridJobs.CurrentCell = targetCell;
            row.Selected = true;
            _selectedJobId = jobId;
            return jobId;
        }

        private void RefreshHistory()
            => RefreshHistory(GetActiveJobId());

        private void RefreshHistory(string? jobId)
        {
            var savedRunFileName = _selectedHistoryRunFileName ?? GetCurrentGridHistoryRunFileName();

            _gridHistory.SuspendLayout();
            _suppressHistorySelectionChanged = true;

            try
            {
                _gridHistory.Rows.Clear();
                _visibleHistoryRuns.Clear();

                if (string.IsNullOrEmpty(jobId))
                {
                    _selectedHistoryRunFileName = null;
                    _gridHistory.CurrentCell = null;
                    UpdateHistoryActionState();
                    return;
                }

                var runs = _historyService.GetRunsForJob(jobId);

                foreach (var run in runs)
                {
                    var startedText = run.StartedUtc.ToLocalTime().ToString("g");
                    var duration = run.CompletedUtc >= run.StartedUtc
                        ? run.CompletedUtc - run.StartedUtc
                        : TimeSpan.Zero;
                    var durationText = duration.TotalHours >= 1
                        ? duration.ToString(@"hh\:mm\:ss")
                        : duration.ToString(@"mm\:ss");

                    var resultText = GetRunResultText(run);

                    var errorText = run.ErrorMessage ?? string.Empty;

                    var rowIndex = _gridHistory.Rows.Add(
                        startedText,
                        durationText,
                        resultText,
                        errorText);

                    var row = _gridHistory.Rows[rowIndex];
                    row.Tag = run.RunFileName;
                    _visibleHistoryRuns[run.RunFileName] = run;

                    // Color-code result
                    var resultCell = row.Cells["Result"];
                    if (run.WasSkipped)
                    {
                        resultCell.Style.ForeColor = Color.Orange;
                    }
                    else if (run.WasCancelled)
                    {
                        resultCell.Style.ForeColor = Color.Goldenrod;
                    }
                    else if (run.Success)
                    {
                        resultCell.Style.ForeColor = _darkMode
                            ? Color.FromArgb(0, 255, 0)
                            : Color.FromArgb(0, 128, 0);
                    }
                    else
                    {
                        resultCell.Style.ForeColor = Color.FromArgb(220, 53, 69);
                    }
                }

                _selectedHistoryRunFileName = SelectActiveHistoryRun(savedRunFileName);
                UpdateHistoryActionState();
            }
            finally
            {
                _suppressHistorySelectionChanged = false;
                _gridHistory.ResumeLayout();
            }
        }

        #endregion

        #region Helper Methods

        private string GetScheduleDescription(JobDefinition job)
        {
            return job.ScheduleType switch
            {
                ScheduleType.Recurring => _schedulingService.GetDescription(job.CronExpression) ?? job.CronExpression ?? "Unknown",
                ScheduleType.OneTime => job.OneTimeScheduleUtc?.ToLocalTime().ToString("g") ?? "Not set",
                _ => "On demand"
            };
        }

        private string GetNextRunText(JobDefinition job)
        {
            if (!job.IsEnabled)
                return "Disabled";

            if (job.ScheduleType == ScheduleType.Recurring)
            {
                var nextRun = _schedulingService.GetNextRunLocal(job.CronExpression);
                return nextRun?.ToString("g") ?? "N/A";
            }

            if (job.ScheduleType == ScheduleType.OneTime)
            {
                if (job.OneTimeScheduleUtc.HasValue && job.OneTimeScheduleUtc.Value > DateTime.UtcNow)
                    return job.OneTimeScheduleUtc.Value.ToLocalTime().ToString("g");
                return "Completed";
            }

            return "-";
        }

        private string GetLastResultText(JobDefinition job)
        {
            var runs = _historyService.GetRunsForJob(job.Id, new JobRunFilter { MaxResults = 1 });
            if (runs.Count == 0)
                return "Never run";

            var lastRun = runs[0];
            return GetRunResultText(lastRun);
        }

        private string? GetSelectedJobId()
        {
            if (_gridJobs.SelectedRows.Count == 0)
                return null;
            return _gridJobs.SelectedRows[0].Tag as string;
        }

        private string? GetCurrentGridJobId()
        {
            if (_gridJobs.CurrentRow?.Tag is string currentRowJobId)
                return currentRowJobId;

            return GetSelectedJobId();
        }

        private string? GetActiveJobId()
            => _selectedJobId ?? GetCurrentGridJobId();

        private List<JobDefinition> GetSelectedJobs()
        {
            var jobs = new List<JobDefinition>();
            var selectedRows = _gridJobs.SelectedRows;

            if (selectedRows.Count == 0)
            {
                var activeJobId = GetActiveJobId();
                if (activeJobId == null)
                    return jobs;

                var activeJob = _jobStorage.Get(activeJobId);
                if (activeJob != null)
                    jobs.Add(activeJob);

                return jobs;
            }

            foreach (DataGridViewRow row in selectedRows)
            {
                var jobId = row.Tag as string;
                if (jobId != null)
                {
                    var job = _jobStorage.Get(jobId);
                    if (job != null)
                        jobs.Add(job);
                }
            }
            return jobs;
        }

        private string? GetSelectedHistoryRunFileName()
        {
            if (_gridHistory.SelectedRows.Count == 0)
                return null;
            return _gridHistory.SelectedRows[0].Tag as string;
        }

        private string? GetCurrentGridHistoryRunFileName()
        {
            if (_gridHistory.CurrentRow?.Tag is string currentRowRunFileName)
                return currentRowRunFileName;

            return GetSelectedHistoryRunFileName();
        }

        private string? GetActiveHistoryRunFileName()
            => _selectedHistoryRunFileName ?? GetCurrentGridHistoryRunFileName();

        private JobRunRecord? GetActiveHistoryRunRecord()
        {
            var runFileName = GetActiveHistoryRunFileName();
            if (string.IsNullOrEmpty(runFileName))
                return null;

            return _visibleHistoryRuns.TryGetValue(runFileName, out var run)
                ? run
                : null;
        }

        private string? SelectActiveHistoryRun(string? preferredRunFileName)
        {
            _gridHistory.ClearSelection();

            if (TrySelectHistoryRunRow(preferredRunFileName))
                return preferredRunFileName;

            foreach (DataGridViewRow row in _gridHistory.Rows)
            {
                if (TrySelectHistoryRunRow(row.Tag as string))
                {
                    return row.Tag as string;
                }
            }

            _gridHistory.CurrentCell = null;
            return null;
        }

        private bool TrySelectHistoryRunRow(string? runFileName)
        {
            if (string.IsNullOrEmpty(runFileName))
                return false;

            foreach (DataGridViewRow row in _gridHistory.Rows)
            {
                if (!string.Equals(row.Tag as string, runFileName, StringComparison.Ordinal))
                    continue;

                row.Selected = true;
                _gridHistory.CurrentCell = row.Cells[0];
                return true;
            }

            return false;
        }

        private void UpdateHistoryActionState()
        {
            var run = GetActiveHistoryRunRecord();
            _btnViewOutput.Enabled = run != null && !IsSkippedSummary(run);
        }

        private void UpdateJobActionState()
        {
            var jobId = GetActiveJobId();
            var hasJob = !string.IsNullOrEmpty(jobId);
            var isRunning = hasJob && _executionService.IsJobRunning(jobId!);

            SetEnabled(_btnEditJob, hasJob);
            SetEnabled(_btnRunNowJob, hasJob && !isRunning);
            SetEnabled(_btnCancelJob, hasJob && isRunning);
            SetEnabled(_btnEnableDisableJob, hasJob);
            SetEnabled(_btnDeleteJob, hasJob);
            SetEnabled(_btnDuplicateJob, hasJob);
            SetEnabled(_menuEditJob, hasJob);
            SetEnabled(_menuRunNowJob, hasJob && !isRunning);
            SetEnabled(_menuCancelJob, hasJob && isRunning);
            SetEnabled(_menuEnableDisableJob, hasJob);
            SetEnabled(_menuDeleteJob, hasJob);
            SetEnabled(_menuDuplicateJob, hasJob);
        }

        private static void SetEnabled(ToolStripItem? item, bool enabled)
        {
            if (item != null)
            {
                item.Enabled = enabled;
            }
        }

        private static bool IsSkippedSummary(JobRunRecord run)
            => run.WasSkipped && run.SkippedRunCount > 0;

        private static int GetSkippedRunCount(JobRunRecord run)
            => run.WasSkipped ? Math.Max(run.SkippedRunCount, 1) : 0;

        private static int GetConsecutiveFailureCount(JobRunRecord run)
            => !run.Success && !run.WasSkipped ? Math.Max(run.ConsecutiveFailureCount, 1) : 0;

        private static string GetRunResultText(JobRunRecord run)
        {
            if (run.WasSkipped)
            {
                var skippedRunCount = GetSkippedRunCount(run);
                return skippedRunCount > 1
                    ? $"SKIPPED ({skippedRunCount})"
                    : "SKIPPED";
            }

            var total = run.HostsSucceeded + run.HostsFailed;
            if (run.WasCancelled)
            {
                return total > 0
                    ? $"CANCELLED ({run.HostsSucceeded}/{total})"
                    : "CANCELLED";
            }

            return run.Success
                ? $"OK ({run.HostsSucceeded}/{total})"
                : GetConsecutiveFailureCount(run) > 1
                    ? $"FAIL x{GetConsecutiveFailureCount(run)} ({run.HostsSucceeded}/{total})"
                    : $"FAIL ({run.HostsSucceeded}/{total})";
        }

        #endregion

        #region Grid Event Handlers

        private void OnJobSelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressJobSelectionChanged)
                return;

            _selectedJobId = GetCurrentGridJobId();
            RefreshHistory(_selectedJobId);
            UpdateJobActionState();
        }

        private void OnHistorySelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressHistorySelectionChanged)
                return;

            _selectedHistoryRunFileName = GetCurrentGridHistoryRunFileName();
            UpdateHistoryActionState();
        }

        private void OnJobGridDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                EditSelectedJob();
        }

        private void OnJobGridCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            SelectJobRowAt(e.RowIndex, e.ColumnIndex);
        }

        private void OnJobGridCellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (!string.Equals(_gridJobs.Columns[e.ColumnIndex].Name, "Enabled", StringComparison.Ordinal))
                return;

            var jobId = SelectJobRowAt(e.RowIndex, e.ColumnIndex);
            if (jobId == null)
                return;

            ToggleJobEnabled(jobId);
        }

        private void OnHistoryGridDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                ViewSelectedOutput();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (_gridJobs.Focused || _gridJobs.ContainsFocus)
            {
                switch (e.KeyCode)
                {
                    case Keys.Delete:
                        e.Handled = true;
                        DeleteSelectedJob();
                        break;
                    case Keys.Enter:
                    case Keys.F2:
                        e.Handled = true;
                        EditSelectedJob();
                        break;
                    case Keys.F5:
                        e.Handled = true;
                        RefreshJobList();
                        break;
                }
            }
        }

        #endregion

        #region Toolbar Action Handlers

        private void OnNewClick(object? sender, EventArgs e)
        {
            using var editor = new JobEditorDialog(
                null, _presetManager, _schedulingService,
                _credentialProvider,
                _getMainGridRows, _getMainGridColumns,
                _darkMode, _fontFamily, _fontSize);

            if (editor.ShowDialog(this) == DialogResult.OK && editor.Result != null)
            {
                try
                {
                    _jobStorage.Save(editor.Result);
                    RefreshJobList();
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Failed to save job: {ex.Message}",
                        "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnEditClick(object? sender, EventArgs e)
        {
            EditSelectedJob();
        }

        private async void OnRunNowClick(object? sender, EventArgs e)
        {
            var jobId = GetActiveJobId();
            if (jobId == null) return;

            var job = _jobStorage.Get(jobId);
            if (job == null) return;

            var result = await RunNowJobAsync(jobId);
            if (!result)
            {
                DialogTheme.Show(this,
                    $"Cannot run job '{job.Name}'. It may already be running.",
                    "Run Now", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnCancelClick(object? sender, EventArgs e)
        {
            var jobId = GetActiveJobId();
            if (jobId == null) return;

            var job = _jobStorage.Get(jobId);
            if (job == null) return;

            if (!_executionService.CancelJob(jobId))
            {
                DialogTheme.Show(this,
                    $"Job '{job.Name}' is not currently running.",
                    "Cancel Job", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            RefreshJobList();
        }

        private void OnEnableDisableClick(object? sender, EventArgs e)
        {
            var jobId = GetActiveJobId();
            if (jobId == null) return;

            ToggleJobEnabled(jobId);
        }

        private void OnDeleteClick(object? sender, EventArgs e)
        {
            DeleteSelectedJob();
        }

        private void OnDuplicateClick(object? sender, EventArgs e)
        {
            var jobId = GetActiveJobId();
            if (jobId == null) return;

            var job = _jobStorage.Get(jobId);
            if (job == null) return;

            // Deep clone via JSON serialization
            var json = JsonConvert.SerializeObject(job);
            var clone = JsonConvert.DeserializeObject<JobDefinition>(json);
            if (clone == null) return;

            clone.Id = Guid.NewGuid().ToString("N");
            clone.Name = $"{clone.Name} (copy)";
            clone.ModifiedUtc = DateTime.UtcNow;
            clone.CreatedUtc = DateTime.UtcNow;
            clone.RunningState = null;

            try
            {
                SaveDuplicatedJob(job, clone);
                RefreshJobList();
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to duplicate job: {ex.Message}",
                    "Duplicate Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExportFileClick(object? sender, EventArgs e)
        {
            var selectedJobs = GetSelectedJobs();
            if (selectedJobs.Count == 0)
            {
                DialogTheme.Show(this, "No jobs selected for export.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = ".sshjobs files (*.sshjobs)|*.sshjobs",
                DefaultExt = ".sshjobs",
                FileName = selectedJobs.Count == 1 ? selectedJobs[0].Name : "exported-jobs"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    _exportService.ExportToFile(selectedJobs, dlg.FileName);
                    DialogTheme.Show(this,
                        $"Exported {selectedJobs.Count} job(s) to {Path.GetFileName(dlg.FileName)}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Export failed: {ex.Message}",
                        "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnExportClipboardClick(object? sender, EventArgs e)
        {
            var selectedJobs = GetSelectedJobs();
            if (selectedJobs.Count == 0)
            {
                DialogTheme.Show(this, "No jobs selected for export.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var encoded = _exportService.ExportToString(selectedJobs);
                Clipboard.SetText(encoded);
                DialogTheme.Show(this,
                    $"Copied {selectedJobs.Count} job(s) to clipboard",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Export failed: {ex.Message}",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnImportFileClick(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = ".sshjobs files (*.sshjobs)|*.sshjobs",
                DefaultExt = ".sshjobs"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var importedJobs = _exportService.ImportFromFile(dlg.FileName);
                ProcessImportedJobs(importedJobs);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Could not read job file: {ex.Message}",
                    "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnImportClipboardClick(object? sender, EventArgs e)
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                DialogTheme.Show(this, "Clipboard is empty.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var importedJobs = _exportService.ImportFromString(text);
                ProcessImportedJobs(importedJobs);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Could not parse clipboard data: {ex.Message}",
                    "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region History Actions

        private void OnViewOutputClick(object? sender, EventArgs e)
        {
            ViewSelectedOutput();
        }

        private void OnClearHistoryClick(object? sender, EventArgs e)
        {
            var jobId = GetActiveJobId();
            if (jobId == null) return;

            var job = _jobStorage.Get(jobId);
            var jobName = job?.Name ?? "this job";

            if (!DialogTheme.Confirm(this,
                $"Clear all history for '{jobName}'?",
                "Clear History", _darkMode))
                return;

            ClearHistoryForJob(jobId);
        }

        #endregion

        #region Action Helpers

        private void ToggleJobEnabled(string jobId)
        {
            var job = _jobStorage.Get(jobId);
            if (job == null)
                return;

            job.IsEnabled = !job.IsEnabled;
            if (job.IsEnabled)
                job.DisabledReason = null;

            try
            {
                _jobStorage.Save(job);
                RefreshJobList();
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this, $"Failed to update job: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveDuplicatedJob(JobDefinition sourceJob, JobDefinition duplicateJob)
        {
            _jobStorage.Save(duplicateJob);

            try
            {
                CopyStoredCredentialForDuplicate(sourceJob, duplicateJob);
            }
            catch
            {
                _jobStorage.Delete(duplicateJob.Id, cleanupCredentials: false);
                throw;
            }
        }

        private void CopyStoredCredentialForDuplicate(JobDefinition sourceJob, JobDefinition duplicateJob)
        {
            if (sourceJob.CredentialMode != CredentialMode.Stored || _credentialProvider?.IsAvailable != true)
            {
                return;
            }

            var sourceTarget = CredentialTargets.JobPasswordTarget(sourceJob.Id);
            if (!_credentialProvider.TryGetPassword(sourceTarget, out var username, out var password))
            {
                return;
            }

            var duplicateTarget = CredentialTargets.JobPasswordTarget(duplicateJob.Id);
            if (!_credentialProvider.SavePassword(
                    duplicateTarget,
                    username,
                    password,
                    "Scheduler job credential"))
            {
                throw new InvalidOperationException("Failed to copy the duplicated job credential.");
            }
        }

        private void ClearHistoryForJob(string jobId)
        {
            _historyService.DeleteAllHistory(jobId);
            RefreshJobList();
        }

        private void EditSelectedJob()
        {
            var jobId = GetActiveJobId();
            if (jobId == null) return;

            var job = _jobStorage.Get(jobId);
            if (job == null) return;

            using var editor = new JobEditorDialog(
                job, _presetManager, _schedulingService,
                _credentialProvider,
                _getMainGridRows, _getMainGridColumns,
                _darkMode, _fontFamily, _fontSize);

            if (editor.ShowDialog(this) == DialogResult.OK && editor.Result != null)
            {
                try
                {
                    _jobStorage.Save(editor.Result);
                    RefreshJobList();
                }
                catch (Exception ex)
                {
                    DialogTheme.Show(this, $"Failed to save job: {ex.Message}",
                        "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteSelectedJob()
        {
            var jobId = GetActiveJobId();
            if (jobId == null) return;

            var job = _jobStorage.Get(jobId);
            if (job == null) return;

            if (!DialogTheme.Confirm(this,
                $"Delete job '{job.Name}'?",
                "Delete Job", _darkMode))
                return;

            _jobStorage.Delete(jobId);
            _historyService.DeleteAllHistory(jobId);
            RefreshJobList();
        }

        private void ViewSelectedOutput()
        {
            var jobId = GetActiveJobId();
            var runFileName = GetActiveHistoryRunFileName();

            if (jobId == null || runFileName == null)
            {
                DialogTheme.Show(this, "Select a run in the history list to view output.",
                    "View Output", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var run = GetActiveHistoryRunRecord();
            if (run != null && IsSkippedSummary(run))
            {
                DialogTheme.Show(this,
                    "Output is not available for skipped downtime summary entries.",
                    "View Output", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var payload = _historyService.LoadRunPayload(jobId, runFileName);
            if (payload == null)
            {
                DialogTheme.Show(this, "Could not load run output.",
                    "View Output", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var viewer = new RunOutputViewerDialog(payload, _darkMode, _fontFamily, _fontSize);
            viewer.ShowDialog(this);
        }

        private void ProcessImportedJobs(List<JobDefinition> importedJobs)
        {
            if (importedJobs.Count == 0)
            {
                DialogTheme.Show(this, "No valid jobs found in the import data.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var existingNames = _jobStorage.Jobs.Values
                .Select(j => j.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var entries = _exportService.PrepareImport(importedJobs, existingNames);

            // Set MissingTarget flag by checking if target preset/folder exists
            foreach (var entry in entries)
            {
                if (entry.Job.TargetType == JobTargetType.Preset)
                {
                    entry.MissingTarget = !_presetManager.Presets.ContainsKey(entry.Job.TargetName);
                }
                else if (entry.Job.TargetType == JobTargetType.Folder)
                {
                    entry.MissingTarget = !_presetManager.Folders.ContainsKey(entry.Job.TargetName);
                }
                else
                {
                    entry.MissingTarget = false;
                }
            }

            using var preview = new ImportPreviewDialog(entries, _darkMode, _fontFamily, _fontSize);
            if (preview.ShowDialog(this) != DialogResult.OK || preview.AcceptedEntries == null)
                return;

            var (savedCount, failures) = CommitImportedEntries(_jobStorage, preview.AcceptedEntries);

            RefreshJobList();

            DialogTheme.Show(this,
                BuildImportCompletionMessage(savedCount, failures),
                "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        internal static (int SavedCount, IReadOnlyList<string> Failures) CommitImportedEntries(
            JobStorageService jobStorage,
            IEnumerable<JobExportService.ImportJobEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(jobStorage);
            ArgumentNullException.ThrowIfNull(entries);

            var savedCount = 0;
            var failures = new List<string>();

            foreach (var entry in entries)
            {
                try
                {
                    entry.Job.Name = entry.ResolvedName;
                    if (entry.MissingTarget)
                    {
                        SchedulerJobIntegrityUtilities.ApplyMissingTargetImportState(entry.Job);
                    }

                    jobStorage.Save(entry.Job);
                    savedCount++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{entry.ResolvedName}: {ex.GetBaseException().Message}");
                }
            }

            return (savedCount, failures);
        }

        internal static string BuildImportCompletionMessage(int savedCount, IReadOnlyList<string> failures)
        {
            failures ??= Array.Empty<string>();

            if (savedCount > 0 && failures.Count == 0)
                return $"Imported {savedCount} job(s) successfully.";

            if (savedCount == 0 && failures.Count == 0)
                return "No jobs were imported.";

            var heading = savedCount > 0
                ? $"Imported {savedCount} job(s). {failures.Count} job(s) failed to import."
                : $"No jobs were imported. {failures.Count} job(s) failed to import.";

            return heading + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, failures);
        }

        private Task<bool> RunNowJobAsync(string jobId)
        {
            if (_runNowInvoker != null)
            {
                return _runNowInvoker(jobId);
            }

            return _executionService.RunNowAsync(jobId);
        }

        #endregion

        #region Dark Mode ToolStrip Support

        /// <summary>
        /// Provides dark-mode colors for the ToolStrip renderer.
        /// </summary>
        private sealed class DarkToolStripColorTable : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin => DialogTheme.DarkSurface1;
            public override Color ToolStripGradientMiddle => DialogTheme.DarkSurface1;
            public override Color ToolStripGradientEnd => DialogTheme.DarkSurface1;
            public override Color ToolStripBorder => DialogTheme.DarkBorder;
            public override Color MenuItemSelected => DialogTheme.DarkSurface2;
            public override Color MenuItemSelectedGradientBegin => DialogTheme.DarkSurface2;
            public override Color MenuItemSelectedGradientEnd => DialogTheme.DarkSurface2;
            public override Color MenuItemBorder => DialogTheme.DarkBorder;
            public override Color MenuStripGradientBegin => DialogTheme.DarkSurface1;
            public override Color MenuStripGradientEnd => DialogTheme.DarkSurface1;
            public override Color MenuItemPressedGradientBegin => DialogTheme.DarkSurface2;
            public override Color MenuItemPressedGradientEnd => DialogTheme.DarkSurface2;
            public override Color ImageMarginGradientBegin => DialogTheme.DarkSurface1;
            public override Color ImageMarginGradientMiddle => DialogTheme.DarkSurface1;
            public override Color ImageMarginGradientEnd => DialogTheme.DarkSurface1;
            public override Color SeparatorDark => DialogTheme.DarkBorder;
            public override Color SeparatorLight => DialogTheme.DarkSurface2;
            public override Color ButtonSelectedHighlight => DialogTheme.DarkSurface2;
            public override Color ButtonSelectedBorder => DialogTheme.DarkBorder;
            public override Color ButtonPressedHighlight => DialogTheme.DarkSurface2;
            public override Color ButtonPressedBorder => DialogTheme.DarkBorder;
            public override Color ButtonCheckedHighlight => DialogTheme.DarkSurface2;
        }

        #endregion
    }
}
