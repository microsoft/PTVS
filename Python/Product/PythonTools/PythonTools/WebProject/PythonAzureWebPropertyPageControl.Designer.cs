namespace Microsoft.PythonTools.Project.Web {
    partial class PythonAzureWebPropertyPageControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonAzureWebPropertyPageControl));
            this._panel = new System.Windows.Forms.TableLayoutPanel();
            this._helpLabel = new System.Windows.Forms.Label();
            this._frameworkLabel = new System.Windows.Forms.Label();
            this._frameworkComboBox = new System.Windows.Forms.ComboBox();
            this._startupCommandLabel = new System.Windows.Forms.Label();
            this._startupCommandTextBox = new System.Windows.Forms.TextBox();
            this._panel.SuspendLayout();
            this.SuspendLayout();
            // 
            // _panel
            // 
            resources.ApplyResources(this._panel, "_panel");
            this._panel.Controls.Add(this._helpLabel, 0, 0);
            this._panel.Controls.Add(this._frameworkLabel, 0, 1);
            this._panel.Controls.Add(this._frameworkComboBox, 0, 3);
            this._panel.Controls.Add(this._startupCommandTextBox, 0, 5);
            this._panel.Controls.Add(this._startupCommandLabel, 0, 4);
            this._panel.Name = "_panel";
            // 
            // _helpLabel
            // 
            resources.ApplyResources(this._helpLabel, "_helpLabel");
            this._panel.SetColumnSpan(this._helpLabel, 2);
            this._helpLabel.Name = "_helpLabel";
            // 
            // _frameworkLabel
            // 
            resources.ApplyResources(this._frameworkLabel, "_frameworkLabel");
            this._frameworkLabel.Name = "_frameworkLabel";
            // 
            // _frameworkComboBox
            // 
            this._frameworkComboBox.FormattingEnabled = true;
            resources.ApplyResources(this._frameworkComboBox, "_frameworkComboBox");
            this._frameworkComboBox.Name = "_frameworkComboBox";
            this._frameworkComboBox.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _startupCommandLabel
            // 
            resources.ApplyResources(this._startupCommandLabel, "_startupCommandLabel");
            this._startupCommandLabel.Name = "_startupCommandLabel";
            // 
            // _startupCommandTextBox
            // 
            resources.ApplyResources(this._startupCommandTextBox, "_startupCommandTextBox");
            this._startupCommandTextBox.Name = "_startupCommandTextBox";
            this._startupCommandTextBox.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // PythonAzureWebPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._panel);
            this.Name = "PythonAzureWebPropertyPageControl";
            this._panel.ResumeLayout(false);
            this._panel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel _panel;
        private System.Windows.Forms.Label _helpLabel;
        private System.Windows.Forms.Label _frameworkLabel;
        private System.Windows.Forms.ComboBox _frameworkComboBox;
        private System.Windows.Forms.TextBox _startupCommandTextBox;
        private System.Windows.Forms.Label _startupCommandLabel;
    }
}
