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

        public override int DebugLaunch(uint flags) {
            
            if (_project.ShouldWarnOnLaunch) {
                var pyService = ((IServiceProvider)_project).GetPythonToolsService();
                if (pyService.DebuggerOptions.PromptBeforeRunningWithBuildError) {
                    var res = new StartWithErrorsDialog(pyService).ShowDialog();
                    if (res == DialogResult.No) {
                        return VSConstants.S_OK;
                    }
                }
            }

            try {
                return base.DebugLaunch(flags);
            } catch (NoInterpretersException) {
                PythonToolsPackage.OpenNoInterpretersHelpPage(ProjectMgr.Site);
                return VSConstants.S_OK;
            }
        }
    }
}
