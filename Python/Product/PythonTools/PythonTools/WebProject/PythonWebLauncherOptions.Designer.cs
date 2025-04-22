namespace Microsoft.PythonTools.Project.Web {
    partial class PythonWebLauncherOptions {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonWebLauncherOptions));
            this._debugGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._environment = new System.Windows.Forms.TextBox();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._arguments = new System.Windows.Forms.TextBox();
            this._interpArgs = new System.Windows.Forms.TextBox();
            this._interpreterPath = new System.Windows.Forms.TextBox();
            this._interpArgsLabel = new System.Windows.Forms.Label();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._searchPathLabel = new System.Windows.Forms.Label();
            this._portNumberLabel = new System.Windows.Forms.Label();
            this._portNumber = new System.Windows.Forms.TextBox();
            this._launchUrl = new System.Windows.Forms.TextBox();
            this._launchUrlLabel = new System.Windows.Forms.Label();
            this._environmentLabel = new System.Windows.Forms.Label();
            this._interpreterPathLabel = new System.Windows.Forms.Label();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._debugServerTarget = new System.Windows.Forms.TextBox();
            this._debugServerArguments = new System.Windows.Forms.TextBox();
            this._debugServerEnvironment = new System.Windows.Forms.TextBox();
            this._debugServerEnvironmentLabel = new System.Windows.Forms.Label();
            this._debugServerArgumentsLabel = new System.Windows.Forms.Label();
            this._debugServerTargetLabel = new System.Windows.Forms.Label();
            this._debugServerTargetType = new System.Windows.Forms.ComboBox();
            this._runServerTarget = new System.Windows.Forms.TextBox();
            this._runServerArguments = new System.Windows.Forms.TextBox();
            this._runServerEnvironment = new System.Windows.Forms.TextBox();
            this._runServerEnvironmentLabel = new System.Windows.Forms.Label();
            this._runServerArgumentsLabel = new System.Windows.Forms.Label();
            this._runServerTargetLabel = new System.Windows.Forms.Label();
            this._runServerTargetType = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._debugServerGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this._runServerGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this._debugGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this._debugServerGroup.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this._runServerGroup.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
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
            this.tableLayoutPanel2.Controls.Add(this._environment, 1, 6);
            this.tableLayoutPanel2.Controls.Add(this._searchPaths, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._arguments, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._interpArgs, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPath, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpArgsLabel, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._argumentsLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._searchPathLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._portNumberLabel, 0, 5);
            this.tableLayoutPanel2.Controls.Add(this._portNumber, 1, 5);
            this.tableLayoutPanel2.Controls.Add(this._launchUrl, 1, 4);
            this.tableLayoutPanel2.Controls.Add(this._launchUrlLabel, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this._environmentLabel, 0, 6);
            this.tableLayoutPanel2.Controls.Add(this._interpreterPathLabel, 0, 2);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _environment
            // 
            resources.ApplyResources(this._environment, "_environment");
            this._environment.Name = "_environment";
            this._toolTip.SetToolTip(this._environment, resources.GetString("_environment.ToolTip"));
            this._environment.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _searchPaths
            // 
            resources.ApplyResources(this._searchPaths, "_searchPaths");
            this._searchPaths.Name = "_searchPaths";
            this._toolTip.SetToolTip(this._searchPaths, resources.GetString("_searchPaths.ToolTip"));
            this._searchPaths.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _arguments
            // 
            resources.ApplyResources(this._arguments, "_arguments");
            this._arguments.Name = "_arguments";
            this._toolTip.SetToolTip(this._arguments, resources.GetString("_arguments.ToolTip"));
            this._arguments.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _interpArgs
            // 
            resources.ApplyResources(this._interpArgs, "_interpArgs");
            this._interpArgs.Name = "_interpArgs";
            this._toolTip.SetToolTip(this._interpArgs, resources.GetString("_interpArgs.ToolTip"));
            this._interpArgs.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _interpreterPath
            // 
            resources.ApplyResources(this._interpreterPath, "_interpreterPath");
            this._interpreterPath.Name = "_interpreterPath";
            this._toolTip.SetToolTip(this._interpreterPath, resources.GetString("_interpreterPath.ToolTip"));
            this._interpreterPath.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _interpArgsLabel
            // 
            resources.ApplyResources(this._interpArgsLabel, "_interpArgsLabel");
            this._interpArgsLabel.Name = "_interpArgsLabel";
            this._toolTip.SetToolTip(this._interpArgsLabel, resources.GetString("_interpArgsLabel.ToolTip"));
            // 
            // _argumentsLabel
            // 
            resources.ApplyResources(this._argumentsLabel, "_argumentsLabel");
            this._argumentsLabel.Name = "_argumentsLabel";
            this._toolTip.SetToolTip(this._argumentsLabel, resources.GetString("_argumentsLabel.ToolTip"));
            // 
            // _searchPathLabel
            // 
            resources.ApplyResources(this._searchPathLabel, "_searchPathLabel");
            this._searchPathLabel.Name = "_searchPathLabel";
            this._toolTip.SetToolTip(this._searchPathLabel, resources.GetString("_searchPathLabel.ToolTip"));
            // 
            // _portNumberLabel
            // 
            resources.ApplyResources(this._portNumberLabel, "_portNumberLabel");
            this._portNumberLabel.Name = "_portNumberLabel";
            this._toolTip.SetToolTip(this._portNumberLabel, resources.GetString("_portNumberLabel.ToolTip"));
            // 
            // _portNumber
            // 
            resources.ApplyResources(this._portNumber, "_portNumber");
            this._portNumber.Name = "_portNumber";
            this._toolTip.SetToolTip(this._portNumber, resources.GetString("_portNumber.ToolTip"));
            this._portNumber.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _launchUrl
            // 
            resources.ApplyResources(this._launchUrl, "_launchUrl");
            this._launchUrl.Name = "_launchUrl";
            this._toolTip.SetToolTip(this._launchUrl, resources.GetString("_launchUrl.ToolTip"));
            this._launchUrl.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _launchUrlLabel
            // 
            resources.ApplyResources(this._launchUrlLabel, "_launchUrlLabel");
            this._launchUrlLabel.Name = "_launchUrlLabel";
            this._toolTip.SetToolTip(this._launchUrlLabel, resources.GetString("_launchUrlLabel.ToolTip"));
            // 
            // _environmentLabel
            // 
            resources.ApplyResources(this._environmentLabel, "_environmentLabel");
            this._environmentLabel.Name = "_environmentLabel";
            this._toolTip.SetToolTip(this._environmentLabel, resources.GetString("_environmentLabel.ToolTip"));
            // 
            // _interpreterPathLabel
            // 
            resources.ApplyResources(this._interpreterPathLabel, "_interpreterPathLabel");
            this._interpreterPathLabel.Name = "_interpreterPathLabel";
            this._toolTip.SetToolTip(this._interpreterPathLabel, resources.GetString("_interpreterPathLabel.ToolTip"));
            // 
            // _debugServerTarget
            // 
            resources.ApplyResources(this._debugServerTarget, "_debugServerTarget");
            this._debugServerTarget.Name = "_debugServerTarget";
            this._toolTip.SetToolTip(this._debugServerTarget, resources.GetString("_debugServerTarget.ToolTip"));
            this._debugServerTarget.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _debugServerArguments
            // 
            resources.ApplyResources(this._debugServerArguments, "_debugServerArguments");
            this.tableLayoutPanel4.SetColumnSpan(this._debugServerArguments, 2);
            this._debugServerArguments.Name = "_debugServerArguments";
            this._toolTip.SetToolTip(this._debugServerArguments, resources.GetString("_debugServerArguments.ToolTip"));
            this._debugServerArguments.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _debugServerEnvironment
            // 
            resources.ApplyResources(this._debugServerEnvironment, "_debugServerEnvironment");
            this.tableLayoutPanel4.SetColumnSpan(this._debugServerEnvironment, 2);
            this._debugServerEnvironment.Name = "_debugServerEnvironment";
            this._toolTip.SetToolTip(this._debugServerEnvironment, resources.GetString("_debugServerEnvironment.ToolTip"));
            this._debugServerEnvironment.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _debugServerEnvironmentLabel
            // 
            resources.ApplyResources(this._debugServerEnvironmentLabel, "_debugServerEnvironmentLabel");
            this._debugServerEnvironmentLabel.Name = "_debugServerEnvironmentLabel";
            this._toolTip.SetToolTip(this._debugServerEnvironmentLabel, resources.GetString("_debugServerEnvironmentLabel.ToolTip"));
            // 
            // _debugServerArgumentsLabel
            // 
            resources.ApplyResources(this._debugServerArgumentsLabel, "_debugServerArgumentsLabel");
            this._debugServerArgumentsLabel.Name = "_debugServerArgumentsLabel";
            this._toolTip.SetToolTip(this._debugServerArgumentsLabel, resources.GetString("_debugServerArgumentsLabel.ToolTip"));
            // 
            // _debugServerTargetLabel
            // 
            resources.ApplyResources(this._debugServerTargetLabel, "_debugServerTargetLabel");
            this._debugServerTargetLabel.Name = "_debugServerTargetLabel";
            this._toolTip.SetToolTip(this._debugServerTargetLabel, resources.GetString("_debugServerTargetLabel.ToolTip"));
            // 
            // _debugServerTargetType
            // 
            resources.ApplyResources(this._debugServerTargetType, "_debugServerTargetType");
            this._debugServerTargetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._debugServerTargetType.FormattingEnabled = true;
            this._debugServerTargetType.Items.AddRange(new object[] {
            resources.GetString("_debugServerTargetType.Items"),
            resources.GetString("_debugServerTargetType.Items1"),
            resources.GetString("_debugServerTargetType.Items2"),
            resources.GetString("_debugServerTargetType.Items3")});
            this._debugServerTargetType.Name = "_debugServerTargetType";
            this._toolTip.SetToolTip(this._debugServerTargetType, resources.GetString("_debugServerTargetType.ToolTip"));
            this._debugServerTargetType.SelectedValueChanged += new System.EventHandler(this.Setting_SelectedValueChanged);
            // 
            // _runServerTarget
            // 
            resources.ApplyResources(this._runServerTarget, "_runServerTarget");
            this._runServerTarget.Name = "_runServerTarget";
            this._toolTip.SetToolTip(this._runServerTarget, resources.GetString("_runServerTarget.ToolTip"));
            this._runServerTarget.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _runServerArguments
            // 
            resources.ApplyResources(this._runServerArguments, "_runServerArguments");
            this.tableLayoutPanel3.SetColumnSpan(this._runServerArguments, 2);
            this._runServerArguments.Name = "_runServerArguments";
            this._toolTip.SetToolTip(this._runServerArguments, resources.GetString("_runServerArguments.ToolTip"));
            this._runServerArguments.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _runServerEnvironment
            // 
            resources.ApplyResources(this._runServerEnvironment, "_runServerEnvironment");
            this.tableLayoutPanel3.SetColumnSpan(this._runServerEnvironment, 2);
            this._runServerEnvironment.Name = "_runServerEnvironment";
            this._toolTip.SetToolTip(this._runServerEnvironment, resources.GetString("_runServerEnvironment.ToolTip"));
            this._runServerEnvironment.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _runServerEnvironmentLabel
            // 
            resources.ApplyResources(this._runServerEnvironmentLabel, "_runServerEnvironmentLabel");
            this._runServerEnvironmentLabel.Name = "_runServerEnvironmentLabel";
            this._toolTip.SetToolTip(this._runServerEnvironmentLabel, resources.GetString("_runServerEnvironmentLabel.ToolTip"));
            // 
            // _runServerArgumentsLabel
            // 
            resources.ApplyResources(this._runServerArgumentsLabel, "_runServerArgumentsLabel");
            this._runServerArgumentsLabel.Name = "_runServerArgumentsLabel";
            this._toolTip.SetToolTip(this._runServerArgumentsLabel, resources.GetString("_runServerArgumentsLabel.ToolTip"));
            // 
            // _runServerTargetLabel
            // 
            resources.ApplyResources(this._runServerTargetLabel, "_runServerTargetLabel");
            this._runServerTargetLabel.Name = "_runServerTargetLabel";
            this._toolTip.SetToolTip(this._runServerTargetLabel, resources.GetString("_runServerTargetLabel.ToolTip"));
            // 
            // _runServerTargetType
            // 
            resources.ApplyResources(this._runServerTargetType, "_runServerTargetType");
            this._runServerTargetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._runServerTargetType.FormattingEnabled = true;
            this._runServerTargetType.Items.AddRange(new object[] {
            resources.GetString("_runServerTargetType.Items"),
            resources.GetString("_runServerTargetType.Items1"),
            resources.GetString("_runServerTargetType.Items2"),
            resources.GetString("_runServerTargetType.Items3")});
            this._runServerTargetType.Name = "_runServerTargetType";
            this._toolTip.SetToolTip(this._runServerTargetType, resources.GetString("_runServerTargetType.ToolTip"));
            this._runServerTargetType.SelectedValueChanged += new System.EventHandler(this.Setting_SelectedValueChanged);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._debugServerGroup, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this._runServerGroup, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._debugGroup, 0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _debugServerGroup
            // 
            resources.ApplyResources(this._debugServerGroup, "_debugServerGroup");
            this._debugServerGroup.Controls.Add(this.tableLayoutPanel4);
            this._debugServerGroup.Name = "_debugServerGroup";
            this._debugServerGroup.TabStop = false;
            // 
            // tableLayoutPanel4
            // 
            resources.ApplyResources(this.tableLayoutPanel4, "tableLayoutPanel4");
            this.tableLayoutPanel4.Controls.Add(this._debugServerTarget, 1, 0);
            this.tableLayoutPanel4.Controls.Add(this._debugServerArguments, 1, 1);
            this.tableLayoutPanel4.Controls.Add(this._debugServerEnvironment, 1, 2);
            this.tableLayoutPanel4.Controls.Add(this._debugServerEnvironmentLabel, 0, 2);
            this.tableLayoutPanel4.Controls.Add(this._debugServerArgumentsLabel, 0, 1);
            this.tableLayoutPanel4.Controls.Add(this._debugServerTargetLabel, 0, 0);
            this.tableLayoutPanel4.Controls.Add(this._debugServerTargetType, 2, 0);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            // 
            // _runServerGroup
            // 
            resources.ApplyResources(this._runServerGroup, "_runServerGroup");
            this._runServerGroup.Controls.Add(this.tableLayoutPanel3);
            this._runServerGroup.Name = "_runServerGroup";
            this._runServerGroup.TabStop = false;
            // 
            // tableLayoutPanel3
            // 
            resources.ApplyResources(this.tableLayoutPanel3, "tableLayoutPanel3");
            this.tableLayoutPanel3.Controls.Add(this._runServerTarget, 1, 0);
            this.tableLayoutPanel3.Controls.Add(this._runServerArguments, 1, 1);
            this.tableLayoutPanel3.Controls.Add(this._runServerEnvironment, 1, 2);
            this.tableLayoutPanel3.Controls.Add(this._runServerEnvironmentLabel, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this._runServerArgumentsLabel, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._runServerTargetLabel, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._runServerTargetType, 2, 0);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            // 
            // PythonWebLauncherOptions
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PythonWebLauncherOptions";
            this._debugGroup.ResumeLayout(false);
            this._debugGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this._debugServerGroup.ResumeLayout(false);
            this._debugServerGroup.PerformLayout();
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this._runServerGroup.ResumeLayout(false);
            this._runServerGroup.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
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
        private System.Windows.Forms.Label _portNumberLabel;
        private System.Windows.Forms.TextBox _portNumber;
        private System.Windows.Forms.TextBox _launchUrl;
        private System.Windows.Forms.Label _launchUrlLabel;
        private System.Windows.Forms.GroupBox _debugServerGroup;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private System.Windows.Forms.TextBox _debugServerTarget;
        private System.Windows.Forms.TextBox _debugServerArguments;
        private System.Windows.Forms.TextBox _debugServerEnvironment;
        private System.Windows.Forms.Label _debugServerEnvironmentLabel;
        private System.Windows.Forms.Label _debugServerArgumentsLabel;
        private System.Windows.Forms.Label _debugServerTargetLabel;
        private System.Windows.Forms.ComboBox _debugServerTargetType;
        private System.Windows.Forms.GroupBox _runServerGroup;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.TextBox _runServerTarget;
        private System.Windows.Forms.TextBox _runServerArguments;
        private System.Windows.Forms.TextBox _runServerEnvironment;
        private System.Windows.Forms.Label _runServerEnvironmentLabel;
        private System.Windows.Forms.Label _runServerArgumentsLabel;
        private System.Windows.Forms.Label _runServerTargetLabel;
        private System.Windows.Forms.ComboBox _runServerTargetType;
        private System.Windows.Forms.TextBox _environment;
        private System.Windows.Forms.Label _environmentLabel;
    }
}
