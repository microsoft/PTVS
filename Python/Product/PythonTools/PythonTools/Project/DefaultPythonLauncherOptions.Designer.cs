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
            this._debugGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._searchPathLabel = new System.Windows.Forms.Label();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._arguments = new System.Windows.Forms.TextBox();
            this._interpArgsLabel = new System.Windows.Forms.Label();
            this._interpArgs = new System.Windows.Forms.TextBox();
            this._interpreterPathLabel = new System.Windows.Forms.Label();
            this._interpreterPath = new System.Windows.Forms.TextBox();
            this._mixedMode = new System.Windows.Forms.CheckBox();
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
            this._debugGroup.Size = new System.Drawing.Size(438, 156);
            this._debugGroup.TabIndex = 17;
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
            this.tableLayoutPanel2.Controls.Add(this._searchPathLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._searchPaths, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._argumentsLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._arguments, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._interpArgsLabel, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpArgs, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPathLabel, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPath, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this._mixedMode, 0, 4);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 21);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 5;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(426, 127);
            this.tableLayoutPanel2.TabIndex = 24;
            // 
            // _searchPathLabel
            // 
            this._searchPathLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._searchPathLabel.AutoSize = true;
            this._searchPathLabel.Location = new System.Drawing.Point(6, 6);
            this._searchPathLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._searchPathLabel.Name = "_searchPathLabel";
            this._searchPathLabel.Size = new System.Drawing.Size(74, 13);
            this._searchPathLabel.TabIndex = 17;
            this._searchPathLabel.Text = "Search &Paths:";
            this._searchPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _searchPaths
            // 
            this._searchPaths.Dock = System.Windows.Forms.DockStyle.Fill;
            this._searchPaths.Location = new System.Drawing.Point(129, 3);
            this._searchPaths.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._searchPaths.MinimumSize = new System.Drawing.Size(40, 4);
            this._searchPaths.Name = "_searchPaths";
            this._searchPaths.Size = new System.Drawing.Size(291, 20);
            this._searchPaths.TabIndex = 0;
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
            this._argumentsLabel.TabIndex = 19;
            this._argumentsLabel.Text = "Script &Arguments:";
            this._argumentsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _arguments
            // 
            this._arguments.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arguments.Location = new System.Drawing.Point(129, 29);
            this._arguments.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._arguments.MinimumSize = new System.Drawing.Size(40, 4);
            this._arguments.Name = "_arguments";
            this._arguments.Size = new System.Drawing.Size(291, 20);
            this._arguments.TabIndex = 1;
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
            this._interpArgsLabel.TabIndex = 21;
            this._interpArgsLabel.Text = "Interpreter A&rguments:";
            this._interpArgsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _interpArgs
            // 
            this._interpArgs.Dock = System.Windows.Forms.DockStyle.Fill;
            this._interpArgs.Location = new System.Drawing.Point(129, 55);
            this._interpArgs.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._interpArgs.MinimumSize = new System.Drawing.Size(40, 4);
            this._interpArgs.Name = "_interpArgs";
            this._interpArgs.Size = new System.Drawing.Size(291, 20);
            this._interpArgs.TabIndex = 20;
            this._interpArgs.TextChanged += new System.EventHandler(this.InterpreterArgumentsTextChanged);
            // 
            // _interpreterPathLabel
            // 
            this._interpreterPathLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._interpreterPathLabel.AutoSize = true;
            this._interpreterPathLabel.Location = new System.Drawing.Point(6, 84);
            this._interpreterPathLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            this._interpreterPathLabel.Size = new System.Drawing.Size(83, 13);
            this._interpreterPathLabel.TabIndex = 23;
            this._interpreterPathLabel.Text = "Interpreter &Path:";
            this._interpreterPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _interpreterPath
            // 
            this._interpreterPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this._interpreterPath.Location = new System.Drawing.Point(129, 81);
            this._interpreterPath.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._interpreterPath.MinimumSize = new System.Drawing.Size(40, 4);
            this._interpreterPath.Name = "_interpreterPath";
            this._interpreterPath.Size = new System.Drawing.Size(291, 20);
            this._interpreterPath.TabIndex = 22;
            this._interpreterPath.TextChanged += new System.EventHandler(this.InterpreterPathTextChanged);
            // 
            // _mixedMode
            // 
            this._mixedMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._mixedMode.AutoSize = true;
            this.tableLayoutPanel2.SetColumnSpan(this._mixedMode, 2);
            this._mixedMode.Location = new System.Drawing.Point(6, 107);
            this._mixedMode.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._mixedMode.Name = "_mixedMode";
            this._mixedMode.Size = new System.Drawing.Size(414, 17);
            this._mixedMode.TabIndex = 24;
            this._mixedMode.Text = "Enable na&tive code debugging";
            this._mixedMode.UseVisualStyleBackColor = true;
            this._mixedMode.Visible = false;
            this._mixedMode.CheckedChanged += new System.EventHandler(this._mixedMode_CheckedChanged);
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
            this.tableLayoutPanel1.Size = new System.Drawing.Size(450, 192);
            this.tableLayoutPanel1.TabIndex = 18;
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
            this.Size = new System.Drawing.Size(450, 192);
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
        private System.Windows.Forms.CheckBox _mixedMode;
    }
}
