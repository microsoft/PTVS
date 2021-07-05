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

using Microsoft.PythonTools.Debugger;
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

            string errorMessage = null;
            try {
                return base.DebugLaunch(flags);
            } catch (MissingInterpreterException ex) {
                if (_project.ActiveInterpreter == _project.InterpreterRegistry.NoInterpretersValue) {
                    PythonToolsPackage.OpenNoInterpretersHelpPage(ProjectMgr.Site, ex.HelpPage);
                } else {
                    errorMessage = ex.Message;
                }
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(ProjectMgr.Site, ex.HelpPage);
            } catch (IOException ex) {
                errorMessage = ex.Message;
            } catch (NoStartupFileException ex) {
                errorMessage = ex.Message;
            } catch (ArgumentException ex) {
                // Previously used to handle "No startup file" which now has its own exception.
                // Keeping it in case some launchers started relying on us catching this.
                errorMessage = ex.Message;
            }

            if (!string.IsNullOrEmpty(errorMessage)) {
                var td = new TaskDialog(ProjectMgr.Site) {
                    Title = Strings.ProductTitle,
                    MainInstruction = Strings.FailedToLaunchDebugger,
                    Content = errorMessage,
                    AllowCancellation = true
                };
                td.Buttons.Add(TaskDialogButton.Close);
                td.ShowModal();
            }

            return VSConstants.S_OK;
        }
    }
}
