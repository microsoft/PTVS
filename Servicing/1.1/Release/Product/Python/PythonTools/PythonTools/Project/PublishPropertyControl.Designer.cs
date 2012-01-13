namespace Microsoft.PythonTools.Project {
    partial class PublishPropertyControl {
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
            this._publishLocationGroupBox = new System.Windows.Forms.GroupBox();
            this._pubNowButton = new System.Windows.Forms.Button();
            this._pubUrl = new System.Windows.Forms.TextBox();
            this._publishLocationLabel = new System.Windows.Forms.Label();
            this._publishLocationGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // _publishLocationGroupBox
            // 
            this._publishLocationGroupBox.Controls.Add(this._pubNowButton);
            this._publishLocationGroupBox.Controls.Add(this._pubUrl);
            this._publishLocationGroupBox.Controls.Add(this._publishLocationLabel);
            this._publishLocationGroupBox.Location = new System.Drawing.Point(4, 4);
            this._publishLocationGroupBox.Name = "_publishLocationGroupBox";
            this._publishLocationGroupBox.Size = new System.Drawing.Size(580, 233);
            this._publishLocationGroupBox.TabIndex = 0;
            this._publishLocationGroupBox.TabStop = false;
            this._publishLocationGroupBox.Text = "Publish Location";
            // 
            // _pubNowButton
            // 
            this._pubNowButton.Location = new System.Drawing.Point(499, 204);
            this._pubNowButton.Name = "_pubNowButton";
            this._pubNowButton.Size = new System.Drawing.Size(75, 23);
            this._pubNowButton.TabIndex = 2;
            this._pubNowButton.Text = "Publish Now";
            this._pubNowButton.UseVisualStyleBackColor = true;
            this._pubNowButton.Click += new System.EventHandler(this._pubNowButton_Click);
            // 
            // _pubUrl
            // 
            this._pubUrl.Location = new System.Drawing.Point(10, 36);
            this._pubUrl.Name = "_pubUrl";
            this._pubUrl.Size = new System.Drawing.Size(564, 20);
            this._pubUrl.TabIndex = 1;
            this._pubUrl.TextChanged += new System.EventHandler(this._pubUrl_TextChanged);
            // 
            // _publishLocationLabel
            // 
            this._publishLocationLabel.AutoSize = true;
            this._publishLocationLabel.Location = new System.Drawing.Point(7, 20);
            this._publishLocationLabel.Name = "_publishLocationLabel";
            this._publishLocationLabel.Size = new System.Drawing.Size(287, 13);
            this._publishLocationLabel.TabIndex = 0;
            this._publishLocationLabel.Text = "Publishing Folder Location (web site, ftp server, or file path):";
            // 
            // PublishPropertyControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._publishLocationGroupBox);
            this.Name = "PublishPropertyControl";
            this.Size = new System.Drawing.Size(601, 483);
            this._publishLocationGroupBox.ResumeLayout(false);
            this._publishLocationGroupBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox _publishLocationGroupBox;
        private System.Windows.Forms.TextBox _pubUrl;
        private System.Windows.Forms.Label _publishLocationLabel;
        private System.Windows.Forms.Button _pubNowButton;
    }
}
