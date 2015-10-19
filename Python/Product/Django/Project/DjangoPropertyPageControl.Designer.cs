namespace Microsoft.PythonTools.Django.Project {
    partial class DjangoPropertyPageControl {
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            this._djangoGroup = new System.Windows.Forms.GroupBox();
            this._settingsModuleLabel = new System.Windows.Forms.Label();
            this._settingsModule = new System.Windows.Forms.TextBox();
            this._staticUriLabel = new System.Windows.Forms.Label();
            this._staticUri = new System.Windows.Forms.TextBox();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._djangoGroup.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _djangoGroup
            // 
            this._djangoGroup.AutoSize = true;
            this._djangoGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._djangoGroup.Controls.Add(tableLayoutPanel2);
            this._djangoGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._djangoGroup.Location = new System.Drawing.Point(6, 8);
            this._djangoGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._djangoGroup.Name = "_djangoGroup";
            this._djangoGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._djangoGroup.Size = new System.Drawing.Size(423, 81);
            this._djangoGroup.TabIndex = 0;
            this._djangoGroup.TabStop = false;
            this._djangoGroup.Text = "Django";
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.AutoSize = true;
            tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel2.ColumnCount = 2;
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel2.Controls.Add(this._settingsModuleLabel, 0, 0);
            tableLayoutPanel2.Controls.Add(this._settingsModule, 1, 0);
            tableLayoutPanel2.Controls.Add(this._staticUriLabel, 0, 1);
            tableLayoutPanel2.Controls.Add(this._staticUri, 1, 1);
            tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel2.Location = new System.Drawing.Point(6, 21);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 2;
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.Size = new System.Drawing.Size(411, 52);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // _settingsModuleLabel
            // 
            this._settingsModuleLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._settingsModuleLabel.AutoSize = true;
            this._settingsModuleLabel.Location = new System.Drawing.Point(6, 6);
            this._settingsModuleLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._settingsModuleLabel.Name = "_settingsModuleLabel";
            this._settingsModuleLabel.Size = new System.Drawing.Size(86, 13);
            this._settingsModuleLabel.TabIndex = 0;
            this._settingsModuleLabel.Text = "Settings &Module:";
            this._settingsModuleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _settingsModule
            // 
            this._settingsModule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._settingsModule.Location = new System.Drawing.Point(114, 3);
            this._settingsModule.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._settingsModule.MinimumSize = new System.Drawing.Size(50, 4);
            this._settingsModule.Name = "_settingsModule";
            this._settingsModule.Size = new System.Drawing.Size(291, 20);
            this._settingsModule.TabIndex = 1;
            this._settingsModule.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _staticUriLabel
            // 
            this._staticUriLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._staticUriLabel.AutoSize = true;
            this._staticUriLabel.Location = new System.Drawing.Point(6, 32);
            this._staticUriLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._staticUriLabel.Name = "_staticUriLabel";
            this._staticUriLabel.Size = new System.Drawing.Size(96, 13);
            this._staticUriLabel.TabIndex = 2;
            this._staticUriLabel.Text = "&Static URI Pattern:";
            this._staticUriLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _staticUri
            // 
            this._staticUri.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._staticUri.Location = new System.Drawing.Point(114, 29);
            this._staticUri.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._staticUri.MinimumSize = new System.Drawing.Size(50, 4);
            this._staticUri.Name = "_staticUri";
            this._staticUri.Size = new System.Drawing.Size(291, 20);
            this._staticUri.TabIndex = 3;
            this._staticUri.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(this._djangoGroup, 0, 0);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel1.Size = new System.Drawing.Size(435, 117);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // DjangoPropertyPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "DjangoPropertyPageControl";
            this.Size = new System.Drawing.Size(435, 117);
            this._djangoGroup.ResumeLayout(false);
            this._djangoGroup.PerformLayout();
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _djangoGroup;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Label _settingsModuleLabel;
        private System.Windows.Forms.TextBox _settingsModule;
        private System.Windows.Forms.Label _staticUriLabel;
        private System.Windows.Forms.TextBox _staticUri;
    }
}
