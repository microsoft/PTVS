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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this._unitTestArgsLabel = new System.Windows.Forms.Label();
            this._unitTestArgs = new System.Windows.Forms.TextBox();
            this._testFrameworkLabel = new System.Windows.Forms.Label();
            this._testFramework = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel, 0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // tableLayoutPanel
            // 
            resources.ApplyResources(this.tableLayoutPanel, "tableLayoutPanel");
            this.tableLayoutPanel.Controls.Add(this._unitTestArgsLabel, 0, 1);
            this.tableLayoutPanel.Controls.Add(this._unitTestArgs, 1, 1);
            this.tableLayoutPanel.Controls.Add(this._testFrameworkLabel, 0, 0);
            this.tableLayoutPanel.Controls.Add(this._testFramework, 1, 0);
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            // 
            // _unitTestArgsLabel
            // 
            resources.ApplyResources(this._unitTestArgsLabel, "_unitTestArgsLabel");
            this._unitTestArgsLabel.AutoEllipsis = true;
            this._unitTestArgsLabel.Name = "_unitTestArgsLabel";
            // 
            // _unitTestArgs
            // 
            resources.ApplyResources(this._unitTestArgs, "_unitTestArgs");
            this._unitTestArgs.Name = "_unitTestArgs";
            this._unitTestArgs.TextChanged += new System.EventHandler(this.Changed);
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
            this._testFramework.SelectedIndexChanged += new System.EventHandler(this.TestFrameworkSelectedIndexChanged);             // 
            // PythonTestPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PythonTestPropertyPageControl";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.tableLayoutPanel.ResumeLayout(false);
            this.tableLayoutPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label _unitTestArgsLabel;
        private System.Windows.Forms.TextBox _unitTestArgs;
        private System.Windows.Forms.Label _testFrameworkLabel;
        private System.Windows.Forms.ComboBox _testFramework;
    }
}
