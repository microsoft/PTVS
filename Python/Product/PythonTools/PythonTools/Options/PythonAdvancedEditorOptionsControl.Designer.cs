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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonAdvancedEditorOptionsControl));
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
            this._autoListIdentifiers = new System.Windows.Forms.CheckBox();
            this._miscOptionsGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this._outliningOnOpen = new System.Windows.Forms.CheckBox();
            this._pasteRemovesReplPrompts = new System.Windows.Forms.CheckBox();
            this._colorNames = new System.Windows.Forms.CheckBox();
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
            resources.ApplyResources(this._completionCommitedBy, "_completionCommitedBy");
            this._completionCommitedBy.Name = "_completionCommitedBy";
            // 
            // _completionCommitedByLabel
            // 
            resources.ApplyResources(this._completionCommitedByLabel, "_completionCommitedByLabel");
            this._completionCommitedByLabel.AutoEllipsis = true;
            this._completionCommitedByLabel.Name = "_completionCommitedByLabel";
            // 
            // _enterCommits
            // 
            resources.ApplyResources(this._enterCommits, "_enterCommits");
            this._enterCommits.AutoEllipsis = true;
            this._enterCommits.Name = "_enterCommits";
            this._enterCommits.UseVisualStyleBackColor = true;
            // 
            // _intersectMembers
            // 
            resources.ApplyResources(this._intersectMembers, "_intersectMembers");
            this._intersectMembers.AutoEllipsis = true;
            this._intersectMembers.Name = "_intersectMembers";
            this._intersectMembers.UseVisualStyleBackColor = true;
            // 
            // _selectionInCompletionGroupBox
            // 
            resources.ApplyResources(this._selectionInCompletionGroupBox, "_selectionInCompletionGroupBox");
            this._selectionInCompletionGroupBox.Controls.Add(this.tableLayoutPanel3);
            this._selectionInCompletionGroupBox.Name = "_selectionInCompletionGroupBox";
            this._selectionInCompletionGroupBox.TabStop = false;
            // 
            // tableLayoutPanel3
            // 
            resources.ApplyResources(this.tableLayoutPanel3, "tableLayoutPanel3");
            this.tableLayoutPanel3.Controls.Add(this._completionCommitedByLabel, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._completionCommitedBy, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._enterCommits, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this._newLineAfterCompleteCompletion, 0, 3);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            // 
            // _newLineAfterCompleteCompletion
            // 
            resources.ApplyResources(this._newLineAfterCompleteCompletion, "_newLineAfterCompleteCompletion");
            this._newLineAfterCompleteCompletion.AutoEllipsis = true;
            this._newLineAfterCompleteCompletion.Name = "_newLineAfterCompleteCompletion";
            this._newLineAfterCompleteCompletion.UseVisualStyleBackColor = true;
            // 
            // _completionResultsGroupBox
            // 
            resources.ApplyResources(this._completionResultsGroupBox, "_completionResultsGroupBox");
            this._completionResultsGroupBox.Controls.Add(this.tableLayoutPanel2);
            this._completionResultsGroupBox.Name = "_completionResultsGroupBox";
            this._completionResultsGroupBox.TabStop = false;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this._intersectMembers, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._filterCompletions, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._autoListIdentifiers, 0, 2);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _filterCompletions
            // 
            resources.ApplyResources(this._filterCompletions, "_filterCompletions");
            this._filterCompletions.AutoEllipsis = true;
            this._filterCompletions.Name = "_filterCompletions";
            this._filterCompletions.UseVisualStyleBackColor = true;
            // 
            // _autoListIdentifiers
            // 
            resources.ApplyResources(this._autoListIdentifiers, "_autoListIdentifiers");
            this._autoListIdentifiers.AutoEllipsis = true;
            this._autoListIdentifiers.Name = "_autoListIdentifiers";
            this._autoListIdentifiers.UseVisualStyleBackColor = true;
            // 
            // _miscOptionsGroupBox
            // 
            resources.ApplyResources(this._miscOptionsGroupBox, "_miscOptionsGroupBox");
            this._miscOptionsGroupBox.Controls.Add(this.tableLayoutPanel4);
            this._miscOptionsGroupBox.Name = "_miscOptionsGroupBox";
            this._miscOptionsGroupBox.TabStop = false;
            // 
            // tableLayoutPanel4
            // 
            resources.ApplyResources(this.tableLayoutPanel4, "tableLayoutPanel4");
            this.tableLayoutPanel4.Controls.Add(this._outliningOnOpen, 0, 0);
            this.tableLayoutPanel4.Controls.Add(this._pasteRemovesReplPrompts, 0, 1);
            this.tableLayoutPanel4.Controls.Add(this._colorNames, 0, 2);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            // 
            // _outliningOnOpen
            // 
            resources.ApplyResources(this._outliningOnOpen, "_outliningOnOpen");
            this._outliningOnOpen.AutoEllipsis = true;
            this._outliningOnOpen.Name = "_outliningOnOpen";
            this._outliningOnOpen.UseVisualStyleBackColor = true;
            // 
            // _pasteRemovesReplPrompts
            // 
            resources.ApplyResources(this._pasteRemovesReplPrompts, "_pasteRemovesReplPrompts");
            this._pasteRemovesReplPrompts.AutoEllipsis = true;
            this._pasteRemovesReplPrompts.Name = "_pasteRemovesReplPrompts";
            this._pasteRemovesReplPrompts.UseVisualStyleBackColor = true;
            // 
            // _colorNames
            // 
            resources.ApplyResources(this._colorNames, "_colorNames");
            this._colorNames.AutoEllipsis = true;
            this._colorNames.Name = "_colorNames";
            this._colorNames.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._completionResultsGroupBox, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._selectionInCompletionGroupBox, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._miscOptionsGroupBox, 0, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // PythonAdvancedEditorOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PythonAdvancedEditorOptionsControl";
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
            this.PerformLayout();

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
        private System.Windows.Forms.CheckBox _colorNames;
        private System.Windows.Forms.CheckBox _autoListIdentifiers;
    }
}
