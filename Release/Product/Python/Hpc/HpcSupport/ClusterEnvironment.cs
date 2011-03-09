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
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;

namespace Microsoft.PythonTools.Hpc {
    [Editor(typeof(RunEnvironmentEditor), typeof(UITypeEditor))]
    [TypeConverter(typeof(ExpandableObjectConverter))] 
    class ClusterEnvironment {
        private readonly string _headNode;
        private readonly int _numberOfProcesses;
        private readonly ScheduleProcessPer _schedulePer;
        private readonly string _pickNodesFrom;
        private readonly string[] _selectedNodes;

        public ClusterEnvironment(string description) {
            if (description != null) {
                string[] elements = description.Split('/');
                if (elements.Length >= 1) {
                    _headNode = elements[0];
                    if (elements.Length >= 2) {

                        Int32.TryParse(elements[1], out _numberOfProcesses);

                        if (elements.Length >= 3) {
                            switch (elements[2].ToLower(CultureInfo.InvariantCulture)) {
                                case "node": _schedulePer = ScheduleProcessPer.Node; break;
                                case "socket": _schedulePer = ScheduleProcessPer.Socket; break;
                                case "core": _schedulePer = ScheduleProcessPer.Core; break;
                            }

                            if (elements.Length >= 4) {
                                _pickNodesFrom = elements[3];

                                if (elements.Length >= 5) {
                                    _selectedNodes = elements[4].Split(',');
                                }
                            }
                        }
                    }
                }
            } else {
                _headNode = "localhost";
                _numberOfProcesses = 1;
            }
        }

        public ClusterEnvironment(string headNode, int numberOfProcesses, ScheduleProcessPer schedulePer, string pickNodesFrom, string[] selectedItems) {
            _headNode = headNode;
            _numberOfProcesses = numberOfProcesses;
            _schedulePer = schedulePer;
            _pickNodesFrom = pickNodesFrom;
            _selectedNodes = selectedItems;
        }

        public string HeadNode {
            get {
                return _headNode;
            }
        }

        public int NumberOfProcesses {
            get {
                return _numberOfProcesses;
            }
        }

        public ScheduleProcessPer ScheduleProcessPer {
            get {
                return _schedulePer;
            }

        }

        public string PickNodesFrom {
            get {
                return _pickNodesFrom;
            }
        }

        public string[] SelectedNodes {
            get {
                return _selectedNodes;
            }
        }        

        public override string ToString() {
            string res = _headNode + "/" + _numberOfProcesses + "/" + _schedulePer + "/" + _pickNodesFrom;

            if (_selectedNodes != null) {
                res += "/" + String.Join(",", _selectedNodes);
            }
            return res;
        }
    }
}
