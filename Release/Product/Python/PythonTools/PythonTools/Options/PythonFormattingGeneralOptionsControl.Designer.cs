namespace Microsoft.PythonTools.Options {
    partial class PythonFormattingGeneralOptionsControl {
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
            this._generalGroupBox = new System.Windows.Forms.GroupBox();
            this._formatOnPaste = new System.Windows.Forms.CheckBox();
            this._pasteRemovesReplPrompts = new System.Windows.Forms.CheckBox();
            this._generalGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // _generalGroupBox
            // 
            this._generalGroupBox.Controls.Add(this._pasteRemovesReplPrompts);
            this._generalGroupBox.Controls.Add(this._formatOnPaste);
            this._generalGroupBox.Location = new System.Drawing.Point(4, 4);
            this._generalGroupBox.Name = "_generalGroupBox";
            this._generalGroupBox.Size = new System.Drawing.Size(374, 269);
            this._generalGroupBox.TabIndex = 0;
            this._generalGroupBox.TabStop = false;
            this._generalGroupBox.Text = "General";
            // 
            // _formatOnPaste
            // 
            this._formatOnPaste.AutoSize = true;
            this._formatOnPaste.Location = new System.Drawing.Point(18, 31);
            this._formatOnPaste.Name = "_formatOnPaste";
            this._formatOnPaste.Size = new System.Drawing.Size(164, 17);
            this._formatOnPaste.TabIndex = 1;
            this._formatOnPaste.Text = "Automatically format on paste";
            this._formatOnPaste.UseVisualStyleBackColor = true;
            // 
            // _pasteRemovesReplPrompts
            // 
            this._pasteRemovesReplPrompts.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._pasteRemovesReplPrompts.AutoSize = true;
            this._pasteRemovesReplPrompts.Location = new System.Drawing.Point(18, 54);
            this._pasteRemovesReplPrompts.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._pasteRemovesReplPrompts.Name = "_pasteRemovesReplPrompts";
            this._pasteRemovesReplPrompts.Size = new System.Drawing.Size(167, 17);
            this._pasteRemovesReplPrompts.TabIndex = 4;
            this._pasteRemovesReplPrompts.Text = "&Paste removes REPL prompts";
            this._pasteRemovesReplPrompts.UseVisualStyleBackColor = true;
            // 
            // PythonFormattingGeneralOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._generalGroupBox);
            this.Name = "PythonFormattingGeneralOptionsControl";
            this.Size = new System.Drawing.Size(381, 276);
            this._generalGroupBox.ResumeLayout(false);
            this._generalGroupBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox _generalGroupBox;
        private System.Windows.Forms.CheckBox _formatOnPaste;
        private System.Windows.Forms.CheckBox _pasteRemovesReplPrompts;
    }
}
