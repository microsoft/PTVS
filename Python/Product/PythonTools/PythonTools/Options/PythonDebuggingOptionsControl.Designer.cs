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
            this._useLegacyDebugger = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this._varPresSpecialComboBox = new System.Windows.Forms.ComboBox();
            this._varPresSpecialLabel = new System.Windows.Forms.Label();
            this._varPresProtectedComboBox = new System.Windows.Forms.ComboBox();
            this._varPresProtectedLabel = new System.Windows.Forms.Label();
            this._varPresFunctionComboBox = new System.Windows.Forms.ComboBox();
            this._varPresFunctionLabel = new System.Windows.Forms.Label();
            this._varPresClassComboBox = new System.Windows.Forms.ComboBox();
            this._varPresClassLabel = new System.Windows.Forms.Label();
            this.tableLayoutPanel2.SuspendLayout();
            this.groupBox1.SuspendLayout();
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
            // _useLegacyDebugger
            // 
            resources.ApplyResources(this._useLegacyDebugger, "_useLegacyDebugger");
            this._useLegacyDebugger.AutoEllipsis = true;
            this._useLegacyDebugger.Name = "_useLegacyDebugger";
            this._useLegacyDebugger.UseVisualStyleBackColor = true;
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
            this.tableLayoutPanel2.Controls.Add(this._useLegacyDebugger, 0, 7);
            this.tableLayoutPanel2.Controls.Add(this.groupBox1, 0, 9);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this._varPresSpecialComboBox);
            this.groupBox1.Controls.Add(this._varPresSpecialLabel);
            this.groupBox1.Controls.Add(this._varPresProtectedComboBox);
            this.groupBox1.Controls.Add(this._varPresProtectedLabel);
            this.groupBox1.Controls.Add(this._varPresFunctionComboBox);
            this.groupBox1.Controls.Add(this._varPresFunctionLabel);
            this.groupBox1.Controls.Add(this._varPresClassComboBox);
            this.groupBox1.Controls.Add(this._varPresClassLabel);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // _varPresSpecialComboBox
            // 
            this._varPresSpecialComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._varPresSpecialComboBox.FormattingEnabled = true;
            resources.ApplyResources(this._varPresSpecialComboBox, "_varPresSpecialComboBox");
            this._varPresSpecialComboBox.Name = "_varPresSpecialComboBox";
            // 
            // _varPresSpecialLabel
            // 
            resources.ApplyResources(this._varPresSpecialLabel, "_varPresSpecialLabel");
            this._varPresSpecialLabel.Name = "_varPresSpecialLabel";
            // 
            // _varPresProtectedComboBox
            // 
            this._varPresProtectedComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._varPresProtectedComboBox.FormattingEnabled = true;
            resources.ApplyResources(this._varPresProtectedComboBox, "_varPresProtectedComboBox");
            this._varPresProtectedComboBox.Name = "_varPresProtectedComboBox";
            // 
            // _varPresProtectedLabel
            // 
            resources.ApplyResources(this._varPresProtectedLabel, "_varPresProtectedLabel");
            this._varPresProtectedLabel.Name = "_varPresProtectedLabel";
            // 
            // _varPresFunctionComboBox
            // 
            this._varPresFunctionComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._varPresFunctionComboBox.FormattingEnabled = true;
            resources.ApplyResources(this._varPresFunctionComboBox, "_varPresFunctionComboBox");
            this._varPresFunctionComboBox.Name = "_varPresFunctionComboBox";
            // 
            // _varPresFunctionLabel
            // 
            resources.ApplyResources(this._varPresFunctionLabel, "_varPresFunctionLabel");
            this._varPresFunctionLabel.Name = "_varPresFunctionLabel";
            // 
            // _varPresClassComboBox
            // 
            this._varPresClassComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._varPresClassComboBox.FormattingEnabled = true;
            resources.ApplyResources(this._varPresClassComboBox, "_varPresClassComboBox");
            this._varPresClassComboBox.Name = "_varPresClassComboBox";
            // 
            // _varPresClassLabel
            // 
            resources.ApplyResources(this._varPresClassLabel, "_varPresClassLabel");
            this._varPresClassLabel.Name = "_varPresClassLabel";
            // 
            // PythonDebuggingOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel2);
            this.Name = "PythonDebuggingOptionsControl";
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
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
        private System.Windows.Forms.CheckBox _useLegacyDebugger;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox _varPresSpecialComboBox;
        private System.Windows.Forms.Label _varPresSpecialLabel;
        private System.Windows.Forms.ComboBox _varPresProtectedComboBox;
        private System.Windows.Forms.Label _varPresProtectedLabel;
        private System.Windows.Forms.ComboBox _varPresFunctionComboBox;
        private System.Windows.Forms.Label _varPresFunctionLabel;
        private System.Windows.Forms.ComboBox _varPresClassComboBox;
        private System.Windows.Forms.Label _varPresClassLabel;
    }
}
