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
using System.Linq;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Django.Debugger {
    [Export(typeof(IPythonLauncherProvider))]
    class DjangoLauncherProvider : IPythonLauncherProvider {
        internal readonly IEnumerable<Lazy<IPythonLauncherProvider>> _providers;

        [ImportingConstructor]
        public DjangoLauncherProvider([ImportMany]IEnumerable<Lazy<IPythonLauncherProvider>> allProviders) {
            _providers = allProviders;
        }

        #region IPythonLauncherProvider Members

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new DjangoLauncherOptions(properties);
        }

        public string Name {
            get { return "Django launcher"; }
        }

        public string Description {
            get {
                return "Launches Django web sites using the Python debugger.  This enables launching and starting a web browser automatically.  It launches using manage.py and passes the runserver flag as well as additional options to configure the port and other settings.";
            }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            var webLauncher = _providers.FirstOrDefault(p => p.Value.Name == PythonConstants.WebLauncherName);

            if (webLauncher == null) {
                throw new InvalidOperationException("Cannot find Python Web launcher");
            }

            return webLauncher.Value.CreateLauncher(project);
        }

        #endregion
    }
}
