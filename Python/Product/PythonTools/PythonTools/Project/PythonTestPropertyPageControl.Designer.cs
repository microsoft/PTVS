namespace Microsoft.PythonTools.Project {
    partial class PythonTestPropertyPageControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonTestPropertyPageControl));
            this._pytestGroup = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._pytestPathLabel = new System.Windows.Forms.Label();
            this._pytestPath = new System.Windows.Forms.TextBox();
            this._argumentsLabel = new System.Windows.Forms.Label();
            this._arguments = new System.Windows.Forms.TextBox();
            this._pytestEnabled = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._pytestGroup.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _pytestGroup
            // 
            resources.ApplyResources(this._pytestGroup, "_pytestGroup");
            this._pytestGroup.Controls.Add(this.tableLayoutPanel2);
            this._pytestGroup.Name = "_pytestGroup";
            this._pytestGroup.TabStop = false;
            this._pytestGroup.Enter += new System.EventHandler(this._applicationGroup_Enter);
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this._pytestPath, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this._pytestPathLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._argumentsLabel, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._arguments, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this._pytestEnabled, 0, 2);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.Paint += new System.Windows.Forms.PaintEventHandler(this.TableLayoutPanel2_Paint);
            // 
            // _pytestPathLabel
            // 
            resources.ApplyResources(this._pytestPathLabel, "_pytestPathLabel");
            this._pytestPathLabel.AutoEllipsis = true;
            this._pytestPathLabel.Name = "_pytestPathLabel";
            // 
            // _pytestPath
            // 
            resources.ApplyResources(this._pytestPath, "_pytestPath");
            this._pytestPath.Name = "_pytestPath";
            this._pytestPath.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _argumentsLabel
            // 
            resources.ApplyResources(this._argumentsLabel, "_argumentsLabel");
            this._argumentsLabel.AutoEllipsis = true;
            this._argumentsLabel.Name = "_argumentsLabel";
            // 
            // _arguments
            // 
            resources.ApplyResources(this._arguments, "_arguments");
            this._arguments.Name = "_arguments";
            this._arguments.TextChanged += new System.EventHandler(this.Changed);
            // 
            // _pytestEnabled
            // 
            resources.ApplyResources(this._pytestEnabled, "_pytestEnabled");
            this.tableLayoutPanel2.SetColumnSpan(this._pytestEnabled, 2);
            this._pytestEnabled.Name = "_pytestEnabled";
            this._pytestEnabled.UseVisualStyleBackColor = true;
            this._pytestEnabled.CheckedChanged += new System.EventHandler(this.Changed);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._pytestGroup, 0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // PythonTestPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PythonTestPropertyPageControl";
            this._pytestGroup.ResumeLayout(false);
            this._pytestGroup.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _pytestGroup;
        private System.Windows.Forms.Label _pytestPathLabel;
        private System.Windows.Forms.TextBox _pytestPath;
        private System.Windows.Forms.TextBox _arguments;
        private System.Windows.Forms.Label _argumentsLabel;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckBox _pytestEnabled;
    }
}
