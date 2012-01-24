namespace Microsoft.PythonTools.Commands {
    partial class DiagnosticsForm {
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DiagnosticsForm));
            this._textBox = new System.Windows.Forms.TextBox();
            this._ok = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._copy = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _textBox
            // 
            this.tableLayoutPanel1.SetColumnSpan(this._textBox, 3);
            this._textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._textBox.Location = new System.Drawing.Point(6, 3);
            this._textBox.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._textBox.Multiline = true;
            this._textBox.Name = "_textBox";
            this._textBox.ReadOnly = true;
            this._textBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._textBox.Size = new System.Drawing.Size(812, 464);
            this._textBox.TabIndex = 0;
            // 
            // _ok
            // 
            this._ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._ok.AutoSize = true;
            this._ok.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._ok.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._ok.Location = new System.Drawing.Point(732, 473);
            this._ok.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._ok.MinimumSize = new System.Drawing.Size(86, 24);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(86, 24);
            this._ok.TabIndex = 1;
            this._ok.Text = "&OK";
            this._ok.UseVisualStyleBackColor = true;
            this._ok.Click += new System.EventHandler(this._ok_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this._textBox, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._ok, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this._copy, 1, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(824, 500);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // _copy
            // 
            this._copy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._copy.AutoSize = true;
            this._copy.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._copy.Location = new System.Drawing.Point(634, 473);
            this._copy.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._copy.MinimumSize = new System.Drawing.Size(86, 24);
            this._copy.Name = "_copy";
            this._copy.Size = new System.Drawing.Size(86, 24);
            this._copy.TabIndex = 1;
            this._copy.Text = "&Copy";
            this._copy.UseVisualStyleBackColor = true;
            this._copy.Click += new System.EventHandler(this._copy_Click);
            // 
            // DiagnosticsForm
            // 
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._ok;
            this.ClientSize = new System.Drawing.Size(824, 500);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "DiagnosticsForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Python Tools for Visual Studio - Diagnostic Info";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox _textBox;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button _copy;
    }
}