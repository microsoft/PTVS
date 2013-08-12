namespace Microsoft.PythonTools.Project {
    partial class PythonGeneralPropertyPageControl {
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
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._startupFileLabel = new System.Windows.Forms.Label();
            this._startupFile = new System.Windows.Forms.TextBox();
            this._workingDirLabel = new System.Windows.Forms.Label();
            this._workingDirectory = new System.Windows.Forms.TextBox();
            this._windowsApplication = new System.Windows.Forms.CheckBox();
            this._defaultInterpreterLabel = new System.Windows.Forms.Label();
            this._defaultInterpreter = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._applicationGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _applicationGroup
            // 
            this._applicationGroup.AutoSize = true;
            this._applicationGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._applicationGroup.Controls.Add(this.tableLayoutPanel2);
            this._applicationGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._applicationGroup.Location = new System.Drawing.Point(6, 8);
            this._applicationGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._applicationGroup.Name = "_applicationGroup";
            this._applicationGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._applicationGroup.Size = new System.Drawing.Size(447, 137);
            this._applicationGroup.TabIndex = 0;
            this._applicationGroup.TabStop = false;
            this._applicationGroup.Text = "Application";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this._startupFileLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._startupFile, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._workingDirLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._workingDirectory, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._windowsApplication, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._defaultInterpreterLabel, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this._defaultInterpreter, 1, 3);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(6, 23);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(435, 106);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // _startupFileLabel
            // 
            this._startupFileLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._startupFileLabel.AutoEllipsis = true;
            this._startupFileLabel.AutoSize = true;
            this._startupFileLabel.Location = new System.Drawing.Point(6, 7);
            this._startupFileLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._startupFileLabel.Name = "_startupFileLabel";
            this._startupFileLabel.Size = new System.Drawing.Size(66, 13);
            this._startupFileLabel.TabIndex = 0;
            this._startupFileLabel.Text = "Startup File";
            this._startupFileLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _startupFile
            // 
            this._startupFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._startupFile.Location = new System.Drawing.Point(119, 3);
            this._startupFile.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._startupFile.MinimumSize = new System.Drawing.Size(50, 4);
            this._startupFile.Name = "_startupFile";
            this._startupFile.Size = new System.Drawing.Size(310, 22);
            this._startupFile.TabIndex = 1;
            this._startupFile.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _workingDirLabel
            // 
            this._workingDirLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._workingDirLabel.AutoEllipsis = true;
            this._workingDirLabel.AutoSize = true;
            this._workingDirLabel.Location = new System.Drawing.Point(6, 35);
            this._workingDirLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._workingDirLabel.Name = "_workingDirLabel";
            this._workingDirLabel.Size = new System.Drawing.Size(101, 13);
            this._workingDirLabel.TabIndex = 2;
            this._workingDirLabel.Text = "Working Directory";
            this._workingDirLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _workingDirectory
            // 
            this._workingDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._workingDirectory.Location = new System.Drawing.Point(119, 31);
            this._workingDirectory.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._workingDirectory.MinimumSize = new System.Drawing.Size(50, 4);
            this._workingDirectory.Name = "_workingDirectory";
            this._workingDirectory.Size = new System.Drawing.Size(310, 22);
            this._workingDirectory.TabIndex = 3;
            this._workingDirectory.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _windowsApplication
            // 
            this._windowsApplication.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._windowsApplication.AutoSize = true;
            this.tableLayoutPanel2.SetColumnSpan(this._windowsApplication, 2);
            this._windowsApplication.Location = new System.Drawing.Point(6, 59);
            this._windowsApplication.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._windowsApplication.Name = "_windowsApplication";
            this._windowsApplication.Size = new System.Drawing.Size(137, 17);
            this._windowsApplication.TabIndex = 4;
            this._windowsApplication.Text = "Windows Application";
            this._windowsApplication.UseVisualStyleBackColor = true;
            this._windowsApplication.CheckedChanged += new System.EventHandler(this.Changed);
            // 
            // _defaultInterpreterLabel
            // 
            this._defaultInterpreterLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._defaultInterpreterLabel.AutoEllipsis = true;
            this._defaultInterpreterLabel.AutoSize = true;
            this._defaultInterpreterLabel.Location = new System.Drawing.Point(6, 86);
            this._defaultInterpreterLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._defaultInterpreterLabel.Name = "_defaultInterpreterLabel";
            this._defaultInterpreterLabel.Size = new System.Drawing.Size(65, 13);
            this._defaultInterpreterLabel.TabIndex = 5;
            this._defaultInterpreterLabel.Text = "Interpreter:";
            this._defaultInterpreterLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _defaultInterpreter
            // 
            this._defaultInterpreter.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._defaultInterpreter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._defaultInterpreter.FormattingEnabled = true;
            this._defaultInterpreter.Location = new System.Drawing.Point(119, 82);
            this._defaultInterpreter.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._defaultInterpreter.MinimumSize = new System.Drawing.Size(50, 0);
            this._defaultInterpreter.Name = "_defaultInterpreter";
            this._defaultInterpreter.Size = new System.Drawing.Size(310, 21);
            this._defaultInterpreter.TabIndex = 6;
            this._defaultInterpreter.SelectedIndexChanged += new System.EventHandler(this.Changed);
            this._defaultInterpreter.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.Interpreter_Format);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this._applicationGroup, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(459, 153);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // PythonGeneralPropertyPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonGeneralPropertyPageControl";
            this.Size = new System.Drawing.Size(459, 153);
            this._applicationGroup.ResumeLayout(false);
            this._applicationGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

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
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}
