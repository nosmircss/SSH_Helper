using Microsoft.VisualBasic;
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
        private readonly EnvironmentService _environmentService;
        private readonly bool _darkMode;
        private readonly ListBox _lstEnvironments;
        private readonly TextBox _txtName;
        private readonly TextBox _txtDescription;
        private readonly Panel _colorPreview;
        private readonly DataGridView _gridVariables;
        private readonly Button _btnNew;
        private readonly Button _btnDuplicate;
        private readonly Button _btnRename;
        private readonly Button _btnDelete;
        private readonly Button _btnChooseColor;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;
        private string? _currentEnvironmentName;
        private int? _selectedLabelColor;
        private bool _suppressSelectionEvents;

        public EnvironmentDialog(EnvironmentService environmentService, bool darkMode)
        {
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            _darkMode = darkMode;

            Text = "Manage Environments";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(820, 520);
            Size = new Size(920, 620);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 250,
                FixedPanel = FixedPanel.Panel1
            };

            _lstEnvironments = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            _lstEnvironments.SelectedIndexChanged += LstEnvironments_SelectedIndexChanged;
            split.Panel1.Padding = new Padding(10);
            split.Panel1.Controls.Add(_lstEnvironments);

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
            _btnNew.Click += BtnNew_Click;
            _btnDuplicate.Click += BtnDuplicate_Click;
            _btnRename.Click += BtnRename_Click;
            _btnDelete.Click += BtnDelete_Click;
            actionPanel.Controls.AddRange(new Control[] { _btnNew, _btnDuplicate, _btnRename, _btnDelete });

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
            colorPanel.Controls.Add(_colorPreview);
            colorPanel.Controls.Add(_btnChooseColor);

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
                MultiSelect = false
            };
            _gridVariables.Columns.Add("Name", "Variable Name");
            _gridVariables.Columns.Add("Value", "Value");

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
            bottomPanel.Controls.Add(_btnSave);
            bottomPanel.Controls.Add(_btnCancel);

            rightPanel.Controls.Add(_gridVariables);
            rightPanel.Controls.Add(lblVariables);
            rightPanel.Controls.Add(metadataPanel);
            rightPanel.Controls.Add(actionPanel);
            rightPanel.Controls.Add(bottomPanel);

            split.Panel2.Controls.Add(rightPanel);
            Controls.Add(split);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            LoadEnvironmentList(preferredEnvironment: _environmentService.GetActiveEnvironmentName());
            ApplyDialogTheme();
        }

        public string? SelectedEnvironmentName { get; private set; }

        private void ApplyDialogTheme()
        {
            DialogTheme.ApplyTo(this, _darkMode);
            DialogTheme.StyleButton(_btnNew, _darkMode);
            DialogTheme.StyleButton(_btnDuplicate, _darkMode);
            DialogTheme.StyleButton(_btnRename, _darkMode);
            DialogTheme.StyleButton(_btnDelete, _darkMode);
            DialogTheme.StyleButton(_btnChooseColor, _darkMode);
            DialogTheme.StyleButton(_btnSave, _darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnCancel, _darkMode);
            DialogTheme.SetDarkTitleBar(this, _darkMode);

            if (_darkMode)
            {
                _gridVariables.BackgroundColor = DialogTheme.DarkInput;
                _gridVariables.GridColor = DialogTheme.DarkBorder;
                _gridVariables.DefaultCellStyle.BackColor = DialogTheme.DarkInput;
                _gridVariables.DefaultCellStyle.ForeColor = DialogTheme.DarkText;
                _gridVariables.DefaultCellStyle.SelectionBackColor = DialogTheme.DarkSurface2;
                _gridVariables.DefaultCellStyle.SelectionForeColor = DialogTheme.DarkText;
                _gridVariables.ColumnHeadersDefaultCellStyle.BackColor = DialogTheme.DarkSurface2;
                _gridVariables.ColumnHeadersDefaultCellStyle.ForeColor = DialogTheme.DarkText;
                _gridVariables.EnableHeadersVisualStyles = false;
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);
            }
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
                _colorPreview.BackColor = colorDialog.Color;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            PersistCurrentEnvironmentDetails();
            SelectedEnvironmentName = _currentEnvironmentName;
        }

        private void LoadEnvironmentList(string? preferredEnvironment)
        {
            var names = _environmentService.GetEnvironmentNames();
            _suppressSelectionEvents = true;
            _lstEnvironments.Items.Clear();
            foreach (var name in names)
            {
                _lstEnvironments.Items.Add(name);
            }

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
            _colorPreview.BackColor = environment.LabelColor.HasValue
                ? Color.FromArgb(environment.LabelColor.Value)
                : (_darkMode ? DialogTheme.DarkSurface2 : SystemColors.Control);

            _gridVariables.Rows.Clear();
            foreach (var kvp in environment.Variables.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                _gridVariables.Rows.Add(kvp.Key, kvp.Value);
            }
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

        private static string? PromptForEnvironmentName(string title, string prompt, string defaultValue)
        {
            var input = Interaction.InputBox(prompt, title, defaultValue);
            if (string.IsNullOrWhiteSpace(input))
                return null;

            return input.Trim();
        }
    }
}
