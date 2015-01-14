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
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    [Export(typeof(IPythonLauncherProvider))]
    class PythonWebLauncherProvider : IPythonLauncherProvider2 {
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonWebLauncherProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
        }

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new PythonWebLauncherOptions(properties);
        }

        public string Name {
            get {
                return PythonConstants.WebLauncherName;
            }
        }

        public string LocalizedName {
            get {
                return SR.GetString(SR.PythonWebLauncherName);
            }
        }

        public string Description {
            get {
                return SR.GetString(SR.PythonWebLauncherDescription);
            }
        }

        public int SortPriority {
            get {
                return 100;
            }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            return new PythonWebLauncher(_serviceProvider, _pyService, project);
        }
    }
}
