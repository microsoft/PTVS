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
            this._launchModeLabel = new System.Windows.Forms.Label();
            this._launchModeCombo = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // _launchModeLabel
            // 
            this._launchModeLabel.AutoSize = true;
            this._launchModeLabel.Location = new System.Drawing.Point(8, 15);
            this._launchModeLabel.Name = "_launchModeLabel";
            this._launchModeLabel.Size = new System.Drawing.Size(75, 13);
            this._launchModeLabel.TabIndex = 17;
            this._launchModeLabel.Text = "Launch mode:";
            // 
            // _launchModeCombo
            // 
            this._launchModeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._launchModeCombo.FormattingEnabled = true;
            this._launchModeCombo.Location = new System.Drawing.Point(89, 12);
            this._launchModeCombo.Name = "_launchModeCombo";
            this._launchModeCombo.Size = new System.Drawing.Size(351, 21);
            this._launchModeCombo.TabIndex = 18;
            this._launchModeCombo.SelectedIndexChanged += new System.EventHandler(this.LaunchModeComboSelectedIndexChanged);
            // 
            // PythonDebugPropertyPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._launchModeCombo);
            this.Controls.Add(this._launchModeLabel);
            this.Name = "PythonDebugPropertyPageControl";
            this.Size = new System.Drawing.Size(588, 427);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _launchModeLabel;
        private System.Windows.Forms.ComboBox _launchModeCombo;
    }
}
