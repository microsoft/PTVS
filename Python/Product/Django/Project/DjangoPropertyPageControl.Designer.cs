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
            this._djangoGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._settingsModule = new System.Windows.Forms.TextBox();
            this._settingsModuleLabel = new System.Windows.Forms.Label();
            this._djangoGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _djangoGroup
            // 
            this._djangoGroup.AutoSize = true;
            this._djangoGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._djangoGroup.Controls.Add(this.tableLayoutPanel2);
            this._djangoGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._djangoGroup.Location = new System.Drawing.Point(6, 8);
            this._djangoGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._djangoGroup.Name = "_djangoGroup";
            this._djangoGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._djangoGroup.Size = new System.Drawing.Size(413, 55);
            this._djangoGroup.TabIndex = 0;
            this._djangoGroup.TabStop = false;
            this._djangoGroup.Text = "Django";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this._settingsModuleLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._settingsModule, 1, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 21);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(401, 26);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this._djangoGroup, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(425, 91);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // _settingsModule
            // 
            this._settingsModule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._settingsModule.Location = new System.Drawing.Point(104, 3);
            this._settingsModule.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._settingsModule.MinimumSize = new System.Drawing.Size(50, 4);
            this._settingsModule.Name = "_settingsModule";
            this._settingsModule.Size = new System.Drawing.Size(291, 20);
            this._settingsModule.TabIndex = 9;
            this._settingsModule.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _settingsModuleLabel
            // 
            this._settingsModuleLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._settingsModuleLabel.AutoSize = true;
            this._settingsModuleLabel.Location = new System.Drawing.Point(6, 6);
            this._settingsModuleLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._settingsModuleLabel.Name = "_settingsModuleLabel";
            this._settingsModuleLabel.Size = new System.Drawing.Size(86, 13);
            this._settingsModuleLabel.TabIndex = 8;
            this._settingsModuleLabel.Text = "Settings &Module:";
            this._settingsModuleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DjangoLauncherOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "DjangoLauncherOptions";
            this.Size = new System.Drawing.Size(425, 91);
            this._djangoGroup.ResumeLayout(false);
            this._djangoGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _djangoGroup;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label _settingsModuleLabel;
        private System.Windows.Forms.TextBox _settingsModule;
    }
}
