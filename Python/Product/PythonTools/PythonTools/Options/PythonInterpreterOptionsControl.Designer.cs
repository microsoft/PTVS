namespace Microsoft.PythonTools.Options {
    partial class PythonInterpreterOptionsControl {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this._defaultInterpreterLabel = new System.Windows.Forms.Label();
            this._defaultInterpreter = new System.Windows.Forms.ComboBox();
            this._showSettingsForLabel = new System.Windows.Forms.Label();
            this._showSettingsFor = new System.Windows.Forms.ComboBox();
            this._interpreterSettingsGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this._pathLabel = new System.Windows.Forms.Label();
            this._path = new System.Windows.Forms.TextBox();
            this._browsePath = new System.Windows.Forms.Button();
            this._windowsPathLabel = new System.Windows.Forms.Label();
            this._windowsPath = new System.Windows.Forms.TextBox();
            this._browseWindowsPath = new System.Windows.Forms.Button();
            this._libraryPathLabel = new System.Windows.Forms.Label();
            this._libraryPath = new System.Windows.Forms.TextBox();
            this._browseLibraryPath = new System.Windows.Forms.Button();
            this._archLabel = new System.Windows.Forms.Label();
            this._arch = new System.Windows.Forms.ComboBox();
            this._versionLabel = new System.Windows.Forms.Label();
            this._version = new System.Windows.Forms.ComboBox();
            this._pathEnvVarLabel = new System.Windows.Forms.Label();
            this._pathEnvVar = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this._removeInterpreter = new System.Windows.Forms.Button();
            this._toolTips = new System.Windows.Forms.ToolTip(this.components);
            this._addInterpreter = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._interpreterSettingsGroup.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // _defaultInterpreterLabel
            // 
            this._defaultInterpreterLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._defaultInterpreterLabel.AutoEllipsis = true;
            this._defaultInterpreterLabel.AutoSize = true;
            this._defaultInterpreterLabel.Location = new System.Drawing.Point(6, 7);
            this._defaultInterpreterLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._defaultInterpreterLabel.Name = "_defaultInterpreterLabel";
            this._defaultInterpreterLabel.Size = new System.Drawing.Size(106, 13);
            this._defaultInterpreterLabel.TabIndex = 0;
            this._defaultInterpreterLabel.Text = "D&efault Environment:";
            this._defaultInterpreterLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _defaultInterpreter
            // 
            this._defaultInterpreter.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.SetColumnSpan(this._defaultInterpreter, 2);
            this._defaultInterpreter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._defaultInterpreter.FormattingEnabled = true;
            this._defaultInterpreter.Location = new System.Drawing.Point(124, 3);
            this._defaultInterpreter.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._defaultInterpreter.Name = "_defaultInterpreter";
            this._defaultInterpreter.Size = new System.Drawing.Size(279, 21);
            this._defaultInterpreter.TabIndex = 1;
            this._defaultInterpreter.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.Interpreter_Format);
            // 
            // _showSettingsForLabel
            // 
            this._showSettingsForLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._showSettingsForLabel.AutoEllipsis = true;
            this._showSettingsForLabel.AutoSize = true;
            this._showSettingsForLabel.Location = new System.Drawing.Point(6, 35);
            this._showSettingsForLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._showSettingsForLabel.Name = "_showSettingsForLabel";
            this._showSettingsForLabel.Size = new System.Drawing.Size(91, 13);
            this._showSettingsForLabel.TabIndex = 2;
            this._showSettingsForLabel.Text = "Show se&ttings for:";
            this._showSettingsForLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _showSettingsFor
            // 
            this._showSettingsFor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._showSettingsFor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._showSettingsFor.FormattingEnabled = true;
            this._showSettingsFor.Location = new System.Drawing.Point(124, 31);
            this._showSettingsFor.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._showSettingsFor.Name = "_showSettingsFor";
            this._showSettingsFor.Size = new System.Drawing.Size(163, 21);
            this._showSettingsFor.TabIndex = 3;
            this._showSettingsFor.SelectedIndexChanged += new System.EventHandler(this.ShowSettingsForSelectedIndexChanged);
            this._showSettingsFor.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.Interpreter_Format);
            // 
            // _interpreterSettingsGroup
            // 
            this._interpreterSettingsGroup.AutoSize = true;
            this._interpreterSettingsGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._interpreterSettingsGroup.Controls.Add(this.tableLayoutPanel3);
            this._interpreterSettingsGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._interpreterSettingsGroup.Location = new System.Drawing.Point(6, 70);
            this._interpreterSettingsGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._interpreterSettingsGroup.Name = "_interpreterSettingsGroup";
            this._interpreterSettingsGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._interpreterSettingsGroup.Size = new System.Drawing.Size(403, 231);
            this._interpreterSettingsGroup.TabIndex = 1;
            this._interpreterSettingsGroup.TabStop = false;
            this._interpreterSettingsGroup.Text = "Environment Settings";
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.AutoSize = true;
            this.tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel3.ColumnCount = 3;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel3.Controls.Add(this._pathLabel, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._path, 1, 0);
            this.tableLayoutPanel3.Controls.Add(this._browsePath, 2, 0);
            this.tableLayoutPanel3.Controls.Add(this._windowsPathLabel, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._windowsPath, 1, 1);
            this.tableLayoutPanel3.Controls.Add(this._browseWindowsPath, 2, 1);
            this.tableLayoutPanel3.Controls.Add(this._libraryPathLabel, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this._libraryPath, 1, 2);
            this.tableLayoutPanel3.Controls.Add(this._browseLibraryPath, 2, 2);
            this.tableLayoutPanel3.Controls.Add(this._archLabel, 0, 3);
            this.tableLayoutPanel3.Controls.Add(this._arch, 1, 3);
            this.tableLayoutPanel3.Controls.Add(this._versionLabel, 0, 4);
            this.tableLayoutPanel3.Controls.Add(this._version, 1, 4);
            this.tableLayoutPanel3.Controls.Add(this._pathEnvVarLabel, 0, 5);
            this.tableLayoutPanel3.Controls.Add(this._pathEnvVar, 1, 5);
            this.tableLayoutPanel3.Controls.Add(this.tableLayoutPanel4, 0, 7);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(6, 21);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 8;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.Size = new System.Drawing.Size(391, 202);
            this.tableLayoutPanel3.TabIndex = 0;
            // 
            // _pathLabel
            // 
            this._pathLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._pathLabel.AutoEllipsis = true;
            this._pathLabel.AutoSize = true;
            this._pathLabel.Location = new System.Drawing.Point(6, 8);
            this._pathLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._pathLabel.Name = "_pathLabel";
            this._pathLabel.Size = new System.Drawing.Size(32, 13);
            this._pathLabel.TabIndex = 0;
            this._pathLabel.Text = "&Path:";
            this._pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _path
            // 
            this._path.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._path.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this._path.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this._path.Location = new System.Drawing.Point(153, 4);
            this._path.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._path.Name = "_path";
            this._path.Size = new System.Drawing.Size(194, 20);
            this._path.TabIndex = 1;
            this._path.TextChanged += new System.EventHandler(this.PathTextChanged);
            // 
            // _browsePath
            // 
            this._browsePath.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._browsePath.AutoSize = true;
            this._browsePath.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._browsePath.Location = new System.Drawing.Point(359, 3);
            this._browsePath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._browsePath.Name = "_browsePath";
            this._browsePath.Size = new System.Drawing.Size(26, 23);
            this._browsePath.TabIndex = 2;
            this._browsePath.Text = "...";
            this._browsePath.UseVisualStyleBackColor = true;
            this._browsePath.Click += new System.EventHandler(this.BrowsePathClick);
            // 
            // _windowsPathLabel
            // 
            this._windowsPathLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._windowsPathLabel.AutoEllipsis = true;
            this._windowsPathLabel.AutoSize = true;
            this._windowsPathLabel.Location = new System.Drawing.Point(6, 37);
            this._windowsPathLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._windowsPathLabel.Name = "_windowsPathLabel";
            this._windowsPathLabel.Size = new System.Drawing.Size(79, 13);
            this._windowsPathLabel.TabIndex = 3;
            this._windowsPathLabel.Text = "&Windows Path:";
            this._windowsPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _windowsPath
            // 
            this._windowsPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._windowsPath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this._windowsPath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this._windowsPath.Location = new System.Drawing.Point(153, 33);
            this._windowsPath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._windowsPath.Name = "_windowsPath";
            this._windowsPath.Size = new System.Drawing.Size(194, 20);
            this._windowsPath.TabIndex = 4;
            this._windowsPath.TextChanged += new System.EventHandler(this.WindowsPathTextChanged);
            // 
            // _browseWindowsPath
            // 
            this._browseWindowsPath.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._browseWindowsPath.AutoSize = true;
            this._browseWindowsPath.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._browseWindowsPath.Location = new System.Drawing.Point(359, 32);
            this._browseWindowsPath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._browseWindowsPath.Name = "_browseWindowsPath";
            this._browseWindowsPath.Size = new System.Drawing.Size(26, 23);
            this._browseWindowsPath.TabIndex = 5;
            this._browseWindowsPath.Text = "...";
            this._browseWindowsPath.UseVisualStyleBackColor = true;
            this._browseWindowsPath.Click += new System.EventHandler(this.BrowseWindowsPathClick);
            // 
            // _libraryPathLabel
            // 
            this._libraryPathLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._libraryPathLabel.AutoEllipsis = true;
            this._libraryPathLabel.AutoSize = true;
            this._libraryPathLabel.Location = new System.Drawing.Point(6, 66);
            this._libraryPathLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._libraryPathLabel.Name = "_libraryPathLabel";
            this._libraryPathLabel.Size = new System.Drawing.Size(66, 13);
            this._libraryPathLabel.TabIndex = 6;
            this._libraryPathLabel.Text = "&Library Path:";
            this._libraryPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _libraryPath
            // 
            this._libraryPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._libraryPath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this._libraryPath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this._libraryPath.Location = new System.Drawing.Point(153, 62);
            this._libraryPath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._libraryPath.Name = "_libraryPath";
            this._libraryPath.Size = new System.Drawing.Size(194, 20);
            this._libraryPath.TabIndex = 7;
            this._libraryPath.TextChanged += new System.EventHandler(this.LibraryPathTextChanged);
            // 
            // _browseLibraryPath
            // 
            this._browseLibraryPath.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._browseLibraryPath.AutoSize = true;
            this._browseLibraryPath.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._browseLibraryPath.Location = new System.Drawing.Point(359, 61);
            this._browseLibraryPath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._browseLibraryPath.Name = "_browseLibraryPath";
            this._browseLibraryPath.Size = new System.Drawing.Size(26, 23);
            this._browseLibraryPath.TabIndex = 8;
            this._browseLibraryPath.Text = "...";
            this._browseLibraryPath.UseVisualStyleBackColor = true;
            this._browseLibraryPath.Click += new System.EventHandler(this.BrowseLibraryPathClick);
            // 
            // _archLabel
            // 
            this._archLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._archLabel.AutoEllipsis = true;
            this._archLabel.AutoSize = true;
            this._archLabel.Location = new System.Drawing.Point(6, 94);
            this._archLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._archLabel.Name = "_archLabel";
            this._archLabel.Size = new System.Drawing.Size(67, 13);
            this._archLabel.TabIndex = 9;
            this._archLabel.Text = "&Architecture:";
            this._archLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _arch
            // 
            this._arch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel3.SetColumnSpan(this._arch, 2);
            this._arch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._arch.FormattingEnabled = true;
            this._arch.Items.AddRange(new object[] {
            "x86",
            "x64",
            "Unknown"});
            this._arch.Location = new System.Drawing.Point(153, 90);
            this._arch.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._arch.Name = "_arch";
            this._arch.Size = new System.Drawing.Size(232, 21);
            this._arch.TabIndex = 10;
            this._arch.SelectedIndexChanged += new System.EventHandler(this.ArchSelectedIndexChanged);
            // 
            // _versionLabel
            // 
            this._versionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._versionLabel.AutoEllipsis = true;
            this._versionLabel.AutoSize = true;
            this._versionLabel.Location = new System.Drawing.Point(6, 121);
            this._versionLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._versionLabel.Name = "_versionLabel";
            this._versionLabel.Size = new System.Drawing.Size(96, 13);
            this._versionLabel.TabIndex = 11;
            this._versionLabel.Text = "Lan&guage Version:";
            this._versionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _version
            // 
            this._version.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel3.SetColumnSpan(this._version, 2);
            this._version.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._version.FormattingEnabled = true;
            this._version.Items.AddRange(new object[] {
            "2.5",
            "2.6",
            "2.7",
            "3.0",
            "3.1",
            "3.2",
            "3.3",
            "3.4",
            "3.5"});
            this._version.Location = new System.Drawing.Point(153, 117);
            this._version.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._version.Name = "_version";
            this._version.Size = new System.Drawing.Size(232, 21);
            this._version.TabIndex = 12;
            this._version.SelectedIndexChanged += new System.EventHandler(this.Version_SelectedIndexChanged);
            // 
            // _pathEnvVarLabel
            // 
            this._pathEnvVarLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._pathEnvVarLabel.AutoEllipsis = true;
            this._pathEnvVarLabel.AutoSize = true;
            this._pathEnvVarLabel.Location = new System.Drawing.Point(6, 147);
            this._pathEnvVarLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._pathEnvVarLabel.Name = "_pathEnvVarLabel";
            this._pathEnvVarLabel.Size = new System.Drawing.Size(135, 13);
            this._pathEnvVarLabel.TabIndex = 13;
            this._pathEnvVarLabel.Text = "Path Environment &Variable:";
            this._pathEnvVarLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _pathEnvVar
            // 
            this._pathEnvVar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel3.SetColumnSpan(this._pathEnvVar, 2);
            this._pathEnvVar.Location = new System.Drawing.Point(153, 144);
            this._pathEnvVar.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._pathEnvVar.Name = "_pathEnvVar";
            this._pathEnvVar.Size = new System.Drawing.Size(232, 20);
            this._pathEnvVar.TabIndex = 14;
            this._pathEnvVar.TextChanged += new System.EventHandler(this.PathEnvVarTextChanged);
            // 
            // tableLayoutPanel4
            // 
            this.tableLayoutPanel4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel4.AutoSize = true;
            this.tableLayoutPanel4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel4.ColumnCount = 3;
            this.tableLayoutPanel3.SetColumnSpan(this.tableLayoutPanel4, 3);
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel4.Controls.Add(this._removeInterpreter, 1, 0);
            this.tableLayoutPanel4.Location = new System.Drawing.Point(0, 167);
            this.tableLayoutPanel4.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 1;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel4.Size = new System.Drawing.Size(391, 35);
            this.tableLayoutPanel4.TabIndex = 15;
            // 
            // _removeInterpreter
            // 
            this._removeInterpreter.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._removeInterpreter.AutoSize = true;
            this._removeInterpreter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._removeInterpreter.Location = new System.Drawing.Point(124, 3);
            this._removeInterpreter.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._removeInterpreter.Name = "_removeInterpreter";
            this._removeInterpreter.Padding = new System.Windows.Forms.Padding(12, 3, 12, 3);
            this._removeInterpreter.Size = new System.Drawing.Size(143, 29);
            this._removeInterpreter.TabIndex = 0;
            this._removeInterpreter.Text = "&Remove Environment";
            this._removeInterpreter.UseVisualStyleBackColor = true;
            this._removeInterpreter.Click += new System.EventHandler(this.RemoveInterpreterClick);
            // 
            // _addInterpreter
            // 
            this._addInterpreter.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._addInterpreter.AutoSize = true;
            this._addInterpreter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._addInterpreter.Location = new System.Drawing.Point(299, 30);
            this._addInterpreter.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._addInterpreter.Name = "_addInterpreter";
            this._addInterpreter.Padding = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this._addInterpreter.Size = new System.Drawing.Size(104, 23);
            this._addInterpreter.TabIndex = 4;
            this._addInterpreter.Text = "A&dd Environment";
            this._addInterpreter.UseVisualStyleBackColor = true;
            this._addInterpreter.Click += new System.EventHandler(this.AddInterpreterClick);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._interpreterSettingsGroup, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(415, 323);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 3;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this._defaultInterpreterLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._defaultInterpreter, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._showSettingsForLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._showSettingsFor, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._addInterpreter, 2, 1);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 2;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(409, 56);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // PythonInterpreterOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonInterpreterOptionsControl";
            this.Size = new System.Drawing.Size(415, 323);
            this._interpreterSettingsGroup.ResumeLayout(false);
            this._interpreterSettingsGroup.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _defaultInterpreterLabel;
        private System.Windows.Forms.ComboBox _defaultInterpreter;
        private System.Windows.Forms.Label _showSettingsForLabel;
        private System.Windows.Forms.ComboBox _showSettingsFor;
        private System.Windows.Forms.GroupBox _interpreterSettingsGroup;
        private System.Windows.Forms.ToolTip _toolTips;
        private System.Windows.Forms.Button _addInterpreter;
        private System.Windows.Forms.Label _versionLabel;
        private System.Windows.Forms.Label _pathLabel;
        private System.Windows.Forms.Label _windowsPathLabel;
        private System.Windows.Forms.Label _archLabel;
        private System.Windows.Forms.ComboBox _arch;
        private System.Windows.Forms.TextBox _path;
        private System.Windows.Forms.TextBox _windowsPath;
        private System.Windows.Forms.Button _browsePath;
        private System.Windows.Forms.Button _browseWindowsPath;
        private System.Windows.Forms.Button _removeInterpreter;
        private System.Windows.Forms.Label _pathEnvVarLabel;
        private System.Windows.Forms.TextBox _pathEnvVar;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.ComboBox _version;
        private System.Windows.Forms.Label _libraryPathLabel;
        private System.Windows.Forms.TextBox _libraryPath;
        private System.Windows.Forms.Button _browseLibraryPath;

    }
}
