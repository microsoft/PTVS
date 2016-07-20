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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._buildErrorsText = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._icon)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _yesButton
            // 
            this._yesButton.AutoSize = true;
            this._yesButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._yesButton.Location = new System.Drawing.Point(198, 55);
            this._yesButton.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._yesButton.MinimumSize = new System.Drawing.Size(86, 24);
            this._yesButton.Name = "_yesButton";
            this._yesButton.Size = new System.Drawing.Size(86, 24);
            this._yesButton.TabIndex = 1;
            this._yesButton.Text = "&Yes";
            this._yesButton.UseVisualStyleBackColor = true;
            this._yesButton.Click += new System.EventHandler(this.YesButtonClick);
            // 
            // _noButton
            // 
            this._noButton.AutoSize = true;
            this._noButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._noButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._noButton.Location = new System.Drawing.Point(296, 55);
            this._noButton.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._noButton.MinimumSize = new System.Drawing.Size(86, 24);
            this._noButton.Name = "_noButton";
            this._noButton.Size = new System.Drawing.Size(86, 24);
            this._noButton.TabIndex = 2;
            this._noButton.Text = "&No";
            this._noButton.UseVisualStyleBackColor = true;
            this._noButton.Click += new System.EventHandler(this.NoButtonClick);
            // 
            // _dontShowAgainCheckbox
            // 
            this._dontShowAgainCheckbox.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this._dontShowAgainCheckbox, 4);
            this._dontShowAgainCheckbox.Location = new System.Drawing.Point(6, 85);
            this._dontShowAgainCheckbox.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._dontShowAgainCheckbox.Name = "_dontShowAgainCheckbox";
            this._dontShowAgainCheckbox.Size = new System.Drawing.Size(165, 17);
            this._dontShowAgainCheckbox.TabIndex = 3;
            this._dontShowAgainCheckbox.Text = "&Do not show this dialog again";
            this._dontShowAgainCheckbox.UseVisualStyleBackColor = true;
            // 
            // _icon
            // 
            this._icon.Location = new System.Drawing.Point(6, 8);
            this._icon.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._icon.Name = "_icon";
            this.tableLayoutPanel1.SetRowSpan(this._icon, 2);
            this._icon.Size = new System.Drawing.Size(68, 37);
            this._icon.TabIndex = 4;
            this._icon.TabStop = false;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this._icon, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._buildErrorsText, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this._yesButton, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this._noButton, 3, 1);
            this.tableLayoutPanel1.Controls.Add(this._dontShowAgainCheckbox, 0, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(388, 105);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // _buildErrorsText
            // 
            this.tableLayoutPanel1.SetColumnSpan(this._buildErrorsText, 3);
            this._buildErrorsText.Dock = System.Windows.Forms.DockStyle.Fill;
            this._buildErrorsText.Location = new System.Drawing.Point(86, 3);
            this._buildErrorsText.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._buildErrorsText.Name = "_buildErrorsText";
            this._buildErrorsText.Size = new System.Drawing.Size(296, 46);
            this._buildErrorsText.TabIndex = 5;
            this._buildErrorsText.Text = "One or more files in your project contain errors. Do you want to launch anyway?";
            this._buildErrorsText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // StartWithErrorsDialog
            // 
            this.AcceptButton = this._yesButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._noButton;
            this.ClientSize = new System.Drawing.Size(388, 105);
            this.Controls.Add(this.tableLayoutPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StartWithErrorsDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Launch with errors?";
            ((System.ComponentModel.ISupportInitialize)(this._icon)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button _yesButton;
        private System.Windows.Forms.Button _noButton;
        private System.Windows.Forms.CheckBox _dontShowAgainCheckbox;
        private System.Windows.Forms.PictureBox _icon;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label _buildErrorsText;
    }
}