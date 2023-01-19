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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonAnalysisOptionsControl));
            System.Windows.Forms.Label diagnosticModeLabel;
            System.Windows.Forms.Label logLevelLabel;
            System.Windows.Forms.Label typeCheckingModeLabel;
            System.Windows.Forms.Label stubsPathsLabel;
            System.Windows.Forms.Label importFormatLabel;
            this._diagnosticsModeCombo = new System.Windows.Forms.ComboBox();
            this._logLevelCombo = new System.Windows.Forms.ComboBox();
            this._typeCheckingMode = new System.Windows.Forms.ComboBox();
            this._importFormatCombo = new System.Windows.Forms.ComboBox();
            this._stubsPath = new System.Windows.Forms.TextBox();
            this._searchPathsLabel = new System.Windows.Forms.Label();
            this._searchPaths = new System.Windows.Forms.TextBox();
            this._typeShedPathsLabel = new System.Windows.Forms.Label();
            this._typeshedPaths = new System.Windows.Forms.TextBox();
            this._autoSearchPath = new System.Windows.Forms.CheckBox();
            this._indexing = new System.Windows.Forms.CheckBox();
            this._diagnosticModeToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._logLevelToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._typeCheckingToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._stubsPathToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._searchPathsToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._typeshedPathsToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._commonSearchPathsToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._searchPathsLabelToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._typeshedPathsLabelToolTip = new System.Windows.Forms.ToolTip(this.components);
            this._importFormatToolTip = new System.Windows.Forms.ToolTip(this.components);
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            diagnosticModeLabel = new System.Windows.Forms.Label();
            logLevelLabel = new System.Windows.Forms.Label();
            typeCheckingModeLabel = new System.Windows.Forms.Label();
            stubsPathsLabel = new System.Windows.Forms.Label();
            importFormatLabel = new System.Windows.Forms.Label();
            tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(diagnosticModeLabel, 0, 0);
            tableLayoutPanel1.Controls.Add(this._diagnosticsModeCombo, 1, 0);
            tableLayoutPanel1.Controls.Add(logLevelLabel, 0, 1);
            tableLayoutPanel1.Controls.Add(this._logLevelCombo, 1, 1);
            tableLayoutPanel1.Controls.Add(typeCheckingModeLabel, 0, 2);
            tableLayoutPanel1.Controls.Add(this._typeCheckingMode, 1, 2);
            tableLayoutPanel1.Controls.Add(this._importFormatCombo, 1, 3);
            tableLayoutPanel1.Controls.Add(stubsPathsLabel, 0, 4);
            tableLayoutPanel1.Controls.Add(this._stubsPath, 1, 4);
            tableLayoutPanel1.Controls.Add(this._searchPathsLabel, 0, 5);
            tableLayoutPanel1.Controls.Add(this._searchPaths, 1, 5);
            tableLayoutPanel1.Controls.Add(this._typeShedPathsLabel, 0, 6);
            tableLayoutPanel1.Controls.Add(this._typeshedPaths, 1, 6);
            tableLayoutPanel1.Controls.Add(importFormatLabel, 0, 3);
            tableLayoutPanel1.Controls.Add(this._autoSearchPath, 0, 7);
            tableLayoutPanel1.Controls.Add(this._indexing, 0, 8);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // diagnosticModeLabel
            // 
            resources.ApplyResources(diagnosticModeLabel, "diagnosticModeLabel");
            diagnosticModeLabel.Name = "diagnosticModeLabel";
            // 
            // _diagnosticsModeCombo
            // 
            this._diagnosticsModeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._diagnosticsModeCombo.FormattingEnabled = true;
            resources.ApplyResources(this._diagnosticsModeCombo, "_diagnosticsModeCombo");
            this._diagnosticsModeCombo.Name = "_diagnosticsModeCombo";
            // 
            // logLevelLabel
            // 
            resources.ApplyResources(logLevelLabel, "logLevelLabel");
            logLevelLabel.Name = "logLevelLabel";
            // 
            // _logLevelCombo
            // 
            this._logLevelCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._logLevelCombo.FormattingEnabled = true;
            resources.ApplyResources(this._logLevelCombo, "_logLevelCombo");
            this._logLevelCombo.Name = "_logLevelCombo";
            // 
            // typeCheckingModeLabel
            // 
            resources.ApplyResources(typeCheckingModeLabel, "typeCheckingModeLabel");
            typeCheckingModeLabel.Name = "typeCheckingModeLabel";
            // 
            // _typeCheckingMode
            // 
            this._typeCheckingMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._typeCheckingMode.FormattingEnabled = true;
            resources.ApplyResources(this._typeCheckingMode, "_typeCheckingMode");
            this._typeCheckingMode.Name = "_typeCheckingMode";
            // 
            // importFormatLabel
            // 
            resources.ApplyResources(importFormatLabel, "importFormatLabel");
            importFormatLabel.Name = "importFormatLabel";
            // 
            // _importFormatCombo
            // 
            this._importFormatCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._importFormatCombo.DropDownWidth = 253;
            this._importFormatCombo.FormattingEnabled = true;
            resources.ApplyResources(this._importFormatCombo, "_importFormatCombo");
            this._importFormatCombo.Name = "_importFormatCombo";
            // 
            // stubsPathsLabel
            // 
            resources.ApplyResources(stubsPathsLabel, "stubsPathsLabel");
            stubsPathsLabel.Name = "stubsPathsLabel";
            // 
            // _stubsPath
            // 
            resources.ApplyResources(this._stubsPath, "_stubsPath");
            this._stubsPath.Name = "_stubsPath";
            // 
            // _searchPathsLabel
            // 
            resources.ApplyResources(this._searchPathsLabel, "_searchPathsLabel");
            this._searchPathsLabel.Name = "_searchPathsLabel";
            // 
            // _searchPaths
            // 
            this._searchPaths.AcceptsReturn = true;
            resources.ApplyResources(this._searchPaths, "_searchPaths");
            this._searchPaths.Name = "_searchPaths";
            // 
            // _typeShedPathsLabel
            // 
            resources.ApplyResources(this._typeShedPathsLabel, "_typeShedPathsLabel");
            this._typeShedPathsLabel.Name = "_typeShedPathsLabel";
            // 
            // _typeshedPaths
            // 
            this._typeshedPaths.AcceptsReturn = true;
            resources.ApplyResources(this._typeshedPaths, "_typeshedPaths");
            this._typeshedPaths.Name = "_typeshedPaths";
            // 
            // _autoSearchPath
            // 
            resources.ApplyResources(this._autoSearchPath, "_autoSearchPath");
            tableLayoutPanel1.SetColumnSpan(this._autoSearchPath, 2);
            this._autoSearchPath.Name = "_autoSearchPath";
            this._autoSearchPath.UseVisualStyleBackColor = true;
            // 
            // _indexing
            // 
            resources.ApplyResources(this._indexing, "_indexing");
            tableLayoutPanel1.SetColumnSpan(this._indexing, 2);
            this._indexing.Name = "_indexing";
            this._indexing.UseVisualStyleBackColor = true;
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
        private System.Windows.Forms.TextBox _stubsPath;
        private System.Windows.Forms.ComboBox _typeCheckingMode;
        private System.Windows.Forms.TextBox _searchPaths;
        private System.Windows.Forms.TextBox _typeshedPaths;
        private System.Windows.Forms.ToolTip _diagnosticModeToolTip;
        private System.Windows.Forms.ToolTip _logLevelToolTip;
        private System.Windows.Forms.ToolTip _typeCheckingToolTip;
        private System.Windows.Forms.ToolTip _stubsPathToolTip;
        private System.Windows.Forms.ToolTip _searchPathsToolTip;
        private System.Windows.Forms.ToolTip _typeshedPathsToolTip;
        private System.Windows.Forms.ToolTip _commonSearchPathsToolTip;
        private System.Windows.Forms.CheckBox _autoSearchPath;
        private System.Windows.Forms.CheckBox _indexing;
        private System.Windows.Forms.ToolTip _searchPathsLabelToolTip;
        private System.Windows.Forms.ToolTip _typeshedPathsLabelToolTip;
        private System.Windows.Forms.Label _typeShedPathsLabel;
        private System.Windows.Forms.ToolTip _importFormatToolTip;
        private System.Windows.Forms.Label _searchPathsLabel;
        private System.Windows.Forms.ComboBox _importFormatCombo;
    }
}
