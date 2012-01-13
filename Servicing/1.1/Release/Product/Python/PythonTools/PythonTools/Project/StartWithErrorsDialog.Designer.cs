namespace Microsoft.PythonTools.Project {
    partial class StartWithErrorsDialog {
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this._yesButton = new System.Windows.Forms.Button();
            this._noButton = new System.Windows.Forms.Button();
            this._dontShowAgainCheckbox = new System.Windows.Forms.CheckBox();
            this._icon = new System.Windows.Forms.PictureBox();
            this._buildErrorsText = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this._icon)).BeginInit();
            this.SuspendLayout();
            // 
            // _yesButton
            // 
            this._yesButton.Location = new System.Drawing.Point(256, 59);
            this._yesButton.Name = "_yesButton";
            this._yesButton.Size = new System.Drawing.Size(75, 23);
            this._yesButton.TabIndex = 0;
            this._yesButton.Text = "&Yes";
            this._yesButton.UseVisualStyleBackColor = true;
            this._yesButton.Click += new System.EventHandler(this.YesButtonClick);
            // 
            // _noButton
            // 
            this._noButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._noButton.Location = new System.Drawing.Point(337, 59);
            this._noButton.Name = "_noButton";
            this._noButton.Size = new System.Drawing.Size(75, 23);
            this._noButton.TabIndex = 1;
            this._noButton.Text = "&No";
            this._noButton.UseVisualStyleBackColor = true;
            this._noButton.Click += new System.EventHandler(this.NoButtonClick);
            // 
            // _dontShowAgainCheckbox
            // 
            this._dontShowAgainCheckbox.AutoSize = true;
            this._dontShowAgainCheckbox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._dontShowAgainCheckbox.Location = new System.Drawing.Point(12, 83);
            this._dontShowAgainCheckbox.Name = "_dontShowAgainCheckbox";
            this._dontShowAgainCheckbox.Size = new System.Drawing.Size(183, 19);
            this._dontShowAgainCheckbox.TabIndex = 2;
            this._dontShowAgainCheckbox.Text = "&Do not show this dialog again";
            this._dontShowAgainCheckbox.UseVisualStyleBackColor = true;
            // 
            // _icon
            // 
            this._icon.Location = new System.Drawing.Point(12, 12);
            this._icon.Name = "_icon";
            this._icon.Size = new System.Drawing.Size(41, 43);
            this._icon.TabIndex = 4;
            this._icon.TabStop = false;
            // 
            // _buildErrorsText
            // 
            this._buildErrorsText.BackColor = System.Drawing.SystemColors.Control;
            this._buildErrorsText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._buildErrorsText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._buildErrorsText.Location = new System.Drawing.Point(59, 12);
            this._buildErrorsText.Multiline = true;
            this._buildErrorsText.Name = "_buildErrorsText";
            this._buildErrorsText.Size = new System.Drawing.Size(342, 33);
            this._buildErrorsText.TabIndex = 5;
            this._buildErrorsText.Text = "One or more files in your project contain errors.  Do you want to launch anyway?";
            // 
            // StartWithErrorsDialog
            // 
            this.AcceptButton = this._yesButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._noButton;
            this.ClientSize = new System.Drawing.Size(424, 112);
            this.Controls.Add(this._buildErrorsText);
            this.Controls.Add(this._icon);
            this.Controls.Add(this._dontShowAgainCheckbox);
            this.Controls.Add(this._noButton);
            this.Controls.Add(this._yesButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StartWithErrorsDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Launch with errors?";
            ((System.ComponentModel.ISupportInitialize)(this._icon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button _yesButton;
        private System.Windows.Forms.Button _noButton;
        private System.Windows.Forms.CheckBox _dontShowAgainCheckbox;
        private System.Windows.Forms.PictureBox _icon;
        private System.Windows.Forms.TextBox _buildErrorsText;
    }
}