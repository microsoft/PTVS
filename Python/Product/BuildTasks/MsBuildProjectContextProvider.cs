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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.BuildTasks {
    [Export(typeof(IProjectContextProvider))]
    [Export(typeof(MsBuildProjectContextProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class MsBuildProjectContextProvider : IProjectContextProvider {
        private readonly HashSet<object> _contexts = new HashSet<object>();

        public void AddContext(object context) {
            bool added = false;
            lock (_contexts) {
                added = _contexts.Add(context);
            }
            if (added) {
                ProjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        internal void RemoveContext(object context) {
            bool removed = false;
            lock (_contexts) {
                removed = _contexts.Remove(context);
            }
            if (removed) {
                ProjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void InterpreterLoaded(object context, InterpreterConfiguration factory) {
        }

        public void InterpreterUnloaded(object context, InterpreterConfiguration factory) {
        }

        public IEnumerable<object> Projects {
            get {
                return _contexts.ToArray();
            }
        }

        public event EventHandler ProjectsChanged;

        public event EventHandler<ProjectChangedEventArgs> ProjectChanged {
            add {

            }
            remove {
            }
        }
    }
}
