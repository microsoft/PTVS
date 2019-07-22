namespace Microsoft.PythonTools.Project {
    partial class PythonTestPropertyPageControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonTestPropertyPageControl));
            this.tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this._unittestPatternLabel = new System.Windows.Forms.Label();
            this._unittestRootDirLabel = new System.Windows.Forms.Label();
            this._unitTestRootDir = new System.Windows.Forms.TextBox();
            this._testFrameworkLabel = new System.Windows.Forms.Label();
            this._testFramework = new System.Windows.Forms.ComboBox();
            this._unitTestPattern = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            resources.ApplyResources(this.tableLayoutPanel, "tableLayoutPanel");
            this.tableLayoutPanel.Controls.Add(this._unittestPatternLabel, 0, 5);
            this.tableLayoutPanel.Controls.Add(this._unittestRootDirLabel, 0, 1);
            this.tableLayoutPanel.Controls.Add(this._unitTestRootDir, 1, 1);
            this.tableLayoutPanel.Controls.Add(this._testFrameworkLabel, 0, 0);
            this.tableLayoutPanel.Controls.Add(this._testFramework, 1, 0);
            this.tableLayoutPanel.Controls.Add(this._unitTestPattern, 1, 5);
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            // 
            // _unittestPatternLabel
            // 
            resources.ApplyResources(this._unittestPatternLabel, "_unittestPatternLabel");
            this._unittestPatternLabel.AutoEllipsis = true;
            this._unittestPatternLabel.Name = "_unittestPatternLabel";
            // 
            // _unittestRootDirLabel
            // 
            resources.ApplyResources(this._unittestRootDirLabel, "_unittestRootDirLabel");
            this._unittestRootDirLabel.AutoEllipsis = true;
            this._unittestRootDirLabel.Name = "_unittestRootDirLabel";
            // 
            // _unitTestRootDir
            // 
            resources.ApplyResources(this._unitTestRootDir, "_unitTestRootDir");
            this._unitTestRootDir.Name = "_unitTestRootDir";
            this._unitTestRootDir.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _testFrameworkLabel
            // 
            resources.ApplyResources(this._testFrameworkLabel, "_testFrameworkLabel");
            this._testFrameworkLabel.AutoEllipsis = true;
            this._testFrameworkLabel.Name = "_testFrameworkLabel";
            // 
            // _testFramework
            // 
            resources.ApplyResources(this._testFramework, "_testFramework");
            this._testFramework.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._testFramework.FormattingEnabled = true;
            this._testFramework.Items.AddRange(new object[] {
            resources.GetString("_testFramework.Items"),
            resources.GetString("_testFramework.Items1"),
            resources.GetString("_testFramework.Items2")});
            this._testFramework.Name = "_testFramework";
            this._testFramework.SelectedIndexChanged += new System.EventHandler(this.TestFrameworkSelectedIndexChanged);
            // 
            // _unitTestPattern
            // 
            resources.ApplyResources(this._unitTestPattern, "_unitTestPattern");
            this._unitTestPattern.Name = "_unitTestPattern";
            this._unitTestPattern.TextChanged += new System.EventHandler(this.Changed);
            // 
            // PythonTestPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel);
            this.Name = "PythonTestPropertyPageControl";
            this.tableLayoutPanel.ResumeLayout(false);
            this.tableLayoutPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label _unittestRootDirLabel;
        private System.Windows.Forms.TextBox _unitTestRootDir;
        private System.Windows.Forms.Label _testFrameworkLabel;
        private System.Windows.Forms.ComboBox _testFramework;
        private System.Windows.Forms.Label _unittestPatternLabel;
        private System.Windows.Forms.TextBox _unitTestPattern;
    }
}
