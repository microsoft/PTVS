namespace Microsoft.PythonTools.Options {
    partial class PythonGeneralOptionsControl {
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
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this._showOutputWindowForVirtualEnvCreate = new System.Windows.Forms.CheckBox();
            this._showOutputWindowForPackageInstallation = new System.Windows.Forms.CheckBox();
            this._autoAnalysis = new System.Windows.Forms.CheckBox();
            this._updateSearchPathsForLinkedFiles = new System.Windows.Forms.CheckBox();
            this._indentationInconsistentLabel = new System.Windows.Forms.Label();
            this._indentationInconsistentCombo = new System.Windows.Forms.ComboBox();
            this._surveyNewsCheckLabel = new System.Windows.Forms.Label();
            this._surveyNewsCheckCombo = new System.Windows.Forms.ComboBox();
            this._elevatePip = new System.Windows.Forms.CheckBox();
            this._elevateEasyInstall = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.AutoSize = true;
            this.tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel3.ColumnCount = 2;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.Controls.Add(this._showOutputWindowForVirtualEnvCreate, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._showOutputWindowForPackageInstallation, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._autoAnalysis, 0, 4);
            this.tableLayoutPanel3.Controls.Add(this._updateSearchPathsForLinkedFiles, 0, 5);
            this.tableLayoutPanel3.Controls.Add(this._indentationInconsistentLabel, 0, 6);
            this.tableLayoutPanel3.Controls.Add(this._indentationInconsistentCombo, 1, 6);
            this.tableLayoutPanel3.Controls.Add(this._surveyNewsCheckLabel, 0, 7);
            this.tableLayoutPanel3.Controls.Add(this._surveyNewsCheckCombo, 1, 7);
            this.tableLayoutPanel3.Controls.Add(this._elevatePip, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this._elevateEasyInstall, 0, 3);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 9;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(381, 290);
            this.tableLayoutPanel3.TabIndex = 0;
            // 
            // _showOutputWindowForVirtualEnvCreate
            // 
            this._showOutputWindowForVirtualEnvCreate.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._showOutputWindowForVirtualEnvCreate, 2);
            this._showOutputWindowForVirtualEnvCreate.Location = new System.Drawing.Point(6, 3);
            this._showOutputWindowForVirtualEnvCreate.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._showOutputWindowForVirtualEnvCreate.Name = "_showOutputWindowForVirtualEnvCreate";
            this._showOutputWindowForVirtualEnvCreate.Size = new System.Drawing.Size(294, 17);
            this._showOutputWindowForVirtualEnvCreate.TabIndex = 0;
            this._showOutputWindowForVirtualEnvCreate.Text = "Show Output window when creating &virtual environments";
            this._showOutputWindowForVirtualEnvCreate.UseVisualStyleBackColor = true;
            this._showOutputWindowForVirtualEnvCreate.CheckedChanged += new System.EventHandler(this._showOutputWindowForVirtualEnvCreate_CheckedChanged);
            // 
            // _showOutputWindowForPackageInstallation
            // 
            this._showOutputWindowForPackageInstallation.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._showOutputWindowForPackageInstallation, 2);
            this._showOutputWindowForPackageInstallation.Location = new System.Drawing.Point(6, 26);
            this._showOutputWindowForPackageInstallation.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._showOutputWindowForPackageInstallation.Name = "_showOutputWindowForPackageInstallation";
            this._showOutputWindowForPackageInstallation.Size = new System.Drawing.Size(307, 17);
            this._showOutputWindowForPackageInstallation.TabIndex = 1;
            this._showOutputWindowForPackageInstallation.Text = "Show Output window when &installing or removing packages";
            this._showOutputWindowForPackageInstallation.UseVisualStyleBackColor = true;
            this._showOutputWindowForPackageInstallation.CheckedChanged += new System.EventHandler(this._showOutputWindowForPackageInstallation_CheckedChanged);
            // 
            // _autoAnalysis
            // 
            this._autoAnalysis.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._autoAnalysis.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._autoAnalysis, 2);
            this._autoAnalysis.Location = new System.Drawing.Point(6, 95);
            this._autoAnalysis.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._autoAnalysis.Name = "_autoAnalysis";
            this._autoAnalysis.Size = new System.Drawing.Size(272, 17);
            this._autoAnalysis.TabIndex = 2;
            this._autoAnalysis.Text = "&Automatically analyze standard library in background";
            this._autoAnalysis.UseVisualStyleBackColor = true;
            this._autoAnalysis.CheckedChanged += new System.EventHandler(this._autoAnalysis_CheckedChanged);
            // 
            // _updateSearchPathsForLinkedFiles
            // 
            this._updateSearchPathsForLinkedFiles.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._updateSearchPathsForLinkedFiles.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._updateSearchPathsForLinkedFiles, 2);
            this._updateSearchPathsForLinkedFiles.Location = new System.Drawing.Point(6, 118);
            this._updateSearchPathsForLinkedFiles.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._updateSearchPathsForLinkedFiles.Name = "_updateSearchPathsForLinkedFiles";
            this._updateSearchPathsForLinkedFiles.Size = new System.Drawing.Size(241, 17);
            this._updateSearchPathsForLinkedFiles.TabIndex = 3;
            this._updateSearchPathsForLinkedFiles.Text = "&Update search paths when adding linked files";
            this._updateSearchPathsForLinkedFiles.UseVisualStyleBackColor = true;
            this._updateSearchPathsForLinkedFiles.CheckedChanged += new System.EventHandler(this._updateSearchPathsForLinkedFiles_CheckedChanged);
            // 
            // _indentationInconsistentLabel
            // 
            this._indentationInconsistentLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._indentationInconsistentLabel.AutoEllipsis = true;
            this._indentationInconsistentLabel.AutoSize = true;
            this._indentationInconsistentLabel.Location = new System.Drawing.Point(6, 145);
            this._indentationInconsistentLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._indentationInconsistentLabel.Name = "_indentationInconsistentLabel";
            this._indentationInconsistentLabel.Size = new System.Drawing.Size(167, 13);
            this._indentationInconsistentLabel.TabIndex = 4;
            this._indentationInconsistentLabel.Text = "&Report inconsistent indentation as";
            this._indentationInconsistentLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _indentationInconsistentCombo
            // 
            this._indentationInconsistentCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._indentationInconsistentCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._indentationInconsistentCombo.FormattingEnabled = true;
            this._indentationInconsistentCombo.Items.AddRange(new object[] {
            "Errors",
            "Warnings",
            "Don\'t"});
            this._indentationInconsistentCombo.Location = new System.Drawing.Point(185, 141);
            this._indentationInconsistentCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._indentationInconsistentCombo.Name = "_indentationInconsistentCombo";
            this._indentationInconsistentCombo.Size = new System.Drawing.Size(190, 21);
            this._indentationInconsistentCombo.TabIndex = 5;
            this._indentationInconsistentCombo.SelectedIndexChanged += new System.EventHandler(this._indentationInconsistentCombo_SelectedIndexChanged);
            // 
            // _surveyNewsCheckLabel
            // 
            this._surveyNewsCheckLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._surveyNewsCheckLabel.AutoSize = true;
            this._surveyNewsCheckLabel.Location = new System.Drawing.Point(6, 172);
            this._surveyNewsCheckLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._surveyNewsCheckLabel.Name = "_surveyNewsCheckLabel";
            this._surveyNewsCheckLabel.Size = new System.Drawing.Size(117, 13);
            this._surveyNewsCheckLabel.TabIndex = 6;
            this._surveyNewsCheckLabel.Text = "&Check for survey/news";
            this._surveyNewsCheckLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _surveyNewsCheckCombo
            // 
            this._surveyNewsCheckCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._surveyNewsCheckCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._surveyNewsCheckCombo.DropDownWidth = 172;
            this._surveyNewsCheckCombo.FormattingEnabled = true;
            this._surveyNewsCheckCombo.Items.AddRange(new object[] {
            "Never",
            "Once a day",
            "Once a week",
            "Once a month"});
            this._surveyNewsCheckCombo.Location = new System.Drawing.Point(185, 168);
            this._surveyNewsCheckCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._surveyNewsCheckCombo.Name = "_surveyNewsCheckCombo";
            this._surveyNewsCheckCombo.Size = new System.Drawing.Size(190, 21);
            this._surveyNewsCheckCombo.TabIndex = 7;
            this._surveyNewsCheckCombo.SelectedIndexChanged += new System.EventHandler(this._surveyNewsCheckCombo_SelectedIndexChanged);
            // 
            // _elevatePip
            // 
            this._elevatePip.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._elevatePip, 2);
            this._elevatePip.Location = new System.Drawing.Point(6, 49);
            this._elevatePip.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._elevatePip.Name = "_elevatePip";
            this._elevatePip.Size = new System.Drawing.Size(170, 17);
            this._elevatePip.TabIndex = 0;
            this._elevatePip.Text = "Always run &pip as administrator";
            this._elevatePip.UseVisualStyleBackColor = true;
            this._elevatePip.CheckedChanged += new System.EventHandler(this._elevatePip_CheckedChanged);
            // 
            // _elevateEasyInstall
            // 
            this._elevateEasyInstall.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._elevateEasyInstall, 2);
            this._elevateEasyInstall.Location = new System.Drawing.Point(6, 72);
            this._elevateEasyInstall.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._elevateEasyInstall.Name = "_elevateEasyInstall";
            this._elevateEasyInstall.Size = new System.Drawing.Size(210, 17);
            this._elevateEasyInstall.TabIndex = 0;
            this._elevateEasyInstall.Text = "Always run &easy_install as administrator";
            this._elevateEasyInstall.UseVisualStyleBackColor = true;
            this._elevateEasyInstall.CheckedChanged += new System.EventHandler(this._elevateEasyInstall_CheckedChanged);
            // 
            // PythonGeneralOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel3);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonGeneralOptionsControl";
            this.Size = new System.Drawing.Size(381, 290);
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.Label _surveyNewsCheckLabel;
        private System.Windows.Forms.ComboBox _surveyNewsCheckCombo;
        private System.Windows.Forms.CheckBox _showOutputWindowForVirtualEnvCreate;
        private System.Windows.Forms.CheckBox _showOutputWindowForPackageInstallation;
        private System.Windows.Forms.CheckBox _autoAnalysis;
        private System.Windows.Forms.CheckBox _updateSearchPathsForLinkedFiles;
        private System.Windows.Forms.Label _indentationInconsistentLabel;
        private System.Windows.Forms.ComboBox _indentationInconsistentCombo;
        private System.Windows.Forms.CheckBox _elevatePip;
        private System.Windows.Forms.CheckBox _elevateEasyInstall;
    }
}
