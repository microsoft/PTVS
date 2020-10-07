namespace Microsoft.PythonTools.Options {
    partial class PythonAnalysisOptionsControl {
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
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonAnalysisOptionsControl));
            System.Windows.Forms.Label typeCheckingModeLabel;
            System.Windows.Forms.Label stubsPathsLabel;
            System.Windows.Forms.Label logLevelLabel;
            System.Windows.Forms.Label diagnosticModeLabel;
            System.Windows.Forms.Label searchPathsLabel;
            System.Windows.Forms.Label typeShedPathsLabel;
            this._typeCheckingModeCombo = new System.Windows.Forms.ComboBox();
            this._autoSearchPathCheckbox = new System.Windows.Forms.CheckBox();
            this._logLevelCombo = new System.Windows.Forms.ComboBox();
            this._diagnosticsModeCombo = new System.Windows.Forms.ComboBox();
            this._stubsPath = new System.Windows.Forms.TextBox();
            this._searchPathsTextBox = new System.Windows.Forms.TextBox();
            this._typeshedPathsTextBox = new System.Windows.Forms.TextBox();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            typeCheckingModeLabel = new System.Windows.Forms.Label();
            stubsPathsLabel = new System.Windows.Forms.Label();
            logLevelLabel = new System.Windows.Forms.Label();
            diagnosticModeLabel = new System.Windows.Forms.Label();
            searchPathsLabel = new System.Windows.Forms.Label();
            typeShedPathsLabel = new System.Windows.Forms.Label();
            tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(typeShedPathsLabel, 0, 5);
            tableLayoutPanel1.Controls.Add(searchPathsLabel, 0, 4);
            tableLayoutPanel1.Controls.Add(this._typeCheckingModeCombo, 1, 2);
            tableLayoutPanel1.Controls.Add(typeCheckingModeLabel, 0, 2);
            tableLayoutPanel1.Controls.Add(stubsPathsLabel, 0, 3);
            tableLayoutPanel1.Controls.Add(this._logLevelCombo, 1, 1);
            tableLayoutPanel1.Controls.Add(logLevelLabel, 0, 1);
            tableLayoutPanel1.Controls.Add(this._diagnosticsModeCombo, 1, 0);
            tableLayoutPanel1.Controls.Add(diagnosticModeLabel, 0, 0);
            tableLayoutPanel1.Controls.Add(this._stubsPath, 1, 3);
            tableLayoutPanel1.Controls.Add(this._autoSearchPathCheckbox, 0, 6);
            tableLayoutPanel1.Controls.Add(this._searchPathsTextBox, 1, 4);
            tableLayoutPanel1.Controls.Add(this._typeshedPathsTextBox, 1, 5);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _typeCheckingModeCombo
            // 
            this._typeCheckingModeCombo.FormattingEnabled = true;
            resources.ApplyResources(this._typeCheckingModeCombo, "_typeCheckingModeCombo");
            this._typeCheckingModeCombo.Name = "_typeCheckingModeCombo";
            // 
            // typeCheckingModeLabel
            // 
            resources.ApplyResources(typeCheckingModeLabel, "typeCheckingModeLabel");
            typeCheckingModeLabel.Name = "typeCheckingModeLabel";
            // 
            // _autoSearchPathCheckbox
            // 
            resources.ApplyResources(this._autoSearchPathCheckbox, "_autoSearchPathCheckbox");
            tableLayoutPanel1.SetColumnSpan(this._autoSearchPathCheckbox, 2);
            this._autoSearchPathCheckbox.Name = "_autoSearchPathCheckbox";
            this._autoSearchPathCheckbox.UseVisualStyleBackColor = true;
            // 
            // stubsPathsLabel
            // 
            resources.ApplyResources(stubsPathsLabel, "stubsPathsLabel");
            stubsPathsLabel.Name = "stubsPathsLabel";
            // 
            // _logLevelCombo
            // 
            this._logLevelCombo.FormattingEnabled = true;
            resources.ApplyResources(this._logLevelCombo, "_logLevelCombo");
            this._logLevelCombo.Name = "_logLevelCombo";
            // 
            // logLevelLabel
            // 
            resources.ApplyResources(logLevelLabel, "logLevelLabel");
            logLevelLabel.Name = "logLevelLabel";
            // 
            // _diagnosticsModeCombo
            // 
            this._diagnosticsModeCombo.FormattingEnabled = true;
            resources.ApplyResources(this._diagnosticsModeCombo, "_diagnosticsModeCombo");
            this._diagnosticsModeCombo.Name = "_diagnosticsModeCombo";
            // 
            // diagnosticModeLabel
            // 
            resources.ApplyResources(diagnosticModeLabel, "diagnosticModeLabel");
            diagnosticModeLabel.Name = "diagnosticModeLabel";
            // 
            // _stubsPath
            // 
            resources.ApplyResources(this._stubsPath, "_stubsPath");
            this._stubsPath.Name = "_stubsPath";
            // 
            // searchPathsLabel
            // 
            resources.ApplyResources(searchPathsLabel, "searchPathsLabel");
            searchPathsLabel.Name = "searchPathsLabel";
            // 
            // _searchPathsTextBox
            // 
            resources.ApplyResources(this._searchPathsTextBox, "_searchPathsTextBox");
            this._searchPathsTextBox.Name = "_searchPathsTextBox";
            // 
            // typeShedPathsLabel
            // 
            resources.ApplyResources(typeShedPathsLabel, "typeShedPathsLabel");
            typeShedPathsLabel.Name = "typeShedPathsLabel";
            // 
            // _typeshedPathsTextBox
            // 
            resources.ApplyResources(this._typeshedPathsTextBox, "_typeshedPathsTextBox");
            this._typeshedPathsTextBox.Name = "_typeshedPathsTextBox";
            // 
            // PythonAnalysisOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(tableLayoutPanel1);
            this.Name = "PythonAnalysisOptionsControl";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ComboBox _diagnosticsModeCombo;
        private System.Windows.Forms.ComboBox _logLevelCombo;
        private System.Windows.Forms.CheckBox _autoSearchPathCheckbox;
        private System.Windows.Forms.TextBox _stubsPath;
        private System.Windows.Forms.ComboBox _typeCheckingModeCombo;
        private System.Windows.Forms.TextBox _searchPathsTextBox;
        private System.Windows.Forms.TextBox _typeshedPathsTextBox;
    }
}
