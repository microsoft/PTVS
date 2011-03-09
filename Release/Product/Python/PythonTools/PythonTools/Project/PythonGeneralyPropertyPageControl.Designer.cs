namespace Microsoft.PythonTools.Project {
    partial class PythonGeneralyPropertyPageControl {
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
            this._applicationGroup = new System.Windows.Forms.GroupBox();
            this._defaultInterpreter = new System.Windows.Forms.ComboBox();
            this._defaultInterpreterLabel = new System.Windows.Forms.Label();
            this._workingDirLabel = new System.Windows.Forms.Label();
            this._workingDirectory = new System.Windows.Forms.TextBox();
            this._windowsApplication = new System.Windows.Forms.CheckBox();
            this._startupFile = new System.Windows.Forms.TextBox();
            this._startupFileLabel = new System.Windows.Forms.Label();
            this._applicationGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _applicationGroup
            // 
            this._applicationGroup.Controls.Add(this._defaultInterpreter);
            this._applicationGroup.Controls.Add(this._defaultInterpreterLabel);
            this._applicationGroup.Controls.Add(this._workingDirLabel);
            this._applicationGroup.Controls.Add(this._workingDirectory);
            this._applicationGroup.Controls.Add(this._windowsApplication);
            this._applicationGroup.Controls.Add(this._startupFile);
            this._applicationGroup.Controls.Add(this._startupFileLabel);
            this._applicationGroup.Location = new System.Drawing.Point(4, 4);
            this._applicationGroup.Name = "_applicationGroup";
            this._applicationGroup.Size = new System.Drawing.Size(437, 132);
            this._applicationGroup.TabIndex = 0;
            this._applicationGroup.TabStop = false;
            this._applicationGroup.Text = "Application";
            // 
            // _defaultInterpreter
            // 
            this._defaultInterpreter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._defaultInterpreter.FormattingEnabled = true;
            this._defaultInterpreter.Location = new System.Drawing.Point(139, 103);
            this._defaultInterpreter.Name = "_defaultInterpreter";
            this._defaultInterpreter.Size = new System.Drawing.Size(286, 21);
            this._defaultInterpreter.TabIndex = 12;
            this._defaultInterpreter.SelectedIndexChanged += new System.EventHandler(this.Changed);
            // 
            // _defaultInterpreterLabel
            // 
            this._defaultInterpreterLabel.AutoSize = true;
            this._defaultInterpreterLabel.Location = new System.Drawing.Point(7, 103);
            this._defaultInterpreterLabel.Name = "_defaultInterpreterLabel";
            this._defaultInterpreterLabel.Size = new System.Drawing.Size(58, 13);
            this._defaultInterpreterLabel.TabIndex = 11;
            this._defaultInterpreterLabel.Text = "Interpreter:";
            // 
            // _workingDirLabel
            // 
            this._workingDirLabel.AutoSize = true;
            this._workingDirLabel.Location = new System.Drawing.Point(4, 54);
            this._workingDirLabel.Name = "_workingDirLabel";
            this._workingDirLabel.Size = new System.Drawing.Size(92, 13);
            this._workingDirLabel.TabIndex = 10;
            this._workingDirLabel.Text = "Working Directory";
            // 
            // _workingDirectory
            // 
            this._workingDirectory.Location = new System.Drawing.Point(139, 51);
            this._workingDirectory.Name = "_workingDirectory";
            this._workingDirectory.Size = new System.Drawing.Size(286, 20);
            this._workingDirectory.TabIndex = 1;
            this._workingDirectory.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _windowsApplication
            // 
            this._windowsApplication.AutoSize = true;
            this._windowsApplication.Location = new System.Drawing.Point(7, 79);
            this._windowsApplication.Name = "_windowsApplication";
            this._windowsApplication.Size = new System.Drawing.Size(125, 17);
            this._windowsApplication.TabIndex = 2;
            this._windowsApplication.Text = "Windows Application";
            this._windowsApplication.UseVisualStyleBackColor = true;
            this._windowsApplication.CheckedChanged += new System.EventHandler(this.Changed);
            // 
            // _startupFile
            // 
            this._startupFile.Location = new System.Drawing.Point(139, 25);
            this._startupFile.Name = "_startupFile";
            this._startupFile.Size = new System.Drawing.Size(286, 20);
            this._startupFile.TabIndex = 0;
            this._startupFile.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _startupFileLabel
            // 
            this._startupFileLabel.AutoSize = true;
            this._startupFileLabel.Location = new System.Drawing.Point(4, 28);
            this._startupFileLabel.Name = "_startupFileLabel";
            this._startupFileLabel.Size = new System.Drawing.Size(60, 13);
            this._startupFileLabel.TabIndex = 0;
            this._startupFileLabel.Text = "Startup File";
            // 
            // PythonGeneralyPropertyPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._applicationGroup);
            this.Name = "PythonGeneralyPropertyPageControl";
            this.Size = new System.Drawing.Size(457, 285);
            this._applicationGroup.ResumeLayout(false);
            this._applicationGroup.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox _applicationGroup;
        private System.Windows.Forms.Label _startupFileLabel;
        private System.Windows.Forms.TextBox _startupFile;
        private System.Windows.Forms.CheckBox _windowsApplication;
        private System.Windows.Forms.TextBox _workingDirectory;
        private System.Windows.Forms.Label _workingDirLabel;
        private System.Windows.Forms.Label _defaultInterpreterLabel;
        private System.Windows.Forms.ComboBox _defaultInterpreter;
    }
}
