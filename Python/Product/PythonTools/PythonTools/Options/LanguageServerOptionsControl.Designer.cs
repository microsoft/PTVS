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
            System.Windows.Forms.Label label1;
            this._tooltips = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.typeShedPathTextBox = new System.Windows.Forms.TextBox();
            this.browseTypeShedPathButton = new System.Windows.Forms.Button();
            typeShedPathLabel = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // typeShedPathLabel
            // 
            resources.ApplyResources(typeShedPathLabel, "typeShedPathLabel");
            typeShedPathLabel.Name = "typeShedPathLabel";
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(typeShedPathLabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.typeShedPathTextBox, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.browseTypeShedPathButton, 2, 0);
            this.tableLayoutPanel1.Controls.Add(label1, 0, 1);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // typeShedPathTextBox
            // 
            resources.ApplyResources(this.typeShedPathTextBox, "typeShedPathTextBox");
            this.typeShedPathTextBox.Name = "typeShedPathTextBox";
            this.typeShedPathTextBox.TextChanged += new System.EventHandler(this.TypeShedPath_TextChanged);
            // 
            // browseTypeShedPathButton
            // 
            resources.ApplyResources(this.browseTypeShedPathButton, "browseTypeShedPathButton");
            this.browseTypeShedPathButton.Name = "browseTypeShedPathButton";
            this.browseTypeShedPathButton.UseVisualStyleBackColor = true;
            this.browseTypeShedPathButton.Click += new System.EventHandler(this.browseTypeShedPathButton_Click);
            // 
            // label1
            // 
            resources.ApplyResources(label1, "label1");
            this.tableLayoutPanel1.SetColumnSpan(label1, 3);
            label1.Name = "label1";
            // 
            // LanguageServerOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "LanguageServerOptionsControl";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ToolTip _tooltips;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox typeShedPathTextBox;
        private System.Windows.Forms.Button browseTypeShedPathButton;
    }
}
