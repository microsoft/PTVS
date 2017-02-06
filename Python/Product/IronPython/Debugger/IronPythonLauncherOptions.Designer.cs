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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IronPythonLauncherOptions));
            this._debugGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._debugStdLib = new System.Windows.Forms.CheckBox();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._arguments = new System.Windows.Forms.TextBox();
            this._interpArgs = new System.Windows.Forms.TextBox();
            this._interpreterPath = new System.Windows.Forms.TextBox();
            this._interpreterPathLabel = new System.Windows.Forms.Label();
            this._interpArgsLabel = new System.Windows.Forms.Label();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._searchPathLabel = new System.Windows.Forms.Label();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._debugGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _debugGroup
            // 
            resources.ApplyResources(this._debugGroup, "_debugGroup");
            this._debugGroup.Controls.Add(this.tableLayoutPanel2);
            this._debugGroup.Name = "_debugGroup";
            this._debugGroup.TabStop = false;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this._debugStdLib, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this._searchPaths, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._arguments, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._interpArgs, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPath, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPathLabel, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpArgsLabel, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._argumentsLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._searchPathLabel, 0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _debugStdLib
            // 
            resources.ApplyResources(this._debugStdLib, "_debugStdLib");
            this.tableLayoutPanel2.SetColumnSpan(this._debugStdLib, 2);
            this._debugStdLib.Name = "_debugStdLib";
            this._debugStdLib.UseVisualStyleBackColor = true;
            this._debugStdLib.CheckedChanged += new System.EventHandler(this.DebugStdLibCheckedChanged);
            // 
            // _searchPaths
            // 
            resources.ApplyResources(this._searchPaths, "_searchPaths");
            this._searchPaths.Name = "_searchPaths";
            this._searchPaths.TextChanged += new System.EventHandler(this.SearchPathsTextChanged);
            // 
            // _arguments
            // 
            resources.ApplyResources(this._arguments, "_arguments");
            this._arguments.Name = "_arguments";
            this._arguments.TextChanged += new System.EventHandler(this.ArgumentsTextChanged);
            // 
            // _interpArgs
            // 
            resources.ApplyResources(this._interpArgs, "_interpArgs");
            this._interpArgs.Name = "_interpArgs";
            this._interpArgs.TextChanged += new System.EventHandler(this.InterpreterArgsTextChanged);
            // 
            // _interpreterPath
            // 
            resources.ApplyResources(this._interpreterPath, "_interpreterPath");
            this._interpreterPath.Name = "_interpreterPath";
            this._interpreterPath.TextChanged += new System.EventHandler(this.InterpreterPathTextChanged);
            // 
            // _interpreterPathLabel
            // 
            resources.ApplyResources(this._interpreterPathLabel, "_interpreterPathLabel");
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            // 
            // _interpArgsLabel
            // 
            resources.ApplyResources(this._interpArgsLabel, "_interpArgsLabel");
            this._interpArgsLabel.Name = "_interpArgsLabel";
            // 
            // _argumentsLabel
            // 
            resources.ApplyResources(this._argumentsLabel, "_argumentsLabel");
            this._argumentsLabel.Name = "_argumentsLabel";
            // 
            // _searchPathLabel
            // 
            resources.ApplyResources(this._searchPathLabel, "_searchPathLabel");
            this._searchPathLabel.Name = "_searchPathLabel";
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._debugGroup, 0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // IronPythonLauncherOptions
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "IronPythonLauncherOptions";
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
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}
