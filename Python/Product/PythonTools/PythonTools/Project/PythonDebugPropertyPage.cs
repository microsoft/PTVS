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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
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
            get { return "Debug"; }
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
