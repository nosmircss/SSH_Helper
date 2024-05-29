namespace SSH_Helper
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            btnOpenCSV = new Button();
            dgv_variables = new DataGridView();
            contextMenuStrip1 = new ContextMenuStrip(components);
            addColumnToolStripMenuItem = new ToolStripMenuItem();
            renameColumnToolStripMenuItem = new ToolStripMenuItem();
            deleteColumnToolStripMenuItem = new ToolStripMenuItem();
            deleteRowToolStripMenuItem = new ToolStripMenuItem();
            btnSaveAs = new Button();
            btnClear = new Button();
            txtOutput = new TextBox();
            label1 = new Label();
            txtPreset = new TextBox();
            btnSave = new Button();
            label6 = new Label();
            lstPreset = new ListBox();
            label4 = new Label();
            label3 = new Label();
            label2 = new Label();
            btnExecuteAll = new Button();
            txtCommand = new TextBox();
            txtPassword = new TextBox();
            txtUsername = new TextBox();
            contextPresetLst = new ContextMenuStrip(components);
            addPresetToolStripMenuItem = new ToolStripMenuItem();
            renameToolStripMenuItem = new ToolStripMenuItem();
            deleteToolStripMenuItem = new ToolStripMenuItem();
            btnExecuteSelected = new Button();
            contextPresetLstAdd = new ContextMenuStrip(components);
            toolStripMenuItem1 = new ToolStripMenuItem();
            txtDelay = new TextBox();
            label5 = new Label();
            txtTimeout = new TextBox();
            label7 = new Label();
            lstOutput = new ListBox();
            contextHistoryLst = new ContextMenuStrip(components);
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            deleteEntryToolStripMenuItem = new ToolStripMenuItem();
            deleteAllHistoryToolStripMenuItem = new ToolStripMenuItem();
            label8 = new Label();
            btnStopAll = new Button();
            ((System.ComponentModel.ISupportInitialize)dgv_variables).BeginInit();
            contextMenuStrip1.SuspendLayout();
            contextPresetLst.SuspendLayout();
            contextPresetLstAdd.SuspendLayout();
            contextHistoryLst.SuspendLayout();
            SuspendLayout();
            // 
            // btnOpenCSV
            // 
            btnOpenCSV.Location = new Point(16, 12);
            btnOpenCSV.Name = "btnOpenCSV";
            btnOpenCSV.Size = new Size(75, 23);
            btnOpenCSV.TabIndex = 0;
            btnOpenCSV.Text = "Open CSV";
            btnOpenCSV.UseVisualStyleBackColor = true;
            btnOpenCSV.Click += btnOpenCSV_Click;
            // 
            // dgv_variables
            // 
            dgv_variables.AllowUserToOrderColumns = true;
            dgv_variables.BackgroundColor = SystemColors.Window;
            dgv_variables.BorderStyle = BorderStyle.Fixed3D;
            dgv_variables.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            dgv_variables.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv_variables.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv_variables.EditMode = DataGridViewEditMode.EditProgrammatically;
            dgv_variables.Location = new Point(16, 43);
            dgv_variables.Name = "dgv_variables";
            dgv_variables.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            dgv_variables.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgv_variables.Size = new Size(630, 169);
            dgv_variables.TabIndex = 1;
            dgv_variables.CellDoubleClick += dgv_variables_CellDoubleClick;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { addColumnToolStripMenuItem, renameColumnToolStripMenuItem, deleteColumnToolStripMenuItem, deleteRowToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(164, 92);
            // 
            // addColumnToolStripMenuItem
            // 
            addColumnToolStripMenuItem.Name = "addColumnToolStripMenuItem";
            addColumnToolStripMenuItem.Size = new Size(163, 22);
            addColumnToolStripMenuItem.Text = "Add Column";
            addColumnToolStripMenuItem.Click += addColumnToolStripMenuItem_Click;
            // 
            // renameColumnToolStripMenuItem
            // 
            renameColumnToolStripMenuItem.Name = "renameColumnToolStripMenuItem";
            renameColumnToolStripMenuItem.Size = new Size(163, 22);
            renameColumnToolStripMenuItem.Text = "Rename Column";
            renameColumnToolStripMenuItem.Click += renameColumnToolStripMenuItem_Click;
            // 
            // deleteColumnToolStripMenuItem
            // 
            deleteColumnToolStripMenuItem.Name = "deleteColumnToolStripMenuItem";
            deleteColumnToolStripMenuItem.Size = new Size(163, 22);
            deleteColumnToolStripMenuItem.Text = "Delete Column";
            deleteColumnToolStripMenuItem.Click += deleteColumnToolStripMenuItem_Click;
            // 
            // deleteRowToolStripMenuItem
            // 
            deleteRowToolStripMenuItem.Name = "deleteRowToolStripMenuItem";
            deleteRowToolStripMenuItem.Size = new Size(163, 22);
            deleteRowToolStripMenuItem.Text = "Delete Row";
            deleteRowToolStripMenuItem.Click += deleteRowToolStripMenuItem_Click;
            // 
            // btnSaveAs
            // 
            btnSaveAs.Location = new Point(106, 12);
            btnSaveAs.Name = "btnSaveAs";
            btnSaveAs.Size = new Size(75, 23);
            btnSaveAs.TabIndex = 2;
            btnSaveAs.Text = "Save As";
            btnSaveAs.UseVisualStyleBackColor = true;
            btnSaveAs.Click += btnSaveAs_Click;
            // 
            // btnClear
            // 
            btnClear.Location = new Point(197, 12);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(75, 23);
            btnClear.TabIndex = 3;
            btnClear.Text = "Clear";
            btnClear.UseVisualStyleBackColor = true;
            btnClear.Click += btnClear_Click;
            // 
            // txtOutput
            // 
            txtOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtOutput.BackColor = SystemColors.Window;
            txtOutput.Font = new Font("Courier New", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtOutput.Location = new Point(428, 234);
            txtOutput.MaxLength = 1000000;
            txtOutput.Multiline = true;
            txtOutput.Name = "txtOutput";
            txtOutput.ReadOnly = true;
            txtOutput.ScrollBars = ScrollBars.Both;
            txtOutput.Size = new Size(959, 304);
            txtOutput.TabIndex = 4;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(428, 217);
            label1.Name = "label1";
            label1.Size = new Size(45, 15);
            label1.TabIndex = 5;
            label1.Text = "Output";
            // 
            // txtPreset
            // 
            txtPreset.Location = new Point(1007, 10);
            txtPreset.Name = "txtPreset";
            txtPreset.Size = new Size(95, 23);
            txtPreset.TabIndex = 27;
            txtPreset.Visible = false;
            // 
            // btnSave
            // 
            btnSave.Location = new Point(1006, 12);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(96, 23);
            btnSave.TabIndex = 26;
            btnSave.Text = "Save Preset";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(743, 20);
            label6.Name = "label6";
            label6.Size = new Size(81, 15);
            label6.TabIndex = 25;
            label6.Text = "Saved Presets:";
            // 
            // lstPreset
            // 
            lstPreset.FormattingEnabled = true;
            lstPreset.IntegralHeight = false;
            lstPreset.ItemHeight = 15;
            lstPreset.Location = new Point(653, 43);
            lstPreset.Name = "lstPreset";
            lstPreset.Size = new Size(268, 169);
            lstPreset.Sorted = true;
            lstPreset.TabIndex = 24;
            lstPreset.SelectedIndexChanged += lstPreset_SelectedIndexChanged;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(961, 41);
            label4.Name = "label4";
            label4.Size = new Size(40, 15);
            label4.TabIndex = 23;
            label4.Text = "Script:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(474, 15);
            label3.Name = "label3";
            label3.Size = new Size(60, 15);
            label3.TabIndex = 22;
            label3.Text = "Password:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(282, 16);
            label2.Name = "label2";
            label2.Size = new Size(63, 15);
            label2.TabIndex = 21;
            label2.Text = "Username:";
            // 
            // btnExecuteAll
            // 
            btnExecuteAll.Location = new Point(927, 172);
            btnExecuteAll.Name = "btnExecuteAll";
            btnExecuteAll.Size = new Size(75, 40);
            btnExecuteAll.TabIndex = 20;
            btnExecuteAll.Text = "Execute All";
            btnExecuteAll.UseVisualStyleBackColor = true;
            btnExecuteAll.Click += btnExecuteAll_Click;
            // 
            // txtCommand
            // 
            txtCommand.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtCommand.Location = new Point(1007, 43);
            txtCommand.Multiline = true;
            txtCommand.Name = "txtCommand";
            txtCommand.Size = new Size(379, 169);
            txtCommand.TabIndex = 19;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(540, 11);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(152, 23);
            txtPassword.TabIndex = 18;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(351, 13);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(117, 23);
            txtUsername.TabIndex = 17;
            // 
            // contextPresetLst
            // 
            contextPresetLst.ImageScalingSize = new Size(20, 20);
            contextPresetLst.Items.AddRange(new ToolStripItem[] { addPresetToolStripMenuItem, renameToolStripMenuItem, deleteToolStripMenuItem });
            contextPresetLst.Name = "contextPresetLst";
            contextPresetLst.Size = new Size(153, 70);
            // 
            // addPresetToolStripMenuItem
            // 
            addPresetToolStripMenuItem.Name = "addPresetToolStripMenuItem";
            addPresetToolStripMenuItem.Size = new Size(152, 22);
            addPresetToolStripMenuItem.Text = "Add Preset";
            addPresetToolStripMenuItem.Click += addPresetToolStripMenuItem_Click;
            // 
            // renameToolStripMenuItem
            // 
            renameToolStripMenuItem.Name = "renameToolStripMenuItem";
            renameToolStripMenuItem.Size = new Size(152, 22);
            renameToolStripMenuItem.Text = "Rename Preset";
            renameToolStripMenuItem.Click += renameToolStripMenuItem_Click;
            // 
            // deleteToolStripMenuItem
            // 
            deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            deleteToolStripMenuItem.Size = new Size(152, 22);
            deleteToolStripMenuItem.Text = "Delete Preset";
            deleteToolStripMenuItem.Click += deleteToolStripMenuItem_Click;
            // 
            // btnExecuteSelected
            // 
            btnExecuteSelected.Location = new Point(926, 104);
            btnExecuteSelected.Name = "btnExecuteSelected";
            btnExecuteSelected.Size = new Size(75, 40);
            btnExecuteSelected.TabIndex = 29;
            btnExecuteSelected.Text = "Execute Selected";
            btnExecuteSelected.UseVisualStyleBackColor = true;
            btnExecuteSelected.Click += btnExecuteSelected_Click;
            // 
            // contextPresetLstAdd
            // 
            contextPresetLstAdd.ImageScalingSize = new Size(20, 20);
            contextPresetLstAdd.Items.AddRange(new ToolStripItem[] { toolStripMenuItem1 });
            contextPresetLstAdd.Name = "contextPresetLst";
            contextPresetLstAdd.Size = new Size(132, 26);
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(131, 22);
            toolStripMenuItem1.Text = "Add Preset";
            toolStripMenuItem1.Click += contextPresetLstAdd_Click;
            // 
            // txtDelay
            // 
            txtDelay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtDelay.Location = new Point(1341, 11);
            txtDelay.Margin = new Padding(3, 2, 3, 2);
            txtDelay.Name = "txtDelay";
            txtDelay.Size = new Size(45, 23);
            txtDelay.TabIndex = 30;
            txtDelay.TextAlign = HorizontalAlignment.Right;
            // 
            // label5
            // 
            label5.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label5.Location = new Point(1292, 14);
            label5.Name = "label5";
            label5.Size = new Size(44, 15);
            label5.TabIndex = 31;
            label5.Text = "Delay:";
            // 
            // txtTimeout
            // 
            txtTimeout.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtTimeout.Location = new Point(1242, 11);
            txtTimeout.Margin = new Padding(3, 2, 3, 2);
            txtTimeout.Name = "txtTimeout";
            txtTimeout.Size = new Size(44, 23);
            txtTimeout.TabIndex = 32;
            txtTimeout.TextAlign = HorizontalAlignment.Right;
            // 
            // label7
            // 
            label7.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label7.AutoSize = true;
            label7.Location = new Point(1182, 14);
            label7.Name = "label7";
            label7.Size = new Size(54, 15);
            label7.TabIndex = 33;
            label7.Text = "Timeout:";
            // 
            // lstOutput
            // 
            lstOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            lstOutput.ContextMenuStrip = contextHistoryLst;
            lstOutput.FormattingEnabled = true;
            lstOutput.IntegralHeight = false;
            lstOutput.ItemHeight = 15;
            lstOutput.Location = new Point(16, 234);
            lstOutput.Margin = new Padding(3, 2, 3, 2);
            lstOutput.Name = "lstOutput";
            lstOutput.Size = new Size(407, 305);
            lstOutput.TabIndex = 34;
            lstOutput.SelectedIndexChanged += lstOutput_SelectedIndexChanged;
            // 
            // contextHistoryLst
            // 
            contextHistoryLst.ImageScalingSize = new Size(20, 20);
            contextHistoryLst.Items.AddRange(new ToolStripItem[] { saveAsToolStripMenuItem, toolStripSeparator1, deleteEntryToolStripMenuItem, deleteAllHistoryToolStripMenuItem });
            contextHistoryLst.Name = "contextHistoryLst";
            contextHistoryLst.Size = new Size(166, 76);
            // 
            // saveAsToolStripMenuItem
            // 
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.Size = new Size(165, 22);
            saveAsToolStripMenuItem.Text = "Save As";
            saveAsToolStripMenuItem.Click += saveAsToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(162, 6);
            // 
            // deleteEntryToolStripMenuItem
            // 
            deleteEntryToolStripMenuItem.Name = "deleteEntryToolStripMenuItem";
            deleteEntryToolStripMenuItem.Size = new Size(165, 22);
            deleteEntryToolStripMenuItem.Text = "Delete Entry";
            deleteEntryToolStripMenuItem.Click += deleteEntryToolStripMenuItem_Click;
            // 
            // deleteAllHistoryToolStripMenuItem
            // 
            deleteAllHistoryToolStripMenuItem.Name = "deleteAllHistoryToolStripMenuItem";
            deleteAllHistoryToolStripMenuItem.Size = new Size(165, 22);
            deleteAllHistoryToolStripMenuItem.Text = "Delete All History";
            deleteAllHistoryToolStripMenuItem.Click += deleteAllHistoryToolStripMenuItem_Click;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(16, 217);
            label8.Name = "label8";
            label8.Size = new Size(45, 15);
            label8.TabIndex = 35;
            label8.Text = "History";
            // 
            // btnStopAll
            // 
            btnStopAll.Location = new Point(927, 172);
            btnStopAll.Name = "btnStopAll";
            btnStopAll.Size = new Size(75, 40);
            btnStopAll.TabIndex = 36;
            btnStopAll.Text = "Stop Execution";
            btnStopAll.UseVisualStyleBackColor = true;
            btnStopAll.Visible = false;
            btnStopAll.Click += btnStopAll_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1398, 552);
            Controls.Add(label8);
            Controls.Add(lstOutput);
            Controls.Add(label7);
            Controls.Add(txtTimeout);
            Controls.Add(label5);
            Controls.Add(txtDelay);
            Controls.Add(btnExecuteSelected);
            Controls.Add(btnSave);
            Controls.Add(label6);
            Controls.Add(lstPreset);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(txtCommand);
            Controls.Add(txtPassword);
            Controls.Add(txtUsername);
            Controls.Add(label1);
            Controls.Add(txtOutput);
            Controls.Add(btnClear);
            Controls.Add(btnSaveAs);
            Controls.Add(dgv_variables);
            Controls.Add(btnOpenCSV);
            Controls.Add(txtPreset);
            Controls.Add(btnStopAll);
            Controls.Add(btnExecuteAll);
            Name = "Form1";
            Text = "Salihu_Helper";
            WindowState = FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)dgv_variables).EndInit();
            contextMenuStrip1.ResumeLayout(false);
            contextPresetLst.ResumeLayout(false);
            contextPresetLstAdd.ResumeLayout(false);
            contextHistoryLst.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Button btnOpenCSV;
        private DataGridView dgv_variables;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem addColumnToolStripMenuItem;
        private ToolStripMenuItem deleteColumnToolStripMenuItem;
        private ToolStripMenuItem deleteRowToolStripMenuItem;
        private ToolStripMenuItem renameColumnToolStripMenuItem;
        private Button btnSaveAs;
        private Button btnClear;
        private TextBox txtOutput;
        private Label label1;
        private TextBox txtPreset;
        private Button btnSave;
        private Label label6;
        private ListBox lstPreset;
        private Label label4;
        private Label label3;
        private Label label2;
        private Button btnExecuteAll;
        private TextBox txtCommand;
        private TextBox txtPassword;
        private TextBox txtUsername;
        private ContextMenuStrip contextPresetLst;
        private ToolStripMenuItem deleteToolStripMenuItem;
        private ToolStripMenuItem renameToolStripMenuItem;
        private ToolStripMenuItem addPresetToolStripMenuItem;
        private Button btnExecuteSelected;
        private ContextMenuStrip contextPresetLstAdd;
        private ToolStripMenuItem toolStripMenuItem1;
        private TextBox txtDelay;
        private Label label5;
        private TextBox txtTimeout;
        private Label label7;
        private ListBox lstOutput;
        private Label label8;
        private ContextMenuStrip contextHistoryLst;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private ToolStripMenuItem deleteEntryToolStripMenuItem;
        private ToolStripMenuItem deleteAllHistoryToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private Button btnStopAll;
    }
}
