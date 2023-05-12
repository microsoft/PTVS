namespace Microsoft.PythonTools.Options {
    partial class PythonGeneralOptionsControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonGeneralOptionsControl));
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this._showOutputWindowForVirtualEnvCreate = new System.Windows.Forms.CheckBox();
            this._showOutputWindowForPackageInstallation = new System.Windows.Forms.CheckBox();
            this._promptForEnvCreate = new System.Windows.Forms.CheckBox();
            this._promptForPackageInstallation = new System.Windows.Forms.CheckBox();
            this._promptForPytestEnableAndInstall = new System.Windows.Forms.CheckBox();
            this._elevatePip = new System.Windows.Forms.CheckBox();
            // Disable until Pylance supports: this._clearGlobalPythonPath = new System.Windows.Forms.CheckBox();
            this._resetSuppressDialog = new System.Windows.Forms.Button();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel3
            // 
            resources.ApplyResources(this.tableLayoutPanel3, "tableLayoutPanel3");
            this.tableLayoutPanel3.Controls.Add(this._showOutputWindowForVirtualEnvCreate, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._showOutputWindowForPackageInstallation, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._promptForEnvCreate, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this._promptForPackageInstallation, 0, 3);
            this.tableLayoutPanel3.Controls.Add(this._promptForPytestEnableAndInstall, 0, 4);
            this.tableLayoutPanel3.Controls.Add(this._elevatePip, 0, 5);
            //this.tableLayoutPanel3.Controls.Add(this._clearGlobalPythonPath, 0, 6);
            this.tableLayoutPanel3.Controls.Add(this._resetSuppressDialog, 0, 11);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            // 
            // _showOutputWindowForVirtualEnvCreate
            // 
            resources.ApplyResources(this._showOutputWindowForVirtualEnvCreate, "_showOutputWindowForVirtualEnvCreate");
            this.tableLayoutPanel3.SetColumnSpan(this._showOutputWindowForVirtualEnvCreate, 2);
            this._showOutputWindowForVirtualEnvCreate.Name = "_showOutputWindowForVirtualEnvCreate";
            this._showOutputWindowForVirtualEnvCreate.UseVisualStyleBackColor = true;
            // 
            // _showOutputWindowForPackageInstallation
            // 
            resources.ApplyResources(this._showOutputWindowForPackageInstallation, "_showOutputWindowForPackageInstallation");
            this.tableLayoutPanel3.SetColumnSpan(this._showOutputWindowForPackageInstallation, 2);
            this._showOutputWindowForPackageInstallation.Name = "_showOutputWindowForPackageInstallation";
            this._showOutputWindowForPackageInstallation.UseVisualStyleBackColor = true;
            // 
            // _promptForEnvCreate
            // 
            resources.ApplyResources(this._promptForEnvCreate, "_promptForEnvCreate");
            this.tableLayoutPanel3.SetColumnSpan(this._promptForEnvCreate, 2);
            this._promptForEnvCreate.Name = "_promptForEnvCreate";
            this._promptForEnvCreate.UseVisualStyleBackColor = true;
            // 
            // _promptForPackageInstallation
            // 
            resources.ApplyResources(this._promptForPackageInstallation, "_promptForPackageInstallation");
            this.tableLayoutPanel3.SetColumnSpan(this._promptForPackageInstallation, 2);
            this._promptForPackageInstallation.Name = "_promptForPackageInstallation";
            this._promptForPackageInstallation.UseVisualStyleBackColor = true;
            // 
            // _promptForPytestEnableAndInstall
            // 
            resources.ApplyResources(this._promptForPytestEnableAndInstall, "_promptForPytestEnableAndInstall");
            this.tableLayoutPanel3.SetColumnSpan(this._promptForPytestEnableAndInstall, 2);
            this._promptForPytestEnableAndInstall.Name = "_promptForPytestEnableAndInstall";
            this._promptForPytestEnableAndInstall.UseVisualStyleBackColor = true;
            // 
            // _elevatePip
            // 
            resources.ApplyResources(this._elevatePip, "_elevatePip");
            this.tableLayoutPanel3.SetColumnSpan(this._elevatePip, 2);
            this._elevatePip.Name = "_elevatePip";
            this._elevatePip.UseVisualStyleBackColor = true;
            // 
            // _clearGlobalPythonPath
            // 
        /*    resources.ApplyResources(this._clearGlobalPythonPath, "_clearGlobalPythonPath");
            this._clearGlobalPythonPath.AutoEllipsis = true;
            this.tableLayoutPanel3.SetColumnSpan(this._clearGlobalPythonPath, 2);
            this._clearGlobalPythonPath.Name = "_clearGlobalPythonPath";
            this._clearGlobalPythonPath.UseVisualStyleBackColor = true;*/
            // 
            // _resetSuppressDialog
            // 
            resources.ApplyResources(this._resetSuppressDialog, "_resetSuppressDialog");
            this.tableLayoutPanel3.SetColumnSpan(this._resetSuppressDialog, 2);
            this._resetSuppressDialog.Name = "_resetSuppressDialog";
            this._resetSuppressDialog.UseVisualStyleBackColor = true;
            this._resetSuppressDialog.Click += new System.EventHandler(this._resetSuppressDialog_Click);
            // 
            // PythonGeneralOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel3);
            this.Name = "PythonGeneralOptionsControl";
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.CheckBox _showOutputWindowForVirtualEnvCreate;
        private System.Windows.Forms.CheckBox _showOutputWindowForPackageInstallation;
        private System.Windows.Forms.CheckBox _promptForEnvCreate;
        private System.Windows.Forms.CheckBox _promptForPackageInstallation;
        private System.Windows.Forms.CheckBox _promptForPytestEnableAndInstall;
        private System.Windows.Forms.CheckBox _elevatePip;
        //private System.Windows.Forms.CheckBox _clearGlobalPythonPath;
        private System.Windows.Forms.Button _resetSuppressDialog;
    }
}
