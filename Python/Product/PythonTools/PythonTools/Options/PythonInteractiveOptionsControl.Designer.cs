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
            this._smartReplHistory = new System.Windows.Forms.CheckBox();
            this._completionModeGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel5 = new System.Windows.Forms.TableLayoutPanel();
            this._evalNever = new System.Windows.Forms.RadioButton();
            this._evalAlways = new System.Windows.Forms.RadioButton();
            this._evalNoCalls = new System.Windows.Forms.RadioButton();
            this._inlinePrompts = new System.Windows.Forms.CheckBox();
            this._useUserDefinedPrompts = new System.Windows.Forms.CheckBox();
            this._priPromptLabel = new System.Windows.Forms.Label();
            this._priPrompt = new System.Windows.Forms.TextBox();
            this._secPromptLabel = new System.Windows.Forms.Label();
            this._secPrompt = new System.Windows.Forms.TextBox();
            this._tooltips = new System.Windows.Forms.ToolTip(this.components);
            this._startupScript = new System.Windows.Forms.TextBox();
            this._startupScriptButton = new System.Windows.Forms.Button();
            this._startScriptLabel = new System.Windows.Forms.Label();
            this._showSettingsForLabel = new System.Windows.Forms.Label();
            this._showSettingsFor = new System.Windows.Forms.ComboBox();
            this._executionModeLabel = new System.Windows.Forms.Label();
            this._executionMode = new System.Windows.Forms.ComboBox();
            this._interpOptionsLabel = new System.Windows.Forms.Label();
            this._interpreterOptions = new System.Windows.Forms.TextBox();
            this._enableAttach = new System.Windows.Forms.CheckBox();
            this._liveCompletionsOnly = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this._completionModeGroup.SuspendLayout();
            this.tableLayoutPanel5.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // _smartReplHistory
            // 
            this._smartReplHistory.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._smartReplHistory.AutoSize = true;
            this._smartReplHistory.Location = new System.Drawing.Point(6, 3);
            this._smartReplHistory.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._smartReplHistory.Name = "_smartReplHistory";
            this._smartReplHistory.Size = new System.Drawing.Size(160, 17);
            this._smartReplHistory.TabIndex = 0;
            this._smartReplHistory.Text = "Arrow Keys use smart &history";
            this._smartReplHistory.UseVisualStyleBackColor = true;
            this._smartReplHistory.CheckedChanged += new System.EventHandler(this._smartReplHistory_CheckedChanged);
            // 
            // _completionModeGroup
            // 
            this._completionModeGroup.AutoSize = true;
            this._completionModeGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._completionModeGroup.Controls.Add(this.tableLayoutPanel5);
            this._completionModeGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._completionModeGroup.Location = new System.Drawing.Point(6, 194);
            this._completionModeGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._completionModeGroup.Name = "_completionModeGroup";
            this._completionModeGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._completionModeGroup.Size = new System.Drawing.Size(479, 98);
            this._completionModeGroup.TabIndex = 3;
            this._completionModeGroup.TabStop = false;
            this._completionModeGroup.Text = "Completion Mode";
            // 
            // tableLayoutPanel5
            // 
            this.tableLayoutPanel5.AutoSize = true;
            this.tableLayoutPanel5.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel5.ColumnCount = 1;
            this.tableLayoutPanel5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel5.Controls.Add(this._evalNever, 0, 0);
            this.tableLayoutPanel5.Controls.Add(this._evalNoCalls, 0, 1);
            this.tableLayoutPanel5.Controls.Add(this._evalAlways, 0, 2);
            this.tableLayoutPanel5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel5.Location = new System.Drawing.Point(6, 21);
            this.tableLayoutPanel5.Name = "tableLayoutPanel5";
            this.tableLayoutPanel5.RowCount = 3;
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel5.Size = new System.Drawing.Size(467, 69);
            this.tableLayoutPanel5.TabIndex = 0;
            // 
            // _evalNever
            // 
            this._evalNever.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._evalNever.AutoSize = true;
            this._evalNever.Location = new System.Drawing.Point(6, 3);
            this._evalNever.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._evalNever.Name = "_evalNever";
            this._evalNever.Size = new System.Drawing.Size(156, 17);
            this._evalNever.TabIndex = 0;
            this._evalNever.TabStop = true;
            this._evalNever.Text = "&Never evaluate expressions";
            this._evalNever.UseVisualStyleBackColor = true;
            this._evalNever.CheckedChanged += new System.EventHandler(this._evalNever_CheckedChanged);
            // 
            // _evalAlways
            // 
            this._evalAlways.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._evalAlways.AutoSize = true;
            this._evalAlways.Location = new System.Drawing.Point(6, 49);
            this._evalAlways.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._evalAlways.Name = "_evalAlways";
            this._evalAlways.Size = new System.Drawing.Size(160, 17);
            this._evalAlways.TabIndex = 2;
            this._evalAlways.TabStop = true;
            this._evalAlways.Text = "&Always evaluate expressions";
            this._evalAlways.UseVisualStyleBackColor = true;
            this._evalAlways.CheckedChanged += new System.EventHandler(this._evalAlways_CheckedChanged);
            // 
            // _evalNoCalls
            // 
            this._evalNoCalls.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._evalNoCalls.AutoSize = true;
            this._evalNoCalls.Location = new System.Drawing.Point(6, 26);
            this._evalNoCalls.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._evalNoCalls.Name = "_evalNoCalls";
            this._evalNoCalls.Size = new System.Drawing.Size(232, 17);
            this._evalNoCalls.TabIndex = 1;
            this._evalNoCalls.TabStop = true;
            this._evalNoCalls.Text = "Never evaluate expressions containing &calls";
            this._evalNoCalls.UseVisualStyleBackColor = true;
            this._evalNoCalls.CheckedChanged += new System.EventHandler(this._evalNoCalls_CheckedChanged);
            // 
            // _inlinePrompts
            // 
            this._inlinePrompts.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._inlinePrompts.AutoSize = true;
            this._inlinePrompts.Location = new System.Drawing.Point(6, 26);
            this._inlinePrompts.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._inlinePrompts.Name = "_inlinePrompts";
            this._inlinePrompts.Size = new System.Drawing.Size(112, 17);
            this._inlinePrompts.TabIndex = 2;
            this._inlinePrompts.Text = "Use &inline prompts";
            this._inlinePrompts.UseVisualStyleBackColor = true;
            this._inlinePrompts.CheckedChanged += new System.EventHandler(this._inlinePrompts_CheckedChanged);
            // 
            // _useUserDefinedPrompts
            // 
            this._useUserDefinedPrompts.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._useUserDefinedPrompts.AutoSize = true;
            this._useUserDefinedPrompts.Location = new System.Drawing.Point(6, 49);
            this._useUserDefinedPrompts.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._useUserDefinedPrompts.Name = "_useUserDefinedPrompts";
            this._useUserDefinedPrompts.Size = new System.Drawing.Size(146, 17);
            this._useUserDefinedPrompts.TabIndex = 4;
            this._useUserDefinedPrompts.Text = "Use use&r defined prompts";
            this._useUserDefinedPrompts.UseVisualStyleBackColor = true;
            this._useUserDefinedPrompts.CheckedChanged += new System.EventHandler(this._useInterpreterPrompts_CheckedChanged);
            // 
            // _priPromptLabel
            // 
            this._priPromptLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._priPromptLabel.AutoEllipsis = true;
            this._priPromptLabel.AutoSize = true;
            this._priPromptLabel.Location = new System.Drawing.Point(20, 6);
            this._priPromptLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._priPromptLabel.Name = "_priPromptLabel";
            this._priPromptLabel.Size = new System.Drawing.Size(77, 13);
            this._priPromptLabel.TabIndex = 0;
            this._priPromptLabel.Text = "&Primary Prompt";
            this._priPromptLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _priPrompt
            // 
            this._priPrompt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._priPrompt.Location = new System.Drawing.Point(109, 3);
            this._priPrompt.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._priPrompt.MinimumSize = new System.Drawing.Size(40, 4);
            this._priPrompt.Name = "_priPrompt";
            this._priPrompt.Size = new System.Drawing.Size(118, 20);
            this._priPrompt.TabIndex = 1;
            this._priPrompt.TextChanged += new System.EventHandler(this._priPrompt_TextChanged);
            // 
            // _secPromptLabel
            // 
            this._secPromptLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._secPromptLabel.AutoEllipsis = true;
            this._secPromptLabel.AutoSize = true;
            this._secPromptLabel.Location = new System.Drawing.Point(239, 6);
            this._secPromptLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._secPromptLabel.Name = "_secPromptLabel";
            this._secPromptLabel.Size = new System.Drawing.Size(94, 13);
            this._secPromptLabel.TabIndex = 2;
            this._secPromptLabel.Text = "S&econdary Prompt";
            this._secPromptLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _secPrompt
            // 
            this._secPrompt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._secPrompt.Location = new System.Drawing.Point(345, 3);
            this._secPrompt.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._secPrompt.MinimumSize = new System.Drawing.Size(40, 4);
            this._secPrompt.Name = "_secPrompt";
            this._secPrompt.Size = new System.Drawing.Size(118, 20);
            this._secPrompt.TabIndex = 3;
            this._secPrompt.TextChanged += new System.EventHandler(this._secPrompt_TextChanged);
            // 
            // _startupScript
            // 
            this._startupScript.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.SetColumnSpan(this._startupScript, 3);
            this._startupScript.Location = new System.Drawing.Point(115, 31);
            this._startupScript.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._startupScript.Name = "_startupScript";
            this._startupScript.Size = new System.Drawing.Size(326, 20);
            this._startupScript.TabIndex = 3;
            this._startupScript.TextChanged += new System.EventHandler(this._startupScript_TextChanged);
            // 
            // _startupScriptButton
            // 
            this._startupScriptButton.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._startupScriptButton.AutoSize = true;
            this._startupScriptButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._startupScriptButton.Location = new System.Drawing.Point(453, 30);
            this._startupScriptButton.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._startupScriptButton.Name = "_startupScriptButton";
            this._startupScriptButton.Size = new System.Drawing.Size(26, 23);
            this._startupScriptButton.TabIndex = 4;
            this._startupScriptButton.Text = "...";
            this._startupScriptButton.UseVisualStyleBackColor = true;
            this._startupScriptButton.Click += new System.EventHandler(this._startupScriptButton_Click);
            // 
            // _startScriptLabel
            // 
            this._startScriptLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._startScriptLabel.AutoEllipsis = true;
            this._startScriptLabel.AutoSize = true;
            this._startScriptLabel.Location = new System.Drawing.Point(6, 35);
            this._startScriptLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._startScriptLabel.Name = "_startScriptLabel";
            this._startScriptLabel.Size = new System.Drawing.Size(74, 13);
            this._startScriptLabel.TabIndex = 2;
            this._startScriptLabel.Text = "&Startup Script:";
            this._startScriptLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _showSettingsForLabel
            // 
            this._showSettingsForLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._showSettingsForLabel.AutoEllipsis = true;
            this._showSettingsForLabel.AutoSize = true;
            this._showSettingsForLabel.Location = new System.Drawing.Point(6, 7);
            this._showSettingsForLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._showSettingsForLabel.Name = "_showSettingsForLabel";
            this._showSettingsForLabel.Size = new System.Drawing.Size(96, 13);
            this._showSettingsForLabel.TabIndex = 0;
            this._showSettingsForLabel.Text = "Show Se&ttings For:";
            this._showSettingsForLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _showSettingsFor
            // 
            this._showSettingsFor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.SetColumnSpan(this._showSettingsFor, 4);
            this._showSettingsFor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._showSettingsFor.FormattingEnabled = true;
            this._showSettingsFor.Location = new System.Drawing.Point(115, 3);
            this._showSettingsFor.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._showSettingsFor.Name = "_showSettingsFor";
            this._showSettingsFor.Size = new System.Drawing.Size(364, 21);
            this._showSettingsFor.TabIndex = 1;
            this._showSettingsFor.SelectedIndexChanged += new System.EventHandler(this._showSettingsFor_SelectedIndexChanged);
            this._showSettingsFor.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.Interpreter_Format);
            // 
            // _executionModeLabel
            // 
            this._executionModeLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._executionModeLabel.AutoEllipsis = true;
            this._executionModeLabel.AutoSize = true;
            this._executionModeLabel.Location = new System.Drawing.Point(233, 63);
            this._executionModeLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._executionModeLabel.Name = "_executionModeLabel";
            this._executionModeLabel.Size = new System.Drawing.Size(90, 13);
            this._executionModeLabel.TabIndex = 7;
            this._executionModeLabel.Text = "Interactive &Mode:";
            this._executionModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _executionMode
            // 
            this._executionMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel2.SetColumnSpan(this._executionMode, 2);
            this._executionMode.DropDownWidth = 115;
            this._executionMode.FormattingEnabled = true;
            this._executionMode.Location = new System.Drawing.Point(335, 59);
            this._executionMode.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._executionMode.Name = "_executionMode";
            this._executionMode.Size = new System.Drawing.Size(144, 21);
            this._executionMode.TabIndex = 8;
            this._executionMode.SelectedIndexChanged += new System.EventHandler(this._executionMode_SelectedIndexChanged);
            this._executionMode.TextChanged += new System.EventHandler(this._executionMode_TextChanged);
            // 
            // _interpOptionsLabel
            // 
            this._interpOptionsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._interpOptionsLabel.AutoEllipsis = true;
            this._interpOptionsLabel.AutoSize = true;
            this._interpOptionsLabel.Location = new System.Drawing.Point(6, 63);
            this._interpOptionsLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._interpOptionsLabel.Name = "_interpOptionsLabel";
            this._interpOptionsLabel.Size = new System.Drawing.Size(97, 13);
            this._interpOptionsLabel.TabIndex = 5;
            this._interpOptionsLabel.Text = "Interpreter &Options:";
            this._interpOptionsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _interpreterOptions
            // 
            this._interpreterOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._interpreterOptions.Location = new System.Drawing.Point(115, 59);
            this._interpreterOptions.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._interpreterOptions.MinimumSize = new System.Drawing.Size(40, 4);
            this._interpreterOptions.Name = "_interpreterOptions";
            this._interpreterOptions.Size = new System.Drawing.Size(106, 20);
            this._interpreterOptions.TabIndex = 6;
            this._interpreterOptions.TextChanged += new System.EventHandler(this.InterpreterOptionsTextChanged);
            // 
            // _enableAttach
            // 
            this._enableAttach.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._enableAttach.AutoSize = true;
            this._enableAttach.Location = new System.Drawing.Point(224, 3);
            this._enableAttach.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._enableAttach.Name = "_enableAttach";
            this._enableAttach.Size = new System.Drawing.Size(209, 17);
            this._enableAttach.TabIndex = 1;
            this._enableAttach.Text = "&Enable attaching to interactive window";
            this._enableAttach.UseVisualStyleBackColor = true;
            this._enableAttach.CheckedChanged += new System.EventHandler(this.EnableAttachCheckedChanged);
            // 
            // _liveCompletionsOnly
            // 
            this._liveCompletionsOnly.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._liveCompletionsOnly.AutoSize = true;
            this._liveCompletionsOnly.Location = new System.Drawing.Point(224, 26);
            this._liveCompletionsOnly.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._liveCompletionsOnly.Name = "_liveCompletionsOnly";
            this._liveCompletionsOnly.Size = new System.Drawing.Size(145, 17);
            this._liveCompletionsOnly.TabIndex = 3;
            this._liveCompletionsOnly.Text = "Only use li&ve completions";
            this._liveCompletionsOnly.UseVisualStyleBackColor = true;
            this._liveCompletionsOnly.CheckedChanged += new System.EventHandler(this._liveCompletionsOnly_CheckedChanged);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel3, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel4, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this._completionModeGroup, 0, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(491, 315);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel2.ColumnCount = 5;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.Controls.Add(this._showSettingsForLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._showSettingsFor, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._startScriptLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._startupScript, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._startupScriptButton, 4, 1);
            this.tableLayoutPanel2.Controls.Add(this._interpOptionsLabel, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._interpreterOptions, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._executionModeLabel, 2, 2);
            this.tableLayoutPanel2.Controls.Add(this._executionMode, 3, 2);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 4);
            this.tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 3;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel2.Size = new System.Drawing.Size(485, 83);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.AutoSize = true;
            this.tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel3.ColumnCount = 2;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this.tableLayoutPanel3.Controls.Add(this._smartReplHistory, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this._enableAttach, 1, 0);
            this.tableLayoutPanel3.Controls.Add(this._inlinePrompts, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this._liveCompletionsOnly, 1, 1);
            this.tableLayoutPanel3.Controls.Add(this._useUserDefinedPrompts, 0, 2);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 91);
            this.tableLayoutPanel3.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 3;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel3.Size = new System.Drawing.Size(485, 69);
            this.tableLayoutPanel3.TabIndex = 1;
            // 
            // tableLayoutPanel4
            // 
            this.tableLayoutPanel4.AutoSize = true;
            this.tableLayoutPanel4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel4.ColumnCount = 6;
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 5F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 5F));
            this.tableLayoutPanel4.Controls.Add(this._priPromptLabel, 1, 0);
            this.tableLayoutPanel4.Controls.Add(this._priPrompt, 2, 0);
            this.tableLayoutPanel4.Controls.Add(this._secPromptLabel, 3, 0);
            this.tableLayoutPanel4.Controls.Add(this._secPrompt, 4, 0);
            this.tableLayoutPanel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel4.Location = new System.Drawing.Point(3, 160);
            this.tableLayoutPanel4.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 1;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel4.Size = new System.Drawing.Size(485, 26);
            this.tableLayoutPanel4.TabIndex = 2;
            // 
            // PythonInteractiveOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonInteractiveOptionsControl";
            this.Size = new System.Drawing.Size(491, 315);
            this._completionModeGroup.ResumeLayout(false);
            this._completionModeGroup.PerformLayout();
            this.tableLayoutPanel5.ResumeLayout(false);
            this.tableLayoutPanel5.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox _smartReplHistory;
        private System.Windows.Forms.GroupBox _completionModeGroup;
        private System.Windows.Forms.RadioButton _evalAlways;
        private System.Windows.Forms.RadioButton _evalNoCalls;
        private System.Windows.Forms.RadioButton _evalNever;
        private System.Windows.Forms.CheckBox _inlinePrompts;
        private System.Windows.Forms.CheckBox _useUserDefinedPrompts;
        private System.Windows.Forms.TextBox _secPrompt;
        private System.Windows.Forms.TextBox _priPrompt;
        private System.Windows.Forms.Label _secPromptLabel;
        private System.Windows.Forms.Label _priPromptLabel;
        private System.Windows.Forms.ToolTip _tooltips;
        private System.Windows.Forms.TextBox _startupScript;
        private System.Windows.Forms.Button _startupScriptButton;
        private System.Windows.Forms.Label _startScriptLabel;
        private System.Windows.Forms.Label _showSettingsForLabel;
        private System.Windows.Forms.ComboBox _showSettingsFor;
        private System.Windows.Forms.Label _executionModeLabel;
        private System.Windows.Forms.ComboBox _executionMode;
        private System.Windows.Forms.Label _interpOptionsLabel;
        private System.Windows.Forms.TextBox _interpreterOptions;
        private System.Windows.Forms.CheckBox _enableAttach;
        private System.Windows.Forms.CheckBox _liveCompletionsOnly;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel5;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
    }
}
