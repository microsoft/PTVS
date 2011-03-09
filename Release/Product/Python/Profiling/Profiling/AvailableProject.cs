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

namespace Microsoft.PythonTools.Profiling {
    public class AvailableProject {
        private readonly EnvDTE.Project _project;

        public AvailableProject(EnvDTE.Project project) {
            _project = project;
        }

        public string Name {
            get {
                return _project.Name;
            }
        }

        public Guid Guid {
            get {
                return new Guid(_project.Properties.Item("Guid").Value as string);
            }
        }

        public override string ToString() {
            return Name;
        }
    }
}

