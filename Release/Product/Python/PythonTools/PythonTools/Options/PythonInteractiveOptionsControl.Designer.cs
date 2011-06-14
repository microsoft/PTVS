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
            this._evalAlways = new System.Windows.Forms.RadioButton();
            this._evalNoCalls = new System.Windows.Forms.RadioButton();
            this._evalNever = new System.Windows.Forms.RadioButton();
            this._promptOptionsGroup = new System.Windows.Forms.GroupBox();
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
            this._completionModeGroup.SuspendLayout();
            this._promptOptionsGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _smartReplHistory
            // 
            this._smartReplHistory.AutoSize = true;
            this._smartReplHistory.Location = new System.Drawing.Point(184, 19);
            this._smartReplHistory.Name = "_smartReplHistory";
            this._smartReplHistory.Size = new System.Drawing.Size(160, 17);
            this._smartReplHistory.TabIndex = 0;
            this._smartReplHistory.Text = "Arrow Keys use smart &history";
            this._smartReplHistory.UseVisualStyleBackColor = true;
            this._smartReplHistory.CheckedChanged += new System.EventHandler(this._smartReplHistory_CheckedChanged);
            // 
            // _completionModeGroup
            // 
            this._completionModeGroup.Controls.Add(this._evalAlways);
            this._completionModeGroup.Controls.Add(this._evalNoCalls);
            this._completionModeGroup.Controls.Add(this._evalNever);
            this._completionModeGroup.Location = new System.Drawing.Point(6, 107);
            this._completionModeGroup.Name = "_completionModeGroup";
            this._completionModeGroup.Size = new System.Drawing.Size(364, 86);
            this._completionModeGroup.TabIndex = 3;
            this._completionModeGroup.TabStop = false;
            this._completionModeGroup.Text = "Completion Mode";
            // 
            // _evalAlways
            // 
            this._evalAlways.AutoSize = true;
            this._evalAlways.Location = new System.Drawing.Point(12, 58);
            this._evalAlways.Name = "_evalAlways";
            this._evalAlways.Size = new System.Drawing.Size(160, 17);
            this._evalAlways.TabIndex = 20;
            this._evalAlways.TabStop = true;
            this._evalAlways.Text = "&Always evaluate expressions";
            this._evalAlways.UseVisualStyleBackColor = true;
            this._evalAlways.CheckedChanged += new System.EventHandler(this._evalAlways_CheckedChanged);
            // 
            // _evalNoCalls
            // 
            this._evalNoCalls.AutoSize = true;
            this._evalNoCalls.Location = new System.Drawing.Point(12, 39);
            this._evalNoCalls.Name = "_evalNoCalls";
            this._evalNoCalls.Size = new System.Drawing.Size(232, 17);
            this._evalNoCalls.TabIndex = 10;
            this._evalNoCalls.TabStop = true;
            this._evalNoCalls.Text = "Never evaluate expressions containing &calls";
            this._evalNoCalls.UseVisualStyleBackColor = true;
            this._evalNoCalls.CheckedChanged += new System.EventHandler(this._evalNoCalls_CheckedChanged);
            // 
            // _evalNever
            // 
            this._evalNever.AutoSize = true;
            this._evalNever.Location = new System.Drawing.Point(12, 20);
            this._evalNever.Name = "_evalNever";
            this._evalNever.Size = new System.Drawing.Size(156, 17);
            this._evalNever.TabIndex = 0;
            this._evalNever.TabStop = true;
            this._evalNever.Text = "&Never evaluate expressions";
            this._evalNever.UseVisualStyleBackColor = true;
            this._evalNever.CheckedChanged += new System.EventHandler(this._evalNever_CheckedChanged);
            // 
            // _promptOptionsGroup
            // 
            this._promptOptionsGroup.Controls.Add(this._inlinePrompts);
            this._promptOptionsGroup.Controls.Add(this._useUserDefinedPrompts);
            this._promptOptionsGroup.Controls.Add(this._priPromptLabel);
            this._promptOptionsGroup.Controls.Add(this._priPrompt);
            this._promptOptionsGroup.Controls.Add(this._secPromptLabel);
            this._promptOptionsGroup.Controls.Add(this._secPrompt);
            this._promptOptionsGroup.Controls.Add(this._smartReplHistory);
            this._promptOptionsGroup.Location = new System.Drawing.Point(6, 199);
            this._promptOptionsGroup.Name = "_promptOptionsGroup";
            this._promptOptionsGroup.Size = new System.Drawing.Size(364, 94);
            this._promptOptionsGroup.TabIndex = 4;
            this._promptOptionsGroup.TabStop = false;
            this._promptOptionsGroup.Text = "Input/Output Options";
            // 
            // _inlinePrompts
            // 
            this._inlinePrompts.AutoSize = true;
            this._inlinePrompts.Location = new System.Drawing.Point(12, 20);
            this._inlinePrompts.Name = "_inlinePrompts";
            this._inlinePrompts.Size = new System.Drawing.Size(112, 17);
            this._inlinePrompts.TabIndex = 0;
            this._inlinePrompts.Text = "Use &inline prompts";
            this._inlinePrompts.UseVisualStyleBackColor = true;
            this._inlinePrompts.CheckedChanged += new System.EventHandler(this._inlinePrompts_CheckedChanged);
            // 
            // _useUserDefinedPrompts
            // 
            this._useUserDefinedPrompts.AutoSize = true;
            this._useUserDefinedPrompts.Location = new System.Drawing.Point(12, 43);
            this._useUserDefinedPrompts.Name = "_useUserDefinedPrompts";
            this._useUserDefinedPrompts.Size = new System.Drawing.Size(146, 17);
            this._useUserDefinedPrompts.TabIndex = 1;
            this._useUserDefinedPrompts.Text = "Use use&r defined prompts";
            this._useUserDefinedPrompts.UseVisualStyleBackColor = true;
            this._useUserDefinedPrompts.CheckedChanged += new System.EventHandler(this._useInterpreterPrompts_CheckedChanged);
            // 
            // _priPromptLabel
            // 
            this._priPromptLabel.AutoSize = true;
            this._priPromptLabel.Location = new System.Drawing.Point(29, 67);
            this._priPromptLabel.Name = "_priPromptLabel";
            this._priPromptLabel.Size = new System.Drawing.Size(77, 13);
            this._priPromptLabel.TabIndex = 2;
            this._priPromptLabel.Text = "&Primary Prompt";
            // 
            // _priPrompt
            // 
            this._priPrompt.Location = new System.Drawing.Point(129, 66);
            this._priPrompt.Name = "_priPrompt";
            this._priPrompt.Size = new System.Drawing.Size(54, 20);
            this._priPrompt.TabIndex = 4;
            this._priPrompt.TextChanged += new System.EventHandler(this._priPrompt_TextChanged);
            // 
            // _secPromptLabel
            // 
            this._secPromptLabel.AutoSize = true;
            this._secPromptLabel.Location = new System.Drawing.Point(190, 66);
            this._secPromptLabel.Name = "_secPromptLabel";
            this._secPromptLabel.Size = new System.Drawing.Size(94, 13);
            this._secPromptLabel.TabIndex = 3;
            this._secPromptLabel.Text = "S&econdary Prompt";
            // 
            // _secPrompt
            // 
            this._secPrompt.Location = new System.Drawing.Point(290, 66);
            this._secPrompt.Name = "_secPrompt";
            this._secPrompt.Size = new System.Drawing.Size(54, 20);
            this._secPrompt.TabIndex = 5;
            this._secPrompt.TextChanged += new System.EventHandler(this._secPrompt_TextChanged);
            // 
            // _startupScript
            // 
            this._startupScript.Location = new System.Drawing.Point(105, 33);
            this._startupScript.Name = "_startupScript";
            this._startupScript.Size = new System.Drawing.Size(233, 20);
            this._startupScript.TabIndex = 5;
            this._startupScript.TextChanged += new System.EventHandler(this._startupScript_TextChanged);
            // 
            // _startupScriptButton
            // 
            this._startupScriptButton.Location = new System.Drawing.Point(344, 31);
            this._startupScriptButton.Name = "_startupScriptButton";
            this._startupScriptButton.Size = new System.Drawing.Size(26, 23);
            this._startupScriptButton.TabIndex = 6;
            this._startupScriptButton.Text = "...";
            this._startupScriptButton.UseVisualStyleBackColor = true;
            this._startupScriptButton.Click += new System.EventHandler(this._startupScriptButton_Click);
            // 
            // _startScriptLabel
            // 
            this._startScriptLabel.AutoSize = true;
            this._startScriptLabel.Location = new System.Drawing.Point(3, 35);
            this._startScriptLabel.Name = "_startScriptLabel";
            this._startScriptLabel.Size = new System.Drawing.Size(74, 13);
            this._startScriptLabel.TabIndex = 7;
            this._startScriptLabel.Text = "&Startup Script:";
            // 
            // _showSettingsForLabel
            // 
            this._showSettingsForLabel.AutoSize = true;
            this._showSettingsForLabel.Location = new System.Drawing.Point(3, 11);
            this._showSettingsForLabel.Name = "_showSettingsForLabel";
            this._showSettingsForLabel.Size = new System.Drawing.Size(96, 13);
            this._showSettingsForLabel.TabIndex = 8;
            this._showSettingsForLabel.Text = "Show Se&ttings For:";
            // 
            // _showSettingsFor
            // 
            this._showSettingsFor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._showSettingsFor.FormattingEnabled = true;
            this._showSettingsFor.Location = new System.Drawing.Point(105, 8);
            this._showSettingsFor.Name = "_showSettingsFor";
            this._showSettingsFor.Size = new System.Drawing.Size(265, 21);
            this._showSettingsFor.TabIndex = 9;
            this._showSettingsFor.SelectedIndexChanged += new System.EventHandler(this._showSettingsFor_SelectedIndexChanged);
            // 
            // _executionModeLabel
            // 
            this._executionModeLabel.AutoSize = true;
            this._executionModeLabel.Location = new System.Drawing.Point(3, 59);
            this._executionModeLabel.Name = "_executionModeLabel";
            this._executionModeLabel.Size = new System.Drawing.Size(90, 13);
            this._executionModeLabel.TabIndex = 10;
            this._executionModeLabel.Text = "Interactive &Mode:";
            // 
            // _executionMode
            // 
            this._executionMode.FormattingEnabled = true;
            this._executionMode.Location = new System.Drawing.Point(105, 56);
            this._executionMode.Name = "_executionMode";
            this._executionMode.Size = new System.Drawing.Size(265, 21);
            this._executionMode.TabIndex = 11;
            this._executionMode.SelectedIndexChanged += new System.EventHandler(this._executionMode_SelectedIndexChanged);
            this._executionMode.TextChanged += new System.EventHandler(this._executionMode_TextChanged);
            // 
            // _interpOptionsLabel
            // 
            this._interpOptionsLabel.AutoSize = true;
            this._interpOptionsLabel.Location = new System.Drawing.Point(3, 82);
            this._interpOptionsLabel.Name = "_interpOptionsLabel";
            this._interpOptionsLabel.Size = new System.Drawing.Size(97, 13);
            this._interpOptionsLabel.TabIndex = 12;
            this._interpOptionsLabel.Text = "Interpreter &Options:";
            // 
            // _interpreterOptions
            // 
            this._interpreterOptions.Location = new System.Drawing.Point(105, 81);
            this._interpreterOptions.Name = "_interpreterOptions";
            this._interpreterOptions.Size = new System.Drawing.Size(265, 20);
            this._interpreterOptions.TabIndex = 13;
            this._interpreterOptions.TextChanged += new System.EventHandler(this.InterpreterOptionsTextChanged);
            // 
            // PythonInteractiveOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._interpreterOptions);
            this.Controls.Add(this._interpOptionsLabel);
            this.Controls.Add(this._showSettingsForLabel);
            this.Controls.Add(this._showSettingsFor);
            this.Controls.Add(this._startScriptLabel);
            this.Controls.Add(this._startupScript);
            this.Controls.Add(this._executionModeLabel);
            this.Controls.Add(this._executionMode);
            this.Controls.Add(this._startupScriptButton);
            this.Controls.Add(this._completionModeGroup);
            this.Controls.Add(this._promptOptionsGroup);
            this.Name = "PythonInteractiveOptionsControl";
            this.Size = new System.Drawing.Size(395, 305);
            this._completionModeGroup.ResumeLayout(false);
            this._completionModeGroup.PerformLayout();
            this._promptOptionsGroup.ResumeLayout(false);
            this._promptOptionsGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _smartReplHistory;
        private System.Windows.Forms.GroupBox _completionModeGroup;
        private System.Windows.Forms.RadioButton _evalAlways;
        private System.Windows.Forms.RadioButton _evalNoCalls;
        private System.Windows.Forms.RadioButton _evalNever;
        private System.Windows.Forms.GroupBox _promptOptionsGroup;
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
    }
}
