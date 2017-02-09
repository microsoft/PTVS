namespace Microsoft.PythonTools.Project {
    partial class PythonDebugPropertyPageControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonDebugPropertyPageControl));
            this._launchModeLabel = new System.Windows.Forms.Label();
            this._launchModeCombo = new System.Windows.Forms.ComboBox();
            this.tableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayout.SuspendLayout();
            this.SuspendLayout();
            // 
            // _launchModeLabel
            // 
            resources.ApplyResources(this._launchModeLabel, "_launchModeLabel");
            this._launchModeLabel.AutoEllipsis = true;
            this._launchModeLabel.Name = "_launchModeLabel";
            // 
            // _launchModeCombo
            // 
            resources.ApplyResources(this._launchModeCombo, "_launchModeCombo");
            this._launchModeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._launchModeCombo.FormattingEnabled = true;
            this._launchModeCombo.Name = "_launchModeCombo";
            this._launchModeCombo.SelectedIndexChanged += new System.EventHandler(this.LaunchModeComboSelectedIndexChanged);
            this._launchModeCombo.Format += new System.Windows.Forms.ListControlConvertEventHandler(this._launchModeCombo_Format);
            // 
            // tableLayout
            // 
            resources.ApplyResources(this.tableLayout, "tableLayout");
            this.tableLayout.Controls.Add(this._launchModeLabel, 0, 0);
            this.tableLayout.Controls.Add(this._launchModeCombo, 1, 0);
            this.tableLayout.Name = "tableLayout";
            // 
            // PythonDebugPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayout);
            this.DoubleBuffered = true;
            this.Name = "PythonDebugPropertyPageControl";
            this.tableLayout.ResumeLayout(false);
            this.tableLayout.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _launchModeLabel;
        private System.Windows.Forms.ComboBox _launchModeCombo;
        private System.Windows.Forms.TableLayoutPanel tableLayout;
    }
}
