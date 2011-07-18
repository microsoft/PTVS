namespace Microsoft.IronPythonTools.Debugger {
    partial class IronPythonLauncherOptions {
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
            this._debugStdLib = new System.Windows.Forms.CheckBox();
            this._interpreterPath = new System.Windows.Forms.TextBox();
            this._interpreterPathLabel = new System.Windows.Forms.Label();
            this._interpArgsLabel = new System.Windows.Forms.Label();
            this._interpArgs = new System.Windows.Forms.TextBox();
            this._arguments = new System.Windows.Forms.TextBox();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._searchPathLabel = new System.Windows.Forms.Label();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._debugGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _debugGroup
            // 
            this._debugGroup.Controls.Add(this._interpreterPath);
            this._debugGroup.Controls.Add(this._interpreterPathLabel);
            this._debugGroup.Controls.Add(this._interpArgsLabel);
            this._debugGroup.Controls.Add(this._interpArgs);
            this._debugGroup.Controls.Add(this._arguments);
            this._debugGroup.Controls.Add(this._argumentsLabel);
            this._debugGroup.Controls.Add(this._searchPaths);
            this._debugGroup.Controls.Add(this._searchPathLabel);
            this._debugGroup.Controls.Add(this._debugStdLib);
            this._debugGroup.Location = new System.Drawing.Point(3, 3);
            this._debugGroup.Name = "_debugGroup";
            this._debugGroup.Size = new System.Drawing.Size(437, 149);
            this._debugGroup.TabIndex = 18;
            this._debugGroup.TabStop = false;
            this._debugGroup.Text = "Debug";
            // 
            // _debugStdLib
            // 
            this._debugStdLib.AutoSize = true;
            this._debugStdLib.Location = new System.Drawing.Point(12, 121);
            this._debugStdLib.Name = "_debugStdLib";
            this._debugStdLib.Size = new System.Drawing.Size(138, 17);
            this._debugStdLib.TabIndex = 20;
            this._debugStdLib.Text = "Debug Standard Library";
            this._debugStdLib.UseVisualStyleBackColor = true;
            this._debugStdLib.CheckedChanged += new System.EventHandler(this.DebugStdLibCheckedChanged);
            // 
            // _interpreterPath
            // 
            this._interpreterPath.Location = new System.Drawing.Point(142, 95);
            this._interpreterPath.Name = "_interpreterPath";
            this._interpreterPath.Size = new System.Drawing.Size(286, 20);
            this._interpreterPath.TabIndex = 30;
            // 
            // _interpreterPathLabel
            // 
            this._interpreterPathLabel.AutoSize = true;
            this._interpreterPathLabel.Location = new System.Drawing.Point(9, 98);
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            this._interpreterPathLabel.Size = new System.Drawing.Size(83, 13);
            this._interpreterPathLabel.TabIndex = 31;
            this._interpreterPathLabel.Text = "Interpreter Path:";
            // 
            // _interpArgsLabel
            // 
            this._interpArgsLabel.AutoSize = true;
            this._interpArgsLabel.Location = new System.Drawing.Point(9, 71);
            this._interpArgsLabel.Name = "_interpArgsLabel";
            this._interpArgsLabel.Size = new System.Drawing.Size(111, 13);
            this._interpArgsLabel.TabIndex = 29;
            this._interpArgsLabel.Text = "Interpreter Arguments:";
            // 
            // _interpArgs
            // 
            this._interpArgs.Location = new System.Drawing.Point(142, 68);
            this._interpArgs.Name = "_interpArgs";
            this._interpArgs.Size = new System.Drawing.Size(286, 20);
            this._interpArgs.TabIndex = 28;
            this._interpArgs.TextChanged += new System.EventHandler(this.InterpreterArgsTextChanged);
            // 
            // _arguments
            // 
            this._arguments.Location = new System.Drawing.Point(142, 42);
            this._arguments.Name = "_arguments";
            this._arguments.Size = new System.Drawing.Size(286, 20);
            this._arguments.TabIndex = 25;
            // 
            // _argumentsLabel
            // 
            this._argumentsLabel.AutoSize = true;
            this._argumentsLabel.Location = new System.Drawing.Point(9, 45);
            this._argumentsLabel.Name = "_argumentsLabel";
            this._argumentsLabel.Size = new System.Drawing.Size(90, 13);
            this._argumentsLabel.TabIndex = 27;
            this._argumentsLabel.Text = "Script Arguments:";
            // 
            // _searchPaths
            // 
            this._searchPaths.Location = new System.Drawing.Point(142, 16);
            this._searchPaths.Name = "_searchPaths";
            this._searchPaths.Size = new System.Drawing.Size(286, 20);
            this._searchPaths.TabIndex = 24;
            // 
            // _searchPathLabel
            // 
            this._searchPathLabel.AutoSize = true;
            this._searchPathLabel.Location = new System.Drawing.Point(9, 19);
            this._searchPathLabel.Name = "_searchPathLabel";
            this._searchPathLabel.Size = new System.Drawing.Size(74, 13);
            this._searchPathLabel.TabIndex = 26;
            this._searchPathLabel.Text = "Search Paths:";
            // 
            // IronPythonLauncherOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._debugGroup);
            this.Name = "IronPythonLauncherOptions";
            this.Size = new System.Drawing.Size(495, 260);
            this._debugGroup.ResumeLayout(false);
            this._debugGroup.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox _debugGroup;
        private System.Windows.Forms.CheckBox _debugStdLib;
        private System.Windows.Forms.TextBox _interpreterPath;
        private System.Windows.Forms.Label _interpreterPathLabel;
        private System.Windows.Forms.Label _interpArgsLabel;
        private System.Windows.Forms.TextBox _interpArgs;
        private System.Windows.Forms.TextBox _arguments;
        private System.Windows.Forms.Label _argumentsLabel;
        private System.Windows.Forms.TextBox _searchPaths;
        private System.Windows.Forms.Label _searchPathLabel;
        private System.Windows.Forms.ToolTip _toolTip;
    }
}
