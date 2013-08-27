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
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            this._pasteRemovesReplPrompts = new System.Windows.Forms.CheckBox();
            this._formatOnPaste = new System.Windows.Forms.CheckBox();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _pasteRemovesReplPrompts
            // 
            this._pasteRemovesReplPrompts.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._pasteRemovesReplPrompts.AutoSize = true;
            this._pasteRemovesReplPrompts.Location = new System.Drawing.Point(6, 26);
            this._pasteRemovesReplPrompts.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._pasteRemovesReplPrompts.Name = "_pasteRemovesReplPrompts";
            this._pasteRemovesReplPrompts.Size = new System.Drawing.Size(167, 17);
            this._pasteRemovesReplPrompts.TabIndex = 4;
            this._pasteRemovesReplPrompts.Text = "&Paste removes REPL prompts";
            this._pasteRemovesReplPrompts.UseVisualStyleBackColor = true;
            this._pasteRemovesReplPrompts.CheckedChanged += new System.EventHandler(this._pasteRemovesReplPrompts_CheckedChanged);
            // 
            // _formatOnPaste
            // 
            this._formatOnPaste.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._formatOnPaste.AutoSize = true;
            this._formatOnPaste.Enabled = false;
            this._formatOnPaste.Location = new System.Drawing.Point(6, 3);
            this._formatOnPaste.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._formatOnPaste.Name = "_formatOnPaste";
            this._formatOnPaste.Size = new System.Drawing.Size(164, 17);
            this._formatOnPaste.TabIndex = 1;
            this._formatOnPaste.Text = "Automatically &format on paste";
            this._formatOnPaste.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(this._formatOnPaste, 0, 0);
            tableLayoutPanel1.Controls.Add(this._pasteRemovesReplPrompts, 0, 1);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new System.Drawing.Size(381, 276);
            tableLayoutPanel1.TabIndex = 5;
            // 
            // PythonFormattingGeneralOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(tableLayoutPanel1);
            this.Name = "PythonFormattingGeneralOptionsControl";
            this.Size = new System.Drawing.Size(381, 276);
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _formatOnPaste;
        private System.Windows.Forms.CheckBox _pasteRemovesReplPrompts;
    }
}
