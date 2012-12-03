namespace Microsoft.PythonTools.ImportWizard {
    partial class ImportWizardDialog {
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
            System.Windows.Forms.Label label1;
            System.Windows.Forms.Label label2;
            System.Windows.Forms.Label label4;
            System.Windows.Forms.Label label5;
            System.Windows.Forms.Label label7;
            System.Windows.Forms.Label label8;
            System.Windows.Forms.Label label3;
            System.Windows.Forms.Label label6;
            System.Windows.Forms.Label label9;
            System.Windows.Forms.Label label10;
            System.Windows.Forms.Label label11;
            System.Windows.Forms.Label label12;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportWizardDialog));
            this.cancelButton = new System.Windows.Forms.Button();
            this.okButton = new System.Windows.Forms.Button();
            this.nextButton = new System.Windows.Forms.Button();
            this.backButton = new System.Windows.Forms.Button();
            this.step1Panel = new System.Windows.Forms.TableLayoutPanel();
            this.sourcePathTextBox = new System.Windows.Forms.TextBox();
            this.browsePathButton = new System.Windows.Forms.Button();
            this.browseSearchPathButton = new System.Windows.Forms.Button();
            this.searchPathTextBox = new System.Windows.Forms.TextBox();
            this.filterTextBox = new System.Windows.Forms.TextBox();
            this.iconPanel = new System.Windows.Forms.Panel();
            this.pythonImage = new System.Windows.Forms.PictureBox();
            this.step2Panel = new System.Windows.Forms.TableLayoutPanel();
            this.interpreterCombo = new System.Windows.Forms.ComboBox();
            this.startupFileList = new System.Windows.Forms.ListView();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            label5 = new System.Windows.Forms.Label();
            label7 = new System.Windows.Forms.Label();
            label8 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            label10 = new System.Windows.Forms.Label();
            label11 = new System.Windows.Forms.Label();
            label12 = new System.Windows.Forms.Label();
            flowLayoutPanel1.SuspendLayout();
            this.step1Panel.SuspendLayout();
            this.iconPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pythonImage)).BeginInit();
            this.step2Panel.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.AutoSize = true;
            flowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowLayoutPanel1.Controls.Add(this.cancelButton);
            flowLayoutPanel1.Controls.Add(this.okButton);
            flowLayoutPanel1.Controls.Add(this.nextButton);
            flowLayoutPanel1.Controls.Add(this.backButton);
            flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowLayoutPanel1.Location = new System.Drawing.Point(125, 385);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Padding = new System.Windows.Forms.Padding(3, 3, 6, 3);
            flowLayoutPanel1.Size = new System.Drawing.Size(504, 41);
            flowLayoutPanel1.TabIndex = 3;
            // 
            // cancelButton
            // 
            this.cancelButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(422, 6);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(70, 29);
            this.cancelButton.TabIndex = 3;
            this.cancelButton.Text = "&Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // okButton
            // 
            this.okButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(346, 6);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(70, 29);
            this.okButton.TabIndex = 2;
            this.okButton.Text = "&Finish";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Visible = false;
            // 
            // nextButton
            // 
            this.nextButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.nextButton.Location = new System.Drawing.Point(270, 6);
            this.nextButton.Name = "nextButton";
            this.nextButton.Size = new System.Drawing.Size(70, 29);
            this.nextButton.TabIndex = 1;
            this.nextButton.Text = "&Next";
            this.nextButton.UseVisualStyleBackColor = true;
            this.nextButton.Click += new System.EventHandler(this.nextButton_Click);
            // 
            // backButton
            // 
            this.backButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.backButton.Location = new System.Drawing.Point(194, 6);
            this.backButton.Name = "backButton";
            this.backButton.Size = new System.Drawing.Size(70, 29);
            this.backButton.TabIndex = 0;
            this.backButton.Text = "&Back";
            this.backButton.UseVisualStyleBackColor = true;
            this.backButton.Visible = false;
            this.backButton.Click += new System.EventHandler(this.backButton_Click);
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label1.Location = new System.Drawing.Point(9, 6);
            label1.Margin = new System.Windows.Forms.Padding(3, 0, 3, 12);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(330, 50);
            label1.TabIndex = 0;
            label1.Text = "Welcome to the Create New Project from Existing Python Code Wizard";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label2.ForeColor = System.Drawing.SystemColors.GrayText;
            label2.Location = new System.Drawing.Point(21, 83);
            label2.Margin = new System.Windows.Forms.Padding(15, 0, 3, 6);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(270, 15);
            label2.TabIndex = 2;
            label2.Text = "We won\'t move any files from where they are now.";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label4.ForeColor = System.Drawing.SystemColors.GrayText;
            label4.Location = new System.Drawing.Point(21, 237);
            label4.Margin = new System.Windows.Forms.Padding(15, 0, 3, 6);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(327, 30);
            label4.TabIndex = 8;
            label4.Text = "One on each line, and we\'ll make them relative to the project files for you.";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(9, 68);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(317, 15);
            label5.TabIndex = 1;
            label5.Text = "Enter or browse to the folder containing your Python code.";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new System.Drawing.Point(9, 222);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(245, 15);
            label7.TabIndex = 7;
            label7.Text = "Enter any search paths your project will need.";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new System.Drawing.Point(9, 145);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(182, 15);
            label8.TabIndex = 5;
            label8.Text = "Enter the filter for files to include.";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label3.Location = new System.Drawing.Point(9, 6);
            label3.Margin = new System.Windows.Forms.Padding(3, 0, 3, 12);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(330, 50);
            label3.TabIndex = 0;
            label3.Text = "Welcome to the Create New Project from Existing Python Code Wizard";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new System.Drawing.Point(9, 68);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(256, 15);
            label6.TabIndex = 1;
            label6.Text = "Select the Python interpreter and version to use";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label9.ForeColor = System.Drawing.SystemColors.GrayText;
            label9.Location = new System.Drawing.Point(21, 83);
            label9.Margin = new System.Windows.Forms.Padding(15, 0, 3, 6);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(287, 15);
            label9.TabIndex = 2;
            label9.Text = "This setting can be changed later in Project Properties";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new System.Drawing.Point(9, 145);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(222, 15);
            label10.TabIndex = 4;
            label10.Text = "Choose the file to run when F5 is pressed";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label11.ForeColor = System.Drawing.SystemColors.GrayText;
            label11.Location = new System.Drawing.Point(21, 165);
            label11.Margin = new System.Windows.Forms.Padding(15, 0, 3, 6);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(356, 30);
            label11.TabIndex = 5;
            label11.Text = "If it is not in this list, you can right-click any file in your project and choos" +
    "e \"Set as startup file\"";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label12.ForeColor = System.Drawing.SystemColors.GrayText;
            label12.Location = new System.Drawing.Point(21, 160);
            label12.Margin = new System.Windows.Forms.Padding(15, 0, 3, 6);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(258, 15);
            label12.TabIndex = 8;
            label12.Text = "Files with the .py extension are always included.";
            // 
            // step1Panel
            // 
            this.step1Panel.ColumnCount = 2;
            this.step1Panel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.step1Panel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.step1Panel.Controls.Add(label1, 0, 0);
            this.step1Panel.Controls.Add(label2, 0, 2);
            this.step1Panel.Controls.Add(this.sourcePathTextBox, 0, 3);
            this.step1Panel.Controls.Add(this.browsePathButton, 1, 3);
            this.step1Panel.Controls.Add(label4, 0, 8);
            this.step1Panel.Controls.Add(label5, 0, 1);
            this.step1Panel.Controls.Add(label7, 0, 7);
            this.step1Panel.Controls.Add(this.browseSearchPathButton, 1, 9);
            this.step1Panel.Controls.Add(this.searchPathTextBox, 0, 9);
            this.step1Panel.Controls.Add(label8, 0, 4);
            this.step1Panel.Controls.Add(this.filterTextBox, 0, 6);
            this.step1Panel.Controls.Add(label12, 0, 5);
            this.step1Panel.Location = new System.Drawing.Point(125, 0);
            this.step1Panel.Name = "step1Panel";
            this.step1Panel.Padding = new System.Windows.Forms.Padding(6);
            this.step1Panel.RowCount = 10;
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step1Panel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.step1Panel.Size = new System.Drawing.Size(403, 285);
            this.step1Panel.TabIndex = 1;
            // 
            // sourcePathTextBox
            // 
            this.sourcePathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sourcePathTextBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.sourcePathTextBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.sourcePathTextBox.Location = new System.Drawing.Point(9, 107);
            this.sourcePathTextBox.Margin = new System.Windows.Forms.Padding(3, 3, 3, 15);
            this.sourcePathTextBox.Name = "sourcePathTextBox";
            this.sourcePathTextBox.Size = new System.Drawing.Size(353, 23);
            this.sourcePathTextBox.TabIndex = 3;
            // 
            // browsePathButton
            // 
            this.browsePathButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browsePathButton.AutoSize = true;
            this.browsePathButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.browsePathButton.Location = new System.Drawing.Point(368, 107);
            this.browsePathButton.Name = "browsePathButton";
            this.browsePathButton.Size = new System.Drawing.Size(26, 25);
            this.browsePathButton.TabIndex = 4;
            this.browsePathButton.Text = "...";
            this.browsePathButton.UseVisualStyleBackColor = true;
            this.browsePathButton.Click += new System.EventHandler(this.browsePathButton_Click);
            // 
            // browseSearchPathButton
            // 
            this.browseSearchPathButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browseSearchPathButton.AutoSize = true;
            this.browseSearchPathButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.browseSearchPathButton.Location = new System.Drawing.Point(368, 276);
            this.browseSearchPathButton.Name = "browseSearchPathButton";
            this.browseSearchPathButton.Size = new System.Drawing.Size(26, 14);
            this.browseSearchPathButton.TabIndex = 10;
            this.browseSearchPathButton.Text = "...";
            this.browseSearchPathButton.UseVisualStyleBackColor = true;
            this.browseSearchPathButton.Click += new System.EventHandler(this.browseSearchPathButton_Click);
            // 
            // searchPathTextBox
            // 
            this.searchPathTextBox.AcceptsReturn = true;
            this.searchPathTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchPathTextBox.Location = new System.Drawing.Point(9, 276);
            this.searchPathTextBox.Multiline = true;
            this.searchPathTextBox.Name = "searchPathTextBox";
            this.searchPathTextBox.Size = new System.Drawing.Size(353, 14);
            this.searchPathTextBox.TabIndex = 9;
            // 
            // filterTextBox
            // 
            this.filterTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.filterTextBox.Location = new System.Drawing.Point(9, 184);
            this.filterTextBox.Margin = new System.Windows.Forms.Padding(3, 3, 3, 15);
            this.filterTextBox.Name = "filterTextBox";
            this.filterTextBox.Size = new System.Drawing.Size(353, 23);
            this.filterTextBox.TabIndex = 6;
            this.filterTextBox.Text = "*.pyw;*.txt;*.htm;*.html;*.css;*.png;*.jpg;*.gif;*.bmp;*.ico;*.svg";
            // 
            // iconPanel
            // 
            this.iconPanel.BackColor = System.Drawing.SystemColors.Control;
            this.iconPanel.Controls.Add(this.pythonImage);
            this.iconPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.iconPanel.Location = new System.Drawing.Point(0, 0);
            this.iconPanel.Margin = new System.Windows.Forms.Padding(0);
            this.iconPanel.Name = "iconPanel";
            this.iconPanel.Size = new System.Drawing.Size(125, 426);
            this.iconPanel.TabIndex = 0;
            // 
            // pythonImage
            // 
            this.pythonImage.Dock = System.Windows.Forms.DockStyle.Top;
            this.pythonImage.Image = ((System.Drawing.Image)(resources.GetObject("pythonImage.Image")));
            this.pythonImage.Location = new System.Drawing.Point(0, 0);
            this.pythonImage.Name = "pythonImage";
            this.pythonImage.Size = new System.Drawing.Size(125, 127);
            this.pythonImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pythonImage.TabIndex = 0;
            this.pythonImage.TabStop = false;
            // 
            // step2Panel
            // 
            this.step2Panel.ColumnCount = 1;
            this.step2Panel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.step2Panel.Controls.Add(label3, 0, 0);
            this.step2Panel.Controls.Add(label9, 0, 2);
            this.step2Panel.Controls.Add(label6, 0, 1);
            this.step2Panel.Controls.Add(label11, 0, 5);
            this.step2Panel.Controls.Add(label10, 0, 4);
            this.step2Panel.Controls.Add(this.interpreterCombo, 0, 3);
            this.step2Panel.Controls.Add(this.startupFileList, 0, 6);
            this.step2Panel.Location = new System.Drawing.Point(189, 53);
            this.step2Panel.Name = "step2Panel";
            this.step2Panel.Padding = new System.Windows.Forms.Padding(6);
            this.step2Panel.RowCount = 7;
            this.step2Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step2Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step2Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step2Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step2Panel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.step2Panel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.step2Panel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.step2Panel.Size = new System.Drawing.Size(391, 303);
            this.step2Panel.TabIndex = 2;
            // 
            // interpreterCombo
            // 
            this.interpreterCombo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.interpreterCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.interpreterCombo.FormattingEnabled = true;
            this.interpreterCombo.Location = new System.Drawing.Point(9, 107);
            this.interpreterCombo.Margin = new System.Windows.Forms.Padding(3, 3, 3, 15);
            this.interpreterCombo.Name = "interpreterCombo";
            this.interpreterCombo.Size = new System.Drawing.Size(373, 23);
            this.interpreterCombo.TabIndex = 3;
            // 
            // startupFileList
            // 
            this.startupFileList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.startupFileList.FullRowSelect = true;
            this.startupFileList.HideSelection = false;
            this.startupFileList.Location = new System.Drawing.Point(9, 204);
            this.startupFileList.Name = "startupFileList";
            this.startupFileList.Size = new System.Drawing.Size(373, 90);
            this.startupFileList.SmallImageList = this.imageList1;
            this.startupFileList.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.startupFileList.TabIndex = 6;
            this.startupFileList.UseCompatibleStateImageBehavior = false;
            this.startupFileList.View = System.Windows.Forms.View.List;
            this.startupFileList.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.startupFileList_ItemSelectionChanged);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Magenta;
            this.imageList1.Images.SetKeyName(0, "PythonFile");
            this.imageList1.Images.SetKeyName(1, "PythonStartupFile");
            // 
            // ImportWizardDialog
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(629, 426);
            this.ControlBox = false;
            this.Controls.Add(this.step2Panel);
            this.Controls.Add(this.step1Panel);
            this.Controls.Add(flowLayoutPanel1);
            this.Controls.Add(this.iconPanel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "ImportWizardDialog";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "Create New Project from Existing Python Code";
            flowLayoutPanel1.ResumeLayout(false);
            this.step1Panel.ResumeLayout(false);
            this.step1Panel.PerformLayout();
            this.iconPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pythonImage)).EndInit();
            this.step2Panel.ResumeLayout(false);
            this.step2Panel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.TextBox sourcePathTextBox;
        private System.Windows.Forms.Button browsePathButton;
        private System.Windows.Forms.Button browseSearchPathButton;
        private System.Windows.Forms.TextBox searchPathTextBox;
        private System.Windows.Forms.TextBox filterTextBox;
        private System.Windows.Forms.Panel iconPanel;
        private System.Windows.Forms.PictureBox pythonImage;
        private System.Windows.Forms.TableLayoutPanel step1Panel;
        private System.Windows.Forms.TableLayoutPanel step2Panel;
        private System.Windows.Forms.Button backButton;
        private System.Windows.Forms.Button nextButton;
        private System.Windows.Forms.ComboBox interpreterCombo;
        private System.Windows.Forms.ListView startupFileList;
        private System.Windows.Forms.ImageList imageList1;
    }
}