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

namespace Microsoft.PythonTools.Project {
    [Export(typeof(IPythonLauncherProvider))]
    class DefaultLauncherProvider : IPythonLauncherProvider {
        internal const string DefaultLauncherDescription = "Standard Python launcher";

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new DefaultPythonLauncherOptions(properties);
        }

        public string Name {
            get {
                return DefaultLauncherDescription;
            }
        }

        public string Description {
            get {
                return "Launches and debugs Python programs.  This is the default.";
            }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            return new DefaultPythonLauncher(project);
        }
    }
}
