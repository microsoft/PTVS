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
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Hpc {
    [Export(typeof(IPythonLauncherProvider))]
    class HpcLauncherProvider : IPythonLauncherProvider {
        private readonly PythonToolsService _pyService;

        [ImportingConstructor]
        public HpcLauncherProvider([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider) {
            _pyService = (PythonToolsService)serviceProvider.GetService(typeof(PythonToolsService));
        }

        #region IPythonLauncherProvider Members

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new ClusterOptionsControl(properties);
        }

        public string Name {
            get {
                return "MPI Cluster launcher";
            }
        }

        public string Description {
            get {
                return "Launches Python programs on a Windows HPC cluster using mpiexec.  Enables cluster deployment and debugging.";
            }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            return new HpcLauncher(_pyService, project);
        }

        #endregion
    }
}
