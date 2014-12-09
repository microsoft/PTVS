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
using Microsoft.PythonTools;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.IronPythonTools.Debugger {
    [Export(typeof(IPythonLauncherProvider))]
    class IronPythonLauncherProvider : IPythonLauncherProvider {
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public IronPythonLauncherProvider([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = (PythonToolsService)serviceProvider.GetService(typeof(PythonToolsService));
        }

        #region IPythonLauncherProvider Members

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new IronPythonLauncherOptions(properties);
        }

        public string Name {
            get { return "IronPython (.NET) launcher"; }
        }

        public string Description {
            get {
                return "Launches IronPython scripts using the .NET debugger.  This enables debugging both IronPython code as well as other .NET code such as C# or VB.NET.";
            }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            return new IronPythonLauncher(_serviceProvider, _pyService, project);
        }

        #endregion
    }
}
