namespace Microsoft.PythonTools.Uap.Project {
    partial class PythonUapPropertyPageControl {
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
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            this._remoteMachineLabel = new System.Windows.Forms.Label();
            this._remoteMachine = new System.Windows.Forms.TextBox();
            this._uapGroup = new System.Windows.Forms.GroupBox();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            this._uapGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.AutoSize = true;
            tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel2.ColumnCount = 2;
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel2.Controls.Add(this._remoteMachineLabel, 0, 1);
            tableLayoutPanel2.Controls.Add(this._remoteMachine, 1, 1);
            tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel2.Location = new System.Drawing.Point(6, 21);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 2;
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.Size = new System.Drawing.Size(406, 26);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // _remoteMachineLabel
            // 
            this._remoteMachineLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._remoteMachineLabel.AutoSize = true;
            this._remoteMachineLabel.Location = new System.Drawing.Point(6, 6);
            this._remoteMachineLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._remoteMachineLabel.Name = "_remoteMachineLabel";
            this._remoteMachineLabel.Size = new System.Drawing.Size(91, 13);
            this._remoteMachineLabel.TabIndex = 2;
            this._remoteMachineLabel.Text = "&Remote Machine:";
            this._remoteMachineLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _remoteMachine
            // 
            this._remoteMachine.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._remoteMachine.Location = new System.Drawing.Point(109, 3);
            this._remoteMachine.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._remoteMachine.MinimumSize = new System.Drawing.Size(50, 4);
            this._remoteMachine.Name = "_remoteMachine";
            this._remoteMachine.Size = new System.Drawing.Size(291, 20);
            this._remoteMachine.TabIndex = 3;
            this._remoteMachine.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(this._uapGroup, 0, 0);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel1.Size = new System.Drawing.Size(430, 91);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // _uapGroup
            // 
            this._uapGroup.AutoSize = true;
            this._uapGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._uapGroup.Controls.Add(tableLayoutPanel2);
            this._uapGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._uapGroup.Location = new System.Drawing.Point(6, 8);
            this._uapGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._uapGroup.Name = "_uapGroup";
            this._uapGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._uapGroup.Size = new System.Drawing.Size(418, 55);
            this._uapGroup.TabIndex = 0;
            this._uapGroup.TabStop = false;
            this._uapGroup.Text = "Debug Settings";
            // 
            // PythonUapPropertyPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonUapPropertyPageControl";
            this.Size = new System.Drawing.Size(430, 91);
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this._uapGroup.ResumeLayout(false);
            this._uapGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _uapGroup;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Label _remoteMachineLabel;
        private System.Windows.Forms.TextBox _remoteMachine;
    }
}
