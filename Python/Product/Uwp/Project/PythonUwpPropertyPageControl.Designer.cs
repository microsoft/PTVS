namespace Microsoft.PythonTools.Uwp.Project {
    partial class PythonUwpPropertyPageControl {
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
            System.Windows.Forms.TableLayoutPanel _uwpRemoteSettingsPanel;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonUwpPropertyPageControl));
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            this._remoteDeviceLabel = new System.Windows.Forms.Label();
            this._remoteDevice = new System.Windows.Forms.TextBox();
            this._remotePortLabel = new System.Windows.Forms.Label();
            this._remotePort = new System.Windows.Forms.NumericUpDown();
            this._uwpDebugGroup = new System.Windows.Forms.GroupBox();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            _uwpRemoteSettingsPanel = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            _uwpRemoteSettingsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._remotePort)).BeginInit();
            tableLayoutPanel1.SuspendLayout();
            this._uwpDebugGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // _uwpRemoteSettingsPanel
            // 
            resources.ApplyResources(_uwpRemoteSettingsPanel, "_uwpRemoteSettingsPanel");
            _uwpRemoteSettingsPanel.Controls.Add(this._remoteDeviceLabel, 0, 0);
            _uwpRemoteSettingsPanel.Controls.Add(this._remoteDevice, 1, 0);
            _uwpRemoteSettingsPanel.Controls.Add(this._remotePortLabel, 0, 1);
            _uwpRemoteSettingsPanel.Controls.Add(this._remotePort, 1, 1);
            _uwpRemoteSettingsPanel.Name = "_uwpRemoteSettingsPanel";
            // 
            // _remoteDeviceLabel
            // 
            resources.ApplyResources(this._remoteDeviceLabel, "_remoteDeviceLabel");
            this._remoteDeviceLabel.Name = "_remoteDeviceLabel";
            // 
            // _remoteDevice
            // 
            resources.ApplyResources(this._remoteDevice, "_remoteDevice");
            this._remoteDevice.Name = "_remoteDevice";
            this._remoteDevice.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _remotePortLabel
            // 
            resources.ApplyResources(this._remotePortLabel, "_remotePortLabel");
            this._remotePortLabel.Name = "_remotePortLabel";
            // 
            // _remotePort
            // 
            resources.ApplyResources(this._remotePort, "_remotePort");
            this._remotePort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this._remotePort.Name = "_remotePort";
            this._remotePort.Value = new decimal(new int[] {
            5678,
            0,
            0,
            0});
            this._remotePort.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(this._uwpDebugGroup, 0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _uwpDebugGroup
            // 
            resources.ApplyResources(this._uwpDebugGroup, "_uwpDebugGroup");
            this._uwpDebugGroup.Controls.Add(_uwpRemoteSettingsPanel);
            this._uwpDebugGroup.Name = "_uwpDebugGroup";
            this._uwpDebugGroup.TabStop = false;
            // 
            // PythonUwpPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(tableLayoutPanel1);
            this.Name = "PythonUwpPropertyPageControl";
            _uwpRemoteSettingsPanel.ResumeLayout(false);
            _uwpRemoteSettingsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._remotePort)).EndInit();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this._uwpDebugGroup.ResumeLayout(false);
            this._uwpDebugGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _uwpDebugGroup;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Label _remoteDeviceLabel;
        private System.Windows.Forms.NumericUpDown _remotePort;
        private System.Windows.Forms.Label _remotePortLabel;
        private System.Windows.Forms.TextBox _remoteDevice;
    }
}
