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
            this._debugGroup = new System.Windows.Forms.GroupBox();
            this._debugStdLib = new System.Windows.Forms.CheckBox();
            this._arguments = new System.Windows.Forms.TextBox();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._searchPathLabel = new System.Windows.Forms.Label();
            this._interpreterPath = new System.Windows.Forms.TextBox();
            this._interpreterPathLabel = new System.Windows.Forms.Label();
            this._debugGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _debugGroup
            // 
            this._debugGroup.Controls.Add(this._debugStdLib);
            this._debugGroup.Controls.Add(this._arguments);
            this._debugGroup.Controls.Add(this._argumentsLabel);
            this._debugGroup.Controls.Add(this._searchPaths);
            this._debugGroup.Controls.Add(this._searchPathLabel);
            this._debugGroup.Controls.Add(this._interpreterPath);
            this._debugGroup.Controls.Add(this._interpreterPathLabel);
            this._debugGroup.Location = new System.Drawing.Point(3, 3);
            this._debugGroup.Name = "_debugGroup";
            this._debugGroup.Size = new System.Drawing.Size(437, 131);
            this._debugGroup.TabIndex = 18;
            this._debugGroup.TabStop = false;
            this._debugGroup.Text = "Debug";
            // 
            // _debugStdLib
            // 
            this._debugStdLib.AutoSize = true;
            this._debugStdLib.Location = new System.Drawing.Point(7, 100);
            this._debugStdLib.Name = "_debugStdLib";
            this._debugStdLib.Size = new System.Drawing.Size(138, 17);
            this._debugStdLib.TabIndex = 20;
            this._debugStdLib.Text = "Debug Standard Library";
            this._debugStdLib.UseVisualStyleBackColor = true;
            this._debugStdLib.CheckedChanged += new System.EventHandler(this.DebugStdLibCheckedChanged);
            // 
            // _arguments
            // 
            this._arguments.Location = new System.Drawing.Point(139, 48);
            this._arguments.Name = "_arguments";
            this._arguments.Size = new System.Drawing.Size(286, 20);
            this._arguments.TabIndex = 1;
            this._arguments.TextChanged += new System.EventHandler(this.ArgumentsTextChanged);
            // 
            // _argumentsLabel
            // 
            this._argumentsLabel.AutoSize = true;
            this._argumentsLabel.Location = new System.Drawing.Point(4, 51);
            this._argumentsLabel.Name = "_argumentsLabel";
            this._argumentsLabel.Size = new System.Drawing.Size(130, 13);
            this._argumentsLabel.TabIndex = 19;
            this._argumentsLabel.Text = "Command Line Arguments";
            // 
            // _searchPaths
            // 
            this._searchPaths.Location = new System.Drawing.Point(139, 22);
            this._searchPaths.Name = "_searchPaths";
            this._searchPaths.Size = new System.Drawing.Size(286, 20);
            this._searchPaths.TabIndex = 0;
            this._searchPaths.TextChanged += new System.EventHandler(this.SearchPathsTextChanged);
            // 
            // _searchPathLabel
            // 
            this._searchPathLabel.AutoSize = true;
            this._searchPathLabel.Location = new System.Drawing.Point(4, 25);
            this._searchPathLabel.Name = "_searchPathLabel";
            this._searchPathLabel.Size = new System.Drawing.Size(71, 13);
            this._searchPathLabel.TabIndex = 17;
            this._searchPathLabel.Text = "Search Paths";
            // 
            // _interpreterPath
            // 
            this._interpreterPath.Location = new System.Drawing.Point(139, 74);
            this._interpreterPath.Name = "_interpreterPath";
            this._interpreterPath.Size = new System.Drawing.Size(286, 20);
            this._interpreterPath.TabIndex = 2;
            this._interpreterPath.TextChanged += new System.EventHandler(this.InterpreterPathTextChanged);
            // 
            // _interpreterPathLabel
            // 
            this._interpreterPathLabel.AutoSize = true;
            this._interpreterPathLabel.Location = new System.Drawing.Point(4, 77);
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            this._interpreterPathLabel.Size = new System.Drawing.Size(80, 13);
            this._interpreterPathLabel.TabIndex = 15;
            this._interpreterPathLabel.Text = "Interpreter Path";
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
        private System.Windows.Forms.TextBox _arguments;
        private System.Windows.Forms.Label _argumentsLabel;
        private System.Windows.Forms.TextBox _searchPaths;
        private System.Windows.Forms.Label _searchPathLabel;
        private System.Windows.Forms.TextBox _interpreterPath;
        private System.Windows.Forms.Label _interpreterPathLabel;
    }
}
