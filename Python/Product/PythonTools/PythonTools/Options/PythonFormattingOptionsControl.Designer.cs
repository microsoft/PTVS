namespace Microsoft.PythonTools.Options {
    partial class PythonFormattingOptionsControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonFormattingOptionsControl));
            System.Windows.Forms.Label formatterLaber;
            this._pasteRemovesReplPrompts = new System.Windows.Forms.CheckBox();
            this._formatterCombo = new System.Windows.Forms.ComboBox();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            formatterLaber = new System.Windows.Forms.Label();
            tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(this._pasteRemovesReplPrompts, 0, 1);
            tableLayoutPanel1.Controls.Add(this._formatterCombo, 1, 0);
            tableLayoutPanel1.Controls.Add(formatterLaber, 0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _pasteRemovesReplPrompts
            // 
            resources.ApplyResources(this._pasteRemovesReplPrompts, "_pasteRemovesReplPrompts");
            this._pasteRemovesReplPrompts.AutoEllipsis = true;
            tableLayoutPanel1.SetColumnSpan(this._pasteRemovesReplPrompts, 2);
            this._pasteRemovesReplPrompts.Name = "_pasteRemovesReplPrompts";
            this._pasteRemovesReplPrompts.UseVisualStyleBackColor = true;
            // 
            // _formatterCombo
            // 
            this._formatterCombo.FormattingEnabled = true;
            resources.ApplyResources(this._formatterCombo, "_formatterCombo");
            this._formatterCombo.Name = "_formatterCombo";
            // 
            // formatterLaber
            // 
            resources.ApplyResources(formatterLaber, "formatterLaber");
            formatterLaber.Name = "formatterLaber";
            // 
            // PythonFormattingOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(tableLayoutPanel1);
            this.Name = "PythonFormattingOptionsControl";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.CheckBox _pasteRemovesReplPrompts;
        private System.Windows.Forms.ComboBox _formatterCombo;
    }
}
