namespace Microsoft.PythonTools.Options {
    partial class GenerateIntellisenseDbDialog {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GenerateIntellisenseDbDialog));
            this._builtinDb = new System.Windows.Forms.RadioButton();
            this._fullDb = new System.Windows.Forms.RadioButton();
            this._okButton = new System.Windows.Forms.Button();
            this._cancelButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _builtinDb
            // 
            this._builtinDb.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this._builtinDb, 4);
            this._builtinDb.Location = new System.Drawing.Point(6, 37);
            this._builtinDb.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._builtinDb.Name = "_builtinDb";
            this._builtinDb.Size = new System.Drawing.Size(171, 17);
            this._builtinDb.TabIndex = 1;
            this._builtinDb.Text = "Generate built-in database only";
            this._builtinDb.UseVisualStyleBackColor = true;
            // 
            // _fullDb
            // 
            this._fullDb.AutoSize = true;
            this._fullDb.Checked = true;
            this.tableLayoutPanel1.SetColumnSpan(this._fullDb, 4);
            this._fullDb.Location = new System.Drawing.Point(6, 129);
            this._fullDb.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._fullDb.Name = "_fullDb";
            this._fullDb.Size = new System.Drawing.Size(244, 17);
            this._fullDb.TabIndex = 3;
            this._fullDb.TabStop = true;
            this._fullDb.Text = "Generate built-in and standard library database";
            this._fullDb.UseVisualStyleBackColor = true;
            // 
            // _okButton
            // 
            this._okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._okButton.AutoSize = true;
            this._okButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._okButton.Location = new System.Drawing.Point(384, 221);
            this._okButton.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._okButton.Name = "_okButton";
            this._okButton.Padding = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this._okButton.Size = new System.Drawing.Size(69, 29);
            this._okButton.TabIndex = 5;
            this._okButton.Text = "&OK";
            this._okButton.UseVisualStyleBackColor = true;
            this._okButton.Click += new System.EventHandler(this.OkButtonClick);
            // 
            // _cancelButton
            // 
            this._cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._cancelButton.AutoSize = true;
            this._cancelButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancelButton.Location = new System.Drawing.Point(465, 221);
            this._cancelButton.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._cancelButton.Name = "_cancelButton";
            this._cancelButton.Padding = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this._cancelButton.Size = new System.Drawing.Size(70, 29);
            this._cancelButton.TabIndex = 6;
            this._cancelButton.Text = "&Cancel";
            this._cancelButton.UseVisualStyleBackColor = true;
            this._cancelButton.Click += new System.EventHandler(this._cancelButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.label1, 4);
            this.label1.Font = new System.Drawing.Font("Segoe UI", 7.875F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(6, 8);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(233, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "What database would you like to generate?";
            // 
            // textBox1
            // 
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tableLayoutPanel1.SetColumnSpan(this.textBox1, 3);
            this.textBox1.Location = new System.Drawing.Point(60, 70);
            this.textBox1.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(475, 43);
            this.textBox1.TabIndex = 2;
            this.textBox1.Text = "Generates the database for all of the built-in modules supported by the Python in" +
    "terpreter. This process typically takes just a few seconds.";
            // 
            // textBox2
            // 
            this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tableLayoutPanel1.SetColumnSpan(this.textBox2, 3);
            this.textBox2.Location = new System.Drawing.Point(60, 162);
            this.textBox2.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.textBox2.Multiline = true;
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(475, 43);
            this.textBox2.TabIndex = 4;
            this.textBox2.Text = resources.GetString("textBox2.Text");
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._cancelButton, 3, 5);
            this.tableLayoutPanel1.Controls.Add(this.textBox2, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this._okButton, 2, 5);
            this.tableLayoutPanel1.Controls.Add(this._builtinDb, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.textBox1, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this._fullDb, 0, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(12, 15);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(541, 258);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // GenerateIntellisenseDbDialog
            // 
            this.AcceptButton = this._okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancelButton;
            this.ClientSize = new System.Drawing.Size(565, 288);
            this.Controls.Add(this.tableLayoutPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GenerateIntellisenseDbDialog";
            this.Padding = new System.Windows.Forms.Padding(12, 15, 12, 15);
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Generate Intellisense Database";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RadioButton _builtinDb;
        private System.Windows.Forms.RadioButton _fullDb;
        private System.Windows.Forms.Button _okButton;
        private System.Windows.Forms.Button _cancelButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}