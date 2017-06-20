namespace Microsoft.PythonTools.Options {
    partial class PythonDiagnosticsOptionsControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonDiagnosticsOptionsControl));
            this._copyToClipboard = new System.Windows.Forms.Button();
            this._saveToFile = new System.Windows.Forms.Button();
            this._includeAnalysisLogs = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _copyToClipboard
            // 
            resources.ApplyResources(this._copyToClipboard, "_copyToClipboard");
            this._copyToClipboard.Name = "_copyToClipboard";
            this._copyToClipboard.UseVisualStyleBackColor = true;
            this._copyToClipboard.Click += new System.EventHandler(this._copyToClipboard_Click);
            // 
            // _saveToFile
            // 
            resources.ApplyResources(this._saveToFile, "_saveToFile");
            this._saveToFile.Name = "_saveToFile";
            this._saveToFile.UseVisualStyleBackColor = true;
            this._saveToFile.Click += new System.EventHandler(this._saveToFile_Click);
            // 
            // _includeAnalysisLogs
            // 
            resources.ApplyResources(this._includeAnalysisLogs, "_includeAnalysisLogs");
            this._includeAnalysisLogs.AutoEllipsis = true;
            this._includeAnalysisLogs.Checked = true;
            this._includeAnalysisLogs.CheckState = System.Windows.Forms.CheckState.Checked;
            this._includeAnalysisLogs.Name = "_includeAnalysisLogs";
            this._includeAnalysisLogs.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._includeAnalysisLogs, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._saveToFile, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._copyToClipboard, 0, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // PythonDiagnosticsOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PythonDiagnosticsOptionsControl";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button _copyToClipboard;
        private System.Windows.Forms.Button _saveToFile;
        private System.Windows.Forms.CheckBox _includeAnalysisLogs;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}
