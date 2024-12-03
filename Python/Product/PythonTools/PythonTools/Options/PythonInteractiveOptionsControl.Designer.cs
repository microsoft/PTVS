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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonInteractiveOptionsControl));
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._tooltips = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.scriptsTextBox = new System.Windows.Forms.TextBox();
            this.browseScriptsButton = new System.Windows.Forms.Button();
            this.useSmartHistoryCheckBox = new System.Windows.Forms.CheckBox();
            scriptsLabel = new System.Windows.Forms.Label();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // scriptsLabel
            // 
            resources.ApplyResources(scriptsLabel, "scriptsLabel");
            scriptsLabel.Name = "scriptsLabel";
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(scriptsLabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.scriptsTextBox, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.browseScriptsButton, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.useSmartHistoryCheckBox, 0, 1);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // scriptsTextBox
            // 
            resources.ApplyResources(this.scriptsTextBox, "scriptsTextBox");
            this.scriptsTextBox.Name = "scriptsTextBox";
            this.scriptsTextBox.TextChanged += new System.EventHandler(this.Scripts_TextChanged);
            // 
            // browseScriptsButton
            // 
            resources.ApplyResources(this.browseScriptsButton, "browseScriptsButton");
            this.browseScriptsButton.Name = "browseScriptsButton";
            this.browseScriptsButton.UseVisualStyleBackColor = true;
            this.browseScriptsButton.Click += new System.EventHandler(this.browseScriptsButton_Click);
            // 
            // useSmartHistoryCheckBox
            // 
            resources.ApplyResources(this.useSmartHistoryCheckBox, "useSmartHistoryCheckBox");
            this.useSmartHistoryCheckBox.AutoEllipsis = true;
            this.tableLayoutPanel1.SetColumnSpan(this.useSmartHistoryCheckBox, 3);
            this.useSmartHistoryCheckBox.Name = "useSmartHistoryCheckBox";
            this.useSmartHistoryCheckBox.UseVisualStyleBackColor = true;
            this.useSmartHistoryCheckBox.CheckedChanged += new System.EventHandler(this.UseSmartHistory_CheckedChanged);
            // 
            // PythonInteractiveOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PythonInteractiveOptionsControl";
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
    }
}
