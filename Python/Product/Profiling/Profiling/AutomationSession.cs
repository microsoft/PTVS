// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Profiling {
    [ComVisible(true)]
    public sealed class AutomationSession : IPythonProfileSession {
        private readonly SessionNode _node;

        internal AutomationSession(SessionNode session) {
            _node = session;
        }

        #region IPythonProfileSession Members


        public string Name {
            get {
                return _node.Name;
            }
        }

        public string Filename {
            get {
                return _node.Filename;

            }
        }

        public IPythonPerformanceReport GetReport(object item) {
            if (item is int) {
                int id = (int)item - 1;
                if (id >= 0 && id < _node.Reports.Count) {
                    return new ReportWrapper(_node.Reports.Values.ElementAt(id));
                }
            } else if (item is string) {
                string filename = (string)item;
                foreach (var report in _node.Reports.Values) {
                    if (filename == report.Filename || Path.GetFileNameWithoutExtension(report.Filename) == filename) {
                        return new ReportWrapper(report);
                    }
                }
            }
            return null;
        }

        public void Launch(bool openReport) {
            _node.StartProfiling(openReport);
        }

        public void Save(string filename = null) {
            _node.Save(filename);
        }

        public bool IsSaved {
            get {
                return _node.IsSaved;
            }
        }

        #endregion
    }
}
