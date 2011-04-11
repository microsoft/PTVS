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
            this.SuspendLayout();
            // 
            // _promptOnBuildError
            // 
            this._promptOnBuildError.AutoSize = true;
            this._promptOnBuildError.Location = new System.Drawing.Point(7, 12);
            this._promptOnBuildError.Name = "_promptOnBuildError";
            this._promptOnBuildError.Size = new System.Drawing.Size(244, 17);
            this._promptOnBuildError.TabIndex = 3;
            this._promptOnBuildError.Text = "&Prompt before running when errors are present";
            this._promptOnBuildError.UseVisualStyleBackColor = true;
            this._promptOnBuildError.CheckedChanged += new System.EventHandler(this._promptOnBuildError_CheckedChanged);
            // 
            // _indentationInconsistentCombo
            // 
            this._indentationInconsistentCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._indentationInconsistentCombo.FormattingEnabled = true;
            this._indentationInconsistentCombo.Items.AddRange(new object[] {
            "Errors",
            "Warnings",
            "Don\'t"});
            this._indentationInconsistentCombo.Location = new System.Drawing.Point(188, 124);
            this._indentationInconsistentCombo.Name = "_indentationInconsistentCombo";
            this._indentationInconsistentCombo.Size = new System.Drawing.Size(121, 21);
            this._indentationInconsistentCombo.TabIndex = 5;
            this._indentationInconsistentCombo.SelectedIndexChanged += new System.EventHandler(this._indentationInconsistentCombo_SelectedIndexChanged);
            // 
            // _indentationInconsistentLabel
            // 
            this._indentationInconsistentLabel.AutoSize = true;
            this._indentationInconsistentLabel.Location = new System.Drawing.Point(6, 127);
            this._indentationInconsistentLabel.Name = "_indentationInconsistentLabel";
            this._indentationInconsistentLabel.Size = new System.Drawing.Size(167, 13);
            this._indentationInconsistentLabel.TabIndex = 6;
            this._indentationInconsistentLabel.Text = "&Report inconsistent indentation as";
            // 
            // _waitOnAbnormalExit
            // 
            this._waitOnAbnormalExit.AutoSize = true;
            this._waitOnAbnormalExit.Location = new System.Drawing.Point(7, 35);
            this._waitOnAbnormalExit.Name = "_waitOnAbnormalExit";
            this._waitOnAbnormalExit.Size = new System.Drawing.Size(235, 17);
            this._waitOnAbnormalExit.TabIndex = 7;
            this._waitOnAbnormalExit.Text = "&Wait for input when process exits abnormally";
            this._waitOnAbnormalExit.UseVisualStyleBackColor = true;
            this._waitOnAbnormalExit.CheckedChanged += new System.EventHandler(this._waitOnExit_CheckedChanged);
            // 
            // _autoAnalysis
            // 
            this._autoAnalysis.AutoSize = true;
            this._autoAnalysis.Location = new System.Drawing.Point(7, 104);
            this._autoAnalysis.Name = "_autoAnalysis";
            this._autoAnalysis.Size = new System.Drawing.Size(272, 17);
            this._autoAnalysis.TabIndex = 8;
            this._autoAnalysis.Text = "&Automatically analyze standard library in background";
            this._autoAnalysis.UseVisualStyleBackColor = true;
            this._autoAnalysis.CheckedChanged += new System.EventHandler(this._autoAnalysis_CheckedChanged);
            // 
            // _waitOnNormalExit
            // 
            this._waitOnNormalExit.AutoSize = true;
            this._waitOnNormalExit.Location = new System.Drawing.Point(7, 58);
            this._waitOnNormalExit.Name = "_waitOnNormalExit";
            this._waitOnNormalExit.Size = new System.Drawing.Size(223, 17);
            this._waitOnNormalExit.TabIndex = 9;
            this._waitOnNormalExit.Text = "Wai&t for input when process exits normally";
            this._waitOnNormalExit.UseVisualStyleBackColor = true;
            this._waitOnNormalExit.CheckedChanged += new System.EventHandler(this._waitOnNormalExit_CheckedChanged);
            // 
            // _teeStdOut
            // 
            this._teeStdOut.AutoSize = true;
            this._teeStdOut.Location = new System.Drawing.Point(7, 81);
            this._teeStdOut.Name = "_teeStdOut";
            this._teeStdOut.Size = new System.Drawing.Size(240, 17);
            this._teeStdOut.TabIndex = 10;
            this._teeStdOut.Text = "Tee program output to &Debug Output window";
            this._teeStdOut.UseVisualStyleBackColor = true;
            this._teeStdOut.CheckedChanged += new System.EventHandler(this._redirectOutputToVs_CheckedChanged);
            // 
            // PythonAdvancedOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._promptOnBuildError);
            this.Controls.Add(this._waitOnAbnormalExit);
            this.Controls.Add(this._waitOnNormalExit);
            this.Controls.Add(this._teeStdOut);
            this.Controls.Add(this._autoAnalysis);
            this.Controls.Add(this._indentationInconsistentLabel);
            this.Controls.Add(this._indentationInconsistentCombo);
            this.Name = "PythonAdvancedOptionsControl";
            this.Size = new System.Drawing.Size(395, 317);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _promptOnBuildError;
        private System.Windows.Forms.ComboBox _indentationInconsistentCombo;
        private System.Windows.Forms.Label _indentationInconsistentLabel;
        private System.Windows.Forms.CheckBox _waitOnAbnormalExit;
        private System.Windows.Forms.CheckBox _autoAnalysis;
        private System.Windows.Forms.CheckBox _waitOnNormalExit;
        private System.Windows.Forms.CheckBox _teeStdOut;
    }
}
