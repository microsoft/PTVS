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
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using SR = Microsoft.PythonTools.Project.SR;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to start script from a document tab or document window.
    /// </summary>
    abstract class StartScriptCommand : Command {
        private readonly System.IServiceProvider _serviceProvider;

        public StartScriptCommand(System.IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        internal class LaunchFileProperties : IProjectLaunchProperties {
            private readonly string _arguments, _workingDir;
            private readonly Dictionary<string, string> _environment;

            public LaunchFileProperties(string arguments, string workingDir, string searchPathVar, string searchPath) {
                _arguments = arguments;
                _workingDir = workingDir;
                _environment = new Dictionary<string, string> {
                    { searchPathVar, searchPath }
                };
            }

            public string GetArguments() {
                return _arguments;
            }

            public string GetWorkingDirectory() {
                return _workingDir;
            }

            public IDictionary<string, string> GetEnvironment(bool includeSearchPaths) {
                return includeSearchPaths ? _environment : new Dictionary<string, string>();
            }
        }

        public override void DoCommand(object sender, EventArgs args) {
            if (!Utilities.SaveDirtyFiles()) {
                // Abort
                return;
            }

            // Launch with project context if there is one and it contains the active document
            // Fallback to using default python project
            var file = CommonPackage.GetActiveTextView(_serviceProvider).GetFilePath();
            var sln = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
            IEnumerable projects;
            try {
                projects = _serviceProvider.GetDTE().ActiveSolutionProjects as IEnumerable;
            } catch (COMException) {
                // ActiveSolutionProjects can fail if Solution Explorer has not been loaded
                projects = Enumerable.Empty<EnvDTE.Project>();
            }

            var pythonProject = (projects == null ? null : projects.OfType<EnvDTE.Project>()
                .Select(p => p.GetPythonProject())
                .FirstOrDefault(p => p != null && p.FindNodeByFullPath(file) != null) as IPythonProject)
                ?? new DefaultPythonProject(_serviceProvider, file);

            var launcher = PythonToolsPackage.GetLauncher(_serviceProvider, pythonProject);
            try {
                var launcher2 = launcher as IProjectLauncher2;
                if (launcher2 != null) {
                    launcher2.LaunchFile(
                        file,
                        CommandId == CommonConstants.StartDebuggingCmdId,
                        new LaunchFileProperties(
                            null,
                            CommonUtils.GetParent(file),
                            pythonProject.GetInterpreterFactory().Configuration.PathEnvironmentVariable,
                            pythonProject.GetWorkingDirectory()
                        )
                    );
                } else {
                    launcher.LaunchFile(file, CommandId == CommonConstants.StartDebuggingCmdId);
                }
            } catch (MissingInterpreterException ex) {
                MessageBox.Show(ex.Message, SR.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            } catch (NoInterpretersException ex) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(_serviceProvider, ex.HelpPage);
            }
        }

        public override int? EditFilterQueryStatus(ref VisualStudio.OLE.Interop.OLECMD cmd, IntPtr pCmdText) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);
            if (activeView != null && activeView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            } else {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }

            return VSConstants.S_OK;
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = false;
                    ((OleMenuCommand)sender).Supported = false;
                };
            }
        }
    }

    class StartWithoutDebuggingCommand : StartScriptCommand {
        public StartWithoutDebuggingCommand(System.IServiceProvider serviceProvider)
            : base(serviceProvider) {
        }

        public override int CommandId {
            get { return (int)CommonConstants.StartWithoutDebuggingCmdId; }
        }
    }

    class StartDebuggingCommand : StartScriptCommand {
        public StartDebuggingCommand(System.IServiceProvider serviceProvider)
            : base(serviceProvider) {
        }

        public override int CommandId {
            get { return (int)CommonConstants.StartDebuggingCmdId; }
        }
    }
}
