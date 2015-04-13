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
using System.Linq;
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
            var projects = _serviceProvider.GetDTE().ActiveSolutionProjects as System.Collections.IEnumerable;

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
