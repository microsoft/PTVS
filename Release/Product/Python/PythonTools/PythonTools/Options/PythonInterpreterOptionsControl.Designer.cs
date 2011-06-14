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
            this._pathLabel = new System.Windows.Forms.Label();
            this._path = new System.Windows.Forms.TextBox();
            this._browsePath = new System.Windows.Forms.Button();
            this._windowsPathLabel = new System.Windows.Forms.Label();
            this._windowsPath = new System.Windows.Forms.TextBox();
            this._browseWindowsPath = new System.Windows.Forms.Button();
            this._archLabel = new System.Windows.Forms.Label();
            this._arch = new System.Windows.Forms.ComboBox();
            this._versionLabel = new System.Windows.Forms.Label();
            this._version = new System.Windows.Forms.TextBox();
            this._pathEnvVarLabel = new System.Windows.Forms.Label();
            this._pathEnvVar = new System.Windows.Forms.TextBox();
            this._generateCompletionDb = new System.Windows.Forms.Button();
            this._removeInterpreter = new System.Windows.Forms.Button();
            this._toolTips = new System.Windows.Forms.ToolTip(this.components);
            this._addInterpreter = new System.Windows.Forms.Button();
            this._interpreterSettingsGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _defaultInterpreterLabel
            // 
            this._defaultInterpreterLabel.AutoSize = true;
            this._defaultInterpreterLabel.Location = new System.Drawing.Point(3, 19);
            this._defaultInterpreterLabel.Name = "_defaultInterpreterLabel";
            this._defaultInterpreterLabel.Size = new System.Drawing.Size(95, 13);
            this._defaultInterpreterLabel.TabIndex = 0;
            this._defaultInterpreterLabel.Text = "D&efault Interpreter:";
            // 
            // _defaultInterpreter
            // 
            this._defaultInterpreter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._defaultInterpreter.FormattingEnabled = true;
            this._defaultInterpreter.Location = new System.Drawing.Point(105, 16);
            this._defaultInterpreter.Name = "_defaultInterpreter";
            this._defaultInterpreter.Size = new System.Drawing.Size(276, 21);
            this._defaultInterpreter.TabIndex = 1;
            // 
            // _showSettingsForLabel
            // 
            this._showSettingsForLabel.AutoSize = true;
            this._showSettingsForLabel.Location = new System.Drawing.Point(3, 46);
            this._showSettingsForLabel.Name = "_showSettingsForLabel";
            this._showSettingsForLabel.Size = new System.Drawing.Size(91, 13);
            this._showSettingsForLabel.TabIndex = 2;
            this._showSettingsForLabel.Text = "Show se&ttings for:";
            // 
            // _showSettingsFor
            // 
            this._showSettingsFor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._showSettingsFor.FormattingEnabled = true;
            this._showSettingsFor.Location = new System.Drawing.Point(105, 43);
            this._showSettingsFor.Name = "_showSettingsFor";
            this._showSettingsFor.Size = new System.Drawing.Size(180, 21);
            this._showSettingsFor.TabIndex = 3;
            this._showSettingsFor.SelectedIndexChanged += new System.EventHandler(this.ShowSettingsForSelectedIndexChanged);
            // 
            // _interpreterSettingsGroup
            // 
            this._interpreterSettingsGroup.Controls.Add(this._pathLabel);
            this._interpreterSettingsGroup.Controls.Add(this._path);
            this._interpreterSettingsGroup.Controls.Add(this._browsePath);
            this._interpreterSettingsGroup.Controls.Add(this._windowsPathLabel);
            this._interpreterSettingsGroup.Controls.Add(this._windowsPath);
            this._interpreterSettingsGroup.Controls.Add(this._browseWindowsPath);
            this._interpreterSettingsGroup.Controls.Add(this._archLabel);
            this._interpreterSettingsGroup.Controls.Add(this._arch);
            this._interpreterSettingsGroup.Controls.Add(this._versionLabel);
            this._interpreterSettingsGroup.Controls.Add(this._version);
            this._interpreterSettingsGroup.Controls.Add(this._pathEnvVarLabel);
            this._interpreterSettingsGroup.Controls.Add(this._pathEnvVar);
            this._interpreterSettingsGroup.Controls.Add(this._generateCompletionDb);
            this._interpreterSettingsGroup.Controls.Add(this._removeInterpreter);
            this._interpreterSettingsGroup.Location = new System.Drawing.Point(3, 72);
            this._interpreterSettingsGroup.Name = "_interpreterSettingsGroup";
            this._interpreterSettingsGroup.Size = new System.Drawing.Size(387, 206);
            this._interpreterSettingsGroup.TabIndex = 5;
            this._interpreterSettingsGroup.TabStop = false;
            this._interpreterSettingsGroup.Text = "Interpreter Settings";
            // 
            // _pathLabel
            // 
            this._pathLabel.AutoSize = true;
            this._pathLabel.Location = new System.Drawing.Point(12, 22);
            this._pathLabel.Name = "_pathLabel";
            this._pathLabel.Size = new System.Drawing.Size(32, 13);
            this._pathLabel.TabIndex = 0;
            this._pathLabel.Text = "&Path:";
            // 
            // _path
            // 
            this._path.Location = new System.Drawing.Point(159, 19);
            this._path.Name = "_path";
            this._path.Size = new System.Drawing.Size(180, 20);
            this._path.TabIndex = 1;
            this._path.TextChanged += new System.EventHandler(this.PathTextChanged);
            // 
            // _browsePath
            // 
            this._browsePath.Location = new System.Drawing.Point(345, 17);
            this._browsePath.Name = "_browsePath";
            this._browsePath.Size = new System.Drawing.Size(33, 23);
            this._browsePath.TabIndex = 2;
            this._browsePath.Text = "...";
            this._browsePath.UseVisualStyleBackColor = true;
            this._browsePath.Click += new System.EventHandler(this.BrowsePathClick);
            // 
            // _windowsPathLabel
            // 
            this._windowsPathLabel.AutoSize = true;
            this._windowsPathLabel.Location = new System.Drawing.Point(12, 48);
            this._windowsPathLabel.Name = "_windowsPathLabel";
            this._windowsPathLabel.Size = new System.Drawing.Size(79, 13);
            this._windowsPathLabel.TabIndex = 3;
            this._windowsPathLabel.Text = "&Windows Path:";
            // 
            // _windowsPath
            // 
            this._windowsPath.Location = new System.Drawing.Point(159, 45);
            this._windowsPath.Name = "_windowsPath";
            this._windowsPath.Size = new System.Drawing.Size(180, 20);
            this._windowsPath.TabIndex = 4;
            this._windowsPath.TextChanged += new System.EventHandler(this.WindowsPathTextChanged);
            // 
            // _browseWindowsPath
            // 
            this._browseWindowsPath.Location = new System.Drawing.Point(345, 43);
            this._browseWindowsPath.Name = "_browseWindowsPath";
            this._browseWindowsPath.Size = new System.Drawing.Size(33, 23);
            this._browseWindowsPath.TabIndex = 5;
            this._browseWindowsPath.Text = "...";
            this._browseWindowsPath.UseVisualStyleBackColor = true;
            this._browseWindowsPath.Click += new System.EventHandler(this.BrowseWindowsPathClick);
            // 
            // _archLabel
            // 
            this._archLabel.AutoSize = true;
            this._archLabel.Location = new System.Drawing.Point(12, 75);
            this._archLabel.Name = "_archLabel";
            this._archLabel.Size = new System.Drawing.Size(67, 13);
            this._archLabel.TabIndex = 6;
            this._archLabel.Text = "&Architecture:";
            // 
            // _arch
            // 
            this._arch.FormattingEnabled = true;
            this._arch.Items.AddRange(new object[] {
            "x86",
            "x64",
            "Unknown"});
            this._arch.Location = new System.Drawing.Point(159, 72);
            this._arch.Name = "_arch";
            this._arch.Size = new System.Drawing.Size(219, 21);
            this._arch.TabIndex = 7;
            this._arch.SelectedIndexChanged += new System.EventHandler(this.ArchSelectedIndexChanged);
            // 
            // _versionLabel
            // 
            this._versionLabel.AutoSize = true;
            this._versionLabel.Location = new System.Drawing.Point(12, 102);
            this._versionLabel.Name = "_versionLabel";
            this._versionLabel.Size = new System.Drawing.Size(96, 13);
            this._versionLabel.TabIndex = 8;
            this._versionLabel.Text = "Lan&guage Version:";
            // 
            // _version
            // 
            this._version.Location = new System.Drawing.Point(159, 99);
            this._version.Name = "_version";
            this._version.Size = new System.Drawing.Size(219, 20);
            this._version.TabIndex = 9;
            this._version.TextChanged += new System.EventHandler(this.VersionTextChanged);
            // 
            // _pathEnvVarLabel
            // 
            this._pathEnvVarLabel.AutoSize = true;
            this._pathEnvVarLabel.Location = new System.Drawing.Point(12, 128);
            this._pathEnvVarLabel.Name = "_pathEnvVarLabel";
            this._pathEnvVarLabel.Size = new System.Drawing.Size(135, 13);
            this._pathEnvVarLabel.TabIndex = 10;
            this._pathEnvVarLabel.Text = "Path Environment &Variable:";
            // 
            // _pathEnvVar
            // 
            this._pathEnvVar.Location = new System.Drawing.Point(159, 125);
            this._pathEnvVar.Name = "_pathEnvVar";
            this._pathEnvVar.Size = new System.Drawing.Size(219, 20);
            this._pathEnvVar.TabIndex = 11;
            this._pathEnvVar.TextChanged += new System.EventHandler(this.PathEnvVarTextChanged);
            // 
            // _generateCompletionDb
            // 
            this._generateCompletionDb.Location = new System.Drawing.Point(15, 177);
            this._generateCompletionDb.Name = "_generateCompletionDb";
            this._generateCompletionDb.Size = new System.Drawing.Size(174, 23);
            this._generateCompletionDb.TabIndex = 14;
            this._generateCompletionDb.Text = "&Generate Completion Database";
            this._generateCompletionDb.UseVisualStyleBackColor = true;
            this._generateCompletionDb.Click += new System.EventHandler(this.GenerateCompletionDbClick);
            // 
            // _removeInterpreter
            // 
            this._removeInterpreter.Location = new System.Drawing.Point(198, 177);
            this._removeInterpreter.Name = "_removeInterpreter";
            this._removeInterpreter.Size = new System.Drawing.Size(180, 23);
            this._removeInterpreter.TabIndex = 15;
            this._removeInterpreter.Text = "&Remove Interpreter";
            this._removeInterpreter.UseVisualStyleBackColor = true;
            this._removeInterpreter.Click += new System.EventHandler(this.RemoveInterpreterClick);
            // 
            // _addInterpreter
            // 
            this._addInterpreter.Location = new System.Drawing.Point(291, 43);
            this._addInterpreter.Name = "_addInterpreter";
            this._addInterpreter.Size = new System.Drawing.Size(92, 21);
            this._addInterpreter.TabIndex = 4;
            this._addInterpreter.Text = "A&dd Interpreter";
            this._addInterpreter.UseVisualStyleBackColor = true;
            this._addInterpreter.Click += new System.EventHandler(this.AddInterpreterClick);
            // 
            // PythonInterpreterOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._defaultInterpreterLabel);
            this.Controls.Add(this._defaultInterpreter);
            this.Controls.Add(this._showSettingsForLabel);
            this.Controls.Add(this._showSettingsFor);
            this.Controls.Add(this._addInterpreter);
            this.Controls.Add(this._interpreterSettingsGroup);
            this.Name = "PythonInterpreterOptionsControl";
            this.Size = new System.Drawing.Size(395, 317);
            this._interpreterSettingsGroup.ResumeLayout(false);
            this._interpreterSettingsGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

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
        private System.Windows.Forms.TextBox _version;
        private System.Windows.Forms.Label _pathLabel;
        private System.Windows.Forms.Label _windowsPathLabel;
        private System.Windows.Forms.Label _archLabel;
        private System.Windows.Forms.Button _generateCompletionDb;
        private System.Windows.Forms.ComboBox _arch;
        private System.Windows.Forms.TextBox _path;
        private System.Windows.Forms.TextBox _windowsPath;
        private System.Windows.Forms.Button _browsePath;
        private System.Windows.Forms.Button _browseWindowsPath;
        private System.Windows.Forms.Button _removeInterpreter;
        private System.Windows.Forms.Label _pathEnvVarLabel;
        private System.Windows.Forms.TextBox _pathEnvVar;

    }
}
