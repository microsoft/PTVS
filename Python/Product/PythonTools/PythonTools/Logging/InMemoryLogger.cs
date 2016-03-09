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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Keeps track of logged events and makes them available for display in the diagnostics window.
    /// </summary>
    [Export(typeof(IPythonToolsLogger))]
    [Export(typeof(InMemoryLogger))]
    class InMemoryLogger : IPythonToolsLogger {
        private int _installedInterpreters;
        private int _configuredInterpreters;
        private int _debugLaunchCount, _normalLaunchCount;
        private List<PackageInstallDetails> _packageInstalls = new List<PackageInstallDetails>();
        private List<string> _analysisAbnormalities = new List<string>();        

        #region IPythonToolsLogger Members

        public void LogEvent(PythonLogEvent logEvent, object argument) {
            switch (logEvent) {
                case PythonLogEvent.Launch:
                    if ((int)argument != 0) {
                        _debugLaunchCount++;
                    } else {
                        _normalLaunchCount++;
                    }
                    break;
                case PythonLogEvent.InstalledInterpreters:
                    _installedInterpreters = (int)argument;
                    break;
                case PythonLogEvent.ConfiguredInterpreters:
                    _configuredInterpreters = (int)argument;
                    break;
                case PythonLogEvent.PackageInstalled:
                    var packageInstallDetails = argument as PackageInstallDetails;
                    if (packageInstallDetails != null) {
                        _packageInstalls.Add(packageInstallDetails);
                    }
                    break;
                case PythonLogEvent.AnalysisExitedAbnormally:
                    _analysisAbnormalities.Add(DateTime.Now + " Abnormal exit: " + argument);
                    break;
                case PythonLogEvent.AnalysisOperationCancelled:
                    _analysisAbnormalities.Add(DateTime.Now + " Operation Cancelled");
                    break;
                case PythonLogEvent.AnalysisOpertionFailed:
                    _analysisAbnormalities.Add(DateTime.Now + " Operation Failed " + argument);
                    break;
            }
        }

        #endregion

        public override string ToString() {
            StringBuilder res = new StringBuilder();
            res.AppendLine("Installed Interpreters: " + _installedInterpreters);
            res.AppendLine("Configured Interpreters: " + _configuredInterpreters);
            res.AppendLine("Debug Launches: " + _debugLaunchCount);
            res.AppendLine("Normal Launches: " + _normalLaunchCount);

            res.AppendLine();
            if (_packageInstalls.Count > 0) {
                res.AppendLine("Installed Packages:");
                res.AppendLine(PackageInstallDetails.Header());
                res.AppendLine("  Successful Installations");
                foreach (PackageInstallDetails pd in _packageInstalls.Where(p => p.InstallResult == 0)) {
                    res.AppendLine("    " + pd.ToString());
                }
                res.AppendLine();
                res.AppendLine("  Failed Installations");
                foreach (PackageInstallDetails pd in _packageInstalls.Where(p => p.InstallResult != 0)) {
                    res.AppendLine("    " + pd.ToString());
                }
            }

            if (_analysisAbnormalities.Count > 0) {
                res.AppendFormat("Analysis abnormalities ({0}):", _analysisAbnormalities.Count);
                res.AppendLine();
                foreach (var abnormalExit in _analysisAbnormalities) {
                    res.AppendLine(abnormalExit);
                }
            }

            return res.ToString();
        }
    }
}
