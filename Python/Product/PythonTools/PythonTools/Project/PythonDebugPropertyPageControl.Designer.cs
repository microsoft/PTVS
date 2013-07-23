namespace Microsoft.PythonTools.Project {
    partial class PythonDebugPropertyPageControl {
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
            this._launchModeLabel = new System.Windows.Forms.Label();
            this._launchModeCombo = new System.Windows.Forms.ComboBox();
            this.tableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayout.SuspendLayout();
            this.SuspendLayout();
            // 
            // _launchModeLabel
            // 
            this._launchModeLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._launchModeLabel.AutoEllipsis = true;
            this._launchModeLabel.AutoSize = true;
            this._launchModeLabel.Location = new System.Drawing.Point(6, 12);
            this._launchModeLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._launchModeLabel.Name = "_launchModeLabel";
            this._launchModeLabel.Size = new System.Drawing.Size(79, 13);
            this._launchModeLabel.TabIndex = 0;
            this._launchModeLabel.Text = "Launch mode:";
            this._launchModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _launchModeCombo
            // 
            this._launchModeCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._launchModeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._launchModeCombo.FormattingEnabled = true;
            this._launchModeCombo.Location = new System.Drawing.Point(97, 8);
            this._launchModeCombo.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._launchModeCombo.MinimumSize = new System.Drawing.Size(50, 0);
            this._launchModeCombo.Name = "_launchModeCombo";
            this._launchModeCombo.Size = new System.Drawing.Size(351, 21);
            this._launchModeCombo.TabIndex = 1;
            this._launchModeCombo.SelectedIndexChanged += new System.EventHandler(this.LaunchModeComboSelectedIndexChanged);
            // 
            // tableLayout
            // 
            this.tableLayout.AutoScroll = true;
            this.tableLayout.ColumnCount = 2;
            this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayout.Controls.Add(this._launchModeLabel, 0, 0);
            this.tableLayout.Controls.Add(this._launchModeCombo, 1, 0);
            this.tableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayout.Location = new System.Drawing.Point(0, 0);
            this.tableLayout.MinimumSize = new System.Drawing.Size(450, 0);
            this.tableLayout.Name = "tableLayout";
            this.tableLayout.RowCount = 2;
            this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayout.Size = new System.Drawing.Size(454, 171);
            this.tableLayout.TabIndex = 0;
            // 
            // PythonDebugPropertyPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayout);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonDebugPropertyPageControl";
            this.Size = new System.Drawing.Size(454, 171);
            this.tableLayout.ResumeLayout(false);
            this.tableLayout.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _launchModeLabel;
        private System.Windows.Forms.ComboBox _launchModeCombo;
        private System.Windows.Forms.TableLayoutPanel tableLayout;
    }
}
