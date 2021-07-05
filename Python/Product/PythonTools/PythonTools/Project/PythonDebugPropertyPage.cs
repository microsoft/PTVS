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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "object is owned by VS")]
    [Guid(PythonConstants.DebugPropertyPageGuid)]
    class PythonDebugPropertyPage : CommonPropertyPage {
        private readonly PythonDebugPropertyPageControl _control;

        public PythonDebugPropertyPage() {
            _control = new PythonDebugPropertyPageControl(this);
        }

        public override Control Control {
            get {
                return _control;
            }
        }

        internal override CommonProjectNode Project {
            get {
                return base.Project;
            }
            set {
                if (value == null && base.Project != null) {
                    ((PythonProjectNode)base.Project).DebugPropertyPage = null;
                }
                base.Project = value;
                if (value != null) {
                    ((PythonProjectNode)value).DebugPropertyPage = this;
                }
            }
        }

        public override string Name {
            get { return Strings.PythonDebugPropertyPageLabel; }
        }

        public override void Apply() {
            Project.SetProjectProperty(PythonConstants.LaunchProvider, _control.CurrentLauncher);
            _control.SaveSettings();

            IsDirty = false;
        }

        public override void LoadSettings() {
            Loading = true;
            try {
                _control.LoadSettings();
            } finally {
                Loading = false;
            }
        }
    }
}
