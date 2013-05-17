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
            this._completionCommitedBy = new System.Windows.Forms.TextBox();
            this._completionCommitedByLabel = new System.Windows.Forms.Label();
            this._enterCommits = new System.Windows.Forms.CheckBox();
            this._intersectMembers = new System.Windows.Forms.CheckBox();
            this._selectionInCompletionGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this._newLineAfterCompleteCompletion = new System.Windows.Forms.CheckBox();
            this._completionResultsGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._filterCompletions = new System.Windows.Forms.CheckBox();
            this._miscOptionsGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this._outliningOnOpen = new System.Windows.Forms.CheckBox();
            this._pasteRemovesReplPrompts = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._selectionInCompletionGroupBox.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this._completionResultsGroupBox.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this._miscOptionsGroupBox.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _completionCommitedBy
            // 
            this._completionCommitedBy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._completionCommitedBy.Location = new System.Drawing.Point(6, 16);
            this._completionCommitedBy.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._completionCommitedBy.Name = "_completionCommitedBy";
            this._completionCommitedBy.Size = new System.Drawing.Size(458, 20);
            this._completionCommitedBy.TabIndex = 1;
            this._completionCommitedBy.TextChanged += new System.EventHandler(this._completionCommitedBy_TextChanged);
            // 
            // _completionCommitedByLabel
            // 
            this._completionCommitedByLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._completionCommitedByLabel.AutoEllipsis = true;
            this._completionCommitedByLabel.AutoSize = true;
            this._completionCommitedByLabel.Location = new System.Drawing.Point(6, 0);
            this._completionCommitedByLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._completionCommitedByLabel.Name = "_completionCommitedByLabel";
            this._completionCommitedByLabel.Size = new System.Drawing.Size(219, 13);
            this._completionCommitedByLabel.TabIndex = 0;
            this._completionCommitedByLabel.Text = "Committed by &typing the following characters:";
            this._completionCommitedByLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _enterCommits
            // 
            this._enterCommits.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._enterCommits.AutoSize = true;
            this._enterCommits.Location = new System.Drawing.Point(6, 42);
            this._enterCommits.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._enterCommits.Name = "_enterCommits";
            this._enterCommits.Size = new System.Drawing.Size(182, 17);
            this._enterCommits.TabIndex = 2;
            this._enterCommits.Text = "&Enter commits current completion";
            this._enterCommits.UseVisualStyleBackColor = true;
            this._enterCommits.CheckedChanged += new System.EventHandler(this._enterCommits_CheckedChanged);
            // 
            // _intersectMembers
            // 
            this._intersectMembers.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._intersectMembers.AutoSize = true;
            this._intersectMembers.Location = new System.Drawing.Point(6, 3);
            this._intersectMembers.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._intersectMembers.Name = "_intersectMembers";
            this._intersectMembers.Size = new System.Drawing.Size(272, 17);
            this._intersectMembers.TabIndex = 0;
            this._intersectMembers.Text = "Member completion displays &intersection of members";
            this._intersectMembers.UseVisualStyleBackColor = true;
            this._intersectMembers.CheckedChanged += new System.EventHandler(this._intersectMembers_CheckedChanged);
            // 
            // _selectionInCompletionGroupBox
            // 
            this._selectionInCompletionGroupBox.AutoSize = true;
            this._selectionInCompletionGroupBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._selectionInCompletionGroupBox.Controls.Add(this.tableLayoutPanel3);
            this._selectionInCompletionGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._selectionInCompletionGroupBox.Location = new System.Drawing.Point(6, 74);
            this._selectionInCompletionGroupBox.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._selectionInCompletionGroupBox.Name = "_selectionInCompletionGroupBox";
            this._selectionInCompletionGroupBox.Size = new System.Drawing.Size(476, 104);
            this._selectionInCompletionGroupBox.TabIndex = 1;
            this._selectionInCompletionGroupBox.TabStop = false;
            this._selectionInCompletionGroupBox.Text = "Selection in Completion List";
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.AutoSize = true;
            this.tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel3.ColumnCount = 1;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.Controls.Add(this._completionCommitedByLabel, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._completionCommitedBy, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._enterCommits, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this._newLineAfterCompleteCompletion, 0, 3);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 4;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.Size = new System.Drawing.Size(470, 85);
            this.tableLayoutPanel3.TabIndex = 0;
            // 
            // _newLineAfterCompleteCompletion
            // 
            this._newLineAfterCompleteCompletion.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._newLineAfterCompleteCompletion.AutoSize = true;
            this._newLineAfterCompleteCompletion.Location = new System.Drawing.Point(6, 65);
            this._newLineAfterCompleteCompletion.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._newLineAfterCompleteCompletion.Name = "_newLineAfterCompleteCompletion";
            this._newLineAfterCompleteCompletion.Size = new System.Drawing.Size(250, 17);
            this._newLineAfterCompleteCompletion.TabIndex = 3;
            this._newLineAfterCompleteCompletion.Text = "&Add new line on enter at end of fully typed word";
            this._newLineAfterCompleteCompletion.UseVisualStyleBackColor = true;
            this._newLineAfterCompleteCompletion.CheckedChanged += new System.EventHandler(this._newLineAfterCompleteCompletion_CheckedChanged);
            // 
            // _completionResultsGroupBox
            // 
            this._completionResultsGroupBox.AutoSize = true;
            this._completionResultsGroupBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._completionResultsGroupBox.Controls.Add(this.tableLayoutPanel2);
            this._completionResultsGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._completionResultsGroupBox.Location = new System.Drawing.Point(6, 3);
            this._completionResultsGroupBox.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._completionResultsGroupBox.Name = "_completionResultsGroupBox";
            this._completionResultsGroupBox.Size = new System.Drawing.Size(476, 65);
            this._completionResultsGroupBox.TabIndex = 0;
            this._completionResultsGroupBox.TabStop = false;
            this._completionResultsGroupBox.Text = "Completion Results";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Controls.Add(this._intersectMembers, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._filterCompletions, 0, 1);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 2;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(470, 46);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // _filterCompletions
            // 
            this._filterCompletions.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterCompletions.AutoSize = true;
            this._filterCompletions.Location = new System.Drawing.Point(6, 26);
            this._filterCompletions.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._filterCompletions.Name = "_filterCompletions";
            this._filterCompletions.Size = new System.Drawing.Size(173, 17);
            this._filterCompletions.TabIndex = 1;
            this._filterCompletions.Text = "&Filter list based on search string";
            this._filterCompletions.UseVisualStyleBackColor = true;
            this._filterCompletions.CheckedChanged += new System.EventHandler(this._filterCompletions_CheckedChanged);
            // 
            // _miscOptionsGroupBox
            // 
            this._miscOptionsGroupBox.AutoSize = true;
            this._miscOptionsGroupBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._miscOptionsGroupBox.Controls.Add(this.tableLayoutPanel4);
            this._miscOptionsGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._miscOptionsGroupBox.Location = new System.Drawing.Point(6, 184);
            this._miscOptionsGroupBox.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._miscOptionsGroupBox.Name = "_miscOptionsGroupBox";
            this._miscOptionsGroupBox.Size = new System.Drawing.Size(476, 65);
            this._miscOptionsGroupBox.TabIndex = 2;
            this._miscOptionsGroupBox.TabStop = false;
            this._miscOptionsGroupBox.Text = "Miscellaneous Options";
            // 
            // tableLayoutPanel4
            // 
            this.tableLayoutPanel4.AutoSize = true;
            this.tableLayoutPanel4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel4.ColumnCount = 1;
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel4.Controls.Add(this._outliningOnOpen, 0, 0);
            this.tableLayoutPanel4.Controls.Add(this._pasteRemovesReplPrompts, 0, 1);
            this.tableLayoutPanel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel4.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 2;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel4.Size = new System.Drawing.Size(470, 46);
            this.tableLayoutPanel4.TabIndex = 0;
            // 
            // _outliningOnOpen
            // 
            this._outliningOnOpen.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._outliningOnOpen.AutoSize = true;
            this._outliningOnOpen.Location = new System.Drawing.Point(6, 3);
            this._outliningOnOpen.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._outliningOnOpen.Name = "_outliningOnOpen";
            this._outliningOnOpen.Size = new System.Drawing.Size(199, 17);
            this._outliningOnOpen.TabIndex = 0;
            this._outliningOnOpen.Text = "Enter &outlining mode when files open";
            this._outliningOnOpen.UseVisualStyleBackColor = true;
            this._outliningOnOpen.CheckedChanged += new System.EventHandler(this._outliningOnOpen_CheckedChanged);
            // 
            // _pasteRemovesReplPrompts
            // 
            this._pasteRemovesReplPrompts.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._pasteRemovesReplPrompts.AutoSize = true;
            this._pasteRemovesReplPrompts.Location = new System.Drawing.Point(6, 26);
            this._pasteRemovesReplPrompts.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._pasteRemovesReplPrompts.Name = "_pasteRemovesReplPrompts";
            this._pasteRemovesReplPrompts.Size = new System.Drawing.Size(167, 17);
            this._pasteRemovesReplPrompts.TabIndex = 1;
            this._pasteRemovesReplPrompts.Text = "&Paste removes REPL prompts";
            this._pasteRemovesReplPrompts.UseVisualStyleBackColor = true;
            this._pasteRemovesReplPrompts.CheckedChanged += new System.EventHandler(this._pasteRemovesReplPrompts_CheckedChanged);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this._completionResultsGroupBox, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._selectionInCompletionGroupBox, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._miscOptionsGroupBox, 0, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(488, 270);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // PythonAdvancedEditorOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonAdvancedEditorOptionsControl";
            this.Size = new System.Drawing.Size(488, 270);
            this._selectionInCompletionGroupBox.ResumeLayout(false);
            this._selectionInCompletionGroupBox.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this._completionResultsGroupBox.ResumeLayout(false);
            this._completionResultsGroupBox.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this._miscOptionsGroupBox.ResumeLayout(false);
            this._miscOptionsGroupBox.PerformLayout();
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox _enterCommits;
        private System.Windows.Forms.CheckBox _intersectMembers;
        private System.Windows.Forms.TextBox _completionCommitedBy;
        private System.Windows.Forms.Label _completionCommitedByLabel;
        private System.Windows.Forms.GroupBox _selectionInCompletionGroupBox;
        private System.Windows.Forms.GroupBox _completionResultsGroupBox;
        private System.Windows.Forms.CheckBox _newLineAfterCompleteCompletion;
        private System.Windows.Forms.GroupBox _miscOptionsGroupBox;
        private System.Windows.Forms.CheckBox _outliningOnOpen;
        private System.Windows.Forms.CheckBox _pasteRemovesReplPrompts;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckBox _filterCompletions;
    }
}
