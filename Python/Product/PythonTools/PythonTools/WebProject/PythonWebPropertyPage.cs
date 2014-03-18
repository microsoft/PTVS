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
using SR = Microsoft.PythonTools.Project.SR;

namespace Microsoft.PythonTools.Project.Web {
    [Guid(PythonConstants.WebPropertyPageGuid)]
    class PythonWebPropertyPage : CommonPropertyPage {
        private readonly PythonWebPropertyPageControl _control;

        public const string StaticUriPatternSetting = "StaticUriPattern";
        public const string WsgiHandlerSetting = "PythonWsgiHandler";

        public PythonWebPropertyPage() {
            _control = new PythonWebPropertyPageControl(this);
        }

        public override Control Control {
            get { return _control; }
        }

        public override void Apply() {
            SetProjectProperty(StaticUriPatternSetting, _control.StaticUriPattern);
            SetProjectProperty(WsgiHandlerSetting, _control.WsgiHandler);
            IsDirty = false;
        }

        public override void LoadSettings() {
            Loading = true;
            try {
                _control.StaticUriPattern = GetProjectProperty(StaticUriPatternSetting);
                _control.WsgiHandler = GetProjectProperty(WsgiHandlerSetting);
                IsDirty = false;
            } finally {
                Loading = false;
            }
        }

        public override string Name {
            get { return SR.GetString(SR.PythonWebPropertyPageTitle); }
        }
    }
}
