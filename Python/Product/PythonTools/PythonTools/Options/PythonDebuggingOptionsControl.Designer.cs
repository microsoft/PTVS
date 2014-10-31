namespace Microsoft.PythonTools.Options {
    partial class PythonDebuggingOptionsControl {
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
            this._waitOnAbnormalExit = new System.Windows.Forms.CheckBox();
            this._waitOnNormalExit = new System.Windows.Forms.CheckBox();
            this._teeStdOut = new System.Windows.Forms.CheckBox();
            this._breakOnSystemExitZero = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._debugStdLib = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel2.SuspendLayout();
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
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 7;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(381, 290);
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
            // 
            // PythonDebuggingOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel2);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonDebuggingOptionsControl";
            this.Size = new System.Drawing.Size(381, 290);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _promptOnBuildError;
        private System.Windows.Forms.CheckBox _waitOnAbnormalExit;
        private System.Windows.Forms.CheckBox _waitOnNormalExit;
        private System.Windows.Forms.CheckBox _teeStdOut;
        private System.Windows.Forms.CheckBox _breakOnSystemExitZero;
        private System.Windows.Forms.CheckBox _debugStdLib;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
    }
}
