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
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._publishLocationLabel = new System.Windows.Forms.Label();
            this._pubUrl = new System.Windows.Forms.TextBox();
            this._pubNowButton = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._publishLocationGroupBox.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _publishLocationGroupBox
            // 
            this._publishLocationGroupBox.AutoSize = true;
            this._publishLocationGroupBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._publishLocationGroupBox.Controls.Add(this.tableLayoutPanel2);
            this._publishLocationGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._publishLocationGroupBox.Location = new System.Drawing.Point(6, 8);
            this._publishLocationGroupBox.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._publishLocationGroupBox.Name = "_publishLocationGroupBox";
            this._publishLocationGroupBox.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._publishLocationGroupBox.Size = new System.Drawing.Size(341, 113);
            this._publishLocationGroupBox.TabIndex = 0;
            this._publishLocationGroupBox.TabStop = false;
            this._publishLocationGroupBox.Text = "Publish Location";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this._publishLocationLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._pubUrl, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._pubNowButton, 1, 3);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 23);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(329, 82);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // _publishLocationLabel
            // 
            this._publishLocationLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._publishLocationLabel.AutoEllipsis = true;
            this._publishLocationLabel.AutoSize = true;
            this.tableLayoutPanel2.SetColumnSpan(this._publishLocationLabel, 2);
            this._publishLocationLabel.Location = new System.Drawing.Point(6, 3);
            this._publishLocationLabel.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._publishLocationLabel.Name = "_publishLocationLabel";
            this._publishLocationLabel.Size = new System.Drawing.Size(317, 13);
            this._publishLocationLabel.TabIndex = 0;
            this._publishLocationLabel.Text = "Publishing Folder Location (web site, ftp server, or file path):";
            this._publishLocationLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _pubUrl
            // 
            this.tableLayoutPanel2.SetColumnSpan(this._pubUrl, 2);
            this._pubUrl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._pubUrl.Location = new System.Drawing.Point(6, 22);
            this._pubUrl.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._pubUrl.Name = "_pubUrl";
            this._pubUrl.Size = new System.Drawing.Size(317, 22);
            this._pubUrl.TabIndex = 1;
            this._pubUrl.TextChanged += new System.EventHandler(this._pubUrl_TextChanged);
            // 
            // _pubNowButton
            // 
            this._pubNowButton.AutoSize = true;
            this._pubNowButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._pubNowButton.Location = new System.Drawing.Point(217, 50);
            this._pubNowButton.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._pubNowButton.Name = "_pubNowButton";
            this._pubNowButton.Padding = new System.Windows.Forms.Padding(12, 3, 12, 3);
            this._pubNowButton.Size = new System.Drawing.Size(106, 29);
            this._pubNowButton.TabIndex = 2;
            this._pubNowButton.Text = "&Publish Now";
            this._pubNowButton.UseVisualStyleBackColor = true;
            this._pubNowButton.Click += new System.EventHandler(this._pubNowButton_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this._publishLocationGroupBox, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 90F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(353, 149);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // PublishPropertyControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PublishPropertyControl";
            this.Size = new System.Drawing.Size(353, 149);
            this._publishLocationGroupBox.ResumeLayout(false);
            this._publishLocationGroupBox.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _publishLocationGroupBox;
        private System.Windows.Forms.TextBox _pubUrl;
        private System.Windows.Forms.Label _publishLocationLabel;
        private System.Windows.Forms.Button _pubNowButton;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}
