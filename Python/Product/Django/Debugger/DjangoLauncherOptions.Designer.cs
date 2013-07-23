namespace Microsoft.PythonTools.Django.Debugger {
    partial class DjangoLauncherOptions {
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
            this._debugGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._settingsModuleLabel = new System.Windows.Forms.Label();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._arguments = new System.Windows.Forms.TextBox();
            this._interpArgs = new System.Windows.Forms.TextBox();
            this._interpreterPath = new System.Windows.Forms.TextBox();
            this._interpreterPathLabel = new System.Windows.Forms.Label();
            this._interpArgsLabel = new System.Windows.Forms.Label();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._searchPathLabel = new System.Windows.Forms.Label();
            this._settingsModule = new System.Windows.Forms.TextBox();
            this._portNumberLabel = new System.Windows.Forms.Label();
            this._portNumber = new System.Windows.Forms.TextBox();
            this._launchUrl = new System.Windows.Forms.TextBox();
            this._launchUrlLabel = new System.Windows.Forms.Label();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._debugGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _debugGroup
            // 
            this._debugGroup.AutoSize = true;
            this._debugGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._debugGroup.Controls.Add(this.tableLayoutPanel2);
            this._debugGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._debugGroup.Location = new System.Drawing.Point(6, 8);
            this._debugGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._debugGroup.Name = "_debugGroup";
            this._debugGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._debugGroup.Size = new System.Drawing.Size(438, 211);
            this._debugGroup.TabIndex = 0;
            this._debugGroup.TabStop = false;
            this._debugGroup.Text = "Debug";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this._settingsModuleLabel, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this._searchPaths, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._arguments, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._interpArgs, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPath, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPathLabel, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpArgsLabel, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._argumentsLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._searchPathLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._settingsModule, 1, 4);
            this.tableLayoutPanel2.Controls.Add(this._portNumberLabel, 0, 6);
            this.tableLayoutPanel2.Controls.Add(this._portNumber, 1, 6);
            this.tableLayoutPanel2.Controls.Add(this._launchUrl, 1, 5);
            this.tableLayoutPanel2.Controls.Add(this._launchUrlLabel, 0, 5);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 21);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 7;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(426, 182);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // _settingsModuleLabel
            // 
            this._settingsModuleLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._settingsModuleLabel.AutoSize = true;
            this._settingsModuleLabel.Location = new System.Drawing.Point(6, 110);
            this._settingsModuleLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._settingsModuleLabel.Name = "_settingsModuleLabel";
            this._settingsModuleLabel.Size = new System.Drawing.Size(86, 13);
            this._settingsModuleLabel.TabIndex = 8;
            this._settingsModuleLabel.Text = "Settings &Module:";
            this._settingsModuleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _searchPaths
            // 
            this._searchPaths.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._searchPaths.Location = new System.Drawing.Point(129, 3);
            this._searchPaths.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._searchPaths.MinimumSize = new System.Drawing.Size(50, 4);
            this._searchPaths.Name = "_searchPaths";
            this._searchPaths.Size = new System.Drawing.Size(291, 20);
            this._searchPaths.TabIndex = 1;
            this._searchPaths.TextChanged += new System.EventHandler(this.SearchPathsTextChanged);
            // 
            // _arguments
            // 
            this._arguments.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._arguments.Location = new System.Drawing.Point(129, 29);
            this._arguments.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._arguments.MinimumSize = new System.Drawing.Size(50, 4);
            this._arguments.Name = "_arguments";
            this._arguments.Size = new System.Drawing.Size(291, 20);
            this._arguments.TabIndex = 3;
            this._arguments.TextChanged += new System.EventHandler(this.ArgumentsTextChanged);
            // 
            // _interpArgs
            // 
            this._interpArgs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._interpArgs.Location = new System.Drawing.Point(129, 55);
            this._interpArgs.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._interpArgs.MinimumSize = new System.Drawing.Size(50, 4);
            this._interpArgs.Name = "_interpArgs";
            this._interpArgs.Size = new System.Drawing.Size(291, 20);
            this._interpArgs.TabIndex = 5;
            this._interpArgs.TextChanged += new System.EventHandler(this.InterpreterArgsTextChanged);
            // 
            // _interpreterPath
            // 
            this._interpreterPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._interpreterPath.Location = new System.Drawing.Point(129, 81);
            this._interpreterPath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._interpreterPath.MinimumSize = new System.Drawing.Size(50, 4);
            this._interpreterPath.Name = "_interpreterPath";
            this._interpreterPath.Size = new System.Drawing.Size(291, 20);
            this._interpreterPath.TabIndex = 7;
            this._interpreterPath.TextChanged += new System.EventHandler(this.InterpreterPathTextChanged);
            // 
            // _interpreterPathLabel
            // 
            this._interpreterPathLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._interpreterPathLabel.AutoSize = true;
            this._interpreterPathLabel.Location = new System.Drawing.Point(6, 84);
            this._interpreterPathLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            this._interpreterPathLabel.Size = new System.Drawing.Size(83, 13);
            this._interpreterPathLabel.TabIndex = 6;
            this._interpreterPathLabel.Text = "&Interpreter Path:";
            this._interpreterPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _interpArgsLabel
            // 
            this._interpArgsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._interpArgsLabel.AutoSize = true;
            this._interpArgsLabel.Location = new System.Drawing.Point(6, 58);
            this._interpArgsLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._interpArgsLabel.Name = "_interpArgsLabel";
            this._interpArgsLabel.Size = new System.Drawing.Size(111, 13);
            this._interpArgsLabel.TabIndex = 4;
            this._interpArgsLabel.Text = "Interpreter Ar&guments:";
            this._interpArgsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _argumentsLabel
            // 
            this._argumentsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._argumentsLabel.AutoSize = true;
            this._argumentsLabel.Location = new System.Drawing.Point(6, 32);
            this._argumentsLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._argumentsLabel.Name = "_argumentsLabel";
            this._argumentsLabel.Size = new System.Drawing.Size(90, 13);
            this._argumentsLabel.TabIndex = 2;
            this._argumentsLabel.Text = "Script &Arguments:";
            this._argumentsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _searchPathLabel
            // 
            this._searchPathLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._searchPathLabel.AutoSize = true;
            this._searchPathLabel.Location = new System.Drawing.Point(6, 6);
            this._searchPathLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._searchPathLabel.Name = "_searchPathLabel";
            this._searchPathLabel.Size = new System.Drawing.Size(74, 13);
            this._searchPathLabel.TabIndex = 0;
            this._searchPathLabel.Text = "Search &Paths:";
            this._searchPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _settingsModule
            // 
            this._settingsModule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._settingsModule.Location = new System.Drawing.Point(129, 107);
            this._settingsModule.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._settingsModule.MinimumSize = new System.Drawing.Size(50, 4);
            this._settingsModule.Name = "_settingsModule";
            this._settingsModule.Size = new System.Drawing.Size(291, 20);
            this._settingsModule.TabIndex = 9;
            this._settingsModule.TextChanged += new System.EventHandler(this.SettingsModuleTextChanged);
            // 
            // _portNumberLabel
            // 
            this._portNumberLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._portNumberLabel.AutoSize = true;
            this._portNumberLabel.Location = new System.Drawing.Point(6, 162);
            this._portNumberLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._portNumberLabel.Name = "_portNumberLabel";
            this._portNumberLabel.Size = new System.Drawing.Size(69, 13);
            this._portNumberLabel.TabIndex = 12;
            this._portNumberLabel.Text = "Port Nu&mber:";
            this._portNumberLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _portNumber
            // 
            this._portNumber.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._portNumber.Location = new System.Drawing.Point(129, 159);
            this._portNumber.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._portNumber.Name = "_portNumber";
            this._portNumber.Size = new System.Drawing.Size(291, 20);
            this._portNumber.TabIndex = 13;
            this._portNumber.TextChanged += new System.EventHandler(this.PortNumberTextChanged);
            // 
            // _launchUrl
            // 
            this._launchUrl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._launchUrl.Location = new System.Drawing.Point(129, 133);
            this._launchUrl.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._launchUrl.Name = "_launchUrl";
            this._launchUrl.Size = new System.Drawing.Size(291, 20);
            this._launchUrl.TabIndex = 11;
            this._launchUrl.TextChanged += new System.EventHandler(this.LaunchUrlTextChanged);
            // 
            // _launchUrlLabel
            // 
            this._launchUrlLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._launchUrlLabel.AutoSize = true;
            this._launchUrlLabel.Location = new System.Drawing.Point(6, 136);
            this._launchUrlLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._launchUrlLabel.Name = "_launchUrlLabel";
            this._launchUrlLabel.Size = new System.Drawing.Size(71, 13);
            this._launchUrlLabel.TabIndex = 10;
            this._launchUrlLabel.Text = "Launch &URL:";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this._debugGroup, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(450, 247);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // DjangoLauncherOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "DjangoLauncherOptions";
            this.Size = new System.Drawing.Size(450, 247);
            this._debugGroup.ResumeLayout(false);
            this._debugGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _debugGroup;
        private System.Windows.Forms.TextBox _interpreterPath;
        private System.Windows.Forms.Label _interpreterPathLabel;
        private System.Windows.Forms.Label _interpArgsLabel;
        private System.Windows.Forms.TextBox _interpArgs;
        private System.Windows.Forms.TextBox _arguments;
        private System.Windows.Forms.Label _argumentsLabel;
        private System.Windows.Forms.TextBox _searchPaths;
        private System.Windows.Forms.Label _searchPathLabel;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox _settingsModule;
        private System.Windows.Forms.Label _settingsModuleLabel;
        private System.Windows.Forms.Label _portNumberLabel;
        private System.Windows.Forms.TextBox _portNumber;
        private System.Windows.Forms.TextBox _launchUrl;
        private System.Windows.Forms.Label _launchUrlLabel;
    }
}
