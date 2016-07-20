namespace Microsoft.PythonTools.Options {
    partial class PythonInteractiveOptionsControl {
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.Label scriptsLabel;
            System.Windows.Forms.GroupBox completionModeGroup;
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.neverEvaluateButton = new System.Windows.Forms.RadioButton();
            this.evaluateNoCallsButton = new System.Windows.Forms.RadioButton();
            this.alwaysEvaluateButton = new System.Windows.Forms.RadioButton();
            this.liveCompletionsOnlyCheckBox = new System.Windows.Forms.CheckBox();
            this._tooltips = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.scriptsTextBox = new System.Windows.Forms.TextBox();
            this.browseScriptsButton = new System.Windows.Forms.Button();
            this.useSmartHistoryCheckBox = new System.Windows.Forms.CheckBox();
            scriptsLabel = new System.Windows.Forms.Label();
            completionModeGroup = new System.Windows.Forms.GroupBox();
            completionModeGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // scriptsLabel
            // 
            scriptsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            scriptsLabel.AutoSize = true;
            scriptsLabel.Location = new System.Drawing.Point(3, 8);
            scriptsLabel.Name = "scriptsLabel";
            scriptsLabel.Size = new System.Drawing.Size(42, 13);
            scriptsLabel.TabIndex = 0;
            scriptsLabel.Text = "Scripts:";
            // 
            // completionModeGroup
            // 
            completionModeGroup.AutoSize = true;
            completionModeGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.SetColumnSpan(completionModeGroup, 3);
            completionModeGroup.Controls.Add(this.tableLayoutPanel2);
            completionModeGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            completionModeGroup.Location = new System.Drawing.Point(3, 55);
            completionModeGroup.Name = "completionModeGroup";
            completionModeGroup.Size = new System.Drawing.Size(485, 105);
            completionModeGroup.TabIndex = 4;
            completionModeGroup.TabStop = false;
            completionModeGroup.Text = "Completion Mode";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Controls.Add(this.neverEvaluateButton, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.evaluateNoCallsButton, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.alwaysEvaluateButton, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.liveCompletionsOnlyCheckBox, 0, 3);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(479, 86);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // neverEvaluateButton
            // 
            this.neverEvaluateButton.AutoSize = true;
            this.neverEvaluateButton.Location = new System.Drawing.Point(3, 3);
            this.neverEvaluateButton.Name = "neverEvaluateButton";
            this.neverEvaluateButton.Size = new System.Drawing.Size(156, 17);
            this.neverEvaluateButton.TabIndex = 0;
            this.neverEvaluateButton.TabStop = true;
            this.neverEvaluateButton.Text = "&Never evaluate expressions";
            this.neverEvaluateButton.UseVisualStyleBackColor = true;
            this.neverEvaluateButton.CheckedChanged += new System.EventHandler(this.CompletionMode_CheckedChanged);
            // 
            // evaluateNoCallsButton
            // 
            this.evaluateNoCallsButton.AutoSize = true;
            this.evaluateNoCallsButton.Location = new System.Drawing.Point(3, 23);
            this.evaluateNoCallsButton.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.evaluateNoCallsButton.Name = "evaluateNoCallsButton";
            this.evaluateNoCallsButton.Size = new System.Drawing.Size(250, 17);
            this.evaluateNoCallsButton.TabIndex = 1;
            this.evaluateNoCallsButton.TabStop = true;
            this.evaluateNoCallsButton.Text = "Only evaluate expressions without function &calls";
            this.evaluateNoCallsButton.UseVisualStyleBackColor = true;
            this.evaluateNoCallsButton.CheckedChanged += new System.EventHandler(this.CompletionMode_CheckedChanged);
            // 
            // alwaysEvaluateButton
            // 
            this.alwaysEvaluateButton.AutoSize = true;
            this.alwaysEvaluateButton.Location = new System.Drawing.Point(3, 43);
            this.alwaysEvaluateButton.Name = "alwaysEvaluateButton";
            this.alwaysEvaluateButton.Size = new System.Drawing.Size(138, 17);
            this.alwaysEvaluateButton.TabIndex = 2;
            this.alwaysEvaluateButton.TabStop = true;
            this.alwaysEvaluateButton.Text = "&Evaluate all expressions";
            this.alwaysEvaluateButton.UseVisualStyleBackColor = true;
            this.alwaysEvaluateButton.CheckedChanged += new System.EventHandler(this.CompletionMode_CheckedChanged);
            // 
            // liveCompletionsOnlyCheckBox
            // 
            this.liveCompletionsOnlyCheckBox.AutoSize = true;
            this.liveCompletionsOnlyCheckBox.Location = new System.Drawing.Point(3, 66);
            this.liveCompletionsOnlyCheckBox.Name = "liveCompletionsOnlyCheckBox";
            this.liveCompletionsOnlyCheckBox.Size = new System.Drawing.Size(175, 17);
            this.liveCompletionsOnlyCheckBox.TabIndex = 3;
            this.liveCompletionsOnlyCheckBox.Text = "Hide &static analysis suggestions";
            this.liveCompletionsOnlyCheckBox.UseVisualStyleBackColor = true;
            this.liveCompletionsOnlyCheckBox.CheckedChanged += new System.EventHandler(this.LiveCompletionsOnly_CheckedChanged);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(scriptsLabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.scriptsTextBox, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.browseScriptsButton, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.useSmartHistoryCheckBox, 0, 1);
            this.tableLayoutPanel1.Controls.Add(completionModeGroup, 0, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(491, 315);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // scriptsTextBox
            // 
            this.scriptsTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.scriptsTextBox.Location = new System.Drawing.Point(51, 4);
            this.scriptsTextBox.Name = "scriptsTextBox";
            this.scriptsTextBox.Size = new System.Drawing.Size(405, 20);
            this.scriptsTextBox.TabIndex = 1;
            this.scriptsTextBox.TextChanged += new System.EventHandler(this.Scripts_TextChanged);
            // 
            // browseScriptsButton
            // 
            this.browseScriptsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.browseScriptsButton.AutoSize = true;
            this.browseScriptsButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.browseScriptsButton.Location = new System.Drawing.Point(462, 3);
            this.browseScriptsButton.Name = "browseScriptsButton";
            this.browseScriptsButton.Size = new System.Drawing.Size(26, 23);
            this.browseScriptsButton.TabIndex = 2;
            this.browseScriptsButton.Text = "...";
            this.browseScriptsButton.UseVisualStyleBackColor = true;
            this.browseScriptsButton.Click += new System.EventHandler(this.browseScriptsButton_Click);
            // 
            // useSmartHistoryCheckBox
            // 
            this.useSmartHistoryCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.useSmartHistoryCheckBox.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.useSmartHistoryCheckBox, 3);
            this.useSmartHistoryCheckBox.Location = new System.Drawing.Point(3, 32);
            this.useSmartHistoryCheckBox.Name = "useSmartHistoryCheckBox";
            this.useSmartHistoryCheckBox.Size = new System.Drawing.Size(182, 17);
            this.useSmartHistoryCheckBox.TabIndex = 3;
            this.useSmartHistoryCheckBox.Text = "Up/down arrows navigate &history";
            this.useSmartHistoryCheckBox.UseVisualStyleBackColor = true;
            this.useSmartHistoryCheckBox.CheckedChanged += new System.EventHandler(this.UseSmartHistory_CheckedChanged);
            // 
            // PythonInteractiveOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonInteractiveOptionsControl";
            this.Size = new System.Drawing.Size(491, 315);
            completionModeGroup.ResumeLayout(false);
            completionModeGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ToolTip _tooltips;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox scriptsTextBox;
        private System.Windows.Forms.Button browseScriptsButton;
        private System.Windows.Forms.CheckBox useSmartHistoryCheckBox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.RadioButton neverEvaluateButton;
        private System.Windows.Forms.RadioButton evaluateNoCallsButton;
        private System.Windows.Forms.RadioButton alwaysEvaluateButton;
        private System.Windows.Forms.CheckBox liveCompletionsOnlyCheckBox;
    }
}
