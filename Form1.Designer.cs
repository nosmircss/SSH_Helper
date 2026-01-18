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
            mainSplitContainer = new SplitContainer();
            topSplitContainer = new SplitContainer();
            hostsPanel = new Panel();
            dgv_variables = new DataGridView();
            contextMenuStrip1 = new ContextMenuStrip(components);
            addColumnToolStripMenuItem = new ToolStripMenuItem();
            renameColumnToolStripMenuItem = new ToolStripMenuItem();
            deleteColumnToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator5 = new ToolStripSeparator();
            deleteRowToolStripMenuItem = new ToolStripMenuItem();
            hostsHeaderPanel = new Panel();
            lblHostsTitle = new Label();
            lblHostCount = new Label();
            commandPanel = new Panel();
            commandSplitContainer = new SplitContainer();
            presetsPanel = new Panel();
            trvPresets = new TreeView();
            contextPresetLst = new ContextMenuStrip(components);
            ctxAddPreset = new ToolStripMenuItem();
            ctxDuplicatePreset = new ToolStripMenuItem();
            ctxRenamePreset = new ToolStripMenuItem();
            ctxDeletePreset = new ToolStripMenuItem();
            toolStripSeparator6 = new ToolStripSeparator();
            ctxToggleFavorite = new ToolStripMenuItem();
            toolStripSeparator7 = new ToolStripSeparator();
            ctxExportPreset = new ToolStripMenuItem();
            ctxImportPreset = new ToolStripMenuItem();
            ctxToggleSorting = new ToolStripMenuItem();
            toolStripSeparatorFolders = new ToolStripSeparator();
            ctxAddFolder = new ToolStripMenuItem();
            ctxRenameFolder = new ToolStripMenuItem();
            ctxDeleteFolder = new ToolStripMenuItem();
            ctxMoveToFolder = new ToolStripMenuItem();
            presetsToolStrip = new ToolStrip();
            tsbAddPreset = new ToolStripButton();
            tsbDeletePreset = new ToolStripButton();
            tsbRenamePreset = new ToolStripButton();
            tsbDuplicatePreset = new ToolStripButton();
            tsbSeparatorFolders = new ToolStripSeparator();
            tsbAddFolder = new ToolStripButton();
            tsbDeleteFolder = new ToolStripButton();
            presetsHeaderPanel = new Panel();
            lblPresetsTitle = new Label();
            scriptPanel = new Panel();
            txtCommand = new TextBox();
            scriptFooterPanel = new Panel();
            lblLinePosition = new Label();
            scriptHeaderPanel = new Panel();
            lblPresetName = new Label();
            txtPreset = new TextBox();
            lblTimeoutHeader = new Label();
            txtTimeoutHeader = new TextBox();
            btnSavePreset = new Button();
            lblScriptTitle = new Label();
            executePanel = new Panel();
            btnExecuteAll = new Button();
            btnExecuteSelected = new Button();
            btnStopAll = new Button();
            outputPanel = new Panel();
            outputSplitContainer = new SplitContainer();
            historyPanel = new Panel();
            historySplitContainer = new SplitContainer();
            lstOutput = new ListBox();
            contextHistoryLst = new ContextMenuStrip(components);
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            saveAllToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator8 = new ToolStripSeparator();
            toolStripSeparator9 = new ToolStripSeparator();
            deleteEntryToolStripMenuItem = new ToolStripMenuItem();
            deleteAllHistoryToolStripMenuItem = new ToolStripMenuItem();
            hostListPanel = new Panel();
            lstHosts = new ListBox();
            contextHostLst = new ContextMenuStrip(components);
            exportHostOutputToolStripMenuItem = new ToolStripMenuItem();
            hostHeaderPanel = new Panel();
            lblHostsListTitle = new Label();
            historyHeaderPanel = new Panel();
            lblHistoryTitle = new Label();
            txtOutput = new TextBox();
            mainToolStrip = new ToolStrip();
            tsbOpenCsv = new ToolStripButton();
            tsbSaveCsv = new ToolStripButton();
            tsbSaveCsvAs = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            tsbClearGrid = new ToolStripButton();
            toolStripSeparator2 = new ToolStripSeparator();
            toolStripLabel1 = new ToolStripLabel();
            tsbUsername = new ToolStripTextBox();
            toolStripLabel2 = new ToolStripLabel();
            tsbPassword = new ToolStripTextBox();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            statusProgress = new ToolStripProgressBar();
            statusHostCount = new ToolStripStatusLabel();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openCSVToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem = new ToolStripMenuItem();
            saveAsToolStripMenuItem1 = new ToolStripMenuItem();
            toolStripSeparator4 = new ToolStripSeparator();
            exportAllPresetsToolStripMenuItem = new ToolStripMenuItem();
            importAllPresetsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator9 = new ToolStripSeparator();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator10 = new ToolStripSeparator();
            ExitMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            findToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparatorEdit1 = new ToolStripSeparator();
            debugModeToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            checkForUpdatesToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparatorHelp1 = new ToolStripSeparator();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            contextPresetLstAdd = new ContextMenuStrip(components);
            ctxAddPreset2 = new ToolStripMenuItem();
            ctxImportPreset2 = new ToolStripMenuItem();
            txtUsername = new TextBox();
            txtPassword = new TextBox();
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).BeginInit();
            mainSplitContainer.Panel1.SuspendLayout();
            mainSplitContainer.Panel2.SuspendLayout();
            mainSplitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)topSplitContainer).BeginInit();
            topSplitContainer.Panel1.SuspendLayout();
            topSplitContainer.Panel2.SuspendLayout();
            topSplitContainer.SuspendLayout();
            hostsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgv_variables).BeginInit();
            contextMenuStrip1.SuspendLayout();
            hostsHeaderPanel.SuspendLayout();
            commandPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)commandSplitContainer).BeginInit();
            commandSplitContainer.Panel1.SuspendLayout();
            commandSplitContainer.Panel2.SuspendLayout();
            commandSplitContainer.SuspendLayout();
            presetsPanel.SuspendLayout();
            contextPresetLst.SuspendLayout();
            presetsToolStrip.SuspendLayout();
            presetsHeaderPanel.SuspendLayout();
            scriptPanel.SuspendLayout();
            scriptFooterPanel.SuspendLayout();
            scriptHeaderPanel.SuspendLayout();
            executePanel.SuspendLayout();
            outputPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)outputSplitContainer).BeginInit();
            outputSplitContainer.Panel1.SuspendLayout();
            outputSplitContainer.Panel2.SuspendLayout();
            outputSplitContainer.SuspendLayout();
            historyPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)historySplitContainer).BeginInit();
            historySplitContainer.Panel1.SuspendLayout();
            historySplitContainer.Panel2.SuspendLayout();
            historySplitContainer.SuspendLayout();
            contextHistoryLst.SuspendLayout();
            hostListPanel.SuspendLayout();
            contextHostLst.SuspendLayout();
            hostHeaderPanel.SuspendLayout();
            historyHeaderPanel.SuspendLayout();
            mainToolStrip.SuspendLayout();
            statusStrip.SuspendLayout();
            menuStrip1.SuspendLayout();
            contextPresetLstAdd.SuspendLayout();
            SuspendLayout();
            // 
            // mainSplitContainer
            // 
            mainSplitContainer.Dock = DockStyle.Fill;
            mainSplitContainer.Location = new Point(0, 49);
            mainSplitContainer.Name = "mainSplitContainer";
            mainSplitContainer.Orientation = Orientation.Horizontal;
            // 
            // mainSplitContainer.Panel1
            // 
            mainSplitContainer.Panel1.Controls.Add(topSplitContainer);
            // 
            // mainSplitContainer.Panel2
            // 
            mainSplitContainer.Panel2.Controls.Add(outputPanel);
            mainSplitContainer.Size = new Size(1400, 671);
            mainSplitContainer.SplitterDistance = 354;
            mainSplitContainer.SplitterWidth = 6;
            mainSplitContainer.TabIndex = 1;
            // 
            // topSplitContainer
            // 
            topSplitContainer.Dock = DockStyle.Fill;
            topSplitContainer.Location = new Point(0, 0);
            topSplitContainer.Name = "topSplitContainer";
            // 
            // topSplitContainer.Panel1
            // 
            topSplitContainer.Panel1.Controls.Add(hostsPanel);
            // 
            // topSplitContainer.Panel2
            // 
            topSplitContainer.Panel2.Controls.Add(commandPanel);
            topSplitContainer.Size = new Size(1400, 354);
            topSplitContainer.SplitterDistance = 485;
            topSplitContainer.SplitterWidth = 6;
            topSplitContainer.TabIndex = 0;
            // 
            // hostsPanel
            // 
            hostsPanel.BackColor = Color.White;
            hostsPanel.Controls.Add(dgv_variables);
            hostsPanel.Controls.Add(hostsHeaderPanel);
            hostsPanel.Dock = DockStyle.Fill;
            hostsPanel.Location = new Point(0, 0);
            hostsPanel.Name = "hostsPanel";
            hostsPanel.Padding = new Padding(8);
            hostsPanel.Size = new Size(485, 354);
            hostsPanel.TabIndex = 0;
            // 
            // dgv_variables
            // 
            dgv_variables.AllowUserToOrderColumns = true;
            dgv_variables.BackgroundColor = Color.White;
            dgv_variables.BorderStyle = BorderStyle.None;
            dgv_variables.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            dgv_variables.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv_variables.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv_variables.ContextMenuStrip = contextMenuStrip1;
            dgv_variables.Dock = DockStyle.Fill;
            dgv_variables.EditMode = DataGridViewEditMode.EditProgrammatically;
            dgv_variables.GridColor = Color.FromArgb(222, 226, 230);
            dgv_variables.Location = new Point(8, 44);
            dgv_variables.Name = "dgv_variables";
            dgv_variables.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            dgv_variables.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgv_variables.Size = new Size(469, 302);
            dgv_variables.TabIndex = 1;
            dgv_variables.CellDoubleClick += dgv_variables_CellDoubleClick;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { addColumnToolStripMenuItem, renameColumnToolStripMenuItem, deleteColumnToolStripMenuItem, toolStripSeparator5, deleteRowToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(164, 98);
            // 
            // addColumnToolStripMenuItem
            // 
            addColumnToolStripMenuItem.Name = "addColumnToolStripMenuItem";
            addColumnToolStripMenuItem.Size = new Size(163, 22);
            addColumnToolStripMenuItem.Text = "&Add Column";
            addColumnToolStripMenuItem.Click += addColumnToolStripMenuItem_Click;
            // 
            // renameColumnToolStripMenuItem
            // 
            renameColumnToolStripMenuItem.Name = "renameColumnToolStripMenuItem";
            renameColumnToolStripMenuItem.Size = new Size(163, 22);
            renameColumnToolStripMenuItem.Text = "&Rename Column";
            renameColumnToolStripMenuItem.Click += renameColumnToolStripMenuItem_Click;
            // 
            // deleteColumnToolStripMenuItem
            // 
            deleteColumnToolStripMenuItem.Name = "deleteColumnToolStripMenuItem";
            deleteColumnToolStripMenuItem.Size = new Size(163, 22);
            deleteColumnToolStripMenuItem.Text = "&Delete Column";
            deleteColumnToolStripMenuItem.Click += deleteColumnToolStripMenuItem_Click;
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(160, 6);
            // 
            // deleteRowToolStripMenuItem
            // 
            deleteRowToolStripMenuItem.Name = "deleteRowToolStripMenuItem";
            deleteRowToolStripMenuItem.Size = new Size(163, 22);
            deleteRowToolStripMenuItem.Text = "Delete &Row";
            deleteRowToolStripMenuItem.Click += deleteRowToolStripMenuItem_Click;
            // 
            // hostsHeaderPanel
            // 
            hostsHeaderPanel.BackColor = Color.FromArgb(248, 249, 250);
            hostsHeaderPanel.Controls.Add(lblHostsTitle);
            hostsHeaderPanel.Controls.Add(lblHostCount);
            hostsHeaderPanel.Dock = DockStyle.Top;
            hostsHeaderPanel.Location = new Point(8, 8);
            hostsHeaderPanel.Name = "hostsHeaderPanel";
            hostsHeaderPanel.Padding = new Padding(12, 8, 12, 8);
            hostsHeaderPanel.Size = new Size(469, 36);
            hostsHeaderPanel.TabIndex = 2;
            // 
            // lblHostsTitle
            // 
            lblHostsTitle.AutoSize = true;
            lblHostsTitle.Dock = DockStyle.Left;
            lblHostsTitle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            lblHostsTitle.ForeColor = Color.FromArgb(33, 37, 41);
            lblHostsTitle.Location = new Point(12, 8);
            lblHostsTitle.Name = "lblHostsTitle";
            lblHostsTitle.Size = new Size(44, 19);
            lblHostsTitle.TabIndex = 0;
            lblHostsTitle.Text = "Hosts";
            // 
            // lblHostCount
            // 
            lblHostCount.AutoSize = true;
            lblHostCount.Dock = DockStyle.Right;
            lblHostCount.Font = new Font("Segoe UI", 9F);
            lblHostCount.ForeColor = Color.FromArgb(108, 117, 125);
            lblHostCount.Location = new Point(413, 8);
            lblHostCount.Name = "lblHostCount";
            lblHostCount.Size = new Size(44, 15);
            lblHostCount.TabIndex = 1;
            lblHostCount.Text = "0 hosts";
            // 
            // commandPanel
            // 
            commandPanel.BackColor = Color.White;
            commandPanel.Controls.Add(commandSplitContainer);
            commandPanel.Dock = DockStyle.Fill;
            commandPanel.Location = new Point(0, 0);
            commandPanel.Name = "commandPanel";
            commandPanel.Padding = new Padding(0, 0, 8, 8);
            commandPanel.Size = new Size(909, 354);
            commandPanel.TabIndex = 0;
            // 
            // commandSplitContainer
            // 
            commandSplitContainer.Dock = DockStyle.Fill;
            commandSplitContainer.Location = new Point(0, 0);
            commandSplitContainer.Name = "commandSplitContainer";
            // 
            // commandSplitContainer.Panel1
            // 
            commandSplitContainer.Panel1.Controls.Add(presetsPanel);
            // 
            // commandSplitContainer.Panel2
            // 
            commandSplitContainer.Panel2.Controls.Add(scriptPanel);
            commandSplitContainer.Panel2.Controls.Add(executePanel);
            commandSplitContainer.Size = new Size(901, 346);
            commandSplitContainer.SplitterDistance = 639;
            commandSplitContainer.TabIndex = 0;
            // 
            // presetsPanel
            // 
            presetsPanel.BackColor = Color.FromArgb(248, 249, 250);
            presetsPanel.Controls.Add(trvPresets);
            presetsPanel.Controls.Add(presetsToolStrip);
            presetsPanel.Controls.Add(presetsHeaderPanel);
            presetsPanel.Dock = DockStyle.Fill;
            presetsPanel.Location = new Point(0, 0);
            presetsPanel.Name = "presetsPanel";
            presetsPanel.Padding = new Padding(8);
            presetsPanel.Size = new Size(639, 346);
            presetsPanel.TabIndex = 0;
            //
            // trvPresets
            //
            trvPresets.BackColor = Color.White;
            trvPresets.BorderStyle = BorderStyle.None;
            trvPresets.ContextMenuStrip = contextPresetLst;
            trvPresets.Dock = DockStyle.Fill;
            trvPresets.Font = new Font("Segoe UI", 9.5F);
            trvPresets.FullRowSelect = true;
            trvPresets.HideSelection = false;
            trvPresets.ItemHeight = 20;
            trvPresets.Location = new Point(8, 65);
            trvPresets.Name = "trvPresets";
            trvPresets.ShowLines = true;
            trvPresets.ShowPlusMinus = true;
            trvPresets.ShowRootLines = true;
            trvPresets.Size = new Size(623, 273);
            trvPresets.TabIndex = 1;
            trvPresets.AfterSelect += trvPresets_AfterSelect;
            trvPresets.AfterCollapse += trvPresets_AfterCollapse;
            trvPresets.AfterExpand += trvPresets_AfterExpand;
            trvPresets.BeforeCollapse += trvPresets_BeforeCollapse;
            trvPresets.BeforeExpand += trvPresets_BeforeExpand;
            trvPresets.MouseDown += trvPresets_MouseDown;
            trvPresets.NodeMouseClick += trvPresets_NodeMouseClick;
            trvPresets.NodeMouseDoubleClick += trvPresets_NodeMouseDoubleClick;
            trvPresets.ItemDrag += trvPresets_ItemDrag;
            trvPresets.DragOver += trvPresets_DragOver;
            trvPresets.DragDrop += trvPresets_DragDrop;
            trvPresets.AllowDrop = true;
            // 
            // contextPresetLst
            // 
            contextPresetLst.Items.AddRange(new ToolStripItem[] { ctxAddPreset, ctxDuplicatePreset, ctxRenamePreset, ctxDeletePreset, toolStripSeparator6, ctxToggleFavorite, ctxMoveToFolder, toolStripSeparator7, ctxExportPreset, ctxImportPreset, ctxToggleSorting, toolStripSeparatorFolders, ctxAddFolder, ctxRenameFolder, ctxDeleteFolder });
            contextPresetLst.Name = "contextPresetLst";
            contextPresetLst.Size = new Size(160, 192);
            // 
            // ctxAddPreset
            // 
            ctxAddPreset.Name = "ctxAddPreset";
            ctxAddPreset.Size = new Size(159, 22);
            ctxAddPreset.Text = "&Add Preset";
            ctxAddPreset.Click += addPresetToolStripMenuItem_Click;
            // 
            // ctxDuplicatePreset
            // 
            ctxDuplicatePreset.Name = "ctxDuplicatePreset";
            ctxDuplicatePreset.Size = new Size(159, 22);
            ctxDuplicatePreset.Text = "D&uplicate Preset";
            ctxDuplicatePreset.Click += duplicatePresetToolStripMenuItem_Click;
            // 
            // ctxRenamePreset
            // 
            ctxRenamePreset.Name = "ctxRenamePreset";
            ctxRenamePreset.Size = new Size(159, 22);
            ctxRenamePreset.Text = "&Rename Preset";
            ctxRenamePreset.Click += renameToolStripMenuItem_Click;
            // 
            // ctxDeletePreset
            // 
            ctxDeletePreset.Name = "ctxDeletePreset";
            ctxDeletePreset.Size = new Size(159, 22);
            ctxDeletePreset.Text = "&Delete Preset";
            ctxDeletePreset.Click += deleteToolStripMenuItem_Click;
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            toolStripSeparator6.Size = new Size(156, 6);
            // 
            // ctxToggleFavorite
            // 
            ctxToggleFavorite.Name = "ctxToggleFavorite";
            ctxToggleFavorite.Size = new Size(159, 22);
            ctxToggleFavorite.Text = "Toggle &Favorite";
            ctxToggleFavorite.Click += ctxToggleFavorite_Click;
            // 
            // toolStripSeparator7
            // 
            toolStripSeparator7.Name = "toolStripSeparator7";
            toolStripSeparator7.Size = new Size(156, 6);
            // 
            // ctxExportPreset
            // 
            ctxExportPreset.Name = "ctxExportPreset";
            ctxExportPreset.Size = new Size(159, 22);
            ctxExportPreset.Text = "&Export Preset";
            ctxExportPreset.Click += ExportPreset_Click;
            // 
            // ctxImportPreset
            // 
            ctxImportPreset.Name = "ctxImportPreset";
            ctxImportPreset.Size = new Size(159, 22);
            ctxImportPreset.Text = "&Import Preset";
            ctxImportPreset.Click += ImportPreset_Click;
            //
            // ctxToggleSorting
            //
            ctxToggleSorting.Name = "ctxToggleSorting";
            ctxToggleSorting.Size = new Size(159, 22);
            ctxToggleSorting.Text = "Toggle &Sorting";
            ctxToggleSorting.Click += toggleSortingToolStripMenuItem_Click;
            //
            // toolStripSeparatorFolders
            //
            toolStripSeparatorFolders.Name = "toolStripSeparatorFolders";
            toolStripSeparatorFolders.Size = new Size(156, 6);
            //
            // ctxAddFolder
            //
            ctxAddFolder.Name = "ctxAddFolder";
            ctxAddFolder.Size = new Size(159, 22);
            ctxAddFolder.Text = "New &Folder";
            ctxAddFolder.Click += ctxAddFolder_Click;
            //
            // ctxRenameFolder
            //
            ctxRenameFolder.Name = "ctxRenameFolder";
            ctxRenameFolder.Size = new Size(159, 22);
            ctxRenameFolder.Text = "Rename Fol&der";
            ctxRenameFolder.Click += ctxRenameFolder_Click;
            //
            // ctxDeleteFolder
            //
            ctxDeleteFolder.Name = "ctxDeleteFolder";
            ctxDeleteFolder.Size = new Size(159, 22);
            ctxDeleteFolder.Text = "Delete Folde&r";
            ctxDeleteFolder.Click += ctxDeleteFolder_Click;
            //
            // ctxMoveToFolder
            //
            ctxMoveToFolder.Name = "ctxMoveToFolder";
            ctxMoveToFolder.Size = new Size(159, 22);
            ctxMoveToFolder.Text = "&Move to Folder";
            //
            // presetsToolStrip
            //
            presetsToolStrip.AutoSize = false;
            presetsToolStrip.BackColor = Color.FromArgb(248, 249, 250);
            presetsToolStrip.Dock = DockStyle.Top;
            presetsToolStrip.GripStyle = ToolStripGripStyle.Hidden;
            presetsToolStrip.Items.AddRange(new ToolStripItem[] { tsbAddPreset, tsbDeletePreset, tsbRenamePreset, tsbDuplicatePreset, tsbSeparatorFolders, tsbAddFolder, tsbDeleteFolder });
            presetsToolStrip.LayoutStyle = ToolStripLayoutStyle.Flow;
            presetsToolStrip.Location = new Point(8, 40);
            presetsToolStrip.Name = "presetsToolStrip";
            presetsToolStrip.Padding = new Padding(0);
            presetsToolStrip.RenderMode = ToolStripRenderMode.System;
            presetsToolStrip.Size = new Size(623, 25);
            presetsToolStrip.TabIndex = 0;
            // 
            // tsbAddPreset
            // 
            tsbAddPreset.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbAddPreset.Name = "tsbAddPreset";
            tsbAddPreset.Size = new Size(23, 22);
            tsbAddPreset.Text = "+";
            tsbAddPreset.ToolTipText = "Add new preset";
            tsbAddPreset.Click += addPresetToolStripMenuItem_Click;
            // 
            // tsbDeletePreset
            // 
            tsbDeletePreset.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbDeletePreset.Name = "tsbDeletePreset";
            tsbDeletePreset.Size = new Size(23, 22);
            tsbDeletePreset.Text = "-";
            tsbDeletePreset.ToolTipText = "Delete preset";
            tsbDeletePreset.Click += deleteToolStripMenuItem_Click;
            // 
            // tsbRenamePreset
            // 
            tsbRenamePreset.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbRenamePreset.Name = "tsbRenamePreset";
            tsbRenamePreset.Size = new Size(54, 22);
            tsbRenamePreset.Text = "Rename";
            tsbRenamePreset.ToolTipText = "Rename preset";
            tsbRenamePreset.Click += renameToolStripMenuItem_Click;
            // 
            // tsbDuplicatePreset
            // 
            tsbDuplicatePreset.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbDuplicatePreset.Name = "tsbDuplicatePreset";
            tsbDuplicatePreset.Size = new Size(39, 22);
            tsbDuplicatePreset.Text = "Copy";
            tsbDuplicatePreset.ToolTipText = "Duplicate preset";
            tsbDuplicatePreset.Click += duplicatePresetToolStripMenuItem_Click;
            //
            // tsbSeparatorFolders
            //
            tsbSeparatorFolders.Name = "tsbSeparatorFolders";
            tsbSeparatorFolders.Size = new Size(6, 25);
            //
            // tsbAddFolder
            //
            tsbAddFolder.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbAddFolder.Name = "tsbAddFolder";
            tsbAddFolder.Size = new Size(23, 22);
            tsbAddFolder.Text = "📁+";
            tsbAddFolder.ToolTipText = "Add new folder";
            tsbAddFolder.Click += tsbAddFolder_Click;
            //
            // tsbDeleteFolder
            //
            tsbDeleteFolder.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbDeleteFolder.Name = "tsbDeleteFolder";
            tsbDeleteFolder.Size = new Size(23, 22);
            tsbDeleteFolder.Text = "📁-";
            tsbDeleteFolder.ToolTipText = "Delete folder and all presets";
            tsbDeleteFolder.Click += tsbDeleteFolder_Click;
            //
            // presetsHeaderPanel
            // 
            presetsHeaderPanel.BackColor = Color.FromArgb(248, 249, 250);
            presetsHeaderPanel.Controls.Add(lblPresetsTitle);
            presetsHeaderPanel.Dock = DockStyle.Top;
            presetsHeaderPanel.Location = new Point(8, 8);
            presetsHeaderPanel.Name = "presetsHeaderPanel";
            presetsHeaderPanel.Padding = new Padding(4);
            presetsHeaderPanel.Size = new Size(623, 32);
            presetsHeaderPanel.TabIndex = 2;
            // 
            // lblPresetsTitle
            // 
            lblPresetsTitle.AutoSize = true;
            lblPresetsTitle.Dock = DockStyle.Left;
            lblPresetsTitle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            lblPresetsTitle.ForeColor = Color.FromArgb(33, 37, 41);
            lblPresetsTitle.Location = new Point(4, 4);
            lblPresetsTitle.Name = "lblPresetsTitle";
            lblPresetsTitle.Size = new Size(53, 19);
            lblPresetsTitle.TabIndex = 0;
            lblPresetsTitle.Text = "Presets";
            // 
            // scriptPanel
            // 
            scriptPanel.BackColor = Color.White;
            scriptPanel.Controls.Add(txtCommand);
            scriptPanel.Controls.Add(scriptFooterPanel);
            scriptPanel.Controls.Add(scriptHeaderPanel);
            scriptPanel.Dock = DockStyle.Fill;
            scriptPanel.Location = new Point(0, 0);
            scriptPanel.Name = "scriptPanel";
            scriptPanel.Padding = new Padding(0, 0, 0, 8);
            scriptPanel.Size = new Size(258, 296);
            scriptPanel.TabIndex = 0;
            // 
            // txtCommand
            // 
            txtCommand.AcceptsTab = true;
            txtCommand.BackColor = Color.FromArgb(253, 253, 253);
            txtCommand.BorderStyle = BorderStyle.None;
            txtCommand.Dock = DockStyle.Fill;
            txtCommand.Font = new Font("Cascadia Code", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtCommand.Location = new Point(0, 60);
            txtCommand.Multiline = true;
            txtCommand.Name = "txtCommand";
            txtCommand.ScrollBars = ScrollBars.Both;
            txtCommand.Size = new Size(258, 208);
            txtCommand.TabIndex = 0;
            txtCommand.WordWrap = false;
            // 
            // scriptFooterPanel
            // 
            scriptFooterPanel.BackColor = Color.FromArgb(248, 249, 250);
            scriptFooterPanel.Controls.Add(lblLinePosition);
            scriptFooterPanel.Dock = DockStyle.Bottom;
            scriptFooterPanel.Location = new Point(0, 268);
            scriptFooterPanel.Name = "scriptFooterPanel";
            scriptFooterPanel.Size = new Size(258, 20);
            scriptFooterPanel.TabIndex = 2;
            // 
            // lblLinePosition
            // 
            lblLinePosition.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblLinePosition.Font = new Font("Segoe UI", 8F);
            lblLinePosition.ForeColor = Color.FromArgb(108, 117, 125);
            lblLinePosition.Location = new Point(216, 2);
            lblLinePosition.Name = "lblLinePosition";
            lblLinePosition.Size = new Size(96, 16);
            lblLinePosition.TabIndex = 0;
            lblLinePosition.Text = "Ln 1, Col 1";
            lblLinePosition.TextAlign = ContentAlignment.MiddleRight;
            // 
            // scriptHeaderPanel
            // 
            scriptHeaderPanel.BackColor = Color.FromArgb(248, 249, 250);
            scriptHeaderPanel.Controls.Add(lblPresetName);
            scriptHeaderPanel.Controls.Add(txtPreset);
            scriptHeaderPanel.Controls.Add(lblTimeoutHeader);
            scriptHeaderPanel.Controls.Add(txtTimeoutHeader);
            scriptHeaderPanel.Controls.Add(btnSavePreset);
            scriptHeaderPanel.Controls.Add(lblScriptTitle);
            scriptHeaderPanel.Dock = DockStyle.Top;
            scriptHeaderPanel.Location = new Point(0, 0);
            scriptHeaderPanel.Name = "scriptHeaderPanel";
            scriptHeaderPanel.Padding = new Padding(8, 4, 8, 4);
            scriptHeaderPanel.Size = new Size(258, 60);
            scriptHeaderPanel.TabIndex = 1;
            // 
            // lblPresetName
            // 
            lblPresetName.AutoSize = true;
            lblPresetName.Font = new Font("Segoe UI", 9F);
            lblPresetName.ForeColor = Color.FromArgb(108, 117, 125);
            lblPresetName.Location = new Point(8, 10);
            lblPresetName.Name = "lblPresetName";
            lblPresetName.Size = new Size(42, 15);
            lblPresetName.TabIndex = 0;
            lblPresetName.Text = "Name:";
            // 
            // txtPreset
            // 
            txtPreset.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtPreset.BorderStyle = BorderStyle.FixedSingle;
            txtPreset.Font = new Font("Segoe UI", 9F);
            txtPreset.Location = new Point(55, 7);
            txtPreset.Name = "txtPreset";
            txtPreset.PlaceholderText = "Preset name...";
            txtPreset.Size = new Size(20, 23);
            txtPreset.TabIndex = 1;
            // 
            // lblTimeoutHeader
            // 
            lblTimeoutHeader.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTimeoutHeader.AutoSize = true;
            lblTimeoutHeader.Font = new Font("Segoe UI", 9F);
            lblTimeoutHeader.ForeColor = Color.FromArgb(108, 117, 125);
            lblTimeoutHeader.Location = new Point(80, 10);
            lblTimeoutHeader.Name = "lblTimeoutHeader";
            lblTimeoutHeader.Size = new Size(71, 15);
            lblTimeoutHeader.TabIndex = 2;
            lblTimeoutHeader.Text = "Timeout (s):";
            // 
            // txtTimeoutHeader
            // 
            txtTimeoutHeader.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtTimeoutHeader.BorderStyle = BorderStyle.FixedSingle;
            txtTimeoutHeader.Font = new Font("Segoe UI", 9F);
            txtTimeoutHeader.Location = new Point(152, 7);
            txtTimeoutHeader.Name = "txtTimeoutHeader";
            txtTimeoutHeader.Size = new Size(40, 23);
            txtTimeoutHeader.TabIndex = 3;
            txtTimeoutHeader.Text = "10";
            txtTimeoutHeader.TextAlign = HorizontalAlignment.Center;
            // 
            // btnSavePreset
            // 
            btnSavePreset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSavePreset.BackColor = Color.FromArgb(13, 110, 253);
            btnSavePreset.FlatAppearance.BorderSize = 0;
            btnSavePreset.FlatStyle = FlatStyle.Flat;
            btnSavePreset.Font = new Font("Segoe UI", 9F);
            btnSavePreset.ForeColor = Color.White;
            btnSavePreset.Location = new Point(196, 5);
            btnSavePreset.Name = "btnSavePreset";
            btnSavePreset.Size = new Size(54, 27);
            btnSavePreset.TabIndex = 4;
            btnSavePreset.Text = "Save";
            btnSavePreset.UseVisualStyleBackColor = false;
            btnSavePreset.Click += btnSave_Click;
            // 
            // lblScriptTitle
            // 
            lblScriptTitle.AutoSize = true;
            lblScriptTitle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            lblScriptTitle.ForeColor = Color.FromArgb(33, 37, 41);
            lblScriptTitle.Location = new Point(8, 38);
            lblScriptTitle.Name = "lblScriptTitle";
            lblScriptTitle.Size = new Size(68, 15);
            lblScriptTitle.TabIndex = 5;
            lblScriptTitle.Text = "Commands";
            // 
            // executePanel
            // 
            executePanel.BackColor = Color.FromArgb(248, 249, 250);
            executePanel.Controls.Add(btnExecuteAll);
            executePanel.Controls.Add(btnExecuteSelected);
            executePanel.Controls.Add(btnStopAll);
            executePanel.Dock = DockStyle.Bottom;
            executePanel.Location = new Point(0, 296);
            executePanel.Name = "executePanel";
            executePanel.Padding = new Padding(8);
            executePanel.Size = new Size(258, 50);
            executePanel.TabIndex = 1;
            //
            // btnExecuteAll
            //
            btnExecuteAll.BackColor = Color.FromArgb(25, 135, 84);
            btnExecuteAll.Cursor = Cursors.Hand;
            btnExecuteAll.FlatAppearance.BorderSize = 0;
            btnExecuteAll.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 108, 67);
            btnExecuteAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(21, 117, 73);
            btnExecuteAll.FlatStyle = FlatStyle.Flat;
            btnExecuteAll.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnExecuteAll.ForeColor = Color.White;
            btnExecuteAll.Location = new Point(138, 8);
            btnExecuteAll.Name = "btnExecuteAll";
            btnExecuteAll.Size = new Size(120, 34);
            btnExecuteAll.TabIndex = 1;
            btnExecuteAll.Text = "Run All";
            btnExecuteAll.UseVisualStyleBackColor = false;
            btnExecuteAll.Click += btnExecuteAll_Click;
            //
            // btnExecuteSelected
            //
            btnExecuteSelected.BackColor = Color.FromArgb(108, 117, 125);
            btnExecuteSelected.Cursor = Cursors.Hand;
            btnExecuteSelected.FlatAppearance.BorderSize = 0;
            btnExecuteSelected.FlatAppearance.MouseDownBackColor = Color.FromArgb(86, 94, 100);
            btnExecuteSelected.FlatAppearance.MouseOverBackColor = Color.FromArgb(95, 103, 110);
            btnExecuteSelected.FlatStyle = FlatStyle.Flat;
            btnExecuteSelected.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnExecuteSelected.ForeColor = Color.White;
            btnExecuteSelected.Location = new Point(8, 8);
            btnExecuteSelected.Name = "btnExecuteSelected";
            btnExecuteSelected.Size = new Size(122, 34);
            btnExecuteSelected.TabIndex = 0;
            btnExecuteSelected.Text = "Run Selected";
            btnExecuteSelected.UseVisualStyleBackColor = false;
            btnExecuteSelected.Click += btnExecuteSelected_Click;
            //
            // btnStopAll
            //
            btnStopAll.BackColor = Color.FromArgb(220, 53, 69);
            btnStopAll.Cursor = Cursors.Hand;
            btnStopAll.FlatAppearance.BorderSize = 0;
            btnStopAll.FlatAppearance.MouseDownBackColor = Color.FromArgb(176, 42, 55);
            btnStopAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 48, 63);
            btnStopAll.FlatStyle = FlatStyle.Flat;
            btnStopAll.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnStopAll.ForeColor = Color.White;
            btnStopAll.Location = new Point(266, 8);
            btnStopAll.Name = "btnStopAll";
            btnStopAll.Size = new Size(80, 34);
            btnStopAll.TabIndex = 2;
            btnStopAll.Text = "Stop";
            btnStopAll.UseVisualStyleBackColor = false;
            btnStopAll.Visible = false;
            btnStopAll.Click += btnStopAll_Click;
            // 
            // outputPanel
            // 
            outputPanel.BackColor = Color.White;
            outputPanel.Controls.Add(outputSplitContainer);
            outputPanel.Dock = DockStyle.Fill;
            outputPanel.Location = new Point(0, 0);
            outputPanel.Name = "outputPanel";
            outputPanel.Padding = new Padding(8, 0, 8, 8);
            outputPanel.Size = new Size(1400, 311);
            outputPanel.TabIndex = 0;
            // 
            // outputSplitContainer
            // 
            outputSplitContainer.Dock = DockStyle.Fill;
            outputSplitContainer.Location = new Point(8, 0);
            outputSplitContainer.Name = "outputSplitContainer";
            // 
            // outputSplitContainer.Panel1
            // 
            outputSplitContainer.Panel1.Controls.Add(historyPanel);
            // 
            // outputSplitContainer.Panel2
            // 
            outputSplitContainer.Panel2.Controls.Add(txtOutput);
            outputSplitContainer.Size = new Size(1384, 303);
            outputSplitContainer.SplitterDistance = 257;
            outputSplitContainer.SplitterWidth = 6;
            outputSplitContainer.TabIndex = 0;
            //
            // historyPanel
            //
            historyPanel.Controls.Add(historySplitContainer);
            historyPanel.Controls.Add(historyHeaderPanel);
            historyPanel.Dock = DockStyle.Fill;
            historyPanel.Location = new Point(0, 0);
            historyPanel.Name = "historyPanel";
            historyPanel.Size = new Size(257, 303);
            historyPanel.TabIndex = 0;
            //
            // historySplitContainer
            //
            historySplitContainer.Dock = DockStyle.Fill;
            historySplitContainer.Location = new Point(0, 28);
            historySplitContainer.Name = "historySplitContainer";
            historySplitContainer.Orientation = Orientation.Horizontal;
            //
            // historySplitContainer.Panel1
            //
            historySplitContainer.Panel1.Controls.Add(lstOutput);
            //
            // historySplitContainer.Panel2
            //
            historySplitContainer.Panel2.Controls.Add(hostListPanel);
            historySplitContainer.Panel2Collapsed = true;
            historySplitContainer.Size = new Size(257, 275);
            historySplitContainer.SplitterDistance = 137;
            historySplitContainer.SplitterWidth = 4;
            historySplitContainer.TabIndex = 2;
            //
            // lstOutput
            //
            lstOutput.BackColor = Color.White;
            lstOutput.BorderStyle = BorderStyle.None;
            lstOutput.ContextMenuStrip = contextHistoryLst;
            lstOutput.Dock = DockStyle.Fill;
            lstOutput.Font = new Font("Segoe UI", 9F);
            lstOutput.FormattingEnabled = true;
            lstOutput.IntegralHeight = false;
            lstOutput.ItemHeight = 15;
            lstOutput.Location = new Point(0, 0);
            lstOutput.Name = "lstOutput";
            lstOutput.Size = new Size(257, 275);
            lstOutput.TabIndex = 0;
            lstOutput.SelectedIndexChanged += lstOutput_SelectedIndexChanged;
            // 
            // contextHistoryLst
            // 
            contextHistoryLst.Items.AddRange(new ToolStripItem[] { saveAsToolStripMenuItem, saveAllToolStripMenuItem, toolStripSeparator8, deleteEntryToolStripMenuItem, deleteAllHistoryToolStripMenuItem });
            contextHistoryLst.Name = "contextHistoryLst";
            contextHistoryLst.Size = new Size(201, 98);
            contextHistoryLst.Opening += contextHistoryLst_Opening;
            // 
            // saveAsToolStripMenuItem
            // 
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.Size = new Size(200, 22);
            saveAsToolStripMenuItem.Text = "Save &As...";
            saveAsToolStripMenuItem.Click += saveAsToolStripMenuItem_Click;
            // 
            // saveAllToolStripMenuItem
            // 
            saveAllToolStripMenuItem.Name = "saveAllToolStripMenuItem";
            saveAllToolStripMenuItem.Size = new Size(200, 22);
            saveAllToolStripMenuItem.Text = "Save A&ll History to File...";
            saveAllToolStripMenuItem.Click += saveAllToolStripMenuItem_Click;
            // 
            // toolStripSeparator8
            // 
            toolStripSeparator8.Name = "toolStripSeparator8";
            toolStripSeparator8.Size = new Size(197, 6);
            // 
            // deleteEntryToolStripMenuItem
            // 
            deleteEntryToolStripMenuItem.Name = "deleteEntryToolStripMenuItem";
            deleteEntryToolStripMenuItem.Size = new Size(200, 22);
            deleteEntryToolStripMenuItem.Text = "&Delete Entry";
            deleteEntryToolStripMenuItem.Click += deleteEntryToolStripMenuItem_Click;
            //
            // deleteAllHistoryToolStripMenuItem
            //
            deleteAllHistoryToolStripMenuItem.Name = "deleteAllHistoryToolStripMenuItem";
            deleteAllHistoryToolStripMenuItem.Size = new Size(200, 22);
            deleteAllHistoryToolStripMenuItem.Text = "Delete All &History";
            deleteAllHistoryToolStripMenuItem.Click += deleteAllHistoryToolStripMenuItem_Click;
            //
            // hostListPanel
            //
            hostListPanel.Controls.Add(lstHosts);
            hostListPanel.Controls.Add(hostHeaderPanel);
            hostListPanel.Dock = DockStyle.Fill;
            hostListPanel.Location = new Point(0, 0);
            hostListPanel.Name = "hostListPanel";
            hostListPanel.Size = new Size(257, 134);
            hostListPanel.TabIndex = 0;
            //
            // lstHosts
            //
            lstHosts.BackColor = Color.White;
            lstHosts.BorderStyle = BorderStyle.None;
            lstHosts.ContextMenuStrip = contextHostLst;
            lstHosts.Dock = DockStyle.Fill;
            lstHosts.DrawMode = DrawMode.OwnerDrawFixed;
            lstHosts.Font = new Font("Segoe UI", 9F);
            lstHosts.FormattingEnabled = true;
            lstHosts.IntegralHeight = false;
            lstHosts.ItemHeight = 20;
            lstHosts.Location = new Point(0, 28);
            lstHosts.Name = "lstHosts";
            lstHosts.Size = new Size(257, 106);
            lstHosts.TabIndex = 0;
            lstHosts.DrawItem += lstHosts_DrawItem;
            lstHosts.SelectedIndexChanged += lstHosts_SelectedIndexChanged;
            //
            // contextHostLst
            //
            contextHostLst.Items.AddRange(new ToolStripItem[] { exportHostOutputToolStripMenuItem });
            contextHostLst.Name = "contextHostLst";
            contextHostLst.Size = new Size(181, 48);
            //
            // exportHostOutputToolStripMenuItem
            //
            exportHostOutputToolStripMenuItem.Name = "exportHostOutputToolStripMenuItem";
            exportHostOutputToolStripMenuItem.Size = new Size(180, 22);
            exportHostOutputToolStripMenuItem.Text = "Export to File...";
            exportHostOutputToolStripMenuItem.Click += exportHostOutputToolStripMenuItem_Click;
            //
            // hostHeaderPanel
            //
            hostHeaderPanel.BackColor = Color.FromArgb(248, 249, 250);
            hostHeaderPanel.Controls.Add(lblHostsListTitle);
            hostHeaderPanel.Dock = DockStyle.Top;
            hostHeaderPanel.Location = new Point(0, 0);
            hostHeaderPanel.Name = "hostHeaderPanel";
            hostHeaderPanel.Padding = new Padding(8, 6, 8, 6);
            hostHeaderPanel.Size = new Size(257, 28);
            hostHeaderPanel.TabIndex = 1;
            //
            // lblHostsListTitle
            //
            lblHostsListTitle.AutoSize = true;
            lblHostsListTitle.Dock = DockStyle.Left;
            lblHostsListTitle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            lblHostsListTitle.ForeColor = Color.FromArgb(33, 37, 41);
            lblHostsListTitle.Location = new Point(8, 6);
            lblHostsListTitle.Name = "lblHostsListTitle";
            lblHostsListTitle.Size = new Size(37, 15);
            lblHostsListTitle.TabIndex = 0;
            lblHostsListTitle.Text = "Hosts";
            //
            // historyHeaderPanel
            // 
            historyHeaderPanel.BackColor = Color.FromArgb(248, 249, 250);
            historyHeaderPanel.Controls.Add(lblHistoryTitle);
            historyHeaderPanel.Dock = DockStyle.Top;
            historyHeaderPanel.Location = new Point(0, 0);
            historyHeaderPanel.Name = "historyHeaderPanel";
            historyHeaderPanel.Padding = new Padding(8, 6, 8, 6);
            historyHeaderPanel.Size = new Size(257, 28);
            historyHeaderPanel.TabIndex = 1;
            // 
            // lblHistoryTitle
            // 
            lblHistoryTitle.AutoSize = true;
            lblHistoryTitle.Dock = DockStyle.Left;
            lblHistoryTitle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            lblHistoryTitle.ForeColor = Color.FromArgb(33, 37, 41);
            lblHistoryTitle.Location = new Point(8, 6);
            lblHistoryTitle.Name = "lblHistoryTitle";
            lblHistoryTitle.Size = new Size(45, 15);
            lblHistoryTitle.TabIndex = 0;
            lblHistoryTitle.Text = "History";
            // 
            // txtOutput
            // 
            txtOutput.BackColor = Color.FromArgb(30, 30, 30);
            txtOutput.BorderStyle = BorderStyle.None;
            txtOutput.Dock = DockStyle.Fill;
            txtOutput.Font = new Font("Cascadia Code", 9.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtOutput.ForeColor = Color.FromArgb(212, 212, 212);
            txtOutput.HideSelection = false;
            txtOutput.Location = new Point(0, 0);
            txtOutput.MaxLength = 2000000;
            txtOutput.Multiline = true;
            txtOutput.Name = "txtOutput";
            txtOutput.ReadOnly = true;
            txtOutput.ScrollBars = ScrollBars.Both;
            txtOutput.Size = new Size(1121, 303);
            txtOutput.TabIndex = 0;
            txtOutput.WordWrap = false;
            // 
            // mainToolStrip
            // 
            mainToolStrip.BackColor = Color.FromArgb(248, 249, 250);
            mainToolStrip.GripStyle = ToolStripGripStyle.Hidden;
            mainToolStrip.Items.AddRange(new ToolStripItem[] { tsbOpenCsv, tsbSaveCsv, tsbSaveCsvAs, toolStripSeparator1, tsbClearGrid, toolStripSeparator2, toolStripLabel1, tsbUsername, toolStripLabel2, tsbPassword });
            mainToolStrip.Location = new Point(0, 24);
            mainToolStrip.Name = "mainToolStrip";
            mainToolStrip.Padding = new Padding(8, 0, 8, 0);
            mainToolStrip.Size = new Size(1400, 25);
            mainToolStrip.TabIndex = 0;
            // 
            // tsbOpenCsv
            // 
            tsbOpenCsv.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbOpenCsv.Name = "tsbOpenCsv";
            tsbOpenCsv.Size = new Size(64, 22);
            tsbOpenCsv.Text = "Open CSV";
            tsbOpenCsv.ToolTipText = "Open CSV file (Ctrl+O)";
            tsbOpenCsv.Click += btnOpenCSV_Click;
            // 
            // tsbSaveCsv
            // 
            tsbSaveCsv.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbSaveCsv.Name = "tsbSaveCsv";
            tsbSaveCsv.Size = new Size(35, 22);
            tsbSaveCsv.Text = "Save";
            tsbSaveCsv.ToolTipText = "Save CSV (Ctrl+S)";
            tsbSaveCsv.Click += saveToolStripMenuItem_Click;
            // 
            // tsbSaveCsvAs
            // 
            tsbSaveCsvAs.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbSaveCsvAs.Name = "tsbSaveCsvAs";
            tsbSaveCsvAs.Size = new Size(51, 22);
            tsbSaveCsvAs.Text = "Save As";
            tsbSaveCsvAs.ToolTipText = "Save CSV As (Ctrl+Shift+S)";
            tsbSaveCsvAs.Click += btnSaveAs_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(6, 25);
            // 
            // tsbClearGrid
            // 
            tsbClearGrid.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbClearGrid.Name = "tsbClearGrid";
            tsbClearGrid.Size = new Size(38, 22);
            tsbClearGrid.Text = "Clear";
            tsbClearGrid.ToolTipText = "Clear all hosts";
            tsbClearGrid.Click += btnClear_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(6, 25);
            // 
            // toolStripLabel1
            // 
            toolStripLabel1.Name = "toolStripLabel1";
            toolStripLabel1.Size = new Size(63, 22);
            toolStripLabel1.Text = "Username:";
            // 
            // tsbUsername
            // 
            tsbUsername.BorderStyle = BorderStyle.FixedSingle;
            tsbUsername.Name = "tsbUsername";
            tsbUsername.Size = new Size(120, 25);
            // 
            // toolStripLabel2
            // 
            toolStripLabel2.Name = "toolStripLabel2";
            toolStripLabel2.Size = new Size(60, 22);
            toolStripLabel2.Text = "Password:";
            // 
            // tsbPassword
            // 
            tsbPassword.BorderStyle = BorderStyle.FixedSingle;
            tsbPassword.Name = "tsbPassword";
            tsbPassword.Size = new Size(120, 25);
            // 
            // statusStrip
            // 
            statusStrip.BackColor = Color.FromArgb(248, 249, 250);
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress, statusHostCount });
            statusStrip.Location = new Point(0, 720);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1400, 22);
            statusStrip.TabIndex = 2;
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(1341, 17);
            statusLabel.Spring = true;
            statusLabel.Text = "Ready";
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // statusProgress
            // 
            statusProgress.Name = "statusProgress";
            statusProgress.Size = new Size(100, 18);
            statusProgress.Visible = false;
            // 
            // statusHostCount
            // 
            statusHostCount.Name = "statusHostCount";
            statusHostCount.Size = new Size(44, 17);
            statusHostCount.Text = "0 hosts";
            // 
            // menuStrip1
            // 
            menuStrip1.BackColor = Color.FromArgb(248, 249, 250);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1400, 24);
            menuStrip1.TabIndex = 3;
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openCSVToolStripMenuItem, saveToolStripMenuItem, saveAsToolStripMenuItem1, toolStripSeparator4, exportAllPresetsToolStripMenuItem, importAllPresetsToolStripMenuItem, toolStripSeparator9, settingsToolStripMenuItem, toolStripSeparator10, ExitMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            // 
            // openCSVToolStripMenuItem
            // 
            openCSVToolStripMenuItem.Name = "openCSVToolStripMenuItem";
            openCSVToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openCSVToolStripMenuItem.Size = new Size(195, 22);
            openCSVToolStripMenuItem.Text = "&Open CSV...";
            openCSVToolStripMenuItem.Click += openCSVToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            saveToolStripMenuItem.Size = new Size(195, 22);
            saveToolStripMenuItem.Text = "&Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // saveAsToolStripMenuItem1
            // 
            saveAsToolStripMenuItem1.Name = "saveAsToolStripMenuItem1";
            saveAsToolStripMenuItem1.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveAsToolStripMenuItem1.Size = new Size(195, 22);
            saveAsToolStripMenuItem1.Text = "Save &As...";
            saveAsToolStripMenuItem1.Click += saveAsToolStripMenuItem1_Click;
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new Size(192, 6);
            // 
            // exportAllPresetsToolStripMenuItem
            // 
            exportAllPresetsToolStripMenuItem.Name = "exportAllPresetsToolStripMenuItem";
            exportAllPresetsToolStripMenuItem.Size = new Size(195, 22);
            exportAllPresetsToolStripMenuItem.Text = "E&xport All Presets...";
            exportAllPresetsToolStripMenuItem.Click += exportAllPresetsToolStripMenuItem_Click;
            // 
            // importAllPresetsToolStripMenuItem
            // 
            importAllPresetsToolStripMenuItem.Name = "importAllPresetsToolStripMenuItem";
            importAllPresetsToolStripMenuItem.Size = new Size(195, 22);
            importAllPresetsToolStripMenuItem.Text = "&Import All Presets...";
            importAllPresetsToolStripMenuItem.Click += importAllPresetsToolStripMenuItem_Click;
            // 
            // toolStripSeparator9
            // 
            toolStripSeparator9.Name = "toolStripSeparator9";
            toolStripSeparator9.Size = new Size(192, 6);
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(195, 22);
            settingsToolStripMenuItem.Text = "&Settings...";
            settingsToolStripMenuItem.Click += settingsToolStripMenuItem_Click;
            // 
            // toolStripSeparator10
            // 
            toolStripSeparator10.Name = "toolStripSeparator10";
            toolStripSeparator10.Size = new Size(192, 6);
            // 
            // ExitMenuItem
            // 
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
            ExitMenuItem.Size = new Size(195, 22);
            ExitMenuItem.Text = "E&xit";
            ExitMenuItem.Click += ExitMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { findToolStripMenuItem, toolStripSeparatorEdit1, debugModeToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "&Edit";
            // 
            // findToolStripMenuItem
            // 
            findToolStripMenuItem.Name = "findToolStripMenuItem";
            findToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.F;
            findToolStripMenuItem.Size = new Size(146, 22);
            findToolStripMenuItem.Text = "&Find...";
            findToolStripMenuItem.Click += findToolStripMenuItem_Click;
            // 
            // toolStripSeparatorEdit1
            // 
            toolStripSeparatorEdit1.Name = "toolStripSeparatorEdit1";
            toolStripSeparatorEdit1.Size = new Size(143, 6);
            // 
            // debugModeToolStripMenuItem
            // 
            debugModeToolStripMenuItem.CheckOnClick = true;
            debugModeToolStripMenuItem.Name = "debugModeToolStripMenuItem";
            debugModeToolStripMenuItem.Size = new Size(146, 22);
            debugModeToolStripMenuItem.Text = "&Debug Mode";
            debugModeToolStripMenuItem.ToolTipText = "When enabled, shows timestamps and diagnostic info to help troubleshoot prompt detection";
            debugModeToolStripMenuItem.CheckedChanged += debugModeToolStripMenuItem_CheckedChanged;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { checkForUpdatesToolStripMenuItem, toolStripSeparatorHelp1, aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "&Help";
            // 
            // checkForUpdatesToolStripMenuItem
            // 
            checkForUpdatesToolStripMenuItem.Name = "checkForUpdatesToolStripMenuItem";
            checkForUpdatesToolStripMenuItem.Size = new Size(180, 22);
            checkForUpdatesToolStripMenuItem.Text = "Check for &Updates...";
            checkForUpdatesToolStripMenuItem.Click += checkForUpdatesToolStripMenuItem_Click;
            // 
            // toolStripSeparatorHelp1
            // 
            toolStripSeparatorHelp1.Name = "toolStripSeparatorHelp1";
            toolStripSeparatorHelp1.Size = new Size(177, 6);
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(180, 22);
            aboutToolStripMenuItem.Text = "&About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // contextPresetLstAdd
            // 
            contextPresetLstAdd.Items.AddRange(new ToolStripItem[] { ctxAddPreset2, ctxImportPreset2 });
            contextPresetLstAdd.Name = "contextPresetLstAdd";
            contextPresetLstAdd.Size = new Size(146, 48);
            // 
            // ctxAddPreset2
            // 
            ctxAddPreset2.Name = "ctxAddPreset2";
            ctxAddPreset2.Size = new Size(145, 22);
            ctxAddPreset2.Text = "&Add Preset";
            ctxAddPreset2.Click += contextPresetLstAdd_Click;
            // 
            // ctxImportPreset2
            // 
            ctxImportPreset2.Name = "ctxImportPreset2";
            ctxImportPreset2.Size = new Size(145, 22);
            ctxImportPreset2.Text = "&Import Preset";
            ctxImportPreset2.Click += ImportPreset_Click;
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(-100, -100);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(100, 23);
            txtUsername.TabIndex = 5;
            txtUsername.Visible = false;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(-100, -100);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(100, 23);
            txtPassword.TabIndex = 6;
            txtPassword.Visible = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(233, 236, 239);
            ClientSize = new Size(1400, 742);
            Controls.Add(mainSplitContainer);
            Controls.Add(mainToolStrip);
            Controls.Add(statusStrip);
            Controls.Add(menuStrip1);
            Controls.Add(txtUsername);
            Controls.Add(txtPassword);
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(1024, 600);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "SSH Helper";
            mainSplitContainer.Panel1.ResumeLayout(false);
            mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).EndInit();
            mainSplitContainer.ResumeLayout(false);
            topSplitContainer.Panel1.ResumeLayout(false);
            topSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)topSplitContainer).EndInit();
            topSplitContainer.ResumeLayout(false);
            hostsPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgv_variables).EndInit();
            contextMenuStrip1.ResumeLayout(false);
            hostsHeaderPanel.ResumeLayout(false);
            hostsHeaderPanel.PerformLayout();
            commandPanel.ResumeLayout(false);
            commandSplitContainer.Panel1.ResumeLayout(false);
            commandSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)commandSplitContainer).EndInit();
            commandSplitContainer.ResumeLayout(false);
            presetsPanel.ResumeLayout(false);
            presetsPanel.PerformLayout();
            contextPresetLst.ResumeLayout(false);
            presetsToolStrip.ResumeLayout(false);
            presetsToolStrip.PerformLayout();
            presetsHeaderPanel.ResumeLayout(false);
            presetsHeaderPanel.PerformLayout();
            scriptPanel.ResumeLayout(false);
            scriptPanel.PerformLayout();
            scriptFooterPanel.ResumeLayout(false);
            scriptHeaderPanel.ResumeLayout(false);
            scriptHeaderPanel.PerformLayout();
            executePanel.ResumeLayout(false);
            outputPanel.ResumeLayout(false);
            outputSplitContainer.Panel1.ResumeLayout(false);
            outputSplitContainer.Panel2.ResumeLayout(false);
            outputSplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)outputSplitContainer).EndInit();
            outputSplitContainer.ResumeLayout(false);
            historyPanel.ResumeLayout(false);
            historySplitContainer.Panel1.ResumeLayout(false);
            historySplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)historySplitContainer).EndInit();
            historySplitContainer.ResumeLayout(false);
            contextHistoryLst.ResumeLayout(false);
            hostListPanel.ResumeLayout(false);
            contextHostLst.ResumeLayout(false);
            hostHeaderPanel.ResumeLayout(false);
            hostHeaderPanel.PerformLayout();
            historyHeaderPanel.ResumeLayout(false);
            historyHeaderPanel.PerformLayout();
            mainToolStrip.ResumeLayout(false);
            mainToolStrip.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            contextPresetLstAdd.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        // Main layout
        private SplitContainer mainSplitContainer;
        private SplitContainer topSplitContainer;
        private SplitContainer commandSplitContainer;

        // Toolbar
        private ToolStrip mainToolStrip;
        private ToolStripButton tsbOpenCsv;
        private ToolStripButton tsbSaveCsv;
        private ToolStripButton tsbSaveCsvAs;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton tsbClearGrid;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripLabel toolStripLabel1;
        private ToolStripTextBox tsbUsername;
        private ToolStripLabel toolStripLabel2;
        private ToolStripTextBox tsbPassword;

        // Hosts panel
        private Panel hostsPanel;
        private Panel hostsHeaderPanel;
        private Label lblHostsTitle;
        private Label lblHostCount;
        private DataGridView dgv_variables;

        // Command panel
        private Panel commandPanel;

        // Presets panel
        private Panel presetsPanel;
        private Panel presetsHeaderPanel;
        private Label lblPresetsTitle;
        private TreeView trvPresets;
        private ToolStrip presetsToolStrip;
        private ToolStripButton tsbAddPreset;
        private ToolStripButton tsbDeletePreset;
        private ToolStripButton tsbRenamePreset;
        private ToolStripButton tsbDuplicatePreset;
        private ToolStripSeparator tsbSeparatorFolders;
        private ToolStripButton tsbAddFolder;
        private ToolStripButton tsbDeleteFolder;

        // Script panel
        private Panel scriptPanel;
        private Panel scriptHeaderPanel;
        private Panel scriptFooterPanel;
        private Label lblScriptTitle;
        private Label lblLinePosition;
        private Label lblPresetName;
        private Label lblTimeoutHeader;
        private TextBox txtTimeoutHeader;
        private TextBox txtPreset;
        private Button btnSavePreset;
        private TextBox txtCommand;

        // Execute panel
        private Panel executePanel;
        private Button btnExecuteAll;
        private Button btnExecuteSelected;
        private Button btnStopAll;

        // Output panel
        private Panel outputPanel;
        private SplitContainer outputSplitContainer;
        private TextBox txtOutput;
        private Panel historyPanel;
        private SplitContainer historySplitContainer;
        private Panel historyHeaderPanel;
        private Label lblHistoryTitle;
        private ListBox lstOutput;

        // Host list panel (for folder history)
        private Panel hostListPanel;
        private Panel hostHeaderPanel;
        private Label lblHostsListTitle;
        private ListBox lstHosts;
        private ContextMenuStrip contextHostLst;
        private ToolStripMenuItem exportHostOutputToolStripMenuItem;

        // Status bar
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripProgressBar statusProgress;
        private ToolStripStatusLabel statusHostCount;

        // Menu
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openCSVToolStripMenuItem;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem saveAsToolStripMenuItem1;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem exportAllPresetsToolStripMenuItem;
        private ToolStripMenuItem importAllPresetsToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator9;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator10;
        private ToolStripMenuItem ExitMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem findToolStripMenuItem;
        private ToolStripSeparator toolStripSeparatorEdit1;
        private ToolStripMenuItem debugModeToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem checkForUpdatesToolStripMenuItem;
        private ToolStripSeparator toolStripSeparatorHelp1;
        private ToolStripMenuItem aboutToolStripMenuItem;

        // Context menus
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem addColumnToolStripMenuItem;
        private ToolStripMenuItem renameColumnToolStripMenuItem;
        private ToolStripMenuItem deleteColumnToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem deleteRowToolStripMenuItem;

        private ContextMenuStrip contextPresetLst;
        private ToolStripMenuItem ctxAddPreset;
        private ToolStripMenuItem ctxDuplicatePreset;
        private ToolStripMenuItem ctxRenamePreset;
        private ToolStripMenuItem ctxDeletePreset;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripMenuItem ctxExportPreset;
        private ToolStripMenuItem ctxImportPreset;
        private ToolStripSeparator toolStripSeparator7;
        private ToolStripMenuItem ctxToggleFavorite;
        private ToolStripMenuItem ctxToggleSorting;
        private ToolStripSeparator toolStripSeparatorFolders;
        private ToolStripMenuItem ctxAddFolder;
        private ToolStripMenuItem ctxRenameFolder;
        private ToolStripMenuItem ctxDeleteFolder;
        private ToolStripMenuItem ctxMoveToFolder;

        private ContextMenuStrip contextPresetLstAdd;
        private ToolStripMenuItem ctxAddPreset2;
        private ToolStripMenuItem ctxImportPreset2;

        private ContextMenuStrip contextHistoryLst;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private ToolStripMenuItem saveAllToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator8;
        private ToolStripMenuItem deleteEntryToolStripMenuItem;
        private ToolStripMenuItem deleteAllHistoryToolStripMenuItem;

        // Hidden controls (for compatibility)
        private TextBox txtUsername;
        private TextBox txtPassword;
    }

    /// <summary>
    /// Modern flat-style ToolStrip renderer
    /// </summary>
    public class ModernToolStripRenderer : ToolStripProfessionalRenderer
    {
        public ModernToolStripRenderer() : base(new ModernColorTable()) { }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                using var brush = new SolidBrush(Color.FromArgb(229, 229, 229));
                e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // No border
        }
    }

    public class ModernColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => Color.FromArgb(248, 249, 250);
        public override Color ToolStripGradientMiddle => Color.FromArgb(248, 249, 250);
        public override Color ToolStripGradientEnd => Color.FromArgb(248, 249, 250);
        public override Color MenuStripGradientBegin => Color.FromArgb(248, 249, 250);
        public override Color MenuStripGradientEnd => Color.FromArgb(248, 249, 250);
        public override Color StatusStripGradientBegin => Color.FromArgb(248, 249, 250);
        public override Color StatusStripGradientEnd => Color.FromArgb(248, 249, 250);
    }
}
