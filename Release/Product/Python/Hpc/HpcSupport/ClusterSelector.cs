/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Hpc.Scheduler;
using Microsoft.Hpc.Scheduler.Properties;
using Microsoft.Hpc.Scheduler.Store;

namespace Microsoft.PythonTools.Hpc {
    public partial class ClusterSelector : Form {
        private ClusterEnvironment _environment;
        private string _lastNumProcsValue;

        internal ClusterSelector(ClusterEnvironment environment) {            
            InitializeComponent();

            _environment = environment;
            _headNodeCombo.Items.Add("localhost");
            if (environment.HeadNode != "localhost") {
                _headNodeCombo.Items.Add(environment.HeadNode);
                _headNodeCombo.SelectedIndex = 1;
            } else {
                _headNodeCombo.SelectedIndex = 0;
                _nodesView.Enabled = _manuallySelect.Enabled = _pickNodesCombo.Enabled = _scheduleOneCombo.Enabled = false;
            }
            
            _lastNumProcsValue = environment.NumberOfProcesses.ToString();
            
            _numOfProcsCombo.TextChanged -= NumOfProcsComboChanged;
            _numOfProcsCombo.Text = _lastNumProcsValue;
            _numOfProcsCombo.TextChanged += NumOfProcsComboChanged;

            _scheduleOneCombo.SelectedIndexChanged -= ScheduleOneComboSelectedIndexChanged;
            _scheduleOneCombo.SelectedIndex = (int)environment.ScheduleProcessPer;
            _scheduleOneCombo.SelectedIndexChanged += ScheduleOneComboSelectedIndexChanged;

            if (environment.SelectedNodes == null) {
                this._nodesView.CheckBoxes = false;
            } else {
                this._manuallySelect.Checked = true;
            }
        }

        protected override void OnShown(EventArgs e) {            
            base.OnShown(e);
            Debug.Assert(IsHandleCreated);  // handle needs to be available for BeginInvoke to succeed
            ThreadPool.QueueUserWorkItem(GetAvailableClusters);
        }

        private void GetAvailableClusters(object dummy) {
            string[] clusterNames = Cluster.GetClusters();
            Array.Sort(clusterNames);

            try {
                this.BeginInvoke((Action)(() => {
                    _headNodeCombo.Items.Clear();

                    _headNodeCombo.Items.Add("localhost");

                    foreach (var clusterName in clusterNames) {
                        _headNodeCombo.Items.Add(clusterName);

                        if (clusterName == _environment.HeadNode) {
                            _headNodeCombo.SelectedIndexChanged -= HeadNodeComboSelectedIndexChanged;
                            _headNodeCombo.SelectedIndex = _headNodeCombo.Items.Count - 1;
                            _headNodeCombo.SelectedIndexChanged += HeadNodeComboSelectedIndexChanged;
                        }
                    }
                }));
            } catch (InvalidOperationException) {
                // we can race w/ the form being closed (open form, close immediately before clusters are fetched)
            }
            

            UpdateClusterWorker(_environment.HeadNode);
        }

        private void ManuallySelectCheckedChanged(object sender, EventArgs e) {
            _nodesView.CheckBoxes = _manuallySelect.Checked;
        }

        private void HeadNodeComboSelectedIndexChanged(object sender, EventArgs e) {
            if (_headNodeCombo.Text != _environment.HeadNode) {
                UpdateCurrentCluster();
            }
        }

        private void HeadNodeComboLeave(object sender, EventArgs e) {
            if (_headNodeCombo.Text != _environment.HeadNode) {
                UpdateCurrentCluster();
            }
        }

        private void UpdateCurrentCluster() {
            UpdateEnvironment();

            if (_headNodeCombo.SelectedIndex == 0) {
                // localhost
                _nodesView.Items.Clear();
                _nodesView.Enabled = _manuallySelect.Enabled = _pickNodesCombo.Enabled = _scheduleOneCombo.Enabled = false;
            } else {
                _nodesView.Enabled = _manuallySelect.Enabled = _pickNodesCombo.Enabled = _scheduleOneCombo.Enabled = true;
                ThreadPool.QueueUserWorkItem(UpdateClusterWorker, _headNodeCombo.Text);
            }
        }

        private void UpdateClusterWorker(object newName) {
            try {
                string clusterName = (string)newName;
                List<string> groups = new List<string>();
                ISchedulerCollection nodes;

                try {
                    using (var scheduler = new Scheduler()) {
                        scheduler.Connect(clusterName);

                        nodes = scheduler.GetNodeList(null, null);

                        using (var store = SchedulerStore.Connect(clusterName)) {

                            foreach (var group in store.GetNodeGroups()) {
                                groups.Add(group.Name);
                            }
                        }
                    }
                } catch (SchedulerException) {
                    // unable to connect to the node.
                    return;
                }

                try {
                    this.BeginInvoke((Action)(() => {
                        _pickNodesCombo.SelectedIndexChanged -= PickNodesComboSelectedIndexChanged;
                        _pickNodesCombo.Items.Clear();
                        foreach (var group in groups) {
                            _pickNodesCombo.Items.Add(group);
                            if (group == _environment.PickNodesFrom) {
                                _pickNodesCombo.SelectedIndex = _pickNodesCombo.Items.Count - 1;
                            }
                        }

                        if (_pickNodesCombo.SelectedIndex == -1) {
                            _pickNodesCombo.SelectedIndex = 0;
                        }
                        _pickNodesCombo.SelectedIndexChanged += PickNodesComboSelectedIndexChanged;

                        _nodesView.Items.Clear();
                        int totalCores = 0;
                        string[] selectedNodes = _environment.SelectedNodes;
                        foreach (ISchedulerNode node in nodes) {
                            if (!node.NodeGroups.Contains(_pickNodesCombo.Text)) {
                                continue;
                            }
                            totalCores += node.NumberOfCores;

                            var item = new ListViewItem(
                                new[] { 
                                node.Name,
                                node.CpuSpeed.ToString(),
                                node.MemorySize.ToString(),
                                node.NumberOfCores.ToString(),
                                node.State.ToString()
                            }
                            );

                            if (selectedNodes != null && selectedNodes.Contains(node.Name)) {
                                item.Checked = true;
                            }

                            _nodesView.Items.Add(item);
                        }

                        _numOfProcsCombo.Items.Clear();
                        _numOfProcsCombo.TextChanged -= NumOfProcsComboChanged;
                        for (int i = 0; i < totalCores; i++) {
                            _numOfProcsCombo.Items.Add(i + 1);
                            if (i == _environment.NumberOfProcesses - 1) {
                                _numOfProcsCombo.SelectedIndex = i;
                                _lastNumProcsValue = _numOfProcsCombo.Text;
                            }
                        }
                        _numOfProcsCombo.TextChanged += NumOfProcsComboChanged;
                    }));
                } catch (InvalidOperationException) {
                    // we could race here w/ the form being closed and the handle being disposed.
                }
                
            } catch (System.Net.Sockets.SocketException) {
            }
        }

        public string Description {
            get {
                return _environment.ToString();
            }
        }

        private string GetSelectedNodes() {
            if (_manuallySelect.Checked) {
                List<string> selectedNodes = new List<string>();
                foreach (ListViewItem node in _nodesView.Items) {
                    if (node.Checked) {
                        selectedNodes.Add(node.Text);
                    }
                }

                if (selectedNodes.Count > 0) {
                    return String.Join(",", selectedNodes);
                }
            }
            return null;
        }

        private void NumOfProcsComboChanged(object sender, EventArgs e) {
            uint value;
            if (!UInt32.TryParse(_numOfProcsCombo.Text, out value) || (value > _numOfProcsCombo.Items.Count && _headNodeCombo.Text != "localhost")) {
                _numOfProcsCombo.TextChanged -= NumOfProcsComboChanged;
                _numOfProcsCombo.Text = _lastNumProcsValue;
                _numOfProcsCombo.TextChanged += NumOfProcsComboChanged;
            }
            _lastNumProcsValue = _numOfProcsCombo.Text;
            UpdateEnvironment();
        }

        private void UpdateEnvironment() {
            string[] checkedItems = null;
            if (_manuallySelect.Checked) {
                List<string> checks = new List<string>();
                foreach (ListViewItem item in _nodesView.Items) {
                    if (item.Checked) {
                        checks.Add(item.Text);
                    }
                }
                checkedItems = checks.ToArray();
            }

            _environment = new ClusterEnvironment(_headNodeCombo.Text,
                (int)UInt32.Parse(_numOfProcsCombo.Text),
                (ScheduleProcessPer)_scheduleOneCombo.SelectedIndex,
                _pickNodesCombo.Text,
                checkedItems
            );
        }

        private void ScheduleOneComboSelectedIndexChanged(object sender, EventArgs e) {
            UpdateEnvironment();
        }

        private void PickNodesComboSelectedIndexChanged(object sender, EventArgs e) {
            UpdateEnvironment();
            UpdateClusterWorker(_environment.HeadNode);
        }

        private void _nodesView_ItemChecked(object sender, ItemCheckedEventArgs e) {
            UpdateEnvironment();
        }
    }
}
