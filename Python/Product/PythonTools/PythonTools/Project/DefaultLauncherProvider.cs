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
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudioTools.Project;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Project {
    [Export(typeof(IPythonLauncherProvider))]
    class DefaultLauncherProvider : IPythonLauncherProvider2 {
        private readonly IServiceProvider _serviceProvider;
        private readonly PythonToolsService _pyService;
        internal const string DefaultLauncherName = "Standard Python launcher";

        [ImportingConstructor]
        public DefaultLauncherProvider([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
        }

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new DefaultPythonLauncherOptions(properties);
        }

        public string Name {
            get {
                return DefaultLauncherName;
            }
        }

        public string LocalizedName {
            get {
                return SR.GetString(SR.DefaultLauncherName);
            }
        }

        public string Description {
            get {
                return SR.GetString(SR.DefaultLauncherDescription);
            }
        }

        public int SortPriority {
            get {
                return 0;
            }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            return new DefaultPythonLauncher(_serviceProvider, _pyService, project);
        }
    }
}
