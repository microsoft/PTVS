namespace Microsoft.PythonTools.Options {
    partial class PythonAdvancedOptionsControl {
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
            this._promptOnBuildError = new System.Windows.Forms.CheckBox();
            this._indentationInconsistentCombo = new System.Windows.Forms.ComboBox();
            this._indentationInconsistentLabel = new System.Windows.Forms.Label();
            this._waitOnAbnormalExit = new System.Windows.Forms.CheckBox();
            this._autoAnalysis = new System.Windows.Forms.CheckBox();
            this._waitOnNormalExit = new System.Windows.Forms.CheckBox();
            this._teeStdOut = new System.Windows.Forms.CheckBox();
            this._breakOnSystemExitZero = new System.Windows.Forms.CheckBox();
            this._debuggingGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._debugStdLib = new System.Windows.Forms.CheckBox();
            this._miscOptions = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this._surveyNewsCheckLabel = new System.Windows.Forms.Label();
            this._updateSearchPathsForLinkedFiles = new System.Windows.Forms.CheckBox();
            this._surveyNewsCheckCombo = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._debuggingGroupBox.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this._miscOptions.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _promptOnBuildError
            // 
            this._promptOnBuildError.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._promptOnBuildError.AutoSize = true;
            this._promptOnBuildError.Location = new System.Drawing.Point(6, 3);
            this._promptOnBuildError.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._promptOnBuildError.Name = "_promptOnBuildError";
            this._promptOnBuildError.Size = new System.Drawing.Size(244, 17);
            this._promptOnBuildError.TabIndex = 0;
            this._promptOnBuildError.Text = "&Prompt before running when errors are present";
            this._promptOnBuildError.UseVisualStyleBackColor = true;
            this._promptOnBuildError.CheckedChanged += new System.EventHandler(this._promptOnBuildError_CheckedChanged);
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
            this._indentationInconsistentCombo.Location = new System.Drawing.Point(185, 49);
            this._indentationInconsistentCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._indentationInconsistentCombo.Name = "_indentationInconsistentCombo";
            this._indentationInconsistentCombo.Size = new System.Drawing.Size(172, 21);
            this._indentationInconsistentCombo.TabIndex = 3;
            this._indentationInconsistentCombo.SelectedIndexChanged += new System.EventHandler(this._indentationInconsistentCombo_SelectedIndexChanged);
            // 
            // _indentationInconsistentLabel
            // 
            this._indentationInconsistentLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._indentationInconsistentLabel.AutoEllipsis = true;
            this._indentationInconsistentLabel.AutoSize = true;
            this._indentationInconsistentLabel.Location = new System.Drawing.Point(6, 53);
            this._indentationInconsistentLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._indentationInconsistentLabel.Name = "_indentationInconsistentLabel";
            this._indentationInconsistentLabel.Size = new System.Drawing.Size(167, 13);
            this._indentationInconsistentLabel.TabIndex = 2;
            this._indentationInconsistentLabel.Text = "&Report inconsistent indentation as";
            this._indentationInconsistentLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _waitOnAbnormalExit
            // 
            this._waitOnAbnormalExit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._waitOnAbnormalExit.AutoSize = true;
            this._waitOnAbnormalExit.Location = new System.Drawing.Point(6, 26);
            this._waitOnAbnormalExit.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._waitOnAbnormalExit.Name = "_waitOnAbnormalExit";
            this._waitOnAbnormalExit.Size = new System.Drawing.Size(235, 17);
            this._waitOnAbnormalExit.TabIndex = 1;
            this._waitOnAbnormalExit.Text = "&Wait for input when process exits abnormally";
            this._waitOnAbnormalExit.UseVisualStyleBackColor = true;
            this._waitOnAbnormalExit.CheckedChanged += new System.EventHandler(this._waitOnExit_CheckedChanged);
            // 
            // _autoAnalysis
            // 
            this._autoAnalysis.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._autoAnalysis.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._autoAnalysis, 2);
            this._autoAnalysis.Location = new System.Drawing.Point(6, 3);
            this._autoAnalysis.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._autoAnalysis.Name = "_autoAnalysis";
            this._autoAnalysis.Size = new System.Drawing.Size(272, 17);
            this._autoAnalysis.TabIndex = 0;
            this._autoAnalysis.Text = "&Automatically analyze standard library in background";
            this._autoAnalysis.UseVisualStyleBackColor = true;
            this._autoAnalysis.CheckedChanged += new System.EventHandler(this._autoAnalysis_CheckedChanged);
            // 
            // _waitOnNormalExit
            // 
            this._waitOnNormalExit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._waitOnNormalExit.AutoSize = true;
            this._waitOnNormalExit.Location = new System.Drawing.Point(6, 49);
            this._waitOnNormalExit.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._waitOnNormalExit.Name = "_waitOnNormalExit";
            this._waitOnNormalExit.Size = new System.Drawing.Size(223, 17);
            this._waitOnNormalExit.TabIndex = 2;
            this._waitOnNormalExit.Text = "Wai&t for input when process exits normally";
            this._waitOnNormalExit.UseVisualStyleBackColor = true;
            this._waitOnNormalExit.CheckedChanged += new System.EventHandler(this._waitOnNormalExit_CheckedChanged);
            // 
            // _teeStdOut
            // 
            this._teeStdOut.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._teeStdOut.AutoSize = true;
            this._teeStdOut.Location = new System.Drawing.Point(6, 72);
            this._teeStdOut.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._teeStdOut.Name = "_teeStdOut";
            this._teeStdOut.Size = new System.Drawing.Size(240, 17);
            this._teeStdOut.TabIndex = 3;
            this._teeStdOut.Text = "Tee program output to &Debug Output window";
            this._teeStdOut.UseVisualStyleBackColor = true;
            this._teeStdOut.CheckedChanged += new System.EventHandler(this._redirectOutputToVs_CheckedChanged);
            // 
            // _breakOnSystemExitZero
            // 
            this._breakOnSystemExitZero.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._breakOnSystemExitZero.AutoSize = true;
            this._breakOnSystemExitZero.Location = new System.Drawing.Point(6, 95);
            this._breakOnSystemExitZero.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._breakOnSystemExitZero.Name = "_breakOnSystemExitZero";
            this._breakOnSystemExitZero.Size = new System.Drawing.Size(275, 17);
            this._breakOnSystemExitZero.TabIndex = 4;
            this._breakOnSystemExitZero.Text = "Break on SystemExit exception with exit code of &zero";
            this._breakOnSystemExitZero.UseVisualStyleBackColor = true;
            this._breakOnSystemExitZero.CheckedChanged += new System.EventHandler(this._breakOnSystemExitZero_CheckedChanged);
            // 
            // _debuggingGroupBox
            // 
            this._debuggingGroupBox.AutoSize = true;
            this._debuggingGroupBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._debuggingGroupBox.Controls.Add(this.tableLayoutPanel2);
            this._debuggingGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._debuggingGroupBox.Location = new System.Drawing.Point(6, 3);
            this._debuggingGroupBox.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._debuggingGroupBox.Name = "_debuggingGroupBox";
            this._debuggingGroupBox.Size = new System.Drawing.Size(369, 157);
            this._debuggingGroupBox.TabIndex = 0;
            this._debuggingGroupBox.TabStop = false;
            this._debuggingGroupBox.Text = "Debugging";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this._promptOnBuildError, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._waitOnAbnormalExit, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._waitOnNormalExit, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._teeStdOut, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._breakOnSystemExitZero, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this._debugStdLib, 0, 5);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 6;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(363, 138);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // _debugStdLib
            // 
            this._debugStdLib.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._debugStdLib.AutoSize = true;
            this._debugStdLib.Location = new System.Drawing.Point(6, 118);
            this._debugStdLib.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._debugStdLib.Name = "_debugStdLib";
            this._debugStdLib.Size = new System.Drawing.Size(252, 17);
            this._debugStdLib.TabIndex = 5;
            this._debugStdLib.Text = "Enable debugging of the Python standard &library";
            this._debugStdLib.UseVisualStyleBackColor = true;
            this._debugStdLib.CheckedChanged += new System.EventHandler(this._debugStdLib_CheckedChanged);
            // 
            // _miscOptions
            // 
            this._miscOptions.AutoSize = true;
            this._miscOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._miscOptions.Controls.Add(this.tableLayoutPanel3);
            this._miscOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            this._miscOptions.Location = new System.Drawing.Point(6, 166);
            this._miscOptions.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._miscOptions.Name = "_miscOptions";
            this._miscOptions.Size = new System.Drawing.Size(369, 119);
            this._miscOptions.TabIndex = 1;
            this._miscOptions.TabStop = false;
            this._miscOptions.Text = "Miscellaneous";
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.AutoSize = true;
            this.tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel3.ColumnCount = 2;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.Controls.Add(this._surveyNewsCheckLabel, 0, 3);
            this.tableLayoutPanel3.Controls.Add(this._autoAnalysis, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._updateSearchPathsForLinkedFiles, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._indentationInconsistentLabel, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this._indentationInconsistentCombo, 1, 2);
            this.tableLayoutPanel3.Controls.Add(this._surveyNewsCheckCombo, 1, 3);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 4;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.Size = new System.Drawing.Size(363, 100);
            this.tableLayoutPanel3.TabIndex = 0;
            // 
            // _surveyNewsCheckLabel
            // 
            this._surveyNewsCheckLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._surveyNewsCheckLabel.AutoSize = true;
            this._surveyNewsCheckLabel.Location = new System.Drawing.Point(6, 80);
            this._surveyNewsCheckLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._surveyNewsCheckLabel.Name = "_surveyNewsCheckLabel";
            this._surveyNewsCheckLabel.Size = new System.Drawing.Size(117, 13);
            this._surveyNewsCheckLabel.TabIndex = 4;
            this._surveyNewsCheckLabel.Text = "&Check for survey/news";
            this._surveyNewsCheckLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _updateSearchPathsForLinkedFiles
            // 
            this._updateSearchPathsForLinkedFiles.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._updateSearchPathsForLinkedFiles.AutoSize = true;
            this.tableLayoutPanel3.SetColumnSpan(this._updateSearchPathsForLinkedFiles, 2);
            this._updateSearchPathsForLinkedFiles.Location = new System.Drawing.Point(6, 26);
            this._updateSearchPathsForLinkedFiles.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._updateSearchPathsForLinkedFiles.Name = "_updateSearchPathsForLinkedFiles";
            this._updateSearchPathsForLinkedFiles.Size = new System.Drawing.Size(241, 17);
            this._updateSearchPathsForLinkedFiles.TabIndex = 1;
            this._updateSearchPathsForLinkedFiles.Text = "&Update search paths when adding linked files";
            this._updateSearchPathsForLinkedFiles.UseVisualStyleBackColor = true;
            this._updateSearchPathsForLinkedFiles.CheckedChanged += new System.EventHandler(this._updateSearchPathsForLinkedFiles_CheckedChanged);
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
            this._surveyNewsCheckCombo.Location = new System.Drawing.Point(185, 76);
            this._surveyNewsCheckCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._surveyNewsCheckCombo.Name = "_surveyNewsCheckCombo";
            this._surveyNewsCheckCombo.Size = new System.Drawing.Size(172, 21);
            this._surveyNewsCheckCombo.TabIndex = 5;
            this._surveyNewsCheckCombo.SelectedIndexChanged += new System.EventHandler(this._surveyNewsCheckCombo_SelectedIndexChanged);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this._debuggingGroupBox, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._miscOptions, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(381, 290);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // PythonAdvancedOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonAdvancedOptionsControl";
            this.Size = new System.Drawing.Size(381, 290);
            this._debuggingGroupBox.ResumeLayout(false);
            this._debuggingGroupBox.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this._miscOptions.ResumeLayout(false);
            this._miscOptions.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox _promptOnBuildError;
        private System.Windows.Forms.ComboBox _indentationInconsistentCombo;
        private System.Windows.Forms.Label _indentationInconsistentLabel;
        private System.Windows.Forms.CheckBox _waitOnAbnormalExit;
        private System.Windows.Forms.CheckBox _autoAnalysis;
        private System.Windows.Forms.CheckBox _waitOnNormalExit;
        private System.Windows.Forms.CheckBox _teeStdOut;
        private System.Windows.Forms.CheckBox _breakOnSystemExitZero;
        private System.Windows.Forms.GroupBox _debuggingGroupBox;
        private System.Windows.Forms.GroupBox _miscOptions;
        private System.Windows.Forms.CheckBox _updateSearchPathsForLinkedFiles;
        private System.Windows.Forms.CheckBox _debugStdLib;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label _surveyNewsCheckLabel;
        private System.Windows.Forms.ComboBox _surveyNewsCheckCombo;
    }
}
