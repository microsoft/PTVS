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
            System.Windows.Forms.TableLayoutPanel _uapRemoteSettingsPanel;
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            this._remoteDeviceLabel = new System.Windows.Forms.Label();
            this._remoteDevice = new System.Windows.Forms.TextBox();
            this._remotePortLabel = new System.Windows.Forms.Label();
            this._remotePort = new System.Windows.Forms.NumericUpDown();
            this._uapDebugGroup = new System.Windows.Forms.GroupBox();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            _uapRemoteSettingsPanel = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            _uapRemoteSettingsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._remotePort)).BeginInit();
            tableLayoutPanel1.SuspendLayout();
            this._uapDebugGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _uapRemoteSettingsPanel
            // 
            _uapRemoteSettingsPanel.AutoSize = true;
            _uapRemoteSettingsPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            _uapRemoteSettingsPanel.ColumnCount = 2;
            _uapRemoteSettingsPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            _uapRemoteSettingsPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            _uapRemoteSettingsPanel.Controls.Add(this._remoteDeviceLabel, 0, 0);
            _uapRemoteSettingsPanel.Controls.Add(this._remoteDevice, 1, 0);
            _uapRemoteSettingsPanel.Controls.Add(this._remotePortLabel, 0, 1);
            _uapRemoteSettingsPanel.Controls.Add(this._remotePort, 1, 1);
            _uapRemoteSettingsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            _uapRemoteSettingsPanel.Location = new System.Drawing.Point(6, 21);
            _uapRemoteSettingsPanel.Name = "_uapRemoteSettingsPanel";
            _uapRemoteSettingsPanel.RowCount = 2;
            _uapRemoteSettingsPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            _uapRemoteSettingsPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            _uapRemoteSettingsPanel.Size = new System.Drawing.Size(399, 52);
            _uapRemoteSettingsPanel.TabIndex = 0;
            // 
            // _remoteDeviceLabel
            // 
            this._remoteDeviceLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._remoteDeviceLabel.AutoSize = true;
            this._remoteDeviceLabel.Location = new System.Drawing.Point(6, 6);
            this._remoteDeviceLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._remoteDeviceLabel.Name = "_remoteDeviceLabel";
            this._remoteDeviceLabel.Size = new System.Drawing.Size(84, 13);
            this._remoteDeviceLabel.TabIndex = 2;
            this._remoteDeviceLabel.Text = "Remote &Device:";
            this._remoteDeviceLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _remoteDevice
            // 
            this._remoteDevice.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._remoteDevice.Location = new System.Drawing.Point(102, 3);
            this._remoteDevice.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._remoteDevice.MinimumSize = new System.Drawing.Size(50, 4);
            this._remoteDevice.Name = "_remoteDevice";
            this._remoteDevice.Size = new System.Drawing.Size(291, 20);
            this._remoteDevice.TabIndex = 4;
            // 
            // _remotePortLabel
            // 
            this._remotePortLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._remotePortLabel.AutoSize = true;
            this._remotePortLabel.Location = new System.Drawing.Point(6, 32);
            this._remotePortLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._remotePortLabel.Name = "_remotePortLabel";
            this._remotePortLabel.Size = new System.Drawing.Size(69, 13);
            this._remotePortLabel.TabIndex = 5;
            this._remotePortLabel.Text = "Remote &Port:";
            this._remotePortLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _remotePort
            // 
            this._remotePort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._remotePort.Location = new System.Drawing.Point(102, 29);
            this._remotePort.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._remotePort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this._remotePort.MinimumSize = new System.Drawing.Size(50, 0);
            this._remotePort.Name = "_remotePort";
            this._remotePort.Size = new System.Drawing.Size(61, 20);
            this._remotePort.TabIndex = 3;
            this._remotePort.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this._remotePort.Value = new decimal(new int[] {
            5678,
            0,
            0,
            0});
            this._remotePort.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            this._remoteDevice.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(this._uapDebugGroup, 0, 0);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel1.Size = new System.Drawing.Size(423, 117);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // _uapDebugGroup
            // 
            this._uapDebugGroup.AutoSize = true;
            this._uapDebugGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._uapDebugGroup.Controls.Add(_uapRemoteSettingsPanel);
            this._uapDebugGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._uapDebugGroup.Location = new System.Drawing.Point(6, 8);
            this._uapDebugGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._uapDebugGroup.Name = "_uapDebugGroup";
            this._uapDebugGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._uapDebugGroup.Size = new System.Drawing.Size(411, 81);
            this._uapDebugGroup.TabIndex = 0;
            this._uapDebugGroup.TabStop = false;
            this._uapDebugGroup.Text = "Debug Settings";
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
            this.Size = new System.Drawing.Size(423, 117);
            _uapRemoteSettingsPanel.ResumeLayout(false);
            _uapRemoteSettingsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._remotePort)).EndInit();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this._uapDebugGroup.ResumeLayout(false);
            this._uapDebugGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _uapDebugGroup;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Label _remoteDeviceLabel;
        private System.Windows.Forms.NumericUpDown _remotePort;
        private System.Windows.Forms.Label _remotePortLabel;
        private System.Windows.Forms.TextBox _remoteDevice;
    }
}
