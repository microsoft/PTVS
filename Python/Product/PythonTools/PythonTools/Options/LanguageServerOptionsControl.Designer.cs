namespace Microsoft.PythonTools.Options {
    partial class LanguageServerOptionsControl {
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
            System.Windows.Forms.Label typeShedPathLabel;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LanguageServerOptionsControl));
            System.Windows.Forms.GroupBox groupBox1;
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.suppressTypeShedCheckbox = new System.Windows.Forms.CheckBox();
            this.browseTypeShedPathButton = new System.Windows.Forms.Button();
            this.typeShedPathTextBox = new System.Windows.Forms.TextBox();
            this._tooltips = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._disableLanguageServerCheckbox = new System.Windows.Forms.CheckBox();
            typeShedPathLabel = new System.Windows.Forms.Label();
            groupBox1 = new System.Windows.Forms.GroupBox();
            groupBox1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // typeShedPathLabel
            // 
            resources.ApplyResources(typeShedPathLabel, "typeShedPathLabel");
            typeShedPathLabel.Name = "typeShedPathLabel";
            // 
            // groupBox1
            // 
            resources.ApplyResources(groupBox1, "groupBox1");
            groupBox1.Controls.Add(this.tableLayoutPanel2);
            groupBox1.Name = "groupBox1";
            groupBox1.TabStop = false;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this.suppressTypeShedCheckbox, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.browseTypeShedPathButton, 2, 1);
            this.tableLayoutPanel2.Controls.Add(this.typeShedPathTextBox, 1, 1);
            this.tableLayoutPanel2.Controls.Add(typeShedPathLabel, 0, 1);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // suppressTypeShedCheckbox
            // 
            resources.ApplyResources(this.suppressTypeShedCheckbox, "suppressTypeShedCheckbox");
            this.tableLayoutPanel2.SetColumnSpan(this.suppressTypeShedCheckbox, 3);
            this.suppressTypeShedCheckbox.Name = "suppressTypeShedCheckbox";
            this.suppressTypeShedCheckbox.UseVisualStyleBackColor = true;
            this.suppressTypeShedCheckbox.CheckedChanged += new System.EventHandler(this.suppressTypeShedCheckbox_CheckedChanged);
            // 
            // browseTypeShedPathButton
            // 
            resources.ApplyResources(this.browseTypeShedPathButton, "browseTypeShedPathButton");
            this.browseTypeShedPathButton.Name = "browseTypeShedPathButton";
            this.browseTypeShedPathButton.UseVisualStyleBackColor = true;
            this.browseTypeShedPathButton.Click += new System.EventHandler(this.browseTypeShedPathButton_Click);
            // 
            // typeShedPathTextBox
            // 
            resources.ApplyResources(this.typeShedPathTextBox, "typeShedPathTextBox");
            this.typeShedPathTextBox.Name = "typeShedPathTextBox";
            this.typeShedPathTextBox.TextChanged += new System.EventHandler(this.TypeShedPath_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(groupBox1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._disableLanguageServerCheckbox, 0, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _disableLanguageServerCheckbox
            // 
            resources.ApplyResources(this._disableLanguageServerCheckbox, "_disableLanguageServerCheckbox");
            this._disableLanguageServerCheckbox.Name = "_disableLanguageServerCheckbox";
            this._tooltips.SetToolTip(this._disableLanguageServerCheckbox, resources.GetString("_disableLanguageServerCheckbox.ToolTip"));
            this._disableLanguageServerCheckbox.UseVisualStyleBackColor = true;
            this._disableLanguageServerCheckbox.CheckedChanged += new System.EventHandler(this._enableLanguageServer_CheckedChanged);
            // 
            // LanguageServerOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "LanguageServerOptionsControl";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ToolTip _tooltips;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox typeShedPathTextBox;
        private System.Windows.Forms.Button browseTypeShedPathButton;
        private System.Windows.Forms.CheckBox suppressTypeShedCheckbox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.CheckBox _disableLanguageServerCheckbox;
    }
}