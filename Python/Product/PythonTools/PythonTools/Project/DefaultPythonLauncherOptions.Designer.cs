namespace Microsoft.PythonTools.Project {
    partial class DefaultPythonLauncherOptions {
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
            this._runGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._envVars = new System.Windows.Forms.TextBox();
            this._envVarsLabel = new System.Windows.Forms.Label();
            this._searchPathLabel = new System.Windows.Forms.Label();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._arguments = new System.Windows.Forms.TextBox();
            this._interpArgsLabel = new System.Windows.Forms.Label();
            this._interpArgs = new System.Windows.Forms.TextBox();
            this._interpreterPath = new System.Windows.Forms.TextBox();
            this._interpreterPathLabel = new System.Windows.Forms.Label();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._debugGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this._mixedMode = new System.Windows.Forms.CheckBox();
            this._runGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this._debugGroup.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // _runGroup
            // 
            this._runGroup.AutoSize = true;
            this._runGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._runGroup.Controls.Add(this.tableLayoutPanel2);
            this._runGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._runGroup.Location = new System.Drawing.Point(6, 8);
            this._runGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._runGroup.Name = "_runGroup";
            this._runGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._runGroup.Size = new System.Drawing.Size(442, 189);
            this._runGroup.TabIndex = 17;
            this._runGroup.TabStop = false;
            this._runGroup.Text = "Run";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this._envVars, 1, 4);
            this.tableLayoutPanel2.Controls.Add(this._envVarsLabel, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this._searchPathLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._searchPaths, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._argumentsLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._arguments, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._interpArgsLabel, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpArgs, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPath, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPathLabel, 0, 3);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 21);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 5;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(430, 160);
            this.tableLayoutPanel2.TabIndex = 24;
            // 
            // _envVars
            // 
            this._envVars.AcceptsReturn = true;
            this._envVars.Dock = System.Windows.Forms.DockStyle.Fill;
            this._envVars.Location = new System.Drawing.Point(133, 107);
            this._envVars.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._envVars.MinimumSize = new System.Drawing.Size(40, 4);
            this._envVars.Multiline = true;
            this._envVars.Name = "_envVars";
            this._envVars.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._envVars.Size = new System.Drawing.Size(291, 50);
            this._envVars.TabIndex = 9;
            this._toolTip.SetToolTip(this._envVars, "Specifies environment variables to be set in the spawned process in the form:\r\n\r\n" +
        "NAME1=value1\r\nNAME2=value2\r\n...");
            this._envVars.WordWrap = false;
            this._envVars.TextChanged += new System.EventHandler(this._envVars_TextChanged);
            // 
            // _envVarsLabel
            // 
            this._envVarsLabel.AutoSize = true;
            this._envVarsLabel.Location = new System.Drawing.Point(6, 110);
            this._envVarsLabel.Margin = new System.Windows.Forms.Padding(6, 6, 6, 0);
            this._envVarsLabel.Name = "_envVarsLabel";
            this._envVarsLabel.Size = new System.Drawing.Size(115, 13);
            this._envVarsLabel.TabIndex = 8;
            this._envVarsLabel.Text = "Environment &Variables:";
            this._envVarsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._toolTip.SetToolTip(this._envVarsLabel, "Specifies environment variables to be set in the spawned process in the form:\r\n\r\n" +
        "NAME1=value1\r\nNAME2=value2\r\n...");
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
            this._toolTip.SetToolTip(this._searchPathLabel, "Specifies additional directories which are added to sys.path for making libraries" +
        " available for importing.");
            // 
            // _searchPaths
            // 
            this._searchPaths.Dock = System.Windows.Forms.DockStyle.Fill;
            this._searchPaths.Location = new System.Drawing.Point(133, 3);
            this._searchPaths.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._searchPaths.MinimumSize = new System.Drawing.Size(40, 4);
            this._searchPaths.Name = "_searchPaths";
            this._searchPaths.Size = new System.Drawing.Size(291, 20);
            this._searchPaths.TabIndex = 1;
            this._toolTip.SetToolTip(this._searchPaths, "Specifies additional directories which are added to sys.path for making libraries" +
        " available for importing.");
            this._searchPaths.TextChanged += new System.EventHandler(this.SearchPathsTextChanged);
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
            this._toolTip.SetToolTip(this._argumentsLabel, "Specifies arguments which are passed to the script and available via sys.argv.");
            // 
            // _arguments
            // 
            this._arguments.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arguments.Location = new System.Drawing.Point(133, 29);
            this._arguments.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._arguments.MinimumSize = new System.Drawing.Size(40, 4);
            this._arguments.Name = "_arguments";
            this._arguments.Size = new System.Drawing.Size(291, 20);
            this._arguments.TabIndex = 3;
            this._toolTip.SetToolTip(this._arguments, "Specifies arguments which are passed to the script and available via sys.argv.");
            this._arguments.TextChanged += new System.EventHandler(this.ArgumentsTextChanged);
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
            this._interpArgsLabel.Text = "Interpreter A&rguments:";
            this._interpArgsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._toolTip.SetToolTip(this._interpArgsLabel, "Specifies arguments which alter how the interpreter is started (for example, -O t" +
        "o generate optimized byte code).");
            // 
            // _interpArgs
            // 
            this._interpArgs.Dock = System.Windows.Forms.DockStyle.Fill;
            this._interpArgs.Location = new System.Drawing.Point(133, 55);
            this._interpArgs.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._interpArgs.MinimumSize = new System.Drawing.Size(40, 4);
            this._interpArgs.Name = "_interpArgs";
            this._interpArgs.Size = new System.Drawing.Size(291, 20);
            this._interpArgs.TabIndex = 5;
            this._toolTip.SetToolTip(this._interpArgs, "Specifies arguments which alter how the interpreter is started (for example, -O t" +
        "o generate optimized byte code).");
            this._interpArgs.TextChanged += new System.EventHandler(this.InterpreterArgumentsTextChanged);
            // 
            // _interpreterPath
            // 
            this._interpreterPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this._interpreterPath.Location = new System.Drawing.Point(133, 81);
            this._interpreterPath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._interpreterPath.MinimumSize = new System.Drawing.Size(40, 4);
            this._interpreterPath.Name = "_interpreterPath";
            this._interpreterPath.Size = new System.Drawing.Size(291, 20);
            this._interpreterPath.TabIndex = 7;
            this._toolTip.SetToolTip(this._interpreterPath, "Overrides the interpreter executable which is used for launching the project.");
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
            this._interpreterPathLabel.Text = "Interpreter &Path:";
            this._interpreterPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._toolTip.SetToolTip(this._interpreterPathLabel, "Overrides the interpreter executable which is used for launching the project.");
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this._debugGroup, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._runGroup, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(454, 293);
            this.tableLayoutPanel1.TabIndex = 18;
            // 
            // _debugGroup
            // 
            this._debugGroup.AutoSize = true;
            this._debugGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._debugGroup.Controls.Add(this.tableLayoutPanel3);
            this._debugGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._debugGroup.Location = new System.Drawing.Point(6, 213);
            this._debugGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._debugGroup.Name = "_debugGroup";
            this._debugGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._debugGroup.Size = new System.Drawing.Size(442, 52);
            this._debugGroup.TabIndex = 18;
            this._debugGroup.TabStop = false;
            this._debugGroup.Text = "Debug";
            this._debugGroup.Visible = false;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.AutoSize = true;
            this.tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel3.ColumnCount = 2;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.Controls.Add(this._mixedMode, 0, 0);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(6, 21);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 1;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.Size = new System.Drawing.Size(430, 23);
            this.tableLayoutPanel3.TabIndex = 24;
            // 
            // _mixedMode
            // 
            this._mixedMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._mixedMode.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._mixedMode, 2);
            this._mixedMode.Location = new System.Drawing.Point(6, 3);
            this._mixedMode.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._mixedMode.Name = "_mixedMode";
            this._mixedMode.Size = new System.Drawing.Size(418, 17);
            this._mixedMode.TabIndex = 0;
            this._mixedMode.Text = "Enable na&tive code debugging";
            this._mixedMode.UseVisualStyleBackColor = true;
            this._mixedMode.Visible = false;
            this._mixedMode.CheckedChanged += new System.EventHandler(this._mixedMode_CheckedChanged);
            // 
            // DefaultPythonLauncherOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "DefaultPythonLauncherOptions";
            this.Size = new System.Drawing.Size(454, 293);
            this._runGroup.ResumeLayout(false);
            this._runGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this._debugGroup.ResumeLayout(false);
            this._debugGroup.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _runGroup;
        private System.Windows.Forms.TextBox _arguments;
        private System.Windows.Forms.Label _argumentsLabel;
        private System.Windows.Forms.TextBox _searchPaths;
        private System.Windows.Forms.Label _searchPathLabel;
        private System.Windows.Forms.Label _interpArgsLabel;
        private System.Windows.Forms.TextBox _interpArgs;
        private System.Windows.Forms.TextBox _interpreterPath;
        private System.Windows.Forms.Label _interpreterPathLabel;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.GroupBox _debugGroup;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.CheckBox _mixedMode;
        private System.Windows.Forms.TextBox _envVars;
        private System.Windows.Forms.Label _envVarsLabel;
    }
}
