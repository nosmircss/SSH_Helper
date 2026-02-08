using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Dialog for managing named environments and environment-level variables.
    /// </summary>
    internal sealed class EnvironmentDialog : Form
    {
        private static readonly Color HistoryListDarkBackground = Color.FromArgb(30, 30, 30);
        private static readonly Color HistoryListDarkText = Color.FromArgb(204, 204, 204);
        private static readonly Color HistorySelectionBg = Color.FromArgb(4, 57, 94);
        private static readonly Color HistorySelectionBorder = Color.FromArgb(0, 122, 204);
        private static readonly Color HistoryLightSelectionBg = Color.FromArgb(13, 110, 253);
        private static readonly Color HistoryLightSelectionBorder = Color.FromArgb(10, 88, 202);
        private const string EnvironmentFileFormat = "ssh-helper.environment";
        private const int EnvironmentFileVersion = 1;
        private const int DefaultDialogWidth = 920;
        private const int DefaultDialogHeight = 620;
        private const int DefaultEnvironmentSplitterDistance = 270;
        private const int MinimumEnvironmentPanelWidth = 220;

        private readonly EnvironmentService _environmentService;
        private readonly ConfigurationService _configService;
        private readonly bool _darkMode;
        private readonly SplitContainer _mainSplitContainer;
        private readonly Panel _environmentListBorderPanel;
        private readonly ListBox _lstEnvironments;
        private readonly TextBox _txtName;
        private readonly TextBox _txtDescription;
        private readonly Panel _colorPreview;
        private readonly DataGridView _gridVariables;
        private readonly ContextMenuStrip _variableRowContextMenu;
        private readonly ToolStripMenuItem _deleteVariableMenuItem;
        private readonly Button _btnNew;
        private readonly Button _btnDuplicate;
        private readonly Button _btnRename;
        private readonly Button _btnDelete;
        private readonly Button _btnExport;
        private readonly Button _btnImport;
        private readonly Button _btnChooseColor;
        private readonly Button _btnDefaultColor;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;
        private readonly Dictionary<string, int?> _environmentLabelColors = new(StringComparer.OrdinalIgnoreCase);
        private string? _currentEnvironmentName;
        private int? _selectedLabelColor;
        private bool _suppressSelectionEvents;
        private int _contextMenuRowIndex = -1;
        private readonly int _savedSplitterDistance;

        public EnvironmentDialog(EnvironmentService environmentService, ConfigurationService configService, bool darkMode)
        {
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _darkMode = darkMode;
            var windowState = _configService.GetCurrent().WindowState ?? new WindowState();
            _savedSplitterDistance = windowState.EnvironmentDialogSplitterDistance ?? DefaultEnvironmentSplitterDistance;

            Text = "Manage Environments";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(820, 520);
            Size = new Size(
                Math.Max(MinimumSize.Width, windowState.EnvironmentDialogWidth ?? DefaultDialogWidth),
                Math.Max(MinimumSize.Height, windowState.EnvironmentDialogHeight ?? DefaultDialogHeight));
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = DefaultEnvironmentSplitterDistance,
                FixedPanel = FixedPanel.Panel1
            };
            _mainSplitContainer.Panel1MinSize = MinimumEnvironmentPanelWidth;

            _lstEnvironments = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                Font = new Font("Segoe UI", 9F),
                HorizontalScrollbar = true
            };
            _lstEnvironments.SelectedIndexChanged += LstEnvironments_SelectedIndexChanged;
            _lstEnvironments.DrawItem += LstEnvironments_DrawItem;

            _environmentListBorderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1)
            };
            _environmentListBorderPanel.Controls.Add(_lstEnvironments);

            _mainSplitContainer.Panel1.Padding = new Padding(10);
            _mainSplitContainer.Panel1.Controls.Add(_environmentListBorderPanel);

            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _btnNew = new Button { Text = "New", Width = 90, Height = 28 };
            _btnDuplicate = new Button { Text = "Duplicate", Width = 90, Height = 28 };
            _btnRename = new Button { Text = "Rename", Width = 90, Height = 28 };
            _btnDelete = new Button { Text = "Delete", Width = 90, Height = 28 };
            _btnExport = new Button { Text = "Export", Width = 90, Height = 28 };
            _btnImport = new Button { Text = "Import", Width = 90, Height = 28 };
            _btnNew.Click += BtnNew_Click;
            _btnDuplicate.Click += BtnDuplicate_Click;
            _btnRename.Click += BtnRename_Click;
            _btnDelete.Click += BtnDelete_Click;
            _btnExport.Click += BtnExport_Click;
            _btnImport.Click += BtnImport_Click;
            actionPanel.Controls.AddRange(new Control[] { _btnNew, _btnDuplicate, _btnRename, _btnDelete, _btnExport, _btnImport });

            var metadataPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(0, 10, 0, 10)
            };
            metadataPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            metadataPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            metadataPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblName = new Label { Text = "Name:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
            _txtName = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };

            var lblDescription = new Label { Text = "Description:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
            _txtDescription = new TextBox { Dock = DockStyle.Fill };

            var lblColor = new Label { Text = "Label Color:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
            var colorPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };
            _colorPreview = new Panel
            {
                Width = 28,
                Height = 20,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Control
            };
            _btnChooseColor = new Button { Text = "Choose...", Width = 90, Height = 24, Margin = new Padding(8, 0, 0, 0) };
            _btnChooseColor.Click += BtnChooseColor_Click;
            _btnDefaultColor = new Button { Text = "Default", Width = 70, Height = 24, Margin = new Padding(4, 0, 0, 0) };
            _btnDefaultColor.Click += BtnDefaultColor_Click;
            colorPanel.Controls.Add(_colorPreview);
            colorPanel.Controls.Add(_btnChooseColor);
            colorPanel.Controls.Add(_btnDefaultColor);

            metadataPanel.Controls.Add(lblName, 0, 0);
            metadataPanel.Controls.Add(_txtName, 1, 0);
            metadataPanel.SetColumnSpan(_txtName, 2);
            metadataPanel.Controls.Add(lblDescription, 0, 1);
            metadataPanel.Controls.Add(_txtDescription, 1, 1);
            metadataPanel.SetColumnSpan(_txtDescription, 2);
            metadataPanel.Controls.Add(lblColor, 0, 2);
            metadataPanel.Controls.Add(colorPanel, 1, 2);
            metadataPanel.SetColumnSpan(colorPanel, 2);

            var lblVariables = new Label
            {
                Text = "Environment Variables",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 6)
            };

            _gridVariables = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                BorderStyle = BorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                CellBorderStyle = DataGridViewCellBorderStyle.Single
            };
            _gridVariables.Columns.Add("Name", "Variable Name");
            _gridVariables.Columns.Add("Value", "Value");
            _gridVariables.KeyDown += GridVariables_KeyDown;
            _gridVariables.CellMouseDown += GridVariables_CellMouseDown;

            _deleteVariableMenuItem = new ToolStripMenuItem("Delete Variable")
            {
                ShortcutKeys = Keys.Control | Keys.Delete,
                ShowShortcutKeys = false
            };
            _deleteVariableMenuItem.Click += DeleteVariableMenuItem_Click;

            _variableRowContextMenu = new ContextMenuStrip();
            _variableRowContextMenu.Items.Add(_deleteVariableMenuItem);
            _variableRowContextMenu.Opening += VariableRowContextMenu_Opening;
            _gridVariables.ContextMenuStrip = _variableRowContextMenu;

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                Height = 44
            };

            _btnSave = new Button { Text = "Save", Width = 90, Height = 28, DialogResult = DialogResult.OK };
            _btnCancel = new Button { Text = "Cancel", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
            _btnSave.Click += BtnSave_Click;
            bottomPanel.Controls.Add(_btnCancel);
            bottomPanel.Controls.Add(_btnSave);

            rightPanel.Controls.Add(_gridVariables);
            rightPanel.Controls.Add(lblVariables);
            rightPanel.Controls.Add(metadataPanel);
            rightPanel.Controls.Add(actionPanel);
            rightPanel.Controls.Add(bottomPanel);

            _mainSplitContainer.Panel2.Controls.Add(rightPanel);
            Controls.Add(_mainSplitContainer);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
            Load += EnvironmentDialog_Load;
            FormClosed += EnvironmentDialog_FormClosed;

            LoadEnvironmentList(preferredEnvironment: _environmentService.GetActiveEnvironmentName());
            ApplyDialogTheme();
        }

        public string? SelectedEnvironmentName { get; private set; }

        private void EnvironmentDialog_Load(object? sender, EventArgs e)
        {
            ApplySavedSplitterDistance();
        }

        private void EnvironmentDialog_FormClosed(object? sender, FormClosedEventArgs e)
        {
            SaveDialogLayout();
        }

        private void ApplySavedSplitterDistance()
        {
            int min = _mainSplitContainer.Panel1MinSize;
            int max = _mainSplitContainer.Width - _mainSplitContainer.Panel2MinSize - _mainSplitContainer.SplitterWidth;
            if (max < min)
                max = min;

            int target = Math.Max(min, Math.Min(_savedSplitterDistance, max));
            try
            {
                _mainSplitContainer.SplitterDistance = target;
            }
            catch
            {
                // Keep dialog load resilient if persisted values become invalid for current DPI/layout.
            }
        }

        private void SaveDialogLayout()
        {
            try
            {
                int width = Math.Max(MinimumSize.Width, Width);
                int height = Math.Max(MinimumSize.Height, Height);
                int splitterDistance = _mainSplitContainer.SplitterDistance;

                _configService.Update(config =>
                {
                    config.WindowState ??= new WindowState();
                    config.WindowState.EnvironmentDialogWidth = width;
                    config.WindowState.EnvironmentDialogHeight = height;
                    config.WindowState.EnvironmentDialogSplitterDistance = splitterDistance;
                });
            }
            catch
            {
                // Layout persistence should never block dialog close.
            }
        }

        private void ApplyDialogTheme()
        {
            DialogTheme.ApplyTo(this, _darkMode);
            DialogTheme.StyleButton(_btnNew, _darkMode);
            DialogTheme.StyleButton(_btnDuplicate, _darkMode);
            DialogTheme.StyleButton(_btnRename, _darkMode);
            DialogTheme.StyleButton(_btnDelete, _darkMode);
            DialogTheme.StyleButton(_btnExport, _darkMode);
            DialogTheme.StyleButton(_btnImport, _darkMode);
            DialogTheme.StyleButton(_btnChooseColor, _darkMode);
            DialogTheme.StyleButton(_btnDefaultColor, _darkMode);
            DialogTheme.StyleButton(_btnSave, _darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnCancel, _darkMode);
            DialogTheme.SetDarkTitleBar(this, _darkMode);
            DialogTheme.StyleDataGridView(_gridVariables, _darkMode, flattenHeaderBevel: true);

            if (_darkMode)
            {
                _environmentListBorderPanel.BackColor = DialogTheme.DarkBorder;
                _lstEnvironments.BackColor = HistoryListDarkBackground;
                _lstEnvironments.ForeColor = HistoryListDarkText;
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);
            }
            else
            {
                _environmentListBorderPanel.BackColor = DialogTheme.LightBorder;
                _lstEnvironments.BackColor = DialogTheme.LightPanel;
                _lstEnvironments.ForeColor = DialogTheme.LightText;
            }

            RefreshColorPreview();
            _lstEnvironments.Invalidate();
        }

        private void LstEnvironments_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstEnvironments.Items.Count)
                return;

            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bgColor = isSelected
                ? (_darkMode ? HistorySelectionBg : HistoryLightSelectionBg)
                : (_darkMode ? HistoryListDarkBackground : _lstEnvironments.BackColor);
            using var bgBrush = new SolidBrush(bgColor);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            if (isSelected)
            {
                using var borderPen = new Pen(_darkMode ? HistorySelectionBorder : HistoryLightSelectionBorder, 1);
                e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }

            var text = _lstEnvironments.Items[e.Index]?.ToString() ?? string.Empty;
            _environmentLabelColors.TryGetValue(text, out var labelColorArgb);
            var hasLabelColor = labelColorArgb.HasValue;
            var swatchColor = hasLabelColor
                ? Color.FromArgb(labelColorArgb.GetValueOrDefault())
                : (_darkMode ? DialogTheme.DarkSurface2 : SystemColors.Control);
            var swatchBorderColor = _darkMode ? DialogTheme.DarkBorder : DialogTheme.LightBorder;
            var swatchSize = 10;
            var swatchY = e.Bounds.Top + Math.Max(0, (e.Bounds.Height - swatchSize) / 2);
            var swatchRect = new Rectangle(e.Bounds.Left + 6, swatchY, swatchSize, swatchSize);
            using var swatchBrush = new SolidBrush(swatchColor);
            using var swatchPen = new Pen(swatchBorderColor, 1);
            e.Graphics.FillRectangle(swatchBrush, swatchRect);
            e.Graphics.DrawRectangle(swatchPen, swatchRect);

            var textColor = _darkMode ? HistoryListDarkText : (isSelected ? Color.White : _lstEnvironments.ForeColor);
            using var textBrush = new SolidBrush(textColor);
            using var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            var textRect = new Rectangle(swatchRect.Right + 6, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 24), e.Bounds.Height);
            e.Graphics.DrawString(text, e.Font ?? _lstEnvironments.Font, textBrush, textRect, sf);
        }

        private void LstEnvironments_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressSelectionEvents)
                return;

            PersistCurrentEnvironmentDetails();

            if (_lstEnvironments.SelectedItem is string selected)
            {
                LoadEnvironmentDetails(selected);
            }
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            PersistCurrentEnvironmentDetails();

            var input = PromptForEnvironmentName("New Environment", "Enter environment name:", string.Empty);
            if (input == null)
                return;

            try
            {
                var copyFrom = _currentEnvironmentName;
                _environmentService.CreateEnvironment(input, copyFrom);
                LoadEnvironmentList(input);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Create Environment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnDuplicate_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentEnvironmentName))
                return;

            PersistCurrentEnvironmentDetails();
            var suggested = $"{_currentEnvironmentName}-copy";
            var input = PromptForEnvironmentName("Duplicate Environment", "Enter duplicate environment name:", suggested);
            if (input == null)
                return;

            try
            {
                _environmentService.CreateEnvironment(input, _currentEnvironmentName);
                LoadEnvironmentList(input);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Duplicate Environment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnRename_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentEnvironmentName))
                return;

            PersistCurrentEnvironmentDetails();
            var input = PromptForEnvironmentName("Rename Environment", "Enter new environment name:", _currentEnvironmentName);
            if (input == null)
                return;

            try
            {
                _environmentService.RenameEnvironment(_currentEnvironmentName, input);
                LoadEnvironmentList(input);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Rename Environment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentEnvironmentName))
                return;

            if (MessageBox.Show(
                    $"Delete environment '{_currentEnvironmentName}'?",
                    "Delete Environment",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _environmentService.DeleteEnvironment(_currentEnvironmentName);
                LoadEnvironmentList(EnvironmentConfig.DefaultName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Delete Environment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentEnvironmentName))
                return;

            PersistCurrentEnvironmentDetails();

            try
            {
                var environment = _environmentService.GetEnvironment(_currentEnvironmentName);
                using var saveDialog = new SaveFileDialog
                {
                    Title = "Export Environment",
                    Filter = "Environment Files (*.sshenv.json)|*.sshenv.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    FileName = BuildSuggestedExportFileName(environment.Name),
                    OverwritePrompt = true
                };

                if (saveDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                var package = new EnvironmentTransferPackage
                {
                    Format = EnvironmentFileFormat,
                    Version = EnvironmentFileVersion,
                    ExportedAtUtc = DateTime.UtcNow,
                    Environment = environment
                };

                var json = JsonConvert.SerializeObject(package, Formatting.Indented);
                File.WriteAllText(saveDialog.FileName, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export Environment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            PersistCurrentEnvironmentDetails();

            using var openDialog = new OpenFileDialog
            {
                Title = "Import Environment",
                Filter = "Environment Files (*.sshenv.json;*.json)|*.sshenv.json;*.json|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (openDialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var json = File.ReadAllText(openDialog.FileName);
                var importedEnvironment = ParseImportedEnvironment(json);
                bool overwriteExisting = false;
                var existingNames = _environmentService.GetEnvironmentNames();

                if (existingNames.Any(name => string.Equals(name, importedEnvironment.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var conflictResult = MessageBox.Show(
                        $"Environment '{importedEnvironment.Name}' already exists. Choose Yes to overwrite, No to rename, or Cancel to abort.",
                        "Import Environment",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (conflictResult == DialogResult.Cancel)
                        return;

                    if (conflictResult == DialogResult.Yes)
                    {
                        overwriteExisting = true;
                    }
                    else
                    {
                        var suggested = $"{importedEnvironment.Name}-imported";
                        var renamed = PromptForEnvironmentName("Import Environment", "Enter environment name:", suggested);
                        if (renamed == null)
                            return;

                        importedEnvironment.Name = renamed;
                    }
                }

                var saved = _environmentService.ImportEnvironment(importedEnvironment, overwriteExisting);
                LoadEnvironmentList(saved.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import Environment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnDefaultColor_Click(object? sender, EventArgs e)
        {
            _selectedLabelColor = null;
            if (!string.IsNullOrWhiteSpace(_currentEnvironmentName))
            {
                _environmentLabelColors[_currentEnvironmentName] = null;
            }
            RefreshColorPreview();
            _lstEnvironments.Invalidate();
        }

        private void BtnChooseColor_Click(object? sender, EventArgs e)
        {
            using var colorDialog = new ColorDialog
            {
                FullOpen = true
            };

            if (_selectedLabelColor.HasValue)
            {
                colorDialog.Color = Color.FromArgb(_selectedLabelColor.Value);
            }

            if (colorDialog.ShowDialog(this) == DialogResult.OK)
            {
                _selectedLabelColor = colorDialog.Color.ToArgb();
                if (!string.IsNullOrWhiteSpace(_currentEnvironmentName))
                {
                    _environmentLabelColors[_currentEnvironmentName] = _selectedLabelColor;
                }
                RefreshColorPreview();
                _lstEnvironments.Invalidate();
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            PersistCurrentEnvironmentDetails();
            SelectedEnvironmentName = _currentEnvironmentName;
        }

        private void GridVariables_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Delete)
            {
                DeleteCurrentVariableRow();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void GridVariables_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (e.RowIndex < 0 || e.RowIndex >= _gridVariables.Rows.Count || _gridVariables.Rows[e.RowIndex].IsNewRow)
            {
                _contextMenuRowIndex = -1;
                return;
            }

            _contextMenuRowIndex = e.RowIndex;
            var targetColumn = e.ColumnIndex >= 0 ? e.ColumnIndex : 0;
            _gridVariables.CurrentCell = _gridVariables.Rows[e.RowIndex].Cells[targetColumn];
        }

        private void VariableRowContextMenu_Opening(object? sender, CancelEventArgs e)
        {
            bool canDelete = _contextMenuRowIndex >= 0 &&
                             _contextMenuRowIndex < _gridVariables.Rows.Count &&
                             !_gridVariables.Rows[_contextMenuRowIndex].IsNewRow;
            _deleteVariableMenuItem.Enabled = canDelete;
            e.Cancel = !canDelete;
        }

        private void DeleteVariableMenuItem_Click(object? sender, EventArgs e) => DeleteCurrentVariableRow(_contextMenuRowIndex);

        private void DeleteCurrentVariableRow(int rowIndex = -1)
        {
            if (rowIndex < 0 && _gridVariables.CurrentCell != null)
                rowIndex = _gridVariables.CurrentCell.RowIndex;
            if (rowIndex < 0 || rowIndex >= _gridVariables.Rows.Count)
                return;

            var row = _gridVariables.Rows[rowIndex];
            if (row.IsNewRow)
                return;

            if (_gridVariables.IsCurrentCellInEditMode)
            {
                _gridVariables.EndEdit();
            }

            _gridVariables.Rows.RemoveAt(rowIndex);

            if (_gridVariables.Rows.Count == 0)
                return;

            int nextRowIndex = Math.Min(rowIndex, _gridVariables.Rows.Count - 1);
            if (nextRowIndex >= 0 && !_gridVariables.Rows[nextRowIndex].IsNewRow)
            {
                _gridVariables.CurrentCell = _gridVariables.Rows[nextRowIndex].Cells[0];
            }

            _contextMenuRowIndex = -1;
        }

        private void LoadEnvironmentList(string? preferredEnvironment)
        {
            var names = _environmentService.GetEnvironmentNames();
            _suppressSelectionEvents = true;
            _environmentLabelColors.Clear();
            _lstEnvironments.Items.Clear();
            foreach (var name in names)
            {
                var environment = _environmentService.GetEnvironment(name);
                _environmentLabelColors[name] = environment.LabelColor;
                _lstEnvironments.Items.Add(name);
            }
            UpdateEnvironmentListHorizontalExtent();

            string target = preferredEnvironment ?? EnvironmentConfig.DefaultName;
            int index = names.FindIndex(name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
            _lstEnvironments.SelectedIndex = index >= 0 ? index : 0;
            _suppressSelectionEvents = false;

            if (_lstEnvironments.SelectedItem is string selected)
            {
                LoadEnvironmentDetails(selected);
            }
        }

        private void LoadEnvironmentDetails(string environmentName)
        {
            var environment = _environmentService.GetEnvironment(environmentName);
            _currentEnvironmentName = environment.Name;
            _txtName.Text = environment.Name;
            _txtDescription.Text = environment.Description ?? string.Empty;
            _selectedLabelColor = environment.LabelColor;
            _environmentLabelColors[environment.Name] = environment.LabelColor;
            RefreshColorPreview();
            _lstEnvironments.Invalidate();

            _gridVariables.Rows.Clear();
            foreach (var kvp in environment.Variables.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                _gridVariables.Rows.Add(kvp.Key, kvp.Value);
            }
        }

        private void UpdateEnvironmentListHorizontalExtent()
        {
            int maxTextWidth = 0;
            foreach (var item in _lstEnvironments.Items)
            {
                var text = item?.ToString() ?? string.Empty;
                int textWidth = TextRenderer.MeasureText(text, _lstEnvironments.Font).Width;
                if (textWidth > maxTextWidth)
                {
                    maxTextWidth = textWidth;
                }
            }

            // Include swatch and padding used by owner-draw layout.
            _lstEnvironments.HorizontalExtent = maxTextWidth + 36;
        }

        private void RefreshColorPreview()
        {
            _colorPreview.BackColor = _selectedLabelColor.HasValue
                ? Color.FromArgb(_selectedLabelColor.Value)
                : (_darkMode ? DialogTheme.DarkSurface2 : SystemColors.Control);
        }

        private void PersistCurrentEnvironmentDetails()
        {
            if (string.IsNullOrWhiteSpace(_currentEnvironmentName))
                return;

            try
            {
                _environmentService.UpdateEnvironmentDetails(
                    _currentEnvironmentName,
                    _txtDescription.Text,
                    _selectedLabelColor,
                    CollectVariables());
            }
            catch
            {
                // Keep editing flow responsive; actionable errors are shown on explicit operations.
            }
        }

        private Dictionary<string, string> CollectVariables()
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in _gridVariables.Rows)
            {
                if (row.IsNewRow)
                    continue;

                var key = row.Cells[0].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var value = row.Cells[1].Value?.ToString() ?? string.Empty;
                variables[key] = value;
            }

            return variables;
        }

        private static EnvironmentConfig ParseImportedEnvironment(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Environment file is empty.");

            try
            {
                var token = JToken.Parse(json);
                if (token is not JObject root)
                    throw new InvalidOperationException("Environment file must contain a JSON object.");

                var package = root.ToObject<EnvironmentTransferPackage>();
                if (package?.Environment != null && !string.IsNullOrWhiteSpace(package.Environment.Name))
                {
                    var packagedEnvironment = package.Environment.Clone();
                    var normalizedName = packagedEnvironment.Name.Trim();
                    packagedEnvironment.Name = normalizedName;
                    packagedEnvironment.Normalize(normalizedName);
                    return packagedEnvironment;
                }

                var directEnvironment = root.ToObject<EnvironmentConfig>();
                if (directEnvironment == null || string.IsNullOrWhiteSpace(directEnvironment.Name))
                    throw new InvalidOperationException("Environment file does not contain a valid environment payload.");

                var name = directEnvironment.Name.Trim();
                directEnvironment.Name = name;
                directEnvironment.Normalize(name);
                return directEnvironment;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid environment file JSON: {ex.Message}", ex);
            }
        }

        private static string BuildSuggestedExportFileName(string environmentName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(environmentName
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray());

            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "environment";

            return $"{sanitized}.sshenv.json";
        }

        private static string? PromptForEnvironmentName(string title, string prompt, string defaultValue)
        {
            var input = Interaction.InputBox(prompt, title, defaultValue);
            if (string.IsNullOrWhiteSpace(input))
                return null;

            return input.Trim();
        }

        private sealed class EnvironmentTransferPackage
        {
            public string? Format { get; set; }
            public int Version { get; set; }
            public DateTime ExportedAtUtc { get; set; }
            public EnvironmentConfig? Environment { get; set; }
        }
    }
}
