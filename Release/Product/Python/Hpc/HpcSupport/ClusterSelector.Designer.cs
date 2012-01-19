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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _headNodeLabel
            // 
            this._headNodeLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._headNodeLabel.AutoSize = true;
            this._headNodeLabel.Location = new System.Drawing.Point(6, 7);
            this._headNodeLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._headNodeLabel.Name = "_headNodeLabel";
            this._headNodeLabel.Size = new System.Drawing.Size(65, 13);
            this._headNodeLabel.TabIndex = 0;
            this._headNodeLabel.Text = "&Head Node:";
            this._headNodeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _numProcsLabel
            // 
            this._numProcsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._numProcsLabel.AutoSize = true;
            this._numProcsLabel.Location = new System.Drawing.Point(6, 34);
            this._numProcsLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._numProcsLabel.Name = "_numProcsLabel";
            this._numProcsLabel.Size = new System.Drawing.Size(110, 13);
            this._numProcsLabel.TabIndex = 1;
            this._numProcsLabel.Text = "&Number of processes:";
            this._numProcsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _scheduleOnePerProcLabel
            // 
            this._scheduleOnePerProcLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._scheduleOnePerProcLabel.AutoSize = true;
            this._scheduleOnePerProcLabel.Location = new System.Drawing.Point(6, 61);
            this._scheduleOnePerProcLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._scheduleOnePerProcLabel.Name = "_scheduleOnePerProcLabel";
            this._scheduleOnePerProcLabel.Size = new System.Drawing.Size(134, 13);
            this._scheduleOnePerProcLabel.TabIndex = 2;
            this._scheduleOnePerProcLabel.Text = "&Schedule one process per:";
            this._scheduleOnePerProcLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _pickNodesFromLabel
            // 
            this._pickNodesFromLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._pickNodesFromLabel.AutoSize = true;
            this._pickNodesFromLabel.Location = new System.Drawing.Point(6, 88);
            this._pickNodesFromLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._pickNodesFromLabel.Name = "_pickNodesFromLabel";
            this._pickNodesFromLabel.Size = new System.Drawing.Size(86, 13);
            this._pickNodesFromLabel.TabIndex = 3;
            this._pickNodesFromLabel.Text = "&Pick nodes from:";
            this._pickNodesFromLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _headNodeCombo
            // 
            this._headNodeCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this._headNodeCombo, 3);
            this._headNodeCombo.FormattingEnabled = true;
            this._headNodeCombo.Location = new System.Drawing.Point(152, 3);
            this._headNodeCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._headNodeCombo.Name = "_headNodeCombo";
            this._headNodeCombo.Size = new System.Drawing.Size(502, 21);
            this._headNodeCombo.TabIndex = 4;
            this._headNodeCombo.Text = "localhost";
            this._headNodeCombo.SelectedIndexChanged += new System.EventHandler(this.HeadNodeComboSelectedIndexChanged);
            this._headNodeCombo.Leave += new System.EventHandler(this.HeadNodeComboLeave);
            // 
            // _numOfProcsCombo
            // 
            this._numOfProcsCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this._numOfProcsCombo, 3);
            this._numOfProcsCombo.FormattingEnabled = true;
            this._numOfProcsCombo.Location = new System.Drawing.Point(152, 30);
            this._numOfProcsCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._numOfProcsCombo.Name = "_numOfProcsCombo";
            this._numOfProcsCombo.Size = new System.Drawing.Size(502, 21);
            this._numOfProcsCombo.TabIndex = 5;
            this._numOfProcsCombo.Text = "1";
            this._numOfProcsCombo.SelectedIndexChanged += new System.EventHandler(this.NumOfProcsComboChanged);
            this._numOfProcsCombo.TextChanged += new System.EventHandler(this.NumOfProcsComboChanged);
            // 
            // _scheduleOneCombo
            // 
            this._scheduleOneCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this._scheduleOneCombo, 3);
            this._scheduleOneCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._scheduleOneCombo.FormattingEnabled = true;
            this._scheduleOneCombo.Items.AddRange(new object[] {
            "Core",
            "Socket",
            "Node"});
            this._scheduleOneCombo.Location = new System.Drawing.Point(152, 57);
            this._scheduleOneCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._scheduleOneCombo.Name = "_scheduleOneCombo";
            this._scheduleOneCombo.Size = new System.Drawing.Size(502, 21);
            this._scheduleOneCombo.TabIndex = 6;
            this._scheduleOneCombo.SelectedIndexChanged += new System.EventHandler(this.ScheduleOneComboSelectedIndexChanged);
            // 
            // _pickNodesCombo
            // 
            this._pickNodesCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this._pickNodesCombo, 3);
            this._pickNodesCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._pickNodesCombo.FormattingEnabled = true;
            this._pickNodesCombo.Location = new System.Drawing.Point(152, 84);
            this._pickNodesCombo.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._pickNodesCombo.Name = "_pickNodesCombo";
            this._pickNodesCombo.Size = new System.Drawing.Size(502, 21);
            this._pickNodesCombo.TabIndex = 7;
            this._pickNodesCombo.SelectedIndexChanged += new System.EventHandler(this.PickNodesComboSelectedIndexChanged);
            // 
            // _nodesView
            // 
            this._nodesView.CheckBoxes = true;
            this._nodesView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._nodeColumn,
            this._cpuMhz,
            this._memory,
            this._cores,
            this._stateColumn});
            this.tableLayoutPanel1.SetColumnSpan(this._nodesView, 4);
            this._nodesView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._nodesView.Location = new System.Drawing.Point(6, 134);
            this._nodesView.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._nodesView.Name = "_nodesView";
            this._nodesView.Size = new System.Drawing.Size(648, 323);
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
            this._ok.AutoSize = true;
            this._ok.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._ok.Location = new System.Drawing.Point(512, 463);
            this._ok.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._ok.Name = "_ok";
            this._ok.Padding = new System.Windows.Forms.Padding(12, 3, 12, 3);
            this._ok.Size = new System.Drawing.Size(56, 29);
            this._ok.TabIndex = 9;
            this._ok.Text = "&OK";
            this._ok.UseVisualStyleBackColor = true;
            // 
            // _cancel
            // 
            this._cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._cancel.AutoSize = true;
            this._cancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(580, 463);
            this._cancel.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._cancel.Name = "_cancel";
            this._cancel.Padding = new System.Windows.Forms.Padding(12, 3, 12, 3);
            this._cancel.Size = new System.Drawing.Size(74, 29);
            this._cancel.TabIndex = 10;
            this._cancel.Text = "&Cancel";
            this._cancel.UseVisualStyleBackColor = true;
            // 
            // _manuallySelect
            // 
            this._manuallySelect.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this._manuallySelect, 4);
            this._manuallySelect.Dock = System.Windows.Forms.DockStyle.Fill;
            this._manuallySelect.Location = new System.Drawing.Point(6, 111);
            this._manuallySelect.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._manuallySelect.Name = "_manuallySelect";
            this._manuallySelect.Size = new System.Drawing.Size(648, 17);
            this._manuallySelect.TabIndex = 11;
            this._manuallySelect.Text = "Manually select nodes to include in the allocation";
            this._manuallySelect.UseVisualStyleBackColor = true;
            this._manuallySelect.CheckedChanged += new System.EventHandler(this.ManuallySelectCheckedChanged);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this._headNodeLabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this._cancel, 3, 6);
            this.tableLayoutPanel1.Controls.Add(this._manuallySelect, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this._ok, 2, 6);
            this.tableLayoutPanel1.Controls.Add(this._numProcsLabel, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this._pickNodesCombo, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this._nodesView, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this._scheduleOneCombo, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this._scheduleOnePerProcLabel, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this._numOfProcsCombo, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this._pickNodesFromLabel, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this._headNodeCombo, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 7;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(660, 495);
            this.tableLayoutPanel1.TabIndex = 12;
            // 
            // ClusterSelector
            // 
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(660, 495);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ClusterSelector";
            this.ShowIcon = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Cluster Selector";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

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
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}