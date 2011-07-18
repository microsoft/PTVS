namespace Microsoft.PythonTools.Hpc {
    partial class ClusterSelector {
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this._headNodeLabel = new System.Windows.Forms.Label();
            this._numProcsLabel = new System.Windows.Forms.Label();
            this._scheduleOnePerProcLabel = new System.Windows.Forms.Label();
            this._pickNodesFromLabel = new System.Windows.Forms.Label();
            this._headNodeCombo = new System.Windows.Forms.ComboBox();
            this._numOfProcsCombo = new System.Windows.Forms.ComboBox();
            this._scheduleOneCombo = new System.Windows.Forms.ComboBox();
            this._pickNodesCombo = new System.Windows.Forms.ComboBox();
            this._nodesView = new System.Windows.Forms.ListView();
            this._nodeColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._cpuMhz = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._memory = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._cores = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._stateColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._ok = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            this._manuallySelect = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // _headNodeLabel
            // 
            this._headNodeLabel.AutoSize = true;
            this._headNodeLabel.Location = new System.Drawing.Point(13, 16);
            this._headNodeLabel.Name = "_headNodeLabel";
            this._headNodeLabel.Size = new System.Drawing.Size(65, 13);
            this._headNodeLabel.TabIndex = 0;
            this._headNodeLabel.Text = "&Head Node:";
            // 
            // _numProcsLabel
            // 
            this._numProcsLabel.AutoSize = true;
            this._numProcsLabel.Location = new System.Drawing.Point(13, 44);
            this._numProcsLabel.Name = "_numProcsLabel";
            this._numProcsLabel.Size = new System.Drawing.Size(110, 13);
            this._numProcsLabel.TabIndex = 1;
            this._numProcsLabel.Text = "&Number of processes:";
            // 
            // _scheduleOnePerProcLabel
            // 
            this._scheduleOnePerProcLabel.AutoSize = true;
            this._scheduleOnePerProcLabel.Location = new System.Drawing.Point(13, 72);
            this._scheduleOnePerProcLabel.Name = "_scheduleOnePerProcLabel";
            this._scheduleOnePerProcLabel.Size = new System.Drawing.Size(134, 13);
            this._scheduleOnePerProcLabel.TabIndex = 2;
            this._scheduleOnePerProcLabel.Text = "&Schedule one process per:";
            // 
            // _pickNodesFromLabel
            // 
            this._pickNodesFromLabel.AutoSize = true;
            this._pickNodesFromLabel.Location = new System.Drawing.Point(13, 99);
            this._pickNodesFromLabel.Name = "_pickNodesFromLabel";
            this._pickNodesFromLabel.Size = new System.Drawing.Size(86, 13);
            this._pickNodesFromLabel.TabIndex = 3;
            this._pickNodesFromLabel.Text = "&Pick nodes from:";
            // 
            // _headNodeCombo
            // 
            this._headNodeCombo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this._headNodeCombo.FormattingEnabled = true;
            this._headNodeCombo.Location = new System.Drawing.Point(152, 13);
            this._headNodeCombo.Name = "_headNodeCombo";
            this._headNodeCombo.Size = new System.Drawing.Size(521, 21);
            this._headNodeCombo.TabIndex = 4;
            this._headNodeCombo.Text = "localhost";
            this._headNodeCombo.SelectedIndexChanged += new System.EventHandler(this.HeadNodeComboSelectedIndexChanged);
            this._headNodeCombo.Leave += new System.EventHandler(this.HeadNodeComboLeave);
            // 
            // _numOfProcsCombo
            // 
            this._numOfProcsCombo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this._numOfProcsCombo.FormattingEnabled = true;
            this._numOfProcsCombo.Location = new System.Drawing.Point(152, 41);
            this._numOfProcsCombo.Name = "_numOfProcsCombo";
            this._numOfProcsCombo.Size = new System.Drawing.Size(521, 21);
            this._numOfProcsCombo.TabIndex = 5;
            this._numOfProcsCombo.Text = "1";
            this._numOfProcsCombo.SelectedIndexChanged += new System.EventHandler(this.NumOfProcsComboChanged);
            this._numOfProcsCombo.TextChanged += new System.EventHandler(this.NumOfProcsComboChanged);
            // 
            // _scheduleOneCombo
            // 
            this._scheduleOneCombo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this._scheduleOneCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._scheduleOneCombo.FormattingEnabled = true;
            this._scheduleOneCombo.Items.AddRange(new object[] {
            "Core",
            "Socket",
            "Node"});
            this._scheduleOneCombo.Location = new System.Drawing.Point(152, 69);
            this._scheduleOneCombo.Name = "_scheduleOneCombo";
            this._scheduleOneCombo.Size = new System.Drawing.Size(521, 21);
            this._scheduleOneCombo.TabIndex = 6;
            this._scheduleOneCombo.SelectedIndexChanged += new System.EventHandler(this.ScheduleOneComboSelectedIndexChanged);
            // 
            // _pickNodesCombo
            // 
            this._pickNodesCombo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this._pickNodesCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._pickNodesCombo.FormattingEnabled = true;
            this._pickNodesCombo.Location = new System.Drawing.Point(152, 96);
            this._pickNodesCombo.Name = "_pickNodesCombo";
            this._pickNodesCombo.Size = new System.Drawing.Size(521, 21);
            this._pickNodesCombo.TabIndex = 7;
            this._pickNodesCombo.SelectedIndexChanged += new System.EventHandler(this.PickNodesComboSelectedIndexChanged);
            // 
            // _nodesView
            // 
            this._nodesView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this._nodesView.CheckBoxes = true;
            this._nodesView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._nodeColumn,
            this._cpuMhz,
            this._memory,
            this._cores,
            this._stateColumn});
            this._nodesView.Location = new System.Drawing.Point(16, 147);
            this._nodesView.Name = "_nodesView";
            this._nodesView.Size = new System.Drawing.Size(657, 217);
            this._nodesView.TabIndex = 8;
            this._nodesView.UseCompatibleStateImageBehavior = false;
            this._nodesView.View = System.Windows.Forms.View.Details;
            this._nodesView.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this._nodesView_ItemChecked);
            // 
            // _nodeColumn
            // 
            this._nodeColumn.Text = "Node";
            this._nodeColumn.Width = 101;
            // 
            // _cpuMhz
            // 
            this._cpuMhz.Text = "CPU (MHz)";
            this._cpuMhz.Width = 86;
            // 
            // _memory
            // 
            this._memory.Text = "Memory (MB)";
            this._memory.Width = 94;
            // 
            // _cores
            // 
            this._cores.Text = "Cores";
            this._cores.Width = 90;
            // 
            // _stateColumn
            // 
            this._stateColumn.Text = "State";
            this._stateColumn.Width = 78;
            // 
            // _ok
            // 
            this._ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._ok.Location = new System.Drawing.Point(521, 370);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(75, 23);
            this._ok.TabIndex = 9;
            this._ok.Text = "&OK";
            this._ok.UseVisualStyleBackColor = true;
            // 
            // _cancel
            // 
            this._cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(602, 370);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 23);
            this._cancel.TabIndex = 10;
            this._cancel.Text = "&Cancel";
            this._cancel.UseVisualStyleBackColor = true;
            // 
            // _manuallySelect
            // 
            this._manuallySelect.AutoSize = true;
            this._manuallySelect.Location = new System.Drawing.Point(16, 124);
            this._manuallySelect.Name = "_manuallySelect";
            this._manuallySelect.Size = new System.Drawing.Size(257, 17);
            this._manuallySelect.TabIndex = 11;
            this._manuallySelect.Text = "Manually select nodes to include in the allocation";
            this._manuallySelect.UseVisualStyleBackColor = true;
            this._manuallySelect.CheckedChanged += new System.EventHandler(this.ManuallySelectCheckedChanged);
            // 
            // ClusterSelector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(685, 405);
            this.Controls.Add(this._manuallySelect);
            this.Controls.Add(this._cancel);
            this.Controls.Add(this._ok);
            this.Controls.Add(this._nodesView);
            this.Controls.Add(this._pickNodesCombo);
            this.Controls.Add(this._scheduleOneCombo);
            this.Controls.Add(this._numOfProcsCombo);
            this.Controls.Add(this._headNodeCombo);
            this.Controls.Add(this._pickNodesFromLabel);
            this.Controls.Add(this._scheduleOnePerProcLabel);
            this.Controls.Add(this._numProcsLabel);
            this.Controls.Add(this._headNodeLabel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ClusterSelector";
            this.ShowIcon = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Cluster Selector";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _headNodeLabel;
        private System.Windows.Forms.Label _numProcsLabel;
        private System.Windows.Forms.Label _scheduleOnePerProcLabel;
        private System.Windows.Forms.Label _pickNodesFromLabel;
        private System.Windows.Forms.ComboBox _headNodeCombo;
        private System.Windows.Forms.ComboBox _numOfProcsCombo;
        private System.Windows.Forms.ComboBox _scheduleOneCombo;
        private System.Windows.Forms.ComboBox _pickNodesCombo;
        private System.Windows.Forms.ListView _nodesView;
        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.ColumnHeader _nodeColumn;
        private System.Windows.Forms.ColumnHeader _stateColumn;
        private System.Windows.Forms.ColumnHeader _cpuMhz;
        private System.Windows.Forms.ColumnHeader _memory;
        private System.Windows.Forms.ColumnHeader _cores;
        private System.Windows.Forms.CheckBox _manuallySelect;
    }
}