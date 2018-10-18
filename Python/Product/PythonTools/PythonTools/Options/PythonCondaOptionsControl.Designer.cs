namespace Microsoft.PythonTools.Options {
    partial class PythonCondaOptionsControl {
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
            System.Windows.Forms.Label _condaPathLabel;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonCondaOptionsControl));
            this._tooltips = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._condaPathTextBox = new System.Windows.Forms.TextBox();
            this._condaPathButton = new System.Windows.Forms.Button();
            _condaPathLabel = new System.Windows.Forms.Label();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // _condaPathLabel
            // 
            resources.ApplyResources(_condaPathLabel, "_condaPathLabel");
            _condaPathLabel.Name = "_condaPathLabel";
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(_condaPathLabel, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this._condaPathTextBox, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this._condaPathButton, 2, 2);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _condaPathTextBox
            // 
            resources.ApplyResources(this._condaPathTextBox, "_condaPathTextBox");
            this._condaPathTextBox.Name = "_condaPathTextBox";
            // 
            // _condaPathButton
            // 
            resources.ApplyResources(this._condaPathButton, "_condaPathButton");
            this._condaPathButton.Name = "_condaPathButton";
            this._condaPathButton.UseVisualStyleBackColor = true;
            this._condaPathButton.Click += new System.EventHandler(this.condaPathButton_Click);
            // 
            // PythonCondaOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel2);
            this.Name = "PythonCondaOptionsControl";
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolTip _tooltips;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TextBox _condaPathTextBox;
        private System.Windows.Forms.Button _condaPathButton;
    }
}
