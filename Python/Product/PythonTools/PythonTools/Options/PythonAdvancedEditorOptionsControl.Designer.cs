namespace Microsoft.PythonTools.Options {
    partial class PythonAdvancedEditorOptionsControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonAdvancedEditorOptionsControl));
            this._completeFunctionParens = new System.Windows.Forms.CheckBox();
            this._autoImportCompletions = new System.Windows.Forms.CheckBox();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(this._autoImportCompletions, 0, 0);
            tableLayoutPanel1.Controls.Add(this._completeFunctionParens, 0, 1);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _completeFunctionParens
            // 
            resources.ApplyResources(this._completeFunctionParens, "_completeFunctionParens");
            tableLayoutPanel1.SetColumnSpan(this._completeFunctionParens, 2);
            this._completeFunctionParens.Name = "_completeFunctionParens";
            this._completeFunctionParens.UseVisualStyleBackColor = true;
            // 
            // _autoImportCompletions
            // 
            resources.ApplyResources(this._autoImportCompletions, "_autoImportCompletions");
            this._autoImportCompletions.AutoEllipsis = true;
            tableLayoutPanel1.SetColumnSpan(this._autoImportCompletions, 2);
            this._autoImportCompletions.Name = "_autoImportCompletions";
            this._autoImportCompletions.UseVisualStyleBackColor = true;
            // 
            // PythonAdvancedEditorOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(tableLayoutPanel1);
            this.Name = "PythonAdvancedEditorOptionsControl";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.CheckBox _autoImportCompletions;
        private System.Windows.Forms.CheckBox _completeFunctionParens;
    }
}
