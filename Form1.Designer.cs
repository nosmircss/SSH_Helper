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
            openFileDialog1 = new OpenFileDialog();
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
            ((System.ComponentModel.ISupportInitialize)dgv_variables).BeginInit();
            contextMenuStrip1.SuspendLayout();
            contextPresetLst.SuspendLayout();
            contextPresetLstAdd.SuspendLayout();
            SuspendLayout();
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // btnOpenCSV
            // 
            btnOpenCSV.Location = new Point(18, 16);
            btnOpenCSV.Margin = new Padding(3, 4, 3, 4);
            btnOpenCSV.Name = "btnOpenCSV";
            btnOpenCSV.Size = new Size(86, 31);
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
            dgv_variables.Location = new Point(18, 57);
            dgv_variables.Margin = new Padding(3, 4, 3, 4);
            dgv_variables.Name = "dgv_variables";
            dgv_variables.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            dgv_variables.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgv_variables.Size = new Size(720, 225);
            dgv_variables.TabIndex = 1;
            dgv_variables.CellDoubleClick += dgv_variables_CellDoubleClick;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { addColumnToolStripMenuItem, renameColumnToolStripMenuItem, deleteColumnToolStripMenuItem, deleteRowToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(188, 100);
            // 
            // addColumnToolStripMenuItem
            // 
            addColumnToolStripMenuItem.Name = "addColumnToolStripMenuItem";
            addColumnToolStripMenuItem.Size = new Size(187, 24);
            addColumnToolStripMenuItem.Text = "Add Column";
            addColumnToolStripMenuItem.Click += addColumnToolStripMenuItem_Click;
            // 
            // renameColumnToolStripMenuItem
            // 
            renameColumnToolStripMenuItem.Name = "renameColumnToolStripMenuItem";
            renameColumnToolStripMenuItem.Size = new Size(187, 24);
            renameColumnToolStripMenuItem.Text = "Rename Column";
            renameColumnToolStripMenuItem.Click += renameColumnToolStripMenuItem_Click;
            // 
            // deleteColumnToolStripMenuItem
            // 
            deleteColumnToolStripMenuItem.Name = "deleteColumnToolStripMenuItem";
            deleteColumnToolStripMenuItem.Size = new Size(187, 24);
            deleteColumnToolStripMenuItem.Text = "Delete Column";
            deleteColumnToolStripMenuItem.Click += deleteColumnToolStripMenuItem_Click;
            // 
            // deleteRowToolStripMenuItem
            // 
            deleteRowToolStripMenuItem.Name = "deleteRowToolStripMenuItem";
            deleteRowToolStripMenuItem.Size = new Size(187, 24);
            deleteRowToolStripMenuItem.Text = "Delete Row";
            deleteRowToolStripMenuItem.Click += deleteRowToolStripMenuItem_Click;
            // 
            // btnSaveAs
            // 
            btnSaveAs.Location = new Point(121, 16);
            btnSaveAs.Margin = new Padding(3, 4, 3, 4);
            btnSaveAs.Name = "btnSaveAs";
            btnSaveAs.Size = new Size(86, 31);
            btnSaveAs.TabIndex = 2;
            btnSaveAs.Text = "Save As";
            btnSaveAs.UseVisualStyleBackColor = true;
            btnSaveAs.Click += btnSaveAs_Click;
            // 
            // btnClear
            // 
            btnClear.Location = new Point(225, 16);
            btnClear.Margin = new Padding(3, 4, 3, 4);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(86, 31);
            btnClear.TabIndex = 3;
            btnClear.Text = "Clear";
            btnClear.UseVisualStyleBackColor = true;
            btnClear.Click += btnClear_Click;
            // 
            // txtOutput
            // 
            txtOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtOutput.Location = new Point(225, 312);
            txtOutput.Margin = new Padding(3, 4, 3, 4);
            txtOutput.MaxLength = 1000000;
            txtOutput.Multiline = true;
            txtOutput.Name = "txtOutput";
            txtOutput.ScrollBars = ScrollBars.Both;
            txtOutput.Size = new Size(1359, 404);
            txtOutput.TabIndex = 4;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(225, 288);
            label1.Name = "label1";
            label1.Size = new Size(55, 20);
            label1.TabIndex = 5;
            label1.Text = "Output";
            // 
            // txtPreset
            // 
            txtPreset.Location = new Point(1151, 12);
            txtPreset.Margin = new Padding(3, 4, 3, 4);
            txtPreset.Name = "txtPreset";
            txtPreset.Size = new Size(263, 27);
            txtPreset.TabIndex = 27;
            txtPreset.Visible = false;
            // 
            // btnSave
            // 
            btnSave.Location = new Point(1150, 16);
            btnSave.Margin = new Padding(3, 4, 3, 4);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(110, 31);
            btnSave.TabIndex = 26;
            btnSave.Text = "Save Preset";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(849, 27);
            label6.Name = "label6";
            label6.Size = new Size(102, 20);
            label6.TabIndex = 25;
            label6.Text = "Saved Presets:";
            // 
            // lstPreset
            // 
            lstPreset.FormattingEnabled = true;
            lstPreset.IntegralHeight = false;
            lstPreset.Location = new Point(746, 57);
            lstPreset.Margin = new Padding(3, 4, 3, 4);
            lstPreset.Name = "lstPreset";
            lstPreset.Size = new Size(306, 224);
            lstPreset.Sorted = true;
            lstPreset.TabIndex = 24;
            lstPreset.SelectedIndexChanged += lstPreset_SelectedIndexChanged;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(1098, 55);
            label4.Name = "label4";
            label4.Size = new Size(50, 20);
            label4.TabIndex = 23;
            label4.Text = "Script:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(542, 20);
            label3.Name = "label3";
            label3.Size = new Size(73, 20);
            label3.TabIndex = 22;
            label3.Text = "Password:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(322, 21);
            label2.Name = "label2";
            label2.Size = new Size(78, 20);
            label2.TabIndex = 21;
            label2.Text = "Username:";
            // 
            // btnExecuteAll
            // 
            btnExecuteAll.Location = new Point(1059, 229);
            btnExecuteAll.Margin = new Padding(3, 4, 3, 4);
            btnExecuteAll.Name = "btnExecuteAll";
            btnExecuteAll.Size = new Size(86, 53);
            btnExecuteAll.TabIndex = 20;
            btnExecuteAll.Text = "Execute All";
            btnExecuteAll.UseVisualStyleBackColor = true;
            btnExecuteAll.Click += btnExecuteAll_Click;
            // 
            // txtCommand
            // 
            txtCommand.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtCommand.Location = new Point(1151, 57);
            txtCommand.Margin = new Padding(3, 4, 3, 4);
            txtCommand.Multiline = true;
            txtCommand.Name = "txtCommand";
            txtCommand.Size = new Size(433, 224);
            txtCommand.TabIndex = 19;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(617, 15);
            txtPassword.Margin = new Padding(3, 4, 3, 4);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(173, 27);
            txtPassword.TabIndex = 18;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(401, 17);
            txtUsername.Margin = new Padding(3, 4, 3, 4);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(133, 27);
            txtUsername.TabIndex = 17;
            // 
            // contextPresetLst
            // 
            contextPresetLst.ImageScalingSize = new Size(20, 20);
            contextPresetLst.Items.AddRange(new ToolStripItem[] { addPresetToolStripMenuItem, renameToolStripMenuItem, deleteToolStripMenuItem });
            contextPresetLst.Name = "contextPresetLst";
            contextPresetLst.Size = new Size(177, 76);
            // 
            // addPresetToolStripMenuItem
            // 
            addPresetToolStripMenuItem.Name = "addPresetToolStripMenuItem";
            addPresetToolStripMenuItem.Size = new Size(176, 24);
            addPresetToolStripMenuItem.Text = "Add Preset";
            addPresetToolStripMenuItem.Click += addPresetToolStripMenuItem_Click;
            // 
            // renameToolStripMenuItem
            // 
            renameToolStripMenuItem.Name = "renameToolStripMenuItem";
            renameToolStripMenuItem.Size = new Size(176, 24);
            renameToolStripMenuItem.Text = "Rename Preset";
            renameToolStripMenuItem.Click += renameToolStripMenuItem_Click;
            // 
            // deleteToolStripMenuItem
            // 
            deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            deleteToolStripMenuItem.Size = new Size(176, 24);
            deleteToolStripMenuItem.Text = "Delete Preset";
            deleteToolStripMenuItem.Click += deleteToolStripMenuItem_Click;
            // 
            // btnExecuteSelected
            // 
            btnExecuteSelected.Location = new Point(1058, 139);
            btnExecuteSelected.Margin = new Padding(3, 4, 3, 4);
            btnExecuteSelected.Name = "btnExecuteSelected";
            btnExecuteSelected.Size = new Size(86, 53);
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
            contextPresetLstAdd.Size = new Size(151, 28);
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(150, 24);
            toolStripMenuItem1.Text = "Add Preset";
            toolStripMenuItem1.Click += contextPresetLstAdd_Click;
            // 
            // txtDelay
            // 
            txtDelay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtDelay.Location = new Point(1533, 15);
            txtDelay.Name = "txtDelay";
            txtDelay.Size = new Size(51, 27);
            txtDelay.TabIndex = 30;
            txtDelay.TextAlign = HorizontalAlignment.Right;
            // 
            // label5
            // 
            label5.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label5.Location = new Point(1477, 18);
            label5.Name = "label5";
            label5.Size = new Size(50, 20);
            label5.TabIndex = 31;
            label5.Text = "Delay:";
            // 
            // txtTimeout
            // 
            txtTimeout.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtTimeout.Location = new Point(1420, 15);
            txtTimeout.Name = "txtTimeout";
            txtTimeout.Size = new Size(50, 27);
            txtTimeout.TabIndex = 32;
            txtTimeout.TextAlign = HorizontalAlignment.Right;
            // 
            // label7
            // 
            label7.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label7.AutoSize = true;
            label7.Location = new Point(1351, 18);
            label7.Name = "label7";
            label7.Size = new Size(67, 20);
            label7.TabIndex = 33;
            label7.Text = "Timeout:";
            // 
            // lstOutput
            // 
            lstOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            lstOutput.FormattingEnabled = true;
            lstOutput.IntegralHeight = false;
            lstOutput.Location = new Point(18, 312);
            lstOutput.Name = "lstOutput";
            lstOutput.Size = new Size(201, 405);
            lstOutput.TabIndex = 34;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1598, 736);
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
            Controls.Add(btnExecuteAll);
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
            Margin = new Padding(3, 4, 3, 4);
            Name = "Form1";
            Text = "Salihu_Helper";
            WindowState = FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)dgv_variables).EndInit();
            contextMenuStrip1.ResumeLayout(false);
            contextPresetLst.ResumeLayout(false);
            contextPresetLstAdd.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private OpenFileDialog openFileDialog1;
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
    }
}
