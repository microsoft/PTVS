namespace Microsoft.PythonTools.Project {
    partial class PythonGeneralPropertyPageControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonGeneralPropertyPageControl));
            this._applicationGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._startupFileLabel = new System.Windows.Forms.Label();
            this._startupFile = new System.Windows.Forms.TextBox();
            this._workingDirLabel = new System.Windows.Forms.Label();
            this._workingDirectory = new System.Windows.Forms.TextBox();
            this._windowsApplication = new System.Windows.Forms.CheckBox();
            this._defaultInterpreterLabel = new System.Windows.Forms.Label();
            this._defaultInterpreter = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._applicationGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _applicationGroup
            // 
            resources.ApplyResources(this._applicationGroup, "_applicationGroup");
            this._applicationGroup.Controls.Add(this.tableLayoutPanel2);
            this._applicationGroup.Name = "_applicationGroup";
            this._applicationGroup.TabStop = false;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this._startupFileLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._startupFile, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._workingDirLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._workingDirectory, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._windowsApplication, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._defaultInterpreterLabel, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._defaultInterpreter, 1, 3);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _startupFileLabel
            // 
            resources.ApplyResources(this._startupFileLabel, "_startupFileLabel");
            this._startupFileLabel.AutoEllipsis = true;
            this._startupFileLabel.Name = "_startupFileLabel";
            // 
            // _startupFile
            // 
            resources.ApplyResources(this._startupFile, "_startupFile");
            this._startupFile.Name = "_startupFile";
            this._startupFile.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _workingDirLabel
            // 
            resources.ApplyResources(this._workingDirLabel, "_workingDirLabel");
            this._workingDirLabel.AutoEllipsis = true;
            this._workingDirLabel.Name = "_workingDirLabel";
            // 
            // _workingDirectory
            // 
            resources.ApplyResources(this._workingDirectory, "_workingDirectory");
            this._workingDirectory.Name = "_workingDirectory";
            this._workingDirectory.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _windowsApplication
            // 
            resources.ApplyResources(this._windowsApplication, "_windowsApplication");
            this.tableLayoutPanel2.SetColumnSpan(this._windowsApplication, 2);
            this._windowsApplication.Name = "_windowsApplication";
            this._windowsApplication.UseVisualStyleBackColor = true;
            this._windowsApplication.CheckedChanged += new System.EventHandler(this.Changed);
            // 
            // _defaultInterpreterLabel
            // 
            resources.ApplyResources(this._defaultInterpreterLabel, "_defaultInterpreterLabel");
            this._defaultInterpreterLabel.AutoEllipsis = true;
            this._defaultInterpreterLabel.Name = "_defaultInterpreterLabel";
            // 
            // _defaultInterpreter
            // 
            resources.ApplyResources(this._defaultInterpreter, "_defaultInterpreter");
            this._defaultInterpreter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._defaultInterpreter.FormattingEnabled = true;
            this._defaultInterpreter.Name = "_defaultInterpreter";
            this._defaultInterpreter.SelectedIndexChanged += new System.EventHandler(this.Changed);
            this._defaultInterpreter.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.Interpreter_Format);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._applicationGroup, 0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // PythonGeneralPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PythonGeneralPropertyPageControl";
            this._applicationGroup.ResumeLayout(false);
            this._applicationGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _applicationGroup;
        private System.Windows.Forms.Label _startupFileLabel;
        private System.Windows.Forms.TextBox _startupFile;
        private System.Windows.Forms.CheckBox _windowsApplication;
        private System.Windows.Forms.TextBox _workingDirectory;
        private System.Windows.Forms.Label _workingDirLabel;
        private System.Windows.Forms.Label _defaultInterpreterLabel;
        private System.Windows.Forms.ComboBox _defaultInterpreter;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}
