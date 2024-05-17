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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DefaultPythonLauncherOptions));
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
            resources.ApplyResources(this._runGroup, "_runGroup");
            this._runGroup.Controls.Add(this.tableLayoutPanel2);
            this._runGroup.Name = "_runGroup";
            this._runGroup.TabStop = false;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this._envVars, 1, 4);
            this.tableLayoutPanel2.Controls.Add(this._envVarsLabel, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this._searchPathLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._searchPaths, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._argumentsLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._arguments, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._interpArgs, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPath, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPathLabel, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpArgsLabel, 0, 3);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _envVars
            // 
            this._envVars.AcceptsReturn = true;
            resources.ApplyResources(this._envVars, "_envVars");
            this._envVars.Name = "_envVars";
            this._toolTip.SetToolTip(this._envVars, resources.GetString("_envVars.ToolTip"));
            this._envVars.TextChanged += new System.EventHandler(this._envVars_TextChanged);
            this._envVars.AccessibleName = resources.GetString("_envVars.ToolTip");
            // 
            // _envVarsLabel
            // 
            resources.ApplyResources(this._envVarsLabel, "_envVarsLabel");
            this._envVarsLabel.Name = "_envVarsLabel";
            this._toolTip.SetToolTip(this._envVarsLabel, resources.GetString("_envVarsLabel.ToolTip"));
            this._envVarsLabel.AccessibleName = resources.GetString("_envVarsLabel.ToolTip");
            // 
            // _searchPathLabel
            // 
            resources.ApplyResources(this._searchPathLabel, "_searchPathLabel");
            this._searchPathLabel.Name = "_searchPathLabel";
            this._toolTip.SetToolTip(this._searchPathLabel, resources.GetString("_searchPathLabel.ToolTip"));
            this._searchPathLabel.AccessibleName = resources.GetString("_searchPathLabel.ToolTip");
            // 
            // _searchPaths
            // 
            resources.ApplyResources(this._searchPaths, "_searchPaths");
            this._searchPaths.Name = "_searchPaths";
            this._toolTip.SetToolTip(this._searchPaths, resources.GetString("_searchPaths.ToolTip"));
            this._searchPaths.TextChanged += new System.EventHandler(this.SearchPathsTextChanged);
            this._searchPaths.AccessibleName = resources.GetString("_searchPaths.ToolTip");
            // 
            // _argumentsLabel
            // 
            resources.ApplyResources(this._argumentsLabel, "_argumentsLabel");
            this._argumentsLabel.Name = "_argumentsLabel";
            this._toolTip.SetToolTip(this._argumentsLabel, resources.GetString("_argumentsLabel.ToolTip"));
            this._argumentsLabel.AccessibleName = resources.GetString("_argumentsLabel.ToolTip");
            // 
            // _arguments
            // 
            resources.ApplyResources(this._arguments, "_arguments");
            this._arguments.Name = "_arguments";
            this._toolTip.SetToolTip(this._arguments, resources.GetString("_arguments.ToolTip"));
            this._arguments.TextChanged += new System.EventHandler(this.ArgumentsTextChanged);
            this._arguments.AccessibleName = resources.GetString("_arguments.ToolTip");
            // 
            // _interpArgsLabel
            // 
            resources.ApplyResources(this._interpArgsLabel, "_interpArgsLabel");
            this._interpArgsLabel.Name = "_interpArgsLabel";
            this._toolTip.SetToolTip(this._interpArgsLabel, resources.GetString("_interpArgsLabel.ToolTip"));
            this._interpArgsLabel.AccessibleName = resources.GetString("_interpArgsLabel.ToolTip");
            // 
            // _interpArgs
            // 
            resources.ApplyResources(this._interpArgs, "_interpArgs");
            this._interpArgs.Name = "_interpArgs";
            this._toolTip.SetToolTip(this._interpArgs, resources.GetString("_interpArgs.ToolTip"));
            this._interpArgs.TextChanged += new System.EventHandler(this.InterpreterArgumentsTextChanged);
            this._interpArgs.AccessibleName = resources.GetString("_interpArgs.ToolTip");
            // 
            // _interpreterPath
            // 
            resources.ApplyResources(this._interpreterPath, "_interpreterPath");
            this._interpreterPath.Name = "_interpreterPath";
            this._toolTip.SetToolTip(this._interpreterPath, resources.GetString("_interpreterPath.ToolTip"));
            this._interpreterPath.TextChanged += new System.EventHandler(this.InterpreterPathTextChanged);
            this._interpreterPath.AccessibleName = resources.GetString("_interpreterPath.ToolTip");
            // 
            // _interpreterPathLabel
            // 
            resources.ApplyResources(this._interpreterPathLabel, "_interpreterPathLabel");
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            this._toolTip.SetToolTip(this._interpreterPathLabel, resources.GetString("_interpreterPathLabel.ToolTip"));
            this._interpreterPathLabel.AccessibleName = resources.GetString("_interpreterPathLabel.ToolTip");
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._debugGroup, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._runGroup, 0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _debugGroup
            // 
            resources.ApplyResources(this._debugGroup, "_debugGroup");
            this._debugGroup.Controls.Add(this.tableLayoutPanel3);
            this._debugGroup.Name = "_debugGroup";
            this._debugGroup.TabStop = false;
            // 
            // tableLayoutPanel3
            // 
            resources.ApplyResources(this.tableLayoutPanel3, "tableLayoutPanel3");
            this.tableLayoutPanel3.Controls.Add(this._mixedMode, 0, 0);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            // 
            // _mixedMode
            // 
            resources.ApplyResources(this._mixedMode, "_mixedMode");
            this.tableLayoutPanel3.SetColumnSpan(this._mixedMode, 2);
            this._mixedMode.Name = "_mixedMode";
            this._mixedMode.UseVisualStyleBackColor = true;
            this._mixedMode.CheckedChanged += new System.EventHandler(this._mixedMode_CheckedChanged);
            // 
            // DefaultPythonLauncherOptions
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "DefaultPythonLauncherOptions";
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
