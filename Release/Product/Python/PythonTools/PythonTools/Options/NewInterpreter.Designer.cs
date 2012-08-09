namespace Microsoft.PythonTools.Options {
    partial class NewInterpreter {
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
            this._cancel = new System.Windows.Forms.Button();
            this._ok = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this._description = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _cancel
            // 
            this._cancel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._cancel.AutoSize = true;
            this._cancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(284, 73);
            this._cancel.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._cancel.Name = "_cancel";
            this._cancel.Padding = new System.Windows.Forms.Padding(12, 0, 12, 0);
            this._cancel.Size = new System.Drawing.Size(74, 23);
            this._cancel.TabIndex = 3;
            this._cancel.Text = "&Cancel";
            this._cancel.UseVisualStyleBackColor = true;
            this._cancel.Click += new System.EventHandler(this.CancelClick);
            // 
            // _ok
            // 
            this._ok.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._ok.AutoSize = true;
            this._ok.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._ok.Location = new System.Drawing.Point(216, 73);
            this._ok.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._ok.Name = "_ok";
            this._ok.Padding = new System.Windows.Forms.Padding(12, 0, 12, 0);
            this._ok.Size = new System.Drawing.Size(56, 23);
            this._ok.TabIndex = 2;
            this._ok.Text = "&OK";
            this._ok.UseVisualStyleBackColor = true;
            this._ok.Click += new System.EventHandler(this.OkClick);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label1.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.label1, 3);
            this.label1.Location = new System.Drawing.Point(6, 8);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(152, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Enter name for new interpreter:";
            // 
            // _description
            // 
            this._description.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this._description, 3);
            this._description.Location = new System.Drawing.Point(6, 37);
            this._description.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._description.Name = "_description";
            this._description.Size = new System.Drawing.Size(352, 20);
            this._description.TabIndex = 1;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._description, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._ok, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this._cancel, 2, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(364, 103);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // NewInterpreter
            // 
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(364, 103);
            this.Controls.Add(this.tableLayoutPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NewInterpreter";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add Python Interpreter";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox _description;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}