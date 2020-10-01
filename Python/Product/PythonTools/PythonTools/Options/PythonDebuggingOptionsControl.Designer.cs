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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonDebuggingOptionsControl));
            this._promptOnBuildError = new System.Windows.Forms.CheckBox();
            this._waitOnAbnormalExit = new System.Windows.Forms.CheckBox();
            this._waitOnNormalExit = new System.Windows.Forms.CheckBox();
            this._teeStdOut = new System.Windows.Forms.CheckBox();
            this._breakOnSystemExitZero = new System.Windows.Forms.CheckBox();
            this._debugStdLib = new System.Windows.Forms.CheckBox();
            this._showFunctionReturnValue = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // _promptOnBuildError
            // 
            resources.ApplyResources(this._promptOnBuildError, "_promptOnBuildError");
            this._promptOnBuildError.AutoEllipsis = true;
            this._promptOnBuildError.Name = "_promptOnBuildError";
            this._promptOnBuildError.UseVisualStyleBackColor = true;
            // 
            // _waitOnAbnormalExit
            // 
            resources.ApplyResources(this._waitOnAbnormalExit, "_waitOnAbnormalExit");
            this._waitOnAbnormalExit.AutoEllipsis = true;
            this._waitOnAbnormalExit.Name = "_waitOnAbnormalExit";
            this._waitOnAbnormalExit.UseVisualStyleBackColor = true;
            // 
            // _waitOnNormalExit
            // 
            resources.ApplyResources(this._waitOnNormalExit, "_waitOnNormalExit");
            this._waitOnNormalExit.AutoEllipsis = true;
            this._waitOnNormalExit.Name = "_waitOnNormalExit";
            this._waitOnNormalExit.UseVisualStyleBackColor = true;
            // 
            // _teeStdOut
            // 
            resources.ApplyResources(this._teeStdOut, "_teeStdOut");
            this._teeStdOut.AutoEllipsis = true;
            this._teeStdOut.Name = "_teeStdOut";
            this._teeStdOut.UseVisualStyleBackColor = true;
            // 
            // _breakOnSystemExitZero
            // 
            resources.ApplyResources(this._breakOnSystemExitZero, "_breakOnSystemExitZero");
            this._breakOnSystemExitZero.AutoEllipsis = true;
            this._breakOnSystemExitZero.Name = "_breakOnSystemExitZero";
            this._breakOnSystemExitZero.UseVisualStyleBackColor = true;
            // 
            // _debugStdLib
            // 
            resources.ApplyResources(this._debugStdLib, "_debugStdLib");
            this._debugStdLib.AutoEllipsis = true;
            this._debugStdLib.Name = "_debugStdLib";
            this._debugStdLib.UseVisualStyleBackColor = true;
            // 
            // _showFunctionReturnValue
            // 
            resources.ApplyResources(this._showFunctionReturnValue, "_showFunctionReturnValue");
            this._showFunctionReturnValue.Name = "_showFunctionReturnValue";
            this._showFunctionReturnValue.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this._promptOnBuildError, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._waitOnAbnormalExit, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._waitOnNormalExit, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._teeStdOut, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._breakOnSystemExitZero, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this._debugStdLib, 0, 5);
            this.tableLayoutPanel2.Controls.Add(this._showFunctionReturnValue, 0, 6);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // PythonDebuggingOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel2);
            this.Name = "PythonDebuggingOptionsControl";
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
        private System.Windows.Forms.CheckBox _showFunctionReturnValue;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
    }
}
