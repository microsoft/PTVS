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
            this._debugGroup.Location = new System.Drawing.Point(3, 3);
            this._debugGroup.Name = "_debugGroup";
            this._debugGroup.Size = new System.Drawing.Size(437, 131);
            this._debugGroup.TabIndex = 17;
            this._debugGroup.TabStop = false;
            this._debugGroup.Text = "Debug";
            // 
            // _interpreterPath
            // 
            this._interpreterPath.Location = new System.Drawing.Point(139, 101);
            this._interpreterPath.Name = "_interpreterPath";
            this._interpreterPath.Size = new System.Drawing.Size(286, 20);
            this._interpreterPath.TabIndex = 22;
            this._interpreterPath.TextChanged += new System.EventHandler(this.InterpreterPathTextChanged);
            // 
            // _interpreterPathLabel
            // 
            this._interpreterPathLabel.AutoSize = true;
            this._interpreterPathLabel.Location = new System.Drawing.Point(6, 104);
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            this._interpreterPathLabel.Size = new System.Drawing.Size(83, 13);
            this._interpreterPathLabel.TabIndex = 23;
            this._interpreterPathLabel.Text = "Interpreter Path:";
            // 
            // _interpArgsLabel
            // 
            this._interpArgsLabel.AutoSize = true;
            this._interpArgsLabel.Location = new System.Drawing.Point(6, 77);
            this._interpArgsLabel.Name = "_interpArgsLabel";
            this._interpArgsLabel.Size = new System.Drawing.Size(111, 13);
            this._interpArgsLabel.TabIndex = 21;
            this._interpArgsLabel.Text = "Interpreter Arguments:";
            // 
            // _interpArgs
            // 
            this._interpArgs.Location = new System.Drawing.Point(139, 74);
            this._interpArgs.Name = "_interpArgs";
            this._interpArgs.Size = new System.Drawing.Size(286, 20);
            this._interpArgs.TabIndex = 20;
            this._interpArgs.TextChanged += new System.EventHandler(this.InterpreterArgumentsTextChanged);
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
            this._argumentsLabel.Location = new System.Drawing.Point(6, 51);
            this._argumentsLabel.Name = "_argumentsLabel";
            this._argumentsLabel.Size = new System.Drawing.Size(90, 13);
            this._argumentsLabel.TabIndex = 19;
            this._argumentsLabel.Text = "Script Arguments:";
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
            this._searchPathLabel.Location = new System.Drawing.Point(6, 25);
            this._searchPathLabel.Name = "_searchPathLabel";
            this._searchPathLabel.Size = new System.Drawing.Size(74, 13);
            this._searchPathLabel.TabIndex = 17;
            this._searchPathLabel.Text = "Search Paths:";
            // 
            // DefaultPythonLauncherOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._debugGroup);
            this.Name = "DefaultPythonLauncherOptions";
            this.Size = new System.Drawing.Size(469, 239);
            this._debugGroup.ResumeLayout(false);
            this._debugGroup.PerformLayout();
            this.ResumeLayout(false);

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
    }
}
