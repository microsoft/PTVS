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
using System.Windows.Forms;
using Microsoft.PythonTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonProjectConfig : CommonProjectConfig {
        private readonly PythonProjectNode _project;

        public PythonProjectConfig(PythonProjectNode project, string configuration)
            : base(project, configuration) {
            _project = project;
        }

        /// <summary>
        /// The display name is a two part item
        /// first part is the config name, 2nd part is the platform name
        /// </summary>
        public override int get_DisplayName(out string name) {
            if (!string.IsNullOrEmpty(PlatformName)) {
                name = ConfigName + "|" + PlatformName;
                return VSConstants.S_OK;
            } else {
                return base.get_DisplayName(out name);
            }
        }

        public override int DebugLaunch(uint flags) {
            if (_project.ShouldWarnOnLaunch) {
                var pyService = ProjectMgr.Site.GetPythonToolsService();
                if (pyService.DebuggerOptions.PromptBeforeRunningWithBuildError) {
                    var res = new StartWithErrorsDialog(pyService).ShowDialog();
                    if (res == DialogResult.No) {
                        return VSConstants.S_OK;
                    }
                }
            }

            try {
                return base.DebugLaunch(flags);
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, SR.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return VSConstants.S_OK;
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(ProjectMgr.Site, ex.HelpPage);
                return VSConstants.S_OK;
            }
        }
    }
}
